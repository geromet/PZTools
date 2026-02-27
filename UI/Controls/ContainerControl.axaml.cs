using Avalonia.Controls;
using Avalonia.Interactivity;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class ContainerControl : UserControl
{
    private Container? _model;
    private UndoRedoStack? _undoRedo;
    private bool _loading;

    public ContainerControl()
    {
        InitializeComponent();
    }

    public void Load(Container c, UndoRedoStack undoRedo)
    {
        _model = c;
        _undoRedo = undoRedo;
        _loading = true;
        try
        {
            NameText.Text = c.Name;
            ItemRollsBadge.Text = $"↻ {c.ItemRolls}";
            ItemCountBadge.Text = $"⊞ {c.ItemChances.Count}";
            ProceduralBadge.IsVisible = c.Procedural;

            ItemRollsBox.Text = c.ItemRolls.ToString();
            JunkRollsBox.Text = c.JunkRolls.ToString();
            FillRandCheck.IsChecked = c.FillRand;
            ProceduralCheck.IsChecked = c.Procedural;
            DontSpawnAmmoCheck.IsChecked = c.DontSpawnAmmo;

            ItemRowHelper.Populate(ItemRowsPanel, c.ItemChances, undoRedo, $"{c.Name}.items", c);
            ItemRowHelper.Populate(JunkRowsPanel, c.JunkChances, undoRedo, $"{c.Name}.junk", c);
            ProcListControl.Load(c.ProcListEntries, undoRedo);

            bool hasJunk = c.JunkChances.Count > 0;
            JunkPanel.IsVisible = hasJunk;
            ContentGrid.ColumnDefinitions[2].Width = hasJunk ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            bool hasProc = c.ProcListEntries.Count > 0;
            ProcListPanel.IsVisible = hasProc;
            ContentGrid.ColumnDefinitions[3].Width = hasProc ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        }
        finally
        {
            _loading = false;
        }
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
            $"{_model.Name}.Rolls: {old}→{newVal}",
            v => { _model.ItemRolls = v; ItemRollsBox.Text = v.ToString(); ItemRollsBadge.Text = $"↻ {v}"; _model.IsDirty = true; },
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
            $"{_model.Name}.JunkRolls: {old}→{newVal}",
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
            $"{_model.Name}.FillRand: {old}→{newVal}",
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
            $"{_model.Name}.Procedural: {old}→{newVal}",
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
            $"{_model.Name}.DontSpawnAmmo: {old}→{newVal}",
            v => { _model.DontSpawnAmmo = v; DontSpawnAmmoCheck.IsChecked = v; _model.IsDirty = true; },
            old, newVal));
    }
}
