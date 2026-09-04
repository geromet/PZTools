using Core.Filtering;

namespace UI.Controls;

/// <summary>
/// Shared UI search helpers. Matching semantics come from Core's SearchQueryMatcher; this type
/// keeps only presentation-specific relevance ordering.
/// </summary>
public static class SearchHelper
{
    /// <summary>
    /// Returns a predicate that matches names against the query, or null when the query is empty (= match all).
    /// </summary>
    public static Func<string, bool>? BuildPredicate(string search)
    {
        var matcher = SearchQueryMatcher.Parse(search);
        return matcher.IsEmpty ? null : matcher.IsMatch;
    }

    /// <summary>
    /// Filters and sorts names by relevance to the query.
    /// Query starting with '/' is treated as a regex. Otherwise:
    /// exact match → prefix match → contains match, each group sorted OrdinalIgnoreCase.
    /// Empty query returns all names sorted alphabetically.
    /// </summary>
    public static List<string> SortedByRelevance(IEnumerable<string> names, string query)
    {
        var matcher = SearchQueryMatcher.Parse(query);

        if (matcher.IsEmpty)
            return [.. names.Order(StringComparer.OrdinalIgnoreCase)];

        if (matcher.IsRegex)
            return [.. names.Where(matcher.IsMatch).Order(StringComparer.OrdinalIgnoreCase)];

        return [.. names
            .Where(matcher.IsMatch)
            .OrderBy(n => n.Equals(query, StringComparison.OrdinalIgnoreCase) ? 0
                        : n.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)];
    }
}
