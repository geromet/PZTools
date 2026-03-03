using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Core;
using Data;
using Data.Comments;
using Data.Data;
using Data.Serialization;
using UI.Controls;
using UI.Controls.Helpers;
using UI.UndoRedo;

namespace UI;

public partial class MainWindow : Window
{
    #region Fields

    private readonly UserSettings _settings = UserSettings.Load();
    private string? _lastFolder;
    private IReadOnlyList<Distribution> _distributions = [];
    private CommentMap? _procComments;
    private CommentMap? _distComments;

    private const string BaseTitle = "PZ Distribution Viewer";

    // ── Panel visibility + saved sizes ──
    private bool _listVisible   = true;
    private bool _detailVisible = true;
    private bool _errorVisible  = true;
    private bool _rightVisible  = true;
    private GridLength _savedListWidth   = new(260);
    private GridLength _savedErrorHeight = new(160);
    private GridLength _savedRightWidth  = new(260);

    // ── Tab state ──
    private readonly Dictionary<string, TabState> _openTabs = new();
    private readonly AvaloniaList<TabItem> _tabItems = new();
    private TabState? _activeTab;
    private bool _suppressTabChanged;
    private const int MaxCachedTabs = 10;

    // ── Tab strip scroll ──
    private ItemsPresenter? _tabItemsPresenter;
    private ScrollViewer?   _tabScrollViewer;
    private Button?         _tabScrollLeft;
    private Button?         _tabScrollRight;
    private const double    TabScrollStep = 120;

    #endregion

    #region Construction

    public MainWindow()
    {
        InitializeComponent();

        TabBar.ItemsSource = _tabItems;
        TabBar.TemplateApplied += OnTabBarTemplateApplied;
        DistributionList.SetSettings(_settings);
        DistributionList.OpenRequested         += OnDistributionOpenRequested;
        DistributionList.OpenMultipleRequested += OnDistributionOpenMultipleRequested;
        KeyDown += OnKeyDown;
        AddHandler(ProcListEntryControl.NavigateRequestedEvent, OnNavigateToDistribution);

        RightDetail.GetContentFilters = () => DistributionList.ContentFilters;
        RightDetail.ShowToolbar = false;
        RightDetail.ShowEmpty();

        Loaded += async (_, _) =>
        {
            if (_settings.LastFolder is not null && Directory.Exists(_settings.LastFolder))
                await ParseAsync(_settings.LastFolder);
        };
    }

    #endregion

    #region Tabs — Lifecycle

    private void OpenOrActivateTab(Distribution d)
    {
        if (_openTabs.TryGetValue(d.Name, out var existing))
        {
            existing.LastAccessTick = Environment.TickCount64;
            if (existing.DetailControl is null) RecreateDetailControl(existing);
            _suppressTabChanged = true;
            TabBar.SelectedItem = existing.TabItem;
            _suppressTabChanged = false;
            ActivateTab(existing);
            return;
        }

        var state  = new TabState(d) { LastAccessTick = Environment.TickCount64 };
        var detail = new DistributionDetailControl();
        detail.GetContentFilters = () => DistributionList.ContentFilters;
        detail.Load(d, state.UndoRedo);
        state.DetailControl = detail;

        state.TabItem = new TabItem
        {
            Header  = TabHeaderHelper.Create(state, s => CloseTabAsync(s), CloseAllTabsWithPromptAsync, CloseOtherTabsAsync, CloseTabsToSideAsync),
            Content = detail
        };
        state.UndoRedo.StateChanged += () => OnTabUndoStateChanged(state);

        _openTabs[d.Name] = state;
        _suppressTabChanged = true;
        _tabItems.Add(state.TabItem);
        TabBar.SelectedItem = state.TabItem;
        _suppressTabChanged = false;

        ActivateTab(state);
        EvictOldTabs();
    }

    private void ActivateTab(TabState state)
    {
        _activeTab = state;
        state.LastAccessTick = Environment.TickCount64;

        if (state.PropertiesDistribution is not null)
            RightDetail.Load(state.PropertiesDistribution, state.UndoRedo);
        else
            RightDetail.ShowEmpty();

        RefreshUndoButtons();
        TabHeaderHelper.RefreshDirtyDot(state, DirtyCheck.IsDistributionDirty(state.Distribution));
        ScrollTabIntoView(state.TabItem);
    }

    private void RemoveTab(TabState state)
    {
        _suppressTabChanged = true;
        _tabItems.Remove(state.TabItem);
        _openTabs.Remove(state.Distribution.Name);
        _suppressTabChanged = false;

        if (_activeTab != state) return;

        _activeTab = null;
        RightDetail.ShowEmpty();

        if (_tabItems.Count == 0) { RefreshUndoButtons(); return; }

        TabBar.SelectedIndex = Math.Clamp(TabBar.SelectedIndex, 0, _tabItems.Count - 1);
        var next = _openTabs.Values.FirstOrDefault(t => t.TabItem == TabBar.SelectedItem);
        if (next is not null) ActivateTab(next);
    }

    private void RecreateDetailControl(TabState state)
    {
        var detail = new DistributionDetailControl();
        detail.GetContentFilters = () => DistributionList.ContentFilters;
        detail.Load(state.Distribution, state.UndoRedo);
        state.DetailControl    = detail;
        state.TabItem.Content  = detail;
    }

    private void EvictOldTabs()
    {
        if (_openTabs.Count <= MaxCachedTabs) return;
        foreach (var tab in _openTabs.Values
            .Where(t => t != _activeTab && t.DetailControl is not null)
            .OrderBy(t => t.LastAccessTick)
            .Take(_openTabs.Count - MaxCachedTabs))
        {
            tab.DetailControl = null;
            tab.TabItem.Content = CreateEvictedPlaceholder();
        }
    }

    private static readonly IBrush EvictedTextBrush = SolidColorBrush.Parse("#5A5A6A");
    private static TextBlock CreateEvictedPlaceholder() => new()
    {
        Text = "Click to reload...", Foreground = EvictedTextBrush,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment   = VerticalAlignment.Center
    };

    private void CloseAllTabs()
    {
        _suppressTabChanged = true;
        _tabItems.Clear();
        _openTabs.Clear();
        _activeTab = null;
        _suppressTabChanged = false;
    }

    #endregion

    #region Tabs — Close

    private async Task CloseTabAsync(TabState state, bool skipPinCheck = false)
    {
        if (!skipPinCheck && state.IsPinned) return;
        if (IsDistributionDirty(state.Distribution))
        {
            var r = await ShowCloseTabDialog(state.Distribution.Name);
            if (r == CloseTabResult.Cancel) return;
            if (r == CloseTabResult.Save) await SaveAsync();
        }
        RemoveTab(state);
    }

    // Common bulk close: iterates candidates, skips pinned, prompts for dirty
    private async Task CloseTabsAsync(IEnumerable<TabState> candidates)
    {
        foreach (var tab in candidates.Where(t => !t.IsPinned).ToList())
        {
            if (IsDistributionDirty(tab.Distribution))
            {
                var r = await ShowCloseTabDialog(tab.Distribution.Name);
                if (r == CloseTabResult.Cancel) return;
                if (r == CloseTabResult.Save) await SaveAsync();
            }
            RemoveTab(tab);
        }
    }

    private Task CloseAllTabsWithPromptAsync() =>
        CloseTabsAsync(_openTabs.Values);

    private Task CloseOtherTabsAsync(TabState keep) =>
        CloseTabsAsync(_openTabs.Values.Where(t => t != keep));

    private Task CloseTabsToSideAsync(TabState anchor, bool left)
    {
        var items = _tabItems.ToList();
        int idx = items.IndexOf(anchor.TabItem);
        if (idx < 0) return Task.CompletedTask;
        return CloseTabsAsync(_openTabs.Values.Where(t =>
        {
            int i = items.IndexOf(t.TabItem);
            return i >= 0 && (left ? i < idx : i > idx);
        }));
    }

    #endregion

    #region Tabs — State

    private void TabBar_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTabChanged) return;
        var state = _openTabs.Values.FirstOrDefault(t => t.TabItem == TabBar.SelectedItem);
        if (state is null) return;
        if (state.DetailControl is null) RecreateDetailControl(state);
        ActivateTab(state);
    }

    private void OnTabUndoStateChanged(TabState state)
    {
        if (state == _activeTab) { RefreshUndoButtons(); RefreshDirtyState(); }
        TabHeaderHelper.RefreshDirtyDot(state, DirtyCheck.IsDistributionDirty(state.Distribution));
    }

    private bool IsDistributionDirty(Distribution d) => DirtyCheck.IsDistributionDirty(d);

    #endregion

    #region Tab Strip Scrolling

    private void OnTabBarTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _tabItemsPresenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
        _tabScrollViewer   = e.NameScope.Find<ScrollViewer>("PART_TabScrollViewer");
        _tabScrollLeft     = e.NameScope.Find<Button>("PART_ScrollLeft");
        _tabScrollRight    = e.NameScope.Find<Button>("PART_ScrollRight");
        if (_tabScrollLeft  is not null) _tabScrollLeft.Click  += (_, _) => ScrollTabsLeft();
        if (_tabScrollRight is not null) _tabScrollRight.Click += (_, _) => ScrollTabsRight();
        if (_tabScrollViewer is not null) _tabScrollViewer.ScrollChanged += (_, _) => UpdateScrollButtonStates();
        UpdateScrollButtonStates();
    }

    private void ScrollTabsLeft()
    {
        if (_tabScrollViewer is null) return;
        _tabScrollViewer.Offset = new Vector(Math.Max(0, _tabScrollViewer.Offset.X - TabScrollStep), 0);
    }

    private void ScrollTabsRight()
    {
        if (_tabScrollViewer is null) return;
        var max = _tabScrollViewer.Extent.Width - _tabScrollViewer.Viewport.Width;
        _tabScrollViewer.Offset = new Vector(Math.Min(_tabScrollViewer.Offset.X + TabScrollStep, Math.Max(0, max)), 0);
    }

    private void UpdateScrollButtonStates()
    {
        if (_tabScrollLeft is null || _tabScrollRight is null || _tabScrollViewer is null) return;
        var extent   = _tabScrollViewer.Extent.Width;
        var viewport = _tabScrollViewer.Viewport.Width;
        bool over    = extent > viewport + 0.5;

        _tabScrollLeft.Opacity           = over ? 1 : 0;
        _tabScrollLeft.IsHitTestVisible  = over;
        _tabScrollRight.Opacity          = over ? 1 : 0;
        _tabScrollRight.IsHitTestVisible = over;

        if (over)
        {
            var offset = _tabScrollViewer.Offset.X;
            _tabScrollLeft.IsEnabled  = offset > 0.5;
            _tabScrollRight.IsEnabled = offset < extent - viewport - 0.5;
        }
    }

    private void ScrollTabIntoView(TabItem tabItem)
    {
        if (_tabScrollViewer is null || _tabItemsPresenter is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            var container = TabBar.ContainerFromItem(tabItem);
            if (container is null) return;
            var pos = container.TranslatePoint(new Point(0, 0), _tabItemsPresenter);
            if (pos is null) return;
            var left    = pos.Value.X;
            var right   = left + container.Bounds.Width;
            var offset  = _tabScrollViewer.Offset.X;
            var vp      = _tabScrollViewer.Viewport.Width;
            if (left < offset)           _tabScrollViewer.Offset = new Vector(Math.Max(0, left), 0);
            else if (right > offset + vp) _tabScrollViewer.Offset = new Vector(right - vp, 0);
        }, DispatcherPriority.Loaded);
    }

    #endregion

    #region Navigation

    private void OnNavigateToDistribution(object? sender, NavigateToDistributionEventArgs e)
    {
        if (_activeTab is not null) _activeTab.PropertiesDistribution = e.Distribution;
        RightDetail.Load(e.Distribution, _activeTab?.UndoRedo ?? new UndoRedoStack());
        if (!_rightVisible) SetPanelVisible("Right", true);
    }

    private void OnDistributionOpenRequested(Distribution d)
    {
        if (_detailVisible) OpenOrActivateTab(d);
    }

    private void OnDistributionOpenMultipleRequested(List<Distribution> distributions)
    {
        if (!_detailVisible || distributions.Count == 0) return;
        if (distributions.Count == 1) { OpenOrActivateTab(distributions[0]); return; }

        _suppressTabChanged = true;
        TabState? last = null;
        var newItems = new List<TabItem>();

        foreach (var d in distributions)
        {
            if (_openTabs.TryGetValue(d.Name, out var existing))
            {
                existing.LastAccessTick = Environment.TickCount64;
                last = existing;
                continue;
            }
            var state = new TabState(d) { LastAccessTick = Environment.TickCount64 };
            state.TabItem = new TabItem
            {
                Header  = TabHeaderHelper.Create(state, s => CloseTabAsync(s), CloseAllTabsWithPromptAsync, CloseOtherTabsAsync, CloseTabsToSideAsync),
                Content = CreateEvictedPlaceholder()
            };
            state.UndoRedo.StateChanged += () => OnTabUndoStateChanged(state);
            _openTabs[d.Name] = state;
            newItems.Add(state.TabItem);
            last = state;
        }

        if (last is null) { _suppressTabChanged = false; return; }
        if (newItems.Count > 0) _tabItems.AddRange(newItems);
        if (last.DetailControl is null) RecreateDetailControl(last);
        TabBar.SelectedItem = last.TabItem;
        _suppressTabChanged = false;

        ActivateTab(last);
        EvictOldTabs();
    }

    #endregion

    #region File

    private async void SelectFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (HasUnsavedChanges() && !await ConfirmDiscardChanges()) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select game or mod folder" });
        if (folders.Count == 0) return;
        var path = StorageProviderExtensions.TryGetLocalPath(folders[0]);
        if (path is not null) await ParseAsync(path);
    }

    private async void Reload_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastFolder is null) return;
        if (HasUnsavedChanges() && !await ConfirmDiscardChanges()) return;
        await ParseAsync(_lastFolder);
    }

    private async Task ParseAsync(string folder)
    {
        _lastFolder = _settings.LastFolder = folder;
        _settings.Save();

        LoadingBar.IsVisible = true;
        UndoBtn.IsEnabled = RedoBtn.IsEnabled = false;

        var result = await Task.Run(() => DistributionParser.CreateDefault().Parse(folder));

        CloseAllTabs();
        _distributions = result.Distributions;
        _procComments  = result.ProcComments;
        _distComments  = result.DistComments;
        DistributionList.Load(result.Distributions);
        ErrorList.Load(result.Errors, folder);

        StatusText.Text  = $"{result.Distributions.Count} distributions loaded \u00b7 {result.Errors.Count} parse issues";
        CountText.Text   = string.Empty;
        LoadingBar.IsVisible = false;
        RefreshDirtyState();
    }

    #endregion

    #region Save

    private async void Save_Click(object? sender, RoutedEventArgs e) => await SaveAsync();

    private async Task SaveAsync()
    {
        if (_distributions.Count == 0) return;
        SaveBtn.IsEnabled = false;
        StatusText.Text = "Saving...";
        var written = await Task.Run(() => new DistributionFileWriter().Save(_distributions, _procComments, _distComments));
        StatusText.Text = written.Count > 0 ? $"Saved {written.Count} file(s)" : "No changes to save";
        RefreshDirtyState();
        foreach (var tab in _openTabs.Values)
            TabHeaderHelper.RefreshDirtyDot(tab, DirtyCheck.IsDistributionDirty(tab.Distribution));
    }

    #endregion

    #region Dirty State

    private bool HasUnsavedChanges() => _distributions.Any(DirtyCheck.IsDistributionDirty);

    private void RefreshDirtyState()
    {
        bool dirty = HasUnsavedChanges();
        SaveBtn.IsEnabled = dirty;
        Title = dirty ? $"{BaseTitle} *" : BaseTitle;
    }

    #endregion

    #region Undo

    private void RefreshUndoButtons()
    {
        var stack = _activeTab?.UndoRedo;
        UndoBtn.IsEnabled = stack?.CanUndo ?? false;
        RedoBtn.IsEnabled = stack?.CanRedo ?? false;
        ToolTip.SetTip(UndoBtn, stack?.NextUndoDescription is not null ? $"Undo: {stack.NextUndoDescription}" : "Nothing to undo");
        ToolTip.SetTip(RedoBtn, stack?.NextRedoDescription is not null ? $"Redo: {stack.NextRedoDescription}" : "Nothing to redo");
    }

    private void UndoBtn_Click(object? sender, RoutedEventArgs e) => _activeTab?.UndoRedo.Undo();
    private void RedoBtn_Click(object? sender, RoutedEventArgs e) => _activeTab?.UndoRedo.Redo();

    #endregion

    #region Keyboard

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key, e.KeyModifiers)
        {
            case (Key.S, KeyModifiers.Control):                        _ = SaveAsync();                    e.Handled = true; break;
            case (Key.Z, KeyModifiers.Control):                        _activeTab?.UndoRedo.Undo();        e.Handled = true; break;
            case (Key.Y, KeyModifiers.Control):                        _activeTab?.UndoRedo.Redo();        e.Handled = true; break;
            case (Key.Z, KeyModifiers.Control | KeyModifiers.Shift):   _activeTab?.UndoRedo.Redo();        e.Handled = true; break;
            case (Key.W, KeyModifiers.Control): if (_activeTab is not null) _ = CloseTabAsync(_activeTab); e.Handled = true; break;
        }
    }

    #endregion

    #region Help

    private HelpWindow? _helpWindow;

    private void HelpBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_helpWindow?.IsVisible == true) { _helpWindow.Activate(); return; }
        (_helpWindow = new HelpWindow()).Show(this);
    }

    #endregion

    #region Panels

    private void ClosePanelBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string tag }) SetPanelVisible(tag, false);
    }

    private void ViewMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string tag })
            SetPanelVisible(tag, !IsPanelVisible(tag));
    }

    private bool IsPanelVisible(string tag) => tag switch
    {
        "List"   => _listVisible,
        "Detail" => _detailVisible,
        "Error"  => _errorVisible,
        "Right"  => _rightVisible,
        _        => false
    };

    private void SetPanelVisible(string tag, bool visible)
    {
        switch (tag)
        {
            case "List":   SetListVisible(visible);   break;
            case "Detail": SetDetailVisible(visible); break;
            case "Error":  SetErrorVisible(visible);  break;
            case "Right":  SetRightVisible(visible);  break;
        }
    }

    private void SetListVisible(bool v)
    {
        if (_listVisible == v) return;
        if (_listVisible) _savedListWidth = MainGrid.ColumnDefinitions[0].Width;
        _listVisible = v;
        LeftPanelGrid.IsVisible = v;
        LeftSplitter.IsVisible  = v;
        MainGrid.ColumnDefinitions[0].MinWidth = v ? 100 : 0;
        MainGrid.ColumnDefinitions[0].Width    = v ? _savedListWidth : new GridLength(0);
        MainGrid.ColumnDefinitions[1].Width    = v ? new GridLength(4) : new GridLength(0);
        RefreshViewMenuHeaders();
    }

    private void SetDetailVisible(bool v)
    {
        _detailVisible = v;
        DetailPanelGrid.IsVisible = v;
        ErrorPanelGrid.IsVisible  = v && _errorVisible;
        ErrorSplitter.IsVisible   = v && _errorVisible;
        RefreshViewMenuHeaders();
    }

    private void SetErrorVisible(bool v)
    {
        if (_errorVisible == v) return;
        if (_errorVisible) _savedErrorHeight = MainGrid.RowDefinitions[2].Height;
        _errorVisible = v;
        ErrorPanelGrid.IsVisible = v;
        ErrorSplitter.IsVisible  = v;
        MainGrid.RowDefinitions[2].MinHeight = v ? 50 : 0;
        MainGrid.RowDefinitions[2].Height    = v ? _savedErrorHeight : new GridLength(0);
        MainGrid.RowDefinitions[1].Height    = v ? new GridLength(4) : new GridLength(0);
        RefreshViewMenuHeaders();
    }

    private void SetRightVisible(bool v)
    {
        if (_rightVisible == v) return;
        if (_rightVisible) _savedRightWidth = MainGrid.ColumnDefinitions[4].Width;
        _rightVisible = v;
        RightPanelGrid.IsVisible = v;
        RightSplitter.IsVisible  = v;
        MainGrid.ColumnDefinitions[4].MinWidth = v ? 100 : 0;
        MainGrid.ColumnDefinitions[4].Width    = v ? _savedRightWidth : new GridLength(0);
        MainGrid.ColumnDefinitions[3].Width    = v ? new GridLength(4) : new GridLength(0);
        RefreshViewMenuHeaders();
    }

    private static string Checkmark(bool v) => v ? "\u2713 " : "   ";

    private void RefreshViewMenuHeaders()
    {
        ViewListItem.Header   = Checkmark(_listVisible)   + "Distribution List";
        ViewDetailItem.Header = Checkmark(_detailVisible) + "Detail View";
        ViewErrorItem.Header  = Checkmark(_errorVisible)  + "Error List";
        ViewRightItem.Header  = Checkmark(_rightVisible)  + "Properties";
    }

    #endregion

    #region Dialogs

    private enum CloseTabResult { Save, Discard, Cancel }

    private async Task<CloseTabResult> ShowCloseTabDialog(string distName)
    {
        var result = CloseTabResult.Cancel;
        var dialog = new Window
        {
            Title = "Unsaved Changes", Width = 420, Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false
        };
        var save    = new Button { Content = "Save" };
        var discard = new Button { Content = "Discard" };
        var cancel  = new Button { Content = "Cancel" };
        save.Click    += (_, _) => { result = CloseTabResult.Save;    dialog.Close(); };
        discard.Click += (_, _) => { result = CloseTabResult.Discard; dialog.Close(); };
        cancel.Click  += (_, _) => { dialog.Close(); };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 16,
            Children =
            {
                new TextBlock { Text = $"{distName} has unsaved changes.", TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8, Children = { save, discard, cancel } }
            }
        };
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<bool> ConfirmDiscardChanges()
    {
        bool result = false;
        var dialog = new Window
        {
            Title = "Unsaved Changes", Width = 400, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false
        };
        var yes = new Button { Content = "Discard" };
        var no  = new Button { Content = "Cancel" };
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click  += (_, _) => { dialog.Close(); };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 16,
            Children =
            {
                new TextBlock { Text = "You have unsaved changes. Discard them?", TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8, Children = { yes, no } }
            }
        };
        await dialog.ShowDialog(this);
        return result;
    }

    #endregion
}
