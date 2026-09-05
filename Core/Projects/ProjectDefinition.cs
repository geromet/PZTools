namespace Core.Projects;

/// <summary>
/// Persisted identity for a PZTools authoring project. Reference/game data and writable project
/// output are deliberately separate roots; later layering/import work builds on this boundary.
/// </summary>
public sealed record ProjectDefinition(
    int SchemaVersion,
    string Name,
    string GameRoot,
    string ProjectRoot)
{
    public const int CurrentSchemaVersion = 1;

    public static ProjectDefinition Create(string name, string gameRoot, string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedGameRoot = ProjectPathRules.Normalize(gameRoot);
        var normalizedProjectRoot = ProjectPathRules.Normalize(projectRoot);
        ProjectPathRules.EnsureProjectOutsideGame(normalizedGameRoot, normalizedProjectRoot);

        return new ProjectDefinition(
            CurrentSchemaVersion,
            name.Trim(),
            normalizedGameRoot,
            normalizedProjectRoot);
    }
}
