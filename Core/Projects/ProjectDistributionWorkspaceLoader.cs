using Data;
using Data.Data;
using Data.Errors;

namespace Core.Projects;

/// <summary>
/// Reopens one project into the existing layered distribution workspace by parsing the game
/// reference, each enabled selected reference layer in persisted preview order, and the writable
/// project layer. Each root receives a fresh parser instance so Lua state cannot leak across layers.
/// </summary>
public static class ProjectDistributionWorkspaceLoader
{
    public static ProjectDistributionWorkspace Load(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var references = new List<Distribution>();
        ParseRequiredRoot("Game", project.GameRoot, references);

        var layers = project.ReferenceLayers ?? Array.Empty<ProjectReferenceLayerDefinition>();
        foreach (var layer in layers)
        {
            if (!layer.Enabled)
                continue;

            ParseRequiredRoot(layer.Name, layer.Root, references);
        }

        var projectParse = ParseRequiredRoot("Project", project.ProjectRoot);
        return new ProjectDistributionWorkspace(project, references, projectParse.Distributions);
    }

    private static void ParseRequiredRoot(
        string layerName,
        string root,
        ICollection<Distribution> destination)
    {
        var parse = ParseRequiredRoot(layerName, root);
        foreach (var distribution in parse.Distributions)
            destination.Add(distribution);
    }

    private static ParseResult ParseRequiredRoot(string layerName, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidDataException($"Reference layer '{layerName}' has no configured root.");

        var normalizedRoot = ProjectPathRules.Normalize(root);
        if (!Directory.Exists(normalizedRoot))
        {
            throw new InvalidDataException(
                $"Reference layer '{layerName}' root is missing or unreadable: {normalizedRoot}");
        }

        // DistributionParser owns a Lua loader whose state is intentionally scoped to one root.
        // Recreate it for every layer so globals/references from one mod cannot affect another.
        var parse = DistributionParser.CreateDefault().Parse(normalizedRoot);
        if (!parse.HasFatalErrors)
            return parse;

        var details = string.Join(
            Environment.NewLine,
            parse.FatalErrors.Select(error => error.ToString()));
        throw new InvalidDataException(
            $"Reference layer '{layerName}' at '{normalizedRoot}' could not be parsed.{Environment.NewLine}{details}");
    }
}
