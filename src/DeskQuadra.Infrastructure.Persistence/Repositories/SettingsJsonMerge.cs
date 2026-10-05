using System.Text.Json;

namespace DeskQuadra.Infrastructure.Persistence.Repositories;

/// <summary>
/// Merge best-effort do settings.json compartilhado (densidade + visual + snap).
/// Extração da 3ª repetição do padrão merge-dict (auditoria D7): comportamento idêntico
/// nos 3 serviços — lê o dict existente, aplica os campos do chamador, grava via .tmp.
/// </summary>
internal static class SettingsJsonMerge
{
    public static Dictionary<string, JsonElement>? ReadAll(string settingsFilePath)
    {
        try
        {
            if (!File.Exists(settingsFilePath))
            {
                return null;
            }

            string json = File.ReadAllText(settingsFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonStorageDefaults.SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void WriteMerge(string settingsFilePath, Action<Dictionary<string, object>> fill)
    {
        try
        {
            string? dir = Path.GetDirectoryName(settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var dict = new Dictionary<string, object>();
            var existing = ReadAll(settingsFilePath);
            if (existing != null)
            {
                foreach (var kvp in existing)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }

            fill(dict);

            string json = JsonSerializer.Serialize(dict, JsonStorageDefaults.SerializerOptions);
            string tmp = settingsFilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, settingsFilePath, overwrite: true);
        }
        catch
        {
            // Best-effort: estado segue em memória.
        }
    }
}
