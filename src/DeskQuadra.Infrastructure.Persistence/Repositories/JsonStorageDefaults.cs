using System;
using System.IO;
using System.Text.Json;

namespace DeskQuadra.Infrastructure.Persistence.Repositories;

/// <summary>
/// Defaults compartilhados do armazenamento JSON em %APPDATA%\DeskQuadra.
/// Unifica apenas o que tem comportamento idêntico nos 2 repos
/// (opções do serializador + resolução do diretório AppData).
/// Logs (anchor/guardian/chord-diag) ficam de fora: usam append de texto,
/// comportamento diferente — exceção legítima da regra REUSE.
/// </summary>
public static class JsonStorageDefaults
{
    /// <summary>
    /// Instância única compartilhada: indentado para inspeção manual,
    /// desserialização tolerante a maiúsculas/minúsculas.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Resolve o diretório de dados: customDir quando informado (testes),
    /// senão %APPDATA%\DeskQuadra.
    /// </summary>
    public static string GetAppDataDirectory(string? customDir) => customDir ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DeskQuadra");
}
