using DeskQuadra.Core.Models;

namespace DeskQuadra.Application.Snap;

/// <summary>
/// Motor geométrico puro desacoplado para cálculo de atração magnética elástica (Snap).
/// </summary>
public sealed class SnapEngine : ISnapEngine
{
    public SnapResult CalculateSnap(
        Rect2D current,
        Rect2D workArea,
        IEnumerable<Rect2D>? otherQuadras = null,
        double threshold = 16.0,
        double gap = 0.0)
    {
        double bestX = current.Left;
        double bestY = current.Top;
        double minDistanceX = threshold + 0.001;
        double minDistanceY = threshold + 0.001;
        bool snappedX = false;
        bool snappedY = false;

        void TestSnapX(double candidateX)
        {
            double dist = Math.Abs(candidateX - current.Left);
            if (dist <= threshold && dist < minDistanceX)
            {
                minDistanceX = dist;
                bestX = candidateX;
                snappedX = true;
            }
        }

        void TestSnapY(double candidateY)
        {
            double dist = Math.Abs(candidateY - current.Top);
            if (dist <= threshold && dist < minDistanceY)
            {
                minDistanceY = dist;
                bestY = candidateY;
                snappedY = true;
            }
        }

        // ==========================================
        // 1. Snap com as Bordas do Monitor (WorkArea)
        // ==========================================

        // Borda Esquerda da tela
        TestSnapX(workArea.Left + gap);

        // Borda Direita da tela
        TestSnapX(workArea.Right - gap - current.Width);

        // Borda Superior da tela
        TestSnapY(workArea.Top + gap);

        // Borda Inferior da tela
        TestSnapY(workArea.Bottom - gap - current.Height);

        // ==========================================
        // 2. Snap com Outras Quadras Adjacentes
        // ==========================================
        if (otherQuadras is not null)
        {
            foreach (var other in otherQuadras)
            {
                // Verifica se há proximidade ou projeção vertical para snap horizontal
                bool nearY = (current.Bottom >= other.Top - threshold) && (current.Top <= other.Bottom + threshold);
                if (nearY)
                {
                    // Lado esquerdo de 'other' (current fica à esquerda)
                    TestSnapX(other.Left - gap - current.Width);

                    // Lado direito de 'other' (current fica à direita)
                    TestSnapX(other.Right + gap);

                    // Alinhamento de bordas esquerdas
                    TestSnapX(other.Left);

                    // Alinhamento de bordas direitas
                    TestSnapX(other.Right - current.Width);
                }

                // Verifica se há proximidade ou projeção horizontal para snap vertical
                bool nearX = (current.Right >= other.Left - threshold) && (current.Left <= other.Right + threshold);
                if (nearX)
                {
                    // Acima de 'other' (current fica acima)
                    TestSnapY(other.Top - gap - current.Height);

                    // Abaixo de 'other' (current fica abaixo)
                    TestSnapY(other.Bottom + gap);

                    // Alinhamento de bordas superiores
                    TestSnapY(other.Top);

                    // Alinhamento de bordas inferiores
                    TestSnapY(other.Bottom - current.Height);
                }
            }
        }

        return new SnapResult(bestX, bestY, snappedX, snappedY);
    }
}
