using Data.Data;

namespace Core.Items;

public class ItemIndex
{
    private readonly Dictionary<string, List<ItemOccurrence>> _map;

    private ItemIndex(Dictionary<string, List<ItemOccurrence>> map)
    {
        _map = map;
        SortedItems = [.. map.Keys.Order(StringComparer.OrdinalIgnoreCase)];
    }

    public IReadOnlyList<string> SortedItems { get; }

    public IReadOnlyList<ItemOccurrence> GetOccurrences(string name)
    {
        return _map.TryGetValue(name, out var list) ? list : [];
    }

    public IReadOnlyList<string> GetFiltered(string search, string? distTypeFilter, bool? isJunk)
    {
        var result = new List<string>();
        foreach (var name in SortedItems)
        {
            if (!string.IsNullOrEmpty(search) &&
                name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var occs = _map[name];

            // dist type filter: keep only if at least one occurrence matches
            if (distTypeFilter is not null)
            {
                if (!Enum.TryParse<DistributionType>(distTypeFilter, ignoreCase: true, out var filterType))
                {
                    result.Add(name);
                    continue;
                }
                if (!occs.Any(o => o.Distribution.Type == filterType)) continue;
            }

            // isJunk filter: true = only junk, false = only items, null = both
            if (isJunk.HasValue)
            {
                if (!occs.Any(o => o.IsJunk == isJunk.Value)) continue;
            }

            result.Add(name);
        }
        return result;
    }

    public static ItemIndex Build(IReadOnlyList<Distribution> distributions)
    {
        var map = new Dictionary<string, List<ItemOccurrence>>(StringComparer.OrdinalIgnoreCase);

        foreach (var dist in distributions)
        {
            AddItems(map, dist.ItemChances, dist, null, isJunk: false);
            AddItems(map, dist.JunkChances, dist, null, isJunk: true);

            foreach (var container in dist.Containers)
            {
                AddItems(map, container.ItemChances, dist, container, isJunk: false);
                AddItems(map, container.JunkChances, dist, container, isJunk: true);
            }
        }

        return new ItemIndex(map);
    }

    private static void AddItems(
        Dictionary<string, List<ItemOccurrence>> map,
        List<Item> items,
        Distribution dist,
        Container? container,
        bool isJunk)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var name = items[i].Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (!map.TryGetValue(name, out var list))
            {
                list = [];
                map[name] = list;
            }
            list.Add(new ItemOccurrence(dist, container, isJunk, i));
        }
    }
}
