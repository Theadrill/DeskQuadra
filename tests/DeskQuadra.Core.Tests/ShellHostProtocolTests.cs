using DeskQuadra.Core.ThirdParty;

namespace DeskQuadra.Core.Tests;

// T5 isolamento: protocolo UI ↔ ShellHost (lógica pura movida/criada).
// Sem Shell/COM, sem processo — só JSON + guardas + mapeamento.
public class ShellHostProtocolTests
{
    [Fact]
    public void Timeouts_OrçamentoDoPlano()
    {
        // § T5: query ~3s / invoke ~30s (fonte única, cliente e host usam).
        Assert.Equal(3000, ShellHostProtocol.QueryTimeoutMs);
        Assert.Equal(30000, ShellHostProtocol.InvokeTimeoutMs);
    }

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
    [InlineData(0u, true)]
    [InlineData(0x7FFEu, true)]
    [InlineData(0x7FFFu, false)]
    [InlineData(0xFFFFFFFFu, false)]
    public void IsOffsetInRange_LimitesDoHmenuFantasma(uint offset, bool expected)
    {
        Assert.Equal(expected, ShellHostProtocol.IsOffsetInRange(offset));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void QueryRequest_RoundTrip_PreservaCaminhoEExtended(bool extended)
    {
        string line = ShellHostProtocol.SerializeQueryRequest(@"C:\a\doc.zip", extended);

        Assert.True(ShellHostProtocol.TryReadOp(line, out string op));
        Assert.Equal(ShellHostProtocol.QueryOp, op);
        var req = ShellHostProtocol.ParseQueryRequest(line);

        Assert.NotNull(req);
        Assert.Equal(@"C:\a\doc.zip", req.Path);
        Assert.Equal(extended, req.Extended);
    }

    [Fact]
    public void InvokeRequest_RoundTrip_PreservaTudo_InclusiveHwndEPonto()
    {
        string line = ShellHostProtocol.SerializeInvokeRequest(
            @"C:\a\doc.zip", "SevenZipCompressToZip", 5, extended: true, hwnd: 12345, x: 10, y: 20);

        Assert.True(ShellHostProtocol.TryReadOp(line, out string op));
        Assert.Equal(ShellHostProtocol.InvokeOp, op);
        var req = ShellHostProtocol.ParseInvokeRequest(line);

        Assert.NotNull(req);
        Assert.Equal(@"C:\a\doc.zip", req.Path);
        Assert.Equal("SevenZipCompressToZip", req.Verb);
        Assert.Equal(5u, req.Offset);
        Assert.True(req.Extended);
        Assert.Equal(12345L, req.Hwnd);
        Assert.Equal(10, req.X);
        Assert.Equal(20, req.Y);
    }

    [Fact]
    public void InvokeRequest_SemPonto_PontoNuloAtravessa()
    {
        string line = ShellHostProtocol.SerializeInvokeRequest(
            @"C:\a\doc.zip", "SevenZipCompressToZip", 0, extended: false, hwnd: 0, x: null, y: null);

        var req = ShellHostProtocol.ParseInvokeRequest(line);

        Assert.NotNull(req);
        Assert.Null(req.X);
        Assert.Null(req.Y);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lixo{{{")]
    [InlineData("[]")]
    public void Parse_PedidoRuim_DevolveNuloSemLançar(string? line)
    {
        Assert.False(ShellHostProtocol.TryReadOp(line, out _));
        Assert.Null(ShellHostProtocol.ParseQueryRequest(line));
        Assert.Null(ShellHostProtocol.ParseInvokeRequest(line));
        Assert.Null(ShellHostProtocol.ParseQueryResponse(line));
        Assert.Null(ShellHostProtocol.ParseInvokeResponse(line));
    }

    [Fact]
    public void Parse_ObjetoVazio_ComportamentoSeguro_Placeholder()
    {
        // "{}" é JSON válido mas sem "op": pedidos rejeitados; respostas
        // viram objeto vazio — que o cliente trata como placeholder/false.
        Assert.False(ShellHostProtocol.TryReadOp("{}", out _));
        Assert.Null(ShellHostProtocol.ParseQueryRequest("{}"));
        Assert.Null(ShellHostProtocol.ParseInvokeRequest("{}"));
        Assert.Empty(ShellHostProtocol.ToEntries(ShellHostProtocol.ParseQueryResponse("{}")));
        Assert.False(ShellHostProtocol.ParseInvokeResponse("{}")?.Ok ?? true);
    }

    [Fact]
    public void Parse_OpTrocada_Rejeita()
    {
        string queryAsInvoke = ShellHostProtocol.SerializeQueryRequest(@"C:\a\x.zip", false);
        string invokeAsQuery = ShellHostProtocol.SerializeInvokeRequest(
            @"C:\a\x.zip", "v", 0, false, 0, null, null);

        Assert.Null(ShellHostProtocol.ParseInvokeRequest(queryAsInvoke));
        Assert.Null(ShellHostProtocol.ParseQueryRequest(invokeAsQuery));
    }

    [Fact]
    public void ParseInvoke_FallbackOffsetForaDaFaixa_Rejeita()
    {
        // Verbo vazio + offset inválido nem chega ao engine (guarda do protocolo).
        string line = ShellHostProtocol.SerializeInvokeRequest(
            @"C:\a\doc.zip", string.Empty, 0xFFFFu, false, 0, null, null);

        Assert.Null(ShellHostProtocol.ParseInvokeRequest(line));
    }

    [Fact]
    public void QueryResponse_RoundTrip_PreservaCascata()
    {
        var entries = new List<ThirdPartyMenuEntry>
        {
            new("7-Zip", string.Empty, 0, new List<ThirdPartyMenuEntry>
            {
                new("Add to archive...", "SevenZipAdd", 3, Array.Empty<ThirdPartyMenuEntry>()),
                new("Extrair aqui", "SevenZipExtract", 4, Array.Empty<ThirdPartyMenuEntry>()),
            }),
            new("Abrir com Code", "openwithcode", 9, Array.Empty<ThirdPartyMenuEntry>()),
        };

        string line = ShellHostProtocol.SerializeQueryResponse(entries);
        var response = ShellHostProtocol.ParseQueryResponse(line);

        Assert.NotNull(response);
        var back = ShellHostProtocol.ToEntries(response);

        Assert.Equal(2, back.Count);
        Assert.Equal("7-Zip", back[0].Label);
        Assert.Equal(2, back[0].Children.Count);
        Assert.Equal("Add to archive...", back[0].Children[0].Label);
        Assert.Equal("SevenZipAdd", back[0].Children[0].Verb);
        Assert.Equal(3u, back[0].Children[0].CommandOffset);
        Assert.Equal("Abrir com Code", back[1].Label);
    }

    [Fact]
    public void QueryResponse_ComAcento_RoundTripIntacto()
    {
        var entries = new List<ThirdPartyMenuEntry>
        {
            new("Comprimir e enviar por e-mail", "SevenZipMail", 1, Array.Empty<ThirdPartyMenuEntry>()),
        };

        var back = ShellHostProtocol.ToEntries(
            ShellHostProtocol.ParseQueryResponse(ShellHostProtocol.SerializeQueryResponse(entries)));

        Assert.Single(back);
        Assert.Equal("Comprimir e enviar por e-mail", back[0].Label);
    }

    [Fact]
    public void ToEntries_NuloOuVazio_DevolveVazio()
    {
        Assert.Empty(ShellHostProtocol.ToEntries(null));
        Assert.Empty(ShellHostProtocol.ToEntries(new ShellHostQueryResponse(null, "x")));
        Assert.Empty(ShellHostProtocol.ToEntries(new ShellHostQueryResponse(new List<ShellHostMenuItem>(), null)));
    }

    [Fact]
    public void InvokeResponse_RoundTrip_OkEErro()
    {
        var ok = ShellHostProtocol.ParseInvokeResponse(ShellHostProtocol.SerializeInvokeResponse(true));
        var failed = ShellHostProtocol.ParseInvokeResponse(ShellHostProtocol.SerializeInvokeResponse(false, "invoke-failed"));

        Assert.NotNull(ok);
        Assert.True(ok.Ok);
        Assert.NotNull(failed);
        Assert.False(failed.Ok);
        Assert.Equal("invoke-failed", failed.Error);
    }
}
