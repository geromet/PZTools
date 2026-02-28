using DataInput.Errors;
using NLua;

namespace DataInput.Parsing;

/// <summary>
/// Loads a Lua file from disk using NLua and returns the requested top-level table.
/// Pre-loads Distribution_*.lua siblings first so globals like ClutterTables and
/// BagsAndContainers are defined before the main file runs.
///
/// Reference tracking uses a Lua-side identity map (__pz_refmap) so that table
/// identity comparison works natively — two C# LuaTable wrappers for the same
/// underlying Lua table will resolve correctly via __pz_getref().
/// </summary>
public sealed class LuaFileLoader : ILuaLoader
{
    private readonly Lua _lua = new() { MaximumRecursion = 20 };

    // Directories whose siblings have already been loaded into the Lua state.
    // Prevents double-loading when TryLoadTable is called for both proc and dist
    // files in the same directory.
    private readonly HashSet<string> _loadedDirs = new(StringComparer.OrdinalIgnoreCase);

    public bool TryLoadTable(
        string filePath,
        string tablePath,
        out LuaTable? table,
        out Func<LuaTable, LuaRefInfo?> refLookup,
        out ParseError? error)
    {
        table = null;
        error = null;
        // Default no-op lookup; replaced below on success.
        refLookup = _ => null;

        try
        {
            var dir = Path.GetDirectoryName(filePath);

            if (dir is not null && _loadedDirs.Add(dir))
            {
                // Snapshot _G keys before loading siblings.
                var beforeKeys = SnapshotGlobalKeys();

                foreach (var sibling in Directory.GetFiles(dir, "Distribution_*.lua")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try { _lua.DoFile(sibling); }
                    catch { continue; }
                }

                // Diff to discover new global tables introduced by siblings.
                var newGlobals = DiscoverNewGlobals(beforeKeys);

                // Build the Lua-side refmap for all discovered globals.
                if (newGlobals.Count > 0)
                    BuildLuaRefMap(newGlobals);
            }

            _lua.DoFile(filePath);

            // Build the C# lookup function that calls into Lua.
            var sourceFile = filePath;
            var getRef = _lua.GetFunction("__pz_getref");
            if (getRef is not null)
            {
                refLookup = t =>
                {
                    var result = getRef.Call(t);
                    if (result is [string path])
                        return new LuaRefInfo(path, sourceFile);
                    return null;
                };
            }

            var raw = _lua.GetTable(tablePath);
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

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all string keys currently in Lua's _G table.
    /// </summary>
    private HashSet<string> SnapshotGlobalKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (_lua.GetTable("_G") is LuaTable g)
        {
            foreach (KeyValuePair<object, object> kvp in g)
            {
                if (kvp.Key is string k)
                    keys.Add(k);
            }
        }
        return keys;
    }

    /// <summary>
    /// Compares current _G keys against the snapshot to find new global tables.
    /// </summary>
    private List<string> DiscoverNewGlobals(HashSet<string> beforeKeys)
    {
        var newGlobals = new List<string>();
        if (_lua.GetTable("_G") is LuaTable g)
        {
            foreach (KeyValuePair<object, object> kvp in g)
            {
                if (kvp.Key is not string k) continue;
                if (beforeKeys.Contains(k)) continue;
                if (kvp.Value is LuaTable)
                    newGlobals.Add(k);
            }
        }
        return newGlobals;
    }

    /// <summary>
    /// Injects a Lua script that builds __pz_refmap by iterating all discovered
    /// global tables (two levels deep) and mapping each sub-table to its path.
    /// The __pz_getref function performs a native Lua table identity lookup.
    /// </summary>
    private void BuildLuaRefMap(List<string> globals)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("__pz_refmap = __pz_refmap or {}");

        foreach (var g in globals)
        {
            sb.AppendLine($"if type({g}) == \"table\" then");
            sb.AppendLine($"  for k, v in pairs({g}) do");
            sb.AppendLine($"    if type(v) == \"table\" then");
            sb.AppendLine($"      __pz_refmap[v] = \"{g}.\" .. k");
            sb.AppendLine($"      for k2, v2 in pairs(v) do");
            sb.AppendLine($"        if type(v2) == \"table\" then");
            sb.AppendLine($"          __pz_refmap[v2] = \"{g}.\" .. k .. \".\" .. k2");
            sb.AppendLine($"        end");
            sb.AppendLine($"      end");
            sb.AppendLine($"    end");
            sb.AppendLine($"  end");
            sb.AppendLine($"end");
        }

        sb.AppendLine("function __pz_getref(t)");
        sb.AppendLine("  return __pz_refmap[t]");
        sb.AppendLine("end");

        _lua.DoString(sb.ToString());
    }

    private static ParseError Fatal(ErrorCode code, string message, string file) =>
        new() { Code = code, IsFatal = true, Message = message, SourceFile = file };
}
