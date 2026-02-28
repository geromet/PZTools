namespace DataInput.Data;

/// <summary>
/// A named container within a Distribution (e.g. "shelves", "counter").
/// Inherits item/junk list fields from ItemParent.
/// </summary>
public sealed class Container : ItemParent
{
    public bool Procedural    { get; set; }
    public bool DontSpawnAmmo { get; set; }
    public bool OnlyOne       { get; set; }
    public int? MaxMap        { get; set; }
    public int? StashChance   { get; set; }

    /// <summary>
    /// If this container was sourced from a named Lua reference
    /// (e.g. BagsAndContainers.ProduceStorage_Apple used as "bags = ..."),
    /// stores the path and source file for write-back.
    /// </summary>
    public string? SourceReference     { get; set; }
    public string? SourceReferenceFile { get; set; }

    public List<ProcListEntry> ProcListEntries { get; } = new(4);
}