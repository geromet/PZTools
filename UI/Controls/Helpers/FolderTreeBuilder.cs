using System.Collections.ObjectModel;
using Core.Folders;
using Data.Data;

namespace UI.Controls;

public static class FolderTreeBuilder
{
    public static void Build(
        ObservableCollection<ExplorerNode> rootNodes,
        List<FolderDefinition> folders,
        List<Distribution> filtered,
        bool hideEmptyFolders)
    {
        var filteredSet = new HashSet<string>(filtered.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        rootNodes.Clear();

        // Build folder nodes recursively
        foreach (var folder in folders)
        {
            var folderNode = BuildFolderNode(folder, filtered, filteredSet, assigned, hideEmptyFolders);
            if (folderNode is not null)
                rootNodes.Add(folderNode);
        }

        // Add unfoldered distributions at root level
        foreach (var dist in filtered)
        {
            if (assigned.Contains(dist.Name)) continue;
            rootNodes.Add(ExplorerNode.CreateDistribution(dist));
        }
    }

    private static ExplorerNode? BuildFolderNode(
        FolderDefinition folder,
        List<Distribution> filtered,
        HashSet<string> filteredSet,
        HashSet<string> assigned,
        bool hideEmpty)
    {
        var folderNode = ExplorerNode.CreateFolder(folder.Name);
        folderNode.IsExpanded = folder.IsExpanded;

        // Add child subfolders recursively
        if (folder.Children is not null)
        {
            foreach (var child in folder.Children)
            {
                var childNode = BuildFolderNode(child, filtered, filteredSet, assigned, hideEmpty);
                if (childNode is not null)
                    folderNode.Children.Add(childNode);
            }
        }

        // Add distributions belonging to this folder
        foreach (var distName in folder.DistributionNames)
        {
            if (!filteredSet.Contains(distName)) continue;

            var dist = filtered.FirstOrDefault(d =>
                string.Equals(d.Name, distName, StringComparison.OrdinalIgnoreCase));
            if (dist is null) continue;

            folderNode.Children.Add(ExplorerNode.CreateDistribution(dist));
            assigned.Add(distName);
        }

        // Hide empty folders when filters are active
        if (hideEmpty && folderNode.Children.Count == 0)
            return null;

        return folderNode;
    }
}
