using DeskQuadra.Core.ThirdParty;

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
    [InlineData("print")]
    [InlineData("printto")]
    [InlineData("explore")]
    [InlineData("properties")]
    [InlineData("edit")]
    [InlineData("runas")]
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
    // SONDA T6: nativos que seguem fora (vizinhos dos complementos).
    // openas/sendto/setdesktopwallpaper/rotate90/270 SAÍRAM daqui: agora são
    // COMPLEMENTOS (IsNativeComplement vence antes) — ver teoria de passagem.
    [InlineData("opennewprocess")]
    [InlineData("OpenNewProcess")]
    [InlineData("powershell")]
    [InlineData("Powershell")]
    public void IsThirdParty_VerboCanônico_BloqueadoMesmoComRótuloDeTerceiro(string verb)
    {
        Assert.False(ThirdPartyVerbFilter.IsThirdParty(verb, "7-Zip", isSeparator: false));
    }

    [Theory]
    [InlineData("Abrir")]
    [InlineData("Propriedades")]
    [InlineData("Recortar")]
    [InlineData("Copiar")]
    [InlineData("Colar")]
    [InlineData("Criar atalho")]
    [InlineData("Excluir")]
    [InlineData("Renomear")]
    [InlineData("Compartilhar")]
    [InlineData("Open")]
    [InlineData("Properties")]
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
    // COMPLEMENTOS saíram daqui: "Abrir com"/"Enviar para" (verbo implícito do
    // popup) e "Transmitir para Dispositivo" (allowlist por label) agora PASSAM
    // — ver teoria NativeComplements. Vizinhos nativos acima continuam fora.
    public void IsThirdParty_RótuloNativo_BloqueadoMesmoComVerboVazio(string label)
    {
        Assert.False(ThirdPartyVerbFilter.IsThirdParty(verb: string.Empty, label, isSeparator: false));
    }

    // COMPLEMENTOS NATIVOS (PO pediu de volta): cada um passa (verbo vence a
    // denylist; Transmitir-popup passa pelo label, único sinal com verbo
    // vazio). Rótulos são os do próprio Shell (sem resx novo).
    [Theory]
    [InlineData("openas", "Abrir com")]
    [InlineData("openas", "Open with")]
    [InlineData("OPENAS", "Abrir com")]
    [InlineData("sendto", "Enviar para")]
    [InlineData("sendto", "Send to")]
    [InlineData("setdesktopwallpaper", "Definir como fundo da área de trabalho")]
    [InlineData("SetDesktopWallpaper", "Definir como fundo da área de trabalho")]
    [InlineData("rotate90", "Girar para a direita")]
    [InlineData("rotate270", "Girar para a esquerda")]
    [InlineData("Rotate270", "Girar para a esquerda")]
    [InlineData("casttodevice", "Transmitir para Dispositivo")]
    [InlineData("", "Transmitir para Dispositivo")]
    [InlineData("", "Cast to device")]
    [InlineData(null, "Transmitir para Dispositivo")]
    // Popup sem verbo: verbo implícito mapeado do rótulo (query fixa Verb=null
    // p/ MF_POPUP) — continua "por VERBO" na seleção do invoke.
    [InlineData("", "Enviar para")]
    [InlineData("", "Send to")]
    [InlineData("", "Abrir com")]
    [InlineData("", "Open with")]
    [InlineData(null, "Enviar para")]
    public void IsThirdParty_ComplementoNativo_PassaAntesDaDenylist(string? verb, string label)
    {
        Assert.True(ThirdPartyVerbFilter.IsThirdParty(verb, label, isSeparator: false));
        Assert.True(ThirdPartyVerbFilter.IsNativeComplement(verb, label));
    }

    [Theory]
    [InlineData("", "7-Zip")]
    [InlineData("", "Add to archive...")]
    [InlineData("", "Git Bash Here")]
    [InlineData("7z.open", "Open archive")]
    [InlineData("vscode.open", "Open with Code")]
    [InlineData("", "Open with Code")]
    // SONDA T6: terceiro legítimo instalado na máquina do PO — NÃO pode cair.
    [InlineData("tailscale", "Send with Tailscale...")]
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
    public void IsNativeComplement_VizinhoNativo_NãoÉComplemento()
    {
        Assert.False(ThirdPartyVerbFilter.IsNativeComplement("open", "Abrir"));
        Assert.False(ThirdPartyVerbFilter.IsNativeComplement("opennewprocess", "Abrir em novo processo"));
        Assert.False(ThirdPartyVerbFilter.IsNativeComplement("powershell", "Abrir janela do PowerShell aqui"));
        Assert.False(ThirdPartyVerbFilter.IsNativeComplement("properties", "Propriedades"));
        Assert.False(ThirdPartyVerbFilter.IsNativeComplement(string.Empty, "7-Zip"));
        // Separador nunca é complemento (IsThirdParty barra antes de chegar aqui).
        Assert.False(ThirdPartyVerbFilter.IsThirdParty("sendto", "Enviar para", isSeparator: true));
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
