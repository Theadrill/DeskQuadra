using DeskQuadra.UI.Wpf.Services;

namespace DeskQuadra.UI.Wpf.Tests;

// Lógica pura do overview do MOVER-via-touch (fatia 2): foto, grade estilo launcher e
// restaura fiel — sem Visual/Dispatcher (só Guids + doubles + bools).
public class TouchMoveOverviewLayoutTests
{
    private static QuadraOverviewFrame Frame(
        Guid id, double left, double top, double w, double h,
        bool collapsed = false, double? expanded = null, bool locked = false)
    {
        return new QuadraOverviewFrame(id, left, top, w, h, expanded ?? h, collapsed, locked, 200, 140);
    }

    private static void AssertDentroDaTela(OverviewSlot s, double sl, double st, double sw, double sh)
    {
        Assert.True(s.Left >= sl - 0.001, "Mini fora à esquerda/cima.");
        Assert.True(s.Top >= st - 0.001, "Mini fora à esquerda/cima.");
        Assert.True(s.Left + s.Width <= sl + sw + 0.001, "Mini transborda à direita.");
        Assert.True(s.Top + s.Height <= st + sh + 0.001, "Mini transborda embaixo.");
    }

    private static void AssertSemSobrepor(IReadOnlyList<OverviewSlot> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            for (int j = i + 1; j < slots.Count; j++)
            {
                var a = slots[i];
                var b = slots[j];
                bool separado = a.Left + a.Width <= b.Left + 0.001 || b.Left + b.Width <= a.Left + 0.001
                    || a.Top + a.Height <= b.Top + 0.001 || b.Top + b.Height <= a.Top + 0.001;
                Assert.True(separado, $"Minis {i} e {j} se sobrepõem.");
            }
        }
    }

    [Fact]
    public void Snapshot_CopiaFiel_MantemTudo()
    {
        var frames = new[]
        {
            Frame(Guid.NewGuid(), 10, 20, 360, 520),
            Frame(Guid.NewGuid(), 400, 100, 360, 60, collapsed: true, expanded: 520, locked: true),
        };

        var foto = TouchMoveOverviewLayout.Snapshot(frames);

        Assert.Equal(frames, foto);
    }

    [Fact]
    public void RestoreTargets_RestauraFiel_PosicaoTamanhoRecolhida()
    {
        var foto = new[]
        {
            Frame(Guid.NewGuid(), 10, 20, 360, 520),
            Frame(Guid.NewGuid(), 400, 100, 360, 60, collapsed: true, expanded: 520),
        };

        var alvos = TouchMoveOverviewLayout.RestoreTargets(foto);

        Assert.Equal(foto, alvos);
        Assert.True(alvos[1].IsCollapsed);
        Assert.Equal(520, alvos[1].ExpandedHeight);
    }

    [Fact]
    public void ComputeGrid_SemQuadras_RetornaVazio()
    {
        var slots = TouchMoveOverviewLayout.ComputeGrid(0, 0, 1920, 1080, Array.Empty<QuadraOverviewFrame>());

        Assert.Empty(slots);
    }

    [Fact]
    public void ComputeGrid_TelaDegenerada_RetornaVazio()
    {
        var frames = new[] { Frame(Guid.NewGuid(), 0, 0, 360, 520) };

        Assert.Empty(TouchMoveOverviewLayout.ComputeGrid(0, 0, 0, 1080, frames));
    }

    [Fact]
    public void ComputeGrid_UmaQuadra_MantemTamanhoCentralizada()
    {
        var frames = new[] { Frame(Guid.NewGuid(), 100, 100, 360, 520) };

        var slots = TouchMoveOverviewLayout.ComputeGrid(0, 0, 1920, 1080, frames);

        var slot = Assert.Single(slots);
        Assert.Equal(1872, slot.Width); // célula cheia: 1920 - 2*24
        Assert.Equal(1032, slot.Height); // célula cheia: 1080 - 2*24
        Assert.Equal(24, slot.Left, precision: 3);
        Assert.Equal(24, slot.Top, precision: 3);
    }

    [Fact]
    public void ComputeGrid_Recolhida_UsaAlturaExpandida()
    {
        // Mini uniforme: ignora a proporção (expandida ou não, ocupa a célula cheia).
        var frames = new[] { Frame(Guid.NewGuid(), 100, 100, 360, 60, collapsed: true, expanded: 520) };

        var slot = Assert.Single(TouchMoveOverviewLayout.ComputeGrid(0, 0, 1920, 1080, frames));

        Assert.Equal(1872, slot.Width);
        Assert.Equal(1032, slot.Height);
    }

    [Fact]
    public void ComputeGrid_QuatroQuadras_TodasCabemSemSobrepor()
    {
        var frames = Enumerable.Range(0, 4)
            .Select(i => Frame(Guid.NewGuid(), i * 400, 50, 360, 520))
            .ToList();

        var slots = TouchMoveOverviewLayout.ComputeGrid(0, 0, 1920, 1080, frames);

        Assert.Equal(4, slots.Count);
        Assert.Equal(frames.Select(f => f.Id).ToHashSet(), slots.Select(s => s.Id).ToHashSet());
        foreach (var slot in slots)
        {
            AssertDentroDaTela(slot, 0, 0, 1920, 1080);
        }
        AssertSemSobrepor(slots);
    }

    [Fact]
    public void ComputeGrid_Travada_ParticipaComoDestinoNormal()
    {
        // Lock trava a janela, não o conteúdo: travada ganha mini igual às demais.
        var frames = new[]
        {
            Frame(Guid.NewGuid(), 0, 0, 360, 520),
            Frame(Guid.NewGuid(), 400, 0, 360, 520, locked: true),
        };

        var slots = TouchMoveOverviewLayout.ComputeGrid(0, 0, 1920, 1080, frames);

        Assert.Equal(2, slots.Count);
        Assert.Equal(slots[0].Width, slots[1].Width);
        Assert.Equal(slots[0].Height, slots[1].Height);
    }

    [Fact]
    public void ComputeGrid_NoveQuadras_TodasDentroDaTela()
    {
        var frames = Enumerable.Range(0, 9)
            .Select(i => Frame(Guid.NewGuid(), (i % 3) * 400, (i / 3) * 500, 300, 400))
            .ToList();

        var slots = TouchMoveOverviewLayout.ComputeGrid(0, 0, 1920, 1080, frames);

        Assert.Equal(9, slots.Count);
        foreach (var slot in slots)
        {
            AssertDentroDaTela(slot, 0, 0, 1920, 1080);
        }
        AssertSemSobrepor(slots);
    }
}
