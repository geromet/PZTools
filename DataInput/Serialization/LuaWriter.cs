using System.Diagnostics;
using System.Text;
using DataInput.Comments;
using DataInput.Data;

namespace DataInput.Serialization;

/// <summary>
/// Serializes the domain model back to Lua source text matching the vanilla
/// ProceduralDistributions.lua / Distributions.lua format.
///
/// Design: pure functions, no I/O. Caller decides what to do with the string.
/// Formatting: tab indentation, double-quoted item names, trailing commas,
/// one blank line between distribution entries — matching vanilla file style.
/// </summary>
public static class LuaWriter
{
    /// <summary>
    /// Writes all procedural distributions to a complete ProceduralDistributions.lua file.
    /// </summary>
    public static string WriteProceduralFile(IReadOnlyList<Distribution> distributions, CommentMap? comments = null)
    {
        var procedural = distributions
            .Where(d => d.Type == DistributionType.Procedural)
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder(procedural.Count * 512);
        sb.AppendLine("ProceduralDistributions = {};\n\nProceduralDistributions.list = {");
        string indent = "\t";
        BuildDistributionsString(procedural, sb, indent, comments, isProcedural: true);
        EmitComment(sb, comments, "__trailing", indent);
        sb.AppendLine("\n}");
        if (comments is not null && comments.TryGet("__footer", out _))
        {
            sb.AppendLine();
            EmitVerbatim(sb, comments, "__footer");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Writes all non-procedural distributions to a complete Distributions.lua file.
    /// </summary>
    public static string WriteDistributionsFile(IReadOnlyList<Distribution> distributions, CommentMap? comments = null)
    {
        var rooms= distributions
            .Where(d => d.Type == DistributionType.Room)
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();
        var items= distributions
            .Where(d => d.Type == DistributionType.Item)
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();
        var professions= distributions
            .Where(d => d.Type == DistributionType.Profession)
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();
        var caches= distributions
            .Where(d => d.Type == DistributionType.Cache)
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder(rooms.Count+items.Count+professions.Count+caches.Count * 512);
        sb.AppendLine(" Distributions = Distributions or {};\n\nlocal distributionTable = {");
        sb.AppendLine();

        if (comments is null)
        {
            // Fallback: hardcoded section dividers when no comment map available
            sb.AppendLine("\t-- =====================\n\t--\tRoom List (A-Z)\n\t-- =====================");
            sb.AppendLine();
        }
        BuildDistributionsString(rooms, sb, "\t", comments, isProcedural: false);

        if (comments is null)
        {
            sb.AppendLine("\t-- =====================\n\t--\tBAGS/CONTAINERS\n\t-- =====================");
            sb.AppendLine();
        }
        BuildDistributionsString(items, sb, "\t", comments, isProcedural: false);

        if (comments is null)
        {
            sb.AppendLine("\t-- =====================\n\t--\t  PROFESSIONS\n\t-- =====================");
            sb.AppendLine();
        }
        BuildDistributionsString(professions, sb, "\t", comments, isProcedural: false);

        if (comments is null)
        {
            sb.AppendLine("\t-- =====================\n\t--\t\tCACHES\n\t-- =====================");
            sb.AppendLine();
        }
        BuildDistributionsString(caches, sb, "\t", comments, isProcedural: false);

        EmitComment(sb, comments, "__trailing", "\t");
        sb.AppendLine("}");
        if (comments is not null && comments.TryGet("__footer", out _))
        {
            sb.AppendLine();
            EmitVerbatim(sb, comments, "__footer");
        }
        else
        {
            sb.AppendLine("\ntable.insert(Distributions, 1, distributionTable);\n\n--for mod compat:\nSuburbsDistributions = distributionTable;");
        }
        return sb.ToString();
    }

    private static void BuildDistributionsString(IReadOnlyList<Distribution> distributions, StringBuilder sb, string indent, CommentMap? comments, bool isProcedural)
    {
        for (int i = 0; i < distributions.Count; i++)
        {
            var d = distributions[i];

            // Blank lines between distributions: use original verbatim lines if available.
            if (i > 0)
            {
                var blankLines = comments?.GetBlankLinesBefore(d.Name);
                if (blankLines is not null)
                {
                    foreach (var blankLine in blankLines)
                        sb.AppendLine(blankLine);
                }
                else
                {
                    sb.AppendLine(); // default: 1 blank line
                }
            }

            EmitComment(sb, comments, d.Name, indent);
            sb.Append(indent+d.Name);
            sb.AppendLine(" = {");
            WriteDistributionBody(sb, d, indent+"\t", d.Name, comments, isProcedural);
            sb.AppendLine(indent+"},");
        }
    }

    // ── Distribution body ──

    private static void WriteDistributionBody(StringBuilder sb, Distribution d, string indent, string path, CommentMap? comments, bool isProcedural)
    {
        // Scalar fields
        if (d.IgnoreZombieDensity)
        {
            EmitComment(sb, comments, $"{path}.ignoreZombieDensity", indent);
            sb.AppendLine($"{indent}ignoreZombieDensity = true,");
        }
        
        if (d.IsWorn)
        {
            EmitComment(sb, comments, $"{path}.isWorn", indent);
            sb.AppendLine($"{indent}isWorn = true,");
        }
        // The Java parser checks containsKey("rolls") to decide if a distribution
        // is a direct container. If rolls exists, it unconditionally reads items too
        // (no null check). So whenever we emit rolls, we MUST also emit items.
        bool emitRollsAndItems = isProcedural || d.ItemRolls != 0 || d.ItemChances.Count > 0;

        if (emitRollsAndItems)
        {
            EmitComment(sb, comments, $"{path}.rolls", indent);
            sb.AppendLine($"{indent}rolls = {d.ItemRolls},");
        }

        if (d.IsShop)
        {
            EmitComment(sb, comments, $"{path}.isShop", indent);
            sb.AppendLine($"{indent}isShop = true,");
        }

        if (d.DontSpawnAmmo)
        {
            EmitComment(sb, comments, $"{path}.DontSpawnAmmo", indent);
            sb.AppendLine($"{indent}DontSpawnAmmo = true,");
        }

        if (d.FillRand)
        {
            EmitComment(sb, comments, $"{path}.FillRand", indent);
            sb.AppendLine($"{indent}FillRand = true,");
        }

        if (d.MaxMap.HasValue)
        {
            EmitComment(sb, comments, $"{path}.MaxMap", indent);
            sb.AppendLine($"{indent}MaxMap = {d.MaxMap.Value},");
        }

        if (d.StashChance.HasValue)
        {
            EmitComment(sb, comments, $"{path}.StashChance", indent);
            sb.AppendLine($"{indent}StashChance = {d.StashChance.Value},");
        }

        if (emitRollsAndItems)
        {
            string itemsPath = $"{path}.items";
            EmitComment(sb, comments, itemsPath, indent);
            sb.AppendLine($"{indent}items = {{");
            if (d.ItemChances.Count > 0)
                WriteItemList(sb, d.ItemChances, indent + "\t", itemsPath, comments);
            sb.AppendLine($"{indent}}},");
        }

        // bags container must come before junk (Lua parser expects this order)
        WriteContainersByName(sb, d, indent, path, comments, isProcedural, name => name == "bags");

        // Direct junk — use a reference if this junk came from a named Lua global.
        if (d.JunkReference is not null)
        {
            EmitComment(sb, comments, $"{path}.junk", indent);
            sb.AppendLine($"{indent}junk = {d.JunkReference},");
        }
        else
        {
            if (d.JunkChances.Count > 0 || d.JunkRolls > 0)
            {
                WriteJunkBlock(sb, d, indent, path, comments, isProcedural);
            }
        }

        // Remaining containers
        WriteContainersByName(sb, d, indent, path, comments, isProcedural, name => name != "bags");

        EmitComment(sb, comments, $"{path}.__trailing", indent);
    }

    private static void WriteContainersByName(
        StringBuilder sb, Distribution d, string indent, string path,
        CommentMap? comments, bool isProcedural, Func<string, bool> nameFilter)
    {
        foreach (var c in d.Containers)
        {
            if (!nameFilter(c.Name)) continue;
            string containerPath = $"{path}.{c.Name}";
            if (c.SourceReference is not null)
            {
                EmitComment(sb, comments, containerPath, indent);
                sb.AppendLine($"{indent}{c.Name} = {c.SourceReference},");
            }
            else
            {
                EmitComment(sb, comments, containerPath, indent);
                sb.AppendLine($"{indent}{c.Name} = {{");
                WriteContainer(sb, c, indent + "\t", containerPath, comments, isProcedural);
                sb.AppendLine($"{indent}}},");
            }
        }
    }

    // ── Container ──

    private static void WriteContainer(StringBuilder sb, Container c, string indent, string path, CommentMap? comments, bool isProcedural = false)
    {
        if (c.IgnoreZombieDensity)
        {
            EmitComment(sb, comments, $"{path}.ignoreZombieDensity", indent);
            sb.AppendLine($"{indent}ignoreZombieDensity = true,");
        }

        if (c.OnlyOne)
        {
            EmitComment(sb, comments, $"{path}.onlyOne", indent);
            sb.AppendLine($"{indent}onlyOne = true,");
        }

        // The game's Java parser requires non-procedural containers to have both
        // "rolls" and "items" keys present. Always emit them unless procedural=true.
        EmitComment(sb, comments, $"{path}.rolls", indent);
        sb.AppendLine($"{indent}rolls = {c.ItemRolls},");

        if (c.FillRand)
        {
            EmitComment(sb, comments, $"{path}.fillRand", indent);
            sb.AppendLine($"{indent}fillRand = true,");
        }

        if (c.Procedural)
        {
            EmitComment(sb, comments, $"{path}.procedural", indent);
            sb.AppendLine($"{indent}procedural = true,");
        }

        if (c.DontSpawnAmmo)
        {
            EmitComment(sb, comments, $"{path}.dontSpawnAmmo", indent);
            sb.AppendLine($"{indent}dontSpawnAmmo = true,");
        }

        // Items — write as reference if tracked, otherwise inline.
        if (c.ItemsReference is not null)
        {
            EmitComment(sb, comments, $"{path}.items", indent);
            sb.AppendLine($"{indent}items = {c.ItemsReference},");
        }
        else
        {
            string itemsPath = $"{path}.items";
            EmitComment(sb, comments, itemsPath, indent);
            sb.AppendLine($"{indent}items = {{");
            if (c.ItemChances.Count > 0)
                WriteItemList(sb, c.ItemChances, indent + "\t", itemsPath, comments);
            sb.AppendLine($"{indent}}},");
        }

        if (c.JunkReference is not null)
        {
            EmitComment(sb, comments, $"{path}.junk", indent);
            sb.AppendLine($"{indent}junk = {c.JunkReference},");
        }
        else if (c.JunkChances.Count > 0 || c.JunkRolls > 0)
        {
            WriteJunkBlock(sb, c, indent, path, comments, isProcedural);
        }

        if (c.ProcListEntries.Count > 0)
        {
            WriteProcList(sb, c.ProcListEntries, indent, path, comments);
        }
        else if (c.Procedural) // Procedural but no proclist
        {
            sb.AppendLine(indent + "procList = {\n" + indent + "\n" + indent + "}");
        }

        if (c.MaxMap.HasValue)
        {
            EmitComment(sb, comments, $"{path}.maxMap", indent);
            sb.AppendLine($"{indent}maxMap = {c.MaxMap.Value},");
        }

        if (c.StashChance.HasValue)
        {
            EmitComment(sb, comments, $"{path}.stashChance", indent);
            sb.AppendLine($"{indent}stashChance = {c.StashChance.Value},");
        }

        EmitComment(sb, comments, $"{path}.__trailing", indent);
    }

    // ── Item list ──

    private static void WriteItemList(StringBuilder sb, List<Item> items, string indent, string path, CommentMap? comments)
    {
        for (int i = 0; i < items.Count; i++)
        {
            EmitComment(sb, comments, $"{path}[{i}]", indent);
            var item = items[i];
            if (string.IsNullOrEmpty(item.Name))
            {
                // Orphaned chance value (no preceding item name) — write bare number
                // to preserve malformed original data during roundtrip.
                sb.Append(indent);
                sb.Append(FormatNumber(item.Chance));
                sb.AppendLine(",");
            }
            else
            {
                sb.Append(indent);
                sb.Append('"');
                sb.Append(item.Name);
                sb.Append("\", ");
                sb.Append(FormatNumber(item.Chance));
                sb.AppendLine(",");
            }
        }
        EmitComment(sb, comments, $"{path}.__trailing", indent);
    }

    // ── Junk block ──

    private static void WriteJunkBlock(StringBuilder sb, ItemParent parent, string indent, string path, CommentMap? comments, bool isProcedural = false)
    {
        string junkPath = $"{path}.junk";
        EmitComment(sb, comments, junkPath, indent);
        sb.AppendLine($"{indent}junk = {{");
        var inner = indent + "\t";

        // Java's ExtractContainersFromLua is called recursively on junk
        // and unconditionally reads rolls (no containsKey check). Always emit.
        EmitComment(sb, comments, $"{junkPath}.rolls", inner);
        sb.AppendLine($"{inner}rolls = {parent.JunkRolls},");

        if (parent.JunkIgnoreZombieDensity)
        {
            EmitComment(sb, comments, $"{junkPath}.ignoreZombieDensity", inner);
            sb.AppendLine($"{inner}ignoreZombieDensity = true,");
        }

            string junkItemsPath = $"{junkPath}.items";
            EmitComment(sb, comments, junkItemsPath, inner);
            sb.AppendLine($"{inner}items = {{");
            if (parent.JunkChances.Count > 0)
            {
                WriteItemList(sb, parent.JunkChances, inner + "\t", junkItemsPath, comments);
            }
            else
            {
                sb.AppendLine();
            }
            // items close inside junk never has a trailing comma.
            sb.AppendLine($"{inner}}}");

        EmitComment(sb, comments, $"{junkPath}.__trailing", inner);
        // junk close: comma in distributions, no comma in procedural.
        sb.AppendLine(isProcedural ? $"{indent}}}" : $"{indent}}},");
    }

    // ── Proc list entries ──

    private static void WriteProcList(StringBuilder sb, List<ProcListEntry> entries, string indent, string path, CommentMap? comments)
    {
        string procPath = $"{path}.procList";
        EmitComment(sb, comments, procPath, indent);
        sb.AppendLine($"{indent}procList = {{");
        var inner = indent + "\t";

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            EmitComment(sb, comments, $"{procPath}[{i}]", inner);
            sb.AppendLine($"{inner}{{");
            var field = inner + "\t";

            sb.AppendLine($"{field}name = \"{entry.Name}\",");

            if (entry.Min != 0)
                sb.AppendLine($"{field}min = {entry.Min},");
            if (entry.Max != 0)
                sb.AppendLine($"{field}max = {entry.Max},");
            if (entry.WeightChance != 0)
                sb.AppendLine($"{field}weightChance = {entry.WeightChance},");

            if (!string.IsNullOrEmpty(entry.ForceForTiles))
                sb.AppendLine($"{field}forceForTiles = \"{entry.ForceForTiles}\",");
            if (!string.IsNullOrEmpty(entry.ForceForRooms))
                sb.AppendLine($"{field}forceForRooms = \"{entry.ForceForRooms}\",");
            if (!string.IsNullOrEmpty(entry.ForceForZones))
                sb.AppendLine($"{field}forceForZones = \"{entry.ForceForZones}\",");
            if (!string.IsNullOrEmpty(entry.ForceForItems))
                sb.AppendLine($"{field}forceForItems = \"{entry.ForceForItems}\",");

            sb.AppendLine($"{inner}}},");
        }

        EmitComment(sb, comments, $"{procPath}.__trailing", inner);
        sb.AppendLine($"{indent}}},");
    }

    // ── Comment emission ──

    /// <summary>
    /// Looks up a comment block by path and emits each line re-indented at the given level.
    /// </summary>
    private static void EmitComment(StringBuilder sb, CommentMap? comments, string path, string indent)
    {
        if (comments is null || !comments.TryGet(path, out var block))
            return;

        var lines = block.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
                sb.AppendLine();
            else
                sb.AppendLine($"{indent}{line}");
        }
    }

    /// <summary>
    /// Emits a comment block verbatim (no re-indenting). Used for the footer
    /// which contains code (functions, event registrations) that must be preserved as-is.
    /// </summary>
    private static void EmitVerbatim(StringBuilder sb, CommentMap? comments, string path)
    {
        if (comments is null || !comments.TryGet(path, out var block))
            return;
        sb.AppendLine(block);
    }

    // ── Distribution_*.lua reference file writer ──

    /// <summary>
    /// Writes a self-contained Distribution_*.lua file that defines a single
    /// global table entry (e.g. ClutterTables.DeskJunk or BagsAndContainers.ProduceStorage_Apple).
    ///
    /// Junk entries preserve the intermediate items table that other files may reference directly
    /// (e.g. Distributions.lua uses ClutterTables.BinItems directly in its items field).
    /// Container entries are written inline.
    /// </summary>
    public static string WriteReferenceFileContent(
        string     refPath,
        ItemParent source)
    {
        int dot = refPath.IndexOf('.');
        var globalName = refPath[..dot];
        var entryName  = refPath[(dot + 1)..];

        var sb = new StringBuilder(512);
        sb.AppendLine($"{globalName} = {globalName} or {{}}");
        sb.AppendLine();

        bool isJunkEntry = source.JunkReference == refPath || IsJunkSource(source, refPath);

        if (isJunkEntry)
        {
            // Write the items list as a named table so other files can reference it.
            string itemsName = source.JunkItemsReference is not null
                ? source.JunkItemsReference[(source.JunkItemsReference.IndexOf('.') + 1)..]
                : entryName.Replace("Junk", "Items");

            sb.AppendLine($"{globalName}.{itemsName} = {{");
            WriteItemList(sb, source.JunkChances, "\t", "", null);
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine($"{globalName}.{entryName} = {{");
            if (source.JunkRolls > 0)
                sb.AppendLine($"\trolls = {source.JunkRolls},");
            if (source.JunkIgnoreZombieDensity)
                sb.AppendLine("\tignoreZombieDensity = true,");
            sb.AppendLine($"\titems = {globalName}.{itemsName},");
            sb.AppendLine("}");
        }
        else if (source is Container c)
        {
            // BagsAndContainers.* — write as a plain container object.
            sb.AppendLine($"{globalName}.{entryName} = {{");
            WriteContainer(sb, c, "\t", "", null);
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static bool IsJunkSource(ItemParent source, string refPath) =>
        source.JunkReferenceFile is not null &&
        source.JunkChances.Count > 0;

    // ── Number formatting ──

    private static string FormatNumber(double value)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (value == Math.Floor(value))
            return ((long)value).ToString();
        return value.ToString("G");
    }
}
