namespace UI.Controls;

/// <summary>
/// Common contract for tree nodes used in both the Distribution Explorer and the Items Explorer.
/// Allows shared helpers (drag-drop, keyboard, expand) to work generically over both node types.
/// </summary>
public interface IExplorerNode
{
    string Name { get; set; }
    bool IsFolder { get; }
    bool IsNotFolder { get; }
    bool IsExpanded { get; set; }
    string ChildCountText { get; }

    /// <summary>Children as the base interface type — covariant, safe for read-only traversal.</summary>
    IEnumerable<IExplorerNode> ChildrenBase { get; }
}
