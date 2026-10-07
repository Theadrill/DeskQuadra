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

### Esclarecimento Arquitetural: Registro do Windows vs. Ocultação em Memória (Volátil)
- **Por que falamos de "não ter persistência de ícones escondidos"?**
  - No Windows Explorer, a opção "Mostrar ícones da área de trabalho" do menu de contexto nativo grava `HideIcons = 1` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\HideIcons`. Essa chave é uma persistência do sistema: se gravada como `1`, toda vez que o Windows liga, o Explorer lê o registro e omite os ícones desde o boot.
  - O DeskQuadra **NUNCA usa essa chave para esconder ícones**. Pelo contrário: ele assegura que essa chave esteja sempre em `0` (visível).
- **Como o DeskQuadra esconde os ícones sem persistir?**
  - A ocultação é feita exclusivamente em **tempo de execução (em memória RAM)** via API Win32: `ShowWindow(hDesktopListView, SW_HIDE)`.
  - Esta alteração afeta apenas a janela em exibição no momento. Ela **não grava nada em disco nem no registro**.
  - Quando o Windows é desligado ou reiniciado, a memória é reciclada. No boot seguinte, o Explorer cria uma nova janela de desktop do zero e, como o registro está intacto (`HideIcons = 0`), os ícones nascem **100% visíveis por padrão**.
  - O DeskQuadra só armazena em disco o arquivo `quadras.json` (layout de posições, cores e itens das Quadras). O estado de "ícones ocultos" é estritamente volátil.


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
| **Fase 1** | Handlers de término, `SessionEnding`, sanitização de registro, proteção SafeMode e correção do Guardian | `dotnet-pinvoke`, `wpf-windows-desktop`, `coding-guidelines`, `csharp-refactoring`, `run-tests` |
| **Fase 2** | Novo executável leve `DeskQuadra.Restorer`, auto-restart com notificação toast nativa e atalho Menu Iniciar | `dotnet-pinvoke`, `msbuild-modernization`, `coding-guidelines`, `csharp-refactoring`, `run-tests`, `assertion-quality` |
| **Fase 3** | Task Scheduler único de boot + Safety Net (verificação a cada 1 min) | `dotnet-pinvoke`, `wpf-windows-desktop`, `analyzing-dotnet-performance`, `coding-guidelines` |
| **Fase 4** | Windows Service (Session 0 → dispatcher) e Shell Extension de contexto para instalação completa (`!isPortable`) | `dotnet-pinvoke`, `msbuild-modernization`, `coding-guidelines`, `docs-writer` |

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
        H --> R3["ShowWindow(SW_SHOW) APENAS"]
        
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
        P --> R4["Dispatcher: CreateProcessAsUser + DeskQuadra.Restorer.exe"]
        
        I --> Q["Botão Direito no Desktop: Shell Extension (Registry Entry)"]
        Q --> R5["Clique em 'Restaurar Ícones do Desktop' → Restorer --force"]
    end

    subgraph Camada_Manual["4. Camada de Controle Manual"]
        I --> S["Menu Iniciar: Atalho 'Restaurar Ícones do Desktop'"]
        S --> R6["Executa Restorer --force"]
    end
```

### Detalhamento das Camadas e Decisões Tomadas:
1. **Camada 1 — Exception Handlers (0ms):** Invocação de método estático seguro `NativeDesktopIconService.EmergencyRestoreIcons()` em `AppDomain.CurrentDomain.UnhandledException` e `DispatcherUnhandledException`, sem depender de instâncias de DI que possam ser nulas.
2. **Camada 2 — Evento `SessionEnding` (0ms):** Captura tanto no `System.Windows.Application.SessionEnding` quanto em `Microsoft.Win32.SystemEvents.SessionEnding`. Salva as Quadras ativas e restaura imediatamente os ícones nativos antes que o Windows encerre o subsistema de janelas.
3. **Camada 3 — Processo Guardião (`DeskQuadra.Guardian`, 5-10s):** Monitora o PID do processo pai. **APENAS restaura ícones via `ShowWindow(SW_SHOW)`.** Não reinicia o app principal. Mantém hotkey de pânico `Ctrl + Shift + Alt + Q`.
4. **Camada 4 — Windows Service Dedicado (30s) [Para `!isPortable`]:** Serviço Windows que atua como dispatcher inteligente. Usa `WTSGetActiveConsoleSessionId()` + `CreateProcessAsUser()` para disparar `DeskQuadra.Restorer.exe` na sessão do usuário (Session 1+), contornando a limitação de Session 0. Presente apenas na instalação completa.
5. **Camada 5 — Task Scheduler Safety Net (1 min) + `DeskQuadra.Restorer.exe`:**
   - Agendamento nativo a cada 1 minuto via Task Scheduler.
   - Executa `DeskQuadra.Restorer.exe` (executável leve C# de ~25KB).
   - O Restorer restaura os ícones e reinicia o `DeskQuadra.UI.Wpf.exe` automaticamente com a flag `--recovered`. Ao subir, o app exibe notificação toast nativa do Windows informando a recuperação.
6. **Camada 6 — Atalho no Menu Iniciar:** Criado em `%APPDATA%\Microsoft\Windows\Start Menu\Programs\DeskQuadra\Restaurar Ícones do Desktop.lnk`, permitindo ao usuário abrir o Menu Iniciar, digitar "restaurar" e recuperar seus ícones instantaneamente.
7. **Camada 7 — Shell Extension no Menu de Contexto [Para `!isPortable`]:** Registry Entry simples em `HKCU\Software\Classes\DesktopBackground\Shell\DeskQuadraRestore` que adiciona item "Restaurar Ícones do Desktop (DeskQuadra)" ao menu de contexto do papel de parede, executando `DeskQuadra.Restorer.exe --force`.
8. **Camada 8 — Detecção de Modo Seguro (`SafeMode`):** Se o Windows iniciar em Modo Seguro (`SystemInformation.BootMode != Normal`), restaura os ícones via `MessageBoxW` Win32 (antes do WPF), exibe aviso informativo e encerra imediatamente.
9. **Camada 9 — Sanitização de Registro:** Verificação para assegurar que `HideIcons` seja sempre `0` no registro Windows, impedindo persistência de ícones ocultos entre boots.
10. **Camada 10 — Detecção de Corrupção de Dados (Sistema `.tmp` Existente):** Utiliza o mecanismo de transação dupla já implementado (`quadras.json` / `.tmp` / `.bak`) da Sessão 21 do BRAINSTORMING.md para detectar e recuperar de quedas de energia e crashes durante gravação.

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
   - Se diferente de `Normal`, chamar restauração estática dos ícones via Win32 `MessageBoxW` (antes do WPF), exibir aviso informativo e sair (`return`) antes de inicializar o WPF.
5. **Sanitização da Chave de Registro `HideIcons`:**
   - Assegurar que nenhuma rotina grave `HideIcons = 1` no registro.
   - Criar rotina de sanitização na inicialização e no encerramento que garante `HideIcons = 0` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`.

#### Critério de Aceite do PO & Roteiro Prático de Testes:
- `dotnet test` passando 100% verde e zero avisos de compilação.

##### Roteiro de Testes da Fase 1:
1. **Teste 1 (Saída Graciosa / Bandeja):**
   - *Procedimento:* Iniciar o DeskQuadra. Verificar que as Quadras abrem e os ícones nativos somem. Clicar com o botão direito no ícone da bandeja (System Tray) e selecionar **"Sair"**.
   - *Resultado esperado:* As janelas de Quadras fecham e os ícones originais do Windows reaparecem instantaneamente (< 50ms).
2. **Teste 2 (Simulação de Crash - Exception Handler):**
   - *Script Auxiliar:* `tests/helpers/test-crash.cmd`:
     ```cmd
     @echo off
     echo Disparando crash intencional no DeskQuadra...
     dotnet run --project src/DeskQuadra.UI.Wpf -- --crash-test
     ```
   - *Procedimento:* Executar `tests/helpers/test-crash.cmd`. O app forçará uma exceção não tratada na UI thread.
   - *Resultado esperado:* O handler estático `EmergencyRestoreIcons()` captura a falha e os ícones nativos reaparecem imediatamente no desktop antes do processo ser finalizado.
3. **Teste 3 (Simulação de Logoff / Shutdown - `SessionEnding`):**
   - *Script Auxiliar:* `tests/helpers/test-session-ending.ps1`:
     ```powershell
     # Envia WM_QUERYENDSESSION para a janela principal do DeskQuadra sem deslogar o PC
     $hwnd = (Get-Process -Name "DeskQuadra.UI.Wpf" -ErrorAction SilentlyContinue).MainWindowHandle
     if ($hwnd) {
         Add-Type @"
             using System;
             using System.Runtime.InteropServices;
             public class Win32 {
                 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
             }
"@
         [Win32]::SendMessage($hwnd, 0x0011, [IntPtr]::Zero, [IntPtr]::Zero) # WM_QUERYENDSESSION
         Write-Host "Sinal de encerramento de sessao enviado."
     }
     ```
   - *Resultado esperado:* O DeskQuadra salva o estado em `quadras.json` e chama `ShowWindow(SW_SHOW)`.
4. **Teste 4 (Sanitização do Registro do Windows):**
   - *Script Auxiliar:* `tests/helpers/check-registry.cmd`:
     ```cmd
     @echo off
     powershell -Command "Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'HideIcons' | Select-Object HideIcons"
     ```
   - *Resultado esperado:* O valor `HideIcons` retornado deve ser rigorosamente `0`.

---

### Fase 2 — Executável Dedicado `DeskQuadra.Restorer` & Atalho no Menu Iniciar
**Objetivo:** Criar o módulo executável dedicado e independente para restauração emergencial e auto-recuperação do app, acompanhado de atalho de fácil acesso no Menu Iniciar.

#### O que será feito:
1. **Novo Projeto `DeskQuadra.Restorer`:**
   - Projeto console/WinForms ultraleve (.NET 8 LTS, ~25KB compilado, sem janelas visíveis).
   - Lógica do processo:
     - Argumento `--force`: Restaura imediatamente sem checar processos ativos e sai.
     - Execução padrão:
       1. Verifica se `DeskQuadra.UI.Wpf` está rodando. Se sim, encerra sem ação (< 5ms).
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

#### Critério de Aceite do PO & Roteiro Prático de Testes:

##### Roteiro de Testes da Fase 2:
1. **Teste 1 (Atalho Manual de Emergência no Menu Iniciar):**
   - *Procedimento:*
     1. Com o app rodando (ícones ocultos), matar o DeskQuadra forçadamente: `taskkill /F /IM DeskQuadra.UI.Wpf.exe`.
     2. Pressionar a tecla `Windows` no teclado, digitar `restaurar` e clicar no atalho *"Restaurar Ícones do Desktop"*.
   - *Resultado esperado:* O atalho invoca `DeskQuadra.Restorer.exe --force` e os ícones nativos reaparecem imediatamente no desktop.
2. **Teste 2 (Restorer Standalone com Auto-Restart e Toast):**
   - *Script Auxiliar:* `tests/helpers/test-restorer-recovery.cmd`:
     ```cmd
     @echo off
     echo Finalizando DeskQuadra e Guardian brutalmente...
     taskkill /F /IM DeskQuadra.UI.Wpf.exe
     taskkill /F /IM DeskQuadra.Guardian.exe
     echo Executando DeskQuadra.Restorer.exe em modo verificacao...
     DeskQuadra.Restorer.exe
     ```
   - *Procedimento:* Rodar o script auxiliar no terminal.
   - *Resultado esperado:* O `Restorer.exe` detecta a ausência de processos e o desktop oculto, restaura os ícones e dispara `DeskQuadra.UI.Wpf.exe --recovered`. O app abre e exibe o toast informativo comunicando a recuperação bem-sucedida.

---

### Fase 3 — Task Scheduler Boot Único + Safety Net (Monitoramento a cada 1 Minuto)
**Objetivo:** Estabelecer Task Scheduler como ponto único de boot e automatizar monitoramento em segundo plano via Safety Net, garantindo recuperação em no máximo 1 minuto em caso de falha catastrófica.

#### Decisão Arquitetural: Por Que Task Scheduler Único?
Após análise de custo-benefício e pesquisa de melhores práticas da indústria, decidimos por um **único ponto de boot** (Task Scheduler) pelos seguintes motivos:

1. **Probabilidade de falha é ínfima:** Task Scheduler é serviço crítico do Windows. Se falhar, o sistema inteiro está comprometido.
2. **Safety Net cobre cenários extremos:** Em caso de falha do Task Scheduler, o Restorer detecta e reinicia o app em até 60 segundos.
3. **Over-engineering aumenta complexidade:** Adicionar processos de monitoramento e watchdogs de boot (Registry Run, WTS, etc.) aumenta a superfície de ataque, consome RAM e viola nossos princípios de simplicidade cirúrgica.
4. **Downtime aceitável:** 60 segundos de desktop vazio em um cenário de probabilidade < 0.01% é um trade-off aceitável vs. complexidade.

#### O que será feito:
1. **Task Scheduler de Boot (Primário):**
   - Utilizar a API COM `Schedule.Service` (reusando as regras estabelecidas na Sessão 28 do Brainstorming).
   - Criar tarefa:
     - **Nome:** `DeskQuadra Autostart`
     - **Gatilho:** `AtLogon`, `Delay = PT0S` (sem delay)
     - **Ação:** Executar `DeskQuadra.UI.Wpf.exe` (sem flags especiais)
     - **Prioridade:** 4 (alta prioridade de boot)
     - **Condições:** `DisallowStartIfOnBatteries = false`, `ExecutionTimeLimit = PT0S` (sem limite)

2. **Task Scheduler Safety Net:**
   - Criar tarefa:
     - **Nome:** `DeskQuadra Safety Restore`
     - **Gatilho:** `AtLogon` com repetição a cada 1 minuto indefinidamente (`Repetition.Interval = PT1M`, `Repetition.Duration` = indefinido)
     - **Ação:** Executar `DeskQuadra.Restorer.exe` (modo silencioso padrão)
     - **Condições:** `DisallowStartIfOnBatteries = false`, `ExecutionTimeLimit = PT1M`, sem privilégios elevados (token de usuário normal)

3. **Mutex Simples (Sem Timeout):**
   - Implementar mutex atômico em `Program.Main`:
     ```csharp
     bool createdNew;
     using var mutex = new Mutex(true, "Global\\DeskQuadra_SingleInstance", out createdNew);
     if (!createdNew) return; // Já rodando, sair silenciosamente
     ```
   - Primeira instância vence, segunda morre imediatamente (sem aguardar 8s).

4. **Suporte nos Modos Portátil e Instalador:**
   - No modo portátil, o app auto-registra ambas as tarefas agendadas apontando para o executável em sua pasta de execução.
   - Respeita configuração do usuário (se o usuário desativar inicialização automática via Settings, ambas as tarefas são removidas).

#### Critério de Aceite do PO & Roteiro Prático de Testes:

##### Roteiro de Testes da Fase 3:
1. **Teste 1 (Boot Normal - Task Scheduler Primário):**
   - *Procedimento:*
     1. Reiniciar o computador.
     2. Observar o boot do Windows.
   - *Resultado esperado:* O Task Scheduler dispara `DeskQuadra.UI.Wpf.exe` em 2-5 segundos após login. Ícones nativos são ocultados e Quadras aparecem suavemente.

2. **Teste 2 (O Pior Caso Absoluto: Kill Simultâneo de App e Guardian):**
   - *Script Auxiliar:* `tests/helpers/kill-both-pior-caso.cmd`:
     ```cmd
     @echo off
     echo ========================================================
     echo MATANDO DESKQUADRA E GUARDIAN SIMULTANEAMENTE (KILL /F)
     echo ========================================================
     taskkill /F /IM DeskQuadra.UI.Wpf.exe /IM DeskQuadra.Guardian.exe
     echo Ambos os processos estao mortos. O desktop esta vazio.
     echo Aguarde ate 60 segundos sem tocar em nada...
     ```
   - *Procedimento:*
     1. Com DeskQuadra e Guardian ativos, dê duplo clique em `tests/helpers/kill-both-pior-caso.cmd`.
     2. Os processos morrem simultaneamente. O desktop fica temporariamente vazio.
     3. **Não toque no mouse ou teclado.** Aguarde o ciclo de 60 segundos do Task Scheduler disparar.
   - *Resultado esperado:* Em até 60 segundos, a tarefa agendada executa `DeskQuadra.Restorer.exe`, que restaura a visibilidade do desktop e reinicia o DeskQuadra exibindo o toast de auto-recuperação.

3. **Teste 3 (Disparo Manual Imediato da Tarefa Agendada):**
   - *Script Auxiliar:* `tests/helpers/trigger-safety-task.cmd`:
     ```cmd
     @echo off
     powershell -Command "Start-ScheduledTask -TaskName 'DeskQuadra Safety Restore'"
     ```
   - *Procedimento:* Para validar a tarefa sem aguardar o relógio de 60s, execute o script após derrubar os processos.
   - *Resultado esperado:* Restauração e reinicialização imediatas.

---

### Fase 4 — Blindagem da Instalação Completa (Windows Service Dispatcher + Shell Extension)
**Objetivo:** Adicionar as camadas de proteção máxima exclusivas para o modo instalado (`!isPortable`), aproveitando os privilégios do instalador para redundância via Windows Service (contornando Session 0) e menu de contexto no papel de parede.

#### O que será feito:
1. **Windows Service Dedicado (`DeskQuadra.Service`) como Dispatcher Inteligente:**
   - Serviço em C# (.NET 8 LTS) configurado para rodar como serviço do Windows.
   - **Arquitetura para contornar Session 0:**
     - O Service não tenta acessar janelas diretamente (bloqueado por Session 0 isolation).
     - Timer em background de 30 segundos detecta ausência de `DeskQuadra.UI.Wpf.exe` via `Process.GetProcessesByName()`.
     - Se detectar ausência + ícones ocultos: usa `WTSGetActiveConsoleSessionId()` para obter a sessão do usuário ativo (Session 1+).
     - Usa `WTSQueryUserToken()` + `CreateProcessAsUser()` para disparar `DeskQuadra.Restorer.exe` **na sessão do usuário**, com privilégios corretos.
     - O Restorer (rodando como usuário) tem acesso ao `SysListView32` e executa `ShowWindow(SW_SHOW)` normalmente.
   - Condicionado estritamente a `!isPortable` (nunca ativado no modo portátil).

2. **Shell Extension no Menu de Contexto do Desktop (Registry Entry Simples):**
   - Registro em `HKCU\Software\Classes\DesktopBackground\Shell\DeskQuadraRestore`:
     - `(Default)` = *"Restaurar Ícones do Desktop (DeskQuadra)"*
     - `Icon` = caminho para ícone dedicado
     - `Command\(Default)` = `"<caminho>\DeskQuadra.Restorer.exe" --force`
   - Não requer DLL COM complexa, apenas entrada de registro simples.
   - Condicionado estritamente a `!isPortable`.

3. **Atualização do Script Inno Setup (`installer/DeskQuadra.iss`):**
   - Adicionar arquivos de `DeskQuadra.Restorer` e `DeskQuadra.Service` no payload.
   - Registrar/iniciar o serviço do Windows no fim da instalação.
   - Registrar a entrada de registry para Shell Extension.
   - Adicionar o atalho de emergência na pasta do Menu Iniciar.
   - Limpeza completa e desregistro do serviço e das chaves de registro na desinstalação.

#### Critério de Aceite do PO & Roteiro Prático de Testes:

##### Roteiro de Testes da Fase 4:
1. **Teste 1 (Windows Service como Dispatcher - Session 0 → Session 1+):**
   - *Procedimento:*
     1. Instalar DeskQuadra em modo completo (`!isPortable`).
     2. Verificar que o serviço `DeskQuadra.Service` está rodando em `services.msc`.
     3. Matar App + Guardian: `taskkill /F /IM DeskQuadra.UI.Wpf.exe /IM DeskQuadra.Guardian.exe`
   - *Resultado esperado:* Em até 30 segundos, o Service detecta ausência e dispara `DeskQuadra.Restorer.exe` via `CreateProcessAsUser()`. Ícones são restaurados e app reinicia.

2. **Teste 2 (Menu de Contexto do Papel de Parede):**
   - *Procedimento:* Em uma máquina com a instalação completa realizada, clicar com o botão direito em um espaço vazio da área de trabalho do Windows.
   - *Resultado esperado:* O menu de contexto nativo exibe o item *"Restaurar Ícones do Desktop (DeskQuadra)"* com ícone próprio, que ao ser clicado executa `DeskQuadra.Restorer.exe --force` e restaura a visibilidade dos ícones imediatamente.

3. **Teste 3 (Garantia de Isolamento no Modo Portátil):**
   - *Procedimento:* Rodar o executável portátil (`DeskQuadra.UI.Wpf.exe`).
   - *Resultado esperado:* Verificar no `services.msc` e no registro do Windows que o serviço e a Shell Extension **NÃO** foram instalados, mantendo o modo portátil 100% limpo e sem poluição do sistema.


---

## 6. Padrão de Logs (Reuso do Padrão Existente)

Conforme identificado no código existente (`DeskQuadra.Guardian\Program.cs`, `DeskQuadra.Core\ThirdParty\ShellMenuLog.cs`), o projeto já possui padrão de logs estabelecido:

### Formato Padrão:
```
[yyyy-MM-dd HH:mm:ss.fff] {message}
```

### Características:
- **Best-effort:** `try/catch`, nunca quebra o app
- **Append simples:** `File.AppendAllText()`
- **Localização:** `%APPDATA%\DeskQuadra\`
- **Zero dependências:** Apenas `System.IO`

### Arquivos de Log por Componente:
- `guardian.log` - Processo Guardian
- `shell-menu.log` - Menu de contexto de terceiros
- `chord-diag.log` - Diagnóstico temporário (remover antes de release)
- **NOVOS:**
  - `restorer.log` - DeskQuadra.Restorer
  - `service.log` - DeskQuadra.Service (modo instalado)

### Implementação Padrão (Para Restorer e Service):
```csharp
public static class Logger
{
    private const string LogFileName = "restorer.log"; // ou "service.log"
    
    public static void Log(string message)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DeskQuadra");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, LogFileName),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort: log nunca quebra o processo.
        }
    }
}
```

**Rotação:** Não há rotação nos logs existentes. Manter simplicidade por consistência.

---

## 7. Riscos Conhecidos & Mitigações

- **Risco 1: Falso Positivo de Antivírus:**
  - *Mitigação:* Zero scripts (`.ps1`, `.cmd`, `.bat`). Todos os executáveis são binários C# compilados (~25KB), executando apenas chamadas Win32 padronizadas.
- **Risco 2: Múltiplas Instâncias de Restauração Concorrentes:**
  - *Mitigação:* O `DeskQuadra.Restorer.exe` utiliza verificação rápida de processos ativos (< 5ms) e mutex local de execução para evitar disputas de recursos.
- **Risco 3: Conflito de Restauração (Guardian vs Restorer):**
  - *Mitigação:* O `DeskQuadra.Restorer` só atua se `DeskQuadra.Guardian` também estiver ausente. Guardian tem prioridade de ação.
- **Risco 4: Bloqueio de Permissões no Modo Portátil:**
  - *Mitigação:* As tarefas do Task Scheduler e atalhos do Menu Iniciar no modo portátil rodam estritamente no escopo de usuário (`HKCU` e token interativo normal), nunca exigindo elevação de Administrador (UAC).
- **Risco 5: Windows Service em Session 0 (Sem Acesso ao Desktop):**
  - *Mitigação:* Service atua como dispatcher inteligente, usando `CreateProcessAsUser()` para lançar `Restorer.exe` na sessão do usuário (Session 1+), contornando a limitação de Session 0.

---

## 8. Dependências do Projeto

Todos os projetos utilizam:
- **Runtime:** .NET 8 LTS (`<TargetFramework>net8.0-windows</TargetFramework>`)
- **SDK:** `Microsoft.NET.Sdk` (console) ou `Microsoft.NET.Sdk.WindowsDesktop` (WPF)
- **Packages NuGet (App Principal):**
  - `Windows.UI.Notifications` (toast nativo do Windows)
  - Bibliotecas já existentes no projeto (verificar `.csproj`)
- **Packages NuGet (Restorer/Guardian/Service):** ZERO (apenas BCL)

---

## 9. Diário de Bordo & Status das Fases

- [ ] **Fase 1 — Núcleo de Resiliência no Ciclo de Vida do App** (Pendente)
- [ ] **Fase 2 — Executável Dedicado `DeskQuadra.Restorer` & Atalho no Menu Iniciar** (Pendente)
- [ ] **Fase 3 — Task Scheduler Safety Net (Monitoramento a cada 1 Minuto)** (Pendente)
- [ ] **Fase 4 — Blindagem da Instalação Completa (Windows Service + Shell Extension)** (Pendente)
