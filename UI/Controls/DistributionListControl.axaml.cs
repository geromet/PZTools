using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Core.Filtering;
using Core.Folders;
using Data.Data;

namespace UI.Controls;

public partial class DistributionListControl : UserControl, ITreeDragDropHost
{
    private List<Distribution> _all = [];
    private List<Distribution> _lastFiltered = [];
    private string? _activeTypeFilter;

    private TriState _defaultFilter;
    private TriState _procListFilter;
    private TriState _rollsFilter;
    private TriState _itemsFilter;
    private TriState _junkFilter;
    private TriState _proceduralFilter;
    private TriState _noContentFilter;
    private TriState _invalidFilter;
    private TriState _distributionItemsFilter;

    private List<FolderDefinition> _folders = [];
    private readonly ObservableCollection<ExplorerNode> _rootNodes = [];

    private ExplorerNode? _renamingNode;
    private bool _isCreatingNewFolder;
    private ExplorerNode? _newFolderParent;

    public event Action<Distribution?>? SelectionChanged;
    public event Action<Distribution>? OpenRequested;
    public event Action<List<Distribution>>? OpenMultipleRequested;

    public (TriState ProcList, TriState Rolls, TriState Items, TriState Junk, TriState Procedural,
        TriState Invalid, TriState DistributionItemsFilter) ContentFilters
        => (_procListFilter, _rollsFilter, _itemsFilter, _junkFilter, _proceduralFilter,
            _invalidFilter, _distributionItemsFilter);

    public DistributionListControl()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => ApplyFilter();

        WireRightClickHandlers(ContentFilterPills);
        WireRightClickHandlers(StructureFilterPills);

        var dragDrop = new TreeDragDropHandler(DistTree, this);
        dragDrop.Attach();

        DistTree.DoubleTapped += OnTreeDoubleTapped;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SaveExpansionState();
    }

    public void SetSettings(UserSettings settings) =>
        _folders = FolderService.DeepCopy(FolderSettings.Load());

    public void Load(IReadOnlyList<Distribution> distributions)
    {
        if (_rootNodes.Count > 0)
            SyncExpansionState(_rootNodes, _folders);

        _all = [.. distributions];
        _activeTypeFilter = null;
        _procListFilter = _rollsFilter = _itemsFilter = _junkFilter = _proceduralFilter =
            _noContentFilter = _invalidFilter = _distributionItemsFilter =
            _defaultFilter = TriState.Ignored;
        SearchBox.Text = string.Empty;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    #region Filtering

    private FilterCriteria BuildFilterCriteria() => new(
        _activeTypeFilter, _procListFilter, _rollsFilter,
        _itemsFilter, _junkFilter, _proceduralFilter,
        _noContentFilter, _invalidFilter, _distributionItemsFilter,
        SearchBox.Text?.Trim() ?? string.Empty);

    private void ApplyFilter()
    {
        _lastFiltered = DistributionFilter.Apply(_all, BuildFilterCriteria());
        BuildTree(_lastFiltered);
        CountText.Text = $"{_lastFiltered.Count} / {_all.Count}";
    }

    private void BuildTree(List<Distribution> filtered)
    {
        FolderTreeBuilder.Build(_rootNodes, _folders, filtered,
            DistributionFilter.HasAnyActiveFilter(BuildFilterCriteria()));
        DistTree.ItemsSource = _rootNodes;
    }

    #endregion

    #region Tree refresh

    private void RefreshTree()
    {
        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (DistTree.SelectedItems is not null)
            foreach (var item in DistTree.SelectedItems)
                if (item is ExplorerNode { Distribution: not null } node)
                    selectedNames.Add(node.Distribution.Name);

        var scrollViewer = FindScrollViewer(DistTree);
        var scrollOffset = scrollViewer?.Offset ?? default;

        SyncExpansionState(_rootNodes, _folders);
        BuildTree(_lastFiltered);

        if (selectedNames.Count > 0)
            RestoreSelection(selectedNames, _rootNodes);
        if (scrollViewer is not null)
            scrollViewer.Offset = scrollOffset;
    }

    private void RestoreSelection(HashSet<string> names, ObservableCollection<ExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder && node.Distribution is not null && names.Contains(node.Distribution.Name))
                DistTree.SelectedItems.Add(node);
            if (node.IsFolder && node.Children.Count > 0)
                RestoreSelection(names, node.Children);
        }
    }

    private static ScrollViewer? FindScrollViewer(Visual visual)
    {
        if (visual is ScrollViewer sv) return sv;
        foreach (var child in visual.GetVisualChildren())
        {
            var found = FindScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }

    #endregion

    #region Tree selection

    private void DistTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var item in e.AddedItems)
            if (item is ExplorerNode { IsFolder: false } node)
            {
                SelectionChanged?.Invoke(node.Distribution);
                return;
            }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual visual) return;
        var tvi = visual.FindAncestorOfType<TreeViewItem>();
        if (tvi?.DataContext is ExplorerNode { IsFolder: false, Distribution: not null } node)
        {
            OpenRequested?.Invoke(node.Distribution);
            e.Handled = true;
        }
    }

    private void OpenDistribution_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is ExplorerNode { IsFolder: false, Distribution: not null } node)
            OpenRequested?.Invoke(node.Distribution);
    }

    private void OpenSelectedDistributions_Click(object? sender, RoutedEventArgs e)
    {
        var dists = GetSelectedDistributionNodes()
            .Where(n => n.Distribution is not null)
            .Select(n => n.Distribution!)
            .ToList();
        if (dists.Count > 0)
            OpenMultipleRequested?.Invoke(dists);
    }

    private List<ExplorerNode> GetSelectedDistributionNodes()
    {
        var result = new List<ExplorerNode>();
        if (DistTree.SelectedItems is null) return result;
        foreach (var item in DistTree.SelectedItems)
            if (item is ExplorerNode { IsFolder: false, Distribution: not null } node)
                result.Add(node);
        return result;
    }

    #endregion

    #region ITreeDragDropHost

    ObservableCollection<ExplorerNode> ITreeDragDropHost.RootNodes => _rootNodes;
    List<FolderDefinition> ITreeDragDropHost.Folders => _folders;
    void ITreeDragDropHost.SaveFolders() => SaveFolders();
    void ITreeDragDropHost.RefreshTree() => RefreshTree();

    (FolderDefinition? folder, List<FolderDefinition> parentList) ITreeDragDropHost.FindFolderDefinition(
        ExplorerNode node) => FindFolderDefinition(node);

    bool ITreeDragDropHost.ShowMoveFolderConfirmation(string folderName) =>
        ShowMoveFolderConfirmation(folderName);

    private bool ShowMoveFolderConfirmation(string folderName)
    {
        var dialog = new Window
        {
            Title = "Move Folder", Width = 320, Height = 100,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false,
        };
        var result = false;
        var yesBtn = new Button { Content = "Yes", Margin = new Thickness(0, 0, 8, 0) };
        var noBtn = new Button { Content = "No" };
        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click += (_, _) => { dialog.Close(); };
        yesBtn.IsDefault = true;
        noBtn.IsCancel = true;

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(10), Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Are you sure you want to move the folder \"{folderName}\"?",
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { yesBtn, noBtn }
                }
            }
        };

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            dialog.ShowDialog(desktop.MainWindow);
        return result;
    }

    #endregion

    #region Context menu

    private void TreeContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var selected = DistTree.SelectedItem as ExplorerNode;
        var selectedDistNodes = GetSelectedDistributionNodes();
        bool hasDistSelection = selectedDistNodes.Count > 0;
        bool hasSingleDist = selected is { IsFolder: false, Distribution: not null };
        bool anyInFolder = selectedDistNodes.Any(n =>
            n.Distribution is not null &&
            FolderService.FindFolderContaining(n.Distribution.Name, _folders) is not null);
        bool isFolder = selected is { IsFolder: true };

        OpenDistItem.IsVisible = hasSingleDist;
        OpenSelectedDistsItem.IsVisible = selectedDistNodes.Count > 1;
        NewSubfolderItem.IsVisible = isFolder;

        MoveToFolderMenu.Items.Clear();
        BuildMoveToFolderMenu(MoveToFolderMenu, _folders, "");
        MoveToFolderMenu.IsVisible = hasDistSelection && _folders.Count > 0;
        RemoveFromFolderItem.IsVisible = hasDistSelection && anyInFolder;

        RenameFolderItem.IsVisible = isFolder;
        DeleteFolderItem.IsVisible = isFolder;
        FolderExpandSeparator.IsVisible = isFolder;
        ExpandFolderItem.IsVisible = isFolder;
        CollapseFolderItem.IsVisible = isFolder;
    }

    private void BuildMoveToFolderMenu(MenuItem parent, List<FolderDefinition> folders, string pathPrefix)
    {
        foreach (var folder in folders)
        {
            var path = string.IsNullOrEmpty(pathPrefix) ? folder.Name : $"{pathPrefix}/{folder.Name}";
            var item = new MenuItem { Header = folder.Name, Tag = path };
            item.Click += MoveToFolder_Click;
            parent.Items.Add(item);
            if (folder.Children is { Count: > 0 })
                BuildMoveToFolderMenu(item, folder.Children, path);
        }
    }

    #endregion

    #region Expand / collapse

    private static void SetExpandedRecursive(IEnumerable<ExplorerNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder) continue;
            node.IsExpanded = expanded;
            if (node.Children.Count > 0)
                SetExpandedRecursive(node.Children, expanded);
        }
    }

    private void ExpandAll_Click(object? sender, RoutedEventArgs e) => SetExpandAll(true);
    private void CollapseAll_Click(object? sender, RoutedEventArgs e) => SetExpandAll(false);

    private void ExpandFolderAll_Click(object? sender, RoutedEventArgs e) => SetExpandSelectedFolder(true);
    private void CollapseFolderAll_Click(object? sender, RoutedEventArgs e) => SetExpandSelectedFolder(false);

    private void SetExpandAll(bool expanded)
    {
        SetExpandedRecursive(_rootNodes, expanded);
        SaveExpansionState();
    }

    private void SetExpandSelectedFolder(bool expanded)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } folder) return;
        folder.IsExpanded = expanded;
        SetExpandedRecursive(folder.Children, expanded);
        SaveExpansionState();
    }

    #endregion

    #region Folder CRUD

    private void NewFolder_Click(object? sender, RoutedEventArgs e) =>
        BeginCreateFolder(null);

    private void NewSubfolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is ExplorerNode { IsFolder: true } parentNode)
            BeginCreateFolder(parentNode);
    }

    private void BeginCreateFolder(ExplorerNode? parent)
    {
        _isCreatingNewFolder = true;
        _renamingNode = null;
        _newFolderParent = parent;
        ShowRenameOverlay("New Folder");
    }

    private void RenameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } node) return;
        _isCreatingNewFolder = false;
        _renamingNode = node;
        _newFolderParent = null;
        ShowRenameOverlay(node.Name);
    }

    private void DeleteFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } node) return;
        var (folder, parentList) = FindFolderDefinition(node);
        if (folder is null) return;
        parentList.Remove(folder);
        SaveFolders();
        RefreshTree();
    }

    private void MoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not MenuItem { Tag: string folderPath }) return;
        var folder = FolderService.FindFolderByPath(folderPath, _folders);
        if (folder is null) return;

        foreach (var node in GetSelectedDistributionNodes())
        {
            if (node.Distribution is null) continue;
            FolderService.RemoveDistFromAllFolders(node.Distribution.Name, _folders);
            folder.DistributionNames.Add(node.Distribution.Name);
        }

        SaveFolders();
        RefreshTree();
    }

    private void RemoveFromFolder_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var node in GetSelectedDistributionNodes())
            if (node.Distribution is not null)
                FolderService.RemoveDistFromAllFolders(node.Distribution.Name, _folders);

        SaveFolders();
        RefreshTree();
    }

    #endregion

    #region Inline rename

    private void ShowRenameOverlay(string currentName)
    {
        RenameBox.Text = currentName;
        RenameOverlay.IsVisible = true;
        RenameBox.Focus();
        RenameBox.SelectAll();
    }

    private void CommitRename()
    {
        RenameOverlay.IsVisible = false;
        var newName = RenameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) { ResetRenameState(); return; }

        if (_isCreatingNewFolder)
        {
            List<FolderDefinition> targetList;
            if (_newFolderParent is not null)
            {
                var (parentDef, _) = FindFolderDefinition(_newFolderParent);
                if (parentDef is null) { ResetRenameState(); return; }
                parentDef.Children ??= [];
                targetList = parentDef.Children;
            }
            else
            {
                targetList = _folders;
            }

            if (targetList.Any(f => string.Equals(f.Name, newName, StringComparison.OrdinalIgnoreCase)))
            { ResetRenameState(); return; }

            targetList.Add(new FolderDefinition { Name = newName });
        }
        else if (_renamingNode is not null)
        {
            var (folder, _) = FindFolderDefinition(_renamingNode);
            if (folder is not null)
                folder.Name = newName;
        }

        ResetRenameState();
        SaveFolders();
        RefreshTree();
    }

    private void CancelRename()
    {
        RenameOverlay.IsVisible = false;
        ResetRenameState();
    }

    private void ResetRenameState()
    {
        _renamingNode = null;
        _isCreatingNewFolder = false;
        _newFolderParent = null;
    }

    private void RenameBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitRename(); e.Handled = true; }
        else if (e.Key == Key.Escape) { CancelRename(); e.Handled = true; }
    }

    private void RenameBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (RenameOverlay.IsVisible) CommitRename();
    }

    #endregion

    #region Folder lookup (ExplorerNode → FolderDefinition bridge)

    private List<string>? GetNodePath(ExplorerNode target)
    {
        var path = new List<string>();
        return FindNodePath(target, _rootNodes, path) ? path : null;
    }

    private static bool FindNodePath(
        ExplorerNode target, ObservableCollection<ExplorerNode> nodes, List<string> path)
    {
        foreach (var node in nodes)
        {
            if (node == target) { path.Add(node.Name); return true; }
            if (!node.IsFolder) continue;
            path.Add(node.Name);
            if (FindNodePath(target, node.Children, path)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    private (FolderDefinition? folder, List<FolderDefinition> parentList) FindFolderDefinition(
        ExplorerNode node)
    {
        var path = GetNodePath(node);
        return path is not null
            ? FolderService.FindByNodePath(path, _folders)
            : FolderService.FindByName(node.Name, _folders);
    }

    #endregion

    #region Persistence

    private void SaveFolders()
    {
        SyncExpansionState(_rootNodes, _folders);
        FolderSettings.Save(FolderService.DeepCopy(_folders));
    }

    private void SaveExpansionState()
    {
        SyncExpansionState(_rootNodes, _folders);
        FolderSettings.Save(_folders);
    }

    private static void SyncExpansionState(
        ObservableCollection<ExplorerNode> nodes, List<FolderDefinition> folders)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder) continue;
            var folder = folders.FirstOrDefault(f =>
                string.Equals(f.Name, node.Name, StringComparison.OrdinalIgnoreCase));
            if (folder is null) continue;
            folder.IsExpanded = node.IsExpanded;
            if (folder.Children is not null)
                SyncExpansionState(node.Children, folder.Children);
        }
    }

    #endregion

    #region Filter pills

    private void TypeFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string;
        _activeTypeFilter = (_activeTypeFilter == tag) ? null : tag;
        if (tag is null) _activeTypeFilter = null;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private void ContentFilterPill_Click(object? sender, RoutedEventArgs e) =>
        ToggleTriStateFilter(sender, TriState.Include);

    private void StructureFilterPill_Click(object? sender, RoutedEventArgs e) =>
        ToggleTriStateFilter(sender, TriState.Include);

    private void ContentFilterPill_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleTriStateFilterRightClick(sender, e);

    private void StructureFilterPill_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        ToggleTriStateFilterRightClick(sender, e);

    private void ToggleTriStateFilter(object? sender, TriState targetState)
    {
        if (sender is not Button btn) return;
        ref var state = ref GetFilterRef(btn.Tag as string);
        state = state == targetState ? TriState.Ignored : targetState;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private void ToggleTriStateFilterRightClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (!e.GetCurrentPoint(btn).Properties.IsRightButtonPressed) return;
        ref var state = ref GetFilterRef(btn.Tag as string);
        state = state == TriState.Exclude ? TriState.Ignored : TriState.Exclude;
        UpdateAllPillStyles();
        ApplyFilter();
        e.Handled = true;
    }

    private ref TriState GetFilterRef(string? tag)
    {
        if (tag == "Rolls") return ref _rollsFilter;
        if (tag == "Items") return ref _itemsFilter;
        if (tag == "Junk") return ref _junkFilter;
        if (tag == "Procedural") return ref _proceduralFilter;
        if (tag == "ProcList") return ref _procListFilter;
        if (tag == "Invalid") return ref _invalidFilter;
        if (tag == "NoContent") return ref _noContentFilter;
        if (tag == "DistributionItems") return ref _distributionItemsFilter;
        return ref _defaultFilter;
    }

    private void WireRightClickHandlers(Panel panel)
    {
        foreach (var child in panel.Children)
            if (child is Button btn)
                btn.PointerPressed += panel == ContentFilterPills
                    ? ContentFilterPill_PointerPressed
                    : StructureFilterPill_PointerPressed;
    }

    private void UpdateAllPillStyles()
    {
        foreach (var child in TypeFilterPills.Children)
        {
            if (child is not Button btn) continue;
            if (_activeTypeFilter == btn.Tag as string)
                btn.Classes.Add("active");
            else
                btn.Classes.Remove("active");
        }

        ApplyTriStatePillStyles(ContentFilterPills);
        ApplyTriStatePillStyles(StructureFilterPills);
    }

    private void ApplyTriStatePillStyles(Panel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is not Button btn) continue;
            var state = GetFilterRef(btn.Tag as string);
            btn.Classes.Remove("include");
            btn.Classes.Remove("exclude");
            if (state == TriState.Include) btn.Classes.Add("include");
            else if (state == TriState.Exclude) btn.Classes.Add("exclude");
        }
    }

    #endregion
}
