using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class DistributionDetailControl : UserControl
{
    private Distribution? _model;
    private UndoRedoStack? _undoRedo;
    private bool _loading;
    private bool _showToolbar = true;

    // Shared column proportions (remembered across distribution switches)
    private readonly SharedColumnLayout _sharedColumnLayout = new();

    // Tri-state container filters (remembered across distribution switches)
    private TriState _procListFilter;
    private TriState _rollsFilter;
    private TriState _itemsFilter;
    private TriState _junkFilter;
    private TriState _proceduralFilter;
    private TriState _invalidFilter;

    // Auto-filter state (remembered across distribution switches)
    private bool _autoFilter;

    // Show empty columns toggle (remembered across distribution switches)
    private bool _showEmpty;

    /// <summary>
    /// When auto-filter is on, this func is called to get current content filters
    /// from the distribution list control. Set by MainWindow.
    /// </summary>
    public Func<(TriState ProcList, TriState Rolls, TriState Items, TriState Junk, TriState Procedural, TriState Invalid)>? GetContentFilters { get; set; }

    public Distribution? Model => _model;

    public bool ShowToolbar
    {
        get => _showToolbar;
        set
        {
            _showToolbar = value;
            if (ToolbarPanel is not null) ToolbarPanel.IsVisible = value;
        }
    }

    public DistributionDetailControl()
    {
        InitializeComponent();

        // Wire right-click on container filter pills
        foreach (var child in ContainerFilterPills.Children)
        {
            if (child is Button btn)
                btn.PointerPressed += ContainerFilterPill_PointerPressed;
        }
    }

    public void Load(Distribution d, UndoRedoStack undoRedo)
    {
        _model = d;
        _undoRedo = undoRedo;
        _loading = true;
        try
        {
            EmptyPanel.IsVisible = false;
            DetailPanel.IsVisible = true;
            ToolbarPanel.IsVisible = _showToolbar;

            HeaderName.Text = d.Name;
            HeaderType.Text = d.Type.ToString();

            RollsBox.Text = d.ItemRolls.ToString();
            ShopCheck.IsChecked = d.IsShop;
            NoAmmoCheck.IsChecked = d.DontSpawnAmmo;

            bool hasMaxMap = d.MaxMap.HasValue;
            MaxMapLabel.IsVisible = MaxMapBox.IsVisible = hasMaxMap;
            MaxMapBox.Text = d.MaxMap?.ToString() ?? string.Empty;

            bool hasStash = d.StashChance.HasValue;
            StashLabel.IsVisible = StashBox.IsVisible = hasStash;
            StashBox.Text = d.StashChance?.ToString() ?? string.Empty;

            HeaderContainerCount.Text = d.Containers.Count.ToString();

            // Show distribution-level items (common for procedural distributions which have
            // items/junk directly on the distribution rather than in named sub-containers).
            bool hasDirectItems = d.ItemChances.Count > 0 || d.JunkChances.Count > 0;
            DirectItemsPanel.IsVisible = hasDirectItems;
            HeaderDirectItems.IsVisible = hasDirectItems;
            if (hasDirectItems)
            {
                DirectRollsBadge.Text = $"\u21bb {d.ItemRolls}";
                DirectCountBadge.Text = $"\u229e {d.ItemChances.Count}";
                HeaderDirectItemCount.Text = d.ItemChances.Count.ToString();
                DistItemsControl.Load(d.ItemChances, undoRedo, $"{d.Name}.items", d);
                JunkTab.IsVisible = d.JunkChances.Count > 0;
                if (d.JunkChances.Count > 0)
                    DistJunkControl.Load(d.JunkChances, undoRedo, $"{d.Name}.junk", d);
            }

            ContainersPanel.Children.Clear();
            const int autoExpandLimit = 10;
            for (int i = 0; i < d.Containers.Count; i++)
            {
                var ctrl = new ContainerControl();
                ctrl.Load(d.Containers[i], undoRedo, _sharedColumnLayout, _showEmpty);
                if (i < autoExpandLimit)
                    ctrl.ContainerExpander.IsExpanded = true;
                ContainersPanel.Children.Add(ctrl);
            }

            // If auto-filter is on, sync from distribution list
            if (_autoFilter && GetContentFilters is not null)
            {
                var f = GetContentFilters();
                _procListFilter = f.ProcList;
                _rollsFilter = f.Rolls;
                _itemsFilter = f.Items;
                _junkFilter = f.Junk;
                _proceduralFilter = f.Procedural;
                _invalidFilter = f.Invalid;
            }

            // Re-apply remembered container filter
            UpdateContainerFilterStyles();
            ApplyContainerFilter();
        }
        finally
        {
            _loading = false;
        }
    }

    public void ShowEmpty()
    {
        EmptyPanel.IsVisible = true;
        DetailPanel.IsVisible = false;
        ContainersPanel.Children.Clear();
        _model = null;
    }

    // ── Rolls ──

    private void RollsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        if (!int.TryParse(RollsBox.Text, out var newVal))
        {
            RollsBox.Text = _model.ItemRolls.ToString();
            return;
        }
        if (newVal == _model.ItemRolls) return;
        var old = _model.ItemRolls;
        _undoRedo.Push(new PropertyChangeAction<int>(
            $"{_model.Name}.Rolls: {old}\u2192{newVal}",
            v => { _model.ItemRolls = v; RollsBox.Text = v.ToString(); _model.IsDirty = true; },
            old, newVal));
    }

    // ── IsShop ──

    private void ShopCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        var newVal = ShopCheck.IsChecked == true;
        if (newVal == _model.IsShop) return;
        var old = _model.IsShop;
        _undoRedo.Push(new PropertyChangeAction<bool>(
            $"{_model.Name}.IsShop: {old}\u2192{newVal}",
            v => { _model.IsShop = v; ShopCheck.IsChecked = v; _model.IsDirty = true; },
            old, newVal));
    }

    // ── DontSpawnAmmo ──

    private void NoAmmoCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        var newVal = NoAmmoCheck.IsChecked == true;
        if (newVal == _model.DontSpawnAmmo) return;
        var old = _model.DontSpawnAmmo;
        _undoRedo.Push(new PropertyChangeAction<bool>(
            $"{_model.Name}.DontSpawnAmmo: {old}\u2192{newVal}",
            v => { _model.DontSpawnAmmo = v; NoAmmoCheck.IsChecked = v; _model.IsDirty = true; },
            old, newVal));
    }

    // ── MaxMap ──

    private void MaxMapBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        if (!int.TryParse(MaxMapBox.Text, out var newVal))
        {
            MaxMapBox.Text = _model.MaxMap?.ToString() ?? string.Empty;
            return;
        }
        if (newVal == _model.MaxMap) return;
        var old = _model.MaxMap ?? 0;
        _undoRedo.Push(new PropertyChangeAction<int>(
            $"{_model.Name}.MaxMap: {old}\u2192{newVal}",
            v => { _model.MaxMap = v; MaxMapBox.Text = v.ToString(); _model.IsDirty = true; },
            old, newVal));
    }

    // ── StashChance ──

    private void StashBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        if (!int.TryParse(StashBox.Text, out var newVal))
        {
            StashBox.Text = _model.StashChance?.ToString() ?? string.Empty;
            return;
        }
        if (newVal == _model.StashChance) return;
        var old = _model.StashChance ?? 0;
        _undoRedo.Push(new PropertyChangeAction<int>(
            $"{_model.Name}.StashChance: {old}\u2192{newVal}",
            v => { _model.StashChance = v; StashBox.Text = v.ToString(); _model.IsDirty = true; },
            old, newVal));
    }

    // ── Expand / Collapse All ──

    private void SetAllExpanded(bool expanded)
    {
        // Direct items expander
        if (DirectItemsPanel.IsVisible)
        {
            var expander = DirectItemsPanel.GetVisualDescendants().OfType<Expander>().FirstOrDefault();
            if (expander is not null) expander.IsExpanded = expanded;
        }
        // Container expanders
        foreach (var child in ContainersPanel.Children)
        {
            if (child is not ContainerControl cc) continue;
            cc.ContainerExpander.IsExpanded = expanded;
        }
    }

    private void ExpandAll_Click(object? sender, RoutedEventArgs e) => SetAllExpanded(true);
    private void CollapseAll_Click(object? sender, RoutedEventArgs e) => SetAllExpanded(false);

    // ── Show Empty ──

    private void ShowEmpty_Click(object? sender, RoutedEventArgs e)
    {
        _showEmpty = !_showEmpty;
        if (_showEmpty)
            ShowEmptyBtn.Classes.Add("active");
        else
            ShowEmptyBtn.Classes.Remove("active");

        // Reload current distribution to apply the change
        if (_model is not null && _undoRedo is not null)
            Load(_model, _undoRedo);
    }

    // ── Auto Filter / Clear ──

    private void AutoFilter_Click(object? sender, RoutedEventArgs e)
    {
        _autoFilter = !_autoFilter;

        if (_autoFilter && GetContentFilters is not null)
        {
            var f = GetContentFilters();
            _procListFilter = f.ProcList;
            _rollsFilter = f.Rolls;
            _itemsFilter = f.Items;
            _junkFilter = f.Junk;
            _proceduralFilter = f.Procedural;
            _invalidFilter = f.Invalid;
        }
        else if (!_autoFilter)
        {
            _procListFilter = TriState.Ignored;
            _rollsFilter = TriState.Ignored;
            _itemsFilter = TriState.Ignored;
            _junkFilter = TriState.Ignored;
            _proceduralFilter = TriState.Ignored;
            _invalidFilter = TriState.Ignored;
        }

        UpdateContainerFilterStyles();
        ApplyContainerFilter();
    }

    private void ClearFilters_Click(object? sender, RoutedEventArgs e)
    {
        _procListFilter = TriState.Ignored;
        _rollsFilter = TriState.Ignored;
        _itemsFilter = TriState.Ignored;
        _junkFilter = TriState.Ignored;
        _proceduralFilter = TriState.Ignored;
        _invalidFilter = TriState.Ignored;
        _autoFilter = false;
        UpdateContainerFilterStyles();
        ApplyContainerFilter();
    }

    // ── Container Filters (tri-state) ──

    private void ContainerFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string;
        // Left click: Ignored → Include → Ignored
        ref var state = ref GetContainerFilterRef(tag);
        state = state == TriState.Include ? TriState.Ignored : TriState.Include;
        _autoFilter = false; // manual override disables auto
        UpdateContainerFilterStyles();
        ApplyContainerFilter();
    }

    private void ContainerFilterPill_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button btn) return;
        var point = e.GetCurrentPoint(btn);
        if (!point.Properties.IsRightButtonPressed) return;

        var tag = btn.Tag as string;
        // Right click: Ignored → Exclude → Ignored
        ref var state = ref GetContainerFilterRef(tag);
        state = state == TriState.Exclude ? TriState.Ignored : TriState.Exclude;
        _autoFilter = false; // manual override disables auto
        UpdateContainerFilterStyles();
        ApplyContainerFilter();
        e.Handled = true;
    }

    private ref TriState GetContainerFilterRef(string? tag)
    {
        if (tag == "Rolls") return ref _rollsFilter;
        if (tag == "Items") return ref _itemsFilter;
        if (tag == "Junk") return ref _junkFilter;
        if (tag == "Procedural") return ref _proceduralFilter;
        if (tag == "Invalid") return ref _invalidFilter;
        return ref _procListFilter;
    }

    private void UpdateContainerFilterStyles()
    {
        // Tri-state pills
        foreach (var child in ContainerFilterPills.Children)
        {
            if (child is not Button btn) continue;
            var tag = btn.Tag as string;
            var state = GetContainerFilterRef(tag);
            btn.Classes.Remove("include");
            btn.Classes.Remove("exclude");
            if (state == TriState.Include)
                btn.Classes.Add("include");
            else if (state == TriState.Exclude)
                btn.Classes.Add("exclude");
        }

        // Auto filter button
        if (_autoFilter)
            AutoFilterBtn.Classes.Add("active");
        else
            AutoFilterBtn.Classes.Remove("active");
    }

    private void ApplyContainerFilter()
    {
        foreach (var child in ContainersPanel.Children)
        {
            if (child is not ContainerControl cc || cc.Model is null) continue;
            var c = cc.Model;
            bool visible = true;

            if (_procListFilter != TriState.Ignored)
            {
                bool has = c.ProcListEntries.Count > 0;
                visible &= (_procListFilter == TriState.Include) == has;
            }
            if (_rollsFilter != TriState.Ignored)
            {
                bool has = c.ItemRolls > 0;
                visible &= (_rollsFilter == TriState.Include) == has;
            }
            if (_itemsFilter != TriState.Ignored)
            {
                bool has = c.ItemChances.Count > 0;
                visible &= (_itemsFilter == TriState.Include) == has;
            }
            if (_junkFilter != TriState.Ignored)
            {
                bool has = c.JunkChances.Count > 0;
                visible &= (_junkFilter == TriState.Include) == has;
            }
            if (_proceduralFilter != TriState.Ignored)
            {
                bool has = c.Procedural;
                visible &= (_proceduralFilter == TriState.Include) == has;
            }
            if (_invalidFilter != TriState.Ignored)
            {
                bool invalid = IsContainerInvalid(c);
                visible &= (_invalidFilter == TriState.Include) == invalid;
            }

            cc.IsVisible = visible;
        }
    }

    private static bool IsContainerInvalid(Container c)
    {
        bool hasItems = c.ItemChances.Count > 0;
        bool hasJunk = c.JunkChances.Count > 0;
        bool hasProcList = c.ProcListEntries.Count > 0;
        bool hasRolls = c.ItemRolls > 0;

        if (!hasItems && !hasJunk && !hasProcList)
            return true;
        if (hasRolls && !hasItems && !hasJunk)
            return true;
        if (c.Procedural && !hasProcList)
            return true;

        return false;
    }
}
