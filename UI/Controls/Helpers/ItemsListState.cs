using System.Collections.ObjectModel;
using Core.Filtering;
using Core.Items;

namespace UI.Controls;

public class ItemsListState : ITriStateFilterSource
{
    private TriState _itemsFilter;
    private TriState _junkFilter;
    private TriState _fallback;
    private ItemIndex? _index;

    public string? ActiveTypeFilter { get; set; }
    public List<ItemFolderDefinition> Folders { get; set; } = [];
    public ObservableCollection<ItemExplorerNode> RootNodes { get; } = [];

    #region ITriStateFilterSource

    ref TriState ITriStateFilterSource.GetRef(string? tag)
    {
        if (tag == "Items") return ref _itemsFilter;
        if (tag == "Junk")  return ref _junkFilter;
        return ref _fallback;
    }

    #endregion

    #region Load / Filter

    public void Load(ItemIndex index)
    {
        _index = index;
        _itemsFilter = _junkFilter = TriState.Ignored;
        ActiveTypeFilter = null;
        Folders = ItemFolderSettings.Load();
    }

    public ItemFilterContext GetFilterContext() => new(ActiveTypeFilter, GetIsJunk());

    public (int filtered, int total) ApplyFilter(Func<string, bool>? predicate)
    {
        if (_index is null) return (0, 0);
        var filtered = _index.GetFiltered(predicate, ActiveTypeFilter, GetIsJunk());
        ItemFolderTreeBuilder.Build(RootNodes, Folders, filtered, hideEmptyFolders: false);
        return (filtered.Count, _index.SortedItems.Count);
    }

    public void RebuildTree(Func<string, bool>? predicate, bool syncExpansion = false)
    {
        if (_index is null) return;
        if (syncExpansion) SyncExpansionState();
        var filtered = _index.GetFiltered(predicate, ActiveTypeFilter, GetIsJunk());
        ItemFolderTreeBuilder.Build(RootNodes, Folders, filtered, hideEmptyFolders: false);
    }

    private bool? GetIsJunk() => (_itemsFilter, _junkFilter) switch
    {
        (TriState.Include, TriState.Ignored) => false,
        (TriState.Ignored, TriState.Include) => true,
        _ => null
    };

    #endregion

    #region Expansion

    public static void SetExpandedRecursive(IEnumerable<ItemExplorerNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder) continue;
            node.IsExpanded = expanded;
            SetExpandedRecursive(node.Children, expanded);
        }
    }

    #endregion

    #region Folder mutations

    public void DeleteFolder(ItemExplorerNode node)
    {
        var (folder, parentList) = FindFolderDefinitionByNode(node);
        if (folder is null) return;
        parentList.Remove(folder);
    }

    public void MoveItemToFolder(string itemName, string folderPath)
    {
        var target = ItemFolderService.FindFolderByPath(folderPath, Folders);
        if (target is null) return;
        ItemFolderService.MoveItem(itemName, target, Folders);
    }

    public void RemoveItemFromFolders(string itemName)
        => ItemFolderService.RemoveItemFromAllFolders(itemName, Folders);

    public bool CommitCreate(string newName, ItemExplorerNode? parentNode)
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
            targetList = Folders;
        }

        if (targetList.Any(f => string.Equals(f.Name, newName, StringComparison.OrdinalIgnoreCase)))
            return false;

        targetList.Add(new ItemFolderDefinition { Name = newName });
        return true;
    }

    public bool CommitRenameFolder(ItemExplorerNode node, string newName)
    {
        var (folder, _) = FindFolderDefinitionByNode(node);
        if (folder is null) return false;
        folder.Name = newName;
        return true;
    }

    #endregion

    #region Folder lookup

    public (ItemFolderDefinition? folder, List<ItemFolderDefinition> parentList) FindFolderDefinitionByNode(
        ItemExplorerNode node)
    {
        var path = GetNodePath(node);
        return path is not null
            ? ItemFolderService.FindByNodePath(path, Folders)
            : ItemFolderService.FindByName(node.Name, Folders);
    }

    private List<string>? GetNodePath(ItemExplorerNode target)
    {
        var path = new List<string>();
        return FindNodePath(target, RootNodes, path) ? path : null;
    }

    private static bool FindNodePath(ItemExplorerNode target, IEnumerable<ItemExplorerNode> nodes, List<string> path)
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

    public void SaveFolders()
    {
        SyncExpansionState();
        ItemFolderSettings.Save(Folders);
    }

    private void SyncExpansionState() => SyncExpansionState(RootNodes, Folders);

    private static void SyncExpansionState(IEnumerable<ItemExplorerNode> nodes, List<ItemFolderDefinition> folders)
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
}
