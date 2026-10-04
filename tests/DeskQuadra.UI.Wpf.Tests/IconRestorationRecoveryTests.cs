using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Tests;

public class IconRestorationRecoveryTests
{
    private sealed class TrackingIconExtractor : IIconExtractorService
    {
        public int ExtractionCount { get; private set; }
        public int ClearCacheCount { get; private set; }
        public List<string> InvalidatedPaths { get; } = new();

        public ImageSource GetIcon(string filePath, bool large = true)
        {
            ExtractionCount++;
            return BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgr32, null, new byte[16 * 16 * 4], 16 * 4);
        }

        public void ClearCache() => ClearCacheCount++;

        public void Invalidate(string filePath) => InvalidatedPaths.Add(filePath);
    }

    [Fact]
    public void DesktopItemViewModel_InvalidateIcon_LimpaIconENotificaPropertyChanged()
    {
        var item = new DesktopItem("App", @"C:\App.exe");
        var extractor = new TrackingIconExtractor();
        var vm = new DesktopItemViewModel(item, extractor);

        // Acesso inicial carrega o ícone
        var icon1 = vm.Icon;
        Assert.NotNull(icon1);
        Assert.Equal(1, extractor.ExtractionCount);

        var changed = new List<string>();
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changed.Add(e.PropertyName);
            }
        };

        // Invalida o ícone
        vm.InvalidateIcon();
        Assert.Contains(nameof(DesktopItemViewModel.Icon), changed);

        // Próximo acesso recarrega o ícone
        var icon2 = vm.Icon;
        Assert.NotNull(icon2);
        Assert.Equal(2, extractor.ExtractionCount);
    }

    [Fact]
    public void DesktopItemViewModel_ArquivoRestauradoAposFallback_RecarregaIconeAutomaticamente()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"DeskQuadra_Test_{Guid.NewGuid():N}.txt");
        try
        {
            var item = new DesktopItem("Temp", tempFile);
            var extractor = new TrackingIconExtractor();
            var vm = new DesktopItemViewModel(item, extractor);

            // 1. Arquivo não existe no disco (simula item na Lixeira)
            Assert.False(File.Exists(tempFile));
            var fallbackIcon = vm.Icon;
            Assert.NotNull(fallbackIcon);
            Assert.Equal(1, extractor.ExtractionCount);

            // Acesso subsequente enquanto ainda não existe reutiliza a instância carregada
            var iconStillMissing = vm.Icon;
            Assert.Equal(1, extractor.ExtractionCount);

            // 2. Arquivo é restaurado no disco
            File.WriteAllText(tempFile, "Conteudo restaurado da Lixeira");
            Assert.True(File.Exists(tempFile));

            // 3. Próximo acesso ao Icon detecta que o arquivo voltou e re-extrai automaticamente
            var restoredIcon = vm.Icon;
            Assert.NotNull(restoredIcon);
            Assert.Equal(2, extractor.ExtractionCount);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void QuadraViewModel_InvalidateAllIcons_LimpaCacheDoExtratorEInvalidaTodosOsItens()
    {
        var quadra = new Quadra("Minha Quadra", 0, 0, isDefault: false);
        quadra.Items.Add(new DesktopItem("Item 1", @"C:\1.txt"));
        quadra.Items.Add(new DesktopItem("Item 2", @"C:\2.txt"));

        var extractor = new TrackingIconExtractor();
        var qvm = new QuadraViewModel(quadra, extractor);

        // Carrega ícones de ambos os itens
        _ = qvm.Items[0].Icon;
        _ = qvm.Items[1].Icon;
        Assert.Equal(2, extractor.ExtractionCount);

        // Invalida tudo
        qvm.InvalidateAllIcons();

        Assert.Equal(1, extractor.ClearCacheCount);

        // Acessa novamente os ícones; ambos devem re-extrair
        _ = qvm.Items[0].Icon;
        _ = qvm.Items[1].Icon;
        Assert.Equal(4, extractor.ExtractionCount);
    }

    [Fact]
    public void IconExtractorService_ArquivoInexistente_NaoArmazenaNoCachePermanente()
    {
        var extractor = new IconExtractorService();
        string nonExistentFile = @"C:\FakeDirectory_" + Guid.NewGuid() + @"\NonExistentApp.lnk";

        // Extrai ícone de arquivo inexistente (retorna fallback de folha branca)
        var fallbackIcon = extractor.GetIcon(nonExistentFile, large: true);
        Assert.NotNull(fallbackIcon);

        // Invalidação específica funciona sem erros
        extractor.Invalidate(nonExistentFile);
        extractor.ClearCache();
    }
}
