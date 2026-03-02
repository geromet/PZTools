using Core.Filtering;

namespace UI.Controls;

public class FilterState
{
    private TriState _defaultFilter;
    private TriState _procListFilter;
    private TriState _rollsFilter;
    private TriState _itemsFilter;
    private TriState _junkFilter;
    private TriState _proceduralFilter;
    private TriState _noContentFilter;
    private TriState _invalidFilter;
    private TriState _distributionItemsFilter;

    public string? ActiveTypeFilter { get; set; }

    public (TriState ProcList, TriState Rolls, TriState Items, TriState Junk, TriState Procedural,
        TriState Invalid, TriState DistributionItems) ContentFilters
        => (_procListFilter, _rollsFilter, _itemsFilter, _junkFilter, _proceduralFilter,
            _invalidFilter, _distributionItemsFilter);

    public void Reset()
    {
        ActiveTypeFilter = null;
        _procListFilter = _rollsFilter = _itemsFilter = _junkFilter = _proceduralFilter =
            _noContentFilter = _invalidFilter = _distributionItemsFilter =
            _defaultFilter = TriState.Ignored;
    }

    public FilterCriteria BuildCriteria(string searchQuery) => new(
        ActiveTypeFilter, _procListFilter, _rollsFilter,
        _itemsFilter, _junkFilter, _proceduralFilter,
        _noContentFilter, _invalidFilter, _distributionItemsFilter,
        searchQuery);

    public void ToggleTypeFilter(string? tag)
    {
        ActiveTypeFilter = (ActiveTypeFilter == tag) ? null : tag;
        if (tag is null) ActiveTypeFilter = null;
    }

    public ref TriState GetRef(string? tag)
    {
        if (tag == "Rolls") return ref _rollsFilter;
        if (tag == "Items") return ref _itemsFilter;
        if (tag == "Junk") return ref _junkFilter;
        if (tag == "Procedural") return ref _proceduralFilter;
        if (tag == "ProcList") return ref _procListFilter;
        if (tag == "Invalid") return ref _invalidFilter;
        if (tag == "NoContent") return ref _noContentFilter;
        if (tag == "DistributionItems") return ref _distributionItemsFilter;
        return ref _defaultFilter;
    }
}
