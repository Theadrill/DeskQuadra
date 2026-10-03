using DeskQuadra.Core.FileSystem;

namespace DeskQuadra.Core.Tests;

public class DesktopItemMoverTests
{
    [Fact]
    public void CanMoveInto_CaminhoNuloOuVazio_RetornaFalse()
    {
        Assert.False(DesktopItemMover.CanMoveInto(null, @"C:\Target"));
        Assert.False(DesktopItemMover.CanMoveInto(@"C:\Source.txt", null));
        Assert.False(DesktopItemMover.CanMoveInto("", ""));
        Assert.False(DesktopItemMover.CanMoveInto("   ", "   "));
    }

    [Fact]
    public void CanMoveInto_DiretorioDestinoNaoExiste_RetornaFalse()
    {
        bool result = DesktopItemMover.CanMoveInto(
            @"C:\Source.txt",
            @"C:\NonExistentTarget",
            fileExists: _ => true,
            directoryExists: _ => false);

        Assert.False(result);
    }

    [Fact]
    public void CanMoveInto_OrigemNaoExiste_RetornaFalse()
    {
        bool result = DesktopItemMover.CanMoveInto(
            @"C:\NonExistentSource.txt",
            @"C:\Target",
            fileExists: _ => false,
            directoryExists: path => path == @"C:\Target");

        Assert.False(result);
    }

    [Fact]
    public void CanMoveInto_PastaParaDentroDeSiMesma_RetornaFalse()
    {
        bool result = DesktopItemMover.CanMoveInto(
            @"C:\MinhaPasta",
            @"C:\MinhaPasta",
            fileExists: _ => false,
            directoryExists: _ => true);

        Assert.False(result);
    }

    [Fact]
    public void CanMoveInto_PastaComBarraFinalParaSiMesma_RetornaFalse()
    {
        bool result = DesktopItemMover.CanMoveInto(
            @"C:\MinhaPasta\",
            @"C:\MinhaPasta",
            fileExists: _ => false,
            directoryExists: _ => true);

        Assert.False(result);
    }

    [Fact]
    public void CanMoveInto_PastaParaDentroDeSubpasta_RetornaFalse()
    {
        bool result = DesktopItemMover.CanMoveInto(
            @"C:\MinhaPasta",
            @"C:\MinhaPasta\SubPasta",
            fileExists: _ => false,
            directoryExists: _ => true);

        Assert.False(result);
    }

    [Fact]
    public void CanMoveInto_ArquivoJaDentroDoDestino_RetornaFalse()
    {
        bool result = DesktopItemMover.CanMoveInto(
            @"C:\Target\Arquivo.txt",
            @"C:\Target",
            fileExists: path => path == @"C:\Target\Arquivo.txt",
            directoryExists: path => path == @"C:\Target");

        Assert.False(result);
    }

    [Fact]
    public void CanMoveInto_ArquivoValidoParaDestino_RetornaTrue()
    {
        bool result = DesktopItemMover.CanMoveInto(
            @"C:\Desktop\Relatorio.pdf",
            @"C:\Desktop\Documentos",
            fileExists: path => path == @"C:\Desktop\Relatorio.pdf",
            directoryExists: path => path == @"C:\Desktop\Documentos");

        Assert.True(result);
    }

    [Fact]
    public void CanMoveInto_PastaValidaParaOutraPasta_RetornaTrue()
    {
        bool result = DesktopItemMover.CanMoveInto(
            @"C:\Desktop\Projetos",
            @"C:\Desktop\Arquivo",
            fileExists: _ => false,
            directoryExists: path => path == @"C:\Desktop\Projetos" || path == @"C:\Desktop\Arquivo");

        Assert.True(result);
    }

    [Fact]
    public void GetDestinationPath_SemColisao_RetornaNomeOriginal()
    {
        string targetDir = @"C:\Desktop\Documentos";
        string result = DesktopItemMover.GetDestinationPath(
            @"C:\Desktop\Relatorio.pdf",
            targetDir,
            pathExists: _ => false,
            isSourceDirectory: false);

        Assert.Equal(Path.Combine(targetDir, "Relatorio.pdf"), result);
    }

    [Fact]
    public void GetDestinationPath_ComColisao_GeraSufixoIndexado()
    {
        string targetDir = @"C:\Desktop\Documentos";
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(targetDir, "Relatorio.pdf")
        };

        string result = DesktopItemMover.GetDestinationPath(
            @"C:\Desktop\Relatorio.pdf",
            targetDir,
            pathExists: p => existing.Contains(p),
            isSourceDirectory: false);

        Assert.Equal(Path.Combine(targetDir, "Relatorio (2).pdf"), result);
    }

    [Fact]
    public void GetDestinationPath_ComMultiplasColisoes_IncrementaAteLivre()
    {
        string targetDir = @"C:\Desktop\Documentos";
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(targetDir, "Relatorio.pdf"),
            Path.Combine(targetDir, "Relatorio (2).pdf")
        };

        string result = DesktopItemMover.GetDestinationPath(
            @"C:\Desktop\Relatorio.pdf",
            targetDir,
            pathExists: p => existing.Contains(p),
            isSourceDirectory: false);

        Assert.Equal(Path.Combine(targetDir, "Relatorio (3).pdf"), result);
    }

    [Fact]
    public void GetDestinationPath_DiretorioComColisao_GeraSufixoIndexado()
    {
        string targetDir = @"C:\Desktop\Documentos";
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(targetDir, "SubPasta")
        };

        string result = DesktopItemMover.GetDestinationPath(
            @"C:\Desktop\SubPasta",
            targetDir,
            pathExists: p => existing.Contains(p),
            isSourceDirectory: true);

        Assert.Equal(Path.Combine(targetDir, "SubPasta (2)"), result);
    }

    [Fact]
    public void Move_Arquivo_ChamaMoveFileComCaminhoResolvido()
    {
        string source = @"C:\Desktop\Teste.txt";
        string targetDir = @"C:\Desktop\Pasta";

        string? calledSource = null;
        string? calledDest = null;

        string dest = DesktopItemMover.Move(
            source,
            targetDir,
            moveFile: (s, d) =>
            {
                calledSource = s;
                calledDest = d;
            });

        Assert.Equal(source, calledSource);
        Assert.Equal(Path.Combine(targetDir, "Teste.txt"), calledDest);
        Assert.Equal(calledDest, dest);
    }
}
