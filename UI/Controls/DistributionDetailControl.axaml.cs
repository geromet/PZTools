using Avalonia.Controls;
using Avalonia.Interactivity;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class DistributionDetailControl : UserControl
{
    private Distribution? _model;
    private UndoRedoStack? _undoRedo;
    private bool _loading;

    public DistributionDetailControl()
    {
        InitializeComponent();
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
            foreach (var container in d.Containers)
            {
                var ctrl = new ContainerControl();
                ctrl.Load(container, undoRedo);
                ContainersPanel.Children.Add(ctrl);
            }
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
}
