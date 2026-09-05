using System.Text.Json;
using Core.Projects;
using Xunit;

namespace Data.Tests;

public sealed class ProjectDefinitionTests
{
    [Fact]
    public void Create_RejectsMissingGameReferenceRoot()
    {
        using var workspace = new TempWorkspace();
        var missingGameRoot = Path.Combine(workspace.Root, "missing-game");
        var projectRoot = Path.Combine(workspace.Root, "project");

        Assert.Throws<DirectoryNotFoundException>(() =>
            ProjectDefinition.Create("Example", missingGameRoot, projectRoot));
    }

    [Fact]
    public void Create_RejectsGameRootAndProjectRootBeingTheSameDirectory()
    {
        using var workspace = new TempWorkspace();
        var root = workspace.CreateDirectory("game");

        var ex = Assert.Throws<ArgumentException>(() => ProjectDefinition.Create("Example", root, root));
        Assert.Contains("separate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_RejectsProjectRootInsideGameInstallation()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var projectRoot = Path.Combine(gameRoot, "mods", "MyProject");

        var ex = Assert.Throws<ArgumentException>(() =>
            ProjectDefinition.Create("Example", gameRoot, projectRoot));

        Assert.Contains("inside", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_RejectsProjectRootRoutedThroughSymlinkIntoGameInstallation()
    {
        if (OperatingSystem.IsWindows())
            return; // Linux CI exercises the symlink boundary; Windows may require link privileges.

        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var gameMods = workspace.CreateDirectory(Path.Combine("game", "mods"));
        var alias = Path.Combine(workspace.Root, "project-alias");
        Directory.CreateSymbolicLink(alias, gameMods);
        var projectRoot = Path.Combine(alias, "MyProject");

        var ex = Assert.Throws<ArgumentException>(() =>
            ProjectDefinition.Create("Example", gameRoot, projectRoot));

        Assert.Contains("inside", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_DoesNotConfuseSiblingPrefixWithDescendant()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("ProjectZomboid");
        var projectRoot = workspace.CreateDirectory("ProjectZomboid-Mods");

        var definition = ProjectDefinition.Create("Example", gameRoot, projectRoot);

        Assert.Equal(ProjectPathRules.Normalize(gameRoot), definition.GameRoot);
        Assert.Equal(ProjectPathRules.Normalize(projectRoot), definition.ProjectRoot);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsCanonicalDefinitionAndLeavesNoTemporaryFile()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var projectRoot = Path.Combine(workspace.Root, "authoring", "MyProject");
        var definition = ProjectDefinition.Create("  My Project  ", gameRoot, projectRoot);

        ProjectDefinitionStore.Save(definition);
        var loaded = ProjectDefinitionStore.Load(projectRoot);

        Assert.Equal(ProjectDefinition.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("My Project", loaded.Name);
        Assert.Equal(ProjectPathRules.Normalize(gameRoot), loaded.GameRoot);
        Assert.Equal(ProjectPathRules.Normalize(projectRoot), loaded.ProjectRoot);
        Assert.True(File.Exists(ProjectDefinitionStore.GetDefinitionPath(projectRoot)));
        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(projectRoot, ProjectDefinitionStore.MetadataDirectoryName),
            "*.tmp"));
    }

    [Fact]
    public void Load_RejectsDefinitionClaimingADifferentWritableRoot()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var openedProjectRoot = workspace.CreateDirectory("opened-project");
        var claimedProjectRoot = workspace.CreateDirectory("other-project");
        var metadataDirectory = Path.Combine(openedProjectRoot, ProjectDefinitionStore.MetadataDirectoryName);
        Directory.CreateDirectory(metadataDirectory);

        var tampered = ProjectDefinition.Create("Example", gameRoot, claimedProjectRoot);
        File.WriteAllText(
            Path.Combine(metadataDirectory, ProjectDefinitionStore.FileName),
            JsonSerializer.Serialize(tampered));

        var ex = Assert.Throws<InvalidDataException>(() => ProjectDefinitionStore.Load(openedProjectRoot));
        Assert.Contains("does not match", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsUnsupportedSchemaVersion()
    {
        using var workspace = new TempWorkspace();
        var gameRoot = workspace.CreateDirectory("game");
        var projectRoot = workspace.CreateDirectory("project");
        var metadataDirectory = Path.Combine(projectRoot, ProjectDefinitionStore.MetadataDirectoryName);
        Directory.CreateDirectory(metadataDirectory);

        var unsupported = new ProjectDefinition(999, "Example", gameRoot, projectRoot);
        File.WriteAllText(
            Path.Combine(metadataDirectory, ProjectDefinitionStore.FileName),
            JsonSerializer.Serialize(unsupported));

        Assert.Throws<InvalidDataException>(() => ProjectDefinitionStore.Load(projectRoot));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pztools-project-tests-{Guid.NewGuid():N}");
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
