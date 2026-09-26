# Brainstorming & Linha do Tempo de Decisões — DeskQuadra

> Documento de registro cronológico e vivo das decisões estratégicas, de produto, de arquitetura e de UX tomadas pelo Product Owner (PO) e Tech Lead.

---

## [Sessão 1] Fundação Estratégica & Arquitetura Base
- **Clean-Room Design:** Terminologia proprietária estrita: **"Quadra"**. Tolerância zero para nomes ou referências de softwares concorrentes comerciais.
- **Stack & Performance:** C# / .NET 8 (LTS) e WPF. Foco em pegada ultraleve de memória (~30MB RAM) e aceleração de hardware (DirectX), descartando engines pesadas como Chromium/WebView2.
- **Integração Win32 / Desktop:** Ancoragem das janelas no nível do papel de parede do Windows (`WorkerW` / `Progman`), mantendo as Quadras imunes ao atalho `Win + D` e com ícones nativos do Windows ocultos.
- **Arquitetura Anti-Monólito:** Separação estrita em camadas (Clean Architecture): `Core`, `Application`, `Infrastructure.WindowsShell`, `Infrastructure.Persistence` e `UI.Wpf`.

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
- **Decisão de Produto (Opção A):**
  - **Ativado por padrão** no primeiro uso para garantir a sensação de sistema operacional integrado.
  - **Toggle nas Configurações Gerais do App:** Uma opção clara *"Iniciar DeskQuadra com o Windows"* que permite ao usuário ligar ou desligar o recurso a qualquer momento com um único clique.
  - Sobe em modo 100% silencioso diretamente para a bandeja do sistema (System Tray), restaurando as Quadras sem telas de carregamento ou janelas intrusivas.
- **Engenharia de Performance de Boot (Startup Impact Mínimo):**
  - *O Risco:* O Gerenciador de Tarefas do Windows classifica programas que demoram no boot como "Impacto Alto na Inicialização", gerando atrito com o usuário.
  - *A Solução Técnica:* Injeção na chave de registro do usuário (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`) com a flag de inicialização silenciosa (`--silent` ou `--autostart`). O carregamento do `quadras.json` e dos ícones é feito de forma assíncrona e ultrarrápida (< 30ms), mantendo o impacto no boot classificado como "Nenhum / Baixo".

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














- **Comunicação PO & Tech Lead:**
  - O PO utiliza vocabulário e referências do ecossistema JavaScript/Web para ilustrar ideias.
  - O Tech Lead traduz essas ideias para os padrões idiomáticos de **C# / .NET 8** (Clean Architecture, tipos fortemente tipados, LINQ, Task Parallel Library e padrões MVVM/WPF).
  - **Diretriz de Conduta do Tech Lead:** O Tech Lead atua com **estrita tecnicidade, honestidade intelectual e pragmatismo**, sem bajulação ou foco em agradar. Riscos, complexidades desnecessárias e falhas de arquitetura devem ser apontados diretamente e sem rodeios.




