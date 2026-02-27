using DataInput.Classification;
using DataInput.Data;
using DataInput.Errors;
using NLua;

namespace DataInput.Parsing;

/// <summary>
/// Maps raw LuaTable trees into the Distribution/Container/Item domain model.
///
/// Memory design:
///   - Item is a struct: List&lt;Item&gt; is a contiguous array, no per-item heap pointers.
///   - NamePool interns item name strings: one string instance shared across all references.
///   - LuaValueParser type-switches boxed NLua values directly: no ToString()/TryParse string alloc.
///   - TryParseIntField uses out parameters: no closure/delegate allocation.
///   - _proceduralIndex is a Dictionary built once: all proc lookups are O(1).
///   - List capacities are pre-sized from the LuaTable key count where possible.
///   - ParseResult wraps the lists via AsReadOnly(), not copying them.
///
/// Error handling:
///   Every field parse is isolated. A bad value in one field logs a warning and
///   continues; it does not abort the container or distribution. Mod files with
///   years-old silent errors (missing chance values, broken proc references, unknown
///   keys) are fully tolerated and logged.
/// </summary>
public sealed class DistributionMapper
{
    private readonly List<ParseError> _errors  = new(64);
    private readonly NamePool         _namePool = new();

    // Built once from the procedural parse pass; all subsequent lookups during
    // distribution mapping are O(1). Stored as references — no copies of distributions.
    private Dictionary<string, Distribution> _proceduralIndex =
        new(0, StringComparer.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps both Lua tables and returns a flat list of all distributions plus
    /// any errors/warnings encountered. The lists are handed directly to ParseResult
    /// via AsReadOnly() — no copies.
    /// </summary>
    public (List<Distribution> distributions, List<ParseError> errors) MapAll(
        LuaTable proceduralTable,
        LuaTable distributionsTable,
        string   procSourceFile,
        string   distSourceFile)
    {
        _errors.Clear();
        _namePool.Clear();

        // 1. Parse procedurals first so the index is ready for proc-reference resolution.
        var procedural = MapProceduralTable(proceduralTable, procSourceFile);

        // Build O(1) lookup from the parsed list — stores references, no copies.
        _proceduralIndex = new Dictionary<string, Distribution>(
            procedural.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var d in procedural)
            _proceduralIndex[d.Name] = d;

        // 2. Parse main distributions (rooms, items, caches, professions).
        var distributions = MapDistributionsTable(distributionsTable, distSourceFile);

        // 3. Merge: AddRange avoids multiple reallocs if lists are similar sized.
        distributions.AddRange(procedural);

        return (distributions, _errors);
    }

    // -------------------------------------------------------------------------
    // Top-level table mappers
    // -------------------------------------------------------------------------

    private List<Distribution> MapProceduralTable(LuaTable table, string sourceFile)
    {
        var result = new List<Distribution>(table.Keys.Count);
        int order = 0;
        foreach (KeyValuePair<object, object> kvp in table)
        {
            if (kvp.Key  is not string   name)  continue;
            if (kvp.Value is not LuaTable inner) continue;

            var dist = new Distribution
            {
                Name = _namePool.Intern(name),
                Type = DistributionType.Procedural,
                SourceFile = sourceFile,
                OriginalOrder = order++
            };
            MapDistributionBody(dist, inner, name, sourceFile);
            result.Add(dist);
        }
        return result;
    }

    private List<Distribution> MapDistributionsTable(LuaTable table, string sourceFile)
    {
        // Distributions.lua wraps all entries in one extra table level.
        // We iterate until we find the inner LuaTable and then map that.
        LuaTable? inner = null;
        foreach (var val in table.Values)
        {
            if (val is LuaTable t) { inner = t; break; }
        }

        if (inner is null)
        {
            _errors.Add(Fatal(ErrorCode.LuaLoadFailure,
                "Could not find inner table inside Distributions.", string.Empty, sourceFile));
            return new List<Distribution>(0);
        }

        var result = new List<Distribution>(inner.Keys.Count);
        int order = 0;
        foreach (KeyValuePair<object, object> kvp in inner)
        {
            if (kvp.Key  is not string   name) continue;
            if (kvp.Value is not LuaTable body) continue;

            var dist = new Distribution
            {
                Name = _namePool.Intern(name),
                Type = DistributionClassifier.Classify(name),
                SourceFile = sourceFile,
                OriginalOrder = order++
            };
            MapDistributionBody(dist, body, name, sourceFile);
            result.Add(dist);
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Distribution body
    // -------------------------------------------------------------------------

    private void MapDistributionBody(
        Distribution dist,
        LuaTable     table,
        string       context,
        string       sourceFile)
    {
        foreach (KeyValuePair<object, object> kvp in table)
        {
            if (kvp.Key is not string key) continue;

            switch (key)
            {
                case "isShop":
                    dist.IsShop = true;
                    break;

                case "DontSpawnAmmo":
                    dist.DontSpawnAmmo = true;
                    break;

                case "FillRand":
                    // FillRand may arrive as long(0/1), bool, or string "0"/"1"
                    if (LuaValueParser.TryGetBool(kvp.Value, out bool fillRand))
                        dist.FillRand = fillRand;
                    break;

                case "rolls":
                    if (TryParseIntField(kvp.Value, key, context, sourceFile, out int rolls))
                        dist.ItemRolls = rolls;
                    break;

                case "MaxMap":
                    if (TryParseIntField(kvp.Value, key, context, sourceFile, out int maxMap))
                        dist.MaxMap = maxMap;
                    break;

                case "StashChance":
                    if (TryParseIntField(kvp.Value, key, context, sourceFile, out int stash))
                        dist.StashChance = stash;
                    break;

                case "items":
                    MapItemChances(dist, kvp.Value, context, sourceFile, isJunk: false);
                    break;

                case "junk":
                    MapJunkChances(dist, kvp.Value, context, sourceFile);
                    break;

                default:
                    // Any unknown key that holds a table is treated as a nested container.
                    if (kvp.Value is LuaTable containerTable)
                        dist.Containers.Add(
                            MapContainer(key, containerTable, context, sourceFile));
                    // Non-table unknown keys are silently ignored at distribution level —
                    // vanilla files occasionally have unused metadata fields.
                    break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Container
    // -------------------------------------------------------------------------

    private Container MapContainer(
        string   name,
        LuaTable table,
        string   parentContext,
        string   sourceFile)
    {
        var context   = $"{parentContext}.{name}";
        var container = new Container { Name = _namePool.Intern(name) };

        foreach (KeyValuePair<object, object> kvp in table)
        {
            if (kvp.Key is not string key) continue;

            switch (key)
            {
                case "fillRand":
                    if (LuaValueParser.TryGetBool(kvp.Value, out bool fr))
                        container.FillRand = fr;
                    break;

                case "rolls":
                    if (TryParseIntField(kvp.Value, key, context, sourceFile, out int rolls))
                        container.ItemRolls = rolls;
                    break;

                case "items":
                    MapItemChances(container, kvp.Value, context, sourceFile, isJunk: false);
                    break;

                case "junk":
                    MapJunkChances(container, kvp.Value, context, sourceFile);
                    break;

                case "procedural":
                    if (LuaValueParser.TryGetBool(kvp.Value, out bool proc))
                        container.Procedural = proc;
                    break;

                case "dontSpawnAmmo":
                    if (LuaValueParser.TryGetBool(kvp.Value, out bool dsa))
                        container.DontSpawnAmmo = dsa;
                    break;

                default:
                    // Unknown table values inside a container are proc list blocks.
                    // Unknown non-table values are game-engine metadata (cookFood, isTrash,
                    // ignoreZombieDensity, defaultInventoryLoot, etc.) — silently ignored.
                    if (kvp.Value is LuaTable procTable)
                        MapProcListEntries(container, procTable, context, sourceFile);
                    break;
            }
        }
        return container;
    }

    // -------------------------------------------------------------------------
    // Item chance lists
    // -------------------------------------------------------------------------

    /// <summary>
    /// The items list is alternating key-value pairs: name (string), chance (number), name, chance...
    /// Mod files frequently have missing chance values or orphaned names. Each anomaly is
    /// logged as a warning; parsing continues with remaining pairs.
    /// </summary>
    private void MapItemChances(
        ItemParent parent,
        object?    value,
        string     context,
        string     sourceFile,
        bool       isJunk)
    {
        if (value is not LuaTable table) return;

        var targetList = isJunk ? parent.JunkChances : parent.ItemChances;

        // Items arrive as pairs — pre-size to half the entry count.
        var expectedItems = Math.Max(4, table.Keys.Count / 2);
        if (targetList.Capacity < expectedItems)
            targetList.Capacity = expectedItems;

        string? pendingName = null;

        foreach (KeyValuePair<object, object> kvp in table)
        {
            var val = kvp.Value;

            // Try numeric first — no ToString() allocation, uses boxed NLua type directly.
            if (LuaValueParser.TryGetDouble(val, out double chance))
            {
                if (pendingName is null)
                {
                    _errors.Add(Warn(ErrorCode.MalformedItemList,
                        $"Chance value {chance} has no preceding item name — skipping.",
                        context, sourceFile));
                    continue;
                }

                // Item is a struct — stored inline, no heap allocation.
                targetList.Add(new Item(_namePool.Intern(pendingName), chance));
                pendingName = null;
            }
            else if (val is LuaTable)
            {
                // B42: items lists may contain inline proc-entry sub-tables
                // ({name=..., min=..., weightChance=...}) — not representable as named
                // items, skip silently. Flush any pending orphan name first.
                if (pendingName is not null)
                {
                    _errors.Add(Warn(ErrorCode.MalformedItemList,
                        $"Item '{pendingName}' has no chance value — skipping.",
                        context, sourceFile));
                    pendingName = null;
                }
            }
            else
            {
                // It's a name string. If we already had a pending name, that name
                // had no chance value — log it and move on.
                if (pendingName is not null)
                    _errors.Add(Warn(ErrorCode.MalformedItemList,
                        $"Item '{pendingName}' has no chance value — skipping.",
                        context, sourceFile));

                pendingName = val?.ToString();
            }
        }

        // Trailing name with no following chance at the end of the table.
        if (pendingName is not null)
            _errors.Add(Warn(ErrorCode.MalformedItemList,
                $"Trailing item '{pendingName}' at end of list has no chance value — skipping.",
                context, sourceFile));
    }

    private void MapJunkChances(
        ItemParent parent,
        object?    value,
        string     context,
        string     sourceFile)
    {
        if (value is not LuaTable table) return;

        foreach (KeyValuePair<object, object> kvp in table)
        {
            if (kvp.Key?.ToString() == "rolls")
            {
                if (TryParseIntField(kvp.Value, "junk.rolls", context, sourceFile, out int jr))
                    parent.JunkRolls = jr;
            }
            else
            {
                MapItemChances(parent, kvp.Value, $"{context}.junk", sourceFile, isJunk: true);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Procedural list entries
    // -------------------------------------------------------------------------

    private void MapProcListEntries(
        Container container,
        LuaTable  table,
        string    context,
        string    sourceFile)
    {
        foreach (KeyValuePair<object, object> kvp in table)
        {
            if (kvp.Value is not LuaTable entryTable) continue;

            // B42: some table values that reach here are inline distribution objects
            // (BagsAndContainers.BanditBag, etc.) whose sub-tables have items/rolls
            // rather than a name key. Only parse as a proc entry if name is present.
            if (!TableHasStringKey(entryTable, "name")) continue;

            container.ProcListEntries.Add(
                MapProcListEntry(entryTable, context, sourceFile));
        }
    }

    private static bool TableHasStringKey(LuaTable table, string key)
    {
        foreach (KeyValuePair<object, object> kvp in table)
            if (kvp.Key is string k && k == key) return true;
        return false;
    }

    /// <summary>
    /// Maps a single proc list entry table. Resolves the named procedural distribution
    /// immediately via the pre-built index — O(1) per entry.
    /// </summary>
    private ProcListEntry MapProcListEntry(LuaTable table, string context, string sourceFile)
    {
        var entry = new ProcListEntry();

        foreach (KeyValuePair<object, object> kvp in table)
        {
            if (kvp.Key is not string key) continue;

            switch (key)
            {
                case "name":
                    entry.Name = _namePool.Intern(kvp.Value?.ToString());
                    // Resolve reference immediately — the index is already fully built.
                    if (_proceduralIndex.TryGetValue(entry.Name, out var resolved))
                        entry.ResolvedDistribution = resolved; // store reference, not a copy
                    else
                        _errors.Add(Error(ErrorCode.UnresolvedProcReference,
                            $"No procedural distribution named '{entry.Name}' was found.",
                            context, sourceFile));
                    break;

                case "min":
                    if (TryParseIntField(kvp.Value, key, context, sourceFile, out int min))
                        entry.Min = min;
                    break;

                case "max":
                    if (TryParseIntField(kvp.Value, key, context, sourceFile, out int max))
                        entry.Max = max;
                    break;

                case "weightChance":
                    if (TryParseIntField(kvp.Value, key, context, sourceFile, out int wc))
                        entry.WeightChance = wc;
                    break;

                case "forceForTiles":
                    entry.ForceForTiles = kvp.Value?.ToString();
                    break;
                case "forceForRooms":
                    entry.ForceForRooms = kvp.Value?.ToString();
                    break;
                case "forceForZones":
                    entry.ForceForZones = kvp.Value?.ToString();
                    break;
                case "forceForItems":
                    entry.ForceForItems = kvp.Value?.ToString();
                    break;

                default:
                    _errors.Add(Warn(ErrorCode.UnexpectedKey,
                        $"Unknown key '{key}' in ProcListEntry — may indicate a format change.",
                        context, sourceFile));
                    break;
            }
        }
        return entry;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses an integer from a Lua value using out parameter — avoids closure/delegate allocation.
    /// Logs a warning and returns false if parsing fails; calling code decides what to do.
    /// </summary>
    private bool TryParseIntField(
        object?  value,
        string   field,
        string   context,
        string   sourceFile,
        out int  result)
    {
        if (LuaValueParser.TryGetInt(value, out result))
            return true;

        _errors.Add(Warn(ErrorCode.InvalidNumericValue,
            $"Cannot parse '{value}' as int for field '{field}'.",
            context, sourceFile));
        return false;
    }

    private static ParseError Warn(ErrorCode code, string msg, string ctx, string file) =>
        new() { Code = code, IsFatal = false, Message = msg, Context = ctx, SourceFile = file };

    private static ParseError Error(ErrorCode code, string msg, string ctx, string file) =>
        new() { Code = code, IsFatal = true, Message = msg, Context = ctx, SourceFile = file };

    private static ParseError Fatal(ErrorCode code, string msg, string ctx, string file) =>
        new() { Code = code, IsFatal = true, Message = msg, Context = ctx, SourceFile = file };
}