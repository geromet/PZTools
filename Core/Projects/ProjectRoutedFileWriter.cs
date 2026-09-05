using Data.Serialization;

namespace Core.Projects;

/// <summary>
/// Project-mode file writer that treats model source paths as provenance only. Any path beneath the
/// read-only game/reference root is remapped to the corresponding relative path beneath the writable
/// project root. Paths already owned by the project remain in place. Everything else fails closed.
/// </summary>
public sealed class ProjectRoutedFileWriter : IFileWriter
{
    private readonly ProjectDefinition _project;
    private readonly IFileWriter _inner;

    public ProjectRoutedFileWriter(ProjectDefinition project, IFileWriter? inner = null)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _inner = inner ?? new DiskFileWriter();
    }

    public string ResolveWritePath(string requestedPath)
    {
        var gameRoot = ProjectPathRules.Normalize(_project.GameRoot);
        var projectRoot = ProjectPathRules.Normalize(_project.ProjectRoot);

        // Re-evaluate the relationship for every write so replacing the project directory with a
        // symlink/reparse point after project creation cannot silently redirect writes into game data.
        ProjectPathRules.EnsureProjectOutsideGame(gameRoot, projectRoot);

        var source = ProjectPathRules.Normalize(requestedPath);
        string target;

        if (ProjectPathRules.IsSameOrDescendant(source, projectRoot))
        {
            target = source;
        }
        else if (ProjectPathRules.IsSameOrDescendant(source, gameRoot))
        {
            var relative = Path.GetRelativePath(gameRoot, source);
            target = ProjectPathRules.Normalize(Path.Combine(projectRoot, relative));
        }
        else
        {
            throw new InvalidOperationException(
                $"Project-mode save refused a source path outside configured ownership roots: {source}");
        }

        ProjectPathRules.EnsureInsideProject(projectRoot, target);
        return target;
    }

    public void WriteAllText(string path, string content)
    {
        var target = ResolveWritePath(path);
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Resolve again after directory creation because creating parents may expose a pre-existing
        // symlink/reparse component that was not present during the first ownership check.
        target = ResolveWritePath(target);
        _inner.WriteAllText(target, content);
    }

    public void Backup(string path)
    {
        var target = ResolveWritePath(path);
        _inner.Backup(target);
    }
}
