namespace Data.Data;

/// <summary>
/// An entry inside a Container's procedural list.
/// ResolvedDistribution is null when the named procedural distribution could not be
/// found — tracked as an error rather than thrown, so mod files with broken references
/// don't abort the entire parse.
/// </summary>
public sealed class ProcListEntry : IDirtyEntry
{
    public string Name         { get; set; } = string.Empty;
    public int    Min          { get; set; }
    public int    Max          { get; set; }
    public int    WeightChance { get; set; }

    public string? ForceForTiles { get; set; }
    public string? ForceForRooms { get; set; }
    public string? ForceForZones { get; set; }
    public string? ForceForItems { get; set; }

    /// <summary>
    /// Direct reference to the resolved Distribution — no extra lookup needed downstream.
    /// Null means the reference was unresolved; a corresponding ParseError will exist.
    /// </summary>
    public Distribution? ResolvedDistribution { get; set; }

    /// <summary>
    /// Set by the UI when any field on this entry is modified.
    /// </summary>
    public bool IsDirty { get; set; }

    public bool TryGetInt(string prop, out int value)
    {
        switch (prop)
        {
            case "Min":          value = Min;          return true;
            case "Max":          value = Max;          return true;
            case "WeightChance": value = WeightChance; return true;
            default:             value = 0;            return false;
        }
    }

    public void SetInt(string prop, int value)
    {
        switch (prop)
        {
            case "Min":          Min = value;          break;
            case "Max":          Max = value;          break;
            case "WeightChance": WeightChance = value; break;
        }
    }

    public string? GetString(string prop) => prop switch
    {
        "ForceForTiles" => ForceForTiles,
        "ForceForRooms" => ForceForRooms,
        "ForceForItems" => ForceForItems,
        _               => null
    };

    public void SetString(string prop, string? value)
    {
        switch (prop)
        {
            case "ForceForTiles": ForceForTiles = value; break;
            case "ForceForRooms": ForceForRooms = value; break;
            case "ForceForItems": ForceForItems = value; break;
        }
    }
}