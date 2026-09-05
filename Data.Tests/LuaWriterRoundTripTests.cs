using Data;
using Data.Data;
using Data.Errors;
using Data.Parsing;
using Data.Serialization;
using Xunit;

namespace Data.Tests;

public sealed class LuaWriterRoundTripTests
{
    [Fact]
    public void NamedJunkItemsReferenceSurvivesParseWriteParseWithoutChangingInlineControls()
    {
        string gameFolder = Path.Combine(Path.GetTempPath(), $"pztools-lua-roundtrip-{Guid.NewGuid():N}");
        string itemsFolder = Path.Combine(gameFolder, "media", "lua", "server", "Items");
        Directory.CreateDirectory(itemsFolder);

        try
        {
            File.WriteAllText(
                Path.Combine(itemsFolder, "Distribution_ClutterTables.lua"),
                """
                ClutterTables = ClutterTables or {}

                ClutterTables.DeskItems = {
                    "Base.Paperclip", 7,
                }
                """);

            File.WriteAllText(
                Path.Combine(itemsFolder, "ProceduralDistributions.lua"),
                """
                ProceduralDistributions = {}
                ProceduralDistributions.list = {}
                """);

            string distributionsPath = Path.Combine(itemsFolder, "Distributions.lua");
            File.WriteAllText(
                distributionsPath,
                """
                Distributions = Distributions or {}

                local distributionTable = {
                    NamedJunk = {
                        junk = {
                            rolls = 1,
                            items = ClutterTables.DeskItems,
                        },
                    },
                    InlineJunk = {
                        junk = {
                            rolls = 2,
                            items = {
                                "Base.Nail", 3,
                            },
                        },
                    },
                    NamedItems = {
                        bags = {
                            rolls = 1,
                            items = ClutterTables.DeskItems,
                        },
                    },
                }

                table.insert(Distributions, 1, distributionTable)
                SuburbsDistributions = distributionTable
                """);

            var first = Parse(gameFolder);
            Assert.False(first.HasFatalErrors);

            var namedJunk = GetDistribution(first, "NamedJunk");
            Assert.Equal("ClutterTables.DeskItems", namedJunk.JunkItemsReference);
            Assert.Equal(new Item("Base.Paperclip", 7), Assert.Single(namedJunk.JunkChances));

            var inlineJunk = GetDistribution(first, "InlineJunk");
            Assert.Null(inlineJunk.JunkItemsReference);
            Assert.Equal(new Item("Base.Nail", 3), Assert.Single(inlineJunk.JunkChances));

            var namedItems = GetDistribution(first, "NamedItems");
            var bags = Assert.Single(namedItems.Containers.Where(container => container.Name == "bags"));
            Assert.Equal("ClutterTables.DeskItems", bags.ItemsReference);

            string serialized = LuaWriter.WriteDistributionsFile(first.Distributions);

            Assert.Equal(2, CountOccurrences(serialized, "items = ClutterTables.DeskItems"));
            Assert.DoesNotContain("\"Base.Paperclip\", 7", serialized);
            Assert.Contains("\"Base.Nail\", 3", serialized);

            File.WriteAllText(distributionsPath, serialized);

            var second = Parse(gameFolder);
            Assert.False(second.HasFatalErrors);

            var reparsedNamedJunk = GetDistribution(second, "NamedJunk");
            Assert.Equal("ClutterTables.DeskItems", reparsedNamedJunk.JunkItemsReference);
            Assert.Equal(namedJunk.JunkRolls, reparsedNamedJunk.JunkRolls);
            Assert.Equal(namedJunk.JunkChances.ToArray(), reparsedNamedJunk.JunkChances.ToArray());

            var reparsedInlineJunk = GetDistribution(second, "InlineJunk");
            Assert.Null(reparsedInlineJunk.JunkItemsReference);
            Assert.Equal(inlineJunk.JunkRolls, reparsedInlineJunk.JunkRolls);
            Assert.Equal(inlineJunk.JunkChances.ToArray(), reparsedInlineJunk.JunkChances.ToArray());

            var reparsedNamedItems = GetDistribution(second, "NamedItems");
            var reparsedBags = Assert.Single(reparsedNamedItems.Containers.Where(container => container.Name == "bags"));
            Assert.Equal("ClutterTables.DeskItems", reparsedBags.ItemsReference);
        }
        finally
        {
            if (Directory.Exists(gameFolder))
                Directory.Delete(gameFolder, recursive: true);
        }
    }

    private static ParseResult Parse(string gameFolder) =>
        new DistributionParser(new LuaFileLoader(), new DistributionMapper()).Parse(gameFolder);

    private static Distribution GetDistribution(ParseResult result, string name) =>
        Assert.Single(result.Distributions.Where(distribution => distribution.Name == name));

    private static int CountOccurrences(string value, string needle)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }

        return count;
    }
}
