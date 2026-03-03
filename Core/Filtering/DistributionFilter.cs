using System.Text.RegularExpressions;
using Data.Data;

namespace Core.Filtering;

public record FilterCriteria(
    string? TypeFilter,
    TriState ProcList,
    TriState Rolls,
    TriState Items,
    TriState Junk,
    TriState Procedural,
    TriState NoContent,
    TriState Invalid,
    TriState DistributionItems,
    string SearchQuery);

public static class DistributionFilter
{
    public static List<Distribution> Apply(IReadOnlyList<Distribution> all, FilterCriteria c)
    {
        IEnumerable<Distribution> result = all;

        // Type filter
        if (c.TypeFilter is not null)
            result = result.Where(d => d.Type.ToString() == c.TypeFilter);

        // Content filters (conjunctive per-container)
        if (HasAnyContentFilter(c))
            result = result.Where(d => MatchesContentFilters(d, c));

        // Structural filters (distribution-level)
        if (c.NoContent != TriState.Ignored)
        {
            bool want = c.NoContent == TriState.Include;
            result = result.Where(d => HasNoContent(d) == want);
        }
        if (c.Invalid != TriState.Ignored)
        {
            bool want = c.Invalid == TriState.Include;
            result = result.Where(d => HasInvalidContainers(d) == want);
        }
        if (c.DistributionItems != TriState.Ignored)
        {
            bool want = c.DistributionItems == TriState.Include;
            result = result.Where(d => HasDirectItems(d) == want);
        }

        // Regex search with graceful fallback
        if (c.SearchQuery.Length > 0)
        {
            Regex? regex = null;
            try { regex = new Regex(c.SearchQuery, RegexOptions.IgnoreCase); } catch { }

            result = regex is not null
                ? result.Where(d => regex.IsMatch(d.Name))
                : result.Where(d => d.Name.Contains(c.SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        return result.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool HasAnyActiveFilter(FilterCriteria c)
    {
        return c.TypeFilter is not null
            || c.ProcList != TriState.Ignored
            || c.Rolls != TriState.Ignored
            || c.Items != TriState.Ignored
            || c.Junk != TriState.Ignored
            || c.Procedural != TriState.Ignored
            || c.NoContent != TriState.Ignored
            || c.Invalid != TriState.Ignored
            || c.DistributionItems != TriState.Ignored
            || c.SearchQuery.Length > 0;
    }

    private static bool HasAnyContentFilter(FilterCriteria c)
    {
        return c.ProcList != TriState.Ignored
            || c.Rolls != TriState.Ignored
            || c.Items != TriState.Ignored
            || c.Junk != TriState.Ignored
            || c.Procedural != TriState.Ignored;
    }

    private static bool MatchesContentFilters(Distribution d, FilterCriteria c)
    {
        foreach (var container in d.Containers)
        {
            if (ContainerMatchesAllFilters(container, c))
                return true;
        }

        if (d.ItemChances.Count > 0 || d.JunkChances.Count > 0 || d.ItemRolls > 0)
        {
            if (VirtualContainerMatchesAllFilters(d, c))
                return true;
        }

        return false;
    }

    private static bool ContainerMatchesAllFilters(Container container, FilterCriteria c) =>
        ContainerFilter.IsVisible(container,
            c.ProcList, c.Rolls, c.Items, c.Junk, c.Procedural, TriState.Ignored);

    private static bool VirtualContainerMatchesAllFilters(Distribution d, FilterCriteria c)
    {
        if (c.ProcList != TriState.Ignored)
        {
            if (c.ProcList == TriState.Include) return false;
        }
        if (c.Rolls != TriState.Ignored)
        {
            bool has = d.ItemRolls > 0;
            if ((c.Rolls == TriState.Include) != has) return false;
        }
        if (c.Items != TriState.Ignored)
        {
            bool has = d.ItemChances.Count > 0;
            if ((c.Items == TriState.Include) != has) return false;
        }
        if (c.Junk != TriState.Ignored)
        {
            bool has = d.JunkChances.Count > 0;
            if ((c.Junk == TriState.Include) != has) return false;
        }
        if (c.Procedural != TriState.Ignored)
        {
            if (c.Procedural == TriState.Include) return false;
        }
        return true;
    }

    private static bool HasDirectItems(Distribution d)
    {
        return d.ItemChances.Count > 0;
    }

    private static bool HasNoContent(Distribution d)
    {
        return d.Containers.Count == 0
            && d.ItemChances.Count == 0
            && d.JunkChances.Count == 0;
    }

    private static bool HasInvalidContainers(Distribution d) =>
        d.Containers.Any(ContainerFilter.IsInvalid);
}
