namespace Core.Projects;

/// <summary>
/// Filesystem ownership rules for project mode. The writable project root must never be the game
/// root or a descendant of it, so ordinary project writes cannot target installed game data.
/// </summary>
public static class ProjectPathRules
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
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
