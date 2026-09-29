namespace DeskQuadra.Application.Snap;

/// <summary>
/// Quantização pura do tamanho da Quadra na grade de ícones (matemática de snap de resize).
///
/// REGRAS (mesmo espírito elástico do <see cref="SnapEngine"/> de posição):
/// <list type="bullet">
/// <item>A grade válida é <c>chrome + N × passoCelula</c> (N ≥ 1). O <c>chrome</c> é tudo
/// que não é grade — moldura, margens, paddings e a barra de título — e vem de fora
/// como parâmetro, nunca chapado (a altura do título varia com a densidade:
/// <c>DensityResolver.TitleBarHeight</c> = 28 Normal / 42 Touch).</item>
/// <item>Dentro da banda (<c>threshold</c>) o tamanho GRUDA no múltiplo mais próximo.</item>
/// <item>Forçou além da banda, PASSA DIRETO (elástico): devolve o tamanho atual intacto.</item>
/// <item>HISTERESE (não treme na borda): entrando no detente a banda é
/// <c>threshold</c>; saindo do detente (o <c>direction</c> aponta para longe do
/// múltiplo) a banda estreita para <c>threshold - hysteresis</c>. Ou seja, para
/// soltar precisa forçar um pouco mais do que precisou para grudar.</item>
/// <item>MÍNIMO: o resultado nunca fica abaixo de <c>max(minSize, chrome + passoCelula)</c>
/// (cabe ao menos 1 célula). O mínimo tem precedência sobre a grade.</item>
/// <item>SEGURANÇA: <c>threshold</c> acima da meia-célula é clampado para
/// <c>passoCelula / 2</c> (bandas vizinhas nunca se sobrepõem); empate exato no
/// meio de dois detentes arredonda para cima (determinístico, sem oscilação).</item>
/// </list>
///
/// COMO FIAR NA UI (agente da fiação — chamar a cada DragDelta, sem estado próprio):
/// <list type="bullet">
/// <item><c>cellStep</c>: ItemWidth (78) / ItemHeight (96) do WrapPanel.</item>
/// <item><c>chrome</c> horizontal: moldura + margens + paddings laterais (medir na árvore visual);
/// vertical: altura do título (28/42) + moldura + margens + paddings verticais.</item>
/// <item><c>direction</c>: a DIMENSÃO está crescendo ou encolhendo? Borda direita:
/// HorizontalChange &gt; 0 → <c>Growing</c>. Borda esquerda: HorizontalChange &lt; 0 →
/// <c>Growing</c> (a largura aumenta). Idem no vertical com VerticalChange nas
/// bordas inferior/superior. Quando não souber, <c>Unknown</c> (banda base).</item>
/// <item><c>minSize</c>: MinWidth/MinHeight da janela (200/140 no XAML hoje).</item>
/// <item>Sugestão: <c>threshold</c> 8–12 (sempre &lt; passo/2), <c>hysteresis</c> ~2.</item>
/// </list>
///
/// Sem I/O, sem UI, sem timers. Estático e sem estado: pode chamar direto, sem DI.
/// </summary>
public static class SizeSnapper
{
    /// <summary>
    /// Quantiza UMA dimensão (largura ou altura) na grade de ícones.
    /// </summary>
    /// <param name="currentSize">Tamanho atual da dimensão (janela cheia, com chrome).</param>
    /// <param name="cellStep">Passo da célula na grade (largura ou altura de um slot de ícone). Deve ser &gt; 0.</param>
    /// <param name="chrome">Tudo que não é grade (título + moldura + margens + paddings). Deve ser ≥ 0.</param>
    /// <param name="threshold">Banda elástica de atração (padrão 8). Deve ser ≥ 0; acima da meia-célula é clampado.</param>
    /// <param name="direction">Sentido do resize (para a histerese). <c>Unknown</c> = banda base.</param>
    /// <param name="hysteresis">Quanto a banda estreita ao SAIR do detente (padrão 2). Deve ser ≥ 0.</param>
    /// <param name="minSize">Tamanho mínimo da dimensão (ex: MinWidth/MinHeight). Deve ser ≥ 0.</param>
    /// <returns>Tamanho ajustado + flag de snap + contagem de células.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Quando <c>cellStep</c> ≤ 0 ou algum de <c>chrome</c>, <c>threshold</c>,
    /// <c>hysteresis</c>, <c>minSize</c> for negativo.
    /// </exception>
    public static SizeSnapResult SnapDimension(
        double currentSize,
        double cellStep,
        double chrome,
        double threshold = 8.0,
        ResizeDirection direction = ResizeDirection.Unknown,
        double hysteresis = 2.0,
        double minSize = 0.0)
    {
        // Defensivo no caminho quente do arrasto: entrada não-finita nunca gruda,
        // volta intacta em vez de quebrar o resize da UI.
        if (double.IsNaN(currentSize) || double.IsInfinity(currentSize))
        {
            return new SizeSnapResult(currentSize, false, 0);
        }

        // Validação dos parâmetros via helpers locais (mensagens preservadas verbatim).
        ThrowIfNotPositive(nameof(cellStep), cellStep, "O passo da célula deve ser maior que zero.");
        ThrowIfNegative(nameof(chrome), chrome, "O chrome não pode ser negativo.");
        ThrowIfNegative(nameof(threshold), threshold, "O threshold não pode ser negativo.");
        ThrowIfNegative(nameof(hysteresis), hysteresis, "A histerese não pode ser negativa.");
        ThrowIfNegative(nameof(minSize), minSize, "O tamanho mínimo não pode ser negativo.");

        // Helpers locais: eliminam os 5 blocos if repetidos sem mudar a semântica.
        static void ThrowIfNegative(string name, double value, string message)
        {
            if (value < 0.0)
            {
                throw new ArgumentOutOfRangeException(name, message);
            }
        }

        static void ThrowIfNotPositive(string name, double value, string message)
        {
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, message);
            }
        }

        // Piso: mínimo da janela, garantindo ao menos 1 célula visível.
        double floor = Math.Max(minSize, chrome + cellStep);

        // Banda efetiva: nunca passa da meia-célula (detentes vizinhos não se sobrepõem).
        double limit = Math.Min(threshold, cellStep / 2.0);

        // Detente vizinho mais próximo: chrome + N × passo (N ≥ 1).
        double rawCells = (currentSize - chrome) / cellStep;
        double cellsExact = Math.Max(1.0, Math.Round(rawCells, MidpointRounding.AwayFromZero));
        double target = chrome + cellsExact * cellStep;
        double distance = Math.Abs(currentSize - target);

        // Histerese: saindo do detente, a banda estreita (precisa forçar mais para soltar).
        double effectiveLimit = limit;
        if (direction != ResizeDirection.Unknown && distance > 0.0)
        {
            bool movingAway = (currentSize > target && direction == ResizeDirection.Growing)
                || (currentSize < target && direction == ResizeDirection.Shrinking);
            if (movingAway)
            {
                effectiveLimit = Math.Max(0.0, limit - hysteresis);
            }
        }

        bool snapped = distance <= effectiveLimit;
        double adjusted = snapped ? target : currentSize;

        // Mínimo tem precedência sobre a grade (não conta como snap).
        if (adjusted < floor)
        {
            return new SizeSnapResult(floor, false, 1);
        }

        int cells = Math.Max(1, (int)Math.Round((adjusted - chrome) / cellStep, MidpointRounding.AwayFromZero));
        return new SizeSnapResult(adjusted, snapped, cells);
    }

    /// <summary>
    /// Quantiza largura + altura de uma vez (resize de canto). Delega cada eixo a
    /// <see cref="SnapDimension"/> com seu passo, chrome, direção e mínimo próprios.
    /// </summary>
    /// <param name="currentWidth">Largura atual (janela cheia).</param>
    /// <param name="currentHeight">Altura atual (janela cheia).</param>
    /// <param name="horizontalStep">Passo da célula na horizontal (ex: ItemWidth 78).</param>
    /// <param name="verticalStep">Passo da célula na vertical (ex: ItemHeight 96).</param>
    /// <param name="horizontalChrome">Chrome lateral (moldura + margens + paddings).</param>
    /// <param name="verticalChrome">Chrome vertical (título 28/42 + moldura + margens + paddings).</param>
    /// <param name="threshold">Banda elástica de atração (padrão 8).</param>
    /// <param name="horizontalDirection">Sentido da largura (<c>Growing</c>/<c>Shrinking</c>/<c>Unknown</c>).</param>
    /// <param name="verticalDirection">Sentido da altura (<c>Growing</c>/<c>Shrinking</c>/<c>Unknown</c>).</param>
    /// <param name="hysteresis">Estreitamento da banda ao sair do detente (padrão 2).</param>
    /// <param name="minWidth">Largura mínima (ex: MinWidth 200).</param>
    /// <param name="minHeight">Altura mínima (ex: MinHeight 140).</param>
    public static SizeSnapResult2D SnapSize(
        double currentWidth,
        double currentHeight,
        double horizontalStep,
        double verticalStep,
        double horizontalChrome,
        double verticalChrome,
        double threshold = 8.0,
        ResizeDirection horizontalDirection = ResizeDirection.Unknown,
        ResizeDirection verticalDirection = ResizeDirection.Unknown,
        double hysteresis = 2.0,
        double minWidth = 0.0,
        double minHeight = 0.0)
    {
        var width = SnapDimension(
            currentWidth, horizontalStep, horizontalChrome,
            threshold, horizontalDirection, hysteresis, minWidth);

        var height = SnapDimension(
            currentHeight, verticalStep, verticalChrome,
            threshold, verticalDirection, hysteresis, minHeight);

        return new SizeSnapResult2D(
            width.SnappedSize,
            height.SnappedSize,
            width.Snapped,
            height.Snapped,
            width.Cells,
            height.Cells);
    }
}
