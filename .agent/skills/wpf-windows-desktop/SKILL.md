---
name: wpf-windows-desktop
description: Expert engineering patterns for high-performance WPF (.NET 8) and Windows 11 Shell native desktop integration. Use when implementing WPF views, ViewModels, Win32 P/Invoke interop (WorkerW/Progman desktop parenting), DPI awareness, translucent Acrylic/Mica styling, and dual-density (Mouse/Touch) layouts.
metadata:
  author: DeskQuadra Engineering
  version: '1.0.0'
---

# WPF & Windows 11 Shell Desktop Engineering

Guia de engenharia especializada para desenvolvimento do utilitário nativo DeskQuadra em C# / .NET 8 com WPF e integração profunda ao Windows Shell.

---

## 1. Princípios de Arquitetura & Clean Code

- **Clean Architecture & Inversão de Dependência:**
  - `DeskQuadra.Core`: Domínio puro. Entidades, value objects e contratos. Zero referências a bibliotecas de UI ou Win32.
  - `DeskQuadra.Application`: Casos de uso, orquestradores de layout, motor matemático de Snap Magnético.
  - `DeskQuadra.Infrastructure.WindowsShell`: Implementações concretas de P/Invoke, `WorkerW` parenting, hooks Win32 e Shell APIs.
  - `DeskQuadra.Infrastructure.Persistence`: Repositório transacional de layout com double-buffering.
  - `DeskQuadra.UI.Wpf`: Apresentação XAML, ViewModels e entrypoint com Host DI (`Microsoft.Extensions.DependencyInjection`).
- **Clean-Room Design:**
  - Terminologia oficial estrita: **"Quadra"**.
  - Nenhuma menção a softwares comerciais concorrentes em código, comentários, commits ou documentação.

---

## 2. Ancoragem no Nível do Papel de Parede (WorkerW / Progman)

Para que janelas fiquem imunes ao atalho `Win + D` e fiquem posicionadas exatamente no plano de fundo:

1. **Obtenção do WorkerW de Fundo:**
   - Enviar mensagem não-documentada `0x052C` para a janela `Progman` via `SendMessageTimeout`.
   - Enumerar janelas de nível superior com `EnumWindows`.
   - Localizar a janela `WorkerW` que possui como filha a janela com classe `SHELLDLL_DefView`.
   - A janela `WorkerW` subsequente (irmã logo atrás) é a camada de renderização do papel de parede.
2. **Parenting & Estilos Win32:**
   - Acoplar o `HWND` da Quadra como filho do `WorkerW` via `SetParent(hwndQuadra, workerW)`.
   - Aplicar `WS_EX_TOOLWINDOW` para que a Quadra não apareça na barra de tarefas nem no seletor `Alt + Tab`.
   - Tratar a conversão de coordenadas: uma vez que a janela se torna filha de `WorkerW`, suas coordenadas de tela tornam-se relativas à área de trabalho do `WorkerW`.

---

## 3. Performance & Pegada de Memória (~30MB RAM)

- **Virtualização de UI:** Utilizar `VirtualizingWrapPanel` para listas de atalhos, evitando que dezenas de itens instanciem controles visuais desnecessários.
- **Liberação de Recursos Gráficos:** Congelar (`Freeze()`) todos os `Brush`, `Pen` e `ImageSource` estáticos compartilhados para poupar memória e permitir acesso multithread.
- **Thread Safety:** Manter todas as operações Win32 que exigem mensagens de janela e renderização WPF estritamente na thread STA do `Dispatcher`. Operações de I/O de arquivos e cálculos puros devem rodar em threads de background (`Task.Run`).

---

## 4. Sistema de Densidade Dual (Mouse vs. Touch)

- **Modo Normal (Mouse):**
  - Altura da barra de título: ~28px.
  - Alvo de clique do chevron de recolhimento: ~24px (evitar invasão da área de arraste).
  - Espaçamento denso no grid de atalhos.
- **Modo Touch (Portáteis / Telas Táteis):**
  - Altura da barra de título: ~42px.
  - Hitbox do botão chevron: mínimo de 44x44px.
  - Padding ampliado no grid para evitar toques acidentais.
- Implementado via `ResourceDictionary` dinâmico alternado em tempo de execução sem recriar a janela.

---

## 5. Resiliência do Explorer & Ciclo de Vida

- **TaskbarCreated:** Escutar `RegisterWindowMessage("TaskbarCreated")` via `HwndSourceHook` para reancorar automaticamente as janelas caso o `Explorer.exe` reinicie.
- **Restauração de Emergência:** Em `UnhandledException` e no evento de saída limpa, garantir a chamada `ShowWindow(hDesktopListView, SW_SHOW)` para restaurar os ícones nativos do Windows.
