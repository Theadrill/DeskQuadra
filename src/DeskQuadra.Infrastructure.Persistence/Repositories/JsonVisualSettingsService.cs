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
    private double _generalOpacity = 30.0;
    private bool _isAdvancedMode = false;
    private double _backgroundAlpha = 28.0;
    private double _tintIntensity = 15.0;

    public event EventHandler<bool>? VisualEffectsChanged;
    public event EventHandler<VisualEffectTechnique>? TechniqueChanged;
    public event EventHandler? VisualOpacityChanged;

    public JsonVisualSettingsService(string? customStorageDirectory = null)
    {
        string dir = JsonStorageDefaults.GetAppDataDirectory(customStorageDirectory);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        LoadBestEffort();
    }

    public bool EnableWindows11VisualEffects
    {
        get { lock (_gate) { return _enableWindows11VisualEffects; } }
    }

    public VisualEffectTechnique PreferredTechnique
    {
        get { lock (_gate) { return _preferredTechnique; } }
    }

    public double GeneralOpacity
    {
        get { lock (_gate) { return _generalOpacity; } }
    }

    public bool IsAdvancedMode
    {
        get { lock (_gate) { return _isAdvancedMode; } }
    }

    public double BackgroundAlpha
    {
        get { lock (_gate) { return _backgroundAlpha; } }
    }

    public double TintIntensity
    {
        get { lock (_gate) { return _tintIntensity; } }
    }

    public void SetEnableWindows11VisualEffects(bool enabled)
    {
        bool changed;
        lock (_gate)
        {
            changed = _enableWindows11VisualEffects != enabled;
            _enableWindows11VisualEffects = enabled;
        }

        SaveBestEffort();
        if (changed)
        {
            VisualEffectsChanged?.Invoke(this, enabled);
        }
    }

    public void SetPreferredTechnique(VisualEffectTechnique technique)
    {
        bool changed;
        lock (_gate)
        {
            changed = _preferredTechnique != technique;
            _preferredTechnique = technique;
        }

        SaveBestEffort();
        if (changed)
        {
            TechniqueChanged?.Invoke(this, technique);
        }
    }

    public void SetGeneralOpacity(double opacity)
    {
        double clamped = Math.Clamp(opacity, 5.0, 90.0);
        bool changed;
        lock (_gate)
        {
            changed = Math.Abs(_generalOpacity - clamped) > 0.001;
            _generalOpacity = clamped;
            // Se não estiver em modo avançado, sincroniza fundo e tint proporcionalmente
            if (!_isAdvancedMode)
            {
                _backgroundAlpha = Math.Clamp(clamped * 0.933, 5.0, 90.0); // 30% -> ~28%
                _tintIntensity = Math.Clamp(clamped * 0.5, 0.0, 100.0);    // 30% -> ~15%
            }
        }

        SaveBestEffort();
        if (changed)
        {
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetAdvancedMode(bool isAdvanced)
    {
        bool changed;
        lock (_gate)
        {
            changed = _isAdvancedMode != isAdvanced;
            _isAdvancedMode = isAdvanced;
        }

        SaveBestEffort();
        if (changed)
        {
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetBackgroundAlpha(double alpha)
    {
        double clamped = Math.Clamp(alpha, 5.0, 90.0);
        bool changed;
        lock (_gate)
        {
            changed = Math.Abs(_backgroundAlpha - clamped) > 0.001;
            _backgroundAlpha = clamped;
        }

        SaveBestEffort();
        if (changed)
        {
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetTintIntensity(double tint)
    {
        double clamped = Math.Clamp(tint, 0.0, 100.0);
        bool changed;
        lock (_gate)
        {
            changed = Math.Abs(_tintIntensity - clamped) > 0.001;
            _tintIntensity = clamped;
        }

        SaveBestEffort();
        if (changed)
        {
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ResetToDefaults()
    {
        lock (_gate)
        {
            _generalOpacity = 30.0;
            _backgroundAlpha = 28.0;
            _tintIntensity = 15.0;
        }

        SaveBestEffort();
        VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadBestEffort()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return;
            }

            string json = File.ReadAllText(_settingsFilePath);
            var dto = JsonSerializer.Deserialize<VisualSettingsDto>(json, JsonStorageDefaults.SerializerOptions);
            if (dto != null)
            {
                lock (_gate)
                {
                    _enableWindows11VisualEffects = dto.EnableWindows11VisualEffects ?? true;
                    if (!string.IsNullOrEmpty(dto.PreferredTechnique) &&
                        Enum.TryParse<VisualEffectTechnique>(dto.PreferredTechnique, ignoreCase: true, out var parsedTech))
                    {
                        _preferredTechnique = parsedTech;
                    }

                    if (dto.GeneralOpacity.HasValue)
                    {
                        _generalOpacity = Math.Clamp(dto.GeneralOpacity.Value, 5.0, 90.0);
                    }

                    if (dto.IsAdvancedMode.HasValue)
                    {
                        _isAdvancedMode = dto.IsAdvancedMode.Value;
                    }

                    if (dto.BackgroundAlpha.HasValue)
                    {
                        _backgroundAlpha = Math.Clamp(dto.BackgroundAlpha.Value, 5.0, 90.0);
                    }

                    if (dto.TintIntensity.HasValue)
                    {
                        _tintIntensity = Math.Clamp(dto.TintIntensity.Value, 0.0, 100.0);
                    }
                }
            }
        }
        catch
        {
            // Fallback nos padrões de fábrica já inicializados nos campos
        }
    }

    private void SaveBestEffort()
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

            lock (_gate)
            {
                dict["EnableWindows11VisualEffects"] = _enableWindows11VisualEffects;
                dict["PreferredTechnique"] = _preferredTechnique.ToString();
                dict["GeneralOpacity"] = Math.Round(_generalOpacity, 1);
                dict["IsAdvancedMode"] = _isAdvancedMode;
                dict["BackgroundAlpha"] = Math.Round(_backgroundAlpha, 1);
                dict["TintIntensity"] = Math.Round(_tintIntensity, 1);
            }

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
        public double? GeneralOpacity { get; set; }
        public bool? IsAdvancedMode { get; set; }
        public double? BackgroundAlpha { get; set; }
        public double? TintIntensity { get; set; }
    }
}
