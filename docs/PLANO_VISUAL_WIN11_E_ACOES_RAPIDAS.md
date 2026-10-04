# Plano de Implementação: Visual Windows 11 Fluent & Ações Rápidas de Contexto

> Documento oficial de engenharia, arquitetura e checklist de execução passo a passo para a evolução visual do DeskQuadra para a estética e acabamento nativo do Windows 11 (transparência com backdrop/blur real) e inclusão da barra de comandos rápidos com ícones no menu de contexto.
>
> **Diretrizes Estruturais:**
> 1. Respeito irrestrito à Clean Architecture e Clean-Room design (terminologia: Quadra).
> 2. Preservação mandatória da **Identidade Visual e Densidade Dual** (Mouse ~26-30px vs. Touch ~44-46px com alvo WCAG).
> 3. Implementação estritamente incremental em **Fases Testáveis** com checkpoints de validação e commits locais individuais.
> 4. Push remoto **somente** após teste prático e aprovação expressa do Product Owner (PO).

---

## 1. Visão Geral da Arquitetura & Decisões Técnicas

### 1.1 Detecção de SO e Hardware Graphics Tier
- **Detecção do SO via Registro:** Em vez de `Environment.OSVersion` (que é afetado por manifestos de compatibilidade do aplicativo), a detecção da versão e build do Windows consulta diretamente `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\CurrentBuildNumber`.
- **Níveis de Compatibilidade do Sistema Operacional:**
  - **Tier Moderno (Windows 11 Build 22621+ / 22H2 em diante):** Suporta nativamente as APIs oficiais do DWM `DwmSetWindowAttribute` com atributo `DWMWA_SYSTEMBACKDROP_TYPE` (`DWMSBT_TRANSIENTWINDOW` = 3 para Acrylic, `DWMSBT_MAINWINDOW` = 2 para Mica).
  - **Tier Clássico (Windows 10 Build 10240+ até Windows 11 21H2):** Suporta a política de acrílico clássica do compositor via `SetWindowCompositionAttribute` (`ACCENT_ENABLE_ACRYLICBLURBEHIND`) e extensões de margem DWM.
  - **Fallback de Compatibilidade / Drivers Básicos:** Se o sistema estiver rodando em drivers de vídeo genéricos (*Microsoft Basic Display Adapter*), Remote Desktop (RDP), ou com aceleração de hardware desativada (`RenderCapability.Tier >> 16 < 2` ou `!DwmIsCompositionEnabled()`), os efeitos pesados de blur são desativados automaticamente, mantendo o visual escuro translúcido suave existente sem risco de congelamento ou perda de fluidez.
- **Opção em Configurações (`SettingsWindow`):**
  - O seletor de Efeitos Visuais Windows 11 só é exibido se o ambiente for compatível (`IsWindows11VisualSupported == true`). Caso contrário, permanece oculto.

### 1.2 Estratégia de Implementação do Visual (Clássico ➔ Moderno)
- **Regra de Engenharia Estabelecida com o PO:** Implementar e validar primeiro o método clássico (`SetWindowCompositionAttribute`), garantindo que ele esteja 100% testado, estável e funcional em toda a aplicação. Somente depois de homologado, adiciona-se a camada do método mais novo (APIs de backdrop do Windows 11 22H2+).

### 1.3 Menu de Contexto: Barra de Comandos Rápidos & Ícones
- **Ações Rápidas Superiores:**
  - Barra de ações compacta no topo do menu de contexto de itens: **Recortar**, **Copiar**, **Colar**, **Renomear**, **Excluir**.
  - No modo **Mouse**: Alvos compactos (~28-30px), foco na velocidade do cursor.
  - No modo **Touch**: Alvos expandidos respeitando acessibilidade tátil (mínimo de 44x44px).
  - Ícones vetoriais monocromáticos (Path / Geometrias XAML reutilizáveis ou Segoe Fluent/MDL2) adaptáveis a DPI alto e temas.
- **Linha de Comandos Tradicionais:**
  - Inclusão gradual de ícones alinhados aos itens tradicionais existentes no menu de contexto.

---

## 2. Checklist Geral de Execução

- [ ] **Fase 1: Infraestrutura de Detecção, Fallback e Opção nas Configurações**
  - [x] 1.1 Criar modelo e contrato de detecção (`IWindowsVisualCapabilityService`, `WindowsVisualTier`).
  - [x] 1.2 Implementar leitura resiliente de build do SO e verificação de aceleração gráfica (`RenderCapability.Tier`, DWM composition).
  - [x] 1.3 Adicionar preferência visual no Core/Settings (`IVisualSettingsService`) com persistência em `settings.json`.
  - [x] 1.4 Adicionar toggle na janela de Configurações (`SettingsWindow.xaml` / `SettingsViewModel.cs`) com visibilidade condicional.
  - [x] 1.5 Criar testes unitários para a detecção de build, capability, fallback e persistência (16 novos testes, total de 542 testes passando).
  - [ ] 1.6 **Checkpoint PO:** Executar, testar a exibição da opção em Configurações e realizar commit local.

- [ ] **Fase 2: Motor Visual Clássico (Acrílico / Blur via Compositor Win32)**
  - [ ] 2.1 Implementar P/Invoke e serviço Win32 de acrílico clássico (`SetWindowCompositionAttribute` / `AccentPolicy`).
  - [ ] 2.2 Criar tokens de acrílico calibrados em `Default.xaml` com proteção para o canal alfa.
  - [ ] 2.3 Aplicar o efeito acrílico nas janelas de teste (`SettingsWindow` e `ContextMenu`).
  - [ ] 2.4 Integrar o acrílico às janelas de Quadra (`QuadraWindow`) com tratamento de transparência e DWM.
  - [ ] 2.5 **Checkpoint PO:** Auditar visual e performance em máquina real (sem lag de arraste) e realizar commit local.

- [ ] **Fase 3: Motor Visual Moderno (Windows 11 22H2+ Nativo - Mica / Acrylic Backdrop)**
  - [ ] 3.1 Implementar P/Invoke de `DwmSetWindowAttribute` com `DWMWA_SYSTEMBACKDROP_TYPE` e `DWMWA_WINDOW_CORNER_PREFERENCE`.
  - [ ] 3.2 Adaptar o serviço de backdrop para chavear automaticamente entre o modo moderno (22621+) e o clássico.
  - [ ] 3.3 Testar fidelidade visual com o tema nativo do Windows 11.
  - [ ] 3.4 **Checkpoint PO:** Validação visual direta e commit local.

- [ ] **Fase 4: Barra Superior de Comandos Rápidos no Menu de Contexto (Espaço & Layout)**
  - [ ] 4.1 Estruturar a barra superior no template de `ModernContextMenuStyle` em `App.xaml` / `Default.xaml`.
  - [ ] 4.2 Garantir suporte estrito à Densidade Dual (Mouse ~28px vs Touch 44x44px).
  - [ ] 4.3 **Checkpoint PO:** Avaliar ergonomia e proporções visuais nas duas densidades e realizar commit local.

- [ ] **Fase 5: Ícones Vetoriais das Ações Rápidas (Copiar, Colar, Recortar, Renomear, Excluir)**
  - [ ] 5.1 Criar biblioteca de geometrias XAML vetoriais (`Path` reutilizáveis em `Default.xaml`).
  - [ ] 5.2 Estilizar estados visuais dos botões da barra rápida (Hover, Pressed, Disabled) no estilo Fluent.
  - [ ] 5.3 **Checkpoint PO:** Validar nitidez em diferentes DPIs e realizar commit local.

- [ ] **Fase 6: Lógica e Operações dos Comandos Rápidos**
  - [ ] 6.1 Implementar integração com Clipboard (`Recortar`, `Copiar`, `Colar` de arquivos do Explorer/Desktop).
  - [ ] 6.2 Conectar os comandos rápidos de `Renomear` e `Excluir` aos handlers existentes em `QuadraWindow`.
  - [ ] 6.3 Criar testes unitários para a lógica de transferência de arquivos e estado dos comandos.
  - [ ] 6.4 **Checkpoint PO:** Teste funcional de cada operação rápida e commit local.

- [ ] **Fase 7: Ícones dos Itens de Linha do Menu de Contexto**
  - [ ] 7.1 Expandir os estilos `MouseMenuItemStyle` e `TouchMenuItemStyle` para suportar ícones à esquerda com espaçamento correto.
  - [ ] 7.2 Adicionar ícones correspondentes aos itens tradicionais (Abrir, Abrir local, etc.).
  - [ ] 7.3 **Checkpoint PO:** Validação de alinhamento visual e acabamento geral.

- [ ] **Fase 8: Homologação Final & PUSH Remoto**
  - [ ] 8.1 Executar suíte completa de testes da solução (100% passando).
  - [ ] 8.2 Atualizar documentações (`README.md`, `BRAINSTORMING.md`, `AUDITORIA_REUSO.md`).
  - [ ] 8.3 Auditoria pelo PO e envio oficial (`git push origin main`).
