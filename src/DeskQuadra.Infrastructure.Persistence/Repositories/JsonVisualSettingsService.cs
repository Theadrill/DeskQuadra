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
            if (isAdvanced)
            {
                _backgroundAlpha = Math.Clamp(_generalOpacity * 0.933, 5.0, 90.0);
                _tintIntensity = Math.Clamp(_generalOpacity * 0.5, 0.0, 100.0);
            }
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
        double generalOpacity;
        bool isAdvancedMode;
        double backgroundAlpha;
        double tintIntensity;
        bool enableEffects;
        VisualEffectTechnique preferredTechnique;
        lock (_gate)
        {
            generalOpacity = _generalOpacity;
            isAdvancedMode = _isAdvancedMode;
            backgroundAlpha = _backgroundAlpha;
            tintIntensity = _tintIntensity;
            enableEffects = _enableWindows11VisualEffects;
            preferredTechnique = _preferredTechnique;
        }

        SettingsJsonMerge.WriteMerge(_settingsFilePath, dict =>
        {
            dict["EnableWindows11VisualEffects"] = enableEffects;
            dict["PreferredTechnique"] = preferredTechnique.ToString();
            dict["GeneralOpacity"] = Math.Round(generalOpacity, 1);
            dict["IsAdvancedMode"] = isAdvancedMode;
            dict["BackgroundAlpha"] = Math.Round(backgroundAlpha, 1);
            dict["TintIntensity"] = Math.Round(tintIntensity, 1);
        });
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
