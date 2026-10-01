using DeskQuadra.Infrastructure.WindowsShell.Shell;
using DeskQuadra.ShellHost.Shell;
using Vanara.PInvoke;

namespace DeskQuadra.UI.Wpf.Tests;

// T2 terceiros: chave de cache por extensão + flags travadas §1 (puro, sem Shell).
public class ThirdPartyMenuServiceTests
{
    [Theory]
    [InlineData(@"C:\a\doc.ZIP", ".zip")]
    [InlineData(@"C:\a\nota.txt", ".txt")]
    [InlineData(@"C:\a\atalho.lnk", ".lnk")]
    [InlineData(@"C:\a\semext", "")]
    public void GetCacheKey_Arquivo_NormalizaExtensão(string path, string expected)
    {
        Assert.Equal(expected, ThirdPartyMenuService.GetCacheKey(path, isDirectory: false));
    }

    [Fact]
    public void GetCacheKey_Pasta_BucketÚnico()
    {
        Assert.Equal("<folder>", ThirdPartyMenuService.GetCacheKey(@"C:\a\pasta", isDirectory: true));
    }

    // T6 Shift: query com Shift (CMF_EXTENDEDVERBS) usa bucket separado — o
    // estendido nunca polui o normal e vice-versa (sonda T6: extended lista
    // itens a mais — 7-Zip Extrair no .txt, opennewprocess, Powershell).
    [Theory]
    [InlineData(@"C:\a\doc.zip", false, ".zip")]
    [InlineData(@"C:\a\doc.zip", true, ".zip|ext")]
    [InlineData(@"C:\a\nota.TXT", false, ".txt")]
    [InlineData(@"C:\a\nota.TXT", true, ".txt|ext")]
    public void GetCacheKey_Extended_BucketSeparado(string path, bool extended, string expected)
    {
        Assert.Equal(expected, ThirdPartyMenuService.GetCacheKey(path, isDirectory: false, extended));
    }

    [Fact]
    public void GetCacheKey_PastaExtended_BucketSeparado()
    {
        Assert.Equal("<folder>|ext", ThirdPartyMenuService.GetCacheKey(@"C:\a\pasta", isDirectory: true, extended: true));
        Assert.Equal("<folder>", ThirdPartyMenuService.GetCacheKey(@"C:\a\pasta", isDirectory: true, extended: false));
    }

    [Fact]
    public void BuildQueryFlags_BaseTravada_SemExtendedPorPadrão()
    {
        var flags = ShellThirdPartyQuery.BuildQueryFlags(includeExtendedVerbs: false);

        Assert.True(flags.HasFlag(Shell32.CMF.CMF_NORMAL));
        Assert.True(flags.HasFlag(Shell32.CMF.CMF_ITEMMENU));
        Assert.True(flags.HasFlag(Shell32.CMF.CMF_SYNCCASCADEMENU));
        Assert.False(flags.HasFlag(Shell32.CMF.CMF_EXTENDEDVERBS));
        // Proibidos §1 nunca entram.
        Assert.False(flags.HasFlag(Shell32.CMF.CMF_EXPLORE));
        Assert.False(flags.HasFlag(Shell32.CMF.CMF_NODEFAULT));
        Assert.False(flags.HasFlag(Shell32.CMF.CMF_INCLUDESTATIC));
        Assert.False(flags.HasFlag(Shell32.CMF.CMF_DEFAULTONLY));
    }

    [Fact]
    public void BuildQueryFlags_ComShift_SomaExtendedVerbs()
    {
        var flags = ShellThirdPartyQuery.BuildQueryFlags(includeExtendedVerbs: true);

        Assert.True(flags.HasFlag(Shell32.CMF.CMF_EXTENDEDVERBS));
        Assert.True(flags.HasFlag(Shell32.CMF.CMF_NORMAL));
    }

    [Fact]
    public void GetForPath_Inexistente_DevolveVazioSemLançar()
    {
        var service = new ThirdPartyMenuService();

        Assert.Empty(service.GetForPath(@"C:\caminho\que\nao\existe\xyz123.qqq"));
        Assert.Empty(service.GetForPath(null));
        Assert.Empty(service.GetForPath("   "));
    }
}
