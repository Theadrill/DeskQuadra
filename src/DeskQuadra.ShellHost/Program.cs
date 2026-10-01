using System.Runtime.CompilerServices;
using System.Text;
using DeskQuadra.Core.ThirdParty;
using DeskQuadra.ShellHost.Shell;
using Vanara.PInvoke;

// Engine interno testável via xUnit (builders/flags/validade sem COM).
[assembly: InternalsVisibleTo("DeskQuadra.UI.Wpf.Tests")]

namespace DeskQuadra.ShellHost;

// T5 isolamento: processo STA sem janela que executa o motor de terceiros
// (query+enumeração+filtro / invoke) fora do app. A DLL de um handler nunca
// derruba a UI: se o host travar ou morrer, o cliente só vê "sem resposta" e
// o menu volta ao placeholder (silencioso, padrão do projeto).
//
// IPC (ver ShellHostProtocol, fonte da verdade): stdin/stdout em JSON, UMA
// linha por pedido/resposta, processo ONE-SHOT por operação — sem pipe
// nomeado, sem daemon, sem timer. O cliente spawna, escreve 1 linha, lê 1
// linha com timeout (query ~3s / invoke ~30s), Kill() se travar; a próxima
// operação spawna um host fresco ("relançamento" sem estado).
// Diagnóstico livre vai p/ stderr (o cliente ignora; útil rodando manual).
// shell-menu.log continua no mesmo arquivo (mesmo molde guardian.log).
internal static class Program
{
    [STAThread]
    static int Main()
    {
        // UTF-8 explícito em streams BRUTOS (nunca Console.InputEncoding:
        // com stdin redirecionado o setter lança IOException "identificador
        // inválido" — pego no xUnit ao spawnar o host real no teste de
        // caminho inexistente). Redirecionado ou manual, o byte é UTF-8 nas
        // duas pontas (rótulo com acento atravessa intacto).
        using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };

        string? line;
        try
        {
            // BOM defensivo: quem invoca via arquivo/pipe UTF-8 com BOM não
            // pode quebrar o parse (o cliente escreve sem BOM, mas o host
            // aceita — 1 linha, sem pedido = sem resposta).
            line = stdin.ReadLine()?.TrimStart('\uFEFF');
        }
        catch (Exception ex)
        {
            // Sem stdin legível não há pedido: linha de erro p/ não pendurar
            // cliente que ainda esteja lendo, e código 2 (sem pedido).
            WriteLine(stdout, ShellHostProtocol.SerializeInvokeResponse(false, $"stdin {ex.GetType().Name}"));
            return 2;
        }

        if (!ShellHostProtocol.TryReadOp(line, out string op))
        {
            WriteLine(stdout, ShellHostProtocol.SerializeInvokeResponse(false, "bad-request"));
            return 2;
        }

        try
        {
            if (op == ShellHostProtocol.QueryOp)
            {
                return RunQuery(stdout, line);
            }

            if (op == ShellHostProtocol.InvokeOp)
            {
                return RunInvoke(stdout, line);
            }

            WriteLine(stdout, ShellHostProtocol.SerializeInvokeResponse(false, $"unknown-op {op}"));
            return 2;
        }
        catch (Exception ex)
        {
            // Best-effort final: nunca sair sem 1 linha (cliente pendurado é
            // pior que resposta de erro — ele trata como placeholder/false).
            WriteLine(stdout, ShellHostProtocol.SerializeInvokeResponse(false, $"exception {ex.GetType().Name}"));
            return 1;
        }
    }

    private static int RunQuery(TextWriter stdout, string? line)
    {
        var req = ShellHostProtocol.ParseQueryRequest(line);
        if (req is null)
        {
            WriteLine(stdout, ShellHostProtocol.SerializeQueryResponse(Array.Empty<ThirdPartyMenuEntry>(), "bad-query"));
            return 2;
        }

        // Engine T2/T3 movido p/ cá sem mudar regra: query STA + enumeração
        // recursiva + filtro §1. Falha interna = lista vazia (o engine já loga
        // o motivo no shell-menu.log e nunca lança).
        var raw = ShellThirdPartyQuery.QueryForPath(req.Path, req.Extended);
        var entries = ThirdPartyTreeBuilder.Build(raw);
        WriteLine(stdout, ShellHostProtocol.SerializeQueryResponse(entries));
        return 0;
    }

    private static int RunInvoke(TextWriter stdout, string? line)
    {
        var req = ShellHostProtocol.ParseInvokeRequest(line);
        if (req is null)
        {
            WriteLine(stdout, ShellHostProtocol.SerializeInvokeResponse(false, "bad-invoke"));
            return 2;
        }

        POINT? point = req.X.HasValue && req.Y.HasValue
            ? new POINT { X = req.X.Value, Y = req.Y.Value }
            : null;

        // Engine T3 movido p/ cá sem mudar regra: verbo estável preferido,
        // offset + VALIDATEW de fallback, Unicode sempre, mesma interface raiz.
        bool ok = ShellThirdPartyInvoke.TryInvoke(
            req.Path,
            req.Verb,
            req.Offset,
            req.Extended,
            new IntPtr(req.Hwnd),
            point);
        WriteLine(stdout, ShellHostProtocol.SerializeInvokeResponse(ok, ok ? null : "invoke-failed"));
        return 0;
    }

    private static void WriteLine(TextWriter stdout, string response)
    {
        try
        {
            stdout.WriteLine(response);
            stdout.Flush();
        }
        catch
        {
            // Cliente já foi embora (kill no timeout): nada a fazer.
        }
    }
}
