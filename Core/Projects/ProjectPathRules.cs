namespace Core.Projects;

/// <summary>
/// Filesystem ownership rules for project mode. The writable project root must never be the game
/// root or a descendant of it, including when an existing path component is a symbolic link.
/// </summary>
public static class ProjectPathRules
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new ArgumentException("Path has no filesystem root.", nameof(path));
        var current = root;
        var relative = Path.GetRelativePath(root, fullPath);

        if (relative != ".")
        {
            foreach (var segment in relative.Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(current, segment);
                FileSystemInfo? info = Directory.Exists(candidate)
                    ? new DirectoryInfo(candidate)
                    : File.Exists(candidate)
                        ? new FileInfo(candidate)
                        : null;

                var resolved = info?.ResolveLinkTarget(returnFinalTarget: true);
                current = resolved?.FullName ?? candidate;
            }
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    public static void EnsureProjectOutsideGame(string gameRoot, string projectRoot)
    {
        gameRoot = Normalize(gameRoot);
        projectRoot = Normalize(projectRoot);

        if (IsSameOrDescendant(projectRoot, gameRoot))
            throw new ArgumentException("Project root must be separate from the game root and must not be inside the game installation.", nameof(projectRoot));
    }

    /// <summary>
    /// Returns true when <paramref name="candidate"/> resolves to <paramref name="root"/> itself or
    /// a descendant. Both paths are canonicalized on every call so a symlink/reparse swap that
    /// happens after project creation cannot bypass a later ownership check.
    /// </summary>
    public static bool IsSameOrDescendant(string candidate, string root)
    {
        candidate = Normalize(candidate);
        root = Normalize(root);

        if (string.Equals(candidate, root, PathComparison))
            return true;

        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(rootPrefix, PathComparison);
    }

    public static void EnsureInsideProject(string projectRoot, string targetPath)
    {
        projectRoot = Normalize(projectRoot);
        targetPath = Normalize(targetPath);

        if (!IsSameOrDescendant(targetPath, projectRoot))
            throw new InvalidOperationException($"Project write target escapes the writable project root: {targetPath}");
    }

    public static bool PathsEqual(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), PathComparison);
}
