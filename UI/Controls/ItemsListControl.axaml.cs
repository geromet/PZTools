using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Core.Filtering;
using Core.Items;

namespace UI.Controls;

public partial class ItemsListControl : UserControl, ITriStateFilterSource
{
    private ItemIndex? _index;
    private List<ItemFolderDefinition> _folders = [];
    private readonly ObservableCollection<ItemExplorerNode> _rootNodes = [];
    private string? _activeTypeFilter;

    private TriState _itemsFilter;
    private TriState _junkFilter;
    private TriState _fallback;

    // Rename state
    private ItemExplorerNode? _renamingNode;
    private bool _isCreatingFolder;
    private ItemExplorerNode? _newFolderParent;

    public ItemsListControl()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => ApplyFilter();
        ItemTree.ItemsSource = _rootNodes;

        FilterPillHelper.WireTriStatePills(ItemTypeFilterPills, this, OnItemTypeFilterChanged);
        ItemTree.DoubleTapped += OnTreeDoubleTapped;
    }

    public event Action<string>? ItemOpenRequested;

    #region ITriStateFilterSource

    ref TriState ITriStateFilterSource.GetRef(string? tag)
    {
        if (tag == "Items") return ref _itemsFilter;
        if (tag == "Junk") return ref _junkFilter;
        return ref _fallback;
    }

    #endregion

    #region Load / Filter

    public void Load(ItemIndex index)
    {
        _index = index;
        _folders = ItemFolderSettings.Load();
        _itemsFilter = _junkFilter = TriState.Ignored;
        _activeTypeFilter = null;
        SearchBox.Text = string.Empty;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private string SearchQuery => SearchBox.Text?.Trim() ?? string.Empty;

    private void ApplyFilter()
    {
        if (_index is null) return;

        bool? isJunk = (_itemsFilter, _junkFilter) switch
        {
            (TriState.Include, TriState.Ignored) => false,
            (TriState.Ignored, TriState.Include) => true,
            _ => null
        };

        var filtered = _index.GetFiltered(SearchQuery, _activeTypeFilter, isJunk);
        ItemFolderTreeBuilder.Build(_rootNodes, _folders, filtered, hideEmptyFolders: false);
        CountText.Text = $"{filtered.Count} / {_index.SortedItems.Count}";
    }

    private void RefreshTree()
    {
        if (_index is null) return;
        SyncExpansionState();
        bool? isJunk = (_itemsFilter, _junkFilter) switch
        {
            (TriState.Include, TriState.Ignored) => false,
            (TriState.Ignored, TriState.Include) => true,
            _ => null
        };
        var filtered = _index.GetFiltered(SearchQuery, _activeTypeFilter, isJunk);
        ItemFolderTreeBuilder.Build(_rootNodes, _folders, filtered, hideEmptyFolders: false);
    }

    #endregion

    #region Tree double-tap

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual visual) return;
        var tvi = visual.FindAncestorOfType<TreeViewItem>();
        if (tvi?.DataContext is ItemExplorerNode { IsFolder: false, ItemName: not null } node)
        {
            ItemOpenRequested?.Invoke(node.ItemName);
            e.Handled = true;
        }
    }

    #endregion

    #region Context menu

    private void TreeContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var selected = ItemTree.SelectedItem as ItemExplorerNode;
        var isFolder = selected is { IsFolder: true };
        var isItem   = selected is { IsFolder: false, ItemName: not null };
        var inFolder = isItem && selected?.ItemName is not null
            && ItemFolderService.FindFolderContaining(selected.ItemName, _folders) is not null;

        NewSubfolderItem.IsVisible = isFolder;
        MoveToFolderMenu.Items.Clear();
        BuildMoveToFolderMenu(MoveToFolderMenu, _folders, "");
        MoveToFolderMenu.IsVisible = isItem && _folders.Count > 0;
        RemoveFromFolderItem.IsVisible = isItem && inFolder;

        RenameFolderItem.IsVisible = isFolder;
        DeleteFolderItem.IsVisible = isFolder;
        FolderExpandSeparator.IsVisible = isFolder;
        ExpandFolderItem.IsVisible = isFolder;
        CollapseFolderItem.IsVisible = isFolder;
    }

    private void BuildMoveToFolderMenu(MenuItem parent, List<ItemFolderDefinition> folders, string pathPrefix)
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

    #region Expand / Collapse

    private void ExpandAll_Click(object? sender, RoutedEventArgs e)
        => SetExpandAll(true);

    private void CollapseAll_Click(object? sender, RoutedEventArgs e)
        => SetExpandAll(false);

    private void ExpandFolderAll_Click(object? sender, RoutedEventArgs e)
        => SetExpandSelectedFolder(true);

    private void CollapseFolderAll_Click(object? sender, RoutedEventArgs e)
        => SetExpandSelectedFolder(false);

    private void SetExpandAll(bool expanded)
    {
        SetExpandedRecursive(_rootNodes, expanded);
        SyncExpansionState();
        ItemFolderSettings.Save(_folders);
    }

    private void SetExpandSelectedFolder(bool expanded)
    {
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: true } folder) return;
        folder.IsExpanded = expanded;
        SetExpandedRecursive(folder.Children, expanded);
        SyncExpansionState();
        ItemFolderSettings.Save(_folders);
    }

    private static void SetExpandedRecursive(IEnumerable<ItemExplorerNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder) continue;
            node.IsExpanded = expanded;
            SetExpandedRecursive(node.Children, expanded);
        }
    }

    #endregion

    #region Folder CRUD

    private void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        _isCreatingFolder = true;
        _newFolderParent = null;
        _renamingNode = null;
        ShowRenameOverlay("New Folder");
    }

    private void NewSubfolder_Click(object? sender, RoutedEventArgs e)
    {
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: true } parentNode) return;
        _isCreatingFolder = true;
        _newFolderParent = parentNode;
        _renamingNode = null;
        ShowRenameOverlay("New Folder");
    }

    private void RenameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: true } node) return;
        _isCreatingFolder = false;
        _renamingNode = node;
        _newFolderParent = null;
        ShowRenameOverlay(node.Name);
    }

    private void DeleteFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: true } node) return;
        DeleteFolderNode(node);
        SaveFolders();
        RefreshTree();
    }

    private void DeleteFolderNode(ItemExplorerNode node)
    {
        var (folder, parentList) = FindFolderDefinitionByNode(node);
        if (folder is null) return;
        parentList.Remove(folder);
    }

    private void MoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not MenuItem { Tag: string folderPath }) return;
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: false, ItemName: not null } node) return;

        var target = FindFolderByPath(folderPath, _folders);
        if (target is null) return;
        ItemFolderService.MoveItem(node.ItemName, target, _folders);
        SaveFolders();
        RefreshTree();
    }

    private void RemoveFromFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: false, ItemName: not null } node) return;
        ItemFolderService.RemoveItemFromAllFolders(node.ItemName, _folders);
        SaveFolders();
        RefreshTree();
    }

    private static ItemFolderDefinition? FindFolderByPath(string path, List<ItemFolderDefinition> folders)
    {
        var parts = path.Split('/');
        var current = folders;
        ItemFolderDefinition? result = null;
        foreach (var part in parts)
        {
            result = current.FirstOrDefault(f =>
                string.Equals(f.Name, part, StringComparison.OrdinalIgnoreCase));
            if (result is null) return null;
            current = result.Children ?? [];
        }
        return result;
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
        if (string.IsNullOrEmpty(newName))
        {
            ResetRenameState();
            return;
        }

        bool changed;
        if (_isCreatingFolder)
            changed = CommitCreate(newName, _newFolderParent);
        else if (_renamingNode is not null)
            changed = CommitRenameFolder(_renamingNode, newName);
        else
            changed = false;

        ResetRenameState();
        if (!changed) return;
        SaveFolders();
        RefreshTree();
    }

    private bool CommitCreate(string newName, ItemExplorerNode? parentNode)
    {
        List<ItemFolderDefinition> targetList;
        if (parentNode is not null)
        {
            var (parentDef, _) = FindFolderDefinitionByNode(parentNode);
            if (parentDef is null) return false;
            parentDef.Children ??= [];
            targetList = parentDef.Children;
        }
        else
        {
            targetList = _folders;
        }

        if (targetList.Any(f => string.Equals(f.Name, newName, StringComparison.OrdinalIgnoreCase)))
            return false;

        targetList.Add(new ItemFolderDefinition { Name = newName });
        return true;
    }

    private bool CommitRenameFolder(ItemExplorerNode node, string newName)
    {
        var (folder, _) = FindFolderDefinitionByNode(node);
        if (folder is null) return false;
        folder.Name = newName;
        return true;
    }

    private void CancelRename()
    {
        RenameOverlay.IsVisible = false;
        ResetRenameState();
    }

    private void ResetRenameState()
    {
        _renamingNode = null;
        _isCreatingFolder = false;
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

    #region Folder lookup

    private (ItemFolderDefinition? folder, List<ItemFolderDefinition> parentList) FindFolderDefinitionByNode(
        ItemExplorerNode node)
    {
        var path = GetNodePath(node);
        return path is not null
            ? ItemFolderService.FindByNodePath(path, _folders)
            : ItemFolderService.FindByName(node.Name, _folders);
    }

    private List<string>? GetNodePath(ItemExplorerNode target)
    {
        var path = new List<string>();
        return FindNodePath(target, _rootNodes, path) ? path : null;
    }

    private static bool FindNodePath(
        ItemExplorerNode target,
        IEnumerable<ItemExplorerNode> nodes,
        List<string> path)
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

    #endregion

    #region Persistence

    private void SaveFolders()
    {
        SyncExpansionState();
        ItemFolderSettings.Save(_folders);
    }

    private void SyncExpansionState()
        => SyncExpansionState(_rootNodes, _folders);

    private static void SyncExpansionState(
        IEnumerable<ItemExplorerNode> nodes, List<ItemFolderDefinition> folders)
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

    private void OnItemTypeFilterChanged()
    {
        UpdateAllPillStyles();
        ApplyFilter();
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

        FilterPillHelper.ApplyTriStateStyles(ItemTypeFilterPills, this);
    }

    #endregion
}

