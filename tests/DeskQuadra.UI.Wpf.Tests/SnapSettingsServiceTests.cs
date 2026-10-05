using System.IO;
using DeskQuadra.Infrastructure.Persistence.Repositories;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class SnapSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SnapSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeskQuadra_SnapSettingsTests_" + Guid.NewGuid().ToString("N"));
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
    public void DefaultsToEight_WhenFileDoesNotExist()
    {
        var service = new JsonSnapSettingsService(_tempDir);
        Assert.Equal(8.0, service.Gap);
    }

    [Fact]
    public void SetGap_PersistsAndFiresEvent()
    {
        var service = new JsonSnapSettingsService(_tempDir);
        bool eventFired = false;
        double newValue = 0;

        service.GapChanged += (s, val) =>
        {
            eventFired = true;
            newValue = val;
        };

        service.SetGap(16);

        Assert.True(eventFired);
        Assert.Equal(16.0, newValue);
        Assert.Equal(16.0, service.Gap);

        var reloaded = new JsonSnapSettingsService(_tempDir);
        Assert.Equal(16.0, reloaded.Gap);
    }

    [Fact]
    public void SetGap_ClampsToRange()
    {
        var service = new JsonSnapSettingsService(_tempDir);

        service.SetGap(-5);
        Assert.Equal(0.0, service.Gap);

        service.SetGap(99);
        Assert.Equal(24.0, service.Gap);

        var reloaded = new JsonSnapSettingsService(_tempDir);
        Assert.Equal(24.0, reloaded.Gap);
    }

    [Fact]
    public void PreservesOtherPreferences_WhenSavingGap()
    {
        var densityService = new JsonDensitySettingsService(_tempDir);
        densityService.Set(DeskQuadra.Core.Models.DensityPreference.Touch);

        var snapService = new JsonSnapSettingsService(_tempDir);
        snapService.SetGap(12);

        var reloadDensity = new JsonDensitySettingsService(_tempDir);
        var reloadSnap = new JsonSnapSettingsService(_tempDir);

        Assert.Equal(DeskQuadra.Core.Models.DensityPreference.Touch, reloadDensity.Current);
        Assert.Equal(12.0, reloadSnap.Gap);
    }
}
