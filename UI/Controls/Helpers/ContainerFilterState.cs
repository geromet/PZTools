using Core.Filtering;

namespace UI.Controls;

public class ContainerFilterState
{
    public ContentFilterSet Content { get; } = new();
    public bool AutoFilter { get; set; }
    public bool ShowEmpty { get; set; }

    public ref TriState GetRef(string? tag) => ref Content.GetRef(tag);

    public void SyncFrom(ContentFilterSet source) => Content.CopyFrom(source);

    public void ClearAll()
    {
        Content.ClearAll();
        AutoFilter = false;
    }

    public bool IsContainerVisible(Data.Data.Container c) =>
        Content.MatchesContainer(c);
}
