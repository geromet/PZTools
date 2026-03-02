using Core.Filtering;

namespace UI.Controls;

public class ContainerFilterState
{
    private TriState _procListFilter;
    private TriState _rollsFilter;
    private TriState _itemsFilter;
    private TriState _junkFilter;
    private TriState _proceduralFilter;
    private TriState _invalidFilter;
    private TriState _distributionItemsFilter;
    private TriState _defaultFilter;

    public bool AutoFilter { get; set; }
    public bool ShowEmpty { get; set; }

    public ref TriState GetRef(string? tag)
    {
        if (tag == "Rolls") return ref _rollsFilter;
        if (tag == "Items") return ref _itemsFilter;
        if (tag == "Junk") return ref _junkFilter;
        if (tag == "Procedural") return ref _proceduralFilter;
        if (tag == "Invalid") return ref _invalidFilter;
        if (tag == "ProcList") return ref _procListFilter;
        return ref _defaultFilter;
    }

    public void SyncFromContentFilters(
        (TriState ProcList, TriState Rolls, TriState Items, TriState Junk,
         TriState Procedural, TriState Invalid, TriState DistributionItems) f)
    {
        _procListFilter = f.ProcList;
        _rollsFilter = f.Rolls;
        _itemsFilter = f.Items;
        _junkFilter = f.Junk;
        _proceduralFilter = f.Procedural;
        _invalidFilter = f.Invalid;
        _distributionItemsFilter = f.DistributionItems;
    }

    public void ClearAll()
    {
        _procListFilter = TriState.Ignored;
        _rollsFilter = TriState.Ignored;
        _itemsFilter = TriState.Ignored;
        _junkFilter = TriState.Ignored;
        _proceduralFilter = TriState.Ignored;
        _invalidFilter = TriState.Ignored;
        _distributionItemsFilter = TriState.Ignored;
        AutoFilter = false;
    }

    public bool IsContainerVisible(Data.Data.Container c) =>
        ContainerFilter.IsVisible(c,
            _procListFilter, _rollsFilter, _itemsFilter,
            _junkFilter, _proceduralFilter, _invalidFilter);
}
