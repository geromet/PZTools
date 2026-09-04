using Data.Data;
using Data.Validation;
using Xunit;

namespace Data.Tests;

public sealed class DistributionSemanticValidatorTests
{
    private readonly DistributionSemanticValidator _validator = new();

    [Fact]
    public void ValidItemChancePairsProduceNoDiagnostics()
    {
        var distribution = new Distribution
        {
            Name = "Kitchen",
            Type = DistributionType.Room,
            SourceFile = "/fixtures/Distributions.lua",
        };
        distribution.ItemChances.Add(new Item("Base.Spoon", 10));
        distribution.JunkChances.Add(new Item("Base.Fork", 5));

        var container = new Container { Name = "counter" };
        container.ItemChances.Add(new Item("Base.Plate", 4));
        container.JunkChances.Add(new Item("Base.Mug", 2));
        distribution.Containers.Add(container);

        Assert.Empty(_validator.Validate([distribution]));
    }

    [Fact]
    public void ReportsEveryPreservedOrphanChanceWithStableNavigation()
    {
        var distribution = new Distribution
        {
            Name = "Kitchen",
            Type = DistributionType.Room,
            SourceFile = "/fixtures/Distributions.lua",
        };
        distribution.ItemChances.Add(new Item(string.Empty, 10));
        distribution.JunkChances.Add(new Item(string.Empty, 5));

        var container = new Container { Name = "counter" };
        container.ItemChances.Add(new Item(string.Empty, 4));
        container.JunkChances.Add(new Item(string.Empty, 2));
        distribution.Containers.Add(container);

        var diagnostics = _validator.Validate([distribution]).ToArray();

        Assert.Equal(4, diagnostics.Length);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal(DistributionSemanticValidator.OrphanItemChanceCode, diagnostic.Code);
            Assert.Equal(SemanticDiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal("Kitchen", diagnostic.DistributionName);
            Assert.Null(diagnostic.Reference);
            Assert.Equal("/fixtures/Distributions.lua", diagnostic.SourceFile);
        });

        Assert.Equal(
            new[]
            {
                "Kitchen.items[0]",
                "Kitchen.junk.items[0]",
                "Kitchen.counter.items[0]",
                "Kitchen.counter.junk.items[0]",
            },
            diagnostics.Select(diagnostic => diagnostic.NavigationTarget).ToArray());
    }

    [Fact]
    public void ReferencedContainerUsesItsSourceFileForDiagnosticProvenance()
    {
        var distribution = new Distribution
        {
            Name = "Store",
            Type = DistributionType.Room,
            SourceFile = "/fixtures/Distributions.lua",
        };
        var container = new Container
        {
            Name = "bags",
            SourceReference = "BagsAndContainers.StoreBags",
            SourceReferenceFile = "/fixtures/Distribution_BagsAndContainers.lua",
        };
        container.ItemChances.Add(new Item(string.Empty, 25));
        distribution.Containers.Add(container);

        var diagnostic = Assert.Single(_validator.Validate([distribution]));

        Assert.Equal("bags", diagnostic.ContainerName);
        Assert.Equal("/fixtures/Distribution_BagsAndContainers.lua", diagnostic.SourceFile);
        Assert.Equal("Store.bags.items[0]", diagnostic.NavigationTarget);
    }
}
