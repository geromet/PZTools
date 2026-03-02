using Avalonia.Controls;
using Core.Filtering;

namespace UI.Controls;

public static class FilterPillHelper
{
    public static void ApplyTriStateStyles(Panel panel, ContentFilterSet filters)
    {
        foreach (var child in panel.Children)
        {
            if (child is not Button btn) continue;
            var state = filters.GetRef(btn.Tag as string);
            btn.Classes.Remove("include");
            btn.Classes.Remove("exclude");
            if (state == TriState.Include) btn.Classes.Add("include");
            else if (state == TriState.Exclude) btn.Classes.Add("exclude");
        }
    }
}
