using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using DeskQuadra.Core.ThirdParty;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

/// <summary>
/// Executa a adição de arquivos a contêineres utilizando fallback em camadas (Fase F4):
/// 1. Formato .zip: manipulação in-box pura do .NET (System.IO.Compression).
/// 2. Formatos .7z, .tar, .gz, .tgz, .bz2, .xz: delegação ao 7-Zip (7zG.exe com GUI nativa ou 7z.exe).
/// 3. Formato .rar: delegação ao WinRAR (WinRAR.exe).
/// 4. Caso o compactador necessário não exista: reporta missingToolName para notificação ao usuário.
/// </summary>
public sealed class ArchiveFallbackHandler : IArchiveFallbackHandler
{
    private readonly IExternalArchiverLocator _locator;

    public ArchiveFallbackHandler()
        : this(new ExternalArchiverLocator())
    {
    }

    public ArchiveFallbackHandler(IExternalArchiverLocator locator)
    {
        _locator = locator;
    }

    public bool TryAddToArchive(string containerPath, IReadOnlyList<string> sourcePaths, out string? missingToolName)
    {
        missingToolName = null;

        if (string.IsNullOrWhiteSpace(containerPath) || sourcePaths == null || sourcePaths.Count == 0)
        {
            return false;
        }

        string ext = Path.GetExtension(containerPath).ToLowerInvariant();

        // 1. Formato .zip: tenta primeiramente o manipulador nativo do .NET (sem dependências externas)
        if (ext == ".zip")
        {
            if (TryAddToZipNative(containerPath, sourcePaths))
            {
                ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                    containerPath, "(drop-fallback)", (uint)sourcePaths.Count, "n/a", "n/a", "n/a", "ok-dotnet-zip"));
                return true;
            }
        }

        // 2. Formato .rar: requer WinRAR (único que suporta criação/atualização de RAR proprietário)
        if (ext == ".rar")
        {
            string? winRarExe = _locator.FindWinRar();
            if (string.IsNullOrEmpty(winRarExe))
            {
                missingToolName = "WinRAR";
                ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                    containerPath, "(drop-fallback)", (uint)sourcePaths.Count, "n/a", "n/a", "n/a", "missing-winrar"));
                return false;
            }

            return RunWinRar(winRarExe, containerPath, sourcePaths, out missingToolName);
        }

        // 3. Formatos 7-Zip (.7z, .tar, .gz, .tgz, .bz2, .xz e fallback de .zip)
        if (ext is ".7z" or ".tar" or ".gz" or ".tgz" or ".bz2" or ".xz" or ".zip")
        {
            string? sevenZipExe = _locator.FindSevenZip();
            if (!string.IsNullOrEmpty(sevenZipExe))
            {
                return RunSevenZip(sevenZipExe, containerPath, sourcePaths, out missingToolName);
            }

            // Se for .tar e não tiver 7-Zip, verifica se WinRAR está disponível
            if (ext == ".tar")
            {
                string? winRarExe = _locator.FindWinRar();
                if (!string.IsNullOrEmpty(winRarExe))
                {
                    return RunWinRar(winRarExe, containerPath, sourcePaths, out missingToolName);
                }
            }

            // Não encontrou compactador para o formato
            missingToolName = "7-Zip";
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                containerPath, "(drop-fallback)", (uint)sourcePaths.Count, "n/a", "n/a", "n/a", "missing-7zip"));
            return false;
        }

        return false;
    }

    private static bool TryAddToZipNative(string zipPath, IReadOnlyList<string> sourcePaths)
    {
        try
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
            foreach (var source in sourcePaths)
            {
                if (File.Exists(source))
                {
                    string entryName = Path.GetFileName(source);
                    archive.GetEntry(entryName)?.Delete();
                    archive.CreateEntryFromFile(source, entryName);
                }
                else if (Directory.Exists(source))
                {
                    AddDirectoryRecursively(archive, source, Path.GetFileName(source));
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                zipPath, "(drop-fallback-dotnet)", (uint)sourcePaths.Count, "n/a", "n/a", "n/a", $"ex-{ex.GetType().Name}"));
            return false;
        }
    }

    private static void AddDirectoryRecursively(ZipArchive archive, string sourceDir, string rootEntryName)
    {
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            string relPath = Path.GetRelativePath(sourceDir, file);
            string entryName = Path.Combine(rootEntryName, relPath).Replace('\\', '/');
            archive.GetEntry(entryName)?.Delete();
            archive.CreateEntryFromFile(file, entryName);
        }
    }

    private static bool RunSevenZip(
        string sevenZipExe,
        string containerPath,
        IReadOnlyList<string> sourcePaths,
        out string? missingToolName)
    {
        missingToolName = null;
        string? tempFileList = null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = sevenZipExe,
                UseShellExecute = false
            };

            bool isGui = sevenZipExe.EndsWith("7zG.exe", StringComparison.OrdinalIgnoreCase);
            psi.CreateNoWindow = !isGui;
            psi.WindowStyle = isGui ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;

            // Se a lista de argumentos for grande, cria arquivo de lista temporário em UTF-8
            long estimatedLen = containerPath.Length + sourcePaths.Sum(s => s.Length + 4);
            if (estimatedLen > 6000)
            {
                tempFileList = Path.Combine(Path.GetTempPath(), $"deskquadra_7z_{Guid.NewGuid():N}.txt");
                File.WriteAllLines(tempFileList, sourcePaths, Encoding.UTF8);
                psi.Arguments = $"a -y -scsUTF-8 -- \"{containerPath}\" @\"{tempFileList}\"";
            }
            else
            {
                var sb = new StringBuilder();
                sb.Append("a -y -scsUTF-8 -- ");
                sb.Append('"').Append(containerPath).Append("\" ");
                foreach (var src in sourcePaths)
                {
                    sb.Append('"').Append(src).Append("\" ");
                }
                psi.Arguments = sb.ToString().TrimEnd();
            }

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return false;
            }

            bool exited = proc.WaitForExit(120_000); // 2 minutos máximo
            if (!exited)
            {
                try { proc.Kill(); } catch { }
                ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                    containerPath, "(drop-fallback-7zip)", (uint)sourcePaths.Count, "n/a", "n/a", "n/a", "timeout"));
                return false;
            }

            bool ok = proc.ExitCode == 0;
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                containerPath, "(drop-fallback-7zip)", (uint)sourcePaths.Count, "n/a", "n/a", $"0x{proc.ExitCode:X8}", ok ? "ok" : "exit-failed"));
            return ok;
        }
        catch (Exception ex)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                containerPath, "(drop-fallback-7zip)", (uint)sourcePaths.Count, "n/a", "n/a", "n/a", $"ex-{ex.GetType().Name}"));
            return false;
        }
        finally
        {
            if (tempFileList != null && File.Exists(tempFileList))
            {
                try { File.Delete(tempFileList); } catch { }
            }
        }
    }

    private static bool RunWinRar(
        string winRarExe,
        string containerPath,
        IReadOnlyList<string> sourcePaths,
        out string? missingToolName)
    {
        missingToolName = null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = winRarExe,
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            var sb = new StringBuilder();
            sb.Append("a -ibck -y -- ");
            sb.Append('"').Append(containerPath).Append("\" ");
            foreach (var src in sourcePaths)
            {
                sb.Append('"').Append(src).Append("\" ");
            }
            psi.Arguments = sb.ToString().TrimEnd();

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return false;
            }

            bool exited = proc.WaitForExit(120_000);
            if (!exited)
            {
                try { proc.Kill(); } catch { }
                ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                    containerPath, "(drop-fallback-winrar)", (uint)sourcePaths.Count, "n/a", "n/a", "n/a", "timeout"));
                return false;
            }

            bool ok = proc.ExitCode == 0;
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                containerPath, "(drop-fallback-winrar)", (uint)sourcePaths.Count, "n/a", "n/a", $"0x{proc.ExitCode:X8}", ok ? "ok" : "exit-failed"));
            return ok;
        }
        catch (Exception ex)
        {
            ShellMenuLog.Log(ShellMenuLog.FormatInvoke(
                containerPath, "(drop-fallback-winrar)", (uint)sourcePaths.Count, "n/a", "n/a", "n/a", $"ex-{ex.GetType().Name}"));
            return false;
        }
    }
}
