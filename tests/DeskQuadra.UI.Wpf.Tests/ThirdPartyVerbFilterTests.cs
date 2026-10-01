using DeskQuadra.Infrastructure.WindowsShell.Shell;

namespace DeskQuadra.UI.Wpf.Tests;

// T2 terceiros: lógica pura do filtro "só terceiros" (§1 do PLANO_MENU_TERCEIROS.md).
// Sem Shell/COM — só a denylist aplicada a verbo E label + MFT_SEPARATOR fora.
public class ThirdPartyVerbFilterTests
{
    [Theory]
    [InlineData("open")]
    [InlineData("Open")]
    [InlineData("OPEN")]
    [InlineData("opennew")]
    [InlineData("openas")]
    [InlineData("print")]
    [InlineData("printto")]
    [InlineData("explore")]
    [InlineData("properties")]
    [InlineData("edit")]
    [InlineData("runas")]
    [InlineData("sendto")]
    [InlineData("cut")]
    [InlineData("copy")]
    [InlineData("paste")]
    [InlineData("link")]
    [InlineData("delete")]
    [InlineData("rename")]
    [InlineData("new")]
    [InlineData("refresh")]
    [InlineData("share")]
    [InlineData("windows.share")]
    [InlineData("Windows.ShareFlyout")]
    [InlineData("pintohomefile")]
    [InlineData("PinToStartScreen")]
    [InlineData("makeavailableoffline")]
    [InlineData("makeavailableonline")]
    [InlineData("previousversions")]
    [InlineData("copyaspath")]
    [InlineData("includelibrary")]
    [InlineData("{5250E46F-BB09-D602-5891-F476DC89B702}")]
    public void IsThirdParty_VerboCanônico_BloqueadoMesmoComRótuloDeTerceiro(string verb)
    {
        Assert.False(ThirdPartyVerbFilter.IsThirdParty(verb, "7-Zip", isSeparator: false));
    }

    [Theory]
    [InlineData("Abrir")]
    [InlineData("Propriedades")]
    [InlineData("Enviar para")]
    [InlineData("Abrir com")]
    [InlineData("Recortar")]
    [InlineData("Copiar")]
    [InlineData("Colar")]
    [InlineData("Criar atalho")]
    [InlineData("Excluir")]
    [InlineData("Renomear")]
    [InlineData("Compartilhar")]
    [InlineData("Open")]
    [InlineData("Properties")]
    [InlineData("Send to")]
    [InlineData("Open with")]
    [InlineData("Compartilhar com Contatos")]
    [InlineData("Share with Contacts")]
    [InlineData("Sempre manter neste dispositivo")]
    [InlineData("Liberar espaço")]
    [InlineData("Copiar Link")]
    [InlineData("Gerenciar acesso")]
    [InlineData("Mover para o OneDrive")]
    [InlineData("Copiar como caminho")]
    [InlineData("Restaurar versões anteriores")]
    [InlineData("Adicionar aos Favoritos")]
    [InlineData("Fixar em Iniciar")]
    [InlineData("Incluir na biblioteca")]
    public void IsThirdParty_RótuloNativo_BloqueadoMesmoComVerboVazio(string label)
    {
        Assert.False(ThirdPartyVerbFilter.IsThirdParty(verb: string.Empty, label, isSeparator: false));
    }

    [Theory]
    [InlineData("", "7-Zip")]
    [InlineData("", "Add to archive...")]
    [InlineData("", "Git Bash Here")]
    [InlineData("7z.open", "Open archive")]
    [InlineData("vscode.open", "Open with Code")]
    [InlineData("", "Open with Code")]
    public void IsThirdParty_Terceiro_PassaIncluindoVerboVazio(string verb, string label)
    {
        Assert.True(ThirdPartyVerbFilter.IsThirdParty(verb, label, isSeparator: false));
    }

    [Fact]
    public void IsThirdParty_Separador_SempreFora()
    {
        Assert.False(ThirdPartyVerbFilter.IsThirdParty(verb: string.Empty, label: string.Empty, isSeparator: true));
    }

    [Fact]
    public void NormalizeLabel_RemoveAceleradorEspaçoESufixoTab()
    {
        Assert.Equal("exit", ThirdPartyVerbFilter.NormalizeLabel("E&xit"));
        Assert.Equal("abrir", ThirdPartyVerbFilter.NormalizeLabel("  Abrir  "));
        Assert.Equal("open", ThirdPartyVerbFilter.NormalizeLabel("&Open\tCtrl+O"));
    }

    [Fact]
    public void CleanLabelForDisplay_PreservaCaixa_RemoveAceleradorETab()
    {
        Assert.Equal("Add to archive...", ThirdPartyVerbFilter.CleanLabelForDisplay("&Add to archive...\tCtrl+A"));
        Assert.Equal("7-Zip", ThirdPartyVerbFilter.CleanLabelForDisplay("7-Zip"));
        Assert.Equal(string.Empty, ThirdPartyVerbFilter.CleanLabelForDisplay(null));
    }
}
