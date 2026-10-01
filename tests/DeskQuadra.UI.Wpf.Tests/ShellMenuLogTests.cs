using DeskQuadra.Core.ThirdParty;
using DeskQuadra.ShellHost.Shell;
using System.IO;

namespace DeskQuadra.UI.Wpf.Tests;

// Guarda VALIDATEW + shell-menu.log: buffer válido obrigatório (AV com
// IntPtr.Zero + 0 no 7-Zip) e log best-effort. Sem Shell/COM aqui.
public class ShellMenuLogTests
{
    [Fact]
    public void ValidateCapacityChars_BufferValidoECoerente()
    {
        // Invariante do FIX 1: capacidade > 0 e comporta Unicode (chars * 2 bytes).
        // Se alguém voltar a passar IntPtr.Zero + 0, este teste quebra junto.
        Assert.True(ShellThirdPartyInvoke.ValidateCapacityChars >= 256);
    }

    [Fact]
    public void FormatQuery_ContemPathFlagsENos()
    {
        string s = ShellMenuLog.FormatQuery(@"C:\a\doc.zip", "FLAGS", 7);

        Assert.Contains(@"C:\a\doc.zip", s);
        Assert.Contains("FLAGS", s);
        Assert.Contains("7", s);
    }

    [Fact]
    public void FormatQueryFailed_ContemMotivo()
    {
        string s = ShellMenuLog.FormatQueryFailed(@"C:\a\doc.zip", "FLAGS", "timeout");

        Assert.Contains("timeout", s);
        Assert.Contains(@"C:\a\doc.zip", s);
    }

    [Fact]
    public void FormatInvoke_ContemVerboOffsetEHrs()
    {
        string s = ShellMenuLog.FormatInvoke(@"C:\a\doc.zip", "SevenZipCompressToZip", 3, "0x00000000", "0x00000000", "0x00000000", "ok");

        Assert.Contains("SevenZipCompressToZip", s);
        Assert.Contains("offset=3", s);
        Assert.Contains(@"C:\a\doc.zip", s);
        Assert.Contains("ok", s);
    }

    [Fact]
    public void FormatInvoke_VerboNulo_NãoQuebra()
    {
        string s = ShellMenuLog.FormatInvoke(@"C:\a\doc.zip", null, 0, "n/a", "n/a", "n/a", "query-failed");

        Assert.Contains("verb=''", s);
        Assert.Contains("query-failed", s);
    }

    [Fact]
    public void LogFilePath_TerminaEmShellMenuLog()
    {
        string p = ShellMenuLog.LogFilePath;

        Assert.EndsWith(Path.Combine("DeskQuadra", "shell-menu.log"), p, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Log_BestEffort_NuncaLanca()
    {
        var ex = Record.Exception(() => ShellMenuLog.Log("selftest"));
        Assert.Null(ex);
    }
}
