# Auditoria de Reuso / Duplicação

- **Data:** 2026-09-27
- **Status:** parcial — delta pós-D1-D14 (2026-09-27) incorporado; trabalhar pontos após finalizar o .tmp

Relatório detalhado de auditoria de reuso/duplicação.

## Resumo

| ID | Título | Severidade | Prioridade |
|----|--------|------------|------------|
| D1 | Predicado de input touch divergente | ALTA | TOP 1 |
| D2 | Factory DispatcherTimer one-shot 5x | MÉDIA-ALTA | TOP 2 |
| D3 | DuplicateFileOnDisk arquivo vs diretório + loop nome único 4x | ALTA | TOP 3 |
| D4 | Helper Theme\<T\> 3x | MÉDIA | — |
| D5 | Ex-style Win32 Get+Set | MÉDIA | — |
| D6 | Conversão DPI | MÉDIA | — |
| D7 | JsonSerializerOptions + AppData + best-effort IO | MÉDIA | — |
| D8 | Sort+Notify 4x + Notify em ~13 pontos | MÉDIA | — |
| D9 | Guards IsLocked/IsCollapsed em 8 Resize | MÉDIA | — |
| D10 | Display-name .lnk 2x | BAIXA | — |
| D11 | Fakes de teste dispersos | MÉDIA | — |
| D12 | Checagem janela do próprio processo 2x | BAIXA | — |
| D13 | Hover Enter/Leave 2x | BAIXA | — |
| D14 | Fallback pastas Desktop 4x | BAIXA | — |

---

## D1 — Predicado de input touch divergente (ALTA) — TOP 1

| Campo | Detalhe |
|-------|---------|
| **Severidade** | ALTA |
| **Prioridade** | TOP 1 |

### Onde

- `Views/QuadraWindow.xaml.cs:845-849` — `IsTouchPromotedMouse` local;
- `:1055-1057` — `DesktopItem_PreviewMouseLeftButtonDown` (`_isTouchActive \|\| Stylus Touch \|\| PInvoke`);
- `:1087` — `ContextMenuOpening` (`_isTouchActive \|\| PInvoke`, sem Stylus!);
- `:1118-1120` — `PreviewMouseMove`;
- `:237` — `PeekMouseEnter`;
- `:120-135` — tracking local `_isTouchActive`;
- `Services/InputDeviceDetector.cs:80,91-99,112-123,139-154` — tracker global com `_isTouchActive` estático próprio.

### Problema

Repete expressão `StylusDevice Touch || IsCurrentMessageFromTouch` em ~6 formas; dois `_isTouchActive` que podem divergir.

### Unificação

Estender `InputDeviceDetector` com `IsPromotedTouch`/`IsTouchInteraction`, eliminar `_isTouchActive` local, call sites `237,845,872,900,1055,1087,1118` + ctor.

---

## D2 — Factory DispatcherTimer one-shot 5x (MÉDIA-ALTA) — TOP 2

| Campo | Detalhe |
|-------|---------|
| **Severidade** | MÉDIA-ALTA |
| **Prioridade** | TOP 2 |

### Onde

Tudo em `QuadraWindow.xaml.cs`:

- `_peekEnterTimer` `246-251/254/303-311`,
- `_peekExitTimer` `284-289/292/313-321`,
- `_springTimer` `1265-1270/1274/1296-1304`,
- `_postDropCollapseTimer` `1316-1321/1335/1324-1333`,
- `_touchInertiaTimer` `999-1007` (recorrente),
- `CancelPeekTimers` `323-327`,
- `OnClosed` `1568-1571`.

### Unificação

`Services/OneShotTimer.cs` com `Arm(ref slot, ms, tick)` + `Cancel`.

---

## D3 — DuplicateFileOnDisk arquivo vs diretório + loop nome único 4x (ALTA) — TOP 3

| Campo | Detalhe |
|-------|---------|
| **Severidade** | ALTA |
| **Prioridade** | TOP 3 |

### Onde

- `QuadraWindow.xaml.cs:` ramo arquivo `1432-1473`, ramo diretório `1475-1510`, `CopyDirectoryRecursively` `1520-1533`, call sites `1388` e `1417`, zero teste.

### Unificação

Classe pura `Core/FileDuplication/FileDuplicator.cs` (`GetUniquePath` + `DuplicateFileOnDisk`) + xUnit.

---

## D4 — Helper Theme\<T\> 3x (MÉDIA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | MÉDIA |

### Onde

- `DarkDialog.cs:17-18`,
- `DragPreviewWindow.cs:33-34`,
- `App.xaml.cs:523-524`.

### Unificação

`Theme/ThemeResolver.cs` `Get<T>(key, fallback)`.

---

## D5 — Ex-style Win32 Get+Set (MÉDIA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | MÉDIA |

### Onde

- `DragPreviewWindow.cs:98-109`,
- `DesktopSelectionWindow.xaml.cs:10-12/24-28` (redefine consts locais!),
- `AnchorService` `35-53`,
- `QuadraWindow` `447-451`.

### Unificação

Helpers `ApplyClickThroughNoActivate`/`ApplyToolWindow` no `NativeMethods`.

---

## D6 — Conversão DPI (MÉDIA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | MÉDIA |

### Onde

- `DesktopSelectionWindow` `33-40`,
- `DragPreviewWindow` `93-95`,
- `App` `186-193`,
- `QuadraWindow` `441-445` e `561-584`.

### Unificação

`Services/DpiHelper.cs`.

---

## D7 — JsonSerializerOptions + AppData + best-effort IO (MÉDIA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | MÉDIA |

### Onde

- `JsonLayoutRepository` `19-23/28-31`,
- `JsonDensitySettingsService` `17-21/26-30`,
- `anchor.log`/`guardian.log`.

### Unificação

`Persistence/JsonStorageDefaults.cs`.

---

## D8 — Sort+Notify 4x + Notify em ~13 pontos (MÉDIA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | MÉDIA |

### Onde

- `SortByName/Type/Date/Manual` `772-794`.

### Unificação

`ApplySortAndPersist`.

---

## D9 — Guards IsLocked/IsCollapsed em 8 Resize (MÉDIA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | MÉDIA |

### Onde

- `1585-1741`.

### Unificação

`CanTransform()` + `ResizeCore`.

---

## D10 — Display-name .lnk 2x (BAIXA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | BAIXA |

### Onde

- `QuadraViewModel` `156-158`,
- `DesktopScannerService` `66-68`.

### Unificação

`Core/DesktopItemNames.GetDisplayName` + teste.

---

## D11 — Fakes de teste dispersos (MÉDIA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | MÉDIA |

### Onde

- `LayoutCoordinatorTests` `10-44`,
- `InputDeviceDetectorContractTests` `7-23`.

### Unificação

Projeto `tests/DeskQuadra.TestDoubles/`.

---

## D12 — Checagem janela do próprio processo 2x (BAIXA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | BAIXA |

### Onde

- `App` `163-171`,
- `DesktopDrawingService` `173-178`.

### Unificação

`NativeMethods.IsOwnProcessWindow`.

---

## D13 — Hover Enter/Leave 2x (BAIXA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | BAIXA |

### Onde

- `App` `569-573`.

### Unificação

`AttachHover` local.

---

## D14 — Fallback pastas Desktop 4x (BAIXA)

| Campo | Detalhe |
|-------|---------|
| **Severidade** | BAIXA |

### Onde

- `DesktopScannerService` `91-124`.

### Unificação

`AddIfUnique` local.

---

## Exclusões da auditoria

- `DarkDialog` (já unificado),
- logs/comentários,
- defaults TUDO/Minha Quadra (decisão arquitetural),
- Tray WinForms vs menu WPF (padrões diferentes de propósito).

---

## Delta pós-D1-D14 (2026-09-27) — código do .tmp/DarkDialog

**Veredito:** código novo limpo, nenhuma duplicação nova relevante (nada MÉDIA ou acima); o commit inclusive reduziu duplicação antiga.

### N1 (BAIXA) — Plumbing de timestamps p/ Decide repetido 3x

- **Onde:** `JsonLayoutRepository.cs:61-62`, `:207-209`, `:216` — leitura de `LastWriteTimeUtc` de tmp/json para passar ao `Decide`.
- **Futuro:** `GetRecoveryTimestamps()` local retornando o par `(tmpTime, jsonTime)`.

### N2 (BAIXA, observar) — File.Copy tmp→json 2x com comportamento diferente

- **Onde:** `Load:73-74` (sem `.bak`) vs `Resolve:251-252` (com `.bak`).
- **Decisão:** não unificar — regra: só unificar com comportamento idêntico.
- **Futuro:** `PromoteTmpToJson(bool keepBak)`, se um terceiro call site surgir.

### N3 (BAIXA) — File.Copy best-effort inline 3x

- **Onde:** `Load:71-78`, `:119-126`, `Resolve:244-257` — `try/catch` best-effort ao redor de `File.Copy`.
- **Futuro:** `CopyBestEffort(...)` local (fecha o D7 dentro deste arquivo).

### N4 (INFORMATIVO) — DarkDialog unificou com mudança intencional

- `ToolWindow` → `SingleBorderWindow` + taskbar foi decisão explícita, não descuido.

### N5 (INFORMATIVO) — Theme\<T\> segue 3x

- `DarkDialog`, `DragPreview`, `App` — já coberto pelo D4, sem ação nova.

### N6 (INFORMATIVO) — DeciderTests poderiam virar Theory/MemberData

- Estilo, não duplicação produtiva — sem ação.

### Limpos (sem duplicação)

- `Decider` puro, pré-checagem `App`, recovery via `.resx`, stubs `FakeRepository`.

---

## 2ª Auditoria (2026-09-28, pós-D1–D14/N1–N6)

**Veredito:** fixes seguraram, nenhuma ALTA. Só MÉDIAS e BAIXAs novas + 3 sem-ação.

### Tabela INFRA (E1–E10-infra)

| ID | Sev | Onde | Unificação |
|----|-----|------|------------|
| E1-infra | MÉDIA | `DesktopWindowAnchorService.cs:74-77` ≡ `NativeDesktopIconService.cs:81-84` (fallback FindWindow Progman→GetShellWindow) | `NativeMethods.GetProgmanHandle()` |
| E2-infra | BAIXA | `FileLauncherService.cs:19` ≡ `IconExtractorService.cs:63` (exists arquivo\|\|diretório) | `Core FileSystemUtils.PathExists` + xUnit |
| E3-infra | MÉDIA | `FileDuplicator.cs:66-79` ≡ `:94-106` (arquivo/diretório espelhados) | `DuplicateCore` local + testes existentes |
| E4-infra | MÉDIA | `LayoutCoordinator.cs:174-176,:207-209,:226-228` (Cancel+Dispose 3x) | `CancelDebounceLocked` local |
| E5-infra | BAIXA | `SizeSnapper.cs:73-96` (4 guards throw) | `ThrowIfNegative/ThrowIfNotPositive` + testes |
| E6-infra | BAIXA | `LayoutCoordinator.cs:117-126` Hide ≡ `:128-137` Restore | `SetHidden` local (ou adiar) |
| E7-infra | BAIXA | temp-dir `JsonLayoutRepositoryTests:14,18-30,209` ≡ `FileDuplicatorTests:15,17-30` | fixture `TempDirectory` (não é fake — fora do D11) |
| E8-infra | sem-ação | `Guardian/Program.cs:59-65` RegisterHotKey ≡ NativeMethods | watchdog standalone, não acoplar pânico |
| E9-infra | sem-ação | `DensityResolver` consts SM_* ≡ NativeMethods | Clean Architecture |
| E10-infra | sem-ação | plumbing log guardian/anchor | exceção logs, D7 carimbou |

### Tabela UI (E1–E16-UI)

| ID | Sev | Onde | Unificação |
|----|-----|------|------------|
| E1-UI | MÉDIA | `QuadraWindow.xaml:69-139` 3 estilos ≡ `App.xaml:12-82` | fonte única no App |
| E2-UI | MÉDIA | `InputDeviceDetector.cs:56-127` Initialize morto (zero call sites) | deletar (ou ligar + remover lambdas) |
| E3-UI | MÉDIA | `QuadraWindow.xaml.cs:1485-1685` 8 Resize espelhados | `SnapEdge/SnapCorner` locais |
| E4-UI | MÉDIA | título "Quadra N" `App.xaml.cs:695-696` ≡ `QuadraWindow.xaml.cs:1389-1395` | `QuadraNaming.NextTitle` na UI + xUnit |
| E5-UI | BAIXA | AxisResizeDirection | unificar local |
| E6-UI | BAIXA | thumbs foreach | unificar local |
| E7-UI | BAIXA | NotifyItemsChanged | unificar local |
| E8-UI | BAIXA | SetProperty nos ViewModels (doubles com epsilon próprio) | unificar local |
| E9-UI | BAIXA | CancelTransientTimers | unificar local |
| E10-UI | BAIXA | FinishDrop | unificar local |
| E11-UI | BAIXA | DuplicateWithStandardSuffix | unificar local |
| E12-UI | BAIXA | OpenInExplorer | unificar local |
| E13-UI | BAIXA | RebuildItemViewModels | unificar local |
| E14-UI | BAIXA | DpiHelper.MapPhysicalToDip | unificar local |
| E15-UI | BAIXA | consts Win32→NativeMethods | unificar local |
| E16-UI | BAIXA | reuse ResolveEffectiveIsTouch | unificar local |

### Limpos UI (com motivo)

- timers cobertos, zero DllImport na UI, resx 100%, ChordDiag/HangTestSwitch intocados.

### Fila de trabalho ordenada

1. MÉDIAS: E1-infra, E3-infra, E4-infra, E1-UI, E2-UI, E3-UI, E4-UI.
2. BAIXAs na ordem: E2-infra, E5-infra, E6-infra, E7-infra, E5-UI, E6-UI, E7-UI, E8-UI, E9-UI, E10-UI, E11-UI, E12-UI, E13-UI, E14-UI, E15-UI, E16-UI.
3. Sem-ação: E8-infra (watchdog standalone), E9-infra (Clean Architecture), E10-infra (exceção logs/D7), D11 (fixture `TempDirectory` não é fake).

### Status inicial

- Tudo pendente: E1/E3/E4-infra, E1–E4-UI, E2/E5/E6/E7-infra, E5–E16-UI.

---

## 3ª Auditoria (2026-10-02, terceiros T1–T6 + fix invoke-by-label)

**Veredito:** 1 MÉDIA + 2 BAIXAs reais + 2 observar + 6 sem-ação. Testes na auditoria: 409 aprovados (49 Application + 106 Core + 254 UI.Wpf). Escopo: `Core/ThirdParty/*`, `Infrastructure/Shell/*`, `ShellHost/*`, `UI/.../QuadraWindow` (terceiros).

### F1 — Preâmbulo COM 3x (MÉDIA)

| Campo | Detalhe |
|-------|---------|
| **Onde** | `ShellHost/Shell/ShellThirdPartyQuery.cs` (query) ≡ `ShellHost/Shell/ShellThirdPartyInvoke.cs` `InvokeOnStaThread` ≡ `InvokeByLabelOnStaThread` (`SHParseDisplayName → SHBindToParent → GetUIObjectOf` + `finally ReleaseComObject/pidl.Dispose`) |
| **Problema** | Mesmo abre-porta copiado 3x; bug ali = conserto 3x (regra da 2ª repetição já estourou). |
| **Unificação** | `ShellBindHelper.BindContextMenu(path)` com logs no chamador (logs divergem: `FormatQueryFailed` vs `FormatInvoke`). COM exige STA real — validar manual + `shell-menu.log`. |

### F2 — `IdCmdFirst/Last` 3x (BAIXA)

| Campo | Detalhe |
|-------|---------|
| **Onde** | `Core/ThirdParty/ShellHostProtocol.cs` (private) ≡ `ShellHost/.../ShellThirdPartyQuery.cs` ≡ `ShellHost/.../ShellThirdPartyInvoke.cs` (`1` / `0x7FFF`) |
| **Problema** | Dois números mágicos escritos à mão em 3 arquivos. |
| **Unificação** | Expor no protocolo (já é fonte única de `IsOffsetInRange/HasStableVerb/timeouts`). |

### F3 — Leitura de HMENU 2x (BAIXA)

| Campo | Detalhe |
|-------|---------|
| **Onde** | `ShellThirdPartyQuery.cs: GetItemLabel` ≡ `ShellThirdPartyInvoke.cs: GetMenuLabel` (ambos `StringBuilder(512)` + `GetMenuString MF_BYPOSITION`) + `LabelCapacityChars 512` 2x + init `MENUITEMINFO` 2x |
| **Problema** | Mesmo jeito de ler texto do menu copiado na listagem e na execução. |
| **Unificação** | `ShellMenuNative.GetLabel()` + `BuildItemInfo()` no ShellHost. |

### F4/F5 — Observar (não extrair agora)

- **F4:** `ShellHost/Program.cs` (`RunInvoke` ≡ `RunInvokeByLabel`, X/Y→POINT, 3 linhas, só 2x) — extrair `ToPoint()` se 3º call site surgir.
- **F5:** `Core/ThirdParty/ThirdPartyVerbFilter.cs` (`NormalizeLabel` vs `CleanLabelForDisplay` dividem tab-cut/`&`/trim, diferem só `ToLower`) — propósitos distintos (comparar vs mostrar); extrair `StripLabel()` privado se 3º uso surgir.

### Sem-ação (com motivo)

- **S1:** `JsonOptions` protocolo vs `JsonStorageDefaults.SerializerOptions` — comportamento diferente (sem indent vs indented); regra proíbe unificar.
- **S2:** `ShellMenuLog` AppData/append best-effort — exceção logs já carimbada (`E10-infra`).
- **S3:** `ShellHostClient.LocateHostExe` vs `App.SpawnGuardianProcess` — watchdog standalone, não acoplar (extensão do `E8-infra`).
- **S4:** `Serialize/Parse Query/Invoke/InvokeByLabel` — DTOs distintos, não é repetição.
- **S5:** Fakes `FakeProcess/FakeLauncher` (`ShellHostClientTests`) — interfaces distintas, zero duplicação; `D11` segue válido (5 fakes, sem projeto `TestDoubles`).
- **S6:** `IsShiftPressed` — definição única (`NativeMethods`), sem duplicação; UI §2 intacta (sem `Style` inline, sem timer novo).

### Status 3ª auditoria

- Corrigido, validado pelo PO no app e pushado junto: F1, F2, F3 + fundo real no vazio + supressão de complementos no fundo + denylist "conceder acesso a".
- Observar: F4, F5. Sem-ação: S1–S6.
