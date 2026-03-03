using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Core.Items;

namespace UI.Controls;

public partial class ItemsListControl : UserControl, ITreeDragDropHost<ItemExplorerNode>
{
    private readonly ItemsListState _state = new();
    private readonly RenameState<ItemExplorerNode> _rename = new();

    public ItemsListControl()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => ApplyFilter();
        ItemTree.ItemsSource = _state.RootNodes;
        FilterPillHelper.WireTriStatePills(ItemTypeFilterPills, _state, OnItemTypeFilterChanged);
        ItemTree.DoubleTapped += OnTreeDoubleTapped;

        var dragDrop = new TreeDragDropHandler<ItemExplorerNode>(ItemTree, this);
        dragDrop.Attach();
    }

    public event Action<string>? ItemOpenRequested;
    public event Action? FilterChanged;

    public ItemFilterContext FilterContext => _state.GetFilterContext();

    #region Load / Filter

    public void Load(ItemIndex index)
    {
        _state.Load(index);
        SearchBox.Text = string.Empty;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private string SearchQuery => SearchBox.Text?.Trim() ?? string.Empty;

    private void ApplyFilter()
    {
        var (filtered, total) = _state.ApplyFilter(SearchHelper.BuildPredicate(SearchQuery));
        CountText.Text = $"{filtered} / {total}";
        FilterChanged?.Invoke();
    }

    private void RefreshTree() =>
        _state.RebuildTree(SearchHelper.BuildPredicate(SearchQuery), syncExpansion: true);

    #endregion

    #region Tree selection + keyboard

    private List<ItemExplorerNode> GetSelectedItemNodes()
    {
        var result = new List<ItemExplorerNode>();
        if (ItemTree.SelectedItems is null) return result;
        foreach (var item in ItemTree.SelectedItems)
            if (item is ItemExplorerNode { IsFolder: false, ItemName: not null } node)
                result.Add(node);
        return result;
    }

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

    private void OpenItem_Click(object? sender, RoutedEventArgs e)
    {
        if (ItemTree.SelectedItem is ItemExplorerNode { IsFolder: false, ItemName: not null } node)
            ItemOpenRequested?.Invoke(node.ItemName);
    }

    private void OpenSelectedItems_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var node in GetSelectedItemNodes())
            ItemOpenRequested?.Invoke(node.ItemName!);
    }

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        var node = ItemTree.SelectedItem as ItemExplorerNode;
        switch (e.Key)
        {
            case Key.Enter:
                var items = GetSelectedItemNodes();
                foreach (var n in items)
                    ItemOpenRequested?.Invoke(n.ItemName!);
                if (items.Count > 0) e.Handled = true;
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
                if (node is { IsFolder: false, ItemName: not null })
                {
                    var selected = GetSelectedItemNodes();
                    foreach (var n in selected)
                        if (ItemFolderService.FindFolderContaining(n.ItemName!, _state.Folders) is not null)
                            _state.RemoveItemFromFolders(n.ItemName!);
                    if (selected.Count > 0)
                    {
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

    #region Context menu

    private void TreeContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var selected         = ItemTree.SelectedItem as ItemExplorerNode;
        var selectedItems    = GetSelectedItemNodes();
        var hasSingleItem    = selected is { IsFolder: false, ItemName: not null };
        var hasItemSelection = selectedItems.Count > 0;
        var isFolder         = selected is { IsFolder: true };
        var anyInFolder      = selectedItems.Any(n =>
            n.ItemName is not null &&
            ItemFolderService.FindFolderContaining(n.ItemName, _state.Folders) is not null);

        OpenItemItem.IsVisible          = hasSingleItem;
        OpenSelectedItemsItem.IsVisible = selectedItems.Count > 1;
        NewSubfolderItem.IsVisible      = isFolder;

        MoveToFolderMenu.Items.Clear();
        BuildMoveToFolderMenu(MoveToFolderMenu, _state.Folders, "");
        MoveToFolderMenu.IsVisible     = hasItemSelection && _state.Folders.Count > 0;
        RemoveFromFolderItem.IsVisible = hasItemSelection && anyInFolder;

        RenameFolderItem.IsVisible      = isFolder;
        DeleteFolderItem.IsVisible      = isFolder;
        FolderExpandSeparator.IsVisible = isFolder;
        ExpandFolderItem.IsVisible      = isFolder;
        CollapseFolderItem.IsVisible    = isFolder;
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

    private void ExpandCollapseAll_Click(object? sender, RoutedEventArgs e)
        => SetExpandAll(sender is Control { Tag: "True" });

    private void ExpandCollapseFolder_Click(object? sender, RoutedEventArgs e)
        => SetExpandSelectedFolder(sender is Control { Tag: "True" });

    private void SetExpandAll(bool expanded)
    {
        ItemsListState.SetExpandedRecursive(_state.RootNodes, expanded);
        _state.SaveFolders();
    }

    private void SetExpandSelectedFolder(bool expanded)
    {
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: true } folder) return;
        folder.IsExpanded = expanded;
        ItemsListState.SetExpandedRecursive(folder.Children, expanded);
        _state.SaveFolders();
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
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: true } parentNode) return;
        _rename.BeginCreate(parentNode);
        ShowRenameOverlay("New Folder");
    }

    private void RenameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: true } node) return;
        _rename.BeginRename(node);
        ShowRenameOverlay(node.Name);
    }

    private void DeleteFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (ItemTree.SelectedItem is not ItemExplorerNode { IsFolder: true } node) return;
        _state.DeleteFolder(node);
        _state.SaveFolders();
        RefreshTree();
    }

    private void MoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not MenuItem { Tag: string folderPath }) return;
        var itemNames = GetSelectedItemNodes()
            .Where(n => n.ItemName is not null)
            .Select(n => n.ItemName!)
            .ToList();
        foreach (var name in itemNames)
            _state.MoveItemToFolder(name, folderPath);
        if (itemNames.Count > 0)
        {
            _state.SaveFolders();
            RefreshTree();
        }
    }

    private void RemoveFromFolder_Click(object? sender, RoutedEventArgs e)
    {
        var itemNames = GetSelectedItemNodes()
            .Where(n => n.ItemName is not null)
            .Select(n => n.ItemName!)
            .ToList();
        foreach (var name in itemNames)
            _state.RemoveItemFromFolders(name);
        if (itemNames.Count > 0)
        {
            _state.SaveFolders();
            RefreshTree();
        }
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

        var changed = _rename.IsCreating
            ? _state.CommitCreate(newName, _rename.NewFolderParent)
            : _rename.RenamingNode is not null && _state.CommitRenameFolder(_rename.RenamingNode, newName);

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

    #region ITreeDragDropHost<ItemExplorerNode>

    ObservableCollection<ItemExplorerNode> ITreeDragDropHost<ItemExplorerNode>.RootNodes => _state.RootNodes;
    void ITreeDragDropHost<ItemExplorerNode>.SaveFolders() => _state.SaveFolders();
    void ITreeDragDropHost<ItemExplorerNode>.RefreshTree() => RefreshTree();

    bool ITreeDragDropHost<ItemExplorerNode>.ExecuteNodeDrop(ItemExplorerNode node, ItemExplorerNode? targetFolder)
    {
        if (node.IsFolder)
        {
            if (!TreeControlHelper.ShowMoveFolderConfirmation(node.Name)) return false;
            var (folderDef, oldParentList) = _state.FindFolderDefinitionByNode(node);
            if (folderDef is null) return true;
            var targetDef = targetFolder is not null ? _state.FindFolderDefinitionByNode(targetFolder).folder : null;
            ItemFolderService.MoveFolder(folderDef, oldParentList, targetDef, _state.Folders);
        }
        else if (node.ItemName is not null)
        {
            var targetDef = targetFolder is not null ? _state.FindFolderDefinitionByNode(targetFolder).folder : null;
            ItemFolderService.MoveItem(node.ItemName, targetDef, _state.Folders);
        }
        return true;
    }

    #endregion

    #region Filter pills

    private void TypeFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string;
        _state.ActiveTypeFilter = _state.ActiveTypeFilter == tag ? null : tag;
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
        FilterPillHelper.ApplySingleSelectStyles(TypeFilterPills, _state.ActiveTypeFilter);
        FilterPillHelper.ApplyTriStateStyles(ItemTypeFilterPills, _state);
    }

    #endregion
}
