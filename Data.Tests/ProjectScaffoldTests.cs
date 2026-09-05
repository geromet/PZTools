using Core.Projects;
using Data;
using Xunit;

namespace Data.Tests;

public sealed class ProjectScaffoldTests
{
    [Fact]
    public void Create_MaterializesOnlyCanonicalSkeleton_AndIsIdempotent()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var referencePath = Path.Combine(gameRoot, "reference-marker.lua");
        var referenceBytes = "reference bytes must stay unchanged"u8.ToArray();
        File.WriteAllBytes(referencePath, referenceBytes);
        var projectRoot = Path.Combine(workspace.Root, "project");

        var created = ProjectScaffold.Create("Example", gameRoot, projectRoot);
        var distributionsPath = Path.Combine(projectRoot, ProjectScaffold.DistributionsRelativePath);
        var proceduralPath = Path.Combine(projectRoot, ProjectScaffold.ProceduralDistributionsRelativePath);
        var definitionPath = ProjectDefinitionStore.GetDefinitionPath(projectRoot);

        Assert.True(File.Exists(distributionsPath));
        Assert.True(File.Exists(proceduralPath));
        Assert.True(File.Exists(definitionPath));
        Assert.Equal(referenceBytes, File.ReadAllBytes(referencePath));

        var files = Directory.EnumerateFiles(projectRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(projectRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                Path.Combine(".pztools", "project.json"),
                ProjectScaffold.DistributionsRelativePath,
                ProjectScaffold.ProceduralDistributionsRelativePath,
            }.OrderBy(path => path, StringComparer.Ordinal),
            files);

        var distributionsBefore = File.ReadAllBytes(distributionsPath);
        var proceduralBefore = File.ReadAllBytes(proceduralPath);
        var definitionBefore = File.ReadAllBytes(definitionPath);

        var reopened = ProjectScaffold.Open(projectRoot);
        var recreated = ProjectScaffold.Create("Example", gameRoot, projectRoot);

        Assert.Equal(created, reopened);
        Assert.Equal(created, recreated);
        Assert.Equal(distributionsBefore, File.ReadAllBytes(distributionsPath));
        Assert.Equal(proceduralBefore, File.ReadAllBytes(proceduralPath));
        Assert.Equal(definitionBefore, File.ReadAllBytes(definitionPath));
        Assert.Equal(referenceBytes, File.ReadAllBytes(referencePath));

        var parsed = DistributionParser.CreateDefault().Parse(projectRoot);
        Assert.False(parsed.HasFatalErrors);
        Assert.Empty(parsed.Distributions);
    }

    [Fact]
    public void Create_RejectsNonEmptyUnownedDirectoryInsteadOfOverwritingIt()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var projectRoot = workspace.CreateDirectory("project");
        var existingPath = Path.Combine(projectRoot, "keep.txt");
        File.WriteAllText(existingPath, "keep me");

        var ex = Assert.Throws<InvalidDataException>(() =>
            ProjectScaffold.Create("Example", gameRoot, projectRoot));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("keep me", File.ReadAllText(existingPath));
        Assert.False(File.Exists(ProjectDefinitionStore.GetDefinitionPath(projectRoot)));
    }

    [Fact]
    public void Open_RejectsMissingRequiredProjectOwnedFileWithActionableDiagnostic()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var projectRoot = Path.Combine(workspace.Root, "project");
        ProjectScaffold.Create("Example", gameRoot, projectRoot);
        File.Delete(Path.Combine(projectRoot, ProjectScaffold.ProceduralDistributionsRelativePath));

        var ex = Assert.Throws<InvalidDataException>(() => ProjectScaffold.Open(projectRoot));

        Assert.Contains("missing required", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProceduralDistributions.lua", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_RejectsMalformedProjectLuaThroughRealParser()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var projectRoot = Path.Combine(workspace.Root, "project");
        ProjectScaffold.Create("Example", gameRoot, projectRoot);
        var distributionsPath = Path.Combine(projectRoot, ProjectScaffold.DistributionsRelativePath);
        File.WriteAllText(distributionsPath, "this is not valid lua {{{");

        var ex = Assert.Throws<InvalidDataException>(() => ProjectScaffold.Open(projectRoot));

        Assert.Contains("not valid Lua", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Distributions.lua", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Open_RejectsRequiredFileSymlinkEscapingWritableProjectRoot()
    {
        if (OperatingSystem.IsWindows())
            return; // Linux CI exercises link resolution without requiring Windows link privileges.

        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var projectRoot = Path.Combine(workspace.Root, "project");
        ProjectScaffold.Create("Example", gameRoot, projectRoot);

        var distributionsPath = Path.Combine(projectRoot, ProjectScaffold.DistributionsRelativePath);
        var referencePath = Path.Combine(gameRoot, "outside-distributions.lua");
        File.WriteAllText(referencePath, "Distributions = {};");
        File.Delete(distributionsPath);
        File.CreateSymbolicLink(distributionsPath, referencePath);

        var ex = Assert.Throws<InvalidOperationException>(() => ProjectScaffold.Open(projectRoot));

        Assert.Contains("escapes", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Distributions = {};", File.ReadAllText(referencePath));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pztools-scaffold-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
