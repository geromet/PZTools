using Core.Projects;
using Data.Data;
using Xunit;

namespace Data.Tests;

public sealed class ProjectReferenceLayerTests
{
    [Fact]
    public void DefinitionStore_PersistsSelectedLayersAndPreviewOrder()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.CreateDirectory("project");
        var modA = temp.CreateDirectory("mods/a");
        var modB = temp.CreateDirectory("mods/b");

        var definition = ProjectDefinition.Create(
            "Layered",
            game,
            project,
            [
                new ProjectReferenceLayerDefinition("Mod A", modA),
                new ProjectReferenceLayerDefinition("Mod B", modB, Enabled: false),
            ]);

        ProjectDefinitionStore.Save(definition);
        var reopened = ProjectDefinitionStore.Load(project);

        var layers = Assert.IsAssignableFrom<IReadOnlyList<ProjectReferenceLayerDefinition>>(reopened.ReferenceLayers);
        Assert.Collection(
            layers,
            layer =>
            {
                Assert.Equal("Mod A", layer.Name);
                Assert.Equal(ProjectPathRules.Normalize(modA), layer.Root);
                Assert.True(layer.Enabled);
            },
            layer =>
            {
                Assert.Equal("Mod B", layer.Name);
                Assert.Equal(ProjectPathRules.Normalize(modB), layer.Root);
                Assert.False(layer.Enabled);
            });
    }

    [Fact]
    public void Workspace_UsesPersistedPreviewOrderAndRetainsShadowedProvenance()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.CreateDirectory("project");
        var modA = temp.CreateDirectory("mods/a");
        var modB = temp.CreateDirectory("mods/b");
        var definition = ProjectDefinition.Create(
            "Layered",
            game,
            project,
            [
                new ProjectReferenceLayerDefinition("Mod A", modA),
                new ProjectReferenceLayerDefinition("Mod B", modB),
            ]);

        var gameDistribution = MakeDistribution(game, 1, originalOrder: 20);
        var modADistribution = MakeDistribution(modA, 10, originalOrder: 0);
        var modBDistribution = MakeDistribution(modB, 20, originalOrder: 0);

        // Input order is intentionally unrelated to configured precedence.
        var workspace = new ProjectDistributionWorkspace(
            definition,
            [modBDistribution, gameDistribution, modADistribution]);
        var entry = workspace.Get(DistributionType.Room, "Kitchen");

        Assert.Same(modBDistribution, entry.Effective);
        Assert.Equal("Mod B", entry.ReferenceProvenance?.LayerName);
        Assert.Collection(
            entry.ShadowedReferences,
            shadowed => Assert.Equal("Game", shadowed.LayerName),
            shadowed => Assert.Equal("Mod A", shadowed.LayerName));

        var reversed = ProjectDefinition.Create(
            "Layered",
            game,
            project,
            [
                new ProjectReferenceLayerDefinition("Mod B", modB),
                new ProjectReferenceLayerDefinition("Mod A", modA),
            ]);
        var reversedWorkspace = new ProjectDistributionWorkspace(
            reversed,
            [modBDistribution, gameDistribution, modADistribution]);

        Assert.Same(modADistribution, reversedWorkspace.Get(DistributionType.Room, "Kitchen").Effective);
    }

    [Fact]
    public void Workspace_RejectsDistributionFromDisabledLayer()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.CreateDirectory("project");
        var mod = temp.CreateDirectory("mods/disabled");
        var definition = ProjectDefinition.Create(
            "Layered",
            game,
            project,
            [new ProjectReferenceLayerDefinition("Disabled", mod, Enabled: false)]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ProjectDistributionWorkspace(definition, [MakeDistribution(mod, 2)]));

        Assert.Contains("disabled layer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportedReferenceEdit_FailsClosedWithoutMutatingSource()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.CreateDirectory("project");
        var mod = temp.CreateDirectory("mods/a");
        var source = WriteSource(mod, "original-mod-bytes");
        var originalBytes = File.ReadAllBytes(source);
        var definition = ProjectDefinition.Create(
            "Layered",
            game,
            project,
            [new ProjectReferenceLayerDefinition("Mod A", mod)]);
        var distribution = MakeDistribution(mod, 5, sourcePath: source);
        var workspace = new ProjectDistributionWorkspace(definition, [distribution]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            workspace.Edit(DistributionType.Room, "Kitchen"));

        Assert.Contains("read-only", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(workspace.ProjectOverrides);
        Assert.Equal(originalBytes, File.ReadAllBytes(source));
    }

    [Fact]
    public void Definition_RejectsReferenceLayerOverlappingGameOrProjectRoots()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.CreateDirectory("project");
        var insideGame = temp.CreateDirectory("game/mods/a");
        var insideProject = temp.CreateDirectory("project/imported/a");

        Assert.Throws<ArgumentException>(() =>
            ProjectDefinition.Create(
                "Layered",
                game,
                project,
                [new ProjectReferenceLayerDefinition("Inside game", insideGame)]));

        Assert.Throws<ArgumentException>(() =>
            ProjectDefinition.Create(
                "Layered",
                game,
                project,
                [new ProjectReferenceLayerDefinition("Inside project", insideProject)]));
    }

    private static Distribution MakeDistribution(
        string root,
        int rolls,
        int originalOrder = 0,
        string? sourcePath = null) =>
        new()
        {
            Name = "Kitchen",
            Type = DistributionType.Room,
            ItemRolls = rolls,
            OriginalOrder = originalOrder,
            SourceFile = sourcePath ?? Path.Combine(root, "media", "lua", "server", "Items", "Distributions.lua"),
        };

    private static string WriteSource(string root, string content)
    {
        var items = Path.Combine(root, "media", "lua", "server", "Items");
        Directory.CreateDirectory(items);
        var path = Path.Combine(items, "Distributions.lua");
        File.WriteAllText(path, content);
        return path;
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pztools-reference-layer-tests-{Guid.NewGuid():N}");
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
