using DeskQuadra.Application.Snap;
using Xunit;

namespace DeskQuadra.Application.Tests;

public class SizeSnapperTests
{
    // Grade de referência: célula 78 (ItemWidth do WrapPanel) + chrome 12.
    // Detentes: 90, 168, 246, ...
    private const double Passo = 78.0;
    private const double Chrome = 12.0;

    [Fact]
    public void SnapDimension_MultiploExato_GrudaNaGrade()
    {
        // Arrange: 12 + 2 × 78 = 168 (exato)
        // Act
        var result = SizeSnapper.SnapDimension(168, Passo, Chrome);

        // Assert
        Assert.True(result.Snapped);
        Assert.Equal(168, result.SnappedSize);
        Assert.Equal(2, result.Cells);
    }

    [Fact]
    public void SnapDimension_DentroDoThreshold_GrudaNoDetenteVizinho()
    {
        // Arrange: 172 está a 4px do detente 168 (threshold 8)
        // Act
        var result = SizeSnapper.SnapDimension(172, Passo, Chrome, threshold: 8);

        // Assert
        Assert.True(result.Snapped);
        Assert.Equal(168, result.SnappedSize);
        Assert.Equal(2, result.Cells);
    }

    [Fact]
    public void SnapDimension_ForaDoThreshold_ForcouPassaDireto()
    {
        // Arrange: 185 está a 17px do detente 168 (fora da banda elástica de 8)
        // Act
        var abaixo = SizeSnapper.SnapDimension(185, Passo, Chrome, threshold: 8);
        var acima = SizeSnapper.SnapDimension(110, Passo, Chrome, threshold: 8);

        // Assert (elástico: forçou além da banda, o tamanho passa intacto)
        Assert.False(abaixo.Snapped);
        Assert.Equal(185, abaixo.SnappedSize);
        Assert.False(acima.Snapped);
        Assert.Equal(110, acima.SnappedSize);
    }

    [Theory]
    // Mesmo pixel (168 + 6), decisão depende do sentido: histerese threshold 8 / saída 4.
    [InlineData(174, ResizeDirection.Unknown, true, 168)] // sem sentido: banda base gruda
    [InlineData(174, ResizeDirection.Shrinking, true, 168)] // voltando p/ o detente: entra (gruda)
    [InlineData(174, ResizeDirection.Growing, false, 174)] // saindo do detente: banda estreita, solta
    // Espelho abaixo do detente (168 - 6).
    [InlineData(162, ResizeDirection.Unknown, true, 168)]
    [InlineData(162, ResizeDirection.Growing, true, 168)] // subindo p/ o detente: entra (gruda)
    [InlineData(162, ResizeDirection.Shrinking, false, 162)] // saindo do detente: solta
    public void SnapDimension_Histerese_NaoOscilaNaBorda(
        double atual, ResizeDirection direcao, bool esperadoGrudou, double esperadoTamanho)
    {
        // Act
        var result = SizeSnapper.SnapDimension(atual, Passo, Chrome, threshold: 8, direction: direcao, hysteresis: 4);

        // Assert
        Assert.Equal(esperadoGrudou, result.Snapped);
        Assert.Equal(esperadoTamanho, result.SnappedSize);
    }

    [Fact]
    public void SnapDimension_SaindoDoDetenteExato_SoSoltaComFolgaDaHisterese()
    {
        // Arrange: no pixel exato a distância é 0 — nem a histerese máxima solta.
        // Act
        var exato = SizeSnapper.SnapDimension(168, Passo, Chrome, threshold: 8, direction: ResizeDirection.Growing, hysteresis: 100);
        var vizinho = SizeSnapper.SnapDimension(169, Passo, Chrome, threshold: 8, direction: ResizeDirection.Growing, hysteresis: 100);

        // Assert
        Assert.True(exato.Snapped);
        Assert.False(vizinho.Snapped);
        Assert.Equal(169, vizinho.SnappedSize);
    }

    [Theory]
    // Chrome = altura do título por densidade + resto; passo vertical 96 (ItemHeight).
    [InlineData(28.0, 124.0)] // Normal: 28 + 1 × 96
    [InlineData(42.0, 138.0)] // Touch: 42 + 1 × 96
    public void SnapDimension_ChromeSomado_TituloPorDensidade(double chromeTitulo, double esperado)
    {
        // Act
        var result = SizeSnapper.SnapDimension(esperado, 96, chromeTitulo);

        // Assert
        Assert.True(result.Snapped);
        Assert.Equal(esperado, result.SnappedSize);
        Assert.Equal(1, result.Cells);
    }

    [Fact]
    public void SnapDimension_ChromeDiferente_MudaODetente()
    {
        // Arrange: 138 é detente com chrome 42, mas está a 14px do detente 124 com chrome 28.
        // Act
        var result = SizeSnapper.SnapDimension(138, 96, 28, threshold: 8);

        // Assert
        Assert.False(result.Snapped);
        Assert.Equal(138, result.SnappedSize);
    }

    [Fact]
    public void SnapDimension_AbaixoDoMinimo_PrendeNoMinimoSemGrudar()
    {
        // Act
        var result = SizeSnapper.SnapDimension(100, Passo, Chrome, minSize: 200);

        // Assert
        Assert.False(result.Snapped);
        Assert.Equal(200, result.SnappedSize);
        Assert.Equal(1, result.Cells);
    }

    [Fact]
    public void SnapDimension_MinimoMaiorQueADetente_MinimoVenceAGrade()
    {
        // Arrange: 90 é múltiplo exato (12 + 78), mas está abaixo do MinWidth 200.
        // Act
        var result = SizeSnapper.SnapDimension(90, Passo, Chrome, minSize: 200);

        // Assert
        Assert.False(result.Snapped);
        Assert.Equal(200, result.SnappedSize);
    }

    [Fact]
    public void SnapDimension_SemMinimo_GaranteAoMenosUmaCelula()
    {
        // Arrange: 50 não cabe nem 1 célula (12 + 78 = 90).
        // Act
        var result = SizeSnapper.SnapDimension(50, Passo, Chrome);

        // Assert
        Assert.False(result.Snapped);
        Assert.Equal(90, result.SnappedSize);
        Assert.Equal(1, result.Cells);
    }

    [Fact]
    public void SnapSize_QuantizaLarguraEAlturaComParametrosProprios()
    {
        // Arrange: largura 250 = 16 + 3 × 78 (exato); altura 300 a 16px do detente 316.
        // Act
        var result = SizeSnapper.SnapSize(
            250, 300,
            horizontalStep: 78, verticalStep: 96,
            horizontalChrome: 16, verticalChrome: 28,
            threshold: 8);

        // Assert
        Assert.True(result.SnappedWidth);
        Assert.Equal(250, result.Width);
        Assert.Equal(3, result.Columns);
        Assert.False(result.SnappedHeight);
        Assert.Equal(300, result.Height);
        Assert.Equal(3, result.Rows);
    }

    [Fact]
    public void SnapSize_HisteresePorEixo_Independente()
    {
        // Arrange: largura saindo do detente (solta), altura entrando (gruda).
        // Act
        var result = SizeSnapper.SnapSize(
            174, 172,
            horizontalStep: Passo, verticalStep: Passo,
            horizontalChrome: Chrome, verticalChrome: Chrome,
            threshold: 8,
            horizontalDirection: ResizeDirection.Growing,
            verticalDirection: ResizeDirection.Shrinking,
            hysteresis: 4);

        // Assert
        Assert.False(result.SnappedWidth);
        Assert.Equal(174, result.Width);
        Assert.True(result.SnappedHeight);
        Assert.Equal(168, result.Height);
    }

    [Theory]
    // Ponto médio entre 168 e 246 (meia-célula 39): desempate determinístico p/ cima,
    // mesmo com threshold gigante (clampado na meia-célula, sem ambiguidade).
    [InlineData(207, 246)]
    [InlineData(129, 168)] // ponto médio entre 90 e 168
    public void SnapDimension_ThresholdAcimaDaMeiaCelula_DecisaoDeterministica(double atual, double esperado)
    {
        // Act
        var result = SizeSnapper.SnapDimension(atual, Passo, Chrome, threshold: 100);

        // Assert
        Assert.True(result.Snapped);
        Assert.Equal(esperado, result.SnappedSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-78)]
    public void SnapDimension_PassoInvalido_LancaExcecao(double passo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SizeSnapper.SnapDimension(200, passo, Chrome));
    }

    [Theory]
    [InlineData(-1, 8, 2, 0)] // chrome negativo
    [InlineData(12, -1, 2, 0)] // threshold negativo
    [InlineData(12, 8, -1, 0)] // histerese negativa
    [InlineData(12, 8, 2, -1)] // mínimo negativo
    public void SnapDimension_ParametroNegativo_LancaExcecao(
        double chrome, double threshold, double hysteresis, double minSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SizeSnapper.SnapDimension(200, Passo, chrome, threshold, hysteresis: hysteresis, minSize: minSize));
    }

    [Fact]
    public void SnapDimension_TamanhoNaoFinito_RetornaIntactoSemGrudar()
    {
        // Act
        var nan = SizeSnapper.SnapDimension(double.NaN, Passo, Chrome);
        var infinito = SizeSnapper.SnapDimension(double.PositiveInfinity, Passo, Chrome);

        // Assert (caminho quente do arrasto nunca quebra)
        Assert.False(nan.Snapped);
        Assert.False(infinito.Snapped);
        Assert.Equal(0, nan.Cells);
        Assert.Equal(0, infinito.Cells);
    }
}
