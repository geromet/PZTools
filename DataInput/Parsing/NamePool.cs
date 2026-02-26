namespace DataInput.Parsing;

/// <summary>
/// Per-parse-run string intern pool.
/// Item names such as "Base.Axe" appear hundreds of times across mods.
/// Without interning, each Distribution gets its own heap-allocated copy of the same string.
/// With interning, all references point to a single string instance per unique name.
///
/// Scoped to the parse run — Clear() is called at the start of each MapAll invocation
/// so stale strings from previous runs are not retained.
///
/// Uses Ordinal comparison — item names are ASCII identifiers, no culture rules needed.
/// </summary>
internal sealed class NamePool
{
    // Pre-sized to a reasonable estimate for a vanilla + modded install.
    // This avoids the first several doublings on large mod lists.
    private readonly Dictionary<string, string> _pool =
        new(4096, StringComparer.Ordinal);

    /// <summary>
    /// Returns the canonical string reference for <paramref name="value"/>.
    /// If the value has been seen before, returns the already-stored reference
    /// so callers hold the same object rather than duplicate content.
    /// </summary>
    internal string Intern(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (_pool.TryGetValue(value, out var existing))
            return existing;

        _pool[value] = value;
        return value;
    }

    internal void Clear() => _pool.Clear();
}