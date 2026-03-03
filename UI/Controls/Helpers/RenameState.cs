namespace UI.Controls;

public class RenameState<T> where T : class
{
    public T? RenamingNode { get; private set; }
    public bool IsCreating { get; private set; }
    public T? NewFolderParent { get; private set; }

    public void BeginCreate(T? parent)
    {
        IsCreating = true;
        RenamingNode = null;
        NewFolderParent = parent;
    }

    public void BeginRename(T node)
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
