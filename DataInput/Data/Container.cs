namespace DataInput.Data;

/// <summary>
/// A named container within a Distribution (e.g. "shelves", "counter").
/// Inherits item/junk list fields from ItemParent.
/// </summary>
public sealed class Container : ItemParent
{
    public bool Procedural    { get; set; }
    public bool DontSpawnAmmo { get; set; }

    public List<ProcListEntry> ProcListEntries { get; } = new(4);
}