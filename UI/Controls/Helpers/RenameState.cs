namespace UI.Controls;

public class RenameState
{
    public ExplorerNode? RenamingNode { get; private set; }
    public bool IsCreating { get; private set; }
    public ExplorerNode? NewFolderParent { get; private set; }

    public void BeginCreate(ExplorerNode? parent)
    {
        IsCreating = true;
        RenamingNode = null;
        NewFolderParent = parent;
    }

    public void BeginRename(ExplorerNode node)
    {
        IsCreating = false;
        RenamingNode = node;
        NewFolderParent = null;
    }

    public void Reset()
    {
        RenamingNode = null;
        IsCreating = false;
        NewFolderParent = null;
    }
}
