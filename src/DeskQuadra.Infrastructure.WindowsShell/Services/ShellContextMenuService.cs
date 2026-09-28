using System.Runtime.InteropServices;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Fallback do botão Cancelar: exibe o menu de contexto NATIVO DO FUNDO do desktop
/// via COM (SHGetDesktopFolder → CreateViewObject(IID_IContextMenu) → QueryContextMenu →
/// TrackPopupMenuEx com TPM_RETURNCMD → InvokeCommand).
/// Roda na thread STA da UI (o Click do WPF já roda no Dispatcher STA; o
/// TrackPopupMenuEx é modal e bloqueia até escolher/cancelar). Best-effort:
/// qualquer falha devolve false sem exceção.
/// IContextMenu (1) basta para exibir + invocar; IContextMenu2/3 (HandleMenuMsg)
/// só entraria com defeito real de submenu de extensão — motivo registrado em
/// NativeMethods junto às declarações.
/// </summary>
public sealed class ShellContextMenuService : IShellContextMenuService
{
    public bool TryShowDesktopMenu(nint ownerHwnd, int screenX, int screenY)
    {
        try
        {
            if (ownerHwnd == nint.Zero)
            {
                return false;
            }

            // Pasta do desktop (fundo, não de item).
            if (NativeMethods.SHGetDesktopFolder(out var folder) != 0 || folder is null)
            {
                return false;
            }

            try
            {
                // Menu DO FUNDO da pasta: CreateViewObject(IID_IContextMenu).
                // GetUIObjectOf sem PIDLs (cidl=0) devolve o menu DA PASTA
                // (Abrir, Fixar no Acesso rápido...), não o do fundo.
                Guid iid = NativeMethods.IID_IContextMenu;
                int hr = folder.CreateViewObject(ownerHwnd, ref iid, out var menu);
                if (hr != 0 || menu is null)
                {
                    return false;
                }

                try
                {
                    return ShowAndInvoke(menu, ownerHwnd, screenX, screenY);
                }
                finally
                {
                    Marshal.ReleaseComObject(menu);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(folder);
            }
        }
        catch
        {
            // Best-effort: o chamador já fechou o popup; nunca crasha.
            return false;
        }
    }

    private static bool ShowAndInvoke(NativeMethods.IContextMenu menu, nint ownerHwnd, int screenX, int screenY)
    {
        IntPtr hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            int hr = menu.QueryContextMenu(
                hMenu,
                0,
                NativeMethods.ContextMenuIdFirst,
                NativeMethods.ContextMenuIdLast,
                NativeMethods.CMF_NORMAL);
            if (hr < 0)
            {
                return false;
            }

            int commandId;
            // Receita MSDN p/ TPM_RETURNCMD: dono em foreground antes, senão o
            // menu ignora ESC e o foco fica preso no loop modal.
            ChordDiagLog.Log($"menu-classico antes Track {ChordDiagLog.Snapshot()}"); // ChordDiag
            NativeMethods.SetForegroundWindow(ownerHwnd);
            commandId = NativeMethods.TrackPopupMenuEx(
                hMenu,
                NativeMethods.TPM_RETURNCMD,
                screenX,
                screenY,
                ownerHwnd,
                IntPtr.Zero);
            // Destrava o estado modal do TrackPopupMenuEx (receita MSDN).
            NativeMethods.PostMessage(ownerHwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            ChordDiagLog.Log($"menu-classico apos Track cmd={commandId} {ChordDiagLog.Snapshot()}"); // ChordDiag
            if (!NativeMethods.IsContextMenuCommand(commandId))
            {
                return false; // Usuário cancelou (ou erro); idem: sem comando.
            }

            var info = new NativeMethods.CMINVOKECOMMANDINFO
            {
                hwnd = ownerHwnd,
                lpVerb = (IntPtr)NativeMethods.ToContextMenuVerbOffset(commandId, NativeMethods.ContextMenuIdFirst),
                nShow = NativeMethods.SW_SHOWNORMAL,
            };
            info.cbSize = Marshal.SizeOf<NativeMethods.CMINVOKECOMMANDINFO>();
            bool invoked = menu.InvokeCommand(ref info) == 0;
            ChordDiagLog.Log($"menu-classico apos Invoke cmd={commandId} ok={invoked} {ChordDiagLog.Snapshot()}"); // ChordDiag
            return invoked;
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
        }
    }
}
