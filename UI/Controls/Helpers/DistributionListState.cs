using System.Collections.ObjectModel;
using System.Linq;
using Core.Filtering;
using Core.Folders;
using Data.Data;

namespace UI.Controls;

public class DistributionListState
{
    private List<Distribution> _all = [];

    public List<Distribution> LastFiltered { get; private set; } = [];
    public List<FolderDefinition> Folders { get; set; } = [];
    public ObservableCollection<ExplorerNode> RootNodes { get; } = [];
    public FilterState Filter { get; } = new();

    public int TotalCount => _all.Count;

    #region Load / filter / tree

    public void LoadDistributions(IReadOnlyList<Distribution> distributions)
    {
        if (RootNodes.Count > 0)
            SyncExpansionState();

        _all = [.. distributions];
        Filter.Reset();
    }

    public void LoadFolders() =>
        Folders = FolderService.DeepCopy(FolderSettings.Load());

    public void ApplyFilter(string searchQuery)
    {
        var criteria = Filter.BuildCriteria(searchQuery);
        LastFiltered = DistributionFilter.Apply(_all, criteria);
        FolderTreeBuilder.Build(RootNodes, Folders, LastFiltered, DistributionFilter.HasAnyActiveFilter(criteria));
    }

    public void RebuildTree(string searchQuery, bool syncExpansion = false)
    {
        if (syncExpansion)
            SyncExpansionState();
        var criteria = Filter.BuildCriteria(searchQuery);
        FolderTreeBuilder.Build(RootNodes, Folders, LastFiltered, DistributionFilter.HasAnyActiveFilter(criteria));
    }

    #endregion

    #region Expand / collapse

    public static void SetExpandedRecursive(IEnumerable<ExplorerNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder) continue;
            node.IsExpanded = expanded;
            if (node.Children.Count > 0)
                SetExpandedRecursive(node.Children, expanded);
        }
    }

    #endregion

    #region Folder lookup (ExplorerNode → FolderDefinition bridge)

    public (FolderDefinition? folder, List<FolderDefinition> parentList) FindFolderDefinition(
        ExplorerNode node)
    {
        var path = GetNodePath(node);
        return path is not null
            ? FolderService.FindByNodePath(path, Folders)
            : FolderService.FindByName(node.Name, Folders);
    }

    private List<string>? GetNodePath(ExplorerNode target)
    {
        var path = new List<string>();
        return FindNodePath(target, RootNodes, path) ? path : null;
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

    #endregion

    #region Folder mutations

    public void DeleteFolder(ExplorerNode node)
    {
        var (folder, parentList) = FindFolderDefinition(node);
        if (folder is null) return;
        parentList.Remove(folder);
    }

    public void MoveDistributionsToFolder(List<string> distNames, string folderPath)
    {
        var folder = FolderService.FindFolderByPath(folderPath, Folders);
        if (folder is null) return;
        foreach (var name in distNames)
        {
            FolderService.RemoveDistFromAllFolders(name, Folders);
            folder.DistributionNames.Add(name);
        }
    }

    public void RemoveDistributionsFromFolders(List<string> distNames)
    {
        foreach (var name in distNames)
            FolderService.RemoveDistFromAllFolders(name, Folders);
    }

    public bool CommitCreate(string newName, ExplorerNode? parentNode)
    {
        List<FolderDefinition> targetList;
        if (parentNode is not null)
        {
            var (parentDef, _) = FindFolderDefinition(parentNode);
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

        targetList.Add(new FolderDefinition { Name = newName });
        return true;
    }

    public bool CommitRenameFolder(ExplorerNode node, string newName)
    {
        var (folder, _) = FindFolderDefinition(node);
        if (folder is null) return false;
        folder.Name = newName;
        return true;
    }

    #endregion

    #region Persistence

    public void SaveFolders()
    {
        SyncExpansionState();
        FolderSettings.Save(FolderService.DeepCopy(Folders));
    }

    public void SaveExpansionState()
    {
        SyncExpansionState();
        FolderSettings.Save(Folders);
    }

    private void SyncExpansionState() =>
        SyncExpansionState(RootNodes, Folders);

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
}
