namespace DeskQuadra.UI.Wpf.Theme;

/// <summary>
/// Seam único de temas (Default): equivalente code-behind de DynamicResource.
/// Resolve via TryFindResource com fallback idêntico aos literais anteriores.
/// Usa sempre <see cref="System.Windows.Application.Current"/> qualificado:
/// "Application" sozinho resolve para o namespace DeskQuadra.Application.
/// </summary>
public static class ThemeResolver
{
    public static T Get<T>(string key, T fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) is T hit ? hit : fallback;
}
