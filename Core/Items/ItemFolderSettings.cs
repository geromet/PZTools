using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Items;

public static class ItemFolderSettings
{
    private const string FileName = "itemFolders.json";

    private static readonly string RuntimePath = Path.Combine(
        AppContext.BaseDirectory, FileName);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static List<ItemFolderDefinition> Load()
    {
        try
        {
            if (!File.Exists(RuntimePath))
                return [];

            var json = File.ReadAllText(RuntimePath);
            return JsonSerializer.Deserialize<List<ItemFolderDefinition>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(List<ItemFolderDefinition> folders)
    {
        try
        {
            var json = JsonSerializer.Serialize(folders, JsonOptions);
            File.WriteAllText(RuntimePath, json);
        }
        catch
        {
            // Silently ignore write failures
        }
    }
}
