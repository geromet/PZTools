using System.Collections.ObjectModel;
using Core.Items;

namespace UI.Controls;

public static class ItemFolderTreeBuilder
{
    public static void Build(
        ObservableCollection<ItemExplorerNode> rootNodes,
        List<ItemFolderDefinition> folders,
        IReadOnlyList<string> filteredItems,
        bool hideEmptyFolders)
    {
        var filteredSet = new HashSet<string>(filteredItems, StringComparer.OrdinalIgnoreCase);
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        rootNodes.Clear();

        foreach (var folder in folders)
        {
            var folderNode = BuildFolderNode(folder, filteredSet, assigned, hideEmptyFolders);
            if (folderNode is not null)
                rootNodes.Add(folderNode);
        }

        foreach (var name in filteredItems)
        {
            if (assigned.Contains(name)) continue;
            rootNodes.Add(ItemExplorerNode.CreateItem(name));
        }
    }

    private static ItemExplorerNode? BuildFolderNode(
        ItemFolderDefinition folder,
        HashSet<string> filteredSet,
        HashSet<string> assigned,
        bool hideEmpty)
    {
        var folderNode = ItemExplorerNode.CreateFolder(folder.Name);
        folderNode.IsExpanded = folder.IsExpanded;

        if (folder.Children is not null)
        {
            foreach (var child in folder.Children)
            {
                var childNode = BuildFolderNode(child, filteredSet, assigned, hideEmpty);
                if (childNode is not null)
                    folderNode.Children.Add(childNode);
            }
        }

        foreach (var itemName in folder.ItemNames)
        {
            if (!filteredSet.Contains(itemName)) continue;
            folderNode.Children.Add(ItemExplorerNode.CreateItem(itemName));
            assigned.Add(itemName);
        }

        if (hideEmpty && folderNode.Children.Count == 0)
            return null;

        return folderNode;
    }
}
