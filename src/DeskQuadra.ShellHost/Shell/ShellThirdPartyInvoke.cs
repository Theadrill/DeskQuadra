using System.Diagnostics;
using System.Runtime.InteropServices;
using DeskQuadra.Core.ThirdParty;
using Vanara;
using Vanara.PInvoke;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;

namespace DeskQuadra.ShellHost.Shell;

// T3 terceiros (movido em T5 p/ dentro do host, sem mudar regra):
// InvokeCommand preferencialmente por VERBO canônico estável (GCS_VERBW da
// folha T2, ex. "SevenZipCompressToZip") na MESMA interface IContextMenu raiz
// da query, com lpVerb = ANSI + lpVerbW = Unicode e CMIC_MASK_UNICODE. NADA
// de TrackPopupMenu.
// MICRO-FIX offsets instáveis: handlers dinâmicos reordenam os offsets entre
// duas QueryContextMenu (sonda headless + shell-menu.log provaram: offset 112
// = "Comprimir e enviar por email" numa query e outra coisa noutra), então
// invocar por offset clicava o verbo vizinho (ex. criou "Firefox.7z" em vez
// de "firefox.zip") — e o VALIDATEW NÃO protege disso (o item existe, é
// outro). Verbo-string (HIWORD(lpVerbW)!=0; offset é HIWORD==0) é imune a
// reordenação. Offset (MAKEINTRESOURCE, HIWORD==0) + GCS_VALIDATEW viraram
// FALLBACK p/ verbo vazio/nulo (raro; risco documentado abaixo).
// Estrutura: CMINVOKECOMMANDINFOEX + CMIC_MASK_UNICODE SEMPRE (caminho com
// acento é bug garantido sem isso; lpVerbW exige a flag) + hwnd válido +
// ponto do cursor (PTINVOKE). Molde Vanara/Files: Files.App monta invoke por
// verbo com lpVerb = SafeResourceId(verb, Ansi); aqui o equivalente de vida
// útil explícita — ANSI via HGlobal do chamador (vive até o InvokeCommand) +
// lpVerbW string (o StructureToPtr copia p/ o bloco nativo) + cbSize cheio.
// Interface COM viva do QueryContextMenu até o InvokeCommand na MESMA STA
// dedicada — agora thread do HOST (antes thread do app). Falha/timeout =
// false (o host responde ok=false e a UI segue silenciosa). Tipos P/Invoke
// via Vanara MIT (5.0.7); lógica de invoke nossa.
// Guardas puras (HasStableVerb/IsOffsetInRange) moram no ShellHostProtocol
// (fonte única: cliente valida ANTES de spawnar, host revalida aqui).
internal static class ShellThirdPartyInvoke
{
    private const uint IdCmdFirst = 1;
    private const uint IdCmdLast = 0x7FFF;

    // Guarda VALIDATEW exige buffer VÁLIDO: o contrato diz que VALIDATE não
    // precisa de buffer, mas handlers reais (7-Zip incluído) escrevem sem
    // checar nulo → AV em NULL (AccessViolation em IContextMenu.GetCommandString).
    // Mesmo molde do GetVerbW em ShellThirdPartyQuery.cs. Nunca IntPtr.Zero + 0.
    internal const int ValidateCapacityChars = 512;

    // Guarda GCS_VALIDATEW: S_OK = o item existe; S_FALSE/falha = não invoca.
    // (S_FALSE tem severity success — checar Failed NÃO basta.)
    internal static bool IsValidationSuccess(HRESULT hr)
        => hr == HRESULT.S_OK;

    // Construção pura/testável dos parâmetros de invoke por OFFSET
    // (MAKEINTRESOURCE, HIWORD(lpVerb)==0): FALLBACK p/ verbo vazio/nulo.
    // RISCO DOCUMENTADO: offsets mudam entre duas QueryContextMenu (handlers
    // dinâmicos reordenam), então este caminho pode clicar o verbo vizinho e
    // o VALIDATEW não protege (o item existe, é outro). Unicode SEMPRE,
    // PTINVOKE só com ponto válido, SHIFT_DOWN espelhando a query.
    internal static CMINVOKECOMMANDINFOEX BuildInvokeInfo(
        uint commandOffset,
        HWND hwnd,
        POINT? invokePoint,
        bool shiftDown)
    {
        var info = new CMINVOKECOMMANDINFOEX((int)commandOffset);
        info.cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFOEX>();
        info.hwnd = hwnd;
        info.nShow = ShowWindowCommand.SW_SHOWNORMAL;
        info.fMask |= CMIC.CMIC_MASK_UNICODE;
        if (shiftDown)
        {
            info.fMask |= CMIC.CMIC_MASK_SHIFT_DOWN;
        }

        if (invokePoint.HasValue)
        {
            info.fMask |= CMIC.CMIC_MASK_PTINVOKE;
            info.ptInvoke = invokePoint.Value;
        }

        return info;
    }

    // Construção pura/testável dos parâmetros de invoke por VERBO canônico
    // (preferido): lpVerbW = verbo Unicode + lpVerb = ANSI (HIWORD!=0 =
    // string; offset é HIWORD==0) + CMIC_MASK_UNICODE (lpVerbW exige a flag;
    // sem ela o handler lê só o ANSI) + cbSize cheio. Molde Vanara/Files
    // (Files monta lpVerb via SafeResourceId ANSI); aqui o ANSI chega como
    // ponteiro HGlobal do chamador — que DEVE mantê-lo vivo até o
    // InvokeCommand retornar (o StructureToPtr copia o valor do ponteiro, não
    // o conteúdo; o lpVerbW string o StructureToPtr copia junto ao bloco).
    internal static CMINVOKECOMMANDINFOEX BuildInvokeInfoForVerb(
        string verb,
        IntPtr ansiVerbPtr,
        HWND hwnd,
        POINT? invokePoint,
        bool shiftDown)
    {
        var info = new CMINVOKECOMMANDINFOEX();
        info.cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFOEX>();
        info.hwnd = hwnd;
        info.nShow = ShowWindowCommand.SW_SHOWNORMAL;
        info.lpVerb = ansiVerbPtr;
        info.lpVerbW = verb;
        info.fMask |= CMIC.CMIC_MASK_UNICODE;
        if (shiftDown)
        {
            info.fMask |= CMIC.CMIC_MASK_SHIFT_DOWN;
        }

        if (invokePoint.HasValue)
        {
            info.fMask |= CMIC.CMIC_MASK_PTINVOKE;
            info.ptInvoke = invokePoint.Value;
        }

        return info;
    }

    public static bool TryInvoke(
        string path,
        string? verb,
        uint commandOffset,
        bool includeExtendedVerbs,
        IntPtr hwnd,
        POINT? invokePoint)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Offset só é exigido no caminho-fallback (verbo vazio): no
        // caminho-verbo a seleção é pelo verbo estável, imune a reordenação.
        if (!ShellHostProtocol.HasStableVerb(verb) && !ShellHostProtocol.IsOffsetInRange(commandOffset))
        {
            return false;
        }

        CMF flags = ShellThirdPartyQuery.BuildQueryFlags(includeExtendedVerbs);
        return ShellStaRunner.TryRun(
            () => InvokeOnStaThread(path, verb, commandOffset, flags, hwnd, invokePoint, includeExtendedVerbs),
            ShellStaRunner.InvokeTimeoutMs,
            "DeskQuadra.ShellInvoke",
            path);
    }

    private static bool InvokeOnStaThread(
        string path,
        string? verb,
        uint commandOffset,
        CMF flags,
        IntPtr hwnd,
        POINT? invokePoint,
        bool shiftDown)
    {
        bool byVerb = ShellHostProtocol.HasStableVerb(verb);
        // .lnk = invoke sobre o próprio link (SEM resolver o alvo antes, §1).
        HRESULT hr = SHParseDisplayName(path, null, out PIDL pidl, 0, out _);
        if (hr.Failed || pidl.IsNull)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, $"0x{(uint)hr:X8}", "n/a", "n/a", $"parse-failed"));
            return false;
        }

        try
        {
            hr = SHBindToParent(pidl, typeof(IShellFolder).GUID, out object? folderObj, out IntPtr childRel);
            if (hr.Failed || folderObj is not IShellFolder folder || childRel == IntPtr.Zero)
            {
                ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, $"0x{(uint)hr:X8}", "n/a", "n/a", "bind-failed"));
                return false;
            }

            try
            {
                Guid iidMenu = typeof(IContextMenu).GUID;
                hr = folder.GetUIObjectOf(HWND.NULL, 1, new[] { childRel }, in iidMenu, IntPtr.Zero, out object? menuObj);
                if (hr.Failed || menuObj is not IContextMenu contextMenu)
                {
                    ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, $"0x{(uint)hr:X8}", "n/a", "n/a", "menu-failed"));
                    return false;
                }

                try
                {
                    // A query inicializa os handlers — o invoke exige o mesmo
                    // pipeline/flags da listagem, na MESMA interface raiz. A
                    // seleção agora é pelo VERBO estável (imune a reordenação
                    // de offsets entre queries); re-query continua.
                    using var hMenu = CreatePopupMenu();
                    if (hMenu.IsInvalid)
                    {
                        ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, "n/a", "n/a", "n/a", "hmenu-invalid"));
                        return false;
                    }

                    hr = contextMenu.QueryContextMenu(hMenu, 0, IdCmdFirst, IdCmdLast, flags);
                    string queryHr = $"0x{(uint)hr:X8}";
                    if (hr.Failed)
                    {
                        ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, queryHr, "n/a", "n/a", "query-failed"));
                        return false;
                    }

                    HWND owner = hwnd != IntPtr.Zero ? (HWND)hwnd : GetForegroundWindow();

                    if (byVerb)
                    {
                        return InvokeByVerb(contextMenu, path, verb!, queryHr, owner, invokePoint, shiftDown);
                    }

                    // FALLBACK offset (verbo vazio/nulo, raro): mantém o
                    // comportamento T3 anterior. RISCO ACEITO: offsets mudam
                    // entre queries; o VALIDATEW abaixo só barra offset
                    // inexistente (S_FALSE), NÃO verbo trocado (o item
                    // existe, é outro — foi o caso Firefox.7z).
                    if (!ShellHostProtocol.IsOffsetInRange(commandOffset))
                    {
                        ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, queryHr, "n/a", "n/a", "offset-out-of-range"));
                        return false;
                    }

                    // Guarda do caminho-offset: o offset ainda existe?
                    // Buffer VÁLIDO obrigatório (AV com IntPtr.Zero + 0 no 7-Zip).
                    IntPtr validateBuf = Marshal.AllocCoTaskMem(ValidateCapacityChars * 2);
                    try
                    {
                        hr = contextMenu.GetCommandString(
                            (UIntPtr)commandOffset,
                            GCS.GCS_VALIDATEW,
                            IntPtr.Zero,
                            validateBuf,
                            ValidateCapacityChars);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(validateBuf);
                    }

                    string validateHr = $"0x{(uint)hr:X8}";
                    if (!IsValidationSuccess(hr))
                    {
                        Debug.WriteLine(
                            $"[ShellThirdPartyInvoke] offset {commandOffset} inválido p/ '{path}': {hr}");
                        ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, queryHr, validateHr, "n/a", "validate-rejected"));
                        return false;
                    }

                    var info = BuildInvokeInfo(commandOffset, owner, invokePoint, shiftDown);

                    int size = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>();
                    IntPtr p = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.StructureToPtr(info, p, fDeleteOld: false);
                        hr = contextMenu.InvokeCommand(p);
                        string invokeHr = $"0x{(uint)hr:X8}";
                        bool ok = hr.Succeeded;
                        ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, queryHr, validateHr, invokeHr, ok ? "ok" : "invoke-failed"));
                        return ok;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(p);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(menuObj);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(folder);
            }
        }
        catch (Exception ex)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, commandOffset, "n/a", "n/a", "n/a", $"exception {ex.GetType().Name}"));
            return false;
        }
        finally
        {
            pidl.Dispose();
        }
    }

    private static bool InvokeByVerb(
        IContextMenu contextMenu,
        string path,
        string verb,
        string queryHr,
        HWND owner,
        POINT? invokePoint,
        bool shiftDown)
    {
        // ANSI vivo até o InvokeCommand retornar (lpVerb copia o ponteiro).
        IntPtr ansiVerbPtr = IntPtr.Zero;
        try
        {
            ansiVerbPtr = Marshal.StringToHGlobalAnsi(verb);
        }
        catch (Exception ex)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, 0, queryHr, "n/a", "n/a", $"exception {ex.GetType().Name}"));
            return false;
        }

        try
        {
            var info = BuildInvokeInfoForVerb(verb, ansiVerbPtr, owner, invokePoint, shiftDown);

            int size = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>();
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, p, fDeleteOld: false);
                HRESULT hr = contextMenu.InvokeCommand(p);
                string invokeHr = $"0x{(uint)hr:X8}";
                bool ok = hr.Succeeded;
                ShellMenuLog.Log(ShellMenuLog.FormatInvoke(path, verb, 0, queryHr, "n/a", invokeHr, ok ? "ok" : "invoke-failed"));
                return ok;
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ansiVerbPtr);
        }
    }
}
