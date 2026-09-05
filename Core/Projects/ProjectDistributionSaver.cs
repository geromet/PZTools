using Data.Comments;
using Data.Serialization;

namespace Core.Projects;

/// <summary>
/// Saves only the writable project layer through the existing DistributionFileWriter/LuaWriter
/// pipeline. SourceFile/reference-file paths remain provenance; ProjectRoutedFileWriter decides the
/// final writable location and refuses anything outside configured ownership roots.
/// </summary>
public sealed class ProjectDistributionSaver
{
    private readonly IFileWriter? _innerWriter;

    public ProjectDistributionSaver(IFileWriter? innerWriter = null)
    {
        _innerWriter = innerWriter;
    }

    public IReadOnlyList<string> Save(
        ProjectDistributionWorkspace workspace,
        CommentMap? procComments = null,
        CommentMap? distComments = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var routedWriter = new ProjectRoutedFileWriter(workspace.Project, _innerWriter);
        var distributionWriter = new DistributionFileWriter(routedWriter);
        var requestedWrites = distributionWriter.Save(
            workspace.ProjectOverrides,
            procComments,
            distComments,
            workspace.RemovedSourceFiles);

        var actualWrites = requestedWrites
            .Select(routedWriter.ResolveWritePath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToList();

        workspace.MarkSaved();
        return actualWrites;
    }
}
