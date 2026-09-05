using Data.Classification;
using Data.Data;
using Xunit;

namespace Data.Tests;

public sealed class DistributionClassifierTests
{
    [Theory]
    [InlineData("Bag_FutureBuildContainer")]
    [InlineData("bag_futurebuildcontainer")]
    [InlineData("Bag_ProtectiveCaseNextBuild")]
    public void BagPrefix_ClassifiesPreviouslyUnknownNamesAsItems(string name)
    {
        Assert.Equal(DistributionType.Item, DistributionClassifier.Classify(name));
    }

    [Theory]
    [InlineData("Cashbox", DistributionType.Item)]
    [InlineData("FoodCache1", DistributionType.Cache)]
    [InlineData("Carpenter", DistributionType.Profession)]
    [InlineData("Kitchen", DistributionType.Room)]
    public void ExistingExplicitAndFallbackClassificationsRemainStable(
        string name,
        DistributionType expected)
    {
        Assert.Equal(expected, DistributionClassifier.Classify(name));
    }

    [Theory]
    [InlineData("BagRoom")]
    [InlineData("BaggageClaim")]
    [InlineData("Handbag_Unknown")]
    public void SimilarButNonStructuralNamesStillFallBackToRoom(string name)
    {
        Assert.Equal(DistributionType.Room, DistributionClassifier.Classify(name));
    }
}
