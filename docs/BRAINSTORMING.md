# Brainstorming & Linha do Tempo de Decisões — DeskQuadra

> Documento de registro cronológico e vivo das decisões estratégicas, de produto, de arquitetura e de UX tomadas pelo Product Owner (PO) e Tech Lead.

---

## [Sessão 1] Fundação Estratégica & Arquitetura Base
- **Clean-Room Design:** Terminologia proprietária estrita: **"Quadra"**. Tolerância zero para nomes ou referências de softwares concorrentes comerciais.
- **Stack & Performance:** C# / .NET 8 (LTS) e WPF. Foco em pegada ultraleve de memória (~30MB RAM) e aceleração de hardware (DirectX), descartando engines pesadas como Chromium/WebView2.
- **Integração Win32 / Desktop:** Ancoragem das janelas no nível do papel de parede do Windows (`WorkerW` / `Progman`), mantendo as Quadras imunes ao atalho `Win + D` e com ícones nativos do Windows ocultos.
- **Arquitetura Anti-Monólito:** Separação estrita em camadas (Clean Architecture): `Core`, `Application`, `Infrastructure.WindowsShell`, `Infrastructure.Persistence` e `UI.Wpf`.
- **Governança de Engenharia & Uso Mandatório de Skills:** A cada passo, fase ou tarefa técnica, o Tech Lead deve **SEMPRE selecionar e aplicar as melhores skills especializadas** disponíveis no repositório (`.agent/skills/`) para guiar a implementação. É mandatório cruzar o escopo do que será feito com as diretrizes correspondentes:
  - Interop Win32 / Shell / WorkerW / Handles ➔ `dotnet-pinvoke` e `wpf-windows-desktop`
  - Telas WPF, Acrylic, Densidade Dual, Hitboxes e XAML ➔ `wpf-windows-desktop`, `ui-ux-pro-max` e `ui-visual-validator`
  - Clean Code, Simplicidade Cirúrgica e Anti-Overengineering ➔ `coding-guidelines` e `csharp-refactoring`
  - Performance, Baixo Consumo de RAM (~30MB) e Benchmarks ➔ `analyzing-dotnet-performance` e `microbenchmarking`
  - Configuração de Projetos e Builds .NET ➔ `msbuild-modernization`
  - Testes Automatizados Unitários (xUnit) ➔ `run-tests`, `assertion-quality`, `test-anti-patterns` e `test-smell-detection`
  - Atualização e Rigor de Documentação Técnica ➔ `docs-writer`
- **Reuso (regra permanente):** se dá pra reusar, REUSE — ver subseção em `docs/PLANO_DE_IMPLEMENTACAO.md` e auditoria viva em `docs/AUDITORIA_REUSO.md`.

---

## [Sessão 2] Decisões de Produto & Fluxo de Usuário

### 1. Integridade e Segurança dos Arquivos
- **Decisão:** A Quadra **nunca move ou altera arquivos físicos** no disco. O arquivo real permanece são e salvo em `C:\Users\<User>\Desktop`.
- **Persistência:** Apenas o layout, coordenadas e referências de caminho ficam armazenados em um arquivo leve e veloz (`quadras.json`).
- *Nota do Tech Lead:* Risco zero de perda de dados. Se o DeskQuadra for desinstalado, o desktop nativo do Windows reaparece exatamente como era.

### 2. Experiência de Primeiro Uso (Onboarding / First-Run)
- **Decisão:** Ao abrir pela primeira vez, o DeskQuadra varre o desktop nativo, oculta os ícones originais e cria automaticamente uma Quadra chamada **"TUDO"**, posicionada no canto direito da tela com todos os atalhos encontrados.
- *Nota do Tech Lead:* Reduz drasticamente a fricção inicial do usuário. Em vez de uma tela vazia ("síndrome da página em branco"), o usuário já vê seu desktop limpo e organizado no segundo zero.

### 3. Topologia Visual e Interação com Mouse
- **Decisão:** Cada Quadra é uma **janela WPF independente e transparente** (não uma janela única cobrindo toda a tela).
- **Rolagem Interna:** Se a Quadra for menor que a quantidade de ícones, ela possui suporte a rolagem suave via scroll do mouse.
- **Criação de Novas Quadras:** O usuário clica e arrasta com o **botão direito do mouse** em uma área livre do papel de parede para desenhar o retângulo da nova Quadra (o botão esquerdo permanece reservado para seleção padrão).
- *Nota do Tech Lead:* Janelas independentes evitam bloquear cliques acidentais no papel de parede nativo do Windows.

### 4. Ciclo de Vida da Quadra: Fechar, Esconder e Excluir
- **Decisão ao clicar no "X":** Abre uma caixa de diálogo perguntando se o usuário deseja **"Esconder"** ou **"Excluir"**.
  - **Se Excluir:** Os ícones voltam para a Quadra "TUDO" (caso ela ainda exista). Se "TUDO" tiver sido deletada, os ícones saem da visualização, mas nunca do disco. O usuário pode readicioná-los via botão "+".
  - **Se Esconder:** A janela é fechada, mas seu estado permanece salvo.
- **System Tray (Bandeja do Sistema / Ícone junto ao relógio):** Central de comando do app. Conterá a opção *"Mostrar Quadras Escondidas"* (abrindo uma janela para reativar Quadras individualmente ou todas de uma vez).

### 5. Ciclo de Vida da Quadra Padrão (`IsDefault`) & Monitoramento
- **Identificador Técnico:** A Quadra inicial "TUDO" é identificada no domínio internamente por um identificador técnico imutável (ex: propriedade booleana `IsDefault = true` ou flag enum), e **NUNCA pelo título visível**. O usuário é 100% livre para renomeá-la como quiser (ex: "Entrada", "Geral", "Meus Arquivos").
- **Regra de Exclusão vs. "Adicionar Automaticamente":**
  - Se a opção *"Adicionar novos atalhos/arquivos automaticamente"* estiver **MARCADA**, a Quadra padrão **não pode ser excluída diretamente sem aviso**.
  - Ao tentar excluir, o sistema exibe um diálogo com duas opções:
    1. *Cancelar exclusão* (mantendo a Quadra e a adição automática ativas).
    2. *Desmarcar a opção de adição automática e excluir a Quadra*.

### 6. Customização Visual das Quadras (Opção 2 - Flexível)
- **Escopo Visual no MVP:**
  - Título editável inline.
  - Cor de fundo (ARGB/Hex) e controle deslizante de Opacidade/Transparência.
  - Alinhamento do título (Esquerda / Centro) e cor do texto.
  - **Tamanho dos Ícones:** Pequeno (32x32), Médio (48x48) e Grande (96x96 / Jumbo).
- **Implicações Técnicas de Engenharia:**
  - *Extração Win32:* Ícones 32x32 e 48x48 usam `SHGetFileInfo`. O modo Grande (96px+) exige a interface COM `SHGetImageList` com o flag `SHIL_JUMBO` para garantir alta resolução sem pixelização.
  - *Desempenho de UI:* Uso de virtualização de layout (`VirtualizingWrapPanel`) no WPF para evitar travamentos de renderização (lag de re-layout) em Quadras com dezenas de atalhos.
  - *Esquema do JSON:* O modelo de dados de cada Quadra em `quadras.json` deve incluir `iconSize`, `titleColor`, `titleAlignment`, `backgroundColor` e `backgroundOpacity`.

### 7. Motor de Snap Magnético & Posicionamento
- **Modelo de Interação:** **Opção A (Sobreposição Livre com Ímã Elástico)**. As Quadras não agem como blocos sólidos travados. Elas alinham magneticamente quando próximas, mas permitem atravessar livremente se o usuário forçar o movimento.
- **Bordas da Tela (Monitor Snap):** Suporte nativo a snap nas bordas da área de trabalho (`SystemParameters.WorkArea`: topo, rodapé e laterais), facilitando o alinhamento perfeito nos cantos da tela sem esforço manual milimétrico.
- **Espaçamento Configurável (Snap Gap):**
  - O motor de cálculo aceita um parâmetro de espaçamento (*gap* em pixels, ex: 0px para colar literalmente borda com borda, ou 8px para deixar um respiro visual elegante e uniforme).
- **Implementação Matemática:** Cálculo puro de geometria vetorial 1D/2D desacoplado no `DeskQuadra.Application` (testável via testes unitários automatizados sem depender da interface gráfica ativa).

### 8. Menu de Contexto dos Atalhos (Moderno vs. Clássico)
- **Decisão de Produto & Validação:**
  - **Menu Moderno Customizado (WPF Fluent):** Será desenvolvido como uma Prova de Conceito (PoC) inicial para reproduzir fielmente o visual do Windows 11 com alta velocidade e ações diretas.
  - **Critério de Aceite / Fallback:** Este menu moderno será submetido a teste de usabilidade. Caso a experiência, fluidez ou sensação tátil não fiquem exatamente no padrão de excelência esperado, o projeto adotará exclusivamente o **Menu Clássico nativo do Windows Shell (`IContextMenu`)**.
  - **Menu Clássico:** Mantido via P/Invoke como fallback ou atalho *"Mostrar mais opções"*, garantindo acesso a extensões de terceiros (WinRAR, 7-Zip, Git, antivírus).
- **Implicações Técnicas de Engenharia:**
  - *Interoperabilidade COM:* O menu clássico exige invocar `IShellFolder::GetUIObjectOf` e `IContextMenu3` via P/Invoke, repassando mensagens Win32 (`WM_INITMENUPOPUP`, `WM_MENUCHAR`) no hook do `HwndSource` do WPF.
  - *Mitigação de Travamento:* Extensões shell de terceiros podem ser lentas. O menu Moderno protege a fluidez da aplicação no uso cotidiano, enquanto o Clássico é acionado sob demanda.



### 9. Gestão de Múltiplos Monitores e Desconexão de Telas
- **Detecção em Tempo Real:** Escuta do evento Win32 `WM_DISPLAYCHANGE` (capturado via hook de janela no `HwndSource`).
- **Lógica de Acomodação Inteligente (Opção A Aprimorada):**
  - Quando um monitor secundário é desconectado, o sistema tenta reposicionar as Quadras órfãs em áreas livres do monitor principal.
  - **Regra de Esgotamento de Espaço:** Se o monitor principal já estiver cheio (sem espaço útil para alocar as Quadras sem sobreposição caótica), a Quadra é colocada em estado oculto (`IsHidden = true`) com a marcação de contexto `HiddenReason.DisplayDisconnected`.
  - **Preservação de Estado:** A posição original, tamanho e ID do monitor de origem ficam preservados em `quadras.json`.
  - **Acesso pelo Usuário:** As Quadras ocultadas por falta de espaço aparecem identificadas na lista de *"Quadras Escondidas"* no menu da bandeja (System Tray).
### 10. Modo Roll-up (Gaveta / Recolhimento no Local) & Acessibilidade Touch
- **Decisão de Produto:** A Quadra recolhe seu corpo ao receber duplo clique no título, preservando sua posição original e exibindo apenas a barra de título compacta.
- **Modos de Reabertura (Configuráveis):**
  - *Duplo clique no título.*
  - *Botão dedicado de expansão/recolhimento:* Posicionado no canto da barra de título (ícone chevron/seta).
  - *Hover (Efeito Peek):* Abre ao passar o ponteiro do mouse sobre a barra.
- **Engenharia para Dispositivos Portáteis & Touchscreen (Steam Deck, ROG Ally, 2-em-1):**
  - Em telas de toque não existe evento de *hover* e o duplo clique com o dedo é inconsistente.
  - O botão de expansão no canto da barra terá uma área de toque (*hitbox*) otimizada (mínimo de 32x32px), tornando o DeskQuadra nativamente acessível para telas de toque e PCs portáteis sem prejudicar o visual minimalista no mouse.
- **Requisito Técnico de Usabilidade (Hover):**
  - Implementação de um temporizador de *Debounce* de saída (~350ms). Se o mouse escapar brevemente da borda da janela, a Quadra não se fecha na cara do usuário abruptamente.




### 11. Ocultação Rápida Global ("Modo Desktop Limpo")
- **Decisão de Produto (Opção A):**
  - Duplo clique em qualquer área vazia do papel de parede alterna a visibilidade de todas as Quadras ativas (efeito fade-out/fade-in suave).
  - Outro duplo clique restaura instantaneamente todas as Quadras ao estado anterior.
  - Opção configurável nas configurações gerais (pode ser ativada/desativada).
- **⚠️ ALERTA CRÍTICO DE ENGENHARIA & ARMADILHA TÉCNICA:**
  - *O Risco Amador:* NUNCA criar uma janela transparente invisível cobrindo o desktop para capturar o duplo clique. Isso causaria o "sequestro" de cliques do sistema, quebrando o clique com botão direito do Windows nativo, a seleção retangular de arquivos e a interação com outros monitores.
  - *A Solução Cirúrgica:* Hook global de mouse de baixo nível (`WH_MOUSE_LL` via `SetWindowsHookEx`).
  - *Condição de Disparo:* O hook só processa o duplo clique se `WindowFromPoint(cursorPos)` for exatamente a janela de fundo do Explorer (`Progman` ou `WorkerW`). Se o duplo clique ocorrer dentro de qualquer outro software em uso ou sobre uma Quadra, o evento é ignorado e repassado sem qualquer atraso.

### 12. Bloqueio de Layout ("Travar Quadras" - Global & Individual)
- **Decisão de Produto:** Suporte a dois níveis de bloqueio:
  - **Individual:** Clicar com botão direito na barra de título da Quadra e selecionar *"Travar esta Quadra"*.
  - **Global:** Opção no menu da bandeja (System Tray) para *"Travar todas as Quadras"*.
- **Comportamento quando travada:**
  - Desativa os manipuladores de redimensionamento de borda (*resize grips/adorners*).
  - Desativa o arraste de movimentação pela barra de título.
  - A rolagem interna com o scroll do mouse e os cliques nos ícones continuam funcionando 100% normalmente.
- **Requisito Crítico de UX (Feedback Visual):**
  - Exibição de um pequeno ícone de **cadeado fechado** discreto na barra de título quando a Quadra estiver travada, evitando que o usuário confunda o bloqueio com um bug de travamento do programa.

### 13. Ciclo de Vida de Saída (Exit) & Restauração do Desktop
- **Decisão de Produto (Opção A):**
  - Ao clicar em *"Sair"* no menu da bandeja (System Tray), todas as janelas de Quadras são salvas e fechadas, e a visibilidade dos ícones nativos do Windows é **restaurada instantaneamente**.
  - *A Proteção Técnica:* Inscrição de handlers globais de emergência (`AppDomain.CurrentDomain.UnhandledException` e `DispatcherUnhandledException`). Antes de qualquer encerramento abrupto, o sistema executa a chamada Win32 `ShowWindow(hDesktopListView, SW_SHOW)`, garantindo que os ícones originais do Windows sempre reapareçam, mesmo em caso de falha catastrófica.

### 14. Inicialização com o Windows (Startup / Boot)
- **Decisão de Produto:**
  - **Ativado por padrão** no primeiro uso para garantir a sensação de sistema operacional integrado.
  - **Toggle nas Configurações Gerais do App:** Uma opção clara *"Iniciar DeskQuadra com o Windows"* que permite ao usuário ligar ou desligar o recurso a qualquer momento com um único clique.
  - Sobe em modo 100% silencioso diretamente para a bandeja do sistema (System Tray), restaurando as Quadras sem telas de carregamento ou janelas intrusivas.
- **Decisão Arquitetural (Task Scheduler Único):**
  - **Ponto único de boot:** Windows Task Scheduler com prioridade 4 e delay zero (`PT0S`).
  - **Sem redundância de boot:** Registry Run foi descartado após análise de custo-benefício. Task Scheduler é serviço crítico do Windows; se falhar, o sistema inteiro está comprometido. Redundância aumentaria complexidade sem benefício prático mensurável.
  - **Mutex simples:** Primeira instância vence, segunda morre imediatamente. Sem timeout ou flags de origem.
- **Engenharia de Performance de Boot (Startup Impact Mínimo):**
  - *O Risco:* O Gerenciador de Tarefas do Windows classifica programas que demoram no boot como "Impacto Alto na Inicialização", gerando atrito com o usuário.
  - *A Solução Técnica:* Task Scheduler com prioridade 4 e delay zero. O carregamento do `quadras.json` e dos ícones é feito de forma assíncrona e ultrarrápida (< 30ms), mantendo o impacto no boot classificado como "Nenhum / Baixo".

### 15. Ordenação e Alinhamento de Ícones dentro da Quadra
- **Decisão de Produto:** Suporte a dois modos de organização selecionáveis via menu de contexto da Quadra (*"Classificar por..."*):
  - **Modo Automático (Padrão):** Submenu com opções de classificação instantânea:
    - *Por Nome (Alfabética)*
    - *Por Tipo de Arquivo / Extensão*
    - *Por Data de Modificação (Mais Recentes)*
  - **Modo Manual (Livre no Grid):** Permite que o usuário arraste e reordene os itens como desejar.
- **Engenharia de Layout (Slot-based Reordering):**
  - *Garantia de Grid:* No modo manual, os itens não flutuam soltos em pixels arbitrários; eles se encaixam estritamente em células (*slots*) do grid, garantindo alinhamento estético constante.
  - *Performance com WPF:* No modo automático, o motor utiliza `ICollectionView` com `SortDescriptions` em memória para ordenar centenas de arquivos instantaneamente, sem gerar I/O desnecessário de gravação no disco.
  - *Modelo de Dados:* A entidade `Quadra` persiste `sortMode` (enum) e a lista de itens preserva o índice posicional `orderIndex` no modo manual.

### 16. Formato de Distribuição & Localização dos Dados (MVP)
- **Decisão de Produto:** 
  - **MVP:** Executável único portátil (*Portable .exe*). Sem necessidade de instalação para rodar e testar.
  - **v1.0 Oficial (Pós-MVP):** Criação de instalador nativo do Windows (Inno Setup / MSI) com assistente de boas-vindas e desinstalador limpo.
- **Engenharia de Permissões e Persistência Segura (`%APPDATA%`):**
  - *O Risco:* Se o aplicativo portátil tentar gravar `quadras.json` na mesma pasta do `.exe` (ex: `C:\Program Files` ou pastas corporativas restritas), o Windows bloqueará a gravação com erro de permissão negada (`UnauthorizedAccessException`).
  - *A Solução Arquitetural:* O `quadras.json` será sempre persistido em:
    `%APPDATA%\DeskQuadra\quadras.json` (resolvido programaticamente via `Environment.SpecialFolder.ApplicationData`).
  - *Benefício Adicional:* O usuário pode atualizar o executável portátil substituindo o `.exe` por uma versão nova e seu layout e atalhos permanecerão 100% intactos e preservados.

### 17. Sincronização em Tempo Real com o Sistema de Arquivos (Live Sync)
- **Decisão de Produto:** Sincronização ativa bidirecional de eventos do sistema operacional.
- **Resolução de Caminhos do Desktop (Três Fontes Físicas):**
  - *Desktop do Usuário:* Obtido via `SHGetKnownFolderPath(FOLDERID_Desktop)` para resolver automaticamente redirecionamentos para o **OneDrive** (ex: `C:\Users\<User>\OneDrive\Desktop`).
  - *Desktop Público (All Users):* `FOLDERID_PublicDesktop` (`C:\Users\Public\Desktop`), capturando atalhos de instaladores de programas e jogos.
  - *Desktop Local Legado:* Monitoramento de fallback caso a sincronização do OneDrive seja alternada pelo usuário.
- **Regras de Exibição de Arquivos Ocultos e de Sistema:**
  - O DeskQuadra respeita as preferências globais do Windows Explorer (`HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`):
    - Se o usuário marcou *"Mostrar arquivos ocultos"* no Windows, a Quadra exibe os arquivos com atributo `Hidden` com visual semitransparente sutil (comportamento nativo do Explorer).
    - Se marcou *"Ocultar arquivos protegidos do sistema (Recomendado)"*, arquivos como `desktop.ini` e temporários de edição do Office (`~$*.*`) permanecem estritamente filtrados e ocultos.
- **Estabilização de Eventos (Debounce):**
  - Aplicação de buffer/debounce de ~250ms no `FileSystemWatcher` para neutralizar oscilações e evitar efeito de piscar (*flicker*) durante operações complexas de escrita/renomeação no disco.

### 18. Mecânica de Arraste entre Quadras (Drag & Drop: Mover vs. Duplicar)
- **Semântica Padrão (Sem tecla modificadora):** Operação de **MOVER** o atalho/item entre Quadras. O arquivo físico permanece único no disco; apenas a sua vinculação muda da Quadra de origem para a Quadra de destino.
- **Operação de Duplicação Física (`Ctrl + Drag`):**
  - **Fidelidade ao Windows Explorer:** Se o usuário segurar a tecla `Ctrl` durante o arraste e soltar, o DeskQuadra cria uma **cópia física real** do arquivo ou atalho no disco (ex: `Documento - Copia.docx`).
  - **Uso da API Win32 `IFileOperation`:** A duplicação física utiliza a interface COM nativa do Windows Shell para garantir:
    - Suporte nativo ao Desfazer do Windows (`Ctrl + Z`).
    - Nomenclatura automática de colisões (*" - Copia"*, *" - Copia (2)"*).
    - Barra de progresso nativa do Windows caso o usuário copie um arquivo grande de múltiplos gigabytes.
  - **Destino Fora de Quadras:** Se o usuário soltar a cópia em uma área livre do papel de parede (fora de qualquer Quadra), o novo arquivo cai na Quadra "TUDO" (se ativa) ou fica no desktop físico aguardando inclusão manual via botão "+".
- **Comportamento *Spring-Loaded* em Quadras Recolhidas (Roll-up):**
  - Ao passar o mouse arrastando um arquivo sobre a barra de título de uma Quadra recolhida, ela se expande temporariamente após um atraso deliberado de ~400ms.
  - Se o usuário soltar o arquivo, o item é inserido e a Quadra permanece no estado expandido.
  - Se o usuário retirar o cursor sem soltar o arquivo, a Quadra se recolhe novamente de forma automática.

### 19. Criação de Quadras Desenhando com o Botão Direito
- **Fluxo de Interação:**
  - O usuário pressiona o botão direito do mouse sobre uma área livre do papel de parede e arrasta para delimitar o retângulo da nova Quadra.
  - Um retângulo translúcido suave acompanha o cursor em tempo real.
- **Menu Dual de Confirmação ao Soltar (Decisão B):**
  - Ao soltar o botão direito, surge um menu minimalista no ponto do cursor com duas opções claras:
    1. `[ + Criar Quadra Aqui ]`: Instancia a Quadra com as dimensões desenhadas e coloca o título em modo de edição imediata.
    2. `[ 🗔 Cancelar e Exibir Menu do Windows ]`: Cancela o retângulo e abre instantaneamente o menu de contexto nativo do Windows (para quem pretendia apenas usar o desktop normal).
- **Engenharia de Proteção contra Cliques Acidentais (*Drag Threshold*):**
  - *O Problema:* Ao dar um clique simples com o botão direito para ver o menu do Windows, a mão do usuário move naturalmente 1 ou 2 pixels.
  - *A Solução:* Aplicação do limiar métrico do Windows (`SM_CXDRAG` / `SM_CYDRAG`, calibrado em ~15px). Se o movimento do mouse for menor que 15 pixels, o DeskQuadra considera um **clique simples** e repassa imediatamente para o Windows abrir o menu normal, sem abrir o retângulo ou o diálogo de criação de Quadra.

### 20. Resiliência contra Reinicialização do `Explorer.exe` (Auto-Healing & Watchdog)
- **Cenário de Estresse:** O processo do Windows Explorer sofre um *crash* ou reinicialização (algo frequente durante atualizações, travamentos de drivers de vídeo ou bugs do Windows 11).
- **Mecanismo Primário de Auto-Cura (In-Memory Healing):**
  - O DeskQuadra registra e escuta a mensagem de broadcast do sistema operacional `RegisterWindowMessage("TaskbarCreated")`.
  - Ao receber a mensagem, o sistema aguarda a estabilização do novo desktop (~150ms), reextrai o identificador `HWND` do novo `WorkerW` gerado pelo Windows e reancora (`SetParent`) todas as Quadras ativas de forma transparente e instantânea.
- **Mecanismo de Fallback (Watchdog / Cold Restart):**
  - **Temporizador de Integridade:** Se em até 3,0 segundos após a notificação o `WorkerW` válido não for localizado ou as janelas não confirmarem reancoragem bem-sucedida (estado de corrupção do subsistema gráfico do Explorer):
    - O DeskQuadra salva o estado atual e dispara uma reinicialização limpa do próprio processo (`Process.Start(Environment.ProcessPath)` com a flag `--recovery`), encerrando a instância zumbi com `Environment.Exit(0)`.
  - **Garantia:** O software nunca fica "congelado" ou invisível após falhas do sistema operacional.
- **Nota sobre o Guardian:** O `DeskQuadra.Guardian.exe` monitora o PID do processo pai e **apenas restaura ícones do desktop via `ShowWindow(SW_SHOW)`**. Ele não reinicia o app principal. Sua função é exclusivamente ser um restaurador de ícones + botão de pânico (`Ctrl+Shift+Alt+Q`).

### 21. Integridade de Persistência, Backups Rotativos & Recuperação de Queda de Energia
- **Ciclo Transacional de Gravação (Double-Buffering):**
  1. *Gatilho com Debounce:* Alterações de layout acumulam em memória e aguardam ~400ms de inatividade do mouse.
  2. *Escrita Transacional:* O novo JSON é serializado primeiramente no arquivo temporário `quadras.json.tmp`.
  3. *Rotação Segura do Backup:*
     - O arquivo atual `quadras.json` é copiado/rotacionado para `quadras.json.bak` (garantindo sempre uma cópia estável garantida).
     - O arquivo `quadras.json.tmp` é renomeado atomicamente para `quadras.json`.
     - O arquivo `.tmp` é purgado ao concluir a transação com sucesso.
- **Detecção de Queda de Energia / Crash no Boot (Arquivo `.tmp` Órfão):**
  - Se ao inicializar o DeskQuadra for encontrado um arquivo `quadras.json.tmp` residual no disco, isso indica que o computador sofreu corte repentino de energia no meio de uma gravação.
  - **Validação de Integridade Automatizada:**
    - O sistema valida a sintaxe do `.tmp`:
      - *Se o `.tmp` estiver íntegro e for mais recente:* Exibe um diálogo amigável de recuperação: *"O Windows foi encerrado inesperadamente durante sua última organização. Deseja restaurar o layout mais recente?"* com opções claras: `[ Restaurar Recente ]` ou `[ Manter Anterior ]`.
      - *Se o `.tmp` estiver truncado ou corrompido:* O arquivo quebrado é descartado silenciosamente e o sistema sobe a versão saudável `quadras.json` (ou `.bak`), sem gerar mensagens de erro confusas para o usuário.

### 22. Otimização de Boot & Eliminação de Flicker (Zero-Flicker Startup)
- **Problema de Usabilidade no Boot do Windows:**
  - O `Explorer.exe` renderiza os ícones da área de trabalho antes da execução dos aplicativos de inicialização do usuário. Se o utilitário atrasar, o usuário visualiza os ícones nativos surgirem e sumirem abruptamente (*flicker* perceptível).
- **Técnica de Ocultação Ultra-Precoce (Ponto de Entrada `Main`):**
  - No método de entrada da aplicação (`Main()`), **antes** de instanciar o pipeline gráfico do WPF ou inicializar bibliotecas pesadas de XAML, o DeskQuadra dispara a chamada Win32 `ShowWindow(hDesktopListView, SW_HIDE)`.
  - **Tempo de Execução:** Menos de 2 milissegundos a partir do início da thread, limpando a área de trabalho antes que o olho humano perceba qualquer transição.
- **Transição Visual Suave:**
  - As Quadras são renderizadas em memória e surgem no desktop com um efeito suave de *Fade-in* (~200ms), entregando acabamento profissional superior ao padrão do mercado.

### 23. Botão de Pânico Global (Emergency Panic Hotkey / Kill-Switch)
- **Motivação:** Garantir ao usuário controle soberano absoluto e fuga de qualquer cenário de pane, travamento de tela ou estado de "tela vazia".
- **Implementação Técnica (Win32 Global Hotkey):**
  - Registro de atalho global do sistema via `RegisterHotKey` na inicialização.
  - **Combinação Oficial:** `Ctrl + Shift + Alt + Q` (ou `Ctrl + Shift + Alt + D`), garantindo zero colisão com atalhos de jogos, navegadores ou atalhos nativos do Windows.
- **Protocolo de Execução do Pânico:**
  1. *Restauração Forçada:* Invoca imediatamente `ShowWindow(hDesktopListView, SW_SHOW)` na camada nativa do Windows Explorer.
  2. *Notificação de Segurança:* Dispara uma notificação sutil na bandeja do sistema: *"DeskQuadra encerrado em modo de emergência. Sua área de trabalho foi restaurada."*
  3. *Encerramento Limpo:* Executa `Environment.Exit(0)` sem travar o sistema.
- **Divulgação de Segurança:** O atalho é explicitamente informado na barra de status da janela de Configurações e no guia de primeiro uso.
- **Errata 2026-09-27: dono atual é o Guardian.** O texto original desta seção (mantido acima para histórico) descrevia `RegisterHotKey` na inicialização do app — está desatualizado. Implementação atual: o pânico é do processo Guardian (Form oculto, `Ctrl + Shift + Alt + Q`, encerramento gracioso de 2,5s + kill por PID, log em `%APPDATA%\DeskQuadra\guardian.log`).

### 24. Design System, Acessibilidade e Contraste (Padrão `ui-ux-pro-max` / WCAG)
- **Princípio:** *Smart Defaults* (padrão de fábrica inteligente) com *Override* total pelo usuário.
- **Legibilidade de Texto sobre Papéis de Parede Arbitrários (WCAG AA 4.5:1):**
  - **Comportamento Padrão (Ativo):** Rótulos de atalhos e títulos de Quadras utilizam renderização com sombra suave projetada (*DropShadow* acelerado por hardware: `BlurRadius = 2`, `ShadowDepth = 1`, `Opacity = 0.8`), garantindo legibilidade imediata em 100% dos papéis de parede (claros, escuros ou com alto contraste visual).
  - **Override do Usuário:** O usuário pode desativar a sombra e escolher cores sólidas personalizadas via seletor de cores da Quadra.
- **Tratamento de Nomes Longos de Arquivos (Truncamento & Tooltip):**
  - Nomes extensos são limitados a no máximo **2 linhas com reticências** (*text-overflow: ellipsis*) no grid de ícones.
  - Tooltip informativo surge em *hover* (ou toque contínuo) exibindo o nome completo do arquivo, tipo e tamanho.
- **Arquitetura de Densidade Dual: Modo Normal (Mouse) vs. Modo Touch (Tablet/Portáteis):**
  - *O Problema de Usabilidade Apontado pelo PO:* Em uma barra de título compacta de desktop (~28px), uma hitbox invisível de 44x44px invade a área de arraste do mouse, causando cliques acidentais e recolhimento involuntário da Quadra quando o usuário tenta apenas movê-la.
  - *A Solução Arquitetural (Sistema de Densidade Dinâmica via WPF Resources):*
    1. **Modo Normal (Mouse & Teclado - Padrão):**
       - Barra de título compacta (altura de ~28px).
       - Alvo de clique do chevron restrito a 24x24px (sem vazamento para a área de arraste).
       - Espaçamento denso e eficiente entre atalhos no grid.
    2. **Modo Touch / Portátil (Steam Deck, ROG Ally, Tablets):**
       - Barra de título expandida (altura de ~42px).
       - Alvo de clique amplo de 44x44px (fácil para toque de polegar/indicador).
       - Espaçamento (*padding*) entre os ícones aumentado para evitar toques duplos acidentais com os dedos.
    3. **Controle de Ativação:**
       - *Automático:* Detecção de tela de toque via Win32 `GetSystemMetrics(SM_DIGITIZER)`.
       - *Manual:* Toggle nas Configurações Gerais: `[ Auto | Normal (Mouse) | Touch (Tablet/Portátil) ]`.

### 25. Metodologia de Execução: Fases Iterativas e Testáveis (Goal-Driven Execution)
- **Princípio:** Nenhuma fase de código é entregue sem um executável testável ou resultado visual concreto para validação pelo PO.
- **Ciclo de Trabalho:**
  1. *Definição do Objetivo:* Cada fase resolve uma fatia vertical funcional com critério de sucesso claro.
  2. *Implementação Cirúrgica:* Código mínimo e desacoplado que resolve exatamente a meta da fase.
  3. *Checkpoint de Validação:* O Tech Lead disponibiliza a versão compilada, e o PO testa a funcionalidade na prática no seu próprio Windows antes de liberar o próximo passo.


















- **Comunicação PO & Tech Lead:**
  - O PO utiliza vocabulário e referências do ecossistema JavaScript/Web para ilustrar ideias.
  - O Tech Lead traduz essas ideias para os padrões idiomáticos de **C# / .NET 8** (Clean Architecture, tipos fortemente tipados, LINQ, Task Parallel Library e padrões MVVM/WPF).
  - **Diretriz de Conduta do Tech Lead:** O Tech Lead atua com **estrita tecnicidade, honestidade intelectual e pragmatismo**, sem bajulação ou foco em agradar. Riscos, complexidades desnecessárias e falhas de arquitetura devem ser apontados diretamente e sem rodeios.

---

## [Sessão 26] Identidade Visual Windows 11 Fluent & Comandos Rápidos de Contexto

### 1. Diretriz de Compatibilidade e Verificação em Camadas
- **Detecção Confiável:** A leitura da versão e build do sistema deve consultar a chave de registro `CurrentBuildNumber` do Windows NT, nunca dependendo de manifestos enganosos de aplicativo.
- **Detecção de Capacidade de Hardware (GPU / Drivers Básicos):**
  - O aplicativo deve checar ativamente o nível de aceleração gráfica por hardware (`RenderCapability.Tier >> 16 >= 2`) e se a Composição do DWM está habilitada (`DwmIsCompositionEnabled`).
  - Em ambientes sem aceleração adequada (drivers genéricos *Microsoft Basic Display Adapter*, sessões de Área de Trabalho Remota/RDP ou GPU legada), os efeitos pesados de desfoque/backdrop são bloqueados automaticamente para preservar a fluidez do sistema operacional e evitar travamentos.
- **Hierarquia de Execução & Fallback:**
  1. *Windows 11 Build 22621+ com aceleração Tier 2*: Aplicação do método mais moderno nativo do DWM (`DWMWA_SYSTEMBACKDROP_TYPE` Acrylic/Mica).
  2. *Windows 10 / Windows 11 21H2 com aceleração Tier 2*: Aplicação do método clássico do compositor (`SetWindowCompositionAttribute` com `AccentPolicy`).
  3. *Incompatibilidade, erro ou drivers básicos*: Fallback imediato e suave para o tema translúcido existente sem qualquer exceção ou falha de tela.
- **Ordem de Implementação Mandatória (Decisão Estratégica do PO):**
  - Implementar e validar exaustivamente primeiro o **método clássico** (`SetWindowCompositionAttribute`). Com ele estável e homologado na máquina de testes, avança-se para o **método moderno**.

### 2. Configurações do Aplicativo (`SettingsWindow`)
- O switch/toggle de efeitos visuais de transparência e desfoque só deve ser visível na interface de configurações se a máquina do usuário possuir SO e hardware compatíveis. Caso contrário, a opção permanece oculta para não gerar expectativas ou erros.
- A preferência do usuário é persistida em `settings.json` com comportamento best-effort.

### 3. Ações Rápidas e Ícones no Menu de Contexto
- **Barra Superior de Ações Rápidas:**
  - Posicionada no topo do menu de contexto de atalhos e itens.
  - Inicialmente contempla: **Recortar**, **Copiar**, **Colar**, **Renomear** e **Excluir**.
  - A construção do layout deve preceder a estilização dos ícones, e os ícones devem preceder a lógica individual de cada operação.
- **Identidade Visual e Respeito Estrito à Densidade Dual:**
  - **Modo Mouse:** Ações compactas (~28-30px), foco na agilidade e proximidade com o cursor.
  - **Modo Touch:** Alvos de toque generosos respeitando a norma de acessibilidade (mínimo de 44x44px), espaçamento para evitar toques acidentais.
  - Ícones vetoriais monocromáticos renderizados via geometrias XAML (`Path`) compartilhadas no dicionário de temas para garantir nitidez cristalina em qualquer escala de DPI e tema.
- **Checklist e Execução por Fases:**
  - Documentação viva e detalhada em `docs/PLANO_VISUAL_WIN11_E_ACOES_RAPIDAS.md`.
  - Commits locais a cada passo concluído; envio (`push`) apenas após homologação direta pelo PO.

---

## [Sessão 27] Modernização da Barra de Rolagem (ScrollBar Fluent Windows 11)

### 1. Diretriz de Design e Geometria do Thumb
- **Eliminação de Distorções Elípticas:** No WPF, o uso de `CornerRadius` arbitrário (ex: 99) em elementos retangulares de espessura reduzida gera pontas afiladas e deformações ("agulhas"). A regra de engenharia do DeskQuadra determina que o `CornerRadius` deve ser rigorosamente **metade exata da largura** do Pill (`CornerRadius = Width / 2`), garantindo curvatura semicircular perfeitamente simétrica em repouso e hover.
- **Transições e Densidade Dual:**
  - **Modo Mouse:** Espessura de 4px em repouso (raio 2px), expandindo para 8px no hover (raio 4px). Trilha translúcida discreta.
  - **Modo Touch:** Espessura de 6px em repouso (raio 3px), expandindo para 12px no hover/ativo (raio 6px), com largura total adaptada dinamicamente via `DensityResolver`.
- **Posicionamento Overlay:**
  - A barra é desenhada em sobreposição flutuante (`OverlayScrollViewerStyle`), não consumindo a largura do viewport nem provocando re-wrap do `WrapPanel` ou conflitos com o snap magnético.
  - Alinhamento refinado à direita com margem calibrada (`Margin="0,4,-3,4"`) para equilibrar o respiro visual com a borda da janela (~6px em repouso, ~4px no hover), sem excesso de espaço vazio e sem encostar na borda.

### 2. Separação Estrita de Interação (Mouse vs. Touch)
- **ScrollBar Exclusiva para Mouse:** A ScrollBar lateral atende unicamente a eventos de mouse (arrasto do thumb, clique na trilha e roda do mouse).
- **Toque Direto no Miolo da Quadra:** A rolagem via toque com o dedo opera exclusivamente na área interna da Quadra (estilo smartphone), deslizando os atalhos com inércia e sem conflitar com a barra lateral.

---

## [Sessão 28] Inicialização de Alta Prioridade (Zero-Delay Boot & Fura-Fila do Windows)

### 1. Diagnóstico do Throttling do Windows Explorer
- **O Problema:** Aplicativos registrados unicamente na chave `HKCU\...\Run` sofrem atraso intencional de 10 a 30 segundos imposto pelo Windows Explorer (*Startup Delay*) para liberar CPU/disco para a barra de tarefas.
- **A Solução em Três Camadas Integradas:**
  1. **Windows Task Scheduler (Prioridade 4 & Delay 0):** Registro programático via API COM `Schedule.Service` de uma tarefa com gatilho `AtLogon`, `Delay = PT0S`, `ExecutionTimeLimit = PT0S` e flags `DisallowStartIfOnBatteries = false` (compatível com laptops e Steam Deck em bateria). Roda sob o token interativo do usuário sem exigir UAC/elevação. O Task Scheduler inicia o processo instantaneamente no logon, furando o atraso de 10-30s do Explorer.
  2. **Explorer Serialize (Zero Delay):** Configuração de `StartupDelayInMSec = 0` (DWORD) em `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize`, instruindo o Explorer a não reter a inicialização de programas da sessão do usuário.
  3. **Chave `Run` Espelhada:** Manutenção do registro na chave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` para que o DeskQuadra continue visível na aba "Aplicativos de Inicialização" do Gerenciador de Tarefas do Windows.

### 2. Ponto de Entrada Ultra-Precoce (`Program.cs`) e Zero-Flicker
- **Concretização da Sessão 22:**
  - Ponto de entrada explícito `Program.cs` com `[STAThread] static void Main(string[] args)`.
  - Checagem da trava de instância única (`Mutex`) em menos de 1ms, encerrando instâncias secundárias silenciosamente antes de carregar o WPF ou alocar memória.
  - Invocação da chamada Win32 `QuickHideDesktopIcons()` em menos de 2ms, ocultando os ícones da área de trabalho antes da inicialização do pipeline gráfico do WPF e XAML.
- **Auto-Configuração no Primeiro Boot:**
  - O aplicativo verifica se o usuário optou por desativar o início automático (`StartupDisabledByUser` em `settings.json`). Caso contrário, se o app não estiver registrado para autostart, auto-configura as 3 camadas no primeiro boot de forma transparente e resiliente.

---

## [Sessão 29] Resiliência de Desktop, Boot Seguro & Arquitetura Safety Net (Tolerância Zero a Ícones Ocultos)

### 1. Diagnóstico do Problema & O Paradigma do Desktop Seguro
- **O Problema Diagnosticado:** Se o computador for desligado, se houver queda de energia, ou se o DeskQuadra for encerrado/cair abruptamente, o usuário não pode sob hipótese alguma iniciar o Windows com a área de trabalho vazia. Ele precisa poder ver e clicar em seus ícones normalmente enquanto o sistema operacional carrega, sem depender de comandos complexos caso o DeskQuadra demore a subir ou não inicialize.
- **A Mudança de Paradigma:**
  - O Windows Explorer SEMPRE inicia com os ícones visíveis por padrão (`HideIcons = 0`).
  - É terminantemente proibido persistir `HideIcons = 1` no registro do Windows (`HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\HideIcons`). O DeskQuadra atua exclusivamente em memória de janelas via Win32 `ShowWindow(hListView, SW_HIDE)` e sanitiza o registro garantindo `HideIcons = 0`.
  - Qualquer terminação do processo (saída graciosa pelo tray, desligamento/logoff do Windows via `SessionEnding`, crash em exception handler, ou kill externo) deve invocar Win32 `ShowWindow(SW_SHOW)`.

### 2. Estratégia de Defesa em Profundidade contra Falha Simultânea
- **O Cenário de Morte Simultânea:** Caso o usuário finalize tanto o processo principal `DeskQuadra.exe` quanto o vigilante `DeskQuadra.Guardian.exe` simultaneamente via Gerenciador de Tarefas do Windows, nenhum código em memória roda e os ícones ficariam invisíveis.
- **Arquitetura de Resiliência Definida (Decisão PO & Tech Lead):**
  1. **Exception Handlers Estáticos:** Chamada de emergência estática `NativeDesktopIconService.EmergencyRestoreIcons()` em `AppDomain.UnhandledException` e `DispatcherUnhandledException`.
  2. **Tratamento de `SessionEnding`:** Captura do encerramento da sessão do Windows (`SystemEvents.SessionEnding` e `Application.SessionEnding`), salvando o layout e exibindo os ícones nativos instantaneamente.
  3. **Processo Guardião (`DeskQuadra.Guardian`):** Mantido como vigilante de primeira linha (delay 5-10s) e botão de pânico global (`Ctrl + Shift + Alt + Q`).
  4. **Novo Executável Dedicado `DeskQuadra.Restorer.exe`:** Binário C# ultraleve (~25KB) independente do app principal e imune a bloqueios de script PowerShell. Se detectar processos ausentes com ícones escondidos, restaura via `ShowWindow(SW_SHOW)` e reinicia o `DeskQuadra.exe` com a flag `--recovered`.
  5. **Notificação de Auto-Recuperação:** O app principal, ao iniciar com `--recovered`, exibe notificação/toast informativa explicando que foi recuperado de uma interrupção inesperada e preservou os ícones.
  6. **Task Scheduler Safety Net:** Tarefa agendada nativa (`DeskQuadra Safety Restore`) com frequência de **1 minuto** que executa o `Restorer.exe` silenciosamente em segundo plano tanto na versão instalada quanto na versão portátil.
  7. **Atalho de Emergência no Menu Iniciar:** Criado em `%APPDATA%\Microsoft\Windows\Start Menu\Programs\DeskQuadra\Restaurar Ícones do Desktop.lnk` apontando para o Restorer com flag `--force`.
  8. **Camadas Específicas para Instalação Completa (`!isPortable`):**
     - **Windows Service Dedicado (`DeskQuadra.Service`):** Serviço Windows independente com ciclo de 30 segundos como redundância rápida.
     - **Shell Extension no Menu de Contexto:** Opção "Restaurar Ícones do Desktop (DeskQuadra)" ao clicar com o botão direito no papel de parede.
  9. **Proteção em Modo Seguro (`SafeMode`):** Se iniciado em Modo Seguro (`SystemInformation.BootMode != Normal`), restaura os ícones, exibe aviso e encerra sem subir a interface pesada.
- **Plano de Ação e Fases Testáveis:** Documentação completa estruturada em 4 fatias verticais no documento vivo `docs/PLANO_RESILIENCIA_ICONES_E_SAFETY_NET.md`.

---

## [Sessão 30] Ações em Lote na Seleção Múltipla (Excluir, Remover, Abrir e Atalhos de Teclado)

### 1. Diagnóstico do Comportamento
- Ao selecionar múltiplos itens (via caixa de seleção / marquee, `Ctrl`+clique ou `Shift`+clique) e acionar a ação rápida de contexto "Excluir", o sistema resolvia apenas o item individual sob o cursor (`ResolveTargetItem`), excluindo apenas 1 arquivo em vez de todos os itens selecionados.

### 2. Resolução Arquitetural & Fidelidade ao Windows Shell
- **Resolução Unificada de Alvo (`ItemSelectionResolver`):**
  - Se múltiplos itens estiverem selecionados e o clique com botão direito ocorrer sobre um item que faz parte da seleção (ou via tecla de atalho): todas as operações em lote aplicam-se ao conjunto selecionado completo.
  - Se o clique ocorrer sobre um item fora da seleção: o foco e a operação isolam-se exclusivamente a ele (comportamento nativo do Windows Explorer).
- **Ações Contempladas:**
  - **Excluir:** Diálogo contextual com contagem dinâmica (`"{0} itens selecionados: enviar para a Lixeira ou excluir permanentemente?"`). Envia todos os arquivos selecionados para a Lixeira ou exclusão permanente e desvincula-os da Quadra em lote via `QuadraViewModel.RemoveItems`.
  - **Remover da Quadra:** Desvincula todos os itens selecionados da Quadra em lote atualizando índices (`OrderIndex`) uma única vez.
  - **Abrir:** Inicia todos os arquivos ou atalhos selecionados.
  - **Atalho Teclado `Delete`:** Suporte a tecla `Delete` direto na Quadra para excluir itens selecionados quando não estiver em modo de renomeação.


