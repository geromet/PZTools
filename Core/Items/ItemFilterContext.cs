namespace Core.Items;

/// <summary>
/// Snapshot of the Items Explorer filter state passed to detail controls
/// so they can hide non-matching occurrences when Auto Filter is on.
/// </summary>
public readonly record struct ItemFilterContext(string? DistTypeFilter, bool? IsJunk)
{
    public bool IsActive => DistTypeFilter is not null || IsJunk.HasValue;

    public bool Matches(ItemOccurrence occ) =>
        (DistTypeFilter is null || occ.Distribution.Type.ToString() == DistTypeFilter) &&
        (!IsJunk.HasValue || occ.IsJunk == IsJunk.Value);
}
