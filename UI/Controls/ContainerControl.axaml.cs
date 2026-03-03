using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Data.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class ContainerControl : UserControl
{
    private Container? _model;
    private UndoRedoStack? _undoRedo;
    private SharedColumnLayout? _sharedLayout;
    private bool _loading;
    private bool _showEmpty;
    private bool _contentDirty;
    private bool _hasItems;
    private bool _hasJunk;
    private bool _hasProc;

    public Container? Model => _model;

    public ContainerControl()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
        ContainerExpander.Expanded += OnExpanderExpanded;
    }

    public void Load(Container c, UndoRedoStack undoRedo, SharedColumnLayout? sharedLayout = null, bool showEmpty = false, bool expanded = false)
    {
        _model = c;
        _undoRedo = undoRedo;
        _showEmpty = showEmpty;
        _loading = true;
        try
        {
            if (_sharedLayout is not null)
                _sharedLayout.ProportionsChanged -= OnSharedProportionsChanged;

            _sharedLayout = sharedLayout;
            if (_sharedLayout is not null)
                _sharedLayout.ProportionsChanged += OnSharedProportionsChanged;

            // Header is always visible; populate it immediately.
            NameText.Text = c.Name;
            ItemRollsBadge.Text = $"\u21bb {c.ItemRolls}";
            ItemCountBadge.Text = $"\u229e {c.ItemChances.Count}";
            ProceduralBadge.IsVisible = c.Procedural;

            // Content is only needed when expanded. Defer populate until first expand.
            ContainerExpander.IsExpanded = expanded;
            _contentDirty = !expanded;
            if (expanded)
                PopulateContent();
        }
        finally
        {
            _loading = false;
        }
    }

    // Called when the user (or SetAllExpanded) expands the container.
    private void OnExpanderExpanded(object? sender, RoutedEventArgs e)
    {
        if (_loading) return; // Load() manages populate directly; suppress re-entry
        if (_contentDirty)
            PopulateContent();
    }

    private void PopulateContent()
    {
        if (_model is null || _undoRedo is null) return;
        _contentDirty = false;
        _loading = true;
        try
        {
            var c = _model;
            ItemRollsBox.Text = c.ItemRolls.ToString();
            JunkRollsBox.Text = c.JunkRolls.ToString();
            FillRandCheck.IsChecked = c.FillRand;
            ProceduralCheck.IsChecked = c.Procedural;
            DontSpawnAmmoCheck.IsChecked = c.DontSpawnAmmo;

            ItemRowHelper.Populate(ItemRowsPanel, c.ItemChances, _undoRedo, $"{c.Name}.items", c);
            ItemRowHelper.Populate(JunkRowsPanel, c.JunkChances, _undoRedo, $"{c.Name}.junk", c);
            ProcListControl.Load(c.ProcListEntries, _undoRedo);

            _hasItems = c.ItemChances.Count > 0 || _showEmpty;
            _hasJunk = c.JunkChances.Count > 0 || _showEmpty;
            _hasProc = c.ProcListEntries.Count > 0 || _showEmpty;

            ConfigureColumns();
            ApplySharedProportions();
            WireSplitters();
        }
        finally
        {
            _loading = false;
        }
    }

    #region Column layout

    private GridSplitter[] Splitters => [SettingsItemsSplitter, JunkSplitter, ProcListSplitter];

    private void WireSplitters()
    {
        foreach (var s in Splitters)
        {
            s.DragDelta -= OnSplitterDragDelta;
            s.DragDelta += OnSplitterDragDelta;
        }
    }

    private void ConfigureColumns()
    {
        var zero = new GridLength(0);
        var splitterWidth = new GridLength(4, GridUnitType.Pixel);
        var star = new GridLength(1, GridUnitType.Star);
        var defs = ContentGrid.ColumnDefinitions;

        for (int i = 0; i < 7; i++)
            defs[i].Width = zero;

        defs[0].Width = star;
        int next = 1;

        PlaceColumnPair(defs, SettingsItemsSplitter, ItemsPanel, _hasItems, ref next, splitterWidth, star);
        PlaceColumnPair(defs, JunkSplitter, JunkPanel, _hasJunk, ref next, splitterWidth, star);
        PlaceColumnPair(defs, ProcListSplitter, ProcListPanel, _hasProc, ref next, splitterWidth, star);
    }

    private static void PlaceColumnPair(
        ColumnDefinitions defs, Control splitter, Control panel,
        bool visible, ref int next, GridLength splitterWidth, GridLength star)
    {
        splitter.IsVisible = visible;
        panel.IsVisible = visible;
        if (!visible) return;
        Grid.SetColumn(splitter, next);
        Grid.SetColumn(panel, next + 1);
        defs[next].Width = splitterWidth;
        defs[next + 1].Width = star;
        next += 2;
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
        if (_contentDirty) return; // not populated yet; ApplySharedProportions runs when expanded
        ApplySharedProportions();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_sharedLayout is not null)
            _sharedLayout.ProportionsChanged -= OnSharedProportionsChanged;

        foreach (var s in Splitters)
            s.DragDelta -= OnSplitterDragDelta;
    }

    #endregion

    #region Property editing

    private void ItemRollsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushIntChange(_undoRedo, _model, ItemRollsBox, "Rolls",
            _model.ItemRolls, v => _model.ItemRolls = v,
            v => ItemRollsBadge.Text = $"\u21bb {v}");
    }

    private void JunkRollsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushIntChange(_undoRedo, _model, JunkRollsBox, "JunkRolls",
            _model.JunkRolls, v => _model.JunkRolls = v);
    }

    private void FillRandCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushBoolChange(_undoRedo, _model, FillRandCheck, "FillRand",
            _model.FillRand, v => _model.FillRand = v);
    }

    private void ProceduralCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushBoolChange(_undoRedo, _model, ProceduralCheck, "Procedural",
            _model.Procedural, v => _model.Procedural = v,
            v => ProceduralBadge.IsVisible = v);
    }

    private void DontSpawnAmmoCheck_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushBoolChange(_undoRedo, _model, DontSpawnAmmoCheck, "DontSpawnAmmo",
            _model.DontSpawnAmmo, v => _model.DontSpawnAmmo = v);
    }

    #endregion

    #region Add items

    private void AddItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_model is null || _undoRedo is null) return;
        UndoHelper.PushItemInsert(_undoRedo, _model,
            _model.ItemChances, ItemRowsPanel, $"{_model.Name}.items");
    }

    private void AddJunkItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_model is null || _undoRedo is null) return;
        UndoHelper.PushItemInsert(_undoRedo, _model,
            _model.JunkChances, JunkRowsPanel, $"{_model.Name}.junk");
    }

    #endregion
}
