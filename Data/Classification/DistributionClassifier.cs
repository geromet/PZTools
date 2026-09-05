using Data.Data;

namespace Data.Classification;

/// <summary>
/// Maps a distribution name to its DistributionType using O(1) HashSet lookups.
/// OrdinalIgnoreCase handles mod files that capitalise names inconsistently.
/// Bag_* distributions are structurally item containers, so they do not require
/// a hand-maintained entry for every game build.
/// Static class — no instance state, no allocation on use.
/// </summary>
public static class DistributionClassifier
{
    private static readonly HashSet<string> Caches = new(StringComparer.OrdinalIgnoreCase)
    {
        "FoodCache1",
        "GunCache1",
        "GunCache2",
        "MedicalCache1",
        "SafehouseLoot",
        "ShotgunCache1",
        "ShotgunCache2",
        "SurvivorCache1",
        "SurvivorCache2",
        "ToolsCache1",
        "BombCache1",
        "BoozeCache1",
        "SurvivorCacheBigBuilding",
    };

    private static readonly HashSet<string> Professions = new(StringComparer.OrdinalIgnoreCase)
    {
        "BandPractice",
        "Carpenter",
        "Chef",
        "Electrician",
        "Farmer",
        "Nurse",
    };

    // Non-Bag_* item distributions still need explicit classification. Bag_* names
    // are handled by the structural prefix rule in Classify below.
    private static readonly HashSet<string> Items = new(StringComparer.OrdinalIgnoreCase)
    {
        "AmmoStrap_Bullets",
        "AmmoStrap_Bullets_308",
        "AmmoStrap_Bullets_38",
        "AmmoStrap_Bullets_44",
        "AmmoStrap_Bullets_45",
        "AmmoStrap_Bullets_9mm",
        "AmmoStrap_Shells",
        "Cashbox",
        "CigarBox",
        "JewelleryBox",
        "JewelleryBox_Fancy",
        "Shoebox",
        "Tacklebox",
        "TakeoutBox_Chinese",
        "TakeoutBox_Styrofoam",
        "Toolbox_Farming",
        "Toolbox_Fishing",
        "Toolbox_Gardening",
        "Toolbox_Mechanic",
        "Toolbox_Wooden",
        "CookieJar",
        "CookieJar_Bear",
        "Cooler",
        "Cooler_Beer",
        "Cooler_Meat",
        "Cooler_Seafood",
        "Cooler_Soda",
        "Humidor",
        "ToolRoll_Fabric",
        "ToolRoll_Leather",
        "Wallet",
        "Wallet_Hide",
        "Wallet_Male",
        "WheatSack",
        "WheatSeedSack",
        "FirstAidKit_Camping",
        "FirstAidKit_Camping_New",
        "FirstAidKit_Military",
        "FirstAidKit_New",
        "FirstAidKit_NewPro",
        "FirstAidKit_Pro",
        "HalloweenCandyBucket",
        "HollowBook",
        "HollowBook_Handgun",
        "HollowBook_Kids",
        "HollowBook_Prison",
        "HollowBook_Valuables",
        "HollowBook_Whiskey",
        "KeyRing",
        "KeyRing_Bass",
        "KeyRing_BlueFox",
        "KeyRing_Bug",
        "KeyRing_CarDealer",
        "KeyRing_Clover",
        "KeyRing_EagleFlag",
        "KeyRing_EightBall",
        "KeyRing_Forged",
        "KeyRing_Forged_Gold",
        "KeyRing_Forged_Silver",
        "KeyRing_Hotdog",
        "KeyRing_Kitty",
        "KeyRing_Large",
        "KeyRing_Nolans",
        "KeyRing_Panther",
        "KeyRing_PineTree",
        "KeyRing_PrayingHands",
        "KeyRing_RabbitFoot",
        "KeyRing_RainbowStar",
        "KeyRing_RubberDuck",
        "KeyRing_SecurityPass",
        "KeyRing_Sexy",
        "KeyRing_Spiffos",
        "KeyRing_StinkyFace",
        "KeyRing_WestMaple",
        "Briefcase_Money",
        "MakeupCase_Professional",
        "PencilCase",
        "RifleCase4",
        "Parcel_ExtraLarge",
        "Parcel_ExtraSmall",
        "Parcel_Large",
        "Parcel_Medium",
        "Parcel_Small",
        "PhotoAlbum",
        "PhotoAlbum_Old",
        "Present_ExtraLarge",
        "Present_ExtraSmall",
        "Present_Large",
        "Present_Medium",
        "Present_Small",
        "Briefcase",
        "FirstAidKit",
        "Flightcase",
        "Garbagebag",
        "GroceryBag1",
        "GroceryBag2",
        "GroceryBag3",
        "GroceryBag4",
        "GroceryBag5",
        "Guitarcase",
        "Handbag",
        "Lunchbag",
        "Lunchbox",
        "Lunchbox2",
        "Paperbag",
        "Paperbag_Jays",
        "Paperbag_Spiffos",
        "PistolCase1",
        "PistolCase2",
        "PistolCase3",
        "Plasticbag",
        "Purse",
        "RevolverCase1",
        "RevolverCase2",
        "RevolverCase3",
        "RifleCase1",
        "RifleCase2",
        "RifleCase3",
        "SeedBag",
        "SewingKit",
        "ShotgunCase1",
        "ShotgunCase2",
        "Suitcase",
        "Toolbox",
        "Tote",
        "DiceBag",
        "GemBag",
        "GroceryBagGourmet",
        "Plasticbag_Bags",
        "Plasticbag_Clothing",
        "SeedBag_Farming",
        "Tote_Bags",
    };

    /// <summary>
    /// Returns the DistributionType for the given name.
    /// Bag_* is a stable structural item-container namespace; everything else falls
    /// back to Room unless it is in one of the explicit type sets above.
    /// </summary>
    public static DistributionType Classify(string name)
    {
        if (Caches.Contains(name)) return DistributionType.Cache;
        if (Items.Contains(name) || name.StartsWith("Bag_", StringComparison.OrdinalIgnoreCase))
            return DistributionType.Item;
        if (Professions.Contains(name)) return DistributionType.Profession;
        return DistributionType.Room;
    }
}
