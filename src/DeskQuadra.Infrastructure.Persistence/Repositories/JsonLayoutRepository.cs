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

            // 1. Tentar carregar o arquivo principal quadras.json (guarda resultado + validez para o Decider)
            IReadOnlyList<Quadra>? jsonResult = null;
            bool jsonExists = File.Exists(_jsonFilePath);
            bool jsonValid = false;
            if (jsonExists)
            {
                jsonResult = await TryReadFileAsync(_jsonFilePath, cancellationToken).ConfigureAwait(false);
                jsonValid = jsonResult is not null;
            }

            // 2. .tmp órfão de queda de energia avaliado ANTES do .bak (BRAINSTORMING 219-224).
            //    Ordem antiga (json -> bak -> tmp) fazia o .tmp nunca ser avaliado se o .bak existisse.
            if (File.Exists(_tmpFilePath))
            {
                var tmpResult = await TryReadFileAsync(_tmpFilePath, cancellationToken).ConfigureAwait(false);
                bool tmpValid = tmpResult is not null;
                DateTime tmpTime = SafeGetWriteTimeUtc(_tmpFilePath);
                DateTime jsonTime = jsonExists ? SafeGetWriteTimeUtc(_jsonFilePath) : DateTime.MinValue;

                var action = LayoutRecoveryDecider.Decide(
                    tmpExists: true, tmpValid: tmpValid, tmpWriteUtc: tmpTime,
                    jsonExists: jsonExists, jsonValid: jsonValid, jsonWriteUtc: jsonTime);

                if (action == LayoutRecoveryAction.PromoteTmpAuto && tmpResult is not null)
                {
                    // .json ausente/corrompido: auto-cura silenciosa sem diálogo.
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

                if (action == LayoutRecoveryAction.DiscardTmpKeepJson)
                {
                    // Truncado/corrompido OU obsoleto (<= .json): descarta silencioso, sem diálogo.
                    DeleteBestEffort(_tmpFilePath);
                    if (jsonResult is not null)
                    {
                        return jsonResult;
                    }
                    // Sem .json válido: cai para o .bak abaixo.
                }
                else if (action == LayoutRecoveryAction.PromptUser && jsonResult is not null)
                {
                    // O diálogo vive no boot da UI (App pré-checa e resolve antes do Initialize).
                    // Load direto sem pré-resolução: segue com .json e PRESERVA o .tmp para a UI resolver depois.
                    return jsonResult;
                }
                else if (tmpResult is not null && jsonResult is not null)
                {
                    return jsonResult;
                }
                else if (tmpResult is not null)
                {
                    return tmpResult;
                }
            }
            else if (jsonResult is not null)
            {
                return jsonResult;
            }

            // 3. Se o principal não existir ou estiver corrompido (e sem .tmp aproveitável), tentar o backup (.bak)
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

    /// <summary>
    /// Pré-checagem de boot (UI): retorna oferta somente no caso PromptUser
    /// (.tmp íntegro E mais recente que .json válido). Não muta arquivos.
    /// .tmp corrompido é descartado aqui mesmo, silencioso e sem diálogo.
    /// </summary>
    public async Task<PendingLayoutRecovery?> CheckCrashRecoveryAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_tmpFilePath))
            {
                return null;
            }

            var tmpResult = await TryReadFileAsync(_tmpFilePath, cancellationToken).ConfigureAwait(false);
            if (tmpResult is null)
            {
                // Truncado/corrompido: descarta silencioso, sem diálogo.
                DeleteBestEffort(_tmpFilePath);
                return null;
            }

            bool jsonExists = File.Exists(_jsonFilePath);
            var jsonResult = jsonExists
                ? await TryReadFileAsync(_jsonFilePath, cancellationToken).ConfigureAwait(false)
                : null;

            var action = LayoutRecoveryDecider.Decide(
                tmpExists: true, tmpValid: true, tmpWriteUtc: SafeGetWriteTimeUtc(_tmpFilePath),
                jsonExists: jsonExists, jsonValid: jsonResult is not null,
                jsonWriteUtc: jsonExists ? SafeGetWriteTimeUtc(_jsonFilePath) : DateTime.MinValue);

            if (action != LayoutRecoveryAction.PromptUser)
            {
                return null;
            }

            return new PendingLayoutRecovery(SafeGetWriteTimeUtc(_tmpFilePath), SafeGetWriteTimeUtc(_jsonFilePath));
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// true = Restaurar Recente (promove .tmp a .json mantendo .bak de segurança);
    /// false = Manter Anterior (segue com .json, descarta .tmp). No-op se sem .tmp.
    /// </summary>
    public async Task ResolveCrashRecoveryAsync(bool restoreRecent, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_tmpFilePath))
            {
                return;
            }

            if (!restoreRecent)
            {
                DeleteBestEffort(_tmpFilePath);
                return;
            }

            try
            {
                // .bak de segurança: preserva o anterior antes de promover o recente.
                if (File.Exists(_jsonFilePath))
                {
                    File.Copy(_jsonFilePath, _bakFilePath, overwrite: true);
                }
                File.Copy(_tmpFilePath, _jsonFilePath, overwrite: true);
                File.Delete(_tmpFilePath);
            }
            catch
            {
                // Best-effort: o Load seguinte resolve (json -> bak -> vazio).
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static DateTime SafeGetWriteTimeUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static void DeleteBestEffort(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
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
