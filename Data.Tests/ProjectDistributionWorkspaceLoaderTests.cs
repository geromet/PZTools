using Core.Projects;
using Data.Data;
using Xunit;

namespace Data.Tests;

public sealed class ProjectDistributionWorkspaceLoaderTests
{
    [Fact]
    public void Load_ReopensEnabledLayersInPersistedPreviewOrder()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.Path("project");
        var modA = temp.CreateDirectory("mods/a");
        var modB = temp.CreateDirectory("mods/b");
        var disabled = temp.CreateDirectory("mods/disabled");

        WriteFixture(game, rolls: 1);
        WriteFixture(modA, rolls: 2);
        WriteFixture(modB, rolls: 3);
        WriteFixture(disabled, rolls: 99);
        ProjectScaffold.Create("Layered", game, project);

        var definition = ProjectDefinition.Create(
            "Layered",
            game,
            project,
            [
                new ProjectReferenceLayerDefinition("Mod A", modA),
                new ProjectReferenceLayerDefinition("Mod B", modB),
                new ProjectReferenceLayerDefinition("Disabled", disabled, Enabled: false),
            ]);
        ProjectDefinitionStore.Save(definition);

        var reopened = ProjectDefinitionStore.Load(project);
        var workspace = ProjectDistributionWorkspaceLoader.Load(reopened);
        var entry = workspace.Get(DistributionType.Room, "Kitchen");

        Assert.Equal(3, entry.Effective.ItemRolls);
        Assert.Equal("Mod B", entry.ReferenceProvenance?.LayerName);
        Assert.Collection(
            entry.ShadowedReferences,
            shadowed => Assert.Equal("Game", shadowed.LayerName),
            shadowed => Assert.Equal("Mod A", shadowed.LayerName));
        Assert.DoesNotContain(entry.ShadowedReferences, shadowed => shadowed.LayerName == "Disabled");
        Assert.Empty(workspace.ProjectOverrides);
    }

    [Fact]
    public void Load_ReversedPersistedOrderChangesWinnerWithoutInputEnumeration()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.Path("project");
        var modA = temp.CreateDirectory("mods/a");
        var modB = temp.CreateDirectory("mods/b");

        WriteFixture(game, rolls: 1);
        WriteFixture(modA, rolls: 2);
        WriteFixture(modB, rolls: 3);
        ProjectScaffold.Create("Layered", game, project);

        var definition = ProjectDefinition.Create(
            "Layered",
            game,
            project,
            [
                new ProjectReferenceLayerDefinition("Mod B", modB),
                new ProjectReferenceLayerDefinition("Mod A", modA),
            ]);
        ProjectDefinitionStore.Save(definition);

        var workspace = ProjectDistributionWorkspaceLoader.Load(ProjectDefinitionStore.Load(project));
        var entry = workspace.Get(DistributionType.Room, "Kitchen");

        Assert.Equal(2, entry.Effective.ItemRolls);
        Assert.Equal("Mod A", entry.ReferenceProvenance?.LayerName);
        Assert.Collection(
            entry.ShadowedReferences,
            shadowed => Assert.Equal("Game", shadowed.LayerName),
            shadowed => Assert.Equal("Mod B", shadowed.LayerName));
    }

    [Fact]
    public void Load_MissingEnabledLayerFailsClosedWithLayerIdentity()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.Path("project");
        var missing = temp.Path("mods/missing");

        WriteFixture(game, rolls: 1);
        ProjectScaffold.Create("Layered", game, project);

        // Construct directly so this test exercises the loader boundary even if a previously valid
        // persisted layer disappears between project load and workspace load.
        var definition = new ProjectDefinition(
            ProjectDefinition.CurrentSchemaVersion,
            "Layered",
            game,
            project,
            [new ProjectReferenceLayerDefinition("Missing Mod", missing)]);

        var ex = Assert.Throws<InvalidDataException>(() =>
            ProjectDistributionWorkspaceLoader.Load(definition));

        Assert.Contains("Missing Mod", ex.Message, StringComparison.Ordinal);
        Assert.Contains("missing or unreadable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ProjectPathRules.Normalize(missing), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MalformedEnabledLayerReportsNamedParseFailure()
    {
        using var temp = new TempWorkspace();
        var game = temp.CreateDirectory("game");
        var project = temp.Path("project");
        var malformed = temp.CreateDirectory("mods/malformed");

        WriteFixture(game, rolls: 1);
        ProjectScaffold.Create("Layered", game, project);
        WriteProceduralFixture(malformed);
        var items = Path.Combine(malformed, "media", "lua", "server", "Items");
        File.WriteAllText(Path.Combine(items, "Distributions.lua"), "this is not valid lua !!!");

        var definition = new ProjectDefinition(
            ProjectDefinition.CurrentSchemaVersion,
            "Layered",
            game,
            project,
            [new ProjectReferenceLayerDefinition("Broken Mod", malformed)]);

        var ex = Assert.Throws<InvalidDataException>(() =>
            ProjectDistributionWorkspaceLoader.Load(definition));

        Assert.Contains("Broken Mod", ex.Message, StringComparison.Ordinal);
        Assert.Contains("could not be parsed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteFixture(string root, int rolls)
    {
        WriteProceduralFixture(root);
        var items = Path.Combine(root, "media", "lua", "server", "Items");
        File.WriteAllText(
            Path.Combine(items, "Distributions.lua"),
            $$"""
            Distributions = Distributions or {}

            local distributionTable = {
                Kitchen = {
                    rolls = {{rolls}},
                    items = {
                        "Base.Apple", 2,
                    },
                },
            }

            table.insert(Distributions, 1, distributionTable)
            SuburbsDistributions = distributionTable
            """);
    }

    private static void WriteProceduralFixture(string root)
    {
        var items = Path.Combine(root, "media", "lua", "server", "Items");
        Directory.CreateDirectory(items);
        File.WriteAllText(
            Path.Combine(items, "ProceduralDistributions.lua"),
            """
            ProceduralDistributions = {}
            ProceduralDistributions.list = {}
            """);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"pztools-layer-loader-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Path(string relativePath) => System.IO.Path.Combine(Root, relativePath);

        public string CreateDirectory(string relativePath)
        {
            var path = Path(relativePath);
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
