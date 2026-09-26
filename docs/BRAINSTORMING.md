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







- **Comunicação PO & Tech Lead:**
  - O PO utiliza vocabulário e referências do ecossistema JavaScript/Web para ilustrar ideias.
  - O Tech Lead traduz essas ideias para os padrões idiomáticos de **C# / .NET 8** (Clean Architecture, tipos fortemente tipados, LINQ, Task Parallel Library e padrões MVVM/WPF).
  - **Diretriz de Conduta do Tech Lead:** O Tech Lead atua com **estrita tecnicidade, honestidade intelectual e pragmatismo**, sem bajulação ou foco em agradar. Riscos, complexidades desnecessárias e falhas de arquitetura devem ser apontados diretamente e sem rodeios.




