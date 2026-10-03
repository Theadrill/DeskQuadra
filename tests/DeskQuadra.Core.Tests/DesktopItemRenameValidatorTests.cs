using DeskQuadra.Core;

namespace DeskQuadra.Core.Tests;

public class DesktopItemRenameValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void IsValid_VazioOuSoEspacos_Rejeita(string? candidate)
    {
        Assert.False(DesktopItemRenameValidator.IsValid(candidate));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(" . ")]
    [InlineData(" .. ")]
    public void IsValid_Pontos_Rejeita(string candidate)
    {
        Assert.False(DesktopItemRenameValidator.IsValid(candidate));
    }

    [Theory]
    [InlineData("nome<invalido")]
    [InlineData("nome>invalido")]
    [InlineData("nome:invalido")]
    [InlineData("nome\"invalido")]
    [InlineData("nome/invalido")]
    [InlineData("nome\\invalido")]
    [InlineData("nome|invalido")]
    [InlineData("nome?invalido")]
    [InlineData("nome*invalido")]
    public void IsValid_CaracteresProibidos_Rejeita(string candidate)
    {
        Assert.False(DesktopItemRenameValidator.IsValid(candidate));
    }

    [Theory]
    [InlineData("Nova Pasta")]
    [InlineData("Documento (1)")]
    [InlineData("arquivo.txt")]
    [InlineData("  Meu Atalho  ")]
    [InlineData("Pasta.Com.Pontos")]
    public void IsValid_Valido_Aceita(string candidate)
    {
        Assert.True(DesktopItemRenameValidator.IsValid(candidate));
    }

    [Fact]
    public void TryNormalize_Valido_RetornaTrimado()
    {
        bool ok = DesktopItemRenameValidator.TryNormalize("  Minha Pasta  ", out string normalized);
        Assert.True(ok);
        Assert.Equal("Minha Pasta", normalized);
    }

    [Fact]
    public void TryNormalize_Invalido_RetornaFalse()
    {
        bool ok = DesktopItemRenameValidator.TryNormalize("  *invalido*  ", out string normalized);
        Assert.False(ok);
        Assert.Empty(normalized);
    }

    [Fact]
    public void GetTargetFileName_AtalhoSemExtensao_AdicionaLnk()
    {
        string target = DesktopItemRenameValidator.GetTargetFileName(@"C:\Desktop\Chrome.lnk", "Google Chrome");
        Assert.Equal("Google Chrome.lnk", target);
    }

    [Fact]
    public void GetTargetFileName_AtalhoComExtensao_NaoDuplicaLnk()
    {
        string target = DesktopItemRenameValidator.GetTargetFileName(@"C:\Desktop\Chrome.lnk", "Google Chrome.lnk");
        Assert.Equal("Google Chrome.lnk", target);
    }

    [Fact]
    public void GetTargetFileName_ArquivoComum_MantemNomeDigitado()
    {
        string target = DesktopItemRenameValidator.GetTargetFileName(@"C:\Desktop\documento.docx", "relatorio.docx");
        Assert.Equal("relatorio.docx", target);
    }

    [Fact]
    public void GetTargetFileName_Diretorio_MantemNomeDigitado()
    {
        string target = DesktopItemRenameValidator.GetTargetFileName(@"C:\Desktop\Fotos", "Viagem 2026");
        Assert.Equal("Viagem 2026", target);
    }
}
