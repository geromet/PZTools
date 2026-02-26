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
    public bool FillRand  { get; set; }
}