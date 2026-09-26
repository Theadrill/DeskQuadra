using DeskQuadra.Application.Snap;
using DeskQuadra.Core.Models;
using Xunit;

namespace DeskQuadra.Application.Tests;

public class SnapEngineTests
{
    private readonly SnapEngine _engine = new();
    private readonly Rect2D _workArea = new(0, 0, 1920, 1080);

    [Fact]
    public void CalculateSnap_NearLeftScreenEdge_SnapsToLeft()
    {
        // Arrange (Left em 10px quando a borda é 0px, threshold 16px)
        var current = new Rect2D(10, 200, 300, 200);

        // Act
        var result = _engine.CalculateSnap(current, _workArea, threshold: 16, gap: 0);

        // Assert
        Assert.True(result.SnappedX);
        Assert.False(result.SnappedY);
        Assert.Equal(0, result.X);
        Assert.Equal(200, result.Y);
    }

    [Fact]
    public void CalculateSnap_NearRightScreenEdge_SnapsToRight()
    {
        // Arrange (1920 - 300 = 1620, colocamos em 1615, a 5px da borda direita)
        var current = new Rect2D(1615, 200, 300, 200);

        // Act
        var result = _engine.CalculateSnap(current, _workArea, threshold: 16, gap: 0);

        // Assert
        Assert.True(result.SnappedX);
        Assert.Equal(1620, result.X);
    }

    [Fact]
    public void CalculateSnap_NearTopScreenEdge_SnapsToTop()
    {
        // Arrange
        var current = new Rect2D(500, 8, 300, 200);

        // Act
        var result = _engine.CalculateSnap(current, _workArea, threshold: 16, gap: 0);

        // Assert
        Assert.True(result.SnappedY);
        Assert.Equal(0, result.Y);
    }

    [Fact]
    public void CalculateSnap_NearBottomScreenEdge_SnapsToBottom()
    {
        // Arrange (1080 - 200 = 880, colocamos em 875)
        var current = new Rect2D(500, 875, 300, 200);

        // Act
        var result = _engine.CalculateSnap(current, _workArea, threshold: 16, gap: 0);

        // Assert
        Assert.True(result.SnappedY);
        Assert.Equal(880, result.Y);
    }

    [Fact]
    public void CalculateSnap_WithGap_AppliesGapCorrectly()
    {
        // Arrange (com gap de 8px, borda esquerda snap em 8px)
        var current = new Rect2D(12, 200, 300, 200);

        // Act
        var result = _engine.CalculateSnap(current, _workArea, threshold: 16, gap: 8);

        // Assert
        Assert.True(result.SnappedX);
        Assert.Equal(8, result.X);
    }

    [Fact]
    public void CalculateSnap_FarFromEdges_DoesNotSnap()
    {
        // Arrange
        var current = new Rect2D(500, 500, 300, 200);

        // Act
        var result = _engine.CalculateSnap(current, _workArea, threshold: 16, gap: 0);

        // Assert
        Assert.False(result.SnappedX);
        Assert.False(result.SnappedY);
        Assert.Equal(500, result.X);
        Assert.Equal(500, result.Y);
    }

    [Fact]
    public void CalculateSnap_NearAnotherQuadra_SnapsAdjacent()
    {
        // Arrange: Quadra vizinha em X=200, Width=300 (Right=500).
        // Current em X=508 (a 8px de distância de colar ao lado)
        var existing = new[] { new Rect2D(200, 200, 300, 300) };
        var current = new Rect2D(508, 250, 200, 200);

        // Act
        var result = _engine.CalculateSnap(current, _workArea, existing, threshold: 16, gap: 0);

        // Assert
        Assert.True(result.SnappedX);
        Assert.Equal(500, result.X);
    }

    [Fact]
    public void CalculateSnap_NearAnotherQuadra_AlignsTopEdges()
    {
        // Arrange: Quadra vizinha em X=100 a 400, Y=200. Current em X=350, Y=206 (a 6px de alinhar o topo)
        var existing = new[] { new Rect2D(100, 200, 300, 300) };
        var current = new Rect2D(350, 206, 200, 200);

        // Act
        var result = _engine.CalculateSnap(current, _workArea, existing, threshold: 16, gap: 0);

        // Assert
        Assert.True(result.SnappedY);
        Assert.Equal(200, result.Y);
    }
}
