using DeskQuadra.Core.Models;
using Xunit;

namespace DeskQuadra.Core.Tests;

public class QuadraModelTests
{
    [Fact]
    public void Constructor_Default_InitializesWithValidGuidAndDefaults()
    {
        // Act
        var quadra = new Quadra();

        // Assert
        Assert.NotEqual(Guid.Empty, quadra.Id);
        Assert.Equal("Minha Quadra", quadra.Title);
        Assert.Equal(320, quadra.Width);
        Assert.Equal(240, quadra.Height);
        Assert.False(quadra.IsDefault);
        Assert.False(quadra.IsLocked);
        Assert.False(quadra.IsCollapsed);
        Assert.False(quadra.IsHidden);
    }

    [Fact]
    public void Constructor_CustomValues_SetsPropertiesCorrectly()
    {
        // Act
        var quadra = new Quadra("Trabalho", 150, 200, 400, 300, isDefault: true);

        // Assert
        Assert.NotEqual(Guid.Empty, quadra.Id);
        Assert.Equal("Trabalho", quadra.Title);
        Assert.Equal(150, quadra.Left);
        Assert.Equal(200, quadra.Top);
        Assert.Equal(400, quadra.Width);
        Assert.Equal(300, quadra.Height);
        Assert.True(quadra.IsDefault);
    }

    [Fact]
    public void Properties_CanBeUpdated()
    {
        // Arrange
        var quadra = new Quadra();

        // Act
        quadra.Title = "Projetos 2026";
        quadra.Left = 50;
        quadra.Top = 80;
        quadra.Width = 500;
        quadra.Height = 350;
        quadra.IsLocked = true;
        quadra.IsCollapsed = true;
        quadra.IsHidden = true;

        // Assert
        Assert.Equal("Projetos 2026", quadra.Title);
        Assert.Equal(50, quadra.Left);
        Assert.Equal(80, quadra.Top);
        Assert.Equal(500, quadra.Width);
        Assert.Equal(350, quadra.Height);
        Assert.True(quadra.IsLocked);
        Assert.True(quadra.IsCollapsed);
        Assert.True(quadra.IsHidden);
    }
}
