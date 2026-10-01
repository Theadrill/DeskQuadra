using DeskQuadra.Core.ThirdParty;
using DeskQuadra.ShellHost.Shell;
using Vanara.PInvoke;

namespace DeskQuadra.UI.Wpf.Tests;

// Cascatas lazy/delay-populated (SendTo/OpenWith e QUALQUER outra, genérico):
// o engine envia WM_INITMENUPOPUP via IContextMenu3/2 antes de re-enumerar.
// Só lógica pura aqui (sem COM/HMENU real): valor da mensagem, mapeamento
// wParam/lParam e formato do log (o populate real é best-effort no STA).
public class ShellLazyPopupTests
{
    [Fact]
    public void WmInitMenuPopup_É0117()
    {
        Assert.Equal(0x0117u, ShellThirdPartyQuery.WmInitMenuPopup);
    }

    [Fact]
    public void BuildInitMenuPopupParams_MapeiaSubmenuEPosição()
    {
        HMENU hSub = (HMENU)new IntPtr(0x1234);

        var (wParam, lParam) = ShellThirdPartyQuery.BuildInitMenuPopupParams(hSub, 5);

        Assert.Equal(new IntPtr(0x1234), wParam);
        Assert.Equal(new IntPtr(5), lParam);
    }

    [Fact]
    public void FormatPopupPopulated_ContémLabelEContagens()
    {
        string s = ShellMenuLog.FormatPopupPopulated("Enviar para", 0, 7);

        Assert.Contains("Enviar para", s);
        Assert.Contains("before=0", s);
        Assert.Contains("after=7", s);
    }
}
