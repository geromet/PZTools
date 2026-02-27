using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DataInput.Data;

namespace UI.Controls;

public partial class DistributionListControl : UserControl
{
    private List<Distribution> _all = [];
    private string? _activeFilter; // null = "All"

    public event Action<Distribution?>? SelectionChanged;

    public DistributionListControl()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => ApplyFilter();
    }

    public void Load(IReadOnlyList<Distribution> distributions)
    {
        _all = [.. distributions];
        _activeFilter = null;
        SearchBox.Text = string.Empty;
        UpdatePillStyles();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;

        IEnumerable<Distribution> result = _all;

        if (_activeFilter is not null)
            result = result.Where(d => d.Type.ToString() == _activeFilter);

        if (query.Length > 0)
            result = result.Where(d => d.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        var filtered = result.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
        DistList.ItemsSource = filtered;
        CountText.Text = $"{filtered.Count} / {_all.Count}";
    }

    private void FilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        var tag = btn.Tag as string; // null = "All"
        // Toggle off if already active, otherwise switch to new filter
        _activeFilter = (_activeFilter == tag) ? null : tag;
        // "All" pill (tag=null) always sets null
        if (tag is null) _activeFilter = null;

        UpdatePillStyles();
        ApplyFilter();
    }

    private void UpdatePillStyles()
    {
        foreach (var child in FilterPills.Children)
        {
            if (child is not Button btn) continue;
            var tag = btn.Tag as string;
            if (_activeFilter == tag)
                btn.Classes.Add("active");
            else
                btn.Classes.Remove("active");
        }
    }

    private void DistList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectionChanged?.Invoke(DistList.SelectedItem as Distribution);
    }
}
