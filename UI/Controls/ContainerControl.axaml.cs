using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class ContainerControl : UserControl
{
    private Container? _model;
    private UndoRedoStack? _undoRedo;
    private SharedColumnLayout? _sharedLayout;
    private bool _loading;
    private bool _hasItems;
    private bool _hasJunk;
    private bool _hasProc;

    public Container? Model => _model;

    public ContainerControl()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    public void Load(Container c, UndoRedoStack undoRedo, SharedColumnLayout? sharedLayout = null, bool showEmpty = false)
    {
        _model = c;
        _undoRedo = undoRedo;
        _loading = true;
        try
        {
            // Unsubscribe from previous layout
            if (_sharedLayout is not null)
                _sharedLayout.ProportionsChanged -= OnSharedProportionsChanged;

            _sharedLayout = sharedLayout;
            if (_sharedLayout is not null)
                _sharedLayout.ProportionsChanged += OnSharedProportionsChanged;

            NameText.Text = c.Name;
            ItemRollsBadge.Text = $"\u21bb {c.ItemRolls}";
            ItemCountBadge.Text = $"\u229e {c.ItemChances.Count}";
            ProceduralBadge.IsVisible = c.Procedural;

            ItemRollsBox.Text = c.ItemRolls.ToString();
            JunkRollsBox.Text = c.JunkRolls.ToString();
            FillRandCheck.IsChecked = c.FillRand;
            ProceduralCheck.IsChecked = c.Procedural;
            DontSpawnAmmoCheck.IsChecked = c.DontSpawnAmmo;

            ItemRowHelper.Populate(ItemRowsPanel, c.ItemChances, undoRedo, $"{c.Name}.items", c);
            ItemRowHelper.Populate(JunkRowsPanel, c.JunkChances, undoRedo, $"{c.Name}.junk", c);
            ProcListControl.Load(c.ProcListEntries, undoRedo);

            _hasItems = c.ItemChances.Count > 0 || showEmpty;
            _hasJunk = c.JunkChances.Count > 0 || showEmpty;
            _hasProc = c.ProcListEntries.Count > 0 || showEmpty;

            ConfigureColumns();

            // Apply shared proportions
            ApplySharedProportions();

            // Wire splitter drag events
            SettingsItemsSplitter.DragDelta -= OnSplitterDragDelta;
            SettingsItemsSplitter.DragDelta += OnSplitterDragDelta;
            JunkSplitter.DragDelta -= OnSplitterDragDelta;
            JunkSplitter.DragDelta += OnSplitterDragDelta;
            ProcListSplitter.DragDelta -= OnSplitterDragDelta;
            ProcListSplitter.DragDelta += OnSplitterDragDelta;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Resets all 7 column definitions, then packs every visible panel
    /// into consecutive (splitter, content) pairs starting from col 1.
    /// Each GridSplitter always has a real star-width neighbor on both sides.
    /// Stateless — safe to call repeatedly across toggles and switches.
    /// </summary>
    private void ConfigureColumns()
    {
        var zero = new GridLength(0);
        var splitterWidth = new GridLength(4, GridUnitType.Pixel);
        var star = new GridLength(1, GridUnitType.Star);
        var defs = ContentGrid.ColumnDefinitions;

        // 1. Reset all columns to zero
        for (int i = 0; i < 7; i++)
            defs[i].Width = zero;

        // 2. Col 0 is always Settings
        defs[0].Width = star;

        // 3. Pack visible panels into consecutive column pairs
        //    Each pair is (splitter col, content col).
        //    Layout: Settings(0) | S(n) | Panel(n+1) | S(n+2) | Panel(n+3) | ...
        int next = 1;

        // Items
        ItemsPanel.IsVisible = _hasItems;
        SettingsItemsSplitter.IsVisible = _hasItems;
        if (_hasItems)
        {
            Grid.SetColumn(SettingsItemsSplitter, next);
            Grid.SetColumn(ItemsPanel, next + 1);
            defs[next].Width = splitterWidth;
            defs[next + 1].Width = star;
            next += 2;
        }

        // Junk
        JunkPanel.IsVisible = _hasJunk;
        JunkSplitter.IsVisible = _hasJunk;
        if (_hasJunk)
        {
            Grid.SetColumn(JunkSplitter, next);
            Grid.SetColumn(JunkPanel, next + 1);
            defs[next].Width = splitterWidth;
            defs[next + 1].Width = star;
            next += 2;
        }

        // ProcList
        ProcListPanel.IsVisible = _hasProc;
        ProcListSplitter.IsVisible = _hasProc;
        if (_hasProc)
        {
            Grid.SetColumn(ProcListSplitter, next);
            Grid.SetColumn(ProcListPanel, next + 1);
            defs[next].Width = splitterWidth;
            defs[next + 1].Width = star;
            next += 2;
        }
    }

    private void ApplySharedProportions()
    {
        if (_sharedLayout is null) return;
        var defs = ContentGrid.ColumnDefinitions;

        defs[0].Width = new GridLength(_sharedLayout.Settings, GridUnitType.Star);
        if (_hasItems)
            defs[Grid.GetColumn(ItemsPanel)].Width = new GridLength(_sharedLayout.Items, GridUnitType.Star);
        if (_hasJunk)
            defs[Grid.GetColumn(JunkPanel)].Width = new GridLength(_sharedLayout.Junk, GridUnitType.Star);
        if (_hasProc)
            defs[Grid.GetColumn(ProcListPanel)].Width = new GridLength(_sharedLayout.ProcList, GridUnitType.Star);
    }

    private void OnSplitterDragDelta(object? sender, VectorEventArgs e)
    {
        if (_sharedLayout is null) return;
        var defs = ContentGrid.ColumnDefinitions;

        _sharedLayout.Settings = defs[0].Width.Value;
        if (_hasItems)
            _sharedLayout.Items = defs[Grid.GetColumn(ItemsPanel)].Width.Value;
        if (_hasJunk)
            _sharedLayout.Junk = defs[Grid.GetColumn(JunkPanel)].Width.Value;
        if (_hasProc)
            _sharedLayout.ProcList = defs[Grid.GetColumn(ProcListPanel)].Width.Value;

        _sharedLayout.NotifyChanged(this);
    }

    private void OnSharedProportionsChanged(object? sender)
    {
        if (sender == this) return;
        ApplySharedProportions();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_sharedLayout is not null)
            _sharedLayout.ProportionsChanged -= OnSharedProportionsChanged;

        SettingsItemsSplitter.DragDelta -= OnSplitterDragDelta;
        JunkSplitter.DragDelta -= OnSplitterDragDelta;
        ProcListSplitter.DragDelta -= OnSplitterDragDelta;
    }

    private void ItemRollsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        if (!int.TryParse(ItemRollsBox.Text, out var newVal))
        {
            ItemRollsBox.Text = _model.ItemRolls.ToString();
            return;
        }
        if (newVal == _model.ItemRolls) return;
        var old = _model.ItemRolls;
        _undoRedo.Push(new PropertyChangeAction<int>(
            $"{_model.Name}.Rolls: {old}\u2192{newVal}",
            v => { _model.ItemRolls = v; ItemRollsBox.Text = v.ToString(); ItemRollsBadge.Text = $"\u21bb {v}"; _model.IsDirty = true; },
            old, newVal));
    }

    private void JunkRollsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        if (!int.TryParse(JunkRollsBox.Text, out var newVal))
        {
            JunkRollsBox.Text = _model.JunkRolls.ToString();
            return;
        }
        if (newVal == _model.JunkRolls) return;
        var old = _model.JunkRolls;
        _undoRedo.Push(new PropertyChangeAction<int>(
            $"{_model.Name}.JunkRolls: {old}\u2192{newVal}",
            v => { _model.JunkRolls = v; JunkRollsBox.Text = v.ToString(); _model.IsDirty = true; },
            old, newVal));
    }

    private void FillRandCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        var newVal = FillRandCheck.IsChecked == true;
        if (newVal == _model.FillRand) return;
        var old = _model.FillRand;
        _undoRedo.Push(new PropertyChangeAction<bool>(
            $"{_model.Name}.FillRand: {old}\u2192{newVal}",
            v => { _model.FillRand = v; FillRandCheck.IsChecked = v; _model.IsDirty = true; },
            old, newVal));
    }

    private void ProceduralCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        var newVal = ProceduralCheck.IsChecked == true;
        if (newVal == _model.Procedural) return;
        var old = _model.Procedural;
        _undoRedo.Push(new PropertyChangeAction<bool>(
            $"{_model.Name}.Procedural: {old}\u2192{newVal}",
            v => { _model.Procedural = v; ProceduralCheck.IsChecked = v; ProceduralBadge.IsVisible = v; _model.IsDirty = true; },
            old, newVal));
    }

    private void DontSpawnAmmoCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        var newVal = DontSpawnAmmoCheck.IsChecked == true;
        if (newVal == _model.DontSpawnAmmo) return;
        var old = _model.DontSpawnAmmo;
        _undoRedo.Push(new PropertyChangeAction<bool>(
            $"{_model.Name}.DontSpawnAmmo: {old}\u2192{newVal}",
            v => { _model.DontSpawnAmmo = v; DontSpawnAmmoCheck.IsChecked = v; _model.IsDirty = true; },
            old, newVal));
    }
}
