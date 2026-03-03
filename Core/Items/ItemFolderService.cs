namespace Core.Items;

public static class ItemFolderService
{
    public static ItemFolderDefinition? FindFolderContaining(string itemName, List<ItemFolderDefinition> folders)
    {
        foreach (var f in folders)
        {
            if (f.ItemNames.Any(n => string.Equals(n, itemName, StringComparison.OrdinalIgnoreCase)))
                return f;

            if (f.Children is not null)
            {
                var found = FindFolderContaining(itemName, f.Children);
                if (found is not null) return found;
            }
        }
        return null;
    }

    public static (ItemFolderDefinition? folder, List<ItemFolderDefinition> parentList) FindByNodePath(
        List<string> path, List<ItemFolderDefinition> folders)
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

    public static (ItemFolderDefinition? folder, List<ItemFolderDefinition> parentList) FindByName(
        string folderName, List<ItemFolderDefinition> folders)
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

    public static void RemoveItemFromAllFolders(string itemName, List<ItemFolderDefinition> folders)
    {
        foreach (var f in folders)
        {
            f.ItemNames.RemoveAll(n => string.Equals(n, itemName, StringComparison.OrdinalIgnoreCase));
            if (f.Children is not null)
                RemoveItemFromAllFolders(itemName, f.Children);
        }
    }

    public static void MoveItem(string itemName, ItemFolderDefinition? target, List<ItemFolderDefinition> folders)
    {
        RemoveItemFromAllFolders(itemName, folders);
        target?.ItemNames.Add(itemName);
    }

    public static void MoveFolder(
        ItemFolderDefinition folderDef,
        List<ItemFolderDefinition> oldParentList,
        ItemFolderDefinition? targetDef,
        List<ItemFolderDefinition> rootFolders)
    {
        oldParentList.Remove(folderDef);

        if (targetDef is null)
            rootFolders.Add(folderDef);
        else
        {
            targetDef.Children ??= [];
            targetDef.Children.Add(folderDef);
        }
    }

    public static ItemFolderDefinition? FindFolderByPath(string path, List<ItemFolderDefinition> folders)
    {
        var current = folders;
        ItemFolderDefinition? result = null;
        foreach (var part in path.Split('/'))
        {
            result = current.FirstOrDefault(f => string.Equals(f.Name, part, StringComparison.OrdinalIgnoreCase));
            if (result is null) return null;
            current = result.Children ?? [];
        }
        return result;
    }

    public static List<ItemFolderDefinition> DeepCopy(List<ItemFolderDefinition> source)
    {
        return source.Select(f => new ItemFolderDefinition
        {
            Name = f.Name,
            ItemNames = [.. f.ItemNames],
            Children = f.Children is not null ? DeepCopy(f.Children) : null,
            IsExpanded = f.IsExpanded,
        }).ToList();
    }
}
