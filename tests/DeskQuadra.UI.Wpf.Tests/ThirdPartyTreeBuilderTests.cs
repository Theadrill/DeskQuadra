using DeskQuadra.Infrastructure.WindowsShell.Shell;

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

    [Fact]
    public void Build_CascataSóDeNativos_CaiInteira()
    {
        var raw = new[] { Popup("Enviar para", Leaf("Documentos", "sendto")) };

        Assert.Empty(ThirdPartyTreeBuilder.Build(raw));
    }

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

        Assert.Empty(ThirdPartyTreeBuilder.Build(raw));
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

    [Fact]
    public void Build_Nulo_DevolveVazio()
    {
        Assert.Empty(ThirdPartyTreeBuilder.Build(null));
    }
}
