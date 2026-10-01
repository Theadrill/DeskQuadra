using System.IO;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.FileSystem;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Exclusão via Shell nativo (<c>SHFileOperation</c>, sem confirmação do Explorer).
/// <c>recycle: true</c> = Lixeira (com undo, <c>FOF_ALLOWUNDO</c>);
/// <c>false</c> = permanente (sem undo).
/// </summary>
public sealed class ShellFileDeletionService : IFileDeletionService
{
    public bool Delete(string filePath, bool recycle)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (!FileSystemUtils.PathExists(filePath))
        {
            return false;
        }

        try
        {
            ushort flags = (ushort)(NativeMethods.FOF_NOCONFIRMATION
                | NativeMethods.FOF_NOERRORUI
                | NativeMethods.FOF_SILENT
                | (recycle ? NativeMethods.FOF_ALLOWUNDO : 0));

            var op = new NativeMethods.SHFILEOPSTRUCT
            {
                wFunc = NativeMethods.FO_DELETE,
                // O marshaler termina a string com \0; o \0 embutido fecha a lista dupla-nula.
                pFrom = filePath + '\0',
                fFlags = flags
            };

            int result = NativeMethods.SHFileOperation(ref op);
            if (result != 0 || op.fAnyOperationsAborted)
            {
                return false;
            }

            return !FileSystemUtils.PathExists(filePath);
        }
        catch
        {
            return false;
        }
    }
}
