using System.Runtime.InteropServices;
using DeskQuadra.Core;
using DeskQuadra.Core.Contracts;
using Microsoft.Win32;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Autostart de alta velocidade combinando:
/// 1. Windows Task Scheduler (disparo no logon com PT0S delay, prioridade normal 4, sem throttle do Explorer).
/// 2. HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize (StartupDelayInMSec = 0).
/// 3. HKCU\Software\Microsoft\Windows\CurrentVersion\Run (espelho para visualização no Gerenciador de Tarefas).
/// Best-effort: falha em qualquer camada nunca derruba o app.
/// </summary>
public sealed class RegistryStartupService : IStartupService
{
    private const string SerializeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize";
    private const string StartupDelayValueName = "StartupDelayInMSec";
    private const string TaskName = "DeskQuadra";

    public bool IsEnabled()
    {
        return IsTaskScheduled() || IsRegistryRunEnabled();
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            string? exe = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            string command = StartupCommandBuilder.Build(exe);
            if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(exe))
            {
                return;
            }

            // 1. Task Scheduler: Prioridade interativa (4), delay 0s, compatível com laptops/Steam Deck
            RegisterScheduledTask(exe.Trim().Trim('"'), IStartupService.SilentArgument);

            // 2. Explorer Serialize: neutraliza o delay de 10-30s do Explorer
            SetExplorerStartupDelayZero();

            // 3. Registry Run: espelho para o Gerenciador de Tarefas do Windows
            SetRegistryRun(command);
        }
        else
        {
            DeleteScheduledTask();
            DeleteRegistryRun();
        }
    }

    private static bool IsRegistryRunEnabled()
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

    private static void SetRegistryRun(string command)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(IStartupService.RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(IStartupService.RunKeyPath);
            key?.SetValue(IStartupService.ValueName, command, RegistryValueKind.String);
        }
        catch
        {
            // Best-effort
        }
    }

    private static void DeleteRegistryRun()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(IStartupService.RunKeyPath, writable: true);
            key?.DeleteValue(IStartupService.ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Best-effort
        }
    }

    private static void SetExplorerStartupDelayZero()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SerializeKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(SerializeKeyPath);
            key?.SetValue(StartupDelayValueName, 0, RegistryValueKind.DWord);
        }
        catch
        {
            // Best-effort
        }
    }

    private static bool IsTaskScheduled()
    {
        try
        {
            Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null)
            {
                return false;
            }

            object? service = Activator.CreateInstance(serviceType);
            if (service == null)
            {
                return false;
            }

            try
            {
                dynamic dynamicService = service;
                dynamicService.Connect();
                dynamic folder = dynamicService.GetFolder(@"\");
                dynamic? task = folder.GetTask(TaskName);
                if (task is null)
                {
                    return false;
                }
                return task.Enabled == true;
            }
            finally
            {
                if (Marshal.IsComObject(service))
                {
                    Marshal.ReleaseComObject(service);
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool RegisterScheduledTask(string rawExePath, string arguments)
    {
        try
        {
            Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null)
            {
                return false;
            }

            object? service = Activator.CreateInstance(serviceType);
            if (service == null)
            {
                return false;
            }

            try
            {
                dynamic dynamicService = service;
                dynamicService.Connect();
                dynamic folder = dynamicService.GetFolder(@"\");

                dynamic task = dynamicService.NewTask(0);
                task.RegistrationInfo.Description = "DeskQuadra Instant Startup";

                // Sem limitação de bateria (essencial para portáteis e laptops)
                task.Settings.DisallowStartIfOnBatteries = false;
                task.Settings.StopIfGoingOnBatteries = false;
                task.Settings.ExecutionTimeLimit = "PT0S";
                task.Settings.Priority = 4; // Prioridade Normal interativa (padrão 7 é Idle)

                dynamic action = task.Actions.Create(0); // 0 = TASK_ACTION_EXEC
                action.Path = rawExePath;
                action.Arguments = arguments;

                dynamic trigger = task.Triggers.Create(9); // 9 = TASK_TRIGGER_LOGON
                trigger.UserId = System.Security.Principal.WindowsIdentity.GetCurrent()?.Name ?? string.Empty;
                trigger.Delay = "PT0S";

                // 6 = TASK_CREATE_OR_UPDATE, 3 = TASK_LOGON_INTERACTIVE_TOKEN
                folder.RegisterTaskDefinition(TaskName, task, 6, null, null, 3, null);
                return true;
            }
            finally
            {
                if (Marshal.IsComObject(service))
                {
                    Marshal.ReleaseComObject(service);
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool DeleteScheduledTask()
    {
        try
        {
            Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null)
            {
                return false;
            }

            object? service = Activator.CreateInstance(serviceType);
            if (service == null)
            {
                return false;
            }

            try
            {
                dynamic dynamicService = service;
                dynamicService.Connect();
                dynamic folder = dynamicService.GetFolder(@"\");
                folder.DeleteTask(TaskName, 0);
                return true;
            }
            finally
            {
                if (Marshal.IsComObject(service))
                {
                    Marshal.ReleaseComObject(service);
                }
            }
        }
        catch
        {
            return false;
        }
    }
}
