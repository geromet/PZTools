using DataInput.Errors;
using NLua;

namespace DataInput.Parsing;

/// <summary>
/// Loads a Lua file from disk using NLua and returns the requested top-level table.
/// All exceptions from NLua are caught and converted to fatal ParseErrors so the
/// caller can decide whether to abort or continue with partial data.
/// </summary>
public sealed class LuaFileLoader : ILuaLoader
{
    public bool TryLoadTable(
        string        filePath,
        string        tablePath,
        out LuaTable? table,
        out ParseError? error)
    {
        table = null;
        error = null;

        try
        {
            var lua = new Lua { MaximumRecursion = 20 };

            // Pre-load Distribution_*.lua siblings so shared globals like ClutterTables
            // are available when the target file runs (mirrors game load order).
            var dir = Path.GetDirectoryName(filePath);
            if (dir is not null)
            {
                foreach (var sibling in Directory.GetFiles(dir, "Distribution_*.lua")
                                                 .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try { lua.DoFile(sibling); }
                    catch { /* ignore errors in sibling files — best-effort pre-population */ }
                }
            }

            lua.DoFile(filePath);

            var raw = lua.GetTable(tablePath);
            if (raw is LuaTable luaTable)
            {
                table = luaTable;
                return true;
            }

            error = Fatal(
                ErrorCode.LuaLoadFailure,
                $"'{tablePath}' is not a table or does not exist in this file.",
                filePath);
            return false;
        }
        catch (Exception ex)
        {
            error = Fatal(ErrorCode.LuaLoadFailure, ex.Message, filePath);
            return false;
        }
    }

    private static ParseError Fatal(ErrorCode code, string message, string file) =>
        new() { Code = code, IsFatal = true, Message = message, SourceFile = file };
}