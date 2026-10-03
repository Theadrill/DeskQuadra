using DeskQuadra.Core.ThirdParty;

namespace DeskQuadra.UI.Wpf.Tests;

// T2 terceiros: montagem da árvore filtrada a partir dos nós brutos do HMENU
// fantasma (cascata espelhada só com filho mantido; resto cai).
public class ThirdPartyTreeBuilderTests
{
    private static ShellMenuNode Leaf(string label, string? verb, uint offset = 7) =>
        new(label, verb, offset, IsSeparator: false, IsPopup: false, Array.Empty<ShellMenuNode>());

    private static ShellMenuNode Popup(string label, params ShellMenuNode[] children) =>
        new(label, null, 0, IsSeparator: false, IsPopup: true, children);

    [Fact]
    public void Build_Cascata7Zip_MantémPaisEFilhosTerceiros()
    {
        var raw = new[]
        {
            Leaf("Abrir", "open"),
            Popup("7-Zip",
                Leaf("Open archive", "7z.open", 1),
                Leaf("Extract files...", string.Empty, 2),
                ShellMenuNode.Separator,
                Leaf("Propriedades", "properties")),
        };

        var result = ThirdPartyTreeBuilder.Build(raw);

        var zip = Assert.Single(result);
        Assert.Equal("7-Zip", zip.Label);
        Assert.Equal(2, zip.Children.Count);
        Assert.Equal("Open archive", zip.Children[0].Label);
        Assert.Equal("7z.open", zip.Children[0].Verb);
        Assert.Equal("Extract files...", zip.Children[1].Label);
    }

    // COMPLEMENTOS (T6): "Enviar para" virou complemento (verbo implícito
    // sendto — o popup real chega com Verb=null) e o filho com verbo sendto
    // passa pela allowlist — a cascata SOBREVIVE (antes caía inteira).
    [Fact]
    public void Build_CascataEnviarParaComplemento_MantémPaisEFilhos()
    {
        var raw = new[] { Popup("Enviar para", Leaf("Documentos", "sendto")) };

        var result = ThirdPartyTreeBuilder.Build(raw);

        var popup = Assert.Single(result);
        Assert.Equal("Enviar para", popup.Label);
        var child = Assert.Single(popup.Children);
        Assert.Equal("sendto", child.Verb);
    }

    // COMPLEMENTOS (T6): "Incluir na biblioteca" continua nativa (cai inteira);
    // "Enviar para" virou complemento e SOBREVIVE sozinha no mesmo nível.
    [Fact]
    public void Build_CascataNativa_CaiInteiraPeloRótulo()
    {
        var raw = new[]
        {
            Popup("Incluir na biblioteca",
                Leaf("Documentos", string.Empty, 79),
                Leaf("Imagens", string.Empty, 80)),
            Popup("Enviar para", Leaf("Documentos", "sendto")),
        };

        var result = ThirdPartyTreeBuilder.Build(raw);

        var popup = Assert.Single(result);
        Assert.Equal("Enviar para", popup.Label);
    }

    // COMPLEMENTOS (T6, sonda .jpg): popup "Transmitir para Dispositivo" tem
    // verbo VAZIO nos dois níveis — passa pela allowlist por LABEL (único
    // sinal; a folha "casttodevice" com verbo passa pelo verbo). O filho sem
    // verbo cai no fallback por offset já existente (risco documentado no
    // TreeBuilder/engine: VALIDATEW não protege contra verbo trocado).
    [Fact]
    public void Build_CascataTransmitirComplemento_MantémPopupEFilhoSemVerbo()
    {
        var raw = new[]
        {
            Popup("Transmitir para Dispositivo",
                Leaf("Transmitir para Dispositivo", string.Empty, 203)),
            Leaf("Git Bash Here", "git.bash"),
        };

        var result = ThirdPartyTreeBuilder.Build(raw);

        Assert.Equal(2, result.Count);
        Assert.Equal("Transmitir para Dispositivo", result[0].Label);
        Assert.Equal("Git Bash Here", result[1].Label);
    }

    // COMPLEMENTOS (T6): folhas wallpaper/rotate/openas viraram complemento e
    // passam; vizinhas nativas (opennewprocess/powershell) continuam fora e a
    // terceira de terceiro sobrevive no mesmo nível.
    [Theory]
    [InlineData("setdesktopwallpaper", "Definir como fundo da área de trabalho")]
    [InlineData("rotate90", "Girar para a direita")]
    [InlineData("rotate270", "Girar para a esquerda")]
    [InlineData("openas", "Abrir com")]
    [InlineData("sendto", "Enviar para")]
    [InlineData("casttodevice", "Transmitir para Dispositivo")]
    public void Build_FolhaComplemento_PassaTerceiroVizinnhoFica(string verb, string label)
    {
        var raw = new[]
        {
            Leaf(label, verb),
            Leaf("Send with Tailscale...", "tailscale"),
        };

        var result = ThirdPartyTreeBuilder.Build(raw);

        Assert.Equal(2, result.Count);
        Assert.Equal(label, result[0].Label);
    }

    // Vizinhos nativos da sonda T6 que CONTINUAM fora (não são complementos).
    [Theory]
    [InlineData("opennewprocess", "Abrir em novo processo")]
    [InlineData("Powershell", "Abrir janela do PowerShell aqui")]
    public void Build_FolhaNativaSondaT6_CaiTerceiroVizinnhoFica(string verb, string label)
    {
        var raw = new[]
        {
            Leaf(label, verb),
            Leaf("Send with Tailscale...", "tailscale"),
        };

        var result = ThirdPartyTreeBuilder.Build(raw);

        var single = Assert.Single(result);
        Assert.Equal("Send with Tailscale...", single.Label);
    }

    [Fact]
    public void Build_FolhaSemTextoExibível_Cai()
    {
        var raw = new[] { Leaf("   ", "custom.verb") };

        Assert.Empty(ThirdPartyTreeBuilder.Build(raw));
    }

    [Fact]
    public void Build_SeparadorENativoCaem_TerceiroFica()
    {
        var raw = new[]
        {
            ShellMenuNode.Separator,
            Leaf("Recortar", "cut"),
            Leaf("Git Bash Here", "git.bash"),
        };

        var result = ThirdPartyTreeBuilder.Build(raw);

        var single = Assert.Single(result);
        Assert.Equal("Git Bash Here", single.Label);
    }

    // Cascata-complemento VAZIA/lazy (SendTo sem HandleMenuMsg): mantém o item
    // mesmo assim — com verbo implícito estável (sendto/openas) o invoke usa o
    // caminho-verbo existente; sem ele, offset fora da faixa = handle nulo =
    // item desabilitado (nunca invoca offset errado).
    [Fact]
    public void Build_CascataEnviarVazia_MantémComVerboImplícito()
    {
        var raw = new[] { Popup("Enviar para") };

        var result = ThirdPartyTreeBuilder.Build(raw);

        var single = Assert.Single(result);
        Assert.Equal("Enviar para", single.Label);
        Assert.Equal("sendto", single.Verb);
        Assert.Empty(single.Children);
    }

    [Fact]
    public void Build_CascataTransmitirVazia_MantémDesabilitadaSegura()
    {
        var raw = new[] { Popup("Transmitir para Dispositivo") };

        var result = ThirdPartyTreeBuilder.Build(raw);

        var single = Assert.Single(result);
        Assert.Equal("Transmitir para Dispositivo", single.Label);
        Assert.Empty(single.Children);
        // Offset fora da faixa => CreateHandle nulo => UI desabilita (seguro).
        Assert.False(DeskQuadra.Core.ThirdParty.ShellHostProtocol.IsOffsetInRange(single.CommandOffset));
    }

    // Cascata nativa NÃO-complemento vazia continua caindo (sem item fantasma).
    [Fact]
    public void Build_CascataNativaVazia_Cai()
    {
        var raw = new[] { Popup("Incluir na biblioteca") };

        Assert.Empty(ThirdPartyTreeBuilder.Build(raw));
    }

    [Fact]
    public void Build_Nulo_DevolveVazio()
    {
        Assert.Empty(ThirdPartyTreeBuilder.Build(null));
    }

    // FUNDO (espaço vazio/barra): complementos nativos NÃO passam — mesmo
    // CHEIOS caem inteiros; só terceiro genuíno (Git/7-Zip) sobrevive.
    [Fact]
    public void Build_Fundo_ComplementosCheios_Caem_TerceiroFica()
    {
        var raw = new[]
        {
            Popup("Enviar para", Leaf("Documentos", "sendto")),
            Popup("Abrir com", Leaf("Bloco de Notas", "openas")),
            Popup("Transmitir para Dispositivo",
                Leaf("Transmitir para Dispositivo", string.Empty, 203)),
            Leaf("Definir como fundo da área de trabalho", "setdesktopwallpaper"),
            Leaf("Girar para a direita", "rotate90"),
            Leaf("Girar para a esquerda", "rotate270"),
            Leaf("Transmitir para Dispositivo", "casttodevice"),
            Leaf("Enviar para", "sendto"),
            Leaf("Abrir com", "openas"),
            Leaf("Git Bash Here", "git.bash"),
            Popup("7-Zip", Leaf("Open archive", "7z.open", 1)),
        };

        var result = ThirdPartyTreeBuilder.Build(raw, background: true);

        Assert.Equal(2, result.Count);
        Assert.Equal("Git Bash Here", result[0].Label);
        Assert.Equal("7-Zip", result[1].Label);
    }

    // FUNDO: cascata-complemento VAZIA/lazy também cai (sem item fantasma —
    // o fallback "mantém vazia" vale SÓ p/ item, decisão T6 intacta).
    [Theory]
    [InlineData("Enviar para")]
    [InlineData("Abrir com")]
    [InlineData("Transmitir para Dispositivo")]
    public void Build_Fundo_ComplementoVazio_Cai(string label)
    {
        var raw = new[] { Popup(label) };

        Assert.Empty(ThirdPartyTreeBuilder.Build(raw, background: true));
    }

    // Denylist "Conceder acesso a"/"Give access to": folha e popup caem
    // INTEIROS (como "incluir na biblioteca") — no ITEM e no FUNDO.
    [Theory]
    [InlineData("Conceder acesso a")]
    [InlineData("Give access to")]
    public void Build_ConcederAcesso_Folha_Cai_ItemEFundo(string label)
    {
        var raw = new[]
        {
            Leaf(label, string.Empty),
            Leaf("Git Bash Here", "git.bash"),
        };

        var item = ThirdPartyTreeBuilder.Build(raw);
        var fundo = ThirdPartyTreeBuilder.Build(raw, background: true);

        var singleItem = Assert.Single(item);
        Assert.Equal("Git Bash Here", singleItem.Label);
        var singleFundo = Assert.Single(fundo);
        Assert.Equal("Git Bash Here", singleFundo.Label);
    }

    [Theory]
    [InlineData("Conceder acesso a")]
    [InlineData("Give access to")]
    public void Build_ConcederAcesso_Popup_CaiInteiro_ItemEFundo(string label)
    {
        var raw = new[]
        {
            Popup(label,
                Leaf("Pessoa 1", string.Empty, 11),
                Leaf("Pessoa 2", string.Empty, 12)),
            Leaf("Git Bash Here", "git.bash"),
        };

        var item = ThirdPartyTreeBuilder.Build(raw);
        var fundo = ThirdPartyTreeBuilder.Build(raw, background: true);

        var singleItem = Assert.Single(item);
        Assert.Equal("Git Bash Here", singleItem.Label);
        var singleFundo = Assert.Single(fundo);
        Assert.Equal("Git Bash Here", singleFundo.Label);
    }

    // ITEM intacto (T6): complementos seguem passando no menu de arquivo.
    [Fact]
    public void Build_Item_Complementos_ContinuamPassando()
    {
        var raw = new[]
        {
            Popup("Enviar para", Leaf("Documentos", "sendto")),
            Popup("Enviar para"),
            Leaf("Abrir com", "openas"),
            Leaf("Git Bash Here", "git.bash"),
        };

        var result = ThirdPartyTreeBuilder.Build(raw, background: false);

        Assert.Equal(4, result.Count);
    }
}
