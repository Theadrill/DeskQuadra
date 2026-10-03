using System.IO;
using DeskQuadra.Core.FileDuplication;

namespace DeskQuadra.Core;

/// <summary>
/// Criação de nova pasta com resolução de nomes colidentes ("Nova pasta", "Nova pasta (2)", etc.).
/// REUSA o motor de nomes únicos <see cref="FileDuplicator.GetUniquePath"/>.
/// Lógica pura desacoplada de UI/WPF/timers, coberta por xUnit.
/// </summary>
public static class NewFolderCreator
{
    /// <summary>
    /// Calcula o caminho único para uma nova pasta no diretório indicado, evitando colisões.
    /// </summary>
    public static string GetUniqueFolderPath(
        string targetDirectory,
        string defaultName,
        string indexedFormat,
        Func<string, bool>? directoryExists = null)
    {
        ArgumentNullException.ThrowIfNull(targetDirectory);
        ArgumentNullException.ThrowIfNull(defaultName);
        ArgumentNullException.ThrowIfNull(indexedFormat);

        var exists = directoryExists ?? Directory.Exists;
        int copyIndex = 1;

        return FileDuplicator.GetUniquePath(
            targetDirectory,
            i => i == 1 ? defaultName : string.Format(indexedFormat, i),
            exists,
            ref copyIndex);
    }
}
