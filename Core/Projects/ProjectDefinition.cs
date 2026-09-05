namespace Core.Projects;

/// <summary>
/// Persisted identity for a PZTools authoring project. Reference/game data and writable project
/// output are deliberately separate roots. Additional reference layers are read-only and their list
/// order is the explicit editor preview order.
/// </summary>
public sealed record ProjectDefinition(
    int SchemaVersion,
    string Name,
    string GameRoot,
    string ProjectRoot,
    IReadOnlyList<ProjectReferenceLayerDefinition>? ReferenceLayers = null)
{
    public const int CurrentSchemaVersion = 1;

    public static ProjectDefinition Create(
        string name,
        string gameRoot,
        string projectRoot,
        IEnumerable<ProjectReferenceLayerDefinition>? referenceLayers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedGameRoot = ProjectPathRules.Normalize(gameRoot);
        var normalizedProjectRoot = ProjectPathRules.Normalize(projectRoot);

        if (!Directory.Exists(normalizedGameRoot))
            throw new DirectoryNotFoundException($"Game/reference root does not exist: {normalizedGameRoot}");

        ProjectPathRules.EnsureProjectOutsideGame(normalizedGameRoot, normalizedProjectRoot);

        var normalizedLayers = NormalizeReferenceLayers(
            normalizedGameRoot,
            normalizedProjectRoot,
            referenceLayers);

        return new ProjectDefinition(
            CurrentSchemaVersion,
            name.Trim(),
            normalizedGameRoot,
            normalizedProjectRoot,
            normalizedLayers);
    }

    private static IReadOnlyList<ProjectReferenceLayerDefinition> NormalizeReferenceLayers(
        string gameRoot,
        string projectRoot,
        IEnumerable<ProjectReferenceLayerDefinition>? referenceLayers)
    {
        if (referenceLayers is null)
            return Array.Empty<ProjectReferenceLayerDefinition>();

        var normalized = new List<ProjectReferenceLayerDefinition>();
        foreach (var layer in referenceLayers)
        {
            ArgumentNullException.ThrowIfNull(layer);
            ArgumentException.ThrowIfNullOrWhiteSpace(layer.Name);

            var root = ProjectPathRules.Normalize(layer.Root);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Reference layer root does not exist: {root}");

            if (ProjectPathRules.IsSameOrDescendant(root, gameRoot) ||
                ProjectPathRules.IsSameOrDescendant(gameRoot, root))
            {
                throw new ArgumentException(
                    $"Reference layer '{layer.Name}' must be separate from the configured game/reference root.",
                    nameof(referenceLayers));
            }

            if (ProjectPathRules.IsSameOrDescendant(root, projectRoot) ||
                ProjectPathRules.IsSameOrDescendant(projectRoot, root))
            {
                throw new ArgumentException(
                    $"Reference layer '{layer.Name}' must be separate from the writable project root.",
                    nameof(referenceLayers));
            }

            foreach (var existing in normalized)
            {
                if (ProjectPathRules.IsSameOrDescendant(root, existing.Root) ||
                    ProjectPathRules.IsSameOrDescendant(existing.Root, root))
                {
                    throw new ArgumentException(
                        $"Reference layer '{layer.Name}' overlaps reference layer '{existing.Name}'.",
                        nameof(referenceLayers));
                }
            }

            normalized.Add(new ProjectReferenceLayerDefinition(layer.Name.Trim(), root, layer.Enabled));
        }

        return normalized.Count == 0
            ? Array.Empty<ProjectReferenceLayerDefinition>()
            : normalized;
    }
}
