namespace DataInput.Data;

/// <summary>
/// Shared base for Distribution and Container.
/// Both carry item/junk lists, rolls, and fill behaviour —
/// a base class is appropriate here rather than an interface because the
/// shared state is structural, not contractual.
/// </summary>
public abstract class ItemParent
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Initialised with a small non-zero capacity to avoid the first few
    /// automatic doublings on lists that typically have 10–100 entries.
    /// Capacity is further tuned during parsing when the LuaTable size is known.
    /// </summary>
    public List<Item> ItemChances { get; } = new(8);
    public List<Item> JunkChances { get; } = new(4);

    public int ItemRolls { get; set; }
    public int JunkRolls { get; set; }
    public bool FillRand           { get; set; }
    public bool IgnoreZombieDensity     { get; set; }
    public bool JunkIgnoreZombieDensity { get; set; }

    /// <summary>
    /// If the junk block came from a named Lua reference (e.g. ClutterTables.DeskJunk),
    /// stores that path so the serializer can write the reference instead of inlining.
    /// </summary>
    public string? JunkReference     { get; set; }
    public string? JunkReferenceFile { get; set; }

    /// <summary>
    /// If the items inside the junk block are themselves a named reference
    /// (e.g. ClutterTables.DeskItems), stores that path so write-back preserves it.
    /// </summary>
    public string? JunkItemsReference { get; set; }

    /// <summary>
    /// If the items list is a named Lua reference (e.g. BagsAndContainers.BanditItems),
    /// stores that path so write-back preserves it.
    /// </summary>
    public string? ItemsReference { get; set; }

    public string? BagsReference { get; set; }
    public string? BagsFileReference { get; set; }

    /// <summary>
    /// Set by the UI when any field on this object (or its item lists) is modified.
    /// The serializer uses this to decide whether to include this object in write-back.
    /// Reset after a successful save.
    /// </summary>
    public bool IsDirty { get; set; }
}