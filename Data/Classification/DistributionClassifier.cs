using Data.Data;

namespace Data.Classification;

/// <summary>
/// Maps a distribution name to its DistributionType using exact HashSet lookups plus a bounded
/// structural-prefix scan. OrdinalIgnoreCase handles mod files that capitalise names inconsistently.
/// Stable item-container namespaces do not require a hand-maintained entry for every game build.
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

    // Families already represented by multiple explicit item entries in vanilla data. The
    // underscore is part of the contract: similar names such as "BagRoom" or "KeyRingRoad"
    // must not be promoted just because they begin with the same letters.
    private static readonly string[] ItemPrefixes =
    {
        "AmmoStrap_",
        "Bag_",
        "CookieJar_",
        "Cooler_",
        "FirstAidKit_",
        "HollowBook_",
        "JewelleryBox_",
        "KeyRing_",
        "Parcel_",
        "PhotoAlbum_",
        "Present_",
        "TakeoutBox_",
        "Toolbox_",
        "ToolRoll_",
        "Wallet_",
    };

    // Item distributions without one of the stable structural prefixes above still need explicit
    // classification. Base names such as "Toolbox" intentionally remain exact entries.
    private static readonly HashSet<string> Items = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cashbox",
        "CigarBox",
        "JewelleryBox",
        "Shoebox",
        "Tacklebox",
        "CookieJar",
        "Cooler",
        "Humidor",
        "Wallet",
        "WheatSack",
        "WheatSeedSack",
        "HollowBook",
        "KeyRing",
        "Briefcase_Money",
        "MakeupCase_Professional",
        "PencilCase",
        "RifleCase4",
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
    /// Returns the DistributionType for the given name. Exact cache classification wins before
    /// item-family rules; everything else falls back to Room unless explicitly classified.
    /// </summary>
    public static DistributionType Classify(string name)
    {
        if (Caches.Contains(name)) return DistributionType.Cache;
        if (Items.Contains(name) || HasItemPrefix(name)) return DistributionType.Item;
        if (Professions.Contains(name)) return DistributionType.Profession;
        return DistributionType.Room;
    }

    private static bool HasItemPrefix(string name)
    {
        foreach (var prefix in ItemPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
