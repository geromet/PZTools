using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DataInput;
using DataInput.Data;
using DataInput.Serialization;
using UI.Controls;
using UI.UndoRedo;

namespace UI;

public partial class MainWindow : Window
{
    private readonly UndoRedoStack _undoRedo = new();
    private readonly DistributionDetailControl _detail = new();
    private string? _lastFolder;
    private IReadOnlyList<Distribution> _distributions = [];

    // Panel visibility state + saved sizes
    private bool _listVisible  = true;
    private bool _errorVisible = true;
    private bool _rightVisible = true;
    private GridLength _savedListWidth   = new GridLength(260);
    private GridLength _savedErrorHeight = new GridLength(160);
    private GridLength _savedRightWidth  = new GridLength(260);

    private const string BaseTitle = "PZ Distribution Viewer";

    public MainWindow()
    {
        InitializeComponent();

        _undoRedo.StateChanged += RefreshUndoButtons;
        _undoRedo.StateChanged += RefreshDirtyState;
        DistributionList.SelectionChanged += OnDistributionSelected;
        KeyDown += OnKeyDown;
        AddHandler(ProcListEntryControl.NavigateRequestedEvent, OnNavigateToDistribution);

        DetailPane.Content = _detail;
        _detail.ShowEmpty();
        RightDetail.ShowEmpty();
    }

    private void OnNavigateToDistribution(object? sender, NavigateToDistributionEventArgs e)
    {
        RightDetail.Load(e.Distribution, _undoRedo);
        // Make the right panel visible if it was hidden
        if (!_rightVisible) SetRightVisible(true);
    }

    private void OnDistributionSelected(DataInput.Data.Distribution? d)
    {
        _undoRedo.Clear();
        RightDetail.ShowEmpty();
        if (d is null)
        {
            _detail.ShowEmpty();
            return;
        }
        _detail.Load(d, _undoRedo);
    }

    private async void SelectFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (HasUnsavedChanges() && !await ConfirmDiscardChanges())
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select game or mod folder" });

        if (folders.Count == 0) return;
        var path = StorageProviderExtensions.TryGetLocalPath(folders[0]);
        if (path is null) return;

        await ParseAsync(path);
    }

    private async void Reload_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastFolder is null) return;
        if (HasUnsavedChanges() && !await ConfirmDiscardChanges())
            return;
        await ParseAsync(_lastFolder);
    }

    private async Task ParseAsync(string folder)
    {
        _lastFolder = folder;
        LoadingBar.IsVisible = true;
        UndoBtn.IsEnabled = false;
        RedoBtn.IsEnabled = false;

        var result = await Task.Run(() => DistributionParser.CreateDefault().Parse(folder));

        _undoRedo.Clear();
        _distributions = result.Distributions;
        DistributionList.Load(result.Distributions);
        ErrorList.Load(result.Errors, folder);
        _detail.ShowEmpty();

        int errorCount = result.Errors.Count;
        StatusText.Text = $"{result.Distributions.Count} distributions loaded \u00b7 {errorCount} parse issues";
        CountText.Text = string.Empty;

        LoadingBar.IsVisible = false;
        RefreshDirtyState();
    }

    // ── Save ──

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        if (_distributions.Count == 0) return;

        SaveBtn.IsEnabled = false;
        StatusText.Text = "Saving...";

        var writer = new DistributionFileWriter();
        var written = await Task.Run(() => writer.Save(_distributions));

        if (written.Count > 0)
            StatusText.Text = $"Saved {written.Count} file(s)";
        else
            StatusText.Text = "No changes to save";

        RefreshDirtyState();
    }

    // ── Dirty state tracking ──

    private bool HasUnsavedChanges()
    {
        foreach (var d in _distributions)
        {
            if (d.IsDirty) return true;
            foreach (var c in d.Containers)
            {
                if (c.IsDirty) return true;
                foreach (var p in c.ProcListEntries)
                    if (p.IsDirty) return true;
            }
        }
        return false;
    }

    private void RefreshDirtyState()
    {
        bool dirty = HasUnsavedChanges();
        SaveBtn.IsEnabled = dirty;
        Title = dirty ? $"{BaseTitle} *" : BaseTitle;
    }

    private async Task<bool> ConfirmDiscardChanges()
    {
        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        bool result = false;
        var yesBtn = new Button { Content = "Discard", Margin = new Avalonia.Thickness(0, 0, 8, 0) };
        var noBtn = new Button { Content = "Cancel" };
        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click += (_, _) => { dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "You have unsaved changes. Discard them?", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8, Children = { yesBtn, noBtn } }
            }
        };

        await dialog.ShowDialog(this);
        return result;
    }

    // ── Keyboard shortcuts ──

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            _ = SaveAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
        {
            _undoRedo.Undo();
            e.Handled = true;
        }
        else if ((e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control)
                 || (e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)))
        {
            _undoRedo.Redo();
            e.Handled = true;
        }
    }

    private void RefreshUndoButtons()
    {
        UndoBtn.IsEnabled = _undoRedo.CanUndo;
        RedoBtn.IsEnabled = _undoRedo.CanRedo;
        ToolTip.SetTip(UndoBtn, _undoRedo.NextUndoDescription is not null
            ? $"Undo: {_undoRedo.NextUndoDescription}"
            : "Nothing to undo");
        ToolTip.SetTip(RedoBtn, _undoRedo.NextRedoDescription is not null
            ? $"Redo: {_undoRedo.NextRedoDescription}"
            : "Nothing to redo");
    }

    private void UndoBtn_Click(object? sender, RoutedEventArgs e) => _undoRedo.Undo();
    private void RedoBtn_Click(object? sender, RoutedEventArgs e) => _undoRedo.Redo();

    // ── Panel close buttons ──────────────────────────────────────────────────
    private void CloseListBtn_Click(object? sender, RoutedEventArgs e)  => SetListVisible(false);
    private void CloseErrorBtn_Click(object? sender, RoutedEventArgs e) => SetErrorVisible(false);
    private void CloseRightBtn_Click(object? sender, RoutedEventArgs e) => SetRightVisible(false);

    // ── View menu items ──────────────────────────────────────────────────────
    private void ViewListItem_Click(object? sender, RoutedEventArgs e)  => SetListVisible(!_listVisible);
    private void ViewErrorItem_Click(object? sender, RoutedEventArgs e) => SetErrorVisible(!_errorVisible);
    private void ViewRightItem_Click(object? sender, RoutedEventArgs e) => SetRightVisible(!_rightVisible);

    // ── Toggle helpers ───────────────────────────────────────────────────────
    private void SetListVisible(bool visible)
    {
        if (_listVisible == visible) return;
        if (_listVisible) _savedListWidth = MainGrid.ColumnDefinitions[0].Width;

        _listVisible = visible;
        LeftPanelGrid.IsVisible = visible;
        LeftSplitter.IsVisible  = visible;

        if (visible)
        {
            MainGrid.ColumnDefinitions[0].MinWidth = 100;
            MainGrid.ColumnDefinitions[0].Width = _savedListWidth;
            MainGrid.ColumnDefinitions[1].Width = new GridLength(4);
        }
        else
        {
            MainGrid.ColumnDefinitions[0].MinWidth = 0;
            MainGrid.ColumnDefinitions[0].Width = new GridLength(0);
            MainGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }

        ViewListItem.Header = (visible ? "\u2713 " : "   ") + "Distribution List";
    }

    private void SetErrorVisible(bool visible)
    {
        if (_errorVisible == visible) return;
        if (_errorVisible) _savedErrorHeight = MainGrid.RowDefinitions[2].Height;

        _errorVisible = visible;
        ErrorPanelGrid.IsVisible = visible;
        ErrorSplitter.IsVisible  = visible;

        if (visible)
        {
            MainGrid.RowDefinitions[2].MinHeight = 50;
            MainGrid.RowDefinitions[2].Height = _savedErrorHeight;
            MainGrid.RowDefinitions[1].Height = new GridLength(4);
        }
        else
        {
            MainGrid.RowDefinitions[2].MinHeight = 0;
            MainGrid.RowDefinitions[2].Height = new GridLength(0);
            MainGrid.RowDefinitions[1].Height = new GridLength(0);
        }

        ViewErrorItem.Header = (visible ? "\u2713 " : "   ") + "Error List";
    }

    private void SetRightVisible(bool visible)
    {
        if (_rightVisible == visible) return;
        if (_rightVisible) _savedRightWidth = MainGrid.ColumnDefinitions[4].Width;

        _rightVisible = visible;
        RightPanelGrid.IsVisible = visible;
        RightSplitter.IsVisible  = visible;

        if (visible)
        {
            MainGrid.ColumnDefinitions[4].MinWidth = 100;
            MainGrid.ColumnDefinitions[4].Width = _savedRightWidth;
            MainGrid.ColumnDefinitions[3].Width = new GridLength(4);
        }
        else
        {
            MainGrid.ColumnDefinitions[4].MinWidth = 0;
            MainGrid.ColumnDefinitions[4].Width = new GridLength(0);
            MainGrid.ColumnDefinitions[3].Width = new GridLength(0);
        }

        ViewRightItem.Header = (visible ? "\u2713 " : "   ") + "Properties";
    }
}
