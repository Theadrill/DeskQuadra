using System.IO;
using DeskQuadra.Core.ThirdParty;
using DeskQuadra.Infrastructure.WindowsShell.Shell;

namespace DeskQuadra.UI.Wpf.Tests;

// T8c refresh manual do tray: ClearCache esvazia os buckets (item/fundo/
// extended) e a próxima chamada reconsulta o host (mesmo com TTL fresco).
public class ThirdPartyMenuServiceClearCacheTests
{
    private sealed class FakeProcess : IShellHostProcess
    {
        public string Output = string.Empty;
        public bool ExitResult = true;

        public void WriteRequest(string requestLine)
        {
        }

        public Task<string> ReadOutputAsync() => Task.FromResult(Output);

        public bool WaitForExit(int timeoutMs) => ExitResult;

        public void Kill()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class CountingLauncher : IShellHostLauncher
    {
        public readonly FakeProcess Process = new();
        public int Calls;

        public IShellHostProcess? Start()
        {
            Calls++;
            return Process;
        }
    }

    private static string PositiveLine() => ShellHostProtocol.SerializeQueryResponse(
        new List<ThirdPartyMenuEntry>
        {
            new("App Novo", "AppNovoVerb", 7, Array.Empty<ThirdPartyMenuEntry>()),
        });

    private static string CreateTempFile(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), "dq-t8c-" + Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void ClearCache_EsvaziaEProximaChamadaReconsulta()
    {
        var launcher = new CountingLauncher();
        launcher.Process.Output = PositiveLine();
        DateTime now = DateTime.UtcNow;
        var service = new ThirdPartyMenuService(launcher, () => now);
        string file = CreateTempFile(".t8cclear" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Assert.Single(service.GetForPath(file));
            Assert.Equal(1, launcher.Calls);

            // Hit fresco: sem reconsulta.
            Assert.Single(service.GetForPath(file));
            Assert.Equal(1, launcher.Calls);

            // Refresh manual: esvazia mesmo com TTL fresco; próxima reconsulta.
            service.ClearCache();
            Assert.Single(service.GetForPath(file));
            Assert.Equal(2, launcher.Calls);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ClearCache_EmCacheVazio_NaoFalhaEConsultaNormal()
    {
        var launcher = new CountingLauncher();
        launcher.Process.Output = PositiveLine();
        DateTime now = DateTime.UtcNow;
        var service = new ThirdPartyMenuService(launcher, () => now);
        string file = CreateTempFile(".t8cempty" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            service.ClearCache();
            Assert.Single(service.GetForPath(file));
            Assert.Equal(1, launcher.Calls);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
