using DeskQuadra.Core.Models;

namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Preferência de densidade (Aparência). settings.json dedicado; quadras.json segue só layout.
/// Best-effort: falha de I/O nunca derruba a UI (leitura retorna Auto).
/// </summary>
public interface IDensitySettingsService
{
    DensityPreference Current { get; }

    void Set(DensityPreference preference);

    event EventHandler<DensityPreference>? PreferenceChanged;
}
