using System.Text.Json;
using DeskQuadra.Core.Contracts;

namespace DeskQuadra.Infrastructure.Persistence.Repositories;

/// <summary>
/// Persistência do espaçamento magnético (snap gap) em %APPDATA%\DeskQuadra\settings.json.
/// Reusa <see cref="JsonStorageDefaults"/> (diretório + opções) e <see cref="SettingsJsonMerge"/>
/// (merge best-effort que preserva as demais preferências). Ausente/corrompido =&gt; 8.
/// </summary>
public sealed class JsonSnapSettingsService : ISnapSettingsService
{
    public const double DefaultGap = 8.0;
    public const double MinGap = 0.0;
    public const double MaxGap = 24.0;

    private readonly string _settingsFilePath;
    private readonly object _gate = new();
    private double _gap = DefaultGap;

    public event EventHandler<double>? GapChanged;

    public JsonSnapSettingsService(string? customStorageDirectory = null)
    {
        string dir = JsonStorageDefaults.GetAppDataDirectory(customStorageDirectory);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        _gap = LoadBestEffort();
    }

    public double Gap
    {
        get { lock (_gate) { return _gap; } }
    }

    public void SetGap(double gap)
    {
        double clamped = Math.Clamp(gap, MinGap, MaxGap);
        bool changed;
        lock (_gate)
        {
            changed = Math.Abs(_gap - clamped) > 0.001;
            _gap = clamped;
        }

        SettingsJsonMerge.WriteMerge(_settingsFilePath, dict =>
        {
            lock (_gate)
            {
                dict["SnapGap"] = Math.Round(_gap, 1);
            }
        });

        if (changed)
        {
            GapChanged?.Invoke(this, clamped);
        }
    }

    private double LoadBestEffort()
    {
        try
        {
            var node = SettingsJsonMerge.ReadAll(_settingsFilePath);
            if (node != null && node.TryGetValue("SnapGap", out var element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetDouble(out double value))
            {
                return Math.Clamp(value, MinGap, MaxGap);
            }
        }
        catch
        {
            // Fallback no padrão de fábrica.
        }

        return DefaultGap;
    }
}
