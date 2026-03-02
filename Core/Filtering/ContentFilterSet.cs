using Data.Data;

namespace Core.Filtering;

public class ContentFilterSet
{
    private TriState _procList;
    private TriState _rolls;
    private TriState _items;
    private TriState _junk;
    private TriState _procedural;
    private TriState _invalid;
    private TriState _default;

    public ref TriState GetRef(string? tag)
    {
        if (tag == "ProcList") return ref _procList;
        if (tag == "Rolls") return ref _rolls;
        if (tag == "Items") return ref _items;
        if (tag == "Junk") return ref _junk;
        if (tag == "Procedural") return ref _procedural;
        if (tag == "Invalid") return ref _invalid;
        return ref _default;
    }

    public void ClearAll()
    {
        _procList = _rolls = _items = _junk =
            _procedural = _invalid = _default = TriState.Ignored;
    }

    public void CopyFrom(ContentFilterSet other)
    {
        _procList = other._procList;
        _rolls = other._rolls;
        _items = other._items;
        _junk = other._junk;
        _procedural = other._procedural;
        _invalid = other._invalid;
    }

    public bool MatchesContainer(Container c) =>
        ContainerFilter.IsVisible(c, _procList, _rolls, _items,
            _junk, _procedural, _invalid);

    public bool HasAnyActive =>
        _procList != TriState.Ignored || _rolls != TriState.Ignored ||
        _items != TriState.Ignored || _junk != TriState.Ignored ||
        _procedural != TriState.Ignored || _invalid != TriState.Ignored;

    public FilterCriteria BuildCriteria(string? typeFilter,
        TriState noContent, TriState distributionItems, string searchQuery) =>
        new(typeFilter, _procList, _rolls, _items, _junk, _procedural,
            noContent, _invalid, distributionItems, searchQuery);
}
