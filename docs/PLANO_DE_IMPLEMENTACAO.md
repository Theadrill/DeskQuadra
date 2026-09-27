# Plano de Implementação em Fases Testáveis — DeskQuadra

> Documento oficial de engenharia que detalha a rota de desenvolvimento orientada a metas verificáveis (*Goal-Driven Execution*), em conformidade com as 25 seções aprovadas no [BRAINSTORMING.md](BRAINSTORMING.md).
> Cada fase produz um executável ou resultado tangível que o Product Owner (PO) pode compilar, executar e testar diretamente no seu ambiente Windows.

---

## Visão Geral da Arquitetura da Solução

O projeto DeskQuadra é dividido em 5 camadas estritas (.NET 8 LTS / Clean Architecture):

```
DeskQuadra/
├── src/
│   ├── DeskQuadra.Core/                         # Domínio Puro (Entidades: Quadra, ShortcutItem, BoundingBox, Enums, Contratos)
│   ├── DeskQuadra.Application/                  # Casos de Uso, Motor de Snap Magnético, Orquestrador de Layout, Serviços
│   ├── DeskQuadra.Infrastructure.WindowsShell/  # P/Invoke Win32 (WorkerW parenting, ShellExecute, IFileOperation, SHGetFileInfo, Hotkeys)
│   ├── DeskQuadra.Infrastructure.Persistence/   # Repositório JSON transacional (Double-buffering: .tmp -> .bak -> .json)
│   └── DeskQuadra.UI.Wpf/                       # Views XAML (Fluent Design), ViewModels (MVVM), ResourceDictionaries e App Entrypoint
└── tests/
    ├── DeskQuadra.Core.Tests/                   # Testes unitários de regras de domínio e validação
    └── DeskQuadra.Application.Tests/            # Testes unitários do motor matemático de Snap Magnético e ordenação
```

---

## Matriz de Governança por Skills (`.agent/skills/`)

Conforme estabelecido na Sessão 1 do [BRAINSTORMING.md](BRAINSTORMING.md), a execução de cada passo/fase deve **obrigatoriamente selecionar e ativar as melhores skills** do repositório:

| Domínio de Atuação | Skills Mandatórias |
|--------------------|-------------------|
| **Interoperabilidade Win32 & Shell** | `dotnet-pinvoke`, `wpf-windows-desktop` |
| **Interfaces WPF, XAML, Acrylic & Usabilidade** | `wpf-windows-desktop`, `ui-ux-pro-max`, `ui-visual-validator` |
| **Clean Code, Arquitetura & Refatoração** | `coding-guidelines`, `csharp-refactoring` |
| **Performance, Memória (~30MB) & Benchmarks** | `analyzing-dotnet-performance`, `microbenchmarking` |
| **Estrutura de Projetos & Compilação MSBuild** | `msbuild-modernization` |
| **Testes Automatizados (xUnit)** | `run-tests`, `assertion-quality`, `test-anti-patterns`, `test-smell-detection` |
| **Documentação Técnica & Registros de Decisão** | `docs-writer` |

---

## Fases de Execução & Critérios de Aceite

```
                                      ROTA DE DESENVOLVIMENTO
                                      
  ┌────────────────┐     ┌────────────────┐     ┌────────────────┐     ┌────────────────┐     ┌────────────────┐
  │     FASE 1     │ ──> │     FASE 2     │ ──> │     FASE 3     │ ──> │     FASE 4     │ ──> │     FASE 5     │
  │   Fundação &   │     │ Persistência & │     │ Varredura &    │     │  Gestão & Drag │     │   Tray, Touch  │
  │ 1ª Quadra Viva │     │ Snap Magnético │     │ Onboarding     │     │    and Drop    │     │   & Resiliência│
  └────────────────┘     └────────────────┘     └────────────────┘     └────────────────┘     └────────────────┘
```

---

### Fase 1: Fundação Estrutural & A Primeira Quadra Viva
* **Objetivo:** Estabelecer a Solution .NET 8, configurar as referências dos projetos modulares e renderizar a primeira janela translúcida de Quadra acoplada ao nível do desktop.
* **Skills Mandatórias:** `msbuild-modernization`, `dotnet-pinvoke`, `wpf-windows-desktop`, `coding-guidelines`, `ui-ux-pro-max`.
* **Escopo Técnico:**
  - Criação de `DeskQuadra.sln` e dos 5 projetos `.csproj`.
  - Configuração do `.gitignore` padrão .NET.
  - Implementação inicial de `QuadraWindow.xaml` com efeito translúcido acrílico/Mica e cantos arredondados.
  - P/Invoke inicial no `WindowsShell`: integração de Z-order (`WorkerW`/`Progman`) para tornar a janela imune ao atalho `Win + D`.
  - Manipuladores de movimentação (*drag*) e redimensionamento livre (*resize grips/adorners*).
* **Critério de Teste do PO (O que você vai testar):**
  - Você executará `DeskQuadra.UI.Wpf.exe`.
  - Uma Quadra elegante aparecerá na área de trabalho.
  - Ao pressionar `Win + D`, todas as outras janelas do Windows se minimizam, mas a Quadra permanece visível no papel de parede.
  - Você consegue arrastar a Quadra pela barra de título e redimensioná-la pelas bordas com o mouse.

---

### Fase 2: Motor de Snap Magnético & Persistência Transacional
* **Objetivo:** Implementar o algoritmo geométrico de Snap Magnético e a gravação atômica do layout em disco.
* **Skills Mandatórias:** `coding-guidelines`, `csharp-refactoring`, `analyzing-dotnet-performance`, `run-tests`, `assertion-quality`.
* **Escopo Técnico:**
  - Desenvolvimento do motor de cálculo de colisão e proximidade em `DeskQuadra.Application` (com suporte a espaçamento configurável *Snap Gap* e alinhamento às bordas `WorkArea` do monitor).
  - Implementação de `JsonLayoutRepository` em `DeskQuadra.Infrastructure.Persistence` com rotação de segurança (`quadras.json.tmp` ➔ `.bak` ➔ `.json`) e *Debounce* de ~400ms para poupar I/O.
  - Persistência das coordenadas $(X, Y)$, largura, altura e títulos no diretório `%APPDATA%\DeskQuadra\quadras.json`.
  - Criação da suite de testes automatizados para o algoritmo de Snap (`DeskQuadra.Application.Tests`).
* **Critério de Teste do PO (O que você vai testar):**
  - Ao arrastar a Quadra perto da borda do monitor ou perto de outra Quadra, você sentirá a atração magnética suave puxando e alinhando perfeitamente.
  - Ao soltar, fechar o programa e reabri-lo, a Quadra reaparecerá exatamente no mesmo local e dimensão onde você a deixou.

---

### Fase 3: Varredura de Ícones & Onboarding Automático ("TUDO")
* **Objetivo:** Ocultar os ícones nativos do Windows e criar a experiência de primeiro uso (*First-Run*) com a Quadra "TUDO".
* **Skills Mandatórias:** `dotnet-pinvoke`, `wpf-windows-desktop`, `analyzing-dotnet-performance`, `coding-guidelines`, `ui-ux-pro-max`, `ui-visual-validator`.
* **Escopo Técnico:**
  - Extração da lista de atalhos e arquivos das três origens: Desktop do Usuário (incluindo OneDrive), Desktop Público (`C:\Users\Public\Desktop`) e atalhos locais.
  - Filtro inteligente de arquivos de sistema (`desktop.ini`, temporários `~$*.*`).
  - Ocultação dos ícones nativos via Win32 `ShowWindow(hDesktopListView, SW_HIDE)` no segundo zero de inicialização.
  - Criação automática da Quadra padrão com flag técnica `IsDefault = true` posicionada dinamicamente à direita da tela.
  - Extração de ícones em alta resolução nativa (32px, 48px e 96px Jumbo) e execução com duplo clique via `ShellExecute`.
  - Restauração instantânea dos ícones nativos no evento de saída ou falha.
* **Critério de Teste do PO (O que você vai testar):**
  - Ao abrir o app pela primeira vez, seu desktop nativo fica imediatamente limpo.
  - Surge no canto direito uma Quadra chamada "TUDO" contendo todos os seus atalhos com ícones nítidos e nomes legíveis.
  - Ao dar duplo clique em qualquer ícone da Quadra, o programa/arquivo abre normalmente.
  - Ao fechar o app, todos os ícones nativos originais do Windows voltam instantaneamente para o mesmo lugar.

---

### Fase 4: Gestão de Quadras, Ordenação & Drag and Drop entre Quadras
* **Objetivo:** Permitir a criação de múltiplas Quadras, reorganização de atalhos e transferências por arraste.
* **Skills Mandatórias:** `dotnet-pinvoke`, `wpf-windows-desktop`, `coding-guidelines`, `ui-ux-pro-max`, `ui-visual-validator`.
* **Escopo Técnico:**
  - Criação de novas Quadras desenhando retângulos com o botão direito no desktop (com *Drag Threshold* de 15px e Menu Dual de confirmação).
  - Suporte a adicionar atalhos manualmente pelo botão "+".
  - Arraste de ícones entre Quadras (operação de **MOVER**).
  - Duplicação física no disco ao arrastar segurando a tecla `Ctrl` (`Ctrl + Drag`) via API COM `IFileOperation`.
  - Ordenação automática por submenu (*Nome*, *Tipo*, *Data*) e ordenação manual mantendo o grid (*slot-based*).
* **Critério de Teste do PO (O que você vai testar):**
  - Desenhar com o botão direito numa área vazia do desktop e clicar em *"Criar Quadra Aqui"*.
  - Arrastar ícones do "TUDO" para a nova Quadra.
  - Segurar `Ctrl` e arrastar para criar uma cópia real do arquivo.
  - Alternar a classificação por Nome ou reordenar livremente os ícones dentro da grade.

---

### Fase 5: System Tray, Modo Roll-up & Acessibilidade Dual (Touch/Mouse)
* **Objetivo:** Implementar o hub de controle na bandeja do sistema, modo gaveta e suporte a telas portáteis/touch.
* **Skills Mandatórias:** `wpf-windows-desktop`, `ui-ux-pro-max`, `ui-visual-validator`, `coding-guidelines`.
* **Escopo Técnico:**
  - Ícone na System Tray (bandeja junto ao relógio) com menu de contexto: *"Mostrar Quadras Escondidas"*, *"Travar todas as Quadras"*, *"Configurações"* e *"Sair"*.
  - Diálogo do botão "X" de cada Quadra (*"Esconder"* vs. *"Excluir"* com regras de proteção da Quadra Padrão).
  - Modo Roll-up na barra de título (duplo clique recolhe, botão chevron dedicado e expansão temporária *Spring-Loaded* ao arrastar arquivo por cima).
  - Arquitetura de Densidade Dual: Modo Normal (28px de barra, 24px de botão) vs. Modo Touch (42px de barra, 44px de hitbox para Steam Deck/telas táteis).
* **Critério de Teste do PO (O que você vai testar):**
  - Clicar duas vezes no título para recolher a Quadra em uma barrinha fina.
  - Fechar uma Quadra com itens e vê-la na lista da bandeja para reativar com 1 clique.
  - Testar a alternância entre o Modo Normal e o Modo Touch.

---

### Fase 6: Resiliência Máxima, Watchdog do Explorer & Panic Button
* **Objetivo:** Blindar o sistema contra qualquer falha catastrófica do sistema operacional ou travamento.
* **Skills Mandatórias:** `dotnet-pinvoke`, `wpf-windows-desktop`, `analyzing-dotnet-performance`, `coding-guidelines`, `docs-writer`.
* **Escopo Técnico:**
  - Monitoramento contínuo do Desktop físico via `FileSystemWatcher` com suporte a OneDrive e debounce de 250ms.
  - Registro de broadcast `TaskbarCreated` e Watchdog Timer de 3.0s para recuperação automática caso o `Explorer.exe` reinicie.
  - Registro da Global Hotkey de Pânico: `Ctrl + Shift + Alt + Q` via `RegisterHotKey`.
  - Detecção e recuperação de encerramento anormal através de arquivo `.tmp` órfão no boot.
  - Ocultação ultra-precoce na função `Main()` com fade-in suave de 200ms para eliminação completa de flicker.
* **Critério de Teste do PO (O que você vai testar):**
  - Matar o processo `explorer.exe` no Gerenciador de Tarefas e ver o DeskQuadra se auto-recuperar sozinho em ~150ms.
  - Pressionar `Ctrl + Shift + Alt + Q` a qualquer momento para ver o app encerrar em modo de emergência e o desktop nativo reaparecer imediatamente.

---

## Registro de Decisões de Implementação

> Aprendizados colhidos na prática para não repetir erros. Entradas em ordem cronológica.

### 2026-09-27 — Scroll por gesto de dedo nas Quadras (Steam Deck)
* **Contexto:** Quadras com mais ícones que o espaço visível precisam de scroll por gesto de dedo estilo celular (Fase 5, telas touch). O gesto começa sobre os ícones, não no vazio.
* **Tentativa 1 — Pan nativo (`6a05e6c`):** `ScrollViewer` com `PanningMode="VerticalOnly"` e `ManipulationBoundaryFeedback` tratado, sem handlers de toque nos itens. Funcionou no estágio inicial.
* **Tentativa 2 — Press-and-hold + menu por item (`dc983e1` e seguintes):** adicionou `<Border.ContextMenu>` por item, handlers `PreviewTouchDown/Move/Up`, timer de hold (380ms) e `PanningMode=None` durante arrasto. Quebrou o scroll: o hold automático do framework passou a sequestrar o toque sobre os ícones antes da manipulação iniciar.
* **Tentativa 3 — Scroll-first parcial:** `Stylus.IsPressAndHoldEnabled="False"` + bloqueio de `ContextMenuOpening` para toque. Continuou sem scrollar.
* **Diagnóstico com dado real (`input-diag.log` no `%APPDATA%\DeskQuadra`):** o Steam Deck entrega o dedo como `WM_POINTER` + **mouse promovido** (`GetMessageExtraInfo = 0xFF51578x`, `StylusDevice = null`) e **zero** eventos WPF `Touch`/`Manipulation` na janela. Ou seja, o pan nativo do `ScrollViewer` está morto nesse ambiente por construção — nenhum ajuste de `PanningMode` resolveria.
* **Solução final — Scroll manual 1:1 (`QuadraWindow.xaml.cs`):** arrasto com assinatura de toque (`NativeMethods.IsCurrentMessageFromTouch()`) desloca `ScrollViewer.VerticalOffset` diretamente, com `CaptureMouse` no `ScrollViewer`, desvio da scrollbar e sem `e.Handled` no `Down` (preserva "tocou, seleciona"). Mouse real segue intacto com drag de item. Sem inércia nesta etapa; menu por "segurou e soltou" fica para a próxima fase (máquina de estados tap/hold/drag).
* **Regras para não repetir:**
  1. Nunca detectar toque só por `StylusDevice` — no Deck ele é sempre `null`; o discriminador confiável é `GetMessageExtraInfo` (`0xFF51578x`).
  2. Nunca usar press-and-hold automático do framework em itens dentro de área scrollável — ele rouba o gesto antes do pan.
  3. Não adicionar handlers `PreviewTouch*` que competem com o `ScrollViewer` sem necessidade comprovada por log.
  4. Todo gesto touch deve ser testado começando **sobre um ícone**, nunca só no padding vazio.

### 2026-09-27 (cont.) — Drag por toque: tentativas descartadas
* **Contexto:** após o scroll manual e o menu por hold nativo funcionarem no Deck, tentou-se o "segurou e moveu = arrasta". Nenhuma tentativa funcionou no hardware real; o drag touch foi **removido do código** e está suspenso aguardando nova proposta do PO.
* **Tentativa 1 — Timer próprio de hold + conversão (`TouchHoldDelayMs 380 / limiar 12px`):** descartada antes de validar — competia com o hold do SO e virou gambiarra sobre um caminho que o Windows já faz de graça.
* **Tentativa 2 — Conversão tempo + distância (`350ms` + `>14px` após hold):** nunca converteu no Deck. Causa: corrida contra o hold nativo — segurar mais abre o menu (e `IsItemMenuOpen()` bloqueava o drag no gesto); segurar menos caía no lockout de scroll (`_touchGestureIsScroll`).
* **Tentativa 3 — Morph "menu-aberto + mover vira drag":** nunca converteu. Causa: ao abrir, o popup do `ContextMenu` captura o mouse — os `Move` seguintes vão para o menu e nunca chegam ao `ScrollViewer`.
* **Tentativa 4 — Morph no nível da janela (`PreviewMouseMove` da Window):** também sem efeito no Deck. Hipótese restante: o SO não entrega `Move` promovido à janela enquanto o popup do menu está aberto com o dedo embaixo.
* **Estado final:** `QuadraWindow.xaml.cs` contém só scroll manual 1:1 + inércia + congelamento sob menu aberto; drag de mouse 100% intacto. Regra: não reintroduzir conversão de gesto sem antes logar (`input-diag.log`) o que o SO entrega durante o popup aberto.

### 2026-09-27 (cont.) — Fase 5, Fatia 1: X (Esconder/Excluir) + tray validada
* **Entregue:** diálogo Esconder/Excluir/Cancelar no X (exclusão move itens p/ TUDO; padrão exige confirmação extra; disco nunca tocado); `HideQuadra`/`RestoreQuadra` no coordinator com eventos; `NotifyIcon` WinForms sem NuGet novo (ícone provisório do sistema, `TODO` marcado); menu tray com escondidas + "Mostrar todas" + Sair; `ShutdownMode=OnExplicitShutdown`; escondidas não reabrem no boot. **Validado pelo PO no Windows.**
* **Pendente:** "Configurações", ícone próprio, Hover-Peek do Roll-up.

### 2026-09-27 (cont.) — Fase 5, Fatia 3: Roll-up validada
* **Entregue (seções 10 e 18):** duplo-clique na barra + botão chevron ˄/˅ alternam recolhido (só a barra, ~50px); thumbs ocultos e resize ignorado enquanto recolhido; estado persiste; spring-loaded 400ms ao arrastar sobre recolhida (expande, drop mantém, sair recolhe). **Validado pelo PO no Windows.**
* **Entregue:** Hover-Peek — parou o mouse ~350ms sobre recolhida expande temporário; saiu recolhe após ~350ms (debounce anti-flicker); touch excluído do peek (só chevron/duplo-toque); coexiste com spring sem briga. **Validado pelo PO no Windows.**
* **Entregue (seção 12):** "🔒 Travar esta Quadra" no menu da barra de título + cadeado discreto; trava move e resize (scroll/cliques/menu/X intactos); "🔒 Travar todas as Quadras" no tray; estado persiste em `quadras.json`. **Validado pelo PO no Windows.**
* **Pendente:** "Configurações", ícone próprio, Roll-up.

---

## Próxima Fase Planejada: Interações Touch Completas (Tap / Hold / Drag)

* **Objetivo:** Completar o modelo celular nas Quadras: tocou, seleciona; segurou e soltou, menu de contexto; segurou e moveu, arrasta; gesto, scrolla (scroll já entregue em 2026-09-27).
* **Skills Mandatórias:** `wpf-windows-desktop`, `ui-ux-pro-max`, `coding-guidelines`, `dotnet-pinvoke`.
* **Escopo Técnico:**
  - Menu de contexto por "segurou e soltou" via hold NATIVO do Windows (entregue e validado em 2026-09-27, estilo touch 46px).
  - Inércia no scroll manual (entregue e validada em 2026-09-27).
  - Drag por toque ("segurou e moveu"): SUSPENSO — 4 tentativas descartadas (ver Registro acima); aguarda nova proposta do PO.
  - Manter intactos: tap seleciona e duplo-toque abre (via mouse promovido); mouse real inalterado.
* **Riscos conhecidos:**
  - Desambiguação scroll vs. drag no mesmo gesto (critério tempo + distância, sem `DispatcherTimer` na thread de UI se possível).
  - `DragDrop.DoDragDrop` é modal e trava a thread — desacoplar do pipeline de input como tentado em `1584f94`.
* **Critério de Teste do PO (O que você vai testar):**
  - Arrastar com o dedo sobre ícones scrolla com inércia; tocar seleciona; segurar e soltar abre o menu grande de toque; segurar e mover arrasta o item para outra Quadra.
