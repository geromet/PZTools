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
        var filteredDict = new Dictionary<string, Distribution>(filtered.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var d in filtered) filteredDict[d.Name] = d;

        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        rootNodes.Clear();

        foreach (var folder in folders)
        {
            var folderNode = BuildFolderNode(folder, filteredDict, assigned, hideEmptyFolders);
            if (folderNode is not null)
                rootNodes.Add(folderNode);
        }

        foreach (var dist in filtered)
        {
            if (assigned.Contains(dist.Name)) continue;
            rootNodes.Add(ExplorerNode.CreateDistribution(dist));
        }
    }

    private static ExplorerNode? BuildFolderNode(
        FolderDefinition folder,
        Dictionary<string, Distribution> filteredDict,
        HashSet<string> assigned,
        bool hideEmpty)
    {
        var folderNode = ExplorerNode.CreateFolder(folder.Name);
        folderNode.IsExpanded = folder.IsExpanded;

        if (folder.Children is not null)
        {
            foreach (var child in folder.Children)
            {
                var childNode = BuildFolderNode(child, filteredDict, assigned, hideEmpty);
                if (childNode is not null)
                    folderNode.Children.Add(childNode);
            }
        }

        foreach (var distName in folder.DistributionNames)
        {
            if (!filteredDict.TryGetValue(distName, out var dist)) continue;
            folderNode.Children.Add(ExplorerNode.CreateDistribution(dist));
            assigned.Add(distName);
        }

        if (hideEmpty && folderNode.Children.Count == 0)
            return null;

        return folderNode;
    }
}
