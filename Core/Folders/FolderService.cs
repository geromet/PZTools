namespace Core.Folders;

public static class FolderService
{
    public static FolderDefinition? FindFolderContaining(string distName, List<FolderDefinition> folders)
    {
        foreach (var f in folders)
        {
            if (f.DistributionNames.Any(n =>
                string.Equals(n, distName, StringComparison.OrdinalIgnoreCase)))
                return f;

            if (f.Children is not null)
            {
                var found = FindFolderContaining(distName, f.Children);
                if (found is not null) return found;
            }
        }
        return null;
    }

    public static FolderDefinition? FindFolderByPath(string path, List<FolderDefinition> folders)
    {
        var parts = path.Split('/');
        var current = folders;
        FolderDefinition? result = null;
        foreach (var part in parts)
        {
            result = current.FirstOrDefault(f =>
                string.Equals(f.Name, part, StringComparison.OrdinalIgnoreCase));
            if (result is null) return null;
            current = result.Children ?? [];
        }
        return result;
    }

    public static (FolderDefinition? folder, List<FolderDefinition> parentList) FindByNodePath(
        List<string> path, List<FolderDefinition> folders)
    {
        var currentList = folders;
        for (var i = 0; i < path.Count; i++)
        {
            var name = path[i];
            var match = currentList.FirstOrDefault(f =>
                string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return (null, currentList);
            if (i == path.Count - 1)
                return (match, currentList);
            currentList = match.Children ?? [];
        }
        return (null, currentList);
    }

    public static (FolderDefinition? folder, List<FolderDefinition> parentList) FindByName(
        string folderName, List<FolderDefinition> folders)
    {
        foreach (var f in folders)
        {
            if (string.Equals(f.Name, folderName, StringComparison.OrdinalIgnoreCase))
                return (f, folders);

            if (f.Children is not null)
            {
                var found = FindByName(folderName, f.Children);
                if (found.folder is not null) return found;
            }
        }
        return (null, folders);
    }

    public static void RemoveDistFromAllFolders(string distName, List<FolderDefinition> folders)
    {
        foreach (var f in folders)
        {
            f.DistributionNames.RemoveAll(n =>
                string.Equals(n, distName, StringComparison.OrdinalIgnoreCase));
            if (f.Children is not null)
                RemoveDistFromAllFolders(distName, f.Children);
        }
    }

    public static void MoveDistribution(string distName, FolderDefinition? target, List<FolderDefinition> folders)
    {
        RemoveDistFromAllFolders(distName, folders);
        target?.DistributionNames.Add(distName);
    }

    /// <summary>
    /// Moves a folder from its current parent list to a new target.
    /// Pass targetDef = null to move to root.
    /// </summary>
    public static void MoveFolder(
        FolderDefinition folderDef,
        List<FolderDefinition> oldParentList,
        FolderDefinition? targetDef,
        List<FolderDefinition> rootFolders)
    {
        oldParentList.Remove(folderDef);

        if (targetDef is null)
        {
            rootFolders.Add(folderDef);
        }
        else
        {
            targetDef.Children ??= [];
            targetDef.Children.Add(folderDef);
        }
    }

    public static List<FolderDefinition> DeepCopy(List<FolderDefinition> source)
    {
        return source.Select(f => new FolderDefinition
        {
            Name = f.Name,
            DistributionNames = [.. f.DistributionNames],
            Children = f.Children is not null ? DeepCopy(f.Children) : null,
            IsExpanded = f.IsExpanded,
        }).ToList();
    }
}
