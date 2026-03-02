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

    public Func<ContentFilterSet>? GetContentFilters { get; set; }

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
                _filter.SyncFrom(GetContentFilters());

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

    #region Property editing

    private void RollsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushIntChange(_undoRedo, _model, RollsBox, "Rolls",
            _model.ItemRolls, v => _model.ItemRolls = v);
    }

    private void ShopCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushBoolChange(_undoRedo, _model, ShopCheck, "IsShop",
            _model.IsShop, v => _model.IsShop = v);
    }

    private void NoAmmoCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushBoolChange(_undoRedo, _model, NoAmmoCheck, "DontSpawnAmmo",
            _model.DontSpawnAmmo, v => _model.DontSpawnAmmo = v);
    }

    private void MaxMapBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushIntChange(_undoRedo, _model, MaxMapBox, "MaxMap",
            _model.MaxMap ?? 0, v => _model.MaxMap = v);
    }

    private void StashBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushIntChange(_undoRedo, _model, StashBox, "StashChance",
            _model.StashChance ?? 0, v => _model.StashChance = v);
    }

    #endregion

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
            _filter.SyncFrom(GetContentFilters());
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
        FilterPillHelper.ApplyTriStateStyles(ContainerFilterPills, _filter.Content);
        AutoFilterBtn.Classes.Set("active", _filter.AutoFilter);
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
