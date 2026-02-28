namespace DataInput.Parsing;

/// <summary>
/// Identifies a Lua value that lives in a named global path
/// (e.g. ClutterTables.DeskJunk) inside a specific Distribution_*.lua file.
/// Used to write references back to their source files instead of inlining.
/// </summary>
public sealed record LuaRefInfo(string RefPath, string SourceFile)
{
    /// <summary>
    /// The part before the dot: "ClutterTables" or "BagsAndContainers".
    /// </summary>
    public string GlobalName => RefPath[..RefPath.IndexOf('.')];

    /// <summary>
    /// The part after the dot: "DeskJunk", "ProduceStorage_Apple", etc.
    /// </summary>
    public string EntryName => RefPath[(RefPath.IndexOf('.') + 1)..];
}
