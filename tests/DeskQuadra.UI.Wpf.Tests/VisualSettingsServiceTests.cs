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
}
