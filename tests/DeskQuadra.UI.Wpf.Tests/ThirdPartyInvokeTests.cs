using System.Runtime.InteropServices;
using DeskQuadra.Core.ThirdParty;
using DeskQuadra.Infrastructure.WindowsShell.Shell;
using DeskQuadra.ShellHost.Shell;
using Vanara;
using Vanara.PInvoke;

namespace DeskQuadra.UI.Wpf.Tests;

// T3 terceiros: lógica pura do invoke (construção dos parâmetros, validações,
// guardas do serviço). Sem Shell/COM: nada aqui toca em HWND real ou STA.
// MICRO-FIX offsets instáveis: verbo canônico (GCS_VERBW) é o caminho
// preferido (imune a reordenação); offset + VALIDATEW é fallback p/ verbo vazio.
public class ThirdPartyInvokeTests
{
    [Theory]
    [InlineData("SevenZipCompressToZip", true)]
    [InlineData("x", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasStableVerb_SóNãoVazioÉEstável(string? verb, bool expected)
    {
        Assert.Equal(expected, ShellHostProtocol.HasStableVerb(verb));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(41u)]
    public void BuildInvokeInfo_Fallback_SemprePorOffset(uint offset)
    {
        var info = ShellThirdPartyInvoke.BuildInvokeInfo(offset, HWND.NULL, null, shiftDown: false);

        // lpVerb = MAKEINTRESOURCE(offset): recurso inteiro, nunca string
        // (HIWORD==0 = offset; verbo-string é HIWORD!=0).
        Assert.True(info.lpVerb.Equals((IntPtr)(int)offset));
    }

    [Fact]
    public void BuildInvokeInfoForVerb_LpVerbWÉOVerbo_EUnicodeLigado()
    {
        IntPtr ansi = Marshal.StringToHGlobalAnsi("SevenZipCompressToZip");
        try
        {
            var info = ShellThirdPartyInvoke.BuildInvokeInfoForVerb(
                "SevenZipCompressToZip", ansi, HWND.NULL, null, shiftDown: false);

            // lpVerbW exige CMIC_MASK_UNICODE (sem ela o handler lê só o ANSI).
            Assert.Equal("SevenZipCompressToZip", info.lpVerbW);
            Assert.True((info.fMask & Shell32.CMIC.CMIC_MASK_UNICODE) != 0);
            // lpVerb = ponteiro ANSI (string, HIWORD!=0) — nunca MAKEINTRESOURCE.
            Assert.Equal(ansi, (IntPtr)info.lpVerb);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)info.lpVerb);
            Assert.Equal((uint)Marshal.SizeOf<Shell32.CMINVOKECOMMANDINFOEX>(), info.cbSize);
            Assert.Equal(ShowWindowCommand.SW_SHOWNORMAL, info.nShow);
        }
        finally
        {
            Marshal.FreeHGlobal(ansi);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildInvokeInfoForVerb_ShiftDown_EspelhaQueryEstendida(bool shiftDown)
    {
        IntPtr ansi = Marshal.StringToHGlobalAnsi("SevenZipCompressToZip");
        try
        {
            var info = ShellThirdPartyInvoke.BuildInvokeInfoForVerb(
                "SevenZipCompressToZip", ansi, HWND.NULL, null, shiftDown);

            Assert.Equal(shiftDown, (info.fMask & Shell32.CMIC.CMIC_MASK_SHIFT_DOWN) != 0);
        }
        finally
        {
            Marshal.FreeHGlobal(ansi);
        }
    }

    [Fact]
    public void BuildInvokeInfoForVerb_ComPonto_LigaPtInvoke_E_SemPonto_Desliga()
    {
        IntPtr ansi = Marshal.StringToHGlobalAnsi("SevenZipCompressToZip");
        try
        {
            var withPoint = ShellThirdPartyInvoke.BuildInvokeInfoForVerb(
                "SevenZipCompressToZip", ansi, HWND.NULL, new POINT { X = 10, Y = 20 }, shiftDown: false);
            var withoutPoint = ShellThirdPartyInvoke.BuildInvokeInfoForVerb(
                "SevenZipCompressToZip", ansi, HWND.NULL, null, shiftDown: false);

            Assert.True((withPoint.fMask & Shell32.CMIC.CMIC_MASK_PTINVOKE) != 0);
            Assert.Equal(10, withPoint.ptInvoke.X);
            Assert.Equal(20, withPoint.ptInvoke.Y);
            Assert.True((withoutPoint.fMask & Shell32.CMIC.CMIC_MASK_PTINVOKE) == 0);
        }
        finally
        {
            Marshal.FreeHGlobal(ansi);
        }
    }

    [Fact]
    public void BuildInvokeInfo_Unicode_SempreLigado()
    {
        var info = ShellThirdPartyInvoke.BuildInvokeInfo(3, HWND.NULL, null, shiftDown: false);

        // Caminho com acento é bug garantido sem CMIC_MASK_UNICODE (§1).
        Assert.True((info.fMask & Shell32.CMIC.CMIC_MASK_UNICODE) != 0);
    }

    [Fact]
    public void BuildInvokeInfo_ComPonto_LigaPtInvoke_E_SemPonto_Desliga()
    {
        var withPoint = ShellThirdPartyInvoke.BuildInvokeInfo(
            3, HWND.NULL, new POINT { X = 10, Y = 20 }, shiftDown: false);
        var withoutPoint = ShellThirdPartyInvoke.BuildInvokeInfo(
            3, HWND.NULL, null, shiftDown: false);

        Assert.True((withPoint.fMask & Shell32.CMIC.CMIC_MASK_PTINVOKE) != 0);
        Assert.Equal(10, withPoint.ptInvoke.X);
        Assert.Equal(20, withPoint.ptInvoke.Y);
        Assert.True((withoutPoint.fMask & Shell32.CMIC.CMIC_MASK_PTINVOKE) == 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildInvokeInfo_ShiftDown_EspelhaQueryEstendida(bool shiftDown)
    {
        var info = ShellThirdPartyInvoke.BuildInvokeInfo(3, HWND.NULL, null, shiftDown);

        Assert.Equal(shiftDown, (info.fMask & Shell32.CMIC.CMIC_MASK_SHIFT_DOWN) != 0);
    }

    [Fact]
    public void BuildInvokeInfo_CabeçalhoValido_HwndENshow()
    {
        var hwnd = new HWND((IntPtr)12345);
        var info = ShellThirdPartyInvoke.BuildInvokeInfo(7, hwnd, null, shiftDown: false);

        Assert.Equal((uint)Marshal.SizeOf<Shell32.CMINVOKECOMMANDINFOEX>(), info.cbSize);
        Assert.Equal(hwnd, info.hwnd);
        Assert.Equal(ShowWindowCommand.SW_SHOWNORMAL, info.nShow);
    }

    [Theory]
    [InlineData(0u, true)]
    [InlineData(0x7FFEu, true)]
    [InlineData(0x7FFFu, false)]
    [InlineData(0xFFFFFFFFu, false)]
    public void IsOffsetInRange_LimitesDoHmenuFantasma(uint offset, bool expected)
    {
        Assert.Equal(expected, ShellHostProtocol.IsOffsetInRange(offset));
    }

    [Fact]
    public void IsValidationSuccess_SóS_OK_Autoriza()
    {
        // S_FALSE tem severity success — checar Failed NÃO basta.
        Assert.True(ShellThirdPartyInvoke.IsValidationSuccess(HRESULT.S_OK));
        Assert.False(ShellThirdPartyInvoke.IsValidationSuccess(HRESULT.S_FALSE));
        Assert.False(ShellThirdPartyInvoke.IsValidationSuccess((HRESULT)unchecked((int)0x80004005)));
    }

    [Fact]
    public void CreateHandle_CaminhoNuloOuVazio_DevolveNuloSemLançar()
    {
        var service = new ThirdPartyMenuService();

        Assert.Null(service.CreateHandle(null, "SevenZipCompressToZip", 0));
        Assert.Null(service.CreateHandle("   ", "SevenZipCompressToZip", 0));
    }

    [Fact]
    public void CreateHandle_VerboVazio_OffsetForaDaFaixa_DevolveNulo()
    {
        var service = new ThirdPartyMenuService();

        // Fallback offset exige faixa válida quando não há verbo estável.
        Assert.Null(service.CreateHandle(@"C:\a\doc.zip", "", 0xFFFFu));
        Assert.Null(service.CreateHandle(@"C:\a\doc.zip", null, 0xFFFFu));
    }

    [Fact]
    public void CreateHandle_ComVerboEstável_OffsetIrrelevante_CapturaVerbo()
    {
        var service = new ThirdPartyMenuService();

        // Caminho-verbo é imune a reordenação: offset fora da faixa não barra.
        var handle = service.CreateHandle(@"C:\a\doc.zip", "SevenZipCompressToZip", 0xFFFFu);

        Assert.NotNull(handle);
        Assert.Equal("SevenZipCompressToZip", handle.Verb);
    }

    [Fact]
    public void CreateHandle_Válido_CapturaCaminhoVerboOffsetEFlags()
    {
        var service = new ThirdPartyMenuService();

        var handle = service.CreateHandle(@"C:\a\doc.zip", "SevenZipCompressToZip", 5);

        Assert.NotNull(handle);
        Assert.Equal(@"C:\a\doc.zip", handle.Path);
        Assert.Equal("SevenZipCompressToZip", handle.Verb);
        Assert.Equal(5u, handle.CommandOffset);
    }

    [Fact]
    public void CreateHandle_VerboNulo_NormalizaParaVazio()
    {
        var service = new ThirdPartyMenuService();

        var handle = service.CreateHandle(@"C:\a\doc.zip", null, 5);

        Assert.NotNull(handle);
        Assert.Equal(string.Empty, handle.Verb);
    }

    [Fact]
    public void TryInvoke_Guardas_DevolveFalseSemLançar()
    {
        var service = new ThirdPartyMenuService();

        Assert.False(service.TryInvoke(null, IntPtr.Zero, null));
        Assert.False(service.TryInvoke(new ThirdPartyInvokeHandle("", "", 0, false), IntPtr.Zero, null));
        // Fallback offset fora da faixa nem chega à STA (verbo vazio).
        Assert.False(service.TryInvoke(
            new ThirdPartyInvokeHandle(@"C:\a\doc.zip", "", 0xFFFFu, false), IntPtr.Zero, null));
    }

    [Fact]
    public void TryInvoke_CaminhoInexistente_DevolveFalseSemLançar()
    {
        var service = new ThirdPartyMenuService();
        var handle = new ThirdPartyInvokeHandle(@"C:\caminho\que\nao\existe\xyz123.qqq", "SevenZipCompressToZip", 0, false);

        Assert.False(service.TryInvoke(handle, IntPtr.Zero, null));
    }
}
