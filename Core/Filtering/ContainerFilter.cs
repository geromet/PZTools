using Data.Data;

namespace Core.Filtering;

public static class ContainerFilter
{
    public static bool IsVisible(Container c,
        TriState procList, TriState rolls, TriState items,
        TriState junk, TriState procedural, TriState invalid)
    {
        if (procList != TriState.Ignored)
        {
            bool has = c.ProcListEntries.Count > 0;
            if ((procList == TriState.Include) != has) return false;
        }
        if (rolls != TriState.Ignored)
        {
            bool has = c.ItemRolls > 0;
            if ((rolls == TriState.Include) != has) return false;
        }
        if (items != TriState.Ignored)
        {
            bool has = c.ItemChances.Count > 0;
            if ((items == TriState.Include) != has) return false;
        }
        if (junk != TriState.Ignored)
        {
            bool has = c.JunkChances.Count > 0;
            if ((junk == TriState.Include) != has) return false;
        }
        if (procedural != TriState.Ignored)
        {
            if ((procedural == TriState.Include) != c.Procedural) return false;
        }
        if (invalid != TriState.Ignored)
        {
            bool inv = IsInvalid(c);
            if ((invalid == TriState.Include) != inv) return false;
        }
        return true;
    }

    public static bool IsInvalid(Container c)
    {
        bool hasItems = c.ItemChances.Count > 0;
        bool hasJunk = c.JunkChances.Count > 0;
        bool hasProcList = c.ProcListEntries.Count > 0;
        bool hasRolls = c.ItemRolls > 0;

        if (!hasItems && !hasJunk && !hasProcList) return true;
        if (hasRolls && !hasItems && !hasJunk) return true;
        if (c.Procedural && !hasProcList) return true;

        return false;
    }
}
