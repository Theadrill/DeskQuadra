using System.Text.Json;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;

namespace DeskQuadra.Infrastructure.Persistence.Repositories;

/// <summary>
/// Persistência dedicada da preferência de densidade em %APPDATA%\DeskQuadra\settings.json.
/// Separado de quadras.json (só layout) para não quebrar migração: arquivo ausente/corrompido =&gt; Auto.
/// </summary>
public sealed class JsonDensitySettingsService : IDensitySettingsService
{
    private readonly string _settingsFilePath;
    private readonly object _gate = new();
    private DensityPreference _current = DensityPreference.Auto;

    public event EventHandler<DensityPreference>? PreferenceChanged;

    public JsonDensitySettingsService(string? customStorageDirectory = null)
    {
        string dir = JsonStorageDefaults.GetAppDataDirectory(customStorageDirectory);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        _current = LoadBestEffort();
    }

    public DensityPreference Current
    {
        get { lock (_gate) { return _current; } }
    }

    public void Set(DensityPreference preference)
    {
        bool changed;
        lock (_gate)
        {
            changed = _current != preference;
            _current = preference;
        }

        SaveBestEffort(preference);
        if (changed)
        {
            PreferenceChanged?.Invoke(this, preference);
        }
    }

    private DensityPreference LoadBestEffort()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return DensityPreference.Auto;
            }

            string json = File.ReadAllText(_settingsFilePath);
            var dto = JsonSerializer.Deserialize<SettingsDto>(json, JsonStorageDefaults.SerializerOptions);
            return dto is not null && Enum.IsDefined(typeof(DensityPreference), dto.DensityPreference)
                ? dto.DensityPreference
                : DensityPreference.Auto;
        }
        catch
        {
            return DensityPreference.Auto;
        }
    }

    private void SaveBestEffort(DensityPreference preference)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(new SettingsDto { DensityPreference = preference }, JsonStorageDefaults.SerializerOptions);
            string tmp = _settingsFilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _settingsFilePath, overwrite: true);
        }
        catch
        {
            // Best-effort: preferência segue em memória; próxima abertura relê Auto.
        }
    }

    private sealed class SettingsDto
    {
        public DensityPreference DensityPreference { get; set; } = DensityPreference.Auto;
    }
}
