using Data.Data;

namespace Core.Projects;

public enum DistributionLayer
{
    Reference,
    Project,
}

/// <summary>
/// One effective project entry with explicit provenance. Reference remains available underneath a
/// project override so removing the override reveals the original definition immediately.
/// </summary>
public sealed record ProjectDistributionEntry(
    Distribution Effective,
    DistributionLayer Layer,
    Distribution? Reference);

/// <summary>
/// Thin project-layer projection over the existing distribution model. It does not introduce a
/// second parser/AST: reference and project distributions are the same Data model, indexed by their
/// existing type/name identity. Editing a reference deep-clones it into the project layer so the
/// read-only instance can never be mutated by project-mode UI code.
/// </summary>
public sealed class ProjectDistributionWorkspace
{
    private readonly Dictionary<DistributionKey, Distribution> _references = [];
    private readonly Dictionary<DistributionKey, Distribution> _project = [];
    private readonly HashSet<string> _removedSourceFiles = new(StringComparer.OrdinalIgnoreCase);

    public ProjectDefinition Project { get; }

    public ProjectDistributionWorkspace(
        ProjectDefinition project,
        IEnumerable<Distribution> referenceDistributions,
        IEnumerable<Distribution>? projectDistributions = null)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));

        foreach (var distribution in referenceDistributions)
        {
            ValidateSource(distribution, DistributionLayer.Reference);
            _references[Key(distribution)] = distribution;
        }

        if (projectDistributions is null)
            return;

        foreach (var distribution in projectDistributions)
        {
            ValidateSource(distribution, DistributionLayer.Project);
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
                if (_project.TryGetValue(key, out var projectOverride))
                    entries.Add(new ProjectDistributionEntry(projectOverride, DistributionLayer.Project, reference));
                else
                    entries.Add(new ProjectDistributionEntry(reference, DistributionLayer.Reference, reference));
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
        if (_project.TryGetValue(key, out var projectOverride))
        {
            _references.TryGetValue(key, out var reference);
            return new ProjectDistributionEntry(projectOverride, DistributionLayer.Project, reference);
        }

        if (_references.TryGetValue(key, out var referenceOnly))
            return new ProjectDistributionEntry(referenceOnly, DistributionLayer.Reference, referenceOnly);

        throw new KeyNotFoundException($"Distribution not found: {type}/{name}");
    }

    /// <summary>
    /// Returns the writable project object. A read-only reference is cloned exactly once; subsequent
    /// edits reuse that project override. The clone retains SourceFile as provenance only—the
    /// ProjectRoutedFileWriter derives the actual writable destination from project ownership.
    /// </summary>
    public Distribution Edit(DistributionType type, string name)
    {
        var key = new DistributionKey(type, name);
        if (_project.TryGetValue(key, out var existing))
            return existing;

        if (!_references.TryGetValue(key, out var reference))
            throw new KeyNotFoundException($"Reference distribution not found: {type}/{name}");

        var projectOverride = Clone(reference);
        projectOverride.IsDirty = true;
        _project.Add(key, projectOverride);
        return projectOverride;
    }

    public void AddProjectDistribution(Distribution distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        ValidateSource(distribution, DistributionLayer.Project);
        distribution.IsDirty = true;
        _project[Key(distribution)] = distribution;
    }

    /// <summary>
    /// Removes only the writable layer. The reference object is retained and immediately becomes
    /// effective again. The removed source is remembered so Save can rewrite the project-owned file
    /// even when that removal leaves zero overrides in it.
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

    private void ValidateSource(Distribution distribution, DistributionLayer layer)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        if (string.IsNullOrWhiteSpace(distribution.SourceFile))
            throw new InvalidOperationException($"{layer} distribution '{distribution.Name}' has no source provenance.");

        var root = layer == DistributionLayer.Reference ? Project.GameRoot : Project.ProjectRoot;
        if (!ProjectPathRules.IsSameOrDescendant(distribution.SourceFile, root))
        {
            throw new InvalidOperationException(
                $"{layer} distribution '{distribution.Name}' has source outside its configured root: {distribution.SourceFile}");
        }
    }

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
