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

        if (string.Equals(gameRoot, projectRoot, PathComparison))
            throw new ArgumentException("Project root must be separate from the game root.", nameof(projectRoot));

        var gamePrefix = gameRoot.EndsWith(Path.DirectorySeparatorChar)
            ? gameRoot
            : gameRoot + Path.DirectorySeparatorChar;

        if (projectRoot.StartsWith(gamePrefix, PathComparison))
            throw new ArgumentException("Project root must not be inside the game installation.", nameof(projectRoot));
    }

    public static bool PathsEqual(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), PathComparison);
}
