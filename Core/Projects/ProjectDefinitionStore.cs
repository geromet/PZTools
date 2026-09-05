using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Projects;

/// <summary>
/// Persists project identity inside the writable project root. Metadata writes use a same-directory
/// temporary file followed by replacement so a crash cannot leave a partially-written definition.
/// </summary>
public static class ProjectDefinitionStore
{
    public const string MetadataDirectoryName = ".pztools";
    public const string FileName = "project.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetDefinitionPath(string projectRoot) =>
        Path.Combine(ProjectPathRules.Normalize(projectRoot), MetadataDirectoryName, FileName);

    public static void Save(ProjectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var normalized = ProjectDefinition.Create(definition.Name, definition.GameRoot, definition.ProjectRoot);
        if (definition.SchemaVersion != ProjectDefinition.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported project schema version {definition.SchemaVersion}.");

        Directory.CreateDirectory(normalized.ProjectRoot);
        var metadataDirectory = Path.Combine(normalized.ProjectRoot, MetadataDirectoryName);
        Directory.CreateDirectory(metadataDirectory);

        var targetPath = Path.Combine(metadataDirectory, FileName);
        var temporaryPath = Path.Combine(metadataDirectory, $".{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static ProjectDefinition Load(string projectRoot)
    {
        var requestedProjectRoot = ProjectPathRules.Normalize(projectRoot);
        var definitionPath = GetDefinitionPath(requestedProjectRoot);

        if (!File.Exists(definitionPath))
            throw new FileNotFoundException("PZTools project definition was not found.", definitionPath);

        ProjectDefinition? persisted;
        try
        {
            persisted = JsonSerializer.Deserialize<ProjectDefinition>(File.ReadAllText(definitionPath), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("PZTools project definition is not valid JSON.", ex);
        }

        if (persisted is null)
            throw new InvalidDataException("PZTools project definition is empty.");
        if (persisted.SchemaVersion != ProjectDefinition.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported project schema version {persisted.SchemaVersion}.");

        var normalized = ProjectDefinition.Create(persisted.Name, persisted.GameRoot, persisted.ProjectRoot);
        if (!ProjectPathRules.PathsEqual(normalized.ProjectRoot, requestedProjectRoot))
            throw new InvalidDataException("Project definition root does not match the directory it was opened from.");

        return normalized;
    }
}
