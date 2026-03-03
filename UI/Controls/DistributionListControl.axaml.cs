using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Core.Filtering;
using Core.Folders;
using Data.Data;

namespace UI.Controls;

public partial class DistributionListControl : UserControl, ITreeDragDropHost<ExplorerNode>
{
    private readonly RenameState<ExplorerNode> _rename = new();
    private readonly DistributionListState _state = new();

    public DistributionListControl()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => ApplyFilter();
        DistTree.ItemsSource = _state.RootNodes;

        FilterPillHelper.WireTriStatePills(ContentFilterPills, _state.Filter, OnFilterChanged);
        FilterPillHelper.WireTriStatePills(StructureFilterPills, _state.Filter, OnFilterChanged);

        var dragDrop = new TreeDragDropHandler<ExplorerNode>(DistTree, this);
        dragDrop.Attach();

        DistTree.DoubleTapped += OnTreeDoubleTapped;
    }

    public ContentFilterSet ContentFilters => _state.Filter.Content;

    public event Action<Distribution?>? SelectionChanged;
    public event Action<Distribution>? OpenRequested;
    public event Action<List<Distribution>>? OpenMultipleRequested;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _state.SaveExpansionState();
    }

    public void SetSettings(UserSettings settings) => _state.LoadFolders();

    public void Load(IReadOnlyList<Distribution> distributions)
    {
        _state.LoadDistributions(distributions);
        SearchBox.Text = string.Empty;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    #region Filtering

    private string SearchQuery => SearchBox.Text?.Trim() ?? string.Empty;

    private void ApplyFilter()
    {
        _state.ApplyFilter(SearchQuery);
        CountText.Text = $"{_state.LastFiltered.Count} / {_state.TotalCount}";
    }

    private void RefreshTree()
    {
        var selectedNames = CaptureSelectedNames();
        var scrollViewer  = FindScrollViewer(DistTree);
        var scrollOffset  = scrollViewer?.Offset ?? default;

        _state.RebuildTree(SearchQuery, true);

        if (selectedNames.Count > 0)
            RestoreSelection(selectedNames, _state.RootNodes);
        if (scrollViewer is not null)
            scrollViewer.Offset = scrollOffset;
    }

    private HashSet<string> CaptureSelectedNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (DistTree.SelectedItems is not null)
            foreach (var item in DistTree.SelectedItems)
                if (item is ExplorerNode { Distribution: not null } node)
                    names.Add(node.Distribution.Name);
        return names;
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

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        var node = DistTree.SelectedItem as ExplorerNode;
        switch (e.Key)
        {
            case Key.Enter:
                var dists = GetSelectedDistributionNodes();
                if (dists.Count == 1 && dists[0].Distribution is not null)
                    OpenRequested?.Invoke(dists[0].Distribution!);
                else if (dists.Count > 1)
                    OpenMultipleRequested?.Invoke(dists.Where(n => n.Distribution is not null).Select(n => n.Distribution!).ToList());
                if (dists.Count > 0) e.Handled = true;
                break;
            case Key.F2:
                if (node is { IsFolder: true })
                {
                    _rename.BeginRename(node);
                    ShowRenameOverlay(node.Name);
                    e.Handled = true;
                }
                break;
            case Key.Delete:
                if (node is { IsFolder: false, Distribution: not null })
                {
                    var names = GetSelectedDistributionNodes()
                        .Where(n => n.Distribution is not null)
                        .Select(n => n.Distribution!.Name)
                        .ToList();
                    if (names.Count > 0)
                    {
                        _state.RemoveDistributionsFromFolders(names);
                        _state.SaveFolders();
                        RefreshTree();
                        e.Handled = true;
                    }
                }
                else if (node is { IsFolder: true })
                {
                    _state.DeleteFolder(node);
                    _state.SaveFolders();
                    RefreshTree();
                    e.Handled = true;
                }
                break;
        }
    }

    #endregion

    #region ITreeDragDropHost<ExplorerNode>

    ObservableCollection<ExplorerNode> ITreeDragDropHost<ExplorerNode>.RootNodes => _state.RootNodes;
    void ITreeDragDropHost<ExplorerNode>.SaveFolders() => _state.SaveFolders();
    void ITreeDragDropHost<ExplorerNode>.RefreshTree() => RefreshTree();

    bool ITreeDragDropHost<ExplorerNode>.ExecuteNodeDrop(ExplorerNode node, ExplorerNode? targetFolder)
    {
        if (node.IsFolder)
        {
            if (!TreeControlHelper.ShowMoveFolderConfirmation(node.Name)) return false;
            var (folderDef, oldParentList) = _state.FindFolderDefinition(node);
            if (folderDef is null) return true;
            var targetDef = targetFolder is not null ? _state.FindFolderDefinition(targetFolder).folder : null;
            FolderService.MoveFolder(folderDef, oldParentList, targetDef, _state.Folders);
        }
        else if (node.Distribution is not null)
        {
            var targetDef = targetFolder is not null ? _state.FindFolderDefinition(targetFolder).folder : null;
            FolderService.MoveDistribution(node.Distribution.Name, targetDef, _state.Folders);
        }
        return true;
    }

    #endregion

    #region Context menu

    private void TreeContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var selected          = DistTree.SelectedItem as ExplorerNode;
        var selectedDistNodes = GetSelectedDistributionNodes();
        var hasDistSelection  = selectedDistNodes.Count > 0;
        var hasSingleDist     = selected is { IsFolder: false, Distribution: not null };
        var anyInFolder       = selectedDistNodes.Any(n =>
            n.Distribution is not null &&
            FolderService.FindFolderContaining(n.Distribution.Name, _state.Folders) is not null);
        var isFolder = selected is { IsFolder: true };

        OpenDistItem.IsVisible       = hasSingleDist;
        OpenSelectedDistsItem.IsVisible = selectedDistNodes.Count > 1;
        NewSubfolderItem.IsVisible   = isFolder;

        MoveToFolderMenu.Items.Clear();
        BuildMoveToFolderMenu(MoveToFolderMenu, _state.Folders, "");
        MoveToFolderMenu.IsVisible      = hasDistSelection && _state.Folders.Count > 0;
        RemoveFromFolderItem.IsVisible  = hasDistSelection && anyInFolder;

        RenameFolderItem.IsVisible      = isFolder;
        DeleteFolderItem.IsVisible      = isFolder;
        FolderExpandSeparator.IsVisible = isFolder;
        ExpandFolderItem.IsVisible      = isFolder;
        CollapseFolderItem.IsVisible    = isFolder;
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

    private void ExpandAll_Click(object? sender, RoutedEventArgs e)        => SetExpandAll(true);
    private void CollapseAll_Click(object? sender, RoutedEventArgs e)      => SetExpandAll(false);
    private void ExpandFolderAll_Click(object? sender, RoutedEventArgs e)  => SetExpandSelectedFolder(true);
    private void CollapseFolderAll_Click(object? sender, RoutedEventArgs e) => SetExpandSelectedFolder(false);

    private void SetExpandAll(bool expanded)
    {
        DistributionListState.SetExpandedRecursive(_state.RootNodes, expanded);
        _state.SaveExpansionState();
    }

    private void SetExpandSelectedFolder(bool expanded)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } folder) return;
        folder.IsExpanded = expanded;
        DistributionListState.SetExpandedRecursive(folder.Children, expanded);
        _state.SaveExpansionState();
    }

    #endregion

    #region Folder CRUD

    private void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        _rename.BeginCreate(null);
        ShowRenameOverlay("New Folder");
    }

    private void NewSubfolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } parentNode) return;
        _rename.BeginCreate(parentNode);
        ShowRenameOverlay("New Folder");
    }

    private void RenameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } node) return;
        _rename.BeginRename(node);
        ShowRenameOverlay(node.Name);
    }

    private void DeleteFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } node) return;
        _state.DeleteFolder(node);
        _state.SaveFolders();
        RefreshTree();
    }

    private void MoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not MenuItem { Tag: string folderPath }) return;
        var distNames = GetSelectedDistributionNodes()
            .Where(n => n.Distribution is not null)
            .Select(n => n.Distribution!.Name)
            .ToList();
        if (distNames.Count == 0) return;

        _state.MoveDistributionsToFolder(distNames, folderPath);
        _state.SaveFolders();
        RefreshTree();
    }

    private void RemoveFromFolder_Click(object? sender, RoutedEventArgs e)
    {
        var distNames = GetSelectedDistributionNodes()
            .Where(n => n.Distribution is not null)
            .Select(n => n.Distribution!.Name)
            .ToList();

        _state.RemoveDistributionsFromFolders(distNames);
        _state.SaveFolders();
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
        if (string.IsNullOrEmpty(newName)) { _rename.Reset(); return; }

        bool changed;
        if (_rename.IsCreating)
            changed = _state.CommitCreate(newName, _rename.NewFolderParent);
        else if (_rename.RenamingNode is not null)
            changed = _state.CommitRenameFolder(_rename.RenamingNode, newName);
        else
            changed = false;

        _rename.Reset();
        if (!changed) return;
        _state.SaveFolders();
        RefreshTree();
    }

    private void CancelRename()
    {
        RenameOverlay.IsVisible = false;
        _rename.Reset();
    }

    private void RenameBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)       { CommitRename(); e.Handled = true; }
        else if (e.Key == Key.Escape) { CancelRename(); e.Handled = true; }
    }

    private void RenameBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (RenameOverlay.IsVisible) CommitRename();
    }

    #endregion

    #region Filter pills

    private void TypeFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _state.Filter.ToggleTypeFilter(btn.Tag as string);
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private void OnFilterChanged()
    {
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private void UpdateAllPillStyles()
    {
        foreach (var child in TypeFilterPills.Children)
        {
            if (child is not Button btn) continue;
            if (_state.Filter.ActiveTypeFilter == btn.Tag as string)
                btn.Classes.Add("active");
            else
                btn.Classes.Remove("active");
        }

        FilterPillHelper.ApplyTriStateStyles(ContentFilterPills, _state.Filter);
        FilterPillHelper.ApplyTriStateStyles(StructureFilterPills, _state.Filter);
    }

    #endregion
}
