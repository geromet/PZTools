using Avalonia.Controls;
using UI.UndoRedo;

namespace UI.Controls.Helpers;

public class ItemTabState
{
    public string ItemName { get; }
    public UndoRedoStack UndoRedo { get; } = new();
    public ItemsDetailControl? DetailControl { get; set; }
    public TabItem TabItem { get; set; } = null!;
    public long LastAccessTick { get; set; }
    public bool IsPinned { get; set; }

    public ItemTabState(string itemName)
    {
        ItemName = itemName;
    }
}
