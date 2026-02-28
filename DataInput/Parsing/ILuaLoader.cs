using DataInput.Errors;
using NLua;

namespace DataInput.Parsing;

/// <summary>
/// Abstraction over Lua file loading, primarily to allow the mapper
/// to be tested without touching the filesystem.
/// </summary>
public interface ILuaLoader
{
    /// <summary>
    /// Attempts to load <paramref name="filePath"/> and return the table at
    /// <paramref name="tablePath"/> (e.g. "ProceduralDistributions.list").
    /// Also returns a <paramref name="refLookup"/> function that resolves a
    /// LuaTable to its named global path (e.g. "ClutterTables.DeskJunk") by
    /// calling into Lua where table identity comparison works natively.
    /// </summary>
    /// <returns>True on success; false with a fatal ParseError on failure.</returns>
    bool TryLoadTable(
        string        filePath,
        string        tablePath,
        out LuaTable? table,
        out Func<LuaTable, LuaRefInfo?> refLookup,
        out ParseError? error);
}
