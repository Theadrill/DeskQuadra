using DeskQuadra.Core.ThirdParty;
using DeskQuadra.Infrastructure.WindowsShell.Shell;

namespace DeskQuadra.UI.Wpf.Tests;

// T5 isolamento: supervisão do host com processo FAKE (sem spawn real, sem
// Shell/COM). Prova o contrato do plano: host morto/travado/resposta ruim =
// placeholder silencioso (null) ou false; resposta boa = cardápio mapeado.
public class ShellHostClientTests
{
    private sealed class FakeProcess : IShellHostProcess
    {
        public string? WrittenLine;
        public string Output = string.Empty;
        public bool ExitResult = true;
        public int WaitTimeoutMs = -1;
        public bool KillCalled;
        public bool DisposeCalled;

        public void WriteRequest(string requestLine) => WrittenLine = requestLine;

        public Task<string> ReadOutputAsync() => Task.FromResult(Output);

        public bool WaitForExit(int timeoutMs)
        {
            WaitTimeoutMs = timeoutMs;
            return ExitResult;
        }

        public void Kill() => KillCalled = true;

        public void Dispose() => DisposeCalled = true;
    }

    private sealed class FakeLauncher : IShellHostLauncher
    {
        public IShellHostProcess? ToReturn = new FakeProcess();

        public IShellHostProcess? Start() => ToReturn;
    }

    private static (ShellHostClient Client, FakeLauncher Launcher, FakeProcess Process) Create()
    {
        var launcher = new FakeLauncher();
        var process = new FakeProcess();
        launcher.ToReturn = process;
        return (new ShellHostClient(launcher), launcher, process);
    }

    private static string QueryResponseLine()
    {
        var entries = new List<ThirdPartyMenuEntry>
        {
            new("7-Zip", string.Empty, 0, new List<ThirdPartyMenuEntry>
            {
                new("Add to archive...", "SevenZipAdd", 3, Array.Empty<ThirdPartyMenuEntry>()),
            }),
        };
        return ShellHostProtocol.SerializeQueryResponse(entries);
    }

    [Fact]
    public void Query_HostRespondeCardapio_DevolveEntradasMapeadas()
    {
        var (client, _, process) = Create();
        process.Output = QueryResponseLine() + "\n";

        var entries = client.QueryMenu(@"C:\a\doc.zip", extended: false);

        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal("7-Zip", entries[0].Label);
        Assert.Single(entries[0].Children);
        Assert.Equal("SevenZipAdd", entries[0].Children[0].Verb);
        Assert.Equal(3u, entries[0].Children[0].CommandOffset);
        Assert.False(process.KillCalled);
        Assert.True(process.DisposeCalled);
    }

    [Fact]
    public void Query_PedidoUsaTimeoutDeQuery()
    {
        var (client, _, process) = Create();
        process.Output = QueryResponseLine();

        client.QueryMenu(@"C:\a\doc.zip", extended: true);

        // Orçamento do plano atravessa no WaitForExit (query ~3s).
        Assert.Equal(ShellHostProtocol.QueryTimeoutMs, process.WaitTimeoutMs);
        Assert.Contains("\"Op\":\"query\"", process.WrittenLine ?? string.Empty);
    }

    [Fact]
    public void Query_HostTrava_KillEDevolveNulo()
    {
        var (client, _, process) = Create();
        process.ExitResult = false;

        Assert.Null(client.QueryMenu(@"C:\a\doc.zip", extended: false));
        Assert.True(process.KillCalled);
    }

    [Fact]
    public void Query_HostAusente_DevolveNuloSemLançar()
    {
        var launcher = new FakeLauncher { ToReturn = null };
        var client = new ShellHostClient(launcher);

        Assert.Null(client.QueryMenu(@"C:\a\doc.zip", extended: false));
    }

    [Fact]
    public void Query_RespostaRuim_DevolveNuloSemLançar()
    {
        // Saída que não é JSON de resposta: placeholder silencioso.
        var bad = new FakeLauncher { ToReturn = new FakeProcess { Output = "lixo{{{", ExitResult = true } };

        Assert.Null(new ShellHostClient(bad).QueryMenu(@"C:\a\doc.zip", extended: false));
    }

    [Fact]
    public void Invoke_HostRespondeOk_DevolveTrue()
    {
        var (client, _, process) = Create();
        process.Output = ShellHostProtocol.SerializeInvokeResponse(true);

        bool ok = client.InvokeMenu(@"C:\a\doc.zip", "SevenZipAdd", 3, false, hwnd: 123, x: 1, y: 2);

        Assert.True(ok);
        Assert.False(process.KillCalled);
        Assert.Contains("SevenZipAdd", process.WrittenLine ?? string.Empty);
    }

    [Fact]
    public void Invoke_PedidoUsaTimeoutDeInvoke()
    {
        var (client, _, process) = Create();
        process.Output = ShellHostProtocol.SerializeInvokeResponse(true);

        client.InvokeMenu(@"C:\a\doc.zip", "SevenZipAdd", 3, false, hwnd: 0, x: null, y: null);

        // Orçamento do plano (invoke ~30s — handler pode abrir modal).
        Assert.Equal(ShellHostProtocol.InvokeTimeoutMs, process.WaitTimeoutMs);
    }

    [Fact]
    public void Invoke_HostTrava_KillEDevolveFalse()
    {
        var (client, _, process) = Create();
        process.ExitResult = false;

        Assert.False(client.InvokeMenu(@"C:\a\doc.zip", "SevenZipAdd", 3, false, 0, null, null));
        Assert.True(process.KillCalled);
    }

    [Fact]
    public void Invoke_HostRejeita_DevolveFalse()
    {
        var (client, _, _) = Create();
        var bad = new FakeLauncher
        {
            ToReturn = new FakeProcess
            {
                Output = ShellHostProtocol.SerializeInvokeResponse(false, "invoke-failed"),
                ExitResult = true,
            },
        };

        Assert.False(new ShellHostClient(bad).InvokeMenu(@"C:\a\doc.zip", "SevenZipAdd", 3, false, 0, null, null));
    }
}
