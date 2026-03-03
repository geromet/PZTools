using System.Text.RegularExpressions;

namespace UI.Controls;

/// <summary>
/// Shared search helpers for regex/substring matching and relevance-sorted name lists.
/// Text starting with '/' is treated as a regex; everything else is case-insensitive substring.
/// </summary>
public static class SearchHelper
{
    /// <summary>
    /// Returns a predicate that matches names against the query, or null when the query is empty (= match all).
    /// </summary>
    public static Func<string, bool>? BuildPredicate(string search)
    {
        if (string.IsNullOrEmpty(search)) return null;

        if (search.StartsWith('/'))
        {
            var pattern = search.Length > 1 ? search[1..] : string.Empty;
            try
            {
                var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return name => rx.IsMatch(name);
            }
            catch
            {
                return _ => false;
            }
        }

        return name => name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Filters and sorts names by relevance to the query.
    /// Query starting with '/' is treated as a regex. Otherwise:
    /// exact match → prefix match → contains match, each group sorted OrdinalIgnoreCase.
    /// Empty query returns all names sorted alphabetically.
    /// </summary>
    public static List<string> SortedByRelevance(IEnumerable<string> names, string query)
    {
        if (string.IsNullOrEmpty(query))
            return [.. names.Order(StringComparer.OrdinalIgnoreCase)];

        if (query.StartsWith('/'))
        {
            var pattern = query.Length > 1 ? query[1..] : string.Empty;
            try
            {
                var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return [.. names.Where(n => rx.IsMatch(n)).Order(StringComparer.OrdinalIgnoreCase)];
            }
            catch
            {
                return [];
            }
        }

        return [.. names
            .Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Equals(query, StringComparison.OrdinalIgnoreCase) ? 0
                        : n.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)];
    }
}
