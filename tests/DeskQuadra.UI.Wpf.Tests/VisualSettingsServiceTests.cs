using System.IO;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.Persistence.Repositories;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class VisualSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public VisualSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeskQuadra_VisualSettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void DefaultsToEnabled_WhenFileDoesNotExist()
    {
        var service = new JsonVisualSettingsService(_tempDir);
        Assert.True(service.EnableWindows11VisualEffects);
    }

    [Fact]
    public void SetEnableWindows11VisualEffects_PersistsAndFiresEvent()
    {
        var service = new JsonVisualSettingsService(_tempDir);
        bool eventFired = false;
        bool newValue = false;

        service.VisualEffectsChanged += (s, val) =>
        {
            eventFired = true;
            newValue = val;
        };

        service.SetEnableWindows11VisualEffects(false);

        Assert.True(eventFired);
        Assert.False(newValue);
        Assert.False(service.EnableWindows11VisualEffects);

        // Recria o serviço para garantir que persistiu em disco
        var service2 = new JsonVisualSettingsService(_tempDir);
        Assert.False(service2.EnableWindows11VisualEffects);
    }

    [Fact]
    public void PreservesDensityPreference_WhenSavingVisualEffects()
    {
        var densityService = new JsonDensitySettingsService(_tempDir);
        densityService.Set(DeskQuadra.Core.Models.DensityPreference.Touch);

        var visualService = new JsonVisualSettingsService(_tempDir);
        visualService.SetEnableWindows11VisualEffects(false);

        // Recarrega ambos para verificar integridade do JSON compartilhado
        var reloadDensity = new JsonDensitySettingsService(_tempDir);
        var reloadVisual = new JsonVisualSettingsService(_tempDir);

        Assert.Equal(DeskQuadra.Core.Models.DensityPreference.Touch, reloadDensity.Current);
        Assert.False(reloadVisual.EnableWindows11VisualEffects);
    }

    [Fact]
    public void DefaultsToAutoTechnique_WhenFileDoesNotExist()
    {
        var service = new JsonVisualSettingsService(_tempDir);
        Assert.Equal(DeskQuadra.Core.Models.VisualEffectTechnique.Auto, service.PreferredTechnique);
    }

    [Fact]
    public void SetPreferredTechnique_PersistsAndFiresEvent()
    {
        var service = new JsonVisualSettingsService(_tempDir);
        bool eventFired = false;
        DeskQuadra.Core.Models.VisualEffectTechnique newTech = DeskQuadra.Core.Models.VisualEffectTechnique.Auto;

        service.TechniqueChanged += (s, tech) =>
        {
            eventFired = true;
            newTech = tech;
        };

        service.SetPreferredTechnique(DeskQuadra.Core.Models.VisualEffectTechnique.ClassicBlur);

        Assert.True(eventFired);
        Assert.Equal(DeskQuadra.Core.Models.VisualEffectTechnique.ClassicBlur, newTech);
        Assert.Equal(DeskQuadra.Core.Models.VisualEffectTechnique.ClassicBlur, service.PreferredTechnique);

        // Recria para garantir persistência em disco
        var service2 = new JsonVisualSettingsService(_tempDir);
        Assert.Equal(DeskQuadra.Core.Models.VisualEffectTechnique.ClassicBlur, service2.PreferredTechnique);
    }

    [Fact]
    public void OpacityDefaults_AreExpectedValues()
    {
        var service = new JsonVisualSettingsService(_tempDir);
        Assert.Equal(30.0, service.GeneralOpacity);
        Assert.False(service.IsAdvancedMode);
        Assert.Equal(28.0, service.BackgroundAlpha);
        Assert.Equal(15.0, service.TintIntensity);
    }

    [Fact]
    public void SetGeneralOpacity_ClampsAndPersistsAndFiresEvent()
    {
        var service = new JsonVisualSettingsService(_tempDir);
        bool fired = false;
        service.VisualOpacityChanged += (s, e) => fired = true;

        service.SetGeneralOpacity(50.0);
        Assert.True(fired);
        Assert.Equal(50.0, service.GeneralOpacity);

        // Clamping abaixo do mínimo (5) e acima do máximo (90)
        service.SetGeneralOpacity(1.0);
        Assert.Equal(5.0, service.GeneralOpacity);

        service.SetGeneralOpacity(99.0);
        Assert.Equal(90.0, service.GeneralOpacity);

        // Persistência em disco
        var reloaded = new JsonVisualSettingsService(_tempDir);
        Assert.Equal(90.0, reloaded.GeneralOpacity);
    }

    [Fact]
    public void SetAdvancedModeAndSliders_PersistsIndependently()
    {
        var service = new JsonVisualSettingsService(_tempDir);
        service.SetAdvancedMode(true);
        service.SetBackgroundAlpha(65.0);
        service.SetTintIntensity(40.0);

        var reloaded = new JsonVisualSettingsService(_tempDir);
        Assert.True(reloaded.IsAdvancedMode);
        Assert.Equal(65.0, reloaded.BackgroundAlpha);
        Assert.Equal(40.0, reloaded.TintIntensity);
    }

    [Fact]
    public void ResetToDefaults_RestoresOriginalValues()
    {
        var service = new JsonVisualSettingsService(_tempDir);
        service.SetGeneralOpacity(70.0);
        service.SetBackgroundAlpha(80.0);
        service.SetTintIntensity(50.0);

        service.ResetToDefaults();
        Assert.Equal(30.0, service.GeneralOpacity);
        Assert.Equal(28.0, service.BackgroundAlpha);
        Assert.Equal(15.0, service.TintIntensity);

        var reloaded = new JsonVisualSettingsService(_tempDir);
        Assert.Equal(30.0, reloaded.GeneralOpacity);
        Assert.Equal(28.0, reloaded.BackgroundAlpha);
        Assert.Equal(15.0, reloaded.TintIntensity);
    }
}

