using System.Text.RegularExpressions;

namespace Core.Filtering;

/// <summary>
/// Shared search contract for distribution/name filters.
/// A leading '/' selects regex mode; all other text is a literal case-insensitive substring.
/// Invalid regex queries deliberately match nothing instead of silently changing search mode.
/// </summary>
public sealed class SearchQueryMatcher
{
    private readonly string? _literalQuery;
    private readonly Regex? _regex;

    private SearchQueryMatcher(string query, string? literalQuery, Regex? regex, bool isValid)
    {
        Query = query;
        _literalQuery = literalQuery;
        _regex = regex;
        IsValid = isValid;
    }

    public string Query { get; }

    public bool IsEmpty => Query.Length == 0;

    public bool IsRegex => Query.StartsWith('/');

    public bool IsValid { get; }

    public static SearchQueryMatcher Parse(string? query)
    {
        query ??= string.Empty;

        if (query.Length == 0)
            return new SearchQueryMatcher(query, null, null, true);

        if (!query.StartsWith('/'))
            return new SearchQueryMatcher(query, query, null, true);

        try
        {
            var pattern = query.Length > 1 ? query[1..] : string.Empty;
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return new SearchQueryMatcher(query, null, regex, true);
        }
        catch (ArgumentException)
        {
            return new SearchQueryMatcher(query, null, null, false);
        }
    }

    public bool IsMatch(string value)
    {
        if (IsEmpty)
            return true;

        if (!IsValid)
            return false;

        if (_regex is not null)
            return _regex.IsMatch(value);

        return value.Contains(_literalQuery!, StringComparison.OrdinalIgnoreCase);
    }
}
