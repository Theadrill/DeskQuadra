using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace DeskQuadra.ShellHost.Shell;

// F3 3ª auditoria: leitura de rótulo do HMENU (StringBuilder 512 +
// GetMenuString MF_BYPOSITION) + init MENUITEMINFO duplicados entre
// ShellThirdPartyQuery (GetItemLabel) e ShellThirdPartyInvoke (GetMenuLabel).
// Fonte única, sem mudar comportamento/flags/logs/protocolo.
internal static class ShellMenuNative
{
    internal const int LabelCapacityChars = 512;

    internal static MENUITEMINFO BuildItemInfo()
    {
        return new MENUITEMINFO
        {
            cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
            fMask = MenuItemInfoMask.MIIM_FTYPE | MenuItemInfoMask.MIIM_ID | MenuItemInfoMask.MIIM_SUBMENU,
        };
    }

    internal static string GetLabel(HMENU hMenu, uint posByPosition)
    {
        var sb = new StringBuilder(LabelCapacityChars);
        int len = GetMenuString(hMenu, posByPosition, sb, sb.Capacity, MenuFlags.MF_BYPOSITION);
        return len > 0 ? sb.ToString() : string.Empty;
    }
}
