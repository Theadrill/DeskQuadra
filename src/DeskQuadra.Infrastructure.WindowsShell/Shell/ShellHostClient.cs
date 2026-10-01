using System.Diagnostics;
using System.IO;
using System.Text;
using DeskQuadra.Core.ThirdParty;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T5 isolamento: supervisor do DeskQuadra.ShellHost (processo one-shot por
// operação — ver ShellHostProtocol p/ a decisão de IPC documentada).
// Responsabilidades: localizar o exe (ao lado da UI, mesmo molde do Guardian),
// spawnar sob demanda, escrever 1 linha JSON, esperar com timeout (query ~3s /
// invoke ~30s — WaitForExit, molde do Join da STA em T2/T3, SEM timer novo),
// Kill() se travar, e traduzir "sem resposta" em falha silenciosa (a UI volta
// ao placeholder T1). A próxima operação spawna um host fresco ("relança").
// Deadlock de pipe CHEIO: o stdout é drenado em background (ReadToEndAsync)
// ANTES do WaitForExit — resposta grande (menu cheio passa de 4KB) nunca trava
// o host. stderr NÃO é redirecionado (sem superfície de deadlock; diagnóstico
// do host vive no shell-menu.log). UTF-8 nas duas pontas (acento intacto).
// Testável: o spawn é injetável via IShellHostLauncher (fakes no xUnit).
internal sealed class ShellHostClient
{
    private readonly IShellHostLauncher _launcher;

    public ShellHostClient()
        : this(new ShellHostProcessLauncher(LocateHostExe()))
    {
    }

    internal ShellHostClient(IShellHostLauncher launcher)
    {
        _launcher = launcher;
    }

    public IReadOnlyList<ThirdPartyMenuEntry>? QueryMenu(string path, bool extended)
    {
        string request = ShellHostProtocol.SerializeQueryRequest(path, extended);
        string? line = RunHost(request, ShellHostProtocol.QueryTimeoutMs);
        if (line is null)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatHostQuery(path, extended, "no-response"));
            return null;
        }

        var response = ShellHostProtocol.ParseQueryResponse(line);
        if (response is null)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatHostQuery(path, extended, "bad-response"));
            return null;
        }

        if (!string.IsNullOrEmpty(response.Error))
        {
            ShellMenuLog.Log(ShellMenuLog.FormatHostQuery(path, extended, response.Error));
        }

        return ShellHostProtocol.ToEntries(response);
    }

    public bool InvokeMenu(
        string path,
        string verb,
        uint offset,
        bool extended,
        long hwnd,
        int? x,
        int? y)
    {
        string request = ShellHostProtocol.SerializeInvokeRequest(path, verb, offset, extended, hwnd, x, y);
        string? line = RunHost(request, ShellHostProtocol.InvokeTimeoutMs);
        if (line is null)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatHostInvoke(path, verb, offset, "no-response"));
            return false;
        }

        var response = ShellHostProtocol.ParseInvokeResponse(line);
        if (response is null)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatHostInvoke(path, verb, offset, "bad-response"));
            return false;
        }

        if (!response.Ok)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatHostInvoke(
                path, verb, offset, string.IsNullOrEmpty(response.Error) ? "rejected" : response.Error));
        }

        return response.Ok;
    }

    private string? RunHost(string requestLine, int timeoutMs)
    {
        IShellHostProcess? process = null;
        try
        {
            process = _launcher.Start();
            if (process is null)
            {
                // Exe ausente: host morto = placeholder silencioso (§ T5).
                return null;
            }

            // Drena o stdout em background ANTES de esperar (pipe cheio
            // travaria o host antes do timeout — ver comentário da classe).
            Task<string> drain = process.ReadOutputAsync();
            process.WriteRequest(requestLine);

            if (!process.WaitForExit(timeoutMs))
            {
                // Travou: kill (a próxima operação spawna um host fresco).
                process.Kill();
                return null;
            }

            try
            {
                // Saída limpa fecha o stdout → a drenagem já terminou; folga
                // curta p/ não pendurar o menu se algo fugir do esperado.
                if (!drain.Wait(1000))
                {
                    return null;
                }

                string output = drain.Result ?? string.Empty;
                int eol = output.IndexOf('\n');
                string firstLine = (eol >= 0 ? output.Substring(0, eol) : output).Trim();
                return firstLine.Length > 0 ? firstLine : null;
            }
            catch
            {
                return null;
            }
        }
        catch
        {
            // Silencioso, padrão do projeto: host morto = placeholder.
            return null;
        }
        finally
        {
            try
            {
                process?.Dispose();
            }
            catch
            {
                // Dispose best-effort: nunca quebra o menu.
            }
        }
    }

    // Mesmo molde do SpawnGuardianProcess (App.xaml.cs): primeiro ao lado da
    // UI (deploy copia p/ lá), depois os binários de build p/ dev (F5 sem
    // publish). Null = sem host (menu volta ao placeholder, silencioso).
    internal static string? LocateHostExe()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            [
                Path.Combine(baseDir, ShellHostProtocol.ExeName),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\DeskQuadra.ShellHost\bin\Debug\net8.0-windows\" + ShellHostProtocol.ExeName)),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\DeskQuadra.ShellHost\bin\Release\net8.0-windows\" + ShellHostProtocol.ExeName)),
            ];

            foreach (string candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Candidato inválido: tenta o próximo.
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

// Fronteira de processo injetável p/ teste (fake em xUnit: timeout, morte,
// resposta lenta). A implementação real é o one-shot stdin/stdout.
internal interface IShellHostLauncher
{
    IShellHostProcess? Start();
}

internal interface IShellHostProcess : IDisposable
{
    void WriteRequest(string requestLine);

    Task<string> ReadOutputAsync();

    bool WaitForExit(int timeoutMs);

    void Kill();
}

internal sealed class ShellHostProcessLauncher : IShellHostLauncher
{
    private readonly string? _exePath;

    public ShellHostProcessLauncher(string? exePath)
    {
        _exePath = exePath;
    }

    public IShellHostProcess? Start()
    {
        if (string.IsNullOrEmpty(_exePath))
        {
            return null;
        }

        try
        {
            if (!File.Exists(_exePath))
            {
                return null;
            }

            var psi = new ProcessStartInfo
            {
                FileName = _exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                // stderr propositalmente NÃO redirecionado (ver ShellHostClient).
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = false };
            if (!process.Start())
            {
                process.Dispose();
                return null;
            }

            return new ShellHostProcess(process);
        }
        catch
        {
            return null;
        }
    }

    private sealed class ShellHostProcess : IShellHostProcess
    {
        private readonly Process _process;
        private bool _disposed;

        public ShellHostProcess(Process process)
        {
            _process = process;
        }

        public void WriteRequest(string requestLine)
        {
            _process.StandardInput.WriteLine(requestLine);
            _process.StandardInput.Flush();
            _process.StandardInput.Close();
        }

        public Task<string> ReadOutputAsync()
        {
            return _process.StandardOutput.ReadToEndAsync();
        }

        public bool WaitForExit(int timeoutMs)
        {
            return _process.WaitForExit(timeoutMs);
        }

        public void Kill()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(1000);
                }
            }
            catch
            {
                // Kill best-effort: o SO recolhe o processo órfão.
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _process.Dispose();
            }
            catch
            {
                // Dispose best-effort.
            }
        }
    }
}
