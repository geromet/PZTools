#pragma warning disable CS0618 // DragDrop old API
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace UI.Controls;

public interface ITreeDragDropHost<TNode> where TNode : class, IExplorerNode
{
    ObservableCollection<TNode> RootNodes { get; }
    void SaveFolders();
    void RefreshTree();

    /// <summary>
    /// Executes a drop of a single node onto the given target folder (null = root).
    /// Return false to cancel any remaining drops in the same operation.
    /// </summary>
    bool ExecuteNodeDrop(TNode droppedNode, TNode? targetFolder);
}

public class TreeDragDropHandler<TNode> where TNode : class, IExplorerNode
{
    private readonly TreeView _tree;
    private readonly ITreeDragDropHost<TNode> _host;
    private readonly string _dragDataKey;

    private Point _dragStartPoint;
    private bool _dragStartPending;
    private List<TNode> _draggedNodesSnapshot = [];
    private TreeViewItem? _currentDropTarget;
    private const double DragThreshold = 6;

    public TreeDragDropHandler(TreeView tree, ITreeDragDropHost<TNode> host)
    {
        _tree = tree;
        _host = host;
        _dragDataKey = typeof(TNode).Name + "Nodes";
    }

    public void Attach()
    {
        _tree.AddHandler(InputElement.PointerPressedEvent,  OnPointerPressed,  RoutingStrategies.Tunnel);
        _tree.AddHandler(InputElement.PointerMovedEvent,    OnPointerMoved,    RoutingStrategies.Tunnel);
        _tree.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        _tree.AddHandler(DragDrop.DragOverEvent,  OnDragOver);
        _tree.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        _tree.AddHandler(DragDrop.DropEvent,      OnDrop);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<ScrollBar>() is not null) return;
        if (!e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed) return;
        _dragStartPoint = e.GetPosition(_tree);

        _draggedNodesSnapshot = [];
        if (_tree.SelectedItems is not null)
            foreach (var item in _tree.SelectedItems)
                if (item is TNode node)
                    _draggedNodesSnapshot.Add(node);

        _dragStartPending = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => _dragStartPending = false;

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<ScrollBar>() is not null) return;
        if (!_dragStartPending) return;

        var pos   = e.GetPosition(_tree);
        var delta = pos - _dragStartPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

        _dragStartPending = false;

        var clickedNode  = FindNodeAtPosition(_dragStartPoint);
        var draggedNodes = new List<TNode>(_draggedNodesSnapshot);
        if (clickedNode is not null && !draggedNodes.Contains(clickedNode))
            draggedNodes.Add(clickedNode);
        if (draggedNodes.Count == 0) return;

        var data = new DataObject();
        data.Set(_dragDataKey, draggedNodes);
        try { await DragDrop.DoDragDrop(e, data, DragDropEffects.Move); }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }

        ClearDropTarget();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(_dragDataKey)) { e.DragEffects = DragDropEffects.None; return; }

        var targetNode   = FindNodeAtPosition(e.GetPosition(_tree));
        var draggedNodes = e.Data.Get(_dragDataKey) as List<TNode>;
        var dropFolder   = ResolveDropFolder(targetNode);

        if (draggedNodes is not null && !IsValidDrop(draggedNodes, dropFolder, targetNode))
        {
            e.DragEffects = DragDropEffects.None;
            ClearDropTarget();
            return;
        }

        e.DragEffects = DragDropEffects.Move;

        if (dropFolder is not null)
        {
            var tvi = FindTreeViewItemForNode(dropFolder);
            if (tvi is not null) SetDropTarget(tvi);
            else ClearDropTarget();
        }
        else ClearDropTarget();
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => ClearDropTarget();

    private void OnDrop(object? sender, DragEventArgs e)
    {
        ClearDropTarget();
        if (e.Data.Get(_dragDataKey) is not List<TNode> draggedNodes) return;

        var targetNode = FindNodeAtPosition(e.GetPosition(_tree));
        var dropFolder = ResolveDropFolder(targetNode);
        if (!IsValidDrop(draggedNodes, dropFolder, targetNode)) return;

        foreach (var node in draggedNodes)
            if (!_host.ExecuteNodeDrop(node, dropFolder))
                return;

        _host.SaveFolders();
        _host.RefreshTree();
    }

    // ── Drop resolution ──

    private TNode? ResolveDropFolder(TNode? targetNode)
    {
        if (targetNode is null) return null;
        if (targetNode.IsFolder) return targetNode;
        return FindParentFolderNode(targetNode, _host.RootNodes);
    }

    private static TNode? FindParentFolderNode(TNode target, IEnumerable<IExplorerNode> nodes)
    {
        foreach (var rawNode in nodes)
        {
            if (rawNode is not TNode node || !node.IsFolder) continue;
            if (node.ChildrenBase.Contains(target)) return node;
            var found = FindParentFolderNode(target, node.ChildrenBase);
            if (found is not null) return found;
        }
        return null;
    }

    private static bool IsValidDrop(List<TNode> dragged, TNode? dropFolder, TNode? targetNode)
    {
        foreach (var node in dragged)
        {
            if (node == targetNode) return false;
            if (node == dropFolder) return false;
            if (node.IsFolder && dropFolder is not null && IsDescendantOf(dropFolder, node))
                return false;
        }
        return true;
    }

    private static bool IsDescendantOf(TNode candidate, TNode ancestor)
    {
        foreach (var child in ancestor.ChildrenBase)
        {
            if (child == candidate) return true;
            if (child.IsFolder && child is TNode typedChild && IsDescendantOf(candidate, typedChild))
                return true;
        }
        return false;
    }

    // ── Visual helpers ──

    private TNode? FindNodeAtPosition(Point pos)
    {
        var hit = _tree.InputHitTest(pos);
        if (hit is not Visual visual) return null;
        var tvi = visual.FindAncestorOfType<TreeViewItem>();
        return tvi?.DataContext as TNode;
    }

    private TreeViewItem? FindTreeViewItemForNode(TNode node) =>
        FindTreeViewItemRecursive(_tree, node);

    private static TreeViewItem? FindTreeViewItemRecursive(ItemsControl parent, TNode node)
    {
        foreach (var item in parent.GetRealizedContainers())
        {
            if (item is TreeViewItem tvi)
            {
                if (tvi.DataContext == node) return tvi;
                var found = FindTreeViewItemRecursive(tvi, node);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private void SetDropTarget(TreeViewItem tvi)
    {
        if (_currentDropTarget == tvi) return;
        ClearDropTarget();
        _currentDropTarget = tvi;
        tvi.Classes.Add("droptarget");
    }

    private void ClearDropTarget()
    {
        if (_currentDropTarget is null) return;
        _currentDropTarget.Classes.Remove("droptarget");
        _currentDropTarget = null;
    }
}
