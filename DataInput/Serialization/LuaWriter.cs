using System.Text;
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
    public static string WriteProceduralFile(IReadOnlyList<Distribution> distributions)
    {
        var procedural = distributions
            .Where(d => d.Type == DistributionType.Procedural)
            .OrderBy(d => d.OriginalOrder)
            .ToList();

        var sb = new StringBuilder(procedural.Count * 512);
        sb.AppendLine("ProceduralDistributions = {};");
        sb.AppendLine("ProceduralDistributions.list = {};");
        sb.AppendLine();

        for (int i = 0; i < procedural.Count; i++)
        {
            var d = procedural[i];
            sb.Append("ProceduralDistributions.list.");
            sb.Append(d.Name);
            sb.AppendLine(" = {");
            WriteDistributionBody(sb, d, "\t");
            sb.AppendLine("}");
            if (i < procedural.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes all non-procedural distributions to a complete Distributions.lua file.
    /// </summary>
    public static string WriteDistributionsFile(IReadOnlyList<Distribution> distributions)
    {
        var nonProc = distributions
            .Where(d => d.Type != DistributionType.Procedural)
            .OrderBy(d => d.OriginalOrder)
            .ToList();

        var sb = new StringBuilder(nonProc.Count * 512);
        sb.AppendLine("Distributions = {};");
        sb.AppendLine("Distributions[1] = {};");
        sb.AppendLine();

        for (int i = 0; i < nonProc.Count; i++)
        {
            var d = nonProc[i];
            sb.Append("Distributions[1][\"");
            sb.Append(d.Name);
            sb.AppendLine("\"] = {");
            WriteDistributionBody(sb, d, "\t");
            sb.AppendLine("}");
            if (i < nonProc.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Distribution body ──

    private static void WriteDistributionBody(StringBuilder sb, Distribution d, string indent)
    {
        // Scalar fields
        if (d.ItemRolls != 0)
            sb.AppendLine($"{indent}rolls = {d.ItemRolls},");

        if (d.IsShop)
            sb.AppendLine($"{indent}isShop = true,");

        if (d.DontSpawnAmmo)
            sb.AppendLine($"{indent}DontSpawnAmmo = true,");

        if (d.FillRand)
            sb.AppendLine($"{indent}FillRand = true,");

        if (d.MaxMap.HasValue)
            sb.AppendLine($"{indent}MaxMap = {d.MaxMap.Value},");

        if (d.StashChance.HasValue)
            sb.AppendLine($"{indent}StashChance = {d.StashChance.Value},");

        // Direct items
        if (d.ItemChances.Count > 0)
        {
            sb.AppendLine($"{indent}items = {{");
            WriteItemList(sb, d.ItemChances, indent + "\t");
            sb.AppendLine($"{indent}}},");
        }

        // Direct junk
        if (d.JunkChances.Count > 0 || d.JunkRolls > 0)
        {
            WriteJunkBlock(sb, d, indent);
        }

        // Containers
        foreach (var c in d.Containers)
        {
            sb.AppendLine($"{indent}{c.Name} = {{");
            WriteContainer(sb, c, indent + "\t");
            sb.AppendLine($"{indent}}},");
        }
    }

    // ── Container ──

    private static void WriteContainer(StringBuilder sb, Container c, string indent)
    {
        if (c.ItemRolls != 0)
            sb.AppendLine($"{indent}rolls = {c.ItemRolls},");

        if (c.FillRand)
            sb.AppendLine($"{indent}fillRand = true,");

        if (c.Procedural)
            sb.AppendLine($"{indent}procedural = true,");

        if (c.DontSpawnAmmo)
            sb.AppendLine($"{indent}dontSpawnAmmo = true,");

        if (c.ItemChances.Count > 0)
        {
            sb.AppendLine($"{indent}items = {{");
            WriteItemList(sb, c.ItemChances, indent + "\t");
            sb.AppendLine($"{indent}}},");
        }

        if (c.JunkChances.Count > 0 || c.JunkRolls > 0)
        {
            WriteJunkBlock(sb, c, indent);
        }

        if (c.ProcListEntries.Count > 0)
        {
            WriteProcList(sb, c.ProcListEntries, indent);
        }
    }

    // ── Item list ──

    private static void WriteItemList(StringBuilder sb, List<Item> items, string indent)
    {
        foreach (var item in items)
        {
            sb.Append(indent);
            sb.Append('"');
            sb.Append(item.Name);
            sb.Append("\",");
            sb.AppendLine();
            sb.Append(indent);
            sb.Append(FormatNumber(item.Chance));
            sb.AppendLine(",");
        }
    }

    // ── Junk block ──

    private static void WriteJunkBlock(StringBuilder sb, ItemParent parent, string indent)
    {
        sb.AppendLine($"{indent}junk = {{");
        var inner = indent + "\t";

        if (parent.JunkRolls > 0)
            sb.AppendLine($"{inner}rolls = {parent.JunkRolls},");

        if (parent.JunkChances.Count > 0)
        {
            sb.AppendLine($"{inner}items = {{");
            WriteItemList(sb, parent.JunkChances, inner + "\t");
            sb.AppendLine($"{inner}}},");
        }

        sb.AppendLine($"{indent}}},");
    }

    // ── Proc list entries ──

    private static void WriteProcList(StringBuilder sb, List<ProcListEntry> entries, string indent)
    {
        sb.AppendLine($"{indent}procList = {{");
        var inner = indent + "\t";

        foreach (var entry in entries)
        {
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

        sb.AppendLine($"{indent}}},");
    }

    // ── Number formatting ──

    private static string FormatNumber(double value)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (value == Math.Floor(value))
            return ((long)value).ToString();
        return value.ToString("G");
    }
}
