using Data.Data;

namespace Core.Projects;

public enum DistributionLayer
{
    Reference,
    Project,
}

public sealed record ProjectReferenceProvenance(
    string LayerName,
    string Root,
    int PreviewOrder,
    Distribution Distribution);

/// <summary>
/// One effective project entry with explicit provenance. Reference remains available underneath a
/// project override so removing the override reveals the current configured reference winner.
/// </summary>
public sealed record ProjectDistributionEntry(
    Distribution Effective,
    DistributionLayer Layer,
    Distribution? Reference)
{
    public ProjectReferenceProvenance? ReferenceProvenance { get; init; }
    public IReadOnlyList<ProjectReferenceProvenance> ShadowedReferences { get; init; } = [];
}

/// <summary>
/// Thin project-layer projection over the existing distribution model. It does not introduce a
/// second parser/AST: game, selected reference layers, and project distributions use the same Data
/// model and existing type/name identity. Selected reference layers are read-only; their persisted
/// preview order decides precedence and shadowed provenance is retained for conflict inspection.
/// </summary>
public sealed class ProjectDistributionWorkspace
{
    private readonly Dictionary<DistributionKey, Distribution> _references = [];
    private readonly Dictionary<DistributionKey, List<ProjectReferenceProvenance>> _referenceHistory = [];
    private readonly Dictionary<DistributionKey, Distribution> _project = [];
    private readonly HashSet<string> _removedSourceFiles = new(StringComparer.OrdinalIgnoreCase);

    public ProjectDefinition Project { get; }

    public ProjectDistributionWorkspace(
        ProjectDefinition project,
        IEnumerable<Distribution> referenceDistributions,
        IEnumerable<Distribution>? projectDistributions = null)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        ArgumentNullException.ThrowIfNull(referenceDistributions);

        var orderedReferences = referenceDistributions
            .Select(distribution => (Distribution: distribution, Provenance: ResolveReferenceProvenance(distribution)))
            .OrderBy(pair => pair.Provenance.PreviewOrder)
            .ThenBy(pair => pair.Distribution.OriginalOrder)
            .ToList();

        foreach (var pair in orderedReferences)
        {
            var key = Key(pair.Distribution);
            if (!_referenceHistory.TryGetValue(key, out var history))
            {
                history = [];
                _referenceHistory.Add(key, history);
            }

            history.Add(pair.Provenance);
            _references[key] = pair.Distribution;
        }

        if (projectDistributions is null)
            return;

        foreach (var distribution in projectDistributions)
        {
            ValidateProjectSource(distribution);
            _project[Key(distribution)] = distribution;
        }
    }

    public IReadOnlyList<ProjectDistributionEntry> Entries
    {
        get
        {
            var entries = new List<ProjectDistributionEntry>(_references.Count + _project.Count);
            var seen = new HashSet<DistributionKey>();

            foreach (var reference in _references.Values.OrderBy(d => d.OriginalOrder))
            {
                var key = Key(reference);
                seen.Add(key);
                entries.Add(CreateEntry(key, reference));
            }

            foreach (var projectOnly in _project.Where(pair => !seen.Contains(pair.Key)).Select(pair => pair.Value))
                entries.Add(new ProjectDistributionEntry(projectOnly, DistributionLayer.Project, null));

            return entries;
        }
    }

    public IReadOnlyList<Distribution> ProjectOverrides => _project.Values.ToList();

    internal IReadOnlyCollection<string> RemovedSourceFiles => _removedSourceFiles;

    public ProjectDistributionEntry Get(DistributionType type, string name)
    {
        var key = new DistributionKey(type, name);
        if (_references.TryGetValue(key, out var reference))
            return CreateEntry(key, reference);

        if (_project.TryGetValue(key, out var projectOnly))
            return new ProjectDistributionEntry(projectOnly, DistributionLayer.Project, null);

        throw new KeyNotFoundException($"Distribution not found: {type}/{name}");
    }

    /// <summary>
    /// Returns the writable project object. A game reference is cloned exactly once; subsequent
    /// edits reuse that project override. Selected imported reference layers remain deliberately
    /// read-only until their project-output aggregation/routing seam is implemented, preventing two
    /// mod roots with the same relative source file from silently overwriting one project file.
    /// </summary>
    public Distribution Edit(DistributionType type, string name)
    {
        var key = new DistributionKey(type, name);
        if (_project.TryGetValue(key, out var existing))
            return existing;

        if (!_references.TryGetValue(key, out var reference))
            throw new KeyNotFoundException($"Reference distribution not found: {type}/{name}");

        var provenance = WinnerProvenance(key);
        if (provenance is not null && provenance.PreviewOrder >= 0)
        {
            throw new InvalidOperationException(
                $"Selected reference layer '{provenance.LayerName}' is read-only. " +
                "Create/edit routing for imported-layer overrides requires the project-owned aggregation seam.");
        }

        var projectOverride = Clone(reference);
        projectOverride.IsDirty = true;
        _project.Add(key, projectOverride);
        return projectOverride;
    }

    public void AddProjectDistribution(Distribution distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        ValidateProjectSource(distribution);
        distribution.IsDirty = true;
        _project[Key(distribution)] = distribution;
    }

    /// <summary>
    /// Removes only the writable layer. The configured reference winner is retained and immediately
    /// becomes effective again. The removed source is remembered so Save can rewrite the project-owned
    /// file even when that removal leaves zero overrides in it.
    /// </summary>
    public bool RemoveOverride(DistributionType type, string name)
    {
        var key = new DistributionKey(type, name);
        if (!_project.Remove(key, out var removed))
            return false;

        if (!string.IsNullOrWhiteSpace(removed.SourceFile))
            _removedSourceFiles.Add(removed.SourceFile);
        else if (_references.TryGetValue(key, out var reference) && !string.IsNullOrWhiteSpace(reference.SourceFile))
            _removedSourceFiles.Add(reference.SourceFile);

        return true;
    }

    internal void MarkSaved() => _removedSourceFiles.Clear();

    private ProjectDistributionEntry CreateEntry(DistributionKey key, Distribution reference)
    {
        var winner = WinnerProvenance(key);
        var shadowed = ShadowedProvenance(key);

        if (_project.TryGetValue(key, out var projectOverride))
        {
            return new ProjectDistributionEntry(projectOverride, DistributionLayer.Project, reference)
            {
                ReferenceProvenance = winner,
                ShadowedReferences = shadowed,
            };
        }

        return new ProjectDistributionEntry(reference, DistributionLayer.Reference, reference)
        {
            ReferenceProvenance = winner,
            ShadowedReferences = shadowed,
        };
    }

    private ProjectReferenceProvenance ResolveReferenceProvenance(Distribution distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        if (string.IsNullOrWhiteSpace(distribution.SourceFile))
            throw new InvalidOperationException($"Reference distribution '{distribution.Name}' has no source provenance.");

        if (ProjectPathRules.IsSameOrDescendant(distribution.SourceFile, Project.GameRoot))
            return new ProjectReferenceProvenance("Game", Project.GameRoot, -1, distribution);

        var layers = Project.ReferenceLayers ?? [];
        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            if (!ProjectPathRules.IsSameOrDescendant(distribution.SourceFile, layer.Root))
                continue;

            if (!layer.Enabled)
            {
                throw new InvalidOperationException(
                    $"Reference distribution '{distribution.Name}' came from disabled layer '{layer.Name}'.");
            }

            return new ProjectReferenceProvenance(layer.Name, layer.Root, index, distribution);
        }

        throw new InvalidOperationException(
            $"Reference distribution '{distribution.Name}' has source outside configured game/selected reference roots: {distribution.SourceFile}");
    }

    private void ValidateProjectSource(Distribution distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        if (string.IsNullOrWhiteSpace(distribution.SourceFile))
            throw new InvalidOperationException($"Project distribution '{distribution.Name}' has no source provenance.");

        if (!ProjectPathRules.IsSameOrDescendant(distribution.SourceFile, Project.ProjectRoot))
        {
            throw new InvalidOperationException(
                $"Project distribution '{distribution.Name}' has source outside its configured root: {distribution.SourceFile}");
        }
    }

    private ProjectReferenceProvenance? WinnerProvenance(DistributionKey key) =>
        _referenceHistory.TryGetValue(key, out var history) && history.Count > 0 ? history[^1] : null;

    private IReadOnlyList<ProjectReferenceProvenance> ShadowedProvenance(DistributionKey key) =>
        _referenceHistory.TryGetValue(key, out var history) && history.Count > 1
            ? history.Take(history.Count - 1).ToList()
            : [];

    private static DistributionKey Key(Distribution distribution) =>
        new(distribution.Type, distribution.Name);

    private static Distribution Clone(Distribution source)
    {
        var copy = new Distribution
        {
            Name = source.Name,
            Type = source.Type,
            IsShop = source.IsShop,
            IsWorn = source.IsWorn,
            DontSpawnAmmo = source.DontSpawnAmmo,
            MaxMap = source.MaxMap,
            StashChance = source.StashChance,
            SourceFile = source.SourceFile,
            OriginalOrder = source.OriginalOrder,
        };
        CopyItemParent(source, copy);

        foreach (var container in source.Containers)
        {
            var containerCopy = new Container
            {
                Name = container.Name,
                Procedural = container.Procedural,
                DontSpawnAmmo = container.DontSpawnAmmo,
                OnlyOne = container.OnlyOne,
                MaxMap = container.MaxMap,
                StashChance = container.StashChance,
                SourceReference = container.SourceReference,
                SourceReferenceFile = container.SourceReferenceFile,
            };
            CopyItemParent(container, containerCopy);

            foreach (var entry in container.ProcListEntries)
            {
                containerCopy.ProcListEntries.Add(new ProcListEntry
                {
                    Name = entry.Name,
                    Min = entry.Min,
                    Max = entry.Max,
                    WeightChance = entry.WeightChance,
                    ForceForTiles = entry.ForceForTiles,
                    ForceForRooms = entry.ForceForRooms,
                    ForceForZones = entry.ForceForZones,
                    ForceForItems = entry.ForceForItems,
                    ResolvedDistribution = entry.ResolvedDistribution,
                    IsDirty = false,
                });
            }

            copy.Containers.Add(containerCopy);
        }

        return copy;
    }

    private static void CopyItemParent(ItemParent source, ItemParent target)
    {
        target.ItemRolls = source.ItemRolls;
        target.JunkRolls = source.JunkRolls;
        target.FillRand = source.FillRand;
        target.IgnoreZombieDensity = source.IgnoreZombieDensity;
        target.JunkIgnoreZombieDensity = source.JunkIgnoreZombieDensity;
        target.JunkReference = source.JunkReference;
        target.JunkReferenceFile = source.JunkReferenceFile;
        target.JunkItemsReference = source.JunkItemsReference;
        target.ItemsReference = source.ItemsReference;
        target.BagsReference = source.BagsReference;
        target.BagsFileReference = source.BagsFileReference;
        target.ItemChances.AddRange(source.ItemChances);
        target.JunkChances.AddRange(source.JunkChances);
        target.IsDirty = false;
    }

    private readonly record struct DistributionKey(DistributionType Type, string Name);
}
