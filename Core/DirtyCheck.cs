using Data.Data;

namespace Core;

public static class DirtyCheck
{
    public static bool IsDistributionDirty(Distribution d)
    {
        if (d.IsDirty) return true;
        foreach (var c in d.Containers)
        {
            if (c.IsDirty) return true;
            foreach (var p in c.ProcListEntries)
                if (p.IsDirty) return true;
        }
        return false;
    }
}
