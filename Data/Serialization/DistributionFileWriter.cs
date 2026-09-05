using Data.Comments;
using Data.Data;

namespace Data.Serialization;

/// <summary>
/// Orchestrates saving modified distributions back to their source Lua files.
/// Groups by SourceFile, skips clean groups, creates .bak backups, writes full-file
/// rewrites via LuaWriter.
///
/// Additionally writes any Distribution_*.lua reference files whose data was modified
/// (junk/bags from ClutterTables.* or BagsAndContainers.*).
///
/// The injected <see cref="IFileWriter"/> is the ownership boundary for the final path. Direct
/// folder editing uses <see cref="DiskFileWriter"/>; project mode supplies a writer that remaps
/// reference provenance into the writable project root before any backup/write effect occurs.
/// </summary>
public sealed class DistributionFileWriter
{
    private readonly IFileWriter _fileWriter;

    public DistributionFileWriter(IFileWriter? fileWriter = null)
    {
        _fileWriter = fileWriter ?? new DiskFileWriter();
    }

    /// <summary>
    /// Saves dirty distributions back through the configured writer. <paramref name="forceSourceFiles"/>
    /// lets a layered/project caller rewrite a source file after its final override was removed; an
    /// empty distribution list is therefore meaningful and serializes as an empty valid table.
    /// Also writes Distribution_*.lua reference files for any dirty referenced junk/bags.
    /// Returns the final paths requested from the configured writer.
    /// </summary>
    public IReadOnlyList<string> Save(
        IReadOnlyList<Distribution> allDistributions,
        CommentMap? procComments = null,
        CommentMap? distComments = null,
        IReadOnlyCollection<string>? forceSourceFiles = null)
    {
        var written = new List<string>();

        // ── 1. Write main distribution files (ProceduralDistributions.lua / Distributions.lua) ──

        var groups = allDistributions
            .Where(d => !string.IsNullOrEmpty(d.SourceFile))
            .GroupBy(d => d.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var forced = forceSourceFiles is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(forceSourceFiles, StringComparer.OrdinalIgnoreCase);
        var sourceFiles = new HashSet<string>(groups.Keys, StringComparer.OrdinalIgnoreCase);
        sourceFiles.UnionWith(forced);

        foreach (var sourceFile in sourceFiles)
        {
            var dists = groups.TryGetValue(sourceFile, out var group)
                ? group
                : [];

            if (!forced.Contains(sourceFile) && !HasDirtyContent(dists))
                continue;

            bool isProcedural = dists.Any(d => d.Type == DistributionType.Procedural)
                || string.Equals(
                    Path.GetFileName(sourceFile),
                    "ProceduralDistributions.lua",
                    StringComparison.OrdinalIgnoreCase);

            string content = isProcedural
                ? LuaWriter.WriteProceduralFile(dists, procComments)
                : LuaWriter.WriteDistributionsFile(dists, distComments);

            _fileWriter.Backup(sourceFile);
            _fileWriter.WriteAllText(sourceFile, content);

            ClearDirtyFlags(dists);
            written.Add(sourceFile);
        }

        // ── 2. Write Distribution_*.lua reference files for dirty referenced data ──

        // Collect all dirty ItemParent objects that have a junk reference,
        // and all dirty Containers that have a source reference.
        // Group by reference file path so each file is written once.
        var refFileWrites = new Dictionary<string, (string refPath, ItemParent source)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var dist in allDistributions)
        {
            CollectDirtyRefs(dist, refFileWrites);
            foreach (var c in dist.Containers)
                CollectDirtyRefs(c, refFileWrites);
        }

        foreach (var (refFile, (refPath, source)) in refFileWrites)
        {
            string content = LuaWriter.WriteReferenceFileContent(refPath, source);
            _fileWriter.Backup(refFile);
            _fileWriter.WriteAllText(refFile, content);
            written.Add(refFile);
        }

        return written;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void CollectDirtyRefs(
        ItemParent parent,
        Dictionary<string, (string, ItemParent)> refFileWrites)
    {
        if (parent.IsDirty &&
            parent.JunkReference     is not null &&
            parent.JunkReferenceFile is not null)
        {
            // Last dirty writer for a given file wins (they should all have the same data
            // since they share the same ClutterTables entry in the loaded game).
            refFileWrites[parent.JunkReferenceFile] = (parent.JunkReference, parent);
        }

        if (parent is Container c &&
            c.IsDirty &&
            c.SourceReference     is not null &&
            c.SourceReferenceFile is not null)
        {
            refFileWrites[c.SourceReferenceFile] = (c.SourceReference, c);
        }
    }

    private static void ClearDirtyFlags(IEnumerable<Distribution> dists)
    {
        foreach (var d in dists)
        {
            d.IsDirty = false;
            foreach (var c in d.Containers)
            {
                c.IsDirty = false;
                foreach (var p in c.ProcListEntries)
                    p.IsDirty = false;
            }
        }
    }

    private static bool HasDirtyContent(IEnumerable<Distribution> dists)
    {
        foreach (var d in dists)
        {
            if (d.IsDirty) return true;
            foreach (var c in d.Containers)
            {
                if (c.IsDirty) return true;
                foreach (var p in c.ProcListEntries)
                    if (p.IsDirty) return true;
            }
        }
        return false;
    }
}
