using System;
using Avalonia.Controls;
using Avalonia.Input;
using Core.Filtering;

namespace UI.Controls;

public static class FilterPillHelper
{
    public static void WireTriStatePills(Panel panel, ITriStateFilterSource source, Action onChanged)
    {
        foreach (var child in panel.Children)
        {
            if (child is not Button btn) continue;
            btn.Click += (s, _) =>
            {
                if (s is not Button b) return;
                ref var state = ref source.GetRef(b.Tag as string);
                state = state == TriState.Include ? TriState.Ignored : TriState.Include;
                onChanged();
            };
            btn.PointerPressed += (s, e) =>
            {
                if (s is not Button b) return;
                if (!e.GetCurrentPoint(b).Properties.IsRightButtonPressed) return;
                ref var state = ref source.GetRef(b.Tag as string);
                state = state == TriState.Exclude ? TriState.Ignored : TriState.Exclude;
                onChanged();
                e.Handled = true;
            };
        }
    }

    public static void ApplyTriStateStyles(Panel panel, ITriStateFilterSource source)
    {
        foreach (var child in panel.Children)
        {
            if (child is not Button btn) continue;
            var state = source.GetRef(btn.Tag as string);
            btn.Classes.Remove("include");
            btn.Classes.Remove("exclude");
            if (state == TriState.Include) btn.Classes.Add("include");
            else if (state == TriState.Exclude) btn.Classes.Add("exclude");
        }
    }
}
