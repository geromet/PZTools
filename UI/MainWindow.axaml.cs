using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DataInput;
using DataInput.Comments;
using DataInput.Data;
using DataInput.Serialization;
using UI.Controls;
using UI.UndoRedo;

namespace UI;

public partial class MainWindow : Window
{
    private readonly UserSettings _settings = UserSettings.Load();
    private string? _lastFolder;
    private IReadOnlyList<Distribution> _distributions = [];
    private CommentMap? _procComments;
    private CommentMap? _distComments;

    // Panel visibility state + saved sizes
    private bool _listVisible   = true;
    private bool _detailVisible = true;
    private bool _errorVisible  = true;
    private bool _rightVisible  = true;
    private GridLength _savedListWidth   = new GridLength(260);
    private GridLength _savedErrorHeight = new GridLength(160);
    private GridLength _savedRightWidth  = new GridLength(260);

    private const string BaseTitle = "PZ Distribution Viewer";

    // ── Tab state ──

    private class TabState
    {
        public Distribution Distribution { get; }
        public UndoRedoStack UndoRedo { get; } = new();
        public DistributionDetailControl? DetailControl { get; set; }
        public Distribution? PropertiesDistribution { get; set; }
        public TabItem TabItem { get; set; } = null!;
        public long LastAccessTick { get; set; }

        public bool IsPinned { get; set; }

        public TabState(Distribution distribution)
        {
            Distribution = distribution;
        }
    }

    private readonly Dictionary<string, TabState> _openTabs = new();
    private TabState? _activeTab;
    private bool _suppressTabChanged;
    private const int MaxCachedTabs = 10;

    public MainWindow()
    {
        InitializeComponent();

        DistributionList.SetSettings(_settings);
        DistributionList.OpenRequested += OnDistributionOpenRequested;
        DistributionList.OpenMultipleRequested += OnDistributionOpenMultipleRequested;
        KeyDown += OnKeyDown;
        AddHandler(ProcListEntryControl.NavigateRequestedEvent, OnNavigateToDistribution);

        RightDetail.GetContentFilters = () => DistributionList.ContentFilters;
        RightDetail.ShowToolbar = false;
        RightDetail.ShowEmpty();

        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (_settings.LastFolder is not null && System.IO.Directory.Exists(_settings.LastFolder))
            await ParseAsync(_settings.LastFolder);
    }

    // ── Tab management ──

    private void OpenOrActivateTab(Distribution d)
    {
        if (_openTabs.TryGetValue(d.Name, out var existing))
        {
            existing.LastAccessTick = Environment.TickCount64;
            // If the control was evicted, recreate it
            if (existing.DetailControl is null)
                RecreateDetailControl(existing);
            _suppressTabChanged = true;
            TabBar.SelectedItem = existing.TabItem;
            _suppressTabChanged = false;
            ActivateTab(existing);
            return;
        }

        var state = new TabState(d)
        {
            LastAccessTick = Environment.TickCount64
        };

        var detail = new DistributionDetailControl();
        detail.GetContentFilters = () => DistributionList.ContentFilters;
        detail.Load(d, state.UndoRedo);
        state.DetailControl = detail;

        var tabItem = new TabItem
        {
            Header = CreateTabHeader(state),
            Content = detail
        };
        state.TabItem = tabItem;

        state.UndoRedo.StateChanged += () => OnTabUndoStateChanged(state);

        _openTabs[d.Name] = state;
        _suppressTabChanged = true;
        TabBar.Items.Add(tabItem);
        TabBar.SelectedItem = tabItem;
        _suppressTabChanged = false;

        ActivateTab(state);
        EvictOldTabs();
    }

    private static readonly IBrush DirtyDotBrush = SolidColorBrush.Parse("#C88B2A");
    private static readonly IBrush TabNameBrush = SolidColorBrush.Parse("#8A8A98");
    private static readonly IBrush TabCloseBrush = SolidColorBrush.Parse("#5A5A6A");
    private static readonly IBrush EvictedTextBrush = SolidColorBrush.Parse("#5A5A6A");
    private static readonly IBrush PinIconBrush = SolidColorBrush.Parse("#8A8A98");

    private StackPanel CreateTabHeader(TabState state)
    {
        var pinIcon = new TextBlock
        {
            Text = "\ud83d\udccc",
            FontSize = 10,
            Foreground = PinIconBrush,
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Tag = "pin"
        };

        var dirtyDot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = DirtyDotBrush,
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Tag = "dirty"
        };

        var nameBlock = new TextBlock
        {
            Text = state.Distribution.Name,
            Foreground = TabNameBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        var closeBtn = new Button
        {
            Content = "\u00d7",
            Width = 18,
            Height = 18,
            Padding = new Thickness(0),
            FontSize = 14,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = TabCloseBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        closeBtn.Click += (_, _) => _ = CloseTabAsync(state);

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { pinIcon, dirtyDot, nameBlock, closeBtn }
        };

        header.ContextMenu = CreateTabContextMenu(state);
        return header;
    }

    private ContextMenu CreateTabContextMenu(TabState state)
    {
        var pinItem = new MenuItem { Header = state.IsPinned ? "Unpin This Tab" : "Pin This Tab" };
        pinItem.Click += (_, _) =>
        {
            state.IsPinned = !state.IsPinned;
            RefreshTabPinIcon(state);
        };

        var closeItem = new MenuItem { Header = "Close This Tab" };
        closeItem.Click += (_, _) => _ = CloseTabAsync(state);

        var closeAllItem = new MenuItem { Header = "Close All Tabs" };
        closeAllItem.Click += (_, _) => _ = CloseAllTabsWithPromptAsync();

        var closeOthersItem = new MenuItem { Header = "Close All Tabs Except This" };
        closeOthersItem.Click += (_, _) => _ = CloseOtherTabsAsync(state);

        var closeLeftItem = new MenuItem { Header = "Close All Tabs to the Left" };
        closeLeftItem.Click += (_, _) => _ = CloseTabsToSideAsync(state, left: true);

        var closeRightItem = new MenuItem { Header = "Close All Tabs to the Right" };
        closeRightItem.Click += (_, _) => _ = CloseTabsToSideAsync(state, left: false);

        return new ContextMenu
        {
            Items =
            {
                pinItem,
                new Separator(),
                closeItem,
                closeAllItem,
                closeOthersItem,
                new Separator(),
                closeLeftItem,
                closeRightItem
            }
        };
    }

    private void RefreshTabPinIcon(TabState state)
    {
        if (state.TabItem.Header is not StackPanel header) return;
        foreach (var child in header.Children)
        {
            if (child is TextBlock { Tag: "pin" } pin)
            {
                pin.IsVisible = state.IsPinned;
                break;
            }
        }
        // Rebuild context menu with updated pin/unpin text
        header.ContextMenu = CreateTabContextMenu(state);
    }

    private void ActivateTab(TabState state)
    {
        _activeTab = state;
        state.LastAccessTick = Environment.TickCount64;

        // Restore properties panel
        if (state.PropertiesDistribution is not null)
            RightDetail.Load(state.PropertiesDistribution, state.UndoRedo);
        else
            RightDetail.ShowEmpty();

        RefreshUndoButtons();
        RefreshTabDirtyDot(state);
    }

    private async Task CloseTabAsync(TabState state, bool skipPinCheck = false)
    {
        if (!skipPinCheck && state.IsPinned) return;

        if (IsDistributionDirty(state.Distribution))
        {
            var result = await ShowCloseTabDialog(state.Distribution.Name);
            if (result == CloseTabResult.Cancel) return;
            if (result == CloseTabResult.Save) await SaveAsync();
        }

        RemoveTab(state);
    }

    private void RemoveTab(TabState state)
    {
        _suppressTabChanged = true;
        TabBar.Items.Remove(state.TabItem);
        _openTabs.Remove(state.Distribution.Name);
        _suppressTabChanged = false;

        if (_activeTab == state)
        {
            _activeTab = null;
            RightDetail.ShowEmpty();

            // Activate adjacent tab
            if (TabBar.Items.Count > 0)
            {
                TabBar.SelectedIndex = Math.Min(TabBar.SelectedIndex, TabBar.Items.Count - 1);
                if (TabBar.SelectedIndex < 0) TabBar.SelectedIndex = 0;
                var selected = TabBar.SelectedItem as TabItem;
                var next = _openTabs.Values.FirstOrDefault(t => t.TabItem == selected);
                if (next is not null)
                    ActivateTab(next);
            }
            else
            {
                RefreshUndoButtons();
            }
        }
    }

    private async Task CloseAllTabsWithPromptAsync()
    {
        var tabs = _openTabs.Values.ToList();
        foreach (var tab in tabs)
        {
            if (tab.IsPinned) continue;
            if (IsDistributionDirty(tab.Distribution))
            {
                var result = await ShowCloseTabDialog(tab.Distribution.Name);
                if (result == CloseTabResult.Cancel) return;
                if (result == CloseTabResult.Save) await SaveAsync();
            }
            RemoveTab(tab);
        }
    }

    private async Task CloseOtherTabsAsync(TabState keep)
    {
        var tabs = _openTabs.Values.Where(t => t != keep).ToList();
        foreach (var tab in tabs)
        {
            if (tab.IsPinned) continue;
            if (IsDistributionDirty(tab.Distribution))
            {
                var result = await ShowCloseTabDialog(tab.Distribution.Name);
                if (result == CloseTabResult.Cancel) return;
                if (result == CloseTabResult.Save) await SaveAsync();
            }
            RemoveTab(tab);
        }
    }

    private async Task CloseTabsToSideAsync(TabState anchor, bool left)
    {
        var items = TabBar.Items.Cast<TabItem>().ToList();
        int anchorIndex = items.IndexOf(anchor.TabItem);
        if (anchorIndex < 0) return;

        var toClose = new List<TabState>();
        for (int i = 0; i < items.Count; i++)
        {
            if ((left && i < anchorIndex) || (!left && i > anchorIndex))
            {
                var tab = _openTabs.Values.FirstOrDefault(t => t.TabItem == items[i]);
                if (tab is not null && !tab.IsPinned)
                    toClose.Add(tab);
            }
        }

        foreach (var tab in toClose)
        {
            if (IsDistributionDirty(tab.Distribution))
            {
                var result = await ShowCloseTabDialog(tab.Distribution.Name);
                if (result == CloseTabResult.Cancel) return;
                if (result == CloseTabResult.Save) await SaveAsync();
            }
            RemoveTab(tab);
        }
    }

    private void TabBar_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTabChanged || _openTabs is null) return;

        var selected = TabBar?.SelectedItem as TabItem;
        if (selected is null) return;

        var state = _openTabs.Values.FirstOrDefault(t => t.TabItem == selected);
        if (state is null) return;

        // If control was evicted, recreate it
        if (state.DetailControl is null)
            RecreateDetailControl(state);

        ActivateTab(state);
    }

    private void RecreateDetailControl(TabState state)
    {
        var detail = new DistributionDetailControl();
        detail.GetContentFilters = () => DistributionList.ContentFilters;
        detail.Load(state.Distribution, state.UndoRedo);
        state.DetailControl = detail;
        state.TabItem.Content = detail;
    }

    private void EvictOldTabs()
    {
        if (_openTabs.Count <= MaxCachedTabs) return;

        var toEvict = _openTabs.Values
            .Where(t => t != _activeTab && t.DetailControl is not null)
            .OrderBy(t => t.LastAccessTick)
            .Take(_openTabs.Count - MaxCachedTabs)
            .ToList();

        foreach (var tab in toEvict)
        {
            tab.DetailControl = null;
            tab.TabItem.Content = new TextBlock
            {
                Text = "Click to reload...",
                Foreground = EvictedTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }

    private void OnTabUndoStateChanged(TabState state)
    {
        if (state == _activeTab)
        {
            RefreshUndoButtons();
            RefreshDirtyState();
        }
        RefreshTabDirtyDot(state);
    }

    private void RefreshTabDirtyDot(TabState state)
    {
        if (state.TabItem.Header is StackPanel header)
        {
            foreach (var child in header.Children)
            {
                if (child is Border { Tag: "dirty" } dot)
                {
                    dot.IsVisible = IsDistributionDirty(state.Distribution);
                    break;
                }
            }
        }
    }

    private static bool IsDistributionDirty(Distribution d)
    {
        if (d.IsDirty) return true;
        foreach (var c in d.Containers)
        {
            if (c.IsDirty) return true;
            foreach (var p in c.ProcListEntries)
                if (p.IsDirty) return true;
        }
        return false;
    }

    private void CloseAllTabs()
    {
        _suppressTabChanged = true;
        TabBar.Items.Clear();
        _openTabs.Clear();
        _activeTab = null;
        _suppressTabChanged = false;
    }

    // ── Close tab dialog ──

    private enum CloseTabResult { Save, Discard, Cancel }

    private async Task<CloseTabResult> ShowCloseTabDialog(string distName)
    {
        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 420,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var result = CloseTabResult.Cancel;
        var saveBtn = new Button { Content = "Save", Margin = new Thickness(0, 0, 8, 0) };
        var discardBtn = new Button { Content = "Discard", Margin = new Thickness(0, 0, 8, 0) };
        var cancelBtn = new Button { Content = "Cancel" };
        saveBtn.Click += (_, _) => { result = CloseTabResult.Save; dialog.Close(); };
        discardBtn.Click += (_, _) => { result = CloseTabResult.Discard; dialog.Close(); };
        cancelBtn.Click += (_, _) => { dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"{distName} has unsaved changes.",
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { saveBtn, discardBtn, cancelBtn }
                }
            }
        };

        await dialog.ShowDialog(this);
        return result;
    }

    // ── Navigation ──

    private void OnNavigateToDistribution(object? sender, NavigateToDistributionEventArgs e)
    {
        if (_activeTab is not null)
        {
            _activeTab.PropertiesDistribution = e.Distribution;
            RightDetail.Load(e.Distribution, _activeTab.UndoRedo);
        }
        else
        {
            RightDetail.Load(e.Distribution, new UndoRedoStack());
        }
        if (!_rightVisible) SetRightVisible(true);
    }

    private void OnDistributionOpenRequested(Distribution d)
    {
        if (!_detailVisible) return;
        OpenOrActivateTab(d);
    }

    private void OnDistributionOpenMultipleRequested(List<Distribution> distributions)
    {
        if (!_detailVisible) return;
        foreach (var d in distributions)
            OpenOrActivateTab(d);
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
        _settings.LastFolder = folder;
        _settings.Save();

        LoadingBar.IsVisible = true;
        UndoBtn.IsEnabled = false;
        RedoBtn.IsEnabled = false;

        var result = await Task.Run(() => DistributionParser.CreateDefault().Parse(folder));

        CloseAllTabs();
        _distributions = result.Distributions;
        _procComments = result.ProcComments;
        _distComments = result.DistComments;
        DistributionList.Load(result.Distributions);
        ErrorList.Load(result.Errors, folder);

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
        var written = await Task.Run(() => writer.Save(_distributions, _procComments, _distComments));

        if (written.Count > 0)
            StatusText.Text = $"Saved {written.Count} file(s)";
        else
            StatusText.Text = "No changes to save";

        RefreshDirtyState();

        // Refresh all tab dirty dots after save
        foreach (var tab in _openTabs.Values)
            RefreshTabDirtyDot(tab);
    }

    // ── Dirty state tracking ──

    private bool HasUnsavedChanges()
    {
        foreach (var d in _distributions)
        {
            if (IsDistributionDirty(d)) return true;
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
        var yesBtn = new Button { Content = "Discard", Margin = new Thickness(0, 0, 8, 0) };
        var noBtn = new Button { Content = "Cancel" };
        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click += (_, _) => { dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "You have unsaved changes. Discard them?", TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { yesBtn, noBtn } }
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
            _activeTab?.UndoRedo.Undo();
            e.Handled = true;
        }
        else if ((e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control)
                 || (e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)))
        {
            _activeTab?.UndoRedo.Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control)
        {
            if (_activeTab is not null)
                _ = CloseTabAsync(_activeTab);
            e.Handled = true;
        }
    }

    private void RefreshUndoButtons()
    {
        var stack = _activeTab?.UndoRedo;
        UndoBtn.IsEnabled = stack?.CanUndo ?? false;
        RedoBtn.IsEnabled = stack?.CanRedo ?? false;
        ToolTip.SetTip(UndoBtn, stack?.NextUndoDescription is not null
            ? $"Undo: {stack.NextUndoDescription}"
            : "Nothing to undo");
        ToolTip.SetTip(RedoBtn, stack?.NextRedoDescription is not null
            ? $"Redo: {stack.NextRedoDescription}"
            : "Nothing to redo");
    }

    private void UndoBtn_Click(object? sender, RoutedEventArgs e) => _activeTab?.UndoRedo.Undo();
    private void RedoBtn_Click(object? sender, RoutedEventArgs e) => _activeTab?.UndoRedo.Redo();

    // ── Help ──

    private HelpWindow? _helpWindow;

    private void HelpBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_helpWindow is not null && _helpWindow.IsVisible)
        {
            _helpWindow.Activate();
            return;
        }
        _helpWindow = new HelpWindow();
        _helpWindow.Show(this);
    }

    // ── Panel close buttons ──────────────────────────────────────────────────
    private void CloseListBtn_Click(object? sender, RoutedEventArgs e)   => SetListVisible(false);
    private void CloseDetailBtn_Click(object? sender, RoutedEventArgs e) => SetDetailVisible(false);
    private void CloseErrorBtn_Click(object? sender, RoutedEventArgs e)  => SetErrorVisible(false);
    private void CloseRightBtn_Click(object? sender, RoutedEventArgs e)  => SetRightVisible(false);

    // ── View menu items ──────────────────────────────────────────────────────
    private void ViewListItem_Click(object? sender, RoutedEventArgs e)   => SetListVisible(!_listVisible);
    private void ViewDetailItem_Click(object? sender, RoutedEventArgs e) => SetDetailVisible(!_detailVisible);
    private void ViewErrorItem_Click(object? sender, RoutedEventArgs e)  => SetErrorVisible(!_errorVisible);
    private void ViewRightItem_Click(object? sender, RoutedEventArgs e)  => SetRightVisible(!_rightVisible);

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

    private void SetDetailVisible(bool visible)
    {
        _detailVisible = visible;
        DetailPanelGrid.IsVisible = visible;
        ErrorPanelGrid.IsVisible = visible && _errorVisible;
        ErrorSplitter.IsVisible  = visible && _errorVisible;
        ViewDetailItem.Header = (visible ? "\u2713 " : "   ") + "Detail View";
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
