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
