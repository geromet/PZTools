using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DataInput;
using UI.Controls;
using UI.UndoRedo;

namespace UI;

public partial class MainWindow : Window
{
    private readonly UndoRedoStack _undoRedo = new();
    private readonly DistributionDetailControl _detail = new();
    private string? _lastFolder;

    // Panel visibility state + saved sizes
    private bool _listVisible  = true;
    private bool _errorVisible = true;
    private bool _rightVisible = true;
    private GridLength _savedListWidth   = new GridLength(260);
    private GridLength _savedErrorHeight = new GridLength(160);
    private GridLength _savedRightWidth  = new GridLength(260);

    public MainWindow()
    {
        InitializeComponent();

        _undoRedo.StateChanged += RefreshUndoButtons;
        DistributionList.SelectionChanged += OnDistributionSelected;
        KeyDown += OnKeyDown;

        DetailPane.Content = _detail;
        _detail.ShowEmpty();
    }

    private void OnDistributionSelected(DataInput.Data.Distribution? d)
    {
        _undoRedo.Clear();
        if (d is null)
        {
            _detail.ShowEmpty();
            return;
        }
        _detail.Load(d, _undoRedo);
    }

    private async void SelectFolder_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select game or mod folder" });

        if (folders.Count == 0) return;
        var path = StorageProviderExtensions.TryGetLocalPath(folders[0]);
        if (path is null) return;

        await ParseAsync(path);
    }

    private async void Reload_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastFolder is not null)
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
        DistributionList.Load(result.Distributions);
        ErrorList.Load(result.Errors, folder);
        _detail.ShowEmpty();

        int errorCount = result.Errors.Count;
        StatusText.Text = $"{result.Distributions.Count} distributions loaded · {errorCount} parse issues";
        CountText.Text = string.Empty;

        LoadingBar.IsVisible = false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
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

        ViewListItem.Header = (visible ? "✓ " : "   ") + "Distribution List";
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

        ViewErrorItem.Header = (visible ? "✓ " : "   ") + "Error List";
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

        ViewRightItem.Header = (visible ? "✓ " : "   ") + "Properties";
    }
}
