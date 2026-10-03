// T8a lazy load da seção de terceiros: guarda pura do token de geração.
// A parte síncrona do PopulateThirdPartySection incrementa a geração do
// ContextMenu; a continuação no Dispatcher só aplica se o token capturado
// ainda for o atual (abertura mais nova invalida a anterior). Sem WPF aqui
// de propósito: lógica pura, testável em xUnit.
namespace DeskQuadra.UI.Wpf.Services;

public static class ThirdPartyLazyLoadGuard
{
    public static bool ShouldApply(long capturedGeneration, long latestGeneration)
    {
        return capturedGeneration == latestGeneration;
    }
}
