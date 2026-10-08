# Plano — Drop em Arquivos Container (.zip, .7z, .rar, ...) — DeskQuadra

> Fonte da verdade desta feature. Todo item do README `Drop em arquivos container` sai daqui.
> Execução em fatias verticais testáveis (Goal-Driven), com validação do PO no Windows a cada fatia.

## 1. Objetivo

Permitir arrastar arquivo/pasta de dentro de uma Quadra (ou do Explorer via `FileDrop`) e soltar **em cima de um arquivo container** dentro da Quadra, com o mesmo comportamento do Explorer:

- `.zip` funciona sempre (handler nativo `CompressedFolder` ou fallback nosso).
- `.rar` / `.7z` / `.tar` / `.gz` funcionam **delegando ao app instalado** (WinRAR / 7-Zip) via DropHandler do Shell — sem reimplementar codec proprietário.
- Sem handler instalado: fallback gracioso, nunca quebra o drag existente nem congela a UI.

## 2. Como o Windows faz (pesquisa)

- Por padrão arquivo **não** é drop target. Vira drop target via `DropHandler` registrado em `HKCR\<ProgID>\shellex\DropHandler` = CLSID com `IPersistFile + IDropTarget` — [MS: How to Create Drop Handlers](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-create-drop-handlers).
- Exemplo: `HKCR\CompressedFolder\ShellEx\DropHandler` para `.zip`; WinRAR/7-Zip registram os deles para `.rar/.7z`.
- Para invocar o mesmo handler: `IShellFolder::GetUIObjectOf(..., IID_IDropTarget, ...)` com `cidl = 1` — [MS: GetUIObjectOf](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellfolder-getuiobjectof).
- Receita de referência com `CF_HDROP` + `DragEnter(MK_LBUTTON)` + `Drop(DROPEFFECT_COPY)`: [Dropping Like Files – Zipping Without Libraries](http://blog.airesoft.co.uk/2012/10/dropping-like-files-zipping-without-libraries/).
- RAR-escrita é proprietária RarLab (só WinRAR licencia); 7z-escrita exige SDK externo. Por isso a estratégia é **Shell-first**, nunca codec próprio.

## 3. Regras permanentes do projeto (valem para todas as fatias)

- **Clean-Room:** só o termo `Quadra`. Zero nomes de concorrentes.
- **Stack:** C# / .NET 8 LTS + WPF, ~30MB RAM, DirectX. Sem Chromium/WebView2, sem NuGet pesado novo.
- **Clean Architecture (`docs/PLANO_DE_IMPLEMENTACAO.md`):**
  - `Core`: puro, testável sem WPF/Win32. Detecção de container fica aqui.
  - `Application`: casos de uso / orquestração.
  - `Infrastructure.WindowsShell`: todo P/Invoke/COM (`WorkerW`, `IDropTarget`, `IContextMenu`).
  - `ShellHost`: processo isolado para tudo que toca extensão de terceiro (hang de WinRAR/7-Zip nunca congela a UI).
  - `UI.Wpf`: só chama interface, highlight e cursor.
- **Idioma:** todo texto visível em `Strings.resx` (pt-BR default). Proibido literal pt-BR em C#/XAML novo. Logs/diagnóstico NÃO vão para resx. `Core`/`Application` não dependem de resx da UI.
- **Tema:** `ResourceDictionary` + `DynamicResource`, sem YAML/motor custom. Highlight de container reusa token existente.
- **Persistência:** nada novo em `quadras.json` nesta feature (drop não muda layout, só o conteúdo do arquivo no disco).
- **Goal-Driven:** nenhuma fatia sem executável testável pelo PO no Windows.

## 4. Regras de REUSO (auditoria viva em `docs/AUDITORIA_REUSO.md`)

- Se dá pra reusar, REUSE. Extrair helper/serviço **na 2ª repetição**, nunca antes.
- Unificar somente com comportamento idêntico + testes passando.
- Exceções legítimas com motivo: logs/diagnóstico, Tray WinForms vs menu WPF, defaults de domínio.
- Reusos mapeados nesta feature:
  - `Core/FileSystem/DesktopItemMover.cs` (padrão `CanMoveInto`) + `FileSystemUtils.PathExists` (E2-infra) para o detector.
  - `QuadraWindow.xaml.cs`: `FindItemUnderDragEvent`, `IsCopyRequested`, `OneShotTimer`, `IsDropTarget` — estender `ResolveTargetFolderPath` (~`2800`), `Quadra_DragOver` (~`2829`), `Quadra_Drop` (~`2979`), não duplicar.
  - `Vanara.PInvoke.Shell32` já referenciada + `ShellBindHelper.BindContextMenu` (F1 — preâmbulo COM `SHParseDisplayName → SHBindToParent → GetUIObjectOf` já unificado) + `ShellHostClient` + TTL assimétrico (negativo 60s / positivo 300s) para cache de handler ausente.
  - `FileDuplicator.GetUniquePath`, `JsonStorageDefaults` best-effort IO (D7) no fallback.
  - Densidade dual Mouse/Touch preservada (hitbox 24px vs 44px).
- Atualizar `AUDITORIA_REUSO.md` ao final (F4/F5: só extrair `ToPoint()`/`StripLabel()` se 3º call site surgir).

## 5. Matriz de skills por fatia (`.agent/skills/` — uso mandatório, `PLANO_DE_IMPLEMENTACAO.md:27-39`)

| Fatia | O que faz | Skills |
|---|---|---|
| F1 — Detecção | `Core/FileSystem/ArchiveFormatDetector.IsContainer(path)` puro + xUnit | `coding-guidelines`, `csharp-refactoring`, `run-tests`, `assertion-quality`, `test-anti-patterns`, `test-smell-detection` |
| F2 — Highlight | Estende `ResolveTarget*`, `DragOver` com `Effects=Copy` sempre, `IsDropTarget` no container | `wpf-windows-desktop`, `ui-ux-pro-max`, `ui-visual-validator`, `coding-guidelines` |
| F3 — Forwarder Shell | `IArchiveDropService` no `WindowsShell` + isolamento no `ShellHost` (`IDropTarget` via `GetUIObjectOf`, `CF_HDROP`, STA, timeout/kill, best-effort silencioso) | `dotnet-pinvoke`, `wpf-windows-desktop`, `analyzing-dotnet-performance`, `microbenchmarking` |
| F4 — Fallback | `.zip` via `System.IO.Compression` nativo; `.rar/.7z` via `7zG.exe u` / `WinRAR.exe a` ou toast resx | `msbuild-modernization` (zero dep nova), `coding-guidelines`, `docs-writer` |
| F5 — Validação | PO testa com/sem WinRAR/7-Zip; registra decisão | `docs-writer`, `run-tests` |

## 6. Fatias e critério de aceite do PO

### F1 — Detecção pura (sem UI)
- `ArchiveFormatDetector.IsContainer`: extensão `.zip/.7z/.rar/.tar/.gz/.bz2/.xz` + `File.Exists` (pastas e `.lnk` nunca são container).
- Critério: `dotnet test` verde; nenhum comportamento visível muda.

### F2 — Highlight + cursor (sem escrita)
- Soltar sobre container ainda só rearma timers; mas `DragOver` já acende `IsDropTarget` e força `Copy`.
- Critério: arrastar sobre `.zip/.rar` acende igual pasta, cursor com `+`; soltar não corrompe nada.

### F3 — Shell-first (WinRAR/7-Zip reais)
- `Drop` monta `CF_HDROP` e chama o `IDropTarget` do container via `ShellHost` (fora da UI). Handler mostra o próprio progresso.
- Critério: com 7-Zip/WinRAR instalado, drop adiciona no arquivo; sem app, cai no F4 sem travar.
- **Achados validados em 2026-10-05 (sonda manual no `ShellHost`):**
  - O `IDataObject` das origens vem do próprio Shell (pai comum + `GetUIObjectOf` `IID_IDataObject`) — o `CF_HDROP` montado na mão é recusado pelo `zipfldr` sem nem ser sondado (`DragEnter` S_OK + efeito NONE). `HDropDataObject` manual mantido só como fallback (pastas distintas).
  - `OleInitialize` na thread do drop (diretriz MS; `CoInitialize` da STA não basta).
  - Pós-`Drop` Ok, o host segura a saída até o arquivo estabilizar (~1,5s mín, teto 60s): o `DropTarget` do zip grava em thread própria sem handle — one-shot que sai na hora mata o worker e nada é gravado.
  - Sonda ponta a ponta (4 arquivos em zip temporário, `ShellHost` manual): todos caíram dentro do `.zip` segundos após o Ok — o worker de escrita sobrevive à saída do host (é do Shell, não nosso), então o settle é cortesia best-effort, não carga crítica.
  - `.7z`/`.rar` nesta máquina apontam para `ArchiveFolder` (suporte nativo Win11) **sem** `DropHandler` (`E_NOTIMPL`) — caem no `false` silencioso e aguardam a F4. Com WinRAR/7-Zip registrando handler próprio, o mesmo caminho funciona.

### F4 — Fallback
- `.zip` sem handler: escrita nativa; `.rar/.7z` sem handler: toast resx orientando instalar app.
- Critério: `.zip` sempre funciona; proprietário sem app explica em vez de falhar mudo.

## 7. Riscos conhecidos

- `IDropTarget::Drop` do zip usa `SHCreateThread` sem handle — não dá para esperar com `Join`; se precisar, observar com `ReadDirectoryChangesW`.
- `DragDrop.DoDragDrop` é modal — forwarder roda fora da UI thread STA do `ShellHost`, nunca na thread da `QuadraWindow`.
- `GetUIObjectOf(IDropTarget)` exige `cidl == 1` — um container por chamada; multi-drop itera.
- Efeito é sempre `COPY` (adiciona ao arquivo, origem preservada) — `Move` apagaria a origem, divergindo do Explorer.

## 8. Diário de bordo (onde parei em 2026-10-05)

- **F1 pronta e pushada** (`8cc9a22`): `ArchiveFormatDetector` + 14 testes.
- **F2 pronta e pushada** (mesmo commit): highlight `IsDropTarget` + cursor `Copy` no container; drop consumido sem escrita.
- **Docs de apoio pushados**: este plano (`319f8b0`), comportamento conhecido "7-Zip segue `.lnk`" em `PLANO_MENU_TERCEIROS.md` (`2387891`).
- **F3 implementada, VALIDADA pelo PO no app em 2026-10-05:**
  - Protocolo `drop` no `ShellHostProtocol` (+ testes), `ShellArchiveDrop` no host isolado, `RunDrop`, `DropOntoContainer` no cliente, `IArchiveDropService` + DI, `Quadra_Drop` dispara em background.
  - Validação técnica por sonda manual no `ShellHost.exe` (zip temporário em `C:\Temp`, já removido): 4/4 arquivos caíram no `.zip`. Descobertas no caminho: `IDataObject` precisa vir do Shell (HDrop manual é recusado), `OleInitialize` obrigatório, settle pós-drop best-effort.
  - Testes: Core 184 + UI 382 + Application 53, build 0 avisos/erros.
  - **Validação do PO no app: `teste.txt` arrastado sobre o `.zip` entrou no arquivo. F3 dada como certa.**
  - `.7z` sem `DropHandler` segue no-op até a F4.
- **F4 implementada e testada (2026-10-08):**
  - Fallback in-box nativo para `.zip` via `System.IO.Compression.ZipArchive` (suporte a arquivos e pastas recursivas, sem dependência externa).
  - Detecção dinâmica de utilitários externos em tempo de execução via `IExternalArchiverLocator` / `ExternalArchiverLocator` (localiza 7-Zip em `Program Files`, registro e PATH; localiza WinRAR).
  - Execução de compactadores externos via `IArchiveFallbackHandler` / `ArchiveFallbackHandler` (`7zG.exe a -y -scsUTF-8` para `.7z`, `.tar`, `.gz`, etc., com suporte a listfile UTF-8 para listas longas; `WinRAR.exe a -ibck -y` para `.rar`).
  - Notificação suave ao usuário via evento `ToolMissing` em `IArchiveDropService` e balloon tip nativo na bandeja (`App.ShowNotification` com textos em `Strings.resx`) caso o contêiner exija um compactador ausente.
  - Testes: 6 novos testes em `ArchiveFallbackHandlerTests.cs`, suite com 634 testes 100% passando.
- **Próximo (F5):** Validação prática pelo PO no Windows e auditoria final em `AUDITORIA_REUSO.md`.
