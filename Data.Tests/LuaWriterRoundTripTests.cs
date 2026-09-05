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
    public void NamedItemReferencesSurviveParseWriteParseWithoutChangingInlineControls()
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

                ClutterTables.DeskJunk = {
                    rolls = 4,
                    items = {
                        "Base.Paperclip", 7,
                    },
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
                        counter = {
                            rolls = 2,
                            items = ClutterTables.DeskItems,
                            junk = ClutterTables.DeskJunk,
                        },
                        shelf = {
                            rolls = 3,
                            items = {
                                "Base.Screwdriver", 4,
                            },
                            junk = {
                                rolls = 5,
                                items = {
                                    "Base.Glue", 6,
                                },
                            },
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
            var bags = GetContainer(namedItems, "bags");
            Assert.Equal("ClutterTables.DeskItems", bags.ItemsReference);

            var counter = GetContainer(namedItems, "counter");
            Assert.Equal("ClutterTables.DeskItems", counter.ItemsReference);
            Assert.Equal(new Item("Base.Paperclip", 7), Assert.Single(counter.ItemChances));
            Assert.Equal("ClutterTables.DeskJunk", counter.JunkReference);
            Assert.Equal("ClutterTables.DeskJunk.items", counter.JunkItemsReference);
            Assert.Equal(4, counter.JunkRolls);
            Assert.Equal(new Item("Base.Paperclip", 7), Assert.Single(counter.JunkChances));

            var shelf = GetContainer(namedItems, "shelf");
            Assert.Null(shelf.ItemsReference);
            Assert.Equal(new Item("Base.Screwdriver", 4), Assert.Single(shelf.ItemChances));
            Assert.Null(shelf.JunkReference);
            Assert.Null(shelf.JunkItemsReference);
            Assert.Equal(5, shelf.JunkRolls);
            Assert.Equal(new Item("Base.Glue", 6), Assert.Single(shelf.JunkChances));

            string serialized = LuaWriter.WriteDistributionsFile(first.Distributions);

            Assert.Equal(3, CountOccurrences(serialized, "items = ClutterTables.DeskItems"));
            Assert.Equal(1, CountOccurrences(serialized, "junk = ClutterTables.DeskJunk"));
            Assert.DoesNotContain("\"Base.Paperclip\", 7", serialized);
            Assert.Contains("\"Base.Nail\", 3", serialized);
            Assert.Contains("\"Base.Screwdriver\", 4", serialized);
            Assert.Contains("\"Base.Glue\", 6", serialized);

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
            var reparsedBags = GetContainer(reparsedNamedItems, "bags");
            Assert.Equal("ClutterTables.DeskItems", reparsedBags.ItemsReference);

            var reparsedCounter = GetContainer(reparsedNamedItems, "counter");
            Assert.Equal("ClutterTables.DeskItems", reparsedCounter.ItemsReference);
            Assert.Equal(counter.ItemRolls, reparsedCounter.ItemRolls);
            Assert.Equal(counter.ItemChances.ToArray(), reparsedCounter.ItemChances.ToArray());
            Assert.Equal("ClutterTables.DeskJunk", reparsedCounter.JunkReference);
            Assert.Equal(counter.JunkRolls, reparsedCounter.JunkRolls);
            Assert.Equal(counter.JunkChances.ToArray(), reparsedCounter.JunkChances.ToArray());

            var reparsedShelf = GetContainer(reparsedNamedItems, "shelf");
            Assert.Null(reparsedShelf.ItemsReference);
            Assert.Equal(shelf.ItemRolls, reparsedShelf.ItemRolls);
            Assert.Equal(shelf.ItemChances.ToArray(), reparsedShelf.ItemChances.ToArray());
            Assert.Null(reparsedShelf.JunkReference);
            Assert.Equal(shelf.JunkRolls, reparsedShelf.JunkRolls);
            Assert.Equal(shelf.JunkChances.ToArray(), reparsedShelf.JunkChances.ToArray());
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

    private static Container GetContainer(Distribution distribution, string name) =>
        Assert.Single(distribution.Containers.Where(container => container.Name == name));

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
