using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Vanara;
using Vanara.PInvoke;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T2 terceiros: query SÓ LEITURA do menu clássico do Shell (pipeline §1:
// SHParseDisplayName → SHBindToParent → IShellFolder.GetUIObjectOf(IContextMenu)
// → QueryContextMenu num HMENU fantasma → enumeração recursiva).
// RODA EM THREAD STA DEDICADA COM MESSAGE QUEUE — NUNCA na UI do WPF.
// NENHUM InvokeCommand/execução nesta fase (só QueryContextMenu + GCS_VERBW).
// Tipos P/Invoke via Vanara MIT (5.0.7); lógica de enumeração nossa.
// Falha/timeout = lista vazia (o chamador mantém o placeholder, silencioso).
internal static class ShellThirdPartyQuery
{
    // Flags travadas §1. CMF_EXPLORE/NODEFAULT/INCLUDESTATIC/DEFAULTONLY NÃO entram.
    public const CMF BaseFlags = CMF.CMF_NORMAL | CMF.CMF_ITEMMENU | CMF.CMF_SYNCCASCADEMENU;

    public static CMF BuildQueryFlags(bool includeExtendedVerbs)
    {
        CMF flags = BaseFlags;
        if (includeExtendedVerbs)
        {
            flags |= CMF.CMF_EXTENDEDVERBS;
        }

        return flags;
    }

    private const uint IdCmdFirst = 1;
    private const uint IdCmdLast = 0x7FFF;
    private const int MaxDepth = 8;
    private const int LabelCapacityChars = 512;
    private const int VerbCapacityChars = 512;
    private const int QueryTimeoutMs = 3000;

    public static IReadOnlyList<ShellMenuNode> QueryForPath(string path, bool includeExtendedVerbs)
    {
        CMF flags = BuildQueryFlags(includeExtendedVerbs);
        try
        {
            return RunOnStaThread(() => QueryOnStaThread(path, flags));
        }
        catch (Exception ex)
        {
            // Silencioso, padrão do projeto: falha de COM/timeout nunca quebra o menu.
            Debug.WriteLine($"[ShellThirdPartyQuery] query falhou p/ '{path}': {ex.GetType().Name}");
            return Array.Empty<ShellMenuNode>();
        }
    }

    // STA dedicada com message queue (PeekMessage cria a fila; handlers do Shell
    // podem PostMessage). Join limitado: handler lento não congela a UI (T5 isola
    // de vez com ShellHost + kill; aqui a thread órfã é background e morre sozinha).
    private static IReadOnlyList<ShellMenuNode> RunOnStaThread(Func<IReadOnlyList<ShellMenuNode>> query)
    {
        IReadOnlyList<ShellMenuNode>? result = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                PeekMessage(out MSG _, HWND.NULL, 0, 0, PM.M_NOREMOVE);
                result = query();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = "DeskQuadra.ShellQuery";
        thread.Start();

        if (!thread.Join(QueryTimeoutMs))
        {
            throw new TimeoutException("QueryContextMenu excedeu o limite.");
        }

        if (error is not null)
        {
            throw error;
        }

        return result ?? Array.Empty<ShellMenuNode>();
    }

    private static IReadOnlyList<ShellMenuNode> QueryOnStaThread(string path, CMF flags)
    {
        // .lnk = query sobre o próprio link (SEM resolver o alvo antes).
        HRESULT hr = SHParseDisplayName(path, null, out PIDL pidl, 0, out _);
        if (hr.Failed || pidl.IsNull)
        {
            return Array.Empty<ShellMenuNode>();
        }

        try
        {
            hr = SHBindToParent(pidl, typeof(IShellFolder).GUID, out object? folderObj, out IntPtr childRel);
            if (hr.Failed || folderObj is not IShellFolder folder || childRel == IntPtr.Zero)
            {
                return Array.Empty<ShellMenuNode>();
            }

            try
            {
                Guid iidMenu = typeof(IContextMenu).GUID;
                hr = folder.GetUIObjectOf(HWND.NULL, 1, new[] { childRel }, in iidMenu, IntPtr.Zero, out object? menuObj);
                if (hr.Failed || menuObj is not IContextMenu contextMenu)
                {
                    return Array.Empty<ShellMenuNode>();
                }

                try
                {
                    using var hMenu = CreatePopupMenu();
                    if (hMenu.IsInvalid)
                    {
                        return Array.Empty<ShellMenuNode>();
                    }

                    hr = contextMenu.QueryContextMenu(hMenu, 0, IdCmdFirst, IdCmdLast, flags);
                    if (hr.Failed)
                    {
                        return Array.Empty<ShellMenuNode>();
                    }

                    return EnumerateMenu(hMenu, contextMenu, depth: 0);
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
        finally
        {
            pidl.Dispose();
        }
    }

    private static IReadOnlyList<ShellMenuNode> EnumerateMenu(HMENU hMenu, IContextMenu contextMenu, int depth)
    {
        var nodes = new List<ShellMenuNode>();
        if (depth > MaxDepth)
        {
            return nodes;
        }

        int count = GetMenuItemCount(hMenu);
        for (uint pos = 0; pos < (uint)count; pos++)
        {
            var mii = new MENUITEMINFO
            {
                cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                fMask = MenuItemInfoMask.MIIM_FTYPE | MenuItemInfoMask.MIIM_ID | MenuItemInfoMask.MIIM_SUBMENU,
            };
            if (!GetMenuItemInfo(hMenu, pos, true, ref mii))
            {
                continue;
            }

            if (((uint)mii.fType & (uint)MenuItemType.MFT_SEPARATOR) != 0)
            {
                // MFT_SEPARATOR sempre fora (§1) — cai no Build como separador.
                nodes.Add(ShellMenuNode.Separator);
                continue;
            }

            string label = GetItemLabel(hMenu, pos);

            if (!mii.hSubMenu.IsNull)
            {
                // MF_POPUP/cascata: recursão no submenu (ex.: 7-Zip).
                var children = EnumerateMenu(mii.hSubMenu, contextMenu, depth + 1);
                nodes.Add(new ShellMenuNode(label, null, 0, IsSeparator: false, IsPopup: true, children));
                continue;
            }

            if (mii.wID < IdCmdFirst)
            {
                continue;
            }

            uint offset = mii.wID - IdCmdFirst;
            // GCS_VERBW por offset (§1); verbo vazio ≠ descarte (label decide).
            string? verb = GetVerbW(contextMenu, offset);
            nodes.Add(new ShellMenuNode(label, verb, offset, IsSeparator: false, IsPopup: false, Array.Empty<ShellMenuNode>()));
        }

        return nodes;
    }

    private static string GetItemLabel(HMENU hMenu, uint posByPosition)
    {
        var sb = new StringBuilder(LabelCapacityChars);
        int len = GetMenuString(hMenu, posByPosition, sb, sb.Capacity, MenuFlags.MF_BYPOSITION);
        return len > 0 ? sb.ToString() : string.Empty;
    }

    private static string? GetVerbW(IContextMenu contextMenu, uint offset)
    {
        IntPtr buf = Marshal.AllocCoTaskMem(VerbCapacityChars * 2);
        try
        {
            HRESULT hr = contextMenu.GetCommandString((UIntPtr)offset, GCS.GCS_VERBW, IntPtr.Zero, buf, VerbCapacityChars);
            if (hr.Failed)
            {
                return null;
            }

            return Marshal.PtrToStringUni(buf);
        }
        finally
        {
            Marshal.FreeCoTaskMem(buf);
        }
    }
}
