using Data.Classification;
using Data.Data;
using Xunit;

namespace Data.Tests;

public sealed class DistributionClassifierTests
{
    [Theory]
    [InlineData("Bag_FutureBuildContainer")]
    [InlineData("bag_futurebuildcontainer")]
    [InlineData("AmmoStrap_FutureCalibre")]
    [InlineData("CookieJar_FutureShape")]
    [InlineData("Cooler_FutureContents")]
    [InlineData("FirstAidKit_FutureTier")]
    [InlineData("HollowBook_FutureLoot")]
    [InlineData("JewelleryBox_FutureStyle")]
    [InlineData("KeyRing_FutureCharm")]
    [InlineData("Parcel_FutureSize")]
    [InlineData("PhotoAlbum_FutureAge")]
    [InlineData("Present_FutureSize")]
    [InlineData("TakeoutBox_FutureMaterial")]
    [InlineData("Toolbox_FutureTrade")]
    [InlineData("ToolRoll_FutureMaterial")]
    [InlineData("Wallet_FutureStyle")]
    public void StructuralItemPrefixes_ClassifyFutureVariantsAsItems(string name)
    {
        Assert.Equal(DistributionType.Item, DistributionClassifier.Classify(name));
    }

    [Theory]
    [InlineData("Cashbox", DistributionType.Item)]
    [InlineData("Toolbox", DistributionType.Item)]
    [InlineData("Cooler", DistributionType.Item)]
    [InlineData("FirstAidKit", DistributionType.Item)]
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
    [InlineData("AmmoStrapRoom")]
    [InlineData("CoolerRoom")]
    [InlineData("FirstAidKitRoom")]
    [InlineData("KeyRingRoad")]
    [InlineData("ParcelRoom")]
    [InlineData("PresentRoom")]
    [InlineData("ToolboxRoom")]
    [InlineData("WalletRoom")]
    public void SimilarButNonStructuralNamesStillFallBackToRoom(string name)
    {
        Assert.Equal(DistributionType.Room, DistributionClassifier.Classify(name));
    }
}
