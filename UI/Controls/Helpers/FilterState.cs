using Core.Filtering;

namespace UI.Controls;

public class FilterState : ITriStateFilterSource
{
    private TriState _noContentFilter;
    private TriState _distributionItemsFilter;

    public ContentFilterSet Content { get; } = new();
    public string? ActiveTypeFilter { get; set; }

    public void Reset()
    {
        ActiveTypeFilter = null;
        Content.ClearAll();
        _noContentFilter = _distributionItemsFilter = TriState.Ignored;
    }

    public FilterCriteria BuildCriteria(string searchQuery) =>
        Content.BuildCriteria(ActiveTypeFilter, _noContentFilter,
            _distributionItemsFilter, searchQuery);

    public void ToggleTypeFilter(string? tag)
    {
        ActiveTypeFilter = (ActiveTypeFilter == tag) ? null : tag;
        if (tag is null) ActiveTypeFilter = null;
    }

    public ref TriState GetRef(string? tag)
    {
        if (tag == "NoContent") return ref _noContentFilter;
        if (tag == "DistributionItems") return ref _distributionItemsFilter;
        return ref Content.GetRef(tag);
    }
}
