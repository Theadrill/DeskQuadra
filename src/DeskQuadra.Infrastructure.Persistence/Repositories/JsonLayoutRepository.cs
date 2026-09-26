using System.Text.Json;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.Persistence.Models;

namespace DeskQuadra.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositório transacional de layout com double-buffering (.tmp -> .bak -> .json),
/// garantindo resiliência a quedas de energia e arquivos corrompidos.
/// </summary>
public sealed class JsonLayoutRepository : ILayoutRepository
{
    private readonly string _storageDirectory;
    private readonly string _jsonFilePath;
    private readonly string _bakFilePath;
    private readonly string _tmpFilePath;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public JsonLayoutRepository(string? customStorageDirectory = null)
    {
        _storageDirectory = customStorageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeskQuadra");

        _jsonFilePath = Path.Combine(_storageDirectory, "quadras.json");
        _bakFilePath = Path.Combine(_storageDirectory, "quadras.json.bak");
        _tmpFilePath = Path.Combine(_storageDirectory, "quadras.json.tmp");
    }

    public async Task<IReadOnlyList<Quadra>> LoadLayoutAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_storageDirectory);

            // 1. Tentar carregar o arquivo principal quadras.json
            if (File.Exists(_jsonFilePath))
            {
                var mainResult = await TryReadFileAsync(_jsonFilePath, cancellationToken).ConfigureAwait(false);
                if (mainResult is not null)
                {
                    return mainResult;
                }
            }

            // 2. Se o principal não existir ou estiver corrompido, tentar o backup (.bak)
            if (File.Exists(_bakFilePath))
            {
                var bakResult = await TryReadFileAsync(_bakFilePath, cancellationToken).ConfigureAwait(false);
                if (bakResult is not null)
                {
                    // Auto-healing: restaura o .bak como .json principal
                    try
                    {
                        File.Copy(_bakFilePath, _jsonFilePath, overwrite: true);
                    }
                    catch
                    {
                        // Continua mesmo se a cópia falhar
                    }
                    return bakResult;
                }
            }

            // 3. Se houver um .tmp íntegro deixado por queda de energia no boot
            if (File.Exists(_tmpFilePath))
            {
                var tmpResult = await TryReadFileAsync(_tmpFilePath, cancellationToken).ConfigureAwait(false);
                if (tmpResult is not null)
                {
                    try
                    {
                        File.Copy(_tmpFilePath, _jsonFilePath, overwrite: true);
                        File.Delete(_tmpFilePath);
                    }
                    catch
                    {
                    }
                    return tmpResult;
                }
            }

            return Array.Empty<Quadra>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveLayoutAsync(IEnumerable<Quadra> quadras, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quadras);

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_storageDirectory);

            var dtos = quadras.Select(QuadraDto.FromDomain).ToList();

            // 1. Grava no arquivo temporário .tmp primeiro (Double-Buffering)
            using (var stream = new FileStream(
                _tmpFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, dtos, SerializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // 2. Rotação segura: se o json atual existir, atualiza o .bak
            if (File.Exists(_jsonFilePath))
            {
                File.Copy(_jsonFilePath, _bakFilePath, overwrite: true);
            }

            // 3. Substituição atômica: renomeia o .tmp para o .json definitivo
            File.Move(_tmpFilePath, _jsonFilePath, overwrite: true);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static async Task<IReadOnlyList<Quadra>?> TryReadFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var dtos = await JsonSerializer.DeserializeAsync<List<QuadraDto>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            if (dtos is null)
            {
                return null;
            }

            return dtos.Select(d => d.ToDomain()).ToList();
        }
        catch (JsonException)
        {
            // Arquivo corrompido ou truncado
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
