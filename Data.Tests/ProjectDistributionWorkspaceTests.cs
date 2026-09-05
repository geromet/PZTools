using Core.Projects;
using Data;
using Data.Data;
using Data.Parsing;
using Xunit;

namespace Data.Tests;

public sealed class ProjectDistributionWorkspaceTests
{
    [Fact]
    public void EditSaveReopenAndRemoveOverride_PreservesReferenceBytesAndProvenance()
    {
        using var temp = new TempWorkspace();
        var gameRoot = temp.CreateDirectory("game");
        var projectRoot = temp.CreateDirectory("project");
        var gameDistributionsPath = WriteReferenceFixture(gameRoot);
        WriteEmptyProceduralFile(projectRoot); // project-file scaffolding is a later #54 slice

        var originalReferenceBytes = File.ReadAllBytes(gameDistributionsPath);
        var definition = ProjectDefinition.Create("Project", gameRoot, projectRoot);
        var referenceParse = Parse(gameRoot);
        Assert.False(referenceParse.HasFatalErrors);
        var reference = Assert.Single(referenceParse.Distributions.Where(d => d.Name == "Kitchen"));

        var workspace = new ProjectDistributionWorkspace(definition, referenceParse.Distributions);
        var initial = workspace.Get(reference.Type, reference.Name);
        Assert.Equal(DistributionLayer.Reference, initial.Layer);
        Assert.Same(reference, initial.Effective);

        var editable = workspace.Edit(reference.Type, reference.Name);
        Assert.NotSame(reference, editable);
        Assert.Equal(DistributionLayer.Project, workspace.Get(reference.Type, reference.Name).Layer);
        editable.ItemRolls = 7;
        editable.IsDirty = true;

        var saver = new ProjectDistributionSaver();
        var written = saver.Save(workspace);
        var projectDistributionsPath = Path.Combine(
            projectRoot, "media", "lua", "server", "Items", "Distributions.lua");

        Assert.Contains(ProjectPathRules.Normalize(projectDistributionsPath), written);
        Assert.True(File.Exists(projectDistributionsPath));
        Assert.Contains("rolls = 7", File.ReadAllText(projectDistributionsPath));
        Assert.Equal(originalReferenceBytes, File.ReadAllBytes(gameDistributionsPath));

        var projectParse = Parse(projectRoot);
        Assert.False(projectParse.HasFatalErrors);
        var reopened = new ProjectDistributionWorkspace(
            definition,
            referenceParse.Distributions,
            projectParse.Distributions);
        var reopenedEntry = reopened.Get(reference.Type, reference.Name);
        Assert.Equal(DistributionLayer.Project, reopenedEntry.Layer);
        Assert.NotNull(reopenedEntry.Reference);
        Assert.Equal(7, reopenedEntry.Effective.ItemRolls);
        Assert.Equal(1, reopenedEntry.Reference!.ItemRolls);

        Assert.True(reopened.RemoveOverride(reference.Type, reference.Name));
        var revealed = reopened.Get(reference.Type, reference.Name);
        Assert.Equal(DistributionLayer.Reference, revealed.Layer);
        Assert.Same(reference, revealed.Effective);

        saver.Save(reopened);
        var projectAfterRemoval = Parse(projectRoot);
        Assert.False(projectAfterRemoval.HasFatalErrors);
        Assert.DoesNotContain(projectAfterRemoval.Distributions, d => d.Name == "Kitchen");

        var finalWorkspace = new ProjectDistributionWorkspace(
            definition,
            referenceParse.Distributions,
            projectAfterRemoval.Distributions);
        var finalEntry = finalWorkspace.Get(reference.Type, reference.Name);
        Assert.Equal(DistributionLayer.Reference, finalEntry.Layer);
        Assert.Equal(1, finalEntry.Effective.ItemRolls);
        Assert.Equal(originalReferenceBytes, File.ReadAllBytes(gameDistributionsPath));
    }

    [Fact]
    public void Save_RejectsForgedSourceFileOutsideOwnershipRoots()
    {
        using var temp = new TempWorkspace();
        var gameRoot = temp.CreateDirectory("game");
        var projectRoot = temp.CreateDirectory("project");
        var gameDistributionsPath = WriteReferenceFixture(gameRoot);
        var originalReferenceBytes = File.ReadAllBytes(gameDistributionsPath);
        var definition = ProjectDefinition.Create("Project", gameRoot, projectRoot);
        var referenceParse = Parse(gameRoot);
        var reference = Assert.Single(referenceParse.Distributions.Where(d => d.Name == "Kitchen"));
        var workspace = new ProjectDistributionWorkspace(definition, referenceParse.Distributions);
        var editable = workspace.Edit(reference.Type, reference.Name);
        var outside = Path.Combine(temp.Root, "outside", "Distributions.lua");

        editable.SourceFile = outside;
        editable.ItemRolls = 99;
        editable.IsDirty = true;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ProjectDistributionSaver().Save(workspace));

        Assert.Contains("outside configured ownership roots", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outside));
        Assert.Equal(originalReferenceBytes, File.ReadAllBytes(gameDistributionsPath));
    }

    [Fact]
    public void Save_RejectsProjectSubdirectorySymlinkThatRoutesBackIntoGame()
    {
        if (OperatingSystem.IsWindows())
            return; // Linux CI exercises the real symlink boundary without requiring link privileges.

        using var temp = new TempWorkspace();
        var gameRoot = temp.CreateDirectory("game");
        var projectRoot = temp.CreateDirectory("project");
        var gameDistributionsPath = WriteReferenceFixture(gameRoot);
        var originalReferenceBytes = File.ReadAllBytes(gameDistributionsPath);
        var definition = ProjectDefinition.Create("Project", gameRoot, projectRoot);
        var referenceParse = Parse(gameRoot);
        var reference = Assert.Single(referenceParse.Distributions.Where(d => d.Name == "Kitchen"));
        var workspace = new ProjectDistributionWorkspace(definition, referenceParse.Distributions);
        var editable = workspace.Edit(reference.Type, reference.Name);
        editable.ItemRolls = 42;
        editable.IsDirty = true;

        Directory.CreateSymbolicLink(
            Path.Combine(projectRoot, "media"),
            Path.Combine(gameRoot, "media"));

        Assert.Throws<InvalidOperationException>(() =>
            new ProjectDistributionSaver().Save(workspace));
        Assert.Equal(originalReferenceBytes, File.ReadAllBytes(gameDistributionsPath));
    }

    private static ParseResult Parse(string root) =>
        new DistributionParser(new LuaFileLoader(), new DistributionMapper()).Parse(root);

    private static string WriteReferenceFixture(string gameRoot)
    {
        WriteEmptyProceduralFile(gameRoot);
        var itemsRoot = Path.Combine(gameRoot, "media", "lua", "server", "Items");
        Directory.CreateDirectory(itemsRoot);
        var path = Path.Combine(itemsRoot, "Distributions.lua");
        File.WriteAllText(
            path,
            """
            Distributions = Distributions or {}

            local distributionTable = {
                Kitchen = {
                    rolls = 1,
                    items = {
                        "Base.Apple", 2,
                    },
                },
            }

            table.insert(Distributions, 1, distributionTable)
            SuburbsDistributions = distributionTable
            """);
        return path;
    }

    private static void WriteEmptyProceduralFile(string root)
    {
        var itemsRoot = Path.Combine(root, "media", "lua", "server", "Items");
        Directory.CreateDirectory(itemsRoot);
        File.WriteAllText(
            Path.Combine(itemsRoot, "ProceduralDistributions.lua"),
            """
            ProceduralDistributions = {}
            ProceduralDistributions.list = {}
            """);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pztools-project-routing-tests-{Guid.NewGuid():N}");
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
