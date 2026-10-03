# Plano de Implementação — Menu de Terceiros (verbos clássicos por item/pasta)

> Fonte da verdade desta feature. Pesquisa completa (ondas 1a/1b + fechamento de lacunas) já feita — sem achismo.
> Decisões do PO: seção de terceiros DIRETO no menu (sem "Mais opções ≫" por enquanto); cobre menu do ÍCONE e menu do VAZIO da Quadra.
> Regra de ouro: nenhuma fase commita sem validação do PO; push só quando não houver mais o que mudar.

## 1. O que estamos construindo

Para o arquivo clicado (ou pasta, no vazio), perguntar ao Windows quais ações clássicas de terceiros existem
(7-Zip, WinRAR, Git, Notepad++ — qualquer um, presente ou futuro, sem hardcoded), listar numa seção própria do
nosso menu WPF e executar a escolhida. Pipeline oficial: `SHParseDisplayName → SHBindToParent →`
`IShellFolder.GetUIObjectOf(IContextMenu) → QueryContextMenu` num `HMENU` fantasma → enumeração recursiva
(incluindo `MF_POPUP`/cascata) → `InvokeCommand` por offset na mesma interface raiz.

Decisões técnicas travadas (pesquisa):
- Flags de query: `CMF_NORMAL | CMF_ITEMMENU | CMF_SYNCCASCADEMENU` (+ `CMF_EXTENDEDVERBS` só com Shift).
  `CMF_EXPLORE`, `CMF_NODEFAULT`, `CMF_INCLUDESTATIC`, `CMF_DEFAULTONLY` NÃO entram.
- Unicode sempre: `CMINVOKECOMMANDINFOEX` + `CMIC_MASK_UNICODE` (caminho acentuado é bug garantido sem isso).
- `.lnk`: query sobre o próprio link (não resolve alvo antes).
- Query/invoke em thread STA com message queue (padrão Files.App `ThreadWithMessageQueue`), NUNCA na UI do WPF.
- Tipos via Vanara MIT (`Vanara.PInvoke.Shell32` 5.0.7 — checar versão vigente na época); lógica de
  enumeração/invoke nossa (nada de CodeProject/CPOL como dependência; SharpShell/CodePack/Ookii não resolvem).
- Isolamento: `DeskQuadra.ShellHost.exe` separado com timeout/kill (DLL de terceiro nunca derruba o app).
- Filtro "só terceiros": denylist de verbos canônicos (`open/opennew/print/printto/explore/properties/edit/
  runas/openas/sendto/cut/copy/paste/link/delete/rename/...` + `windows.*share*`) aplicada a `GCS_VERBW` **E**
  label + `MFT_SEPARATOR` sempre fora; resto (inclusive verbo vazio) = terceiro.
- Lacunas assumidas: comandos exclusivos do menu novo Win11 (`IExplorerCommand` sem fallback) não aparecem;
  handlers que exigem site do Explorer podem falhar (lista "não suportado").

## 2. CONTRATO VISUAL — LEIA ANTES DE TODA FASE (vale para agentes também)

Histórico: toda mexida no menu quebrava densidade touch/normal e cores. Isso NÃO se repete. Regras mandatórias:

1. **Novos `MenuItem` NUNCA definem `Style` explícito.** `ApplyStyleRecursively` (`QuadraWindow.xaml.cs`)
   sobrescreve o estilo de todos os itens na abertura via `ApplyMenuDensity` — item sem `Style` herda certo nos
   dois modos. (Exceção: nenhuma. Se o item ficar "estranho", o bug está no caminho de abertura, não no item.)
2. **Separador novo REUSA o padrão existente** (descobrir em T1 qual é — tray/menus já têm; se não houver token,
   criar UM em `Theme/Default.xaml` e reusar, nunca estilo inline).
3. **Zero cor/medida chapada**: tudo via `DynamicResource`/token existente; texto novo em `Strings.resx` (pt-BR).
4. **Checklist visual OBRIGATÓRIO ao fim de TODA fase** (agente executa, Tech Lead confere no diff + PO nos olhos):
   - [ ] Densidade Normal (mouse): altura/alinhamento iguais aos itens vizinhos.
   - [ ] Densidade Touch: mesma altura dos itens touch vizinhos (46px), sem transbordo.
   - [ ] Menu escuro intacto (fundo, hover, desabilitado, separador).
   - [ ] Contraste AA nos rótulos novos (sombra/estilo herdado, nada custom).
   - [ ] Cascata (submenu) nova abre alinhada e com a mesma densidade do pai.
5. **Proibido**: `Style=` inline em item novo, `Background/Foreground` chapado, `Height/FontSize` fixo em item,
   novo `ContextMenu` fora dos dois existentes (ícone e barra/vazio compartilham instância — UM ponto de edição).

## 3. Fases testáveis (Tech Lead delega → audita → PO valida com os olhos → push)

### T1 — Separador + seção vazia (só visual, zero Shell)
- Escopo: item-seção "terceiros" com `Separator` reusado + 1 placeholder desabilitado ("Nenhuma ação de terceiros",
  resx) nos menus do ÍCONE e do VAZIO/BARRA. Sem P/Invoke, sem query, sem timer.
- Skills: molde do código (`ApplyMenuDensity`, `ModernContextMenuStyle`, resx).
- PO valida: separador aparece nos dois menus, placeholder desabilitado, **checklist visual §2 verde nos 2 modos**.
- Push se limpo.

### T2 — Detecção real, só leitura (lista, não executa)
- Escopo: query `CMF_NORMAL|CMF_ITEMMENU|CMF_SYNCCASCADEMENU` em thread STA separada + enumeração recursiva
  (incl. cascata) + filtro §1 → troca o placeholder pelos rótulos reais **desabilitados** (sem invoke ainda).
  `.lnk` = query no link. Cache por extensão. Falha de query = volta ao placeholder (silencioso, padrão do projeto).
- Lógica pura (filtro/normalização) com xUnit.
- PO valida: 7-Zip aparece com cascata no `.zip`; app sem 7-Zip mostra placeholder; menu de pasta no vazio lista
  Git/7-Zip de diretório; **checklist visual §2** (cascata alinhada, densidade herdada).
- Push se limpo.

### T3 — Execução (habilita + invoca)
- Escopo: `InvokeCommand` por offset (`CMINVOKECOMMANDINFOEX` + `CMIC_MASK_UNICODE`) na mesma thread da query,
  interface viva até o invoke; itens habilitados; `GCS_VALIDATEW` como guarda.
- PO valida: "Add to archive" do 7-Zip abre o diálogo e gera o zip; verbo com acento no caminho funciona;
  **visual inalterado (§2)**.
- Push se limpo.

### T4 — Vazio da Quadra = verbos de pasta (se T2/T3 foram só ícone, completa aqui)
- Escopo: mesmo motor, PIDL da pasta; filtro igual. (Se T2 já cobriu o vazio, T4 vira "polimento de pasta".)
- PO valida: botão direito no vazio lista ações de pasta de terceiros e executa 1 (ex.: Git Bash Here).
- Push se limpo.

### T5 — Isolamento `ShellHost` + timeout
- Escopo: novo projeto `DeskQuadra.ShellHost` (x64, STA, sem janela; recebe caminho → devolve JSON
  rótulos/offsets/verbos; executa invoke sob supervisão com timeout/kill + relançamento). UI só espelha o JSON.
  Deploy: copia ao lado da UI (mesmo molde do Guardian).
- PO valida: matar o host no Gerenciador não derruba o app (menu volta ao placeholder); verbo lento não congela a UI.
- Push se limpo.

### T6 — Filtro fino + Shift (extended verbs)
- Escopo: refino da denylist com dados reais (logar `label/GCS_VERBW` dos nativos que vazarem), `CMF_EXTENDEDVERBS`
  com Shift pressionado, "Abrir com"/"Enviar para" como opt-in se o PO pedir.
- PO valida: zero nativo vazando como "terceiro"; Shift mostra extras; **checklist visual §2 final**.
- Push se limpo.

### T7 — Registro em docs + re-auditoria de reuso
- Escopo: entrada no Registro de Decisões (`PLANO_DE_IMPLEMENTACAO.md`), auditoria viva (`AUDITORIA_REUSO.md`),
  TODO do `README.md` atualizado. Sem código.
- Validação: Tech Lead confere; PO só lê o resumo.

## 4. Fora de escopo (não nesta leva)

- Comandos exclusivos do menu novo Win11 (`IExplorerCommand`); "Mais opções ≫" (só se o menu lotar no futuro);
  host elevado/UAC; desabilitar handler por CLSID (futuro, com `Blocked`); ícones dos terceiros no nosso menu.

## 5. T8 — Lazy + TTL + refresh manual (validado pelo PO, pushado)

- **T8a lazy:** seção de terceiros abre com `Carregando...` (resx `ThirdParty_Loading`) e preenche em background
  (`Task` + `Dispatcher`), com token de geração por menu (resultado velho descartado) e reaplicação de densidade
  no swap (§2). Falha/timeout volta ao placeholder T1.
- **T8b TTL assimétrico:** negativo (vazio/falha) 60s, positivo 5 min; expirado refaz a query (em background via lazy).
- **T8c tray:** `Atualizar ações de terceiros` (`ClearCache`) antes do Sair.
- Testes: 458 verdes no push. Host persistente fica como ideia futura (one-shot mantido).
