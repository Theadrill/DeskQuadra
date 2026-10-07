# Plano — Resiliência de Desktop, Boot Seguro & Safety Net — DeskQuadra

> **LEITURA OBRIGATÓRIA ANTES DE QUALQUER LINHA DE CÓDIGO:**
> 1. Leia obrigatoriamente `docs/BRAINSTORMING.md` (com foco especial nas Sessões 1, 13, 28 e 29) e respeite rigorosamente todas as decisões estratégicas e regras arquiteturais já consolidadas.
> 2. Consulte e siga as regras de engenharia em `docs/PLANO_DE_IMPLEMENTACAO.md` e a auditoria viva em `docs/AUDITORIA_REUSO.md`.
> 3. **Uso Mandatório das Skills:** Utilize ativamente as skills especializadas disponíveis no diretório `.agent/skills/` deste repositório para guiar cada etapa de código, revisão, interop Win32, testes e documentação.

---

## 1. Objetivo & Diagnóstico do Problema

### O Problema Diagnosticado
Anteriormente, caso o usuário instalasse o DeskQuadra, utilizasse no dia a dia e desligasse o computador (ou se o processo fosse encerrado inesperadamente), no boot seguinte o desktop do Windows podia aparecer completamente vazio (ícones nativos ocultos). O usuário, muitas vezes com pressa para abrir um aplicativo antes do carregamento completo do sistema ou em máquinas mais lentas, ficava incapacitado de ver ou interagir com seus arquivos da área de trabalho até que o processo do DeskQuadra entrasse em execução e desenhasse as Quadras.

Pior ainda: no cenário catastrófico em que o usuário (ou uma ferramenta externa) abrisse o Gerenciador de Tarefas do Windows e finalizasse **tanto o `DeskQuadra.exe` quanto o `DeskQuadra.Guardian.exe` simultaneamente**, os handlers em memória não chegavam a rodar, o Guardian morria antes de agir e o usuário ficava com a área de trabalho vazia sem saber como recuperar seus ícones.

### A Mudança de Paradigma (O Paradigma do Desktop Seguro)
O DeskQuadra adota a mesma premissa de confiabilidade do Windows Shell:
1. **No Boot do Windows:** O Explorer SEMPRE inicia com os ícones visíveis por padrão (`HideIcons = 0`, `SysListView32` visível). O usuário pode clicar e abrir qualquer atalho imediatamente.
2. **Ao Carregar o DeskQuadra:** O aplicativo carrega suavemente, oculta os ícones nativos via Win32 `ShowWindow(SW_HIDE)` na memória de janelas e renderiza as Quadras organizadas.
3. **Ao Encerrar (Qualquer Motivo):** Seja saída graciosa ("Sair" na bandeja), logoff/shutdown do Windows, crash inesperado ou kill forçado, a visibilidade nativa dos ícones do desktop **DEVE ser restaurada instantaneamente** via Win32 `ShowWindow(SW_SHOW)`.
4. **Proibição Estrita no Registro:** É expressamente **PROIBIDO** persistir `HideIcons = 1` no registro do Windows (`HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\HideIcons`). O DeskQuadra sempre sanitiza e assegura que o registro permaneça com `HideIcons = 0`.
5. **No Próximo Boot:** O desktop sempre acorda com seus ícones visíveis. O usuário **NUNCA** fica refém de uma tela vazia.

---

## 2. Regras Permanentes do Projeto (Valem para Todas as Fases)

- **Clean-Room Design:** Terminologia estritamente proprietária: **"Quadra"**. Tolerância zero para nomes ou marcas de softwares concorrentes comerciais no código-fonte, nomes de arquivos, testes ou documentação.
- **Stack & Eficiência:** C# / .NET 8 LTS + WPF. Alocação de memória restrita (~30MB RAM base), sem dependências pesadas externas e sem engines Chromium/WebView2.
- **Clean Architecture & Isolamento:**
  - `Core`: Lógica de domínio pura e contratos.
  - `Application`: Orquestração de casos de uso e serviços.
  - `Infrastructure.WindowsShell`: Todo P/Invoke Win32 (`user32.dll`, `kernel32.dll`, `TaskScheduler`, `WorkerW`).
  - `Infrastructure.Persistence`: Armazenamento de configurações e logs de diagnóstico.
  - `UI.Wpf`: Telas, viewmodels, estilos e feedback ao usuário.
- **Idioma & Localização:** Todo texto visível ao usuário final deve residir obrigatoriamente em `Strings.resx` (pt-BR default). Proibido texto hardcoded visível em C#/XAML. Logs de diagnóstico técnicos internos permanecem no molde padrão (`safety-restore.log` em `%APPDATA%\DeskQuadra`).
- **Filosofia de Reuso:** Se já existe uma implementação estável (`NativeDesktopIconService`, `NativeMethods`, etc.), REUSE. Extrair helper/serviço compartilhado na 2ª repetição, nunca antes.

---

## 3. Matriz de Skills Mandatórias por Fase (`.agent/skills/`)

Conforme a governança de engenharia estabelecida na Sessão 1 do `docs/BRAINSTORMING.md`, o desenvolvimento de cada fase deve ser guiado pelas skills do repositório:

| Fase | Foco Técnico | Skills Obrigatórias |
|---|---|---|
| **Fase 1** | Handlers de término, `SessionEnding`, sanitização de registro e proteção de SafeMode | `dotnet-pinvoke`, `wpf-windows-desktop`, `coding-guidelines`, `csharp-refactoring`, `run-tests` |
| **Fase 2** | Novo executável leve `DeskQuadra.Restorer`, auto-restart com notificação e atalho Menu Iniciar | `dotnet-pinvoke`, `msbuild-modernization`, `coding-guidelines`, `csharp-refactoring`, `run-tests`, `assertion-quality` |
| **Fase 3** | Windows Task Scheduler Safety Net (verificação a cada 1 min) | `dotnet-pinvoke`, `wpf-windows-desktop`, `analyzing-dotnet-performance`, `coding-guidelines` |
| **Fase 4** | Windows Service dedicado (30s) e Shell Extension de contexto para instalação completa (`!isPortable`) | `dotnet-pinvoke`, `msbuild-modernization`, `coding-guidelines`, `docs-writer` |

---

## 4. Arquitetura de Defesa em Profundidade (As Camadas de Proteção)

Para garantir que o desktop nunca fique vazio sob hipótese alguma, o DeskQuadra opera com múltiplas camadas complementares:

```mermaid
flowchart TD
    subgraph Camadas_Em_Processo["1. Em-Processo (Tempo: 0ms)"]
        A["AppDeskQuadra Rodando"] --> B["Crash / Unhandled Exception"]
        B --> C["Exception Handlers Globais (AppDomain + Dispatcher)"]
        C --> R1["ShowWindow(SW_SHOW) Imediato"]
        
        A --> D["Logoff / Shutdown do Windows"]
        D --> E["SessionEnding Event (SystemEvents + Application)"]
        E --> R2["Salva Quadras + ShowWindow(SW_SHOW)"]
    end

    subgraph Camadas_Fora_De_Processo["2. Fora de Processo (Watchdogs & Safety Net)"]
        A --> F["Processo Morre / Kill no Task Manager"]
        F --> G{"Guardian Vivo?"}
        G -- "Sim (5s - 10s)" --> H["Guardian detecta queda do Pai"]
        H --> R3["ShowWindow(SW_SHOW) + Avisa"]
        
        G -- "Não (Kill Simultâneo)" --> I["Ambos Mortos: Desktop Vazio"]
        I --> J["Task Scheduler dispara DeskQuadra.Restorer.exe (a cada 1 min)"]
        J --> K{"SysListView32 Oculto & App Ausente?"}
        K -- "Sim" --> L["Restorer executa ShowWindow(SW_SHOW)"]
        L --> M["Auto-Restart DeskQuadra com flag --recovered"]
        M --> N["DeskQuadra exibe Notificação Informativa"]
        K -- "Não" --> O["Sai em < 5ms sem efeito"]
    end

    subgraph Camadas_Instalacao_Completa["3. Camadas Adicionais (!isPortable)"]
        I --> P["Windows Service Dedicado (a cada 30s)"]
        P --> R4["ShowWindow(SW_SHOW) via Desktop Handle"]
        
        I --> Q["Botão Direito no Desktop: Shell Extension"]
        Q --> R5["Clique em 'Restaurar Ícones do Desktop'"]
    end

    subgraph Camada_Manual["4. Camada de Controle Manual"]
        I --> S["Menu Iniciar: Atalho 'Restaurar Ícones do Desktop'"]
        S --> R6["Executa Restorer --force"]
    end
```

### Detalhamento das Camadas e Decisões Tomadas:
1. **Camada 1 — Exception Handlers (0ms):** Invocação de método estático seguro `NativeDesktopIconService.EmergencyRestoreIcons()` em `AppDomain.CurrentDomain.UnhandledException` e `DispatcherUnhandledException`, sem depender de instâncias de DI que possam ser nulas.
2. **Camada 2 — Evento `SessionEnding` (0ms):** Captura tanto no `System.Windows.Application.SessionEnding` quanto em `Microsoft.Win32.SystemEvents.SessionEnding`. Salva as Quadras ativas e restaura imediatamente os ícones nativos antes que o Windows encerre o subsistema de janelas.
3. **Camada 3 — Processo Guardião (`DeskQuadra.Guardian`, 5-10s):** Monitora o PID do processo pai. Se o processo pai for encerrado abruptamente, restaura os ícones. Atalho de emergência `Ctrl + Shift + Alt + Q` existente mantido.
4. **Camada 4 — Windows Service Dedicado (30s) [Opção A confirmada para `!isPortable`]:** Serviço nativo leve do Windows que verifica a cada 30 segundos se nenhum processo do DeskQuadra está rodando com ícones invisíveis. Presente apenas na instalação completa (`!isPortable`).
5. **Camada 5 — Task Scheduler Safety Net (1 min) + `DeskQuadra.Restorer.exe` [Opção A + Opção B confirmadas]:**
   - Agendamento nativo a cada 1 minuto (Opção 3 confirmada: 1 min).
   - Executa `DeskQuadra.Restorer.exe` (executável leve C# de ~25KB, sem dependência de script PowerShell que possa ser bloqueado por antivírus ou política de execução).
   - Comportamento de Recuperação (Opção B confirmada): O Restorer restaura os ícones e reinicia o `DeskQuadra.exe` automaticamente com a flag `--recovered`. Ao subir, o app exibe uma notificação/toast informativa explicando a recuperação.
6. **Camada 6 — Atalho no Menu Iniciar:** Criado em `%APPDATA%\Microsoft\Windows\Start Menu\Programs\DeskQuadra\Restaurar Ícones do Desktop.lnk`, permitindo ao usuário abrir o Menu Iniciar, digitar "restaurar" e recuperar seus ícones instantaneamente.
7. **Camada 7 — Shell Extension no Menu de Contexto [Confirmado para `!isPortable`]:** Opção no clique com botão direito no papel de parede: "Restaurar Ícones do Desktop (DeskQuadra)".
8. **Camada 8 — Detecção de Modo Seguro (`SafeMode`):** Se o Windows iniciar em Modo Seguro (`SystemInformation.BootMode != Normal`), restaura os ícones, exibe aviso informativo e encerra imediatamente sem subir o app completo.
9. **Camada 9 — Sanitização de Registro & Boot Forçado:** Verificação contínua para assegurar que `HideIcons` seja sempre `0` no registro e tratamento de reinício pós-queda de energia.

---

## 5. Fases de Execução Testáveis pelo PO (Goal-Driven)

As entregas estão estruturadas em fatias verticais coesas, permitindo que o PO teste cenários reais no Windows ao final de cada fase, sem sobrecarga de validações intermediárias minúsculas.

---

### Fase 1 — Núcleo de Resiliência no Ciclo de Vida do App
**Objetivo:** Blindar o processo principal contra crashes, desligamentos e estados inconsistentes do registro, garantindo que o app nunca deixe os ícones ocultos ao sair de forma graciosa ou com falha.

#### O que será feito:
1. **Método Estático de Emergência:** Criar `NativeDesktopIconService.EmergencyRestoreIcons()` totalmente estático e thread-safe, com busca direta dos handles Win32 `SHELLDLL_DefView` e `SysListView32` e chamada `ShowWindow(SW_SHOW)`, sem depender de instâncias ou DI.
2. **Handlers Globais Robustos:** Conectar `AppDomain.CurrentDomain.UnhandledException` e `DispatcherUnhandledException` diretamente ao método estático de emergência.
3. **Tratamento de `SessionEnding`:**
   - Adicionar subscrição em `SystemEvents.SessionEnding` e override de `Application.OnSessionEnding`.
   - Executar persistência rápida de layout (`SaveNowAsync`) e restauração imediata dos ícones nativos (`ShowWindow(SW_SHOW)`).
4. **Proteção de Modo Seguro (`SafeModeGuard`):**
   - No início de `Program.Main`, verificar `SystemInformation.BootMode`.
   - Se diferente de `Normal`, chamar restauração estática dos ícones, exibir mensagem informativa leve e sair (`return`) antes de inicializar o WPF.
5. **Sanitização da Chave de Registro `HideIcons`:**
   - Assegurar que nenhuma rotina grave `HideIcons = 1` no registro.
   - Criar rotina de sanitização na inicialização e no encerramento que garante `HideIcons = 0` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`.
6. **Detecção de Desligamento Forçado / Queda de Energia:**
   - Gravar timestamp de saída graciosa em `%APPDATA%\DeskQuadra\last_shutdown.timestamp`.
   - Se no boot o tempo decorrido desde o último boot do Windows indicar shutdown abrupto (sem timestamp de saída limpa), garantir que a janela nativa de ícones seja forçada para visível antes de reconfigurar o desktop.

#### Critério de Aceite do PO (Validação no Windows):
- `dotnet test` passando 100% verde e zero avisos de compilação.
- **Teste 1 (Saída Graciosa / Logoff):** Ao fechar o app via bandeja ou solicitar logoff/reinicialização do Windows, os ícones nativos reaparecem imediatamente no desktop.
- **Teste 2 (Simulação de Crash):** Ao provocar uma exceção forçada de teste no app, o handler captura a falha e os ícones originais do Windows reaparecem instantaneamente na tela.
- **Teste 3 (Registro Limpo):** A chave de registro `HideIcons` permanece rigorosamente com valor `0`.

---

### Fase 2 — Executável Dedicado `DeskQuadra.Restorer` & Atalho no Menu Iniciar
**Objetivo:** Criar o módulo executável dedicado e independente para restauração emergencial e auto-recuperação do app, acompanhado de atalho de fácil acesso no Menu Iniciar.

#### O que será feito:
1. **Novo Projeto `DeskQuadra.Restorer`:**
   - Projeto console/WinForms ultraleve (.NET 8 LTS, ~25KB compilado, sem janelas visíveis).
   - Lógica do processo:
     - Argumento `--force`: Restaura imediatamente sem checar processos ativos e sai.
     - Execução padrão:
       1. Verifica se `DeskQuadra.UI.Wpf` está rodando. Se sim, encerra sem ação.
       2. Verifica se `DeskQuadra.Guardian` está rodando. Se sim, encerra sem ação (o Guardian cuida).
       3. Se NENHUM está rodando E a janela de ícones nativa está oculta (`!IsWindowVisible`):
          - Restaura a visibilidade via Win32 `ShowWindow(SW_SHOW)`.
          - Registra log de diagnóstico em `%APPDATA%\DeskQuadra\safety-restore.log`.
          - Dispara o `DeskQuadra.UI.Wpf.exe` passando o parâmetro `--recovered`.
2. **Notificação de Recuperação no App Principal:**
   - No `App.xaml.cs`, tratar o argumento `--recovered`: exibir toast/notificação moderna informativa: *"O DeskQuadra foi reiniciado automaticamente após uma interrupção inesperada e seus ícones foram preservados."*
3. **Criação do Atalho no Menu Iniciar:**
   - Serviço ou rotina de inicialização cria o atalho `Restaurar Ícones do Desktop.lnk` em `%APPDATA%\Microsoft\Windows\Start Menu\Programs\DeskQuadra\`.
   - O atalho aponta para `DeskQuadra.Restorer.exe --force` com ícone de restauração.
4. **Configuração de Build e Cópia:**
   - Atualizar `DeskQuadra.UI.Wpf.csproj` para compilar e copiar `DeskQuadra.Restorer.exe` para as pastas de saída (`bin` e `publish`), idêntico ao padrão já consolidado do `DeskQuadra.Guardian`.

#### Critério de Aceite do PO (Validação no Windows):
- **Teste 1 (Recuperação Manual via Menu Iniciar):** Com o app rodando ou fechado, pressionar a tecla Windows, digitar "restaurar", clicar no atalho -> os ícones nativos aparecem na hora.
- **Teste 2 (Comportamento do Restorer Isolado):** Finalizar o DeskQuadra no Gerenciador de Tarefas e rodar o `DeskQuadra.Restorer.exe` diretamente via terminal -> os ícones reaparecem e o DeskQuadra é reaberto exibindo a notificação de restauração.

---

### Fase 3 — Task Scheduler Safety Net (Monitoramento a cada 1 Minuto)
**Objetivo:** Automatizar o monitoramento em segundo plano sem novos processos residentes pesados, garantindo recuperação em no máximo 1 minuto mesmo se o usuário matar DeskQuadra e Guardian simultaneamente.

#### O que será feito:
1. **Serviço de Agendamento Nativo (`SafetyTaskSchedulerService`):**
   - Utilizar a API COM `Schedule.Service` (reusando as regras estabelecidas na Sessão 28 do Brainstorming).
   - Criar e gerenciar a tarefa agendada:
     - **Nome:** `DeskQuadra Safety Restore`
     - **Gatilho:** `AtLogon` com repetição a cada 1 minuto indefinidamente (`PT1M`).
     - **Ação:** Executar `DeskQuadra.Restorer.exe` (modo silencioso padrão).
     - **Condições:** `DisallowStartIfOnBatteries = false` (funciona em laptops e portáteis), `ExecutionTimeLimit = PT1M`, sem privilégios elevados (token de usuário normal).
2. **Suporte nos Modos Portátil e Instalador:**
   - No modo portátil, o app auto-registra a tarefa agendada apontando para o executável em sua pasta de execução.
   - Respeita configuração do usuário (se o usuário desativar inicialização automática, a tarefa correspondente é ajustada de acordo).

#### Critério de Aceite do PO (Validação no Windows):
- **Teste Extremo do Pior Caso:**
  1. Abrir o Gerenciador de Tarefas do Windows.
  2. Selecionar `DeskQuadra.exe` e `DeskQuadra.Guardian.exe`.
  3. Clicar em "Finalizar tarefa" em AMBOS simultaneamente.
  4. O desktop fica momentaneamente vazio.
  5. Aguardar até 60 segundos sem tocar em nada.
  6. **Resultado:** A tarefa agendada dispara o `DeskQuadra.Restorer.exe`, os ícones do desktop voltam e o DeskQuadra reabre com a notificação explicativa.

---

### Fase 4 — Blindagem da Instalação Completa (Windows Service + Shell Extension)
**Objetivo:** Adicionar as camadas de proteção máxima exclusivas para o modo instalado (`!isPortable`), aproveitando os privilégios do instalador para redundância via Windows Service e menu de contexto no papel de parede.

#### O que será feito:
1. **Windows Service Dedicado (`DeskQuadra.Service`):**
   - Serviço em C# (.NET 8 LTS) configurado para rodar como serviço do Windows.
   - Timer em background de 30 segundos: verifica se os processos interativos do usuário estão ausentes e se a janela de ícones do desktop interativo está oculta; em caso positivo, dispara a restauração.
   - Condicionado estritamente a `!isPortable` (nunca ativado no modo portátil).
2. **Shell Extension no Menu de Contexto do Desktop:**
   - Registro em `HKCU\Software\Classes\DesktopBackground\Shell\DeskQuadraRestore`:
     - Título: *"Restaurar Ícones do Desktop (DeskQuadra)"*
     - Comando: Invocação de `DeskQuadra.Restorer.exe --force`
     - Exibição de ícone dedicado.
   - Condicionado estritamente a `!isPortable`.
3. **Atualização do Script Inno Setup (`installer/DeskQuadra.iss`):**
   - Adicionar arquivos de `DeskQuadra.Restorer` e `DeskQuadra.Service` no payload.
   - Registrar/iniciar o serviço do Windows no fim da instalação.
   - Adicionar o atalho de emergência na pasta do Menu Iniciar.
   - Limpeza completa e desregistro do serviço e das chaves de registro na desinstalação.

#### Critério de Aceite do PO (Validação no Windows):
- **Teste de Menu de Contexto:** Clicar com o botão direito em uma área livre do papel de parede do Windows -> a opção de restauração aparece e funciona perfeitamente ao ser clicada.
- **Teste de Redundância com o Serviço:** Matar os processos na instalação completa -> o serviço restaura os ícones em cerca de 30 segundos (antes do ciclo de 1 minuto do Task Scheduler).
- **Teste de Portabilidade:** Executar o executável portátil -> confirma que nenhuma chave do Shell Extension ou Serviço é instalada no sistema operacional.

---

## 6. Riscos Conhecidos & Mitigações

- **Risco 1: Falso Positivo de Antivírus em Scripts PowerShell:**
  - *Mitigação:* Descartamos completamente o uso de scripts `.ps1`. O `DeskQuadra.Restorer.exe` é um binário C# compilado, assinado e enxuto (~25KB), executando chamadas Win32 padronizadas.
- **Risco 2: Múltiplas Instâncias de Restauração Concorrentes:**
  - *Mitigação:* O `DeskQuadra.Restorer.exe` utiliza verificação rápida de instâncias e mutex local de execução para não disputar recursos caso um ciclo coincida com outro.
- **Risco 3: Conflito de Restauração com o Guardian Ativo:**
  - *Mitigação:* O `DeskQuadra.Restorer` só atua se `DeskQuadra.Guardian` também estiver ausente. O Guardian tem prioridade de ação.
- **Risco 4: Bloqueio de Permissões no Modo Portátil:**
  - *Mitigação:* As tarefas do Task Scheduler e atalhos do Menu Iniciar no modo portátil rodam estritamente no escopo de usuário (`HKCU` e token interativo normal), nunca exigindo elevação de Administrador (UAC).

---

## 7. Diário de Bordo & Status das Fases

- [ ] **Fase 1 — Núcleo de Resiliência no Ciclo de Vida do App** (Pendente)
- [ ] **Fase 2 — Executável Dedicado `DeskQuadra.Restorer` & Atalho no Menu Iniciar** (Pendente)
- [ ] **Fase 3 — Task Scheduler Safety Net (Monitoramento a cada 1 Minuto)** (Pendente)
- [ ] **Fase 4 — Blindagem da Instalação Completa (Windows Service + Shell Extension)** (Pendente)
