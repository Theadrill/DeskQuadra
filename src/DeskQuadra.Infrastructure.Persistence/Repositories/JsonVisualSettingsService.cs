using System.Text.Json;
using DeskQuadra.Core.Contracts;

namespace DeskQuadra.Infrastructure.Persistence.Repositories;

/// <summary>
/// Persistência da preferência de efeitos visuais Windows 11 em %APPDATA%\DeskQuadra\settings.json.
/// Compartilha o arquivo settings.json de forma resiliente com as demais preferências.
/// </summary>
public sealed class JsonVisualSettingsService : IVisualSettingsService
{
    private readonly string _settingsFilePath;
    private readonly object _gate = new();
    private bool _enableWindows11VisualEffects = true;

    public event EventHandler<bool>? VisualEffectsChanged;

    public JsonVisualSettingsService(string? customStorageDirectory = null)
    {
        string dir = JsonStorageDefaults.GetAppDataDirectory(customStorageDirectory);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        _enableWindows11VisualEffects = LoadBestEffort();
    }

    public bool EnableWindows11VisualEffects
    {
        get { lock (_gate) { return _enableWindows11VisualEffects; } }
    }

    public void SetEnableWindows11VisualEffects(bool enabled)
    {
        bool changed;
        lock (_gate)
        {
            changed = _enableWindows11VisualEffects != enabled;
            _enableWindows11VisualEffects = enabled;
        }

        SaveBestEffort(enabled);
        if (changed)
        {
            VisualEffectsChanged?.Invoke(this, enabled);
        }
    }

    private bool LoadBestEffort()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return true;
            }

            string json = File.ReadAllText(_settingsFilePath);
            var dto = JsonSerializer.Deserialize<VisualSettingsDto>(json, JsonStorageDefaults.SerializerOptions);
            return dto?.EnableWindows11VisualEffects ?? true;
        }
        catch
        {
            return true;
        }
    }

    private void SaveBestEffort(bool enabled)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Lê o DTO existente para preservar outros campos (ex: DensityPreference)
            var node = File.Exists(_settingsFilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(_settingsFilePath))
                : new Dictionary<string, JsonElement>();

            var dict = node != null ? new Dictionary<string, object>() : new Dictionary<string, object>();
            if (node != null)
            {
                foreach (var kvp in node)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }

            dict["EnableWindows11VisualEffects"] = enabled;

            string json = JsonSerializer.Serialize(dict, JsonStorageDefaults.SerializerOptions);
            string tmp = _settingsFilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _settingsFilePath, overwrite: true);
        }
        catch
        {
            // Best-effort: se falhar escrita, estado permanece em memória
        }
    }

    private sealed class VisualSettingsDto
    {
        public bool? EnableWindows11VisualEffects { get; set; }
    }
}
