using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UI;

/// <summary>
/// Loads and saves explorer folder definitions from folders.json next to the executable.
/// Ships with defaults; user changes are saved back to the same file.
/// In debug builds, also writes back to the project source so the defaults stay current.
/// </summary>
public static class FolderSettings
{
    private const string FileName = "folders.json";

    private static readonly string RuntimePath = Path.Combine(
        AppContext.BaseDirectory, FileName);

    // Resolved once: walk up from bin dir to find the project-level folders.json.
    private static readonly string? ProjectPath = FindProjectPath();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static List<FolderDefinition> Load()
    {
        try
        {
            if (!File.Exists(RuntimePath))
                return [];

            var json = File.ReadAllText(RuntimePath);
            return JsonSerializer.Deserialize<List<FolderDefinition>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(List<FolderDefinition> folders)
    {
        try
        {
            var json = JsonSerializer.Serialize(folders, JsonOptions);
            File.WriteAllText(RuntimePath, json);

            // Also update the project source copy so defaults stay in sync.
            if (ProjectPath is not null)
                File.WriteAllText(ProjectPath, json);
        }
        catch
        {
            // Silently ignore write failures
        }
    }

    /// <summary>
    /// Walks up from the bin output directory looking for a parent that contains
    /// both a .csproj and folders.json — that's the project root.
    /// </summary>
    private static string? FindProjectPath()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir?.Parent is not null)
            {
                dir = dir.Parent;
                var candidate = Path.Combine(dir.FullName, FileName);
                if (File.Exists(candidate) && dir.GetFiles("*.csproj").Length > 0)
                {
                    // Don't return the same path we already write to.
                    if (!string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(RuntimePath),
                            StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }
        }
        catch
        {
            // Not critical — project sync is best-effort.
        }

        return null;
    }
}