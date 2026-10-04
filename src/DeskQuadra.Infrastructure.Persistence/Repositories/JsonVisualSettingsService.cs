using System.Text.Json;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;

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
    private VisualEffectTechnique _preferredTechnique = VisualEffectTechnique.Auto;

    public event EventHandler<bool>? VisualEffectsChanged;
    public event EventHandler<VisualEffectTechnique>? TechniqueChanged;

    public JsonVisualSettingsService(string? customStorageDirectory = null)
    {
        string dir = JsonStorageDefaults.GetAppDataDirectory(customStorageDirectory);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        (_enableWindows11VisualEffects, _preferredTechnique) = LoadBestEffort();
    }

    public bool EnableWindows11VisualEffects
    {
        get { lock (_gate) { return _enableWindows11VisualEffects; } }
    }

    public VisualEffectTechnique PreferredTechnique
    {
        get { lock (_gate) { return _preferredTechnique; } }
    }

    public void SetEnableWindows11VisualEffects(bool enabled)
    {
        bool changed;
        VisualEffectTechnique tech;
        lock (_gate)
        {
            changed = _enableWindows11VisualEffects != enabled;
            _enableWindows11VisualEffects = enabled;
            tech = _preferredTechnique;
        }

        SaveBestEffort(enabled, tech);
        if (changed)
        {
            VisualEffectsChanged?.Invoke(this, enabled);
        }
    }

    public void SetPreferredTechnique(VisualEffectTechnique technique)
    {
        bool changed;
        bool enabled;
        lock (_gate)
        {
            changed = _preferredTechnique != technique;
            _preferredTechnique = technique;
            enabled = _enableWindows11VisualEffects;
        }

        SaveBestEffort(enabled, technique);
        if (changed)
        {
            TechniqueChanged?.Invoke(this, technique);
        }
    }

    private (bool Enabled, VisualEffectTechnique Technique) LoadBestEffort()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return (true, VisualEffectTechnique.Auto);
            }

            string json = File.ReadAllText(_settingsFilePath);
            var dto = JsonSerializer.Deserialize<VisualSettingsDto>(json, JsonStorageDefaults.SerializerOptions);
            bool enabled = dto?.EnableWindows11VisualEffects ?? true;
            VisualEffectTechnique tech = VisualEffectTechnique.Auto;
            if (!string.IsNullOrEmpty(dto?.PreferredTechnique) &&
                Enum.TryParse<VisualEffectTechnique>(dto.PreferredTechnique, ignoreCase: true, out var parsedTech))
            {
                tech = parsedTech;
            }
            return (enabled, tech);
        }
        catch
        {
            return (true, VisualEffectTechnique.Auto);
        }
    }

    private void SaveBestEffort(bool enabled, VisualEffectTechnique technique)
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
            dict["PreferredTechnique"] = technique.ToString();

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
        public string? PreferredTechnique { get; set; }
    }
}
