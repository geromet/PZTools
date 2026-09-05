using System.Text;
using Data;
using Data.Data;
using Data.Serialization;

namespace Core.Projects;

/// <summary>
/// Creates and validates the minimal project-owned distribution skeleton. The skeleton is generated
/// through the existing LuaWriter so project creation cannot drift into a second distribution format.
/// </summary>
public static class ProjectScaffold
{
    public static readonly string ItemsRelativePath =
        Path.Combine("media", "lua", "server", "Items");

    public static readonly string DistributionsRelativePath =
        Path.Combine(ItemsRelativePath, "Distributions.lua");

    public static readonly string ProceduralDistributionsRelativePath =
        Path.Combine(ItemsRelativePath, "ProceduralDistributions.lua");

    public static ProjectDefinition Create(string name, string gameRoot, string projectRoot)
    {
        var definition = ProjectDefinition.Create(name, gameRoot, projectRoot);
        var definitionPath = ProjectDefinitionStore.GetDefinitionPath(definition.ProjectRoot);

        if (File.Exists(definitionPath))
        {
            var existing = Open(definition.ProjectRoot);
            if (!ProjectPathRules.PathsEqual(existing.GameRoot, definition.GameRoot) ||
                !string.Equals(existing.Name, definition.Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "An existing PZTools project in this directory has different project identity metadata.");
            }

            return existing;
        }

        if (Directory.Exists(definition.ProjectRoot) &&
            Directory.EnumerateFileSystemEntries(definition.ProjectRoot).Any())
        {
            throw new InvalidDataException(
                "Project root must be empty when creating a new PZTools project.");
        }

        Directory.CreateDirectory(definition.ProjectRoot);

        var itemsDirectory = Path.Combine(definition.ProjectRoot, ItemsRelativePath);
        var distributionsPath = Path.Combine(definition.ProjectRoot, DistributionsRelativePath);
        var proceduralPath = Path.Combine(definition.ProjectRoot, ProceduralDistributionsRelativePath);

        // Re-resolve immediately before effects so an existing symlink/reparse component cannot
        // route scaffolding outside the writable project root or back into the game installation.
        ProjectPathRules.EnsureInsideProject(definition.ProjectRoot, itemsDirectory);
        ProjectPathRules.EnsureInsideProject(definition.ProjectRoot, distributionsPath);
        ProjectPathRules.EnsureInsideProject(definition.ProjectRoot, proceduralPath);
        ProjectPathRules.EnsureProjectOutsideGame(definition.GameRoot, itemsDirectory);

        Directory.CreateDirectory(itemsDirectory);
        WriteNewFileAtomically(
            distributionsPath,
            LuaWriter.WriteDistributionsFile(Array.Empty<Distribution>()));
        WriteNewFileAtomically(
            proceduralPath,
            LuaWriter.WriteProceduralFile(Array.Empty<Distribution>()));

        // Metadata is written last and acts as the durable marker that project creation completed.
        ProjectDefinitionStore.Save(definition);
        return Open(definition.ProjectRoot);
    }

    public static ProjectDefinition Open(string projectRoot)
    {
        var definition = ProjectDefinitionStore.Load(projectRoot);
        var distributionsPath = Path.Combine(definition.ProjectRoot, DistributionsRelativePath);
        var proceduralPath = Path.Combine(definition.ProjectRoot, ProceduralDistributionsRelativePath);

        ValidateRequiredFile(definition, distributionsPath, "Distributions.lua");
        ValidateRequiredFile(definition, proceduralPath, "ProceduralDistributions.lua");

        var parseResult = DistributionParser.CreateDefault().Parse(definition.ProjectRoot);
        if (parseResult.HasFatalErrors)
        {
            var details = string.Join(
                Environment.NewLine,
                parseResult.Errors.Where(error => error.IsFatal).Select(error => error.ToString()));
            throw new InvalidDataException(
                $"Project distribution scaffold is not valid Lua.{Environment.NewLine}{details}");
        }

        return definition;
    }

    private static void ValidateRequiredFile(
        ProjectDefinition definition,
        string path,
        string displayName)
    {
        ProjectPathRules.EnsureInsideProject(definition.ProjectRoot, path);
        ProjectPathRules.EnsureProjectOutsideGame(definition.GameRoot, path);

        if (!File.Exists(path))
        {
            throw new InvalidDataException(
                $"Project is missing required project-owned file '{displayName}' at '{path}'.");
        }
    }

    private static void WriteNewFileAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"Project scaffold path has no parent directory: {targetPath}");
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, targetPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
