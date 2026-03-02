using Avalonia.Controls;
using Avalonia.Interactivity;
using Data.Data;
using UI.Controls.Helpers;
using UI.UndoRedo;

namespace UI.Controls;
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

    #region Navigation

    private void DistributionName_Click(object? sender, RoutedEventArgs e)
    {
        if (_model?.ResolvedDistribution is null) return;
        RaiseEvent(new NavigateToDistributionEventArgs(NavigateRequestedEvent, _model.ResolvedDistribution));
    }

    #endregion

    #region Property editing

    private void MinBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushIntChange(_undoRedo, _model, MinBox, "Min",
            _model.Min, v => _model.Min = v);
    }

    private void MaxBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushIntChange(_undoRedo, _model, MaxBox, "Max",
            _model.Max, v => _model.Max = v);
    }

    private void WeightBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushIntChange(_undoRedo, _model, WeightBox, "WeightChance",
            _model.WeightChance, v => _model.WeightChance = v);
    }

    private void ForceTilesBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushStringChange(_undoRedo, _model, ForceTilesBox, "ForceForTiles",
            _model.ForceForTiles, v => _model.ForceForTiles = v);
    }

    private void ForceRoomsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushStringChange(_undoRedo, _model, ForceRoomsBox, "ForceForRooms",
            _model.ForceForRooms, v => _model.ForceForRooms = v);
    }

    private void ForceItemsBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _model is null || _undoRedo is null) return;
        UndoHelper.PushStringChange(_undoRedo, _model, ForceItemsBox, "ForceForItems",
            _model.ForceForItems, v => _model.ForceForItems = v);
    }

    #endregion
}
