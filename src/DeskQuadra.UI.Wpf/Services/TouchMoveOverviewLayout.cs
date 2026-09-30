// Lógica pura do overview do MOVER-via-touch (fatia 2): foto, grade e restaura sem Visual/Dispatcher.
// Testável em xUnit sem STA: só Guids + doubles + bools (janelas/banner vivem na QuadraWindow).
namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Foto de uma Quadra aberta p/ o overview (bounds em DIP + estado).
/// Escondidas ficam de fora: o chamador só inclui janelas abertas.
/// </summary>
public sealed record QuadraOverviewFrame(
    Guid Id,
    double Left,
    double Top,
    double Width,
    double Height,
    double ExpandedHeight, // altura expandida (recolhida vira mini expandida; normal = Height)
    bool IsCollapsed,
    bool IsLocked,
    double MinWidth,
    double MinHeight);

/// <summary>Mini destino na grade (DIP, na tela do gesto).</summary>
public sealed record OverviewSlot(Guid Id, double Left, double Top, double Width, double Height);

/// <summary>
/// Grade estilo launcher do overview: todas as minis cabem na tela do gesto de uma vez.
/// Travada participa como destino normal (lock trava a janela, não o conteúdo).
/// Outros monitores: 1 tela agora — todas as Quadras abertas vêm p/ a grade da tela do
/// gesto (abas multi-monitor ficam p/ depois).
/// </summary>
public static class TouchMoveOverviewLayout
{
    // Respiro da grade na tela do gesto (DIP).
    public const double MarginDip = 24.0;

    public const double GapDip = 16.0;

    // Reserva INFERIOR da faixa de instrução (DIP): o chamador desconta da altura útil (grade no topo, faixa embaixo).
    public const double BannerReserveDip = 64.0;

    // Fotografa as Quadras abertas (cópia fiel p/ restaurar depois sem persistir sujeira).
    public static IReadOnlyList<QuadraOverviewFrame> Snapshot(IEnumerable<QuadraOverviewFrame> open)
    {
        return open.ToList();
    }

    // Grade uniforme: slots TODOS do mesmo tamanho — preenchem a célula cheia
    // (respiro igual via margem/gap); proporção individual ignorada.
    public static IReadOnlyList<OverviewSlot> ComputeGrid(
        double screenLeft, double screenTop, double screenWidth, double screenHeight,
        IReadOnlyList<QuadraOverviewFrame> frames)
    {
        var result = new List<OverviewSlot>(frames.Count);
        if (frames.Count == 0 || screenWidth <= 0 || screenHeight <= 0)
        {
            return result;
        }
        int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(frames.Count * screenWidth / screenHeight)));
        cols = Math.Min(cols, frames.Count); // nunca mais colunas que minis (1 Quadra centraliza)
        int rows = Math.Max(1, (int)Math.Ceiling((double)frames.Count / cols));
        double gridW = Math.Max(0, screenWidth - 2 * MarginDip);
        double gridH = Math.Max(0, screenHeight - 2 * MarginDip);
        double cellW = Math.Max(1, (gridW - (cols - 1) * GapDip) / cols);
        double cellH = Math.Max(1, (gridH - (rows - 1) * GapDip) / rows);
        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            int r = i / cols;
            int c = i % cols;
            double cellX = screenLeft + MarginDip + c * (cellW + GapDip);
            double cellY = screenTop + MarginDip + r * (cellH + GapDip);
            // Mini uniforme: ocupa a célula cheia (sem preservar proporção).
            result.Add(new OverviewSlot(f.Id, cellX, cellY, cellW, cellH));
        }
        return result;
    }

    // Restaura fiel: posição, tamanho e recolhida (o chamador reaplica nas janelas, sem persistir).
    public static IReadOnlyList<QuadraOverviewFrame> RestoreTargets(IReadOnlyList<QuadraOverviewFrame> snapshot)
    {
        return snapshot.ToList();
    }
}
