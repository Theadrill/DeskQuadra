# DeskQuadra

O DeskQuadra redefine a experiência da área de trabalho do Windows, transformando um ambiente antes disperso em um espaço visualmente estruturado, intencional e harmonioso através de áreas dedicadas para organizar e harmonizar seus atalhos.

## Requisitos do Sistema e Compatibilidade

### Requisitos Mínimos
* **Sistema Operacional:** Windows 10 versão 1607 (Build 14393 - Anniversary Update) ou superior (x64 / ARM64).
  * *Nota de arquitetura:* Versões anteriores (como Windows 7, 8.1 ou Windows 10 1507) não são compatíveis devido aos requisitos formais de runtime do **.NET 8** e APIs Win32 de DPI Per-Monitor v2.
* **Ambiente de Execução:** .NET 8 Desktop Runtime (incluso no executável empacotado).
* **Hardware Gráfico:** Placa de vídeo com suporte a DirectX/DWM (aceleração de composição de janelas).

### Tiers Visuais e Degradação Graciosa (Graceful Fallback)

O DeskQuadra detecta as capacidades gráficas e a versão do sistema operacional em tempo de execução:

| Cenário / Versão | Técnica Visual | Comportamento na Interface |
| :--- | :--- | :--- |
| **Windows 10 1803+ (Build 17134+) e Windows 11** | **Acrílico Moderno** e **Blur Clássico** | Ambas as técnicas ficam totalmente habilitadas para escolha do usuário nas Configurações. |
| **Windows 10 1607 a 17133 (Build 14393 a 17133)** | **Blur Clássico (Windows 10)** | O "Blur Clássico" fica ativo; a opção "Acrílico" permanece desabilitada com indicação de versão. |
| **Sem GPU / Sem Aceleração / Erro no DWM / Desativado** | **Tema Sólido de Alta Legibilidade** | Os seletores de transparência são ocultados ou desativados; o app aplica o tema escuro sólido (`#EB1C1C22`), preservando 100% do contraste e funcionalidade. |

## TODO / Roadmap (pós-1.0 — nada bloqueia o release)

- [ ] **MOVER touch — abas multi-monitor:** BLOQUEADO (PO sem segundo monitor para validar; overview traz tudo para a tela do gesto até lá).
- [ ] **Review geral do sistema de toques:** dismiss do menu dual validado como "bom o suficiente" (perfeito dentro das Quadras, inconstante no desktop vazio) — aprimorar no futuro.
- [x] **REDIMENSIONAR touch:** pronto (menu touch, modo armado, bordas grossas + alças, snap corrigido, fonte +25%, botões ocultos no modo).
- [x] **Ícone próprio:** aplicado (exe + janelas + tray, arte do PO).
- [x] **Menu de terceiros (T1–T8):** pronto e validado (seção direta no menu do ícone e do vazio, ShellHost isolado, fundo real, lazy + TTL + refresh no tray; detalhe em `docs/PLANO_MENU_TERCEIROS.md`).
- [ ] **D11 — re-auditar fakes de teste** no final do projeto.
- [ ] **Remover temporários no final:** `HangTestSwitch`, `ChordDiagLog`.
- [ ] **Otimizar impacto na inicialização do Windows:** hoje o Gerenciador marca ALTO (scan + extração de todos os ícones + 1 janela pesada por Quadra + Guardian junto, tudo no login; autostart aponta p/ Debug) — aliviar em fatias futuras (ícones lazy/async, janelas sob demanda, Guardian atrasado, autostart no build final).
- [x] **Seleção múltipla de itens:** Retângulo de seleção com mouse (marquee selection), `Ctrl + clique` (aditivo/alternado), `Shift + clique` (intervalo contínuo) e `Ctrl + A` (selecionar todos).
- [x] **Tooltip/Hover customizado estilo Menu Fluent:** Popover escuro posicionado no mouse com fonte branca, exibindo apenas o nome completo do item (sem caminho completo), sumindo ao sair.
- [ ] **Drop em arquivos container (.zip, etc.):** Suporte a arrastar e soltar itens sobre arquivos compactados/containers (.zip e afins) para inclusão direta no arquivo.
- [x] **Atualização e recuperação em tempo real de ícones restaurados da Lixeira:** Garantir invalidação e recarregamento imediato de ícones em cache quando arquivos/atalhos forem restaurados ou atualizados sem exigir reinicialização do app.
- [x] **Visual transparente estilo Windows 11 (Mica / Acrylic blur):** Implementado via motor híbrido de efeitos (`WindowVisualEffectService`) com suporte a Acrílico Moderno, Blur Clássico e degradação graciosa para tema sólido configurável pelo usuário.
- [x] **Ícones de operações rápidas no menu de contexto estilo Windows 11:** Barra superior de ações compactas (copiar, recortar, colar, renomear, compartilhar e excluir) integrada, padronizada e refinada no topo do menu de contexto.
- [x] **Slider de espaçamento entre quadras nas Configurações:** configurável (0–24px, padrão 8) com re-snap das Quadras abertas após ~1s sem ajuste.
- [ ] **Modernização visual das barras de rolagem (Scrollbar):** Modernizar o design e comportamento da barra de scroll por todo o aplicativo para padrão minimalista e elegante estilo Windows 11 Fluent.

## Créditos (bibliotecas de terceiros)

- **Vanara.PInvoke.Shell32** ([dahall/Vanara](https://github.com/dahall/Vanara), licença MIT) — só as declarações P/Invoke do Shell do Windows (tipos `IContextMenu`, `QueryContextMenu`, `InvokeCommand`); a lógica de menu de terceiros é nossa.
- **Microsoft.Extensions.DependencyInjection** (Microsoft, licença MIT) — injeção de dependência.
- **xUnit + coverlet** (testes; fora do distribuível).
