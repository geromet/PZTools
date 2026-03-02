using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Core.Filtering;
using Data.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class DistributionDetailControl : UserControl
{
    private Distribution? _model;
    private UndoRedoStack? _undoRedo;
    private bool _loading;
    private bool _showToolbar = true;
    private readonly SharedColumnLayout _sharedColumnLayout = new();
    private readonly ContainerFilterState _filter = new();

    public Func<(TriState ProcList, TriState Rolls, TriState Items, TriState Junk, TriState Procedural, TriState Invalid, TriState DistributionItems)>? GetContentFilters { get; set; }

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

            bool hasDirectItems = d.ItemChances.Count > 0 || d.JunkChances.Count > 0 || _filter.ShowEmpty;
            DirectItemsPanel.IsVisible = hasDirectItems;
            HeaderDirectItems.IsVisible = hasDirectItems;
            if (hasDirectItems)
            {
                DirectRollsBadge.Text = $"\u21bb {d.ItemRolls}";
                DirectCountBadge.Text = $"\u229e {d.ItemChances.Count}";
                HeaderDirectItemCount.Text = d.ItemChances.Count.ToString();
                DistItemsControl.Load(d.ItemChances, undoRedo, $"{d.Name}.items", d);
                JunkTab.IsVisible = d.JunkChances.Count > 0 || _filter.ShowEmpty;
                if (d.JunkChances.Count > 0 || _filter.ShowEmpty)
                    DistJunkControl.Load(d.JunkChances, undoRedo, $"{d.Name}.junk", d);
            }

            ContainersPanel.Children.Clear();
            const int autoExpandLimit = 10;
            for (int i = 0; i < d.Containers.Count; i++)
            {
                var ctrl = new ContainerControl();
                ctrl.Load(d.Containers[i], undoRedo, _sharedColumnLayout, _filter.ShowEmpty);
                if (i < autoExpandLimit)
                    ctrl.ContainerExpander.IsExpanded = true;
                ContainersPanel.Children.Add(ctrl);
            }

            if (_filter.AutoFilter && GetContentFilters is not null)
                _filter.SyncFromContentFilters(GetContentFilters());

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
        _filter.ShowEmpty = !_filter.ShowEmpty;
        ShowEmptyBtn.Classes.Set("active", _filter.ShowEmpty);
        if (_model is not null && _undoRedo is not null)
            Load(_model, _undoRedo);
    }

    // ── Auto Filter / Clear ──

    private void AutoFilter_Click(object? sender, RoutedEventArgs e)
    {
        _filter.AutoFilter = !_filter.AutoFilter;

        if (_filter.AutoFilter && GetContentFilters is not null)
            _filter.SyncFromContentFilters(GetContentFilters());
        else if (!_filter.AutoFilter)
            _filter.ClearAll();

        UpdateContainerFilterStyles();
        ApplyContainerFilter();
    }

    private void ClearFilters_Click(object? sender, RoutedEventArgs e)
    {
        _filter.ClearAll();
        UpdateContainerFilterStyles();
        ApplyContainerFilter();
    }

    private void ContainerFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        ref var state = ref _filter.GetRef(btn.Tag as string);
        state = state == TriState.Include ? TriState.Ignored : TriState.Include;
        _filter.AutoFilter = false;
        UpdateContainerFilterStyles();
        ApplyContainerFilter();
    }

    private void ContainerFilterPill_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (!e.GetCurrentPoint(btn).Properties.IsRightButtonPressed) return;
        ref var state = ref _filter.GetRef(btn.Tag as string);
        state = state == TriState.Exclude ? TriState.Ignored : TriState.Exclude;
        _filter.AutoFilter = false;
        UpdateContainerFilterStyles();
        ApplyContainerFilter();
        e.Handled = true;
    }

    private void UpdateContainerFilterStyles()
    {
        foreach (var child in ContainerFilterPills.Children)
        {
            if (child is not Button btn) continue;
            var state = _filter.GetRef(btn.Tag as string);
            btn.Classes.Remove("include");
            btn.Classes.Remove("exclude");
            if (state == TriState.Include) btn.Classes.Add("include");
            else if (state == TriState.Exclude) btn.Classes.Add("exclude");
        }

        if (_filter.AutoFilter)
            AutoFilterBtn.Classes.Add("active");
        else
            AutoFilterBtn.Classes.Remove("active");
    }

    private void ApplyContainerFilter()
    {
        foreach (var child in ContainersPanel.Children)
        {
            if (child is not ContainerControl cc || cc.Model is null) continue;
            cc.IsVisible = _filter.IsContainerVisible(cc.Model);
        }
    }
}
