using DeskQuadra.UI.Wpf.Services;

namespace DeskQuadra.UI.Wpf.Tests;

/// <summary>
/// Type-ahead estilo Explorer (lógica pura, sem Visual/Dispatcher):
/// primeira letra seleciona, mesma letra cicla, sequência rápida acumula,
/// pausa reinicia, busca sem case/acento, dígitos valem.
/// </summary>
public class QuadraTypeAheadTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static List<string?> Names(params string?[] names) => names.ToList();

    [Fact]
    public void Advance_PrimeiraLetra_SelecionaPrimeiroMatch()
    {
        var ta = new QuadraTypeAhead();

        int? hit = ta.Advance(Names("beta", "firma", "alfa"), -1, 'f', T0);

        Assert.Equal(1, hit);
    }

    [Fact]
    public void Advance_MesmaLetraRepetida_CiclaComWrap()
    {
        var ta = new QuadraTypeAhead();
        var names = Names("firma", "beta", "foto", "fatura");
        int? first = ta.Advance(names, -1, 'f', T0);
        Assert.Equal(0, first);

        int? second = ta.Advance(names, first!.Value, 'f', T0.AddMilliseconds(200));
        int? third = ta.Advance(names, second!.Value, 'f', T0.AddMilliseconds(400));
        int? wrapped = ta.Advance(names, third!.Value, 'f', T0.AddMilliseconds(600));

        Assert.Equal(2, second);
        Assert.Equal(3, third);
        Assert.Equal(0, wrapped);
    }

    [Fact]
    public void Advance_LetrasDiferentesRapidas_AcumulamPrefixo()
    {
        var ta = new QuadraTypeAhead();
        var names = Names("fatura", "firma", "foto");

        int? first = ta.Advance(names, -1, 'f', T0);
        int? second = ta.Advance(names, first!.Value, 'i', T0.AddMilliseconds(300));

        Assert.Equal(0, first);
        Assert.Equal(1, second);
        Assert.Equal("fi", ta.Prefix);
    }

    [Fact]
    public void Advance_PausaLonga_ReiniciaPrefixo()
    {
        var ta = new QuadraTypeAhead();
        var names = Names("firma", "foto", "item");

        ta.Advance(names, -1, 'f', T0);
        int? hit = ta.Advance(names, 1, 'i', T0.AddMilliseconds(1500));

        Assert.Equal(2, hit);
        Assert.Equal("i", ta.Prefix);
    }

    [Theory]
    [InlineData('F', "firma", 1)]
    [InlineData('f', "Firma", 1)]
    [InlineData('e', "Éden", 1)]
    [InlineData('a', "árvore", 1)]
    [InlineData('c', "çapa", 1)]
    public void Advance_IgnoraCaseEAcento(char key, string name, int expected)
    {
        var ta = new QuadraTypeAhead();

        Assert.Equal(expected, ta.Advance(Names("zzz", name), -1, key, T0));
    }

    [Fact]
    public void Advance_Digito_CasaPrefixoNumerico()
    {
        var ta = new QuadraTypeAhead();

        int? hit = ta.Advance(Names("ata", "2024-contrato", "2025-notas"), -1, '2', T0);

        Assert.Equal(1, hit);
    }

    [Fact]
    public void Advance_SemMatch_RetornaNullESemSelecao()
    {
        var ta = new QuadraTypeAhead();

        int? hit = ta.Advance(Names("alfa", "beta"), -1, 'z', T0);

        Assert.Null(hit);
    }

    [Fact]
    public void Advance_PrefixoAcumuladoSemMatch_CaiParaLetraSozinha()
    {
        var ta = new QuadraTypeAhead();
        var names = Names("fatura", "item");

        ta.Advance(names, -1, 'f', T0);
        int? hit = ta.Advance(names, 0, 'i', T0.AddMilliseconds(200));

        Assert.Equal(1, hit);
    }

    [Theory]
    [InlineData(' ')]
    [InlineData('.')]
    [InlineData('-')]
    public void Advance_NaoLetraNemDigito_Ignora(char key)
    {
        var ta = new QuadraTypeAhead();

        Assert.Null(ta.Advance(Names("alfa"), -1, key, T0));
    }

    [Fact]
    public void Timeout_ConstanteDocumentada_1s()
    {
        Assert.Equal(1000, QuadraTypeAhead.TimeoutMilliseconds);
    }

    [Fact]
    public void Reset_LimpaPrefixo()
    {
        var ta = new QuadraTypeAhead();
        ta.Advance(Names("firma"), -1, 'f', T0);

        ta.Reset();

        Assert.Equal(string.Empty, ta.Prefix);
    }
}
