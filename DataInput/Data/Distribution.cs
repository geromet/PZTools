namespace DataInput.Data;

/// <summary>
/// Top-level distribution entry. May represent a room, bag, cache, profession,
/// or procedural list depending on Type.
/// </summary>
public sealed class Distribution : ItemParent
{
    public DistributionType Type { get; set; }

    public bool IsShop        { get; set; }
    public bool DontSpawnAmmo { get; set; }
    public int? MaxMap        { get; set; }
    public int? StashChance   { get; set; }

    /// <summary>
    /// Absolute path to the Lua file this distribution was parsed from.
    /// Used by the serializer to write back to the correct file.
    /// </summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based index from the original parse, used to maintain ordering during write-back.
    /// </summary>
    public int OriginalOrder { get; set; }

    /// <summary>
    /// Containers nested inside this distribution (shelves, counters, etc.).
    /// Capacity starts at 4; most rooms have a small number of containers.
    /// </summary>
    public List<Container> Containers { get; } = new(4);
}