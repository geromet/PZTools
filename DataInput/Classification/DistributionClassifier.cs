using DataInput.Data;

namespace DataInput.Classification;

/// <summary>
/// Maps a distribution name to its DistributionType using O(1) HashSet lookups.
/// OrdinalIgnoreCase handles mod files that capitalise names inconsistently.
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

    private static readonly HashSet<string> Bags = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bag_ALICEpack",
        "Bag_ALICEpack_Army",
        "Bag_BigHikingBag",
        "Bag_BowlingBallBag",
        "Bag_DoctorBag",
        "Bag_DuffelBag",
        "Bag_DuffelBagTINT",
        "Bag_FoodSnacks",
        "Bag_FoodCanned",
        "Bag_GolfBag",
        "Bag_InmateEscapedBag",
        "Bag_JanitorToolbox",
        "Bag_MedicalBag",
        "Bag_Military",
        "Bag_MoneyBag",
        "Bag_NormalHikingBag",
        "Bag_Schoolbag",
        "Bag_ShotgunBag",
        "Bag_ShotgunDblBag",
        "Bag_ShotgunDblSawnoffBag",
        "Bag_ShotgunSawnoffBag",
        "Bag_SurvivorBag",
        "Bag_ToolBag",
        "Bag_WeaponBag",
        "Bag_WorkerBag",
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
        "Bag_Satchel",
        "SeedBag",
        "SewingKit",
        "ShotgunCase1",
        "ShotgunCase2",
        "Suitcase",
        "Toolbox",
        "Tote",
    };

    /// <summary>
    /// Returns the DistributionType for the given name.
    /// Falls back to Room for any name not in a known set.
    /// </summary>
    public static DistributionType Classify(string name)
    {
        if (Caches.Contains(name))      return DistributionType.Cache;
        if (Bags.Contains(name))        return DistributionType.Bag;
        if (Professions.Contains(name)) return DistributionType.Profession;
        return DistributionType.Room;
    }
}