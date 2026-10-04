using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using DeskQuadra.Core.Contracts;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Implementação das operações de área de transferência (Clipboard) do Windows Shell.
/// Configura o formato nativo FileDropList e o stream "Preferred DropEffect" para operações de Recortar (Cut).
/// Possui proteção de retry resiliente contra travas temporárias de outros processos no Clipboard.
/// </summary>
public sealed class WindowsClipboardService : IClipboardService
{
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 40;

    public bool SetFileDropList(IEnumerable<string> filePaths, bool isCut = false)
    {
        if (filePaths == null)
        {
            return false;
        }

        var list = new StringCollection();
        foreach (string path in filePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                list.Add(path);
            }
        }

        if (list.Count == 0)
        {
            return false;
        }

        var data = new DataObject();
        data.SetFileDropList(list);

        if (isCut)
        {
            // No Windows Shell: 2 = DROPEFFECT_MOVE (Recortar), 1 = DROPEFFECT_COPY (Copiar)
            byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
            var stream = new MemoryStream(moveEffect);
            data.SetData("Preferred DropEffect", stream);
        }

        for (int i = 0; i < MaxRetries; i++)
        {
            try
            {
                Clipboard.SetDataObject(data, true);
                return true;
            }
            catch (COMException)
            {
                if (i == MaxRetries - 1)
                {
                    return false;
                }
                Thread.Sleep(RetryDelayMs);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public IReadOnlyList<string> GetFileDropList()
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var collection = Clipboard.GetFileDropList();
                var result = new List<string>(collection.Count);
                foreach (string? path in collection)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        result.Add(path);
                    }
                }
                return result;
            }
        }
        catch
        {
            // Silencioso
        }

        return Array.Empty<string>();
    }

    public bool ContainsFileDropList()
    {
        try
        {
            return Clipboard.ContainsFileDropList();
        }
        catch
        {
            return false;
        }
    }
}
