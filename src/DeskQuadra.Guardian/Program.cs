using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DeskQuadra.Infrastructure.WindowsShell.Services;

namespace DeskQuadra.Guardian;

internal static class Program
{
    private const uint SYNCHRONIZE = 0x00100000;
    private const uint INFINITE = 0xFFFFFFFF;
    private const uint GENERIC_ALL = 0x10000000;

    // Botão de pânico (dono: Guardian — hotkey no app principal é inútil com a UI travada).
    private const int HOTKEY_ID = 0xD001;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_Q = 0x51;
    private const int WM_HOTKEY = 0x0312;
    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONINFORMATION = 0x00000040;

    // Nome do evento de saída limpa (o app cria/escuta; o Guardian só sinaliza, best-effort).
    private const string GracefulExitEventName = "DeskQuadraGracefulExit";
    // Espera máxima pela saída limpa do pai antes do kill (gracioso-primeiro-depois-matador).
    private const int PanicGracefulWaitMs = 2500;

    // Debounce: ignora novos WM_HOTKEY com pânico já em curso (0 = livre, 1 = em curso).
    private static int s_panicInFlight;
    private static int s_parentPid;
    private static PanicForm? s_form;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll")]
    private static extern bool SetProcessWindowStation(IntPtr hWinSta);

    [DllImport("user32.dll")]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll")]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    // Diagnóstico mínimo: %APPDATA%\DeskQuadra\guardian.log (responde "a hotkey está viva?").
    private static void Log(string message)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskQuadra");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "guardian.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [PID {Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Log é best-effort e nunca interfere no watchdog/pânico.
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        int parentPid = 0;
        string fullArgs = string.Join(" ", args);
        foreach (var part in fullArgs.Split([' ', '='], StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out int parsedPid) && parsedPid > 0)
            {
                parentPid = parsedPid;
                break;
            }
        }

        if (parentPid == 0)
        {
            var active = Process.GetProcessesByName("DeskQuadra.UI.Wpf");
            if (active.Length > 0)
            {
                parentPid = active[0].Id;
            }
        }

        s_parentPid = parentPid;
        Log($"iniciado (pai={parentPid})");

        // Escuta via Form oculto com message loop real. Se falhar, watchdog puro
        // bloqueante (comportamento anterior preservado).
        try
        {
            ApplicationConfiguration.Initialize();
            s_form = new PanicForm();
            bool hotkeyRegistered = s_form.EnsureHotkey();
            Log(hotkeyRegistered
                ? "hotkey Ctrl+Shift+Alt+Q registrada"
                : "RegisterHotKey FALHOU (colisão?): watchdog puro, sem hotkey de pânico");

            // Espera pelo pai em background; quando o pai morrer SOZINHO, restaura e sai junto.
            var watcher = new Thread(WatchParentAndQuit) { IsBackground = true };
            watcher.Start();

            Application.Run(s_form);
            Log("encerrando");
        }
        catch (Exception ex)
        {
            Log($"message loop indisponível ({ex.GetType().Name}): watchdog puro, sem hotkey de pânico");
            WaitForParentExit(s_parentPid);
            RestoreDesktopIconsIfNoInstances();
        }
    }

    // Espera pelo pai em background (lógica de espera anterior, inalterada); ao fim restaura os
    // ícones e fecha o form para o Guardian sair junto (comportamento anterior preservado).
    private static void WatchParentAndQuit()
    {
        WaitForParentExit(s_parentPid);
        RestoreDesktopIconsIfNoInstances();
        try
        {
            s_form?.BeginInvoke(new Action(() => s_form.Close()));
        }
        catch
        {
            // Form já descartado: o loop já encerrou ou vai encerrar.
        }
    }

    // Espera bloqueante pelo término do pai: handle (SYNCHRONIZE) ou polling por nome no fallback.
    private static void WaitForParentExit(int parentPid)
    {
        if (parentPid > 0)
        {
            IntPtr hProcess = OpenProcess(SYNCHRONIZE, false, parentPid);

            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    WaitForSingleObject(hProcess, INFINITE);
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            else
            {
                while (true)
                {
                    Thread.Sleep(500);
                    var active = Process.GetProcessesByName("DeskQuadra.UI.Wpf");
                    if (active.Length == 0)
                    {
                        break;
                    }
                }
            }
        }
    }

    // Sequência de pânico (background, ao WM_HOTKEY): gracioso-primeiro-depois-matador.
    private static void RunPanicSequence()
    {
        Log("pânico disparado via hotkey");

        // 1. Pedido de saída limpa: sinaliza o evento que o app escuta (best-effort).
        try
        {
            using var gracefulExit = EventWaitHandle.OpenExisting(GracefulExitEventName);
            gracefulExit.Set();
        }
        catch
        {
            // App travado ou evento ausente: segue para o kill.
        }

        // 2. Aguarda o pai terminar até ~2500ms.
        bool exited = WaitForProcessExit(s_parentPid, PanicGracefulWaitMs);

        // 3. Se continuar vivo → Kill pelo PID conhecido (nunca por nome).
        if (!exited && s_parentPid > 0)
        {
            try
            {
                using var parent = Process.GetProcessById(s_parentPid);
                if (!parent.HasExited)
                {
                    parent.Kill();
                    parent.WaitForExit();
                }
            }
            catch
            {
                // PID já saiu ou acesso negado: segue para a restauração.
            }
        }

        // 4. Reaproveita a lógica existente (só restaura se nenhuma instância restante).
        RestoreDesktopIconsIfNoInstances();

        try
        {
            MessageBoxW(IntPtr.Zero, DeskQuadra.Guardian.Properties.Strings.EmergencyMessage, DeskQuadra.Guardian.Properties.Strings.EmergencyTitle, MB_OK | MB_ICONINFORMATION);
        }
        catch
        {
            // Notificação best-effort: nunca impede o encerramento.
        }

        try
        {
            s_form?.BeginInvoke(new Action(() => s_form.Close()));
        }
        catch
        {
            // Form já descartado: o loop já encerrou ou vai encerrar.
        }
    }

    private static bool WaitForProcessExit(int pid, int timeoutMs)
    {
        if (pid <= 0)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return process.WaitForExit(timeoutMs);
        }
        catch (ArgumentException)
        {
            // PID inexistente = processo já saiu.
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Lógica de restauração existente (inalterada): só restaura se nenhuma instância restante.
    private static void RestoreDesktopIconsIfNoInstances()
    {
        Thread.Sleep(150);

        try
        {
            var activeInstances = Process.GetProcessesByName("DeskQuadra.UI.Wpf");
            if (activeInstances.Length == 0)
            {
                var iconService = new NativeDesktopIconService();
                iconService.ShowDesktopIcons();
            }
        }
        catch
        {
            var iconService = new NativeDesktopIconService();
            iconService.ShowDesktopIcons();
        }
    }

    /// <summary>
    /// Janela invisível dedicada a receber WM_HOTKEY com message loop real.
    /// (Message-only via classe de sistema não serve: o WndProc seria do Windows, não nosso.)
    /// </summary>
    private sealed class PanicForm : Form
    {
        private bool _hotkeyRegistered;

        public PanicForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new System.Drawing.Point(-2000, -2000);
            Size = new System.Drawing.Size(0, 0);
        }

        // Nunca se mostra: Application.Run exige um Form, não uma janela visível.
        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

        public bool EnsureHotkey()
        {
            try
            {
                _hotkeyRegistered = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_ALT, VK_Q);
            }
            catch
            {
                _hotkeyRegistered = false;
            }

            return _hotkeyRegistered;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                // Debounce: ignora novos WM_HOTKEY com pânico já em curso.
                if (Interlocked.CompareExchange(ref s_panicInFlight, 1, 0) == 0)
                {
                    ThreadPool.QueueUserWorkItem(_ => RunPanicSequence());
                }

                return;
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (_hotkeyRegistered)
            {
                try
                {
                    UnregisterHotKey(Handle, HOTKEY_ID);
                }
                catch
                {
                    // Best-effort na saída.
                }

                _hotkeyRegistered = false;
            }

            base.Dispose(disposing);
        }
    }
}
