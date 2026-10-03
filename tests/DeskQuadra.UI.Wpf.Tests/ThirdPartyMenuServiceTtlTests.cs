using System.IO;
using DeskQuadra.Core.ThirdParty;
using DeskQuadra.Infrastructure.WindowsShell.Shell;

namespace DeskQuadra.UI.Wpf.Tests;

// T8b TTL assimétrico no cache de terceiros: entrada = (entries, timestampUtc);
// negativo (vazio, inclui falha que resulte em vazio) = 60s, positivo = 300s.
// Hit fresco devolve sem reconsultar; hit expirado/miss reconsulta via
// QueryUncached e regrava com UtcNow. Buckets (ext/<folder>/<background>/|ext)
// intactos — chaves inalteradas.
public class ThirdPartyMenuServiceTtlTests
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

    private static string EmptyLine() => ShellHostProtocol.SerializeQueryResponse(
        Array.Empty<ThirdPartyMenuEntry>());

    private static string CreateTempFile(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), "dq-t8b-" + Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, "x");
        return path;
    }

    private static string UniqueExtension(string prefix) => "." + prefix + Guid.NewGuid().ToString("N").Substring(0, 8);

    [Fact]
    public void Negativo_ExpiraEReconsulta_Apos60s()
    {
        var launcher = new CountingLauncher();
        launcher.Process.Output = EmptyLine();
        DateTime now = DateTime.UtcNow;
        var service = new ThirdPartyMenuService(launcher, () => now);
        string file = CreateTempFile(UniqueExtension("t8bneg"));
        try
        {
            Assert.Empty(service.GetForPath(file));
            Assert.Equal(1, launcher.Calls);

            // Hit fresco negativo: sem reconsulta.
            Assert.Empty(service.GetForPath(file));
            Assert.Equal(1, launcher.Calls);

            // TTL negativo = 60s: após expirar, reconsulta o host.
            now = now.AddSeconds(ThirdPartyMenuService.NegativeTtlSeconds + 1);
            Assert.Empty(service.GetForPath(file));
            Assert.Equal(2, launcher.Calls);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Positivo_Fresco_NaoReconsulta()
    {
        var launcher = new CountingLauncher();
        launcher.Process.Output = PositiveLine();
        DateTime now = DateTime.UtcNow;
        var service = new ThirdPartyMenuService(launcher, () => now);
        string file = CreateTempFile(UniqueExtension("t8bpos"));
        try
        {
            var first = service.GetForPath(file);
            Assert.Single(first);
            Assert.Equal(1, launcher.Calls);

            var second = service.GetForPath(file);
            Assert.Single(second);
            Assert.Equal(1, launcher.Calls);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Positivo_Expirado_Reconsulta_Apos300s()
    {
        var launcher = new CountingLauncher();
        launcher.Process.Output = PositiveLine();
        DateTime now = DateTime.UtcNow;
        var service = new ThirdPartyMenuService(launcher, () => now);
        string file = CreateTempFile(UniqueExtension("t8bexp"));
        try
        {
            Assert.Single(service.GetForPath(file));
            Assert.Equal(1, launcher.Calls);

            // Ainda fresco pouco antes do TTL: sem reconsulta.
            now = now.AddSeconds(ThirdPartyMenuService.PositiveTtlSeconds - 1);
            Assert.Single(service.GetForPath(file));
            Assert.Equal(1, launcher.Calls);

            // Após o TTL positivo: reconsulta.
            now = now.AddSeconds(2);
            Assert.Single(service.GetForPath(file));
            Assert.Equal(2, launcher.Calls);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Buckets_Diferentes_NaoSeContaminam()
    {
        var launcher = new CountingLauncher();
        launcher.Process.Output = PositiveLine();
        DateTime now = DateTime.UtcNow;
        var service = new ThirdPartyMenuService(launcher, () => now);
        string fileA = CreateTempFile(UniqueExtension("t8ba"));
        string fileB = CreateTempFile(UniqueExtension("t8bb"));
        try
        {
            // Bucket A positivo.
            Assert.Single(service.GetForPath(fileA));
            Assert.Equal(1, launcher.Calls);

            // Host passa a devolver vazio (app desinstalado): bucket B é miss
            // e registra negativo, sem tocar no bucket A.
            launcher.Process.Output = EmptyLine();
            Assert.Empty(service.GetForPath(fileB));
            Assert.Equal(2, launcher.Calls);

            // Bucket A segue positivo do cache, sem reconsulta.
            var cachedA = service.GetForPath(fileA);
            Assert.Equal(2, launcher.Calls);
            Assert.Single(cachedA);
            Assert.Equal("AppNovoVerb", cachedA[0].Verb);
        }
        finally
        {
            File.Delete(fileA);
            File.Delete(fileB);
        }
    }
}
