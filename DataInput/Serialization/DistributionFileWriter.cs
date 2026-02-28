using DataInput.Comments;
using DataInput.Data;

namespace DataInput.Serialization;

/// <summary>
/// Orchestrates saving modified distributions back to their source Lua files.
/// Groups by SourceFile, skips clean groups, creates .bak backups, writes full-file
/// rewrites via LuaWriter.
///
/// Additionally writes any Distribution_*.lua reference files whose data was modified
/// (junk/bags from ClutterTables.* or BagsAndContainers.*).
/// </summary>
public sealed class DistributionFileWriter
{
    private readonly IFileWriter _fileWriter;

    public DistributionFileWriter(IFileWriter? fileWriter = null)
    {
        _fileWriter = fileWriter ?? new DiskFileWriter();
    }

    /// <summary>
    /// Saves all dirty distributions back to their source files.
    /// Also writes Distribution_*.lua reference files for any dirty referenced junk/bags.
    /// Returns the list of file paths that were written.
    /// </summary>
    public IReadOnlyList<string> Save(
        IReadOnlyList<Distribution> allDistributions,
        CommentMap? procComments = null,
        CommentMap? distComments = null)
    {
        var written = new List<string>();

        // ── 1. Write main distribution files (ProceduralDistributions.lua / Distributions.lua) ──

        var groups = allDistributions
            .Where(d => !string.IsNullOrEmpty(d.SourceFile))
            .GroupBy(d => d.SourceFile, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var dists = group.ToList();

            if (!HasDirtyContent(dists))
                continue;

            var sourceFile  = group.Key;
            bool isProcedural = dists.Any(d => d.Type == DistributionType.Procedural);

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

    private static void ClearDirtyFlags(List<Distribution> dists)
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

    private static bool HasDirtyContent(List<Distribution> dists)
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
