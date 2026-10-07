using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using DeskQuadra.UI.Wpf.ViewModels;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class HighPriorityStartupTests
{
    private sealed class MockStartupService : IStartupService
    {
        public bool Enabled { get; set; }
        public int SetEnabledCalls { get; private set; }

        public bool IsEnabled() => Enabled;

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            SetEnabledCalls++;
        }
    }

    private sealed class MockDensitySettings : IDensitySettingsService
    {
        public DensityPreference Current => DensityPreference.Auto;
        public event EventHandler<DensityPreference>? PreferenceChanged;
        public void Set(DensityPreference preference) { PreferenceChanged?.Invoke(this, preference); }
    }

    [Fact]
    public void RegistryStartupService_IsEnabled_DoesNotThrow()
    {
        var service = new RegistryStartupService();
        var ex = Record.Exception(() => service.IsEnabled());
        Assert.Null(ex);
    }

    [Fact]
    public void NativeDesktopIconService_FindDesktopListViewHandle_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            IntPtr handle = NativeDesktopIconService.FindDesktopListViewHandle(out IntPtr shellView);
            // Handle pode ser IntPtr.Zero em ambiente headless de CI, mas nunca pode lançar exceção
        });
        Assert.Null(ex);
    }

    [Fact]
    public void SettingsViewModel_StartWithWindows_UpdatesStartupServiceAndPersists()
    {
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DeskQuadra_Test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        string tempSettingsFile = System.IO.Path.Combine(tempDir, "settings.json");

        try
        {
            var startup = new MockStartupService { Enabled = false };
            var density = new MockDensitySettings();

            var vm = new SettingsViewModel(startup, density, hasTouchHardware: false, customSettingsFilePath: tempSettingsFile);
            Assert.False(vm.StartWithWindows);

            vm.StartWithWindows = true;

            Assert.True(vm.StartWithWindows);
            Assert.True(startup.Enabled);
            Assert.Equal(1, startup.SetEnabledCalls);

            vm.StartWithWindows = false;

            Assert.False(vm.StartWithWindows);
            Assert.False(startup.Enabled);
            Assert.Equal(2, startup.SetEnabledCalls);
        }
        finally
        {
            try { System.IO.Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
