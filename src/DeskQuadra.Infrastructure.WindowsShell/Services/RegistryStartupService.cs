using DeskQuadra.Core;
using DeskQuadra.Core.Contracts;
using Microsoft.Win32;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Autostart via HKCU\Software\Microsoft\Windows\CurrentVersion\Run (valor DeskQuadra = "&lt;exe&gt;" --silent).
/// Best-effort: falha de registry nunca derruba a UI (IsEnabled=false, SetEnabled no-op).
/// </summary>
public sealed class RegistryStartupService : IStartupService
{
    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(IStartupService.RunKeyPath, writable: false);
            return key?.GetValue(IStartupService.ValueName) is string current
                && !string.IsNullOrWhiteSpace(current);
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(IStartupService.RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(IStartupService.RunKeyPath);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                string? exe = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                string command = StartupCommandBuilder.Build(exe);
                if (string.IsNullOrWhiteSpace(command))
                {
                    return;
                }

                key.SetValue(IStartupService.ValueName, command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(IStartupService.ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Best-effort: checkbox reflete o estado real na próxima abertura (IsEnabled).
        }
    }
}
