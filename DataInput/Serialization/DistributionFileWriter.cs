using DataInput.Data;

namespace DataInput.Serialization;

/// <summary>
/// Orchestrates saving modified distributions back to their source Lua files.
/// Groups by SourceFile, skips clean groups, creates .bak backups, writes full-file
/// rewrites via LuaWriter.
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
    /// Returns the list of file paths that were written.
    /// </summary>
    public IReadOnlyList<string> Save(IReadOnlyList<Distribution> allDistributions)
    {
        var written = new List<string>();

        // Group by source file
        var groups = allDistributions
            .Where(d => !string.IsNullOrEmpty(d.SourceFile))
            .GroupBy(d => d.SourceFile, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var dists = group.ToList();

            // Skip if nothing in this file is dirty
            if (!HasDirtyContent(dists))
                continue;

            var sourceFile = group.Key;
            bool isProcedural = dists.Any(d => d.Type == DistributionType.Procedural);

            // Generate content
            string content = isProcedural
                ? LuaWriter.WriteProceduralFile(dists)
                : LuaWriter.WriteDistributionsFile(dists);

            // Backup original
            _fileWriter.Backup(sourceFile);

            // Write new content
            _fileWriter.WriteAllText(sourceFile, content);

            // Clear dirty flags
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

            written.Add(sourceFile);
        }

        return written;
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
