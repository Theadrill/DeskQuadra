namespace DeskQuadra.Core.Models;

/// <summary>
/// Preferência de densidade da Quadra (Fatia 2).
/// Auto resolve via hardware touch; Normal/Touch forçam o modo.
/// </summary>
public enum DensityPreference
{
    Auto = 0,
    Normal = 1,
    Touch = 2
}
