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

    // FIX fundo (espaço vazio/barra): o flag Background atravessa o protocolo
    // nos 3 pedidos (query/invoke/invoke-by-label). Default false = item.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void QueryRequest_RoundTrip_PreservaBackground(bool background)
    {
        string line = ShellHostProtocol.SerializeQueryRequest(@"C:\a\pasta", extended: false, background: background);

        var req = ShellHostProtocol.ParseQueryRequest(line);

        Assert.NotNull(req);
        Assert.Equal(background, req.Background);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvokeRequest_RoundTrip_PreservaBackground(bool background)
    {
        string line = ShellHostProtocol.SerializeInvokeRequest(
            @"C:\a\pasta", "DesktopBackgroundVerb", 0, extended: false, hwnd: 0, x: null, y: null, background: background);

        var req = ShellHostProtocol.ParseInvokeRequest(line);

        Assert.NotNull(req);
        Assert.Equal(background, req.Background);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvokeByLabelRequest_RoundTrip_PreservaBackground(bool background)
    {
        string line = ShellHostProtocol.SerializeInvokeByLabelRequest(
            @"C:\a\pasta", new List<string> { "Exibir", "Ícones grandes" }, extended: true, hwnd: 0, x: null, y: null, background: background);

        var req = ShellHostProtocol.ParseInvokeByLabelRequest(line);

        Assert.NotNull(req);
        Assert.Equal(background, req.Background);
        Assert.True(req.Extended);
    }

    [Fact]
    public void Background_DefaultFalse_ItemIntacto()
    {
        // Chamador que não passa o flag continua pedindo item (fundo opt-in).
        Assert.False(ShellHostProtocol.ParseQueryRequest(
            ShellHostProtocol.SerializeQueryRequest(@"C:\a\pasta", false))?.Background ?? true);
        Assert.False(ShellHostProtocol.ParseInvokeRequest(
            ShellHostProtocol.SerializeInvokeRequest(@"C:\a\pasta", "v", 0, false, 0, null, null))?.Background ?? true);
        Assert.False(ShellHostProtocol.ParseInvokeByLabelRequest(
            ShellHostProtocol.SerializeInvokeByLabelRequest(@"C:\a\pasta", new List<string> { "A" }, false, 0, null, null))?.Background ?? true);
    }

    [Fact]
    public void Background_JsonAntigoSemCampo_AssumeItem()
    {
        // Compat: JSON de host/cliente anterior (sem "Background") = item.
        var query = ShellHostProtocol.ParseQueryRequest(
            """{"Op":"query","Path":"C:\\a\\pasta","Extended":false}""");
        var invoke = ShellHostProtocol.ParseInvokeRequest(
            """{"Op":"invoke","Path":"C:\\a\\pasta","Verb":"v","Offset":1,"Extended":false,"Hwnd":0,"X":null,"Y":null}""");
        var byLabel = ShellHostProtocol.ParseInvokeByLabelRequest(
            """{"Op":"invoke-by-label","Path":"C:\\a\\pasta","Extended":false,"Labels":["A"],"Hwnd":0,"X":null,"Y":null}""");

        Assert.NotNull(query);
        Assert.False(query.Background);
        Assert.NotNull(invoke);
        Assert.False(invoke.Background);
        Assert.NotNull(byLabel);
        Assert.False(byLabel.Background);
    }

    [Fact]
    public void DropTimeout_OrçamentoMaiorQueInvoke_ArquivarGBsDemora()
    {
        // F3: drop (120s) > invoke (30s) — o handler mostra progresso próprio.
        Assert.Equal(120000, ShellHostProtocol.DropTimeoutMs);
        Assert.True(ShellHostProtocol.DropTimeoutMs > ShellHostProtocol.InvokeTimeoutMs);
    }

    [Fact]
    public void DropRequest_RoundTrip_PreservaContainerEArquivos()
    {
        string line = ShellHostProtocol.SerializeDropRequest(
            @"C:\a\pacote.zip", new List<string> { @"C:\a\doc.txt", @"C:\a\pasta" });

        Assert.True(ShellHostProtocol.TryReadOp(line, out string op));
        Assert.Equal(ShellHostProtocol.DropOp, op);
        var req = ShellHostProtocol.ParseDropRequest(line);

        Assert.NotNull(req);
        Assert.Equal(@"C:\a\pacote.zip", req.ContainerPath);
        Assert.Equal(new List<string> { @"C:\a\doc.txt", @"C:\a\pasta" }, req.Files);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-json")]
    public void DropRequest_Lixo_RetornaNulo(string? line)
    {
        Assert.Null(ShellHostProtocol.ParseDropRequest(line));
    }

    [Fact]
    public void DropRequest_ContainerVazioOuSemArquivos_RetornaNulo()
    {
        Assert.Null(ShellHostProtocol.SerializeDropRequest("", new List<string> { @"C:\a\doc.txt" }) is string s1
            ? ShellHostProtocol.ParseDropRequest(s1) : null);
        Assert.Null(ShellHostProtocol.ParseDropRequest(
            ShellHostProtocol.SerializeDropRequest(@"C:\a\pacote.zip", new List<string>())));
        Assert.Null(ShellHostProtocol.ParseDropRequest(
            ShellHostProtocol.SerializeDropRequest(@"C:\a\pacote.zip", new List<string> { "  " })));
    }

    [Fact]
    public void DropRequest_OpErrada_RetornaNulo()
    {
        Assert.Null(ShellHostProtocol.ParseDropRequest(
            """{"Op":"invoke","ContainerPath":"C:\\a\\pacote.zip","Files":["C:\\a\\doc.txt"]}"""));
    }
}
