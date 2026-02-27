using Avalonia.Controls;
using Avalonia.Interactivity;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public class NavigateToDistributionEventArgs(RoutedEvent routedEvent, Distribution distribution)
    : RoutedEventArgs(routedEvent)
{
    public Distribution Distribution { get; } = distribution;
}

public partial class ProcListEntryControl : UserControl
{
    public static readonly RoutedEvent<NavigateToDistributionEventArgs> NavigateRequestedEvent =
        RoutedEvent.Register<ProcListEntryControl, NavigateToDistributionEventArgs>(
            nameof(NavigateRequested), RoutingStrategies.Bubble);

    public event EventHandler<NavigateToDistributionEventArgs>? NavigateRequested
    {
        add => AddHandler(NavigateRequestedEvent, value);
        remove => RemoveHandler(NavigateRequestedEvent, value);
    }

    private ProcListEntry? _model;
    private UndoRedoStack? _undoRedo;
    private bool _loading;

    public ProcListEntryControl()
    {
        InitializeComponent();
    }

    public void Load(ProcListEntry entry, UndoRedoStack undoRedo)
    {
        _model = entry;
        _undoRedo = undoRedo;
        _loading = true;
        try
        {
            NameText.Text = entry.Name;
            ResolvedText.Text = entry.ResolvedDistribution?.Name ?? "\u26a0 Unresolved";

            MinBox.Text = entry.Min.ToString();
            MaxBox.Text = entry.Max.ToString();
            WeightBox.Text = entry.WeightChance.ToString();

            ForceTilesBox.Text = entry.ForceForTiles ?? string.Empty;
            ForceTilesLabel.IsVisible = ForceTilesBox.IsVisible = !string.IsNullOrEmpty(entry.ForceForTiles);

            ForceRoomsBox.Text = entry.ForceForRooms ?? string.Empty;
            ForceRoomsLabel.IsVisible = ForceRoomsBox.IsVisible = !string.IsNullOrEmpty(entry.ForceForRooms);

            ForceItemsBox.Text = entry.ForceForItems ?? string.Empty;
            ForceItemsLabel.IsVisible = ForceItemsBox.IsVisible = !string.IsNullOrEmpty(entry.ForceForItems);
        }
        finally
        {
            _loading = false;
        }
    }

    private void DistributionName_Click(object? sender, RoutedEventArgs e)
    {
        if (_model?.ResolvedDistribution is null) return;
        RaiseEvent(new NavigateToDistributionEventArgs(NavigateRequestedEvent, _model.ResolvedDistribution));
    }

    private void MinBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        if (!int.TryParse(MinBox.Text, out var newVal))
        {
            MinBox.Text = _model.Min.ToString();
            return;
        }
        if (newVal == _model.Min) return;
        var old = _model.Min;
        _undoRedo.Push(new PropertyChangeAction<int>(
            $"{_model.Name}.Min: {old}\u2192{newVal}",
            v => { _model.Min = v; MinBox.Text = v.ToString(); _model.IsDirty = true; },
            old, newVal));
    }

    private void MaxBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        if (!int.TryParse(MaxBox.Text, out var newVal))
        {
            MaxBox.Text = _model.Max.ToString();
            return;
        }
        if (newVal == _model.Max) return;
        var old = _model.Max;
        _undoRedo.Push(new PropertyChangeAction<int>(
            $"{_model.Name}.Max: {old}\u2192{newVal}",
            v => { _model.Max = v; MaxBox.Text = v.ToString(); _model.IsDirty = true; },
            old, newVal));
    }

    private void WeightBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        if (!int.TryParse(WeightBox.Text, out var newVal))
        {
            WeightBox.Text = _model.WeightChance.ToString();
            return;
        }
        if (newVal == _model.WeightChance) return;
        var old = _model.WeightChance;
        _undoRedo.Push(new PropertyChangeAction<int>(
            $"{_model.Name}.WeightChance: {old}\u2192{newVal}",
            v => { _model.WeightChance = v; WeightBox.Text = v.ToString(); _model.IsDirty = true; },
            old, newVal));
    }

    private void ForceTilesBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        var newVal = ForceTilesBox.Text ?? string.Empty;
        var oldVal = _model.ForceForTiles ?? string.Empty;
        if (newVal == oldVal) return;
        _undoRedo.Push(new PropertyChangeAction<string>(
            $"{_model.Name}.ForceForTiles",
            v => { _model.ForceForTiles = string.IsNullOrEmpty(v) ? null : v; ForceTilesBox.Text = v; _model.IsDirty = true; },
            oldVal, newVal));
    }

    private void ForceRoomsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        var newVal = ForceRoomsBox.Text ?? string.Empty;
        var oldVal = _model.ForceForRooms ?? string.Empty;
        if (newVal == oldVal) return;
        _undoRedo.Push(new PropertyChangeAction<string>(
            $"{_model.Name}.ForceForRooms",
            v => { _model.ForceForRooms = string.IsNullOrEmpty(v) ? null : v; ForceRoomsBox.Text = v; _model.IsDirty = true; },
            oldVal, newVal));
    }

    private void ForceItemsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        var newVal = ForceItemsBox.Text ?? string.Empty;
        var oldVal = _model.ForceForItems ?? string.Empty;
        if (newVal == oldVal) return;
        _undoRedo.Push(new PropertyChangeAction<string>(
            $"{_model.Name}.ForceForItems",
            v => { _model.ForceForItems = string.IsNullOrEmpty(v) ? null : v; ForceItemsBox.Text = v; _model.IsDirty = true; },
            oldVal, newVal));
    }
}
