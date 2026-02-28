using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UI;

/// <summary>
/// Persists user preferences to %APPDATA%/PZTools/settings.json.
/// </summary>
public class UserSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PZTools");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string? LastFolder { get; set; }
    public List<FolderDefinition>? Folders { get; set; }

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new UserSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently ignore write failures (read-only FS, permissions, etc.)
        }
    }
}

public class FolderDefinition
{
    public string Name { get; set; } = "";
    public List<string> DistributionNames { get; set; } = [];
    public List<FolderDefinition>? Children { get; set; }
    public bool IsExpanded { get; set; } = true;
}
