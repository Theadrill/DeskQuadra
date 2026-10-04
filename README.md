# DeskQuadra

O DeskQuadra redefine a experiência da área de trabalho do Windows, transformando um ambiente antes disperso em um espaço visualmente estruturado, intencional e harmonioso através de áreas dedicadas para organizar e harmonizar seus atalhos.

## TODO / Roadmap (pós-1.0 — nada bloqueia o release)

- [ ] **MOVER touch — abas multi-monitor:** BLOQUEADO (PO sem segundo monitor para validar; overview traz tudo para a tela do gesto até lá).
- [ ] **Review geral do sistema de toques:** dismiss do menu dual validado como "bom o suficiente" (perfeito dentro das Quadras, inconstante no desktop vazio) — aprimorar no futuro.
- [x] **REDIMENSIONAR touch:** pronto (menu touch, modo armado, bordas grossas + alças, snap corrigido, fonte +25%, botões ocultos no modo).
- [x] **Ícone próprio:** aplicado (exe + janelas + tray, arte do PO).
- [x] **Menu de terceiros (T1–T8):** pronto e validado (seção direta no menu do ícone e do vazio, ShellHost isolado, fundo real, lazy + TTL + refresh no tray; detalhe em `docs/PLANO_MENU_TERCEIROS.md`).
- [ ] **D11 — re-auditar fakes de teste** no final do projeto.
- [ ] **Remover temporários no final:** `HangTestSwitch`, `ChordDiagLog`.
- [ ] **Otimizar impacto na inicialização do Windows:** hoje o Gerenciador marca ALTO (scan + extração de todos os ícones + 1 janela pesada por Quadra + Guardian junto, tudo no login; autostart aponta p/ Debug) — aliviar em fatias futuras (ícones lazy/async, janelas sob demanda, Guardian atrasado, autostart no build final).
- [ ] **Seleção múltipla de itens:** Retângulo de seleção com mouse (marquee selection), `Ctrl + clique` (aditivo/alternado) e `Shift + clique` (intervalo contínuo).
- [ ] **Tooltip/Hover customizado estilo Menu Fluent:** Popover escuro posicionado no mouse com fonte branca, exibindo apenas o nome completo do item (sem caminho completo), sumindo ao sair.
- [ ] **Drop em arquivos container (.zip, etc.):** Suporte a arrastar e soltar itens sobre arquivos compactados/containers (.zip e afins) para inclusão direta no arquivo.
- [ ] **Atualização e recuperação em tempo real de ícones restaurados da Lixeira:** Garantir invalidação e recarregamento imediato de ícones em cache quando arquivos/atalhos forem restaurados ou atualizados sem exigir reinicialização do app.

## Créditos (bibliotecas de terceiros)

- **Vanara.PInvoke.Shell32** ([dahall/Vanara](https://github.com/dahall/Vanara), licença MIT) — só as declarações P/Invoke do Shell do Windows (tipos `IContextMenu`, `QueryContextMenu`, `InvokeCommand`); a lógica de menu de terceiros é nossa.
- **Microsoft.Extensions.DependencyInjection** (Microsoft, licença MIT) — injeção de dependência.
- **xUnit + coverlet** (testes; fora do distribuível).
