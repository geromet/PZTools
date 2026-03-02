#pragma warning disable CS0618 // DragDrop old API
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Core.Folders;

namespace UI.Controls;

public interface ITreeDragDropHost
{
    ObservableCollection<ExplorerNode> RootNodes { get; }
    List<FolderDefinition> Folders { get; }
    void SaveFolders();
    void RefreshTree();
    bool ShowMoveFolderConfirmation(string folderName);
    (FolderDefinition? folder, List<FolderDefinition> parentList) FindFolderDefinition(ExplorerNode node);
}

public class TreeDragDropHandler
{
    private readonly TreeView _tree;
    private readonly ITreeDragDropHost _host;

    private Point _dragStartPoint;
    private bool _dragStartPending;
    private List<ExplorerNode> _draggedNodesSnapshot = [];
    private TreeViewItem? _currentDropTarget;
    private const double DragThreshold = 6;

    public TreeDragDropHandler(TreeView tree, ITreeDragDropHost host)
    {
        _tree = tree;
        _host = host;
    }

    public void Attach()
    {
        _tree.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        _tree.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        _tree.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        _tree.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        _tree.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        _tree.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<ScrollBar>() is not null) return;
        if (!e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed) return;
        _dragStartPoint = e.GetPosition(_tree);

        // Snapshot selection now, before the TreeView processes the click and potentially resets it
        _draggedNodesSnapshot = [];
        if (_tree.SelectedItems is not null)
        {
            foreach (var item in _tree.SelectedItems)
            {
                if (item is ExplorerNode node)
                    _draggedNodesSnapshot.Add(node);
            }
        }

        _dragStartPending = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStartPending = false;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<ScrollBar>() is not null) return;
        if (!_dragStartPending) return;

        var pos = e.GetPosition(_tree);
        var delta = pos - _dragStartPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        _dragStartPending = false;

        // Use the snapshot taken at PointerPressed (before TreeView changed the selection)
        // Also include the currently clicked node if it wasn't in the original selection
        var clickedNode = FindNodeAtPosition(_dragStartPoint);
        var draggedNodes = new List<ExplorerNode>(_draggedNodesSnapshot);
        if (clickedNode is not null && !draggedNodes.Contains(clickedNode))
            draggedNodes.Add(clickedNode);
        if (draggedNodes.Count == 0) return;

        var data = new DataObject();
        data.Set("ExplorerNodes", draggedNodes);
        try
        {
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        ClearDropTarget();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("ExplorerNodes"))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var targetNode = FindNodeAtPosition(e.GetPosition(_tree));
        var draggedNodes = e.Data.Get("ExplorerNodes") as List<ExplorerNode>;

        var dropFolder = ResolveDropFolder(targetNode);

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
            if (tvi is not null)
                SetDropTarget(tvi);
            else
                ClearDropTarget();
        }
        else
        {
            ClearDropTarget();
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        ClearDropTarget();
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        ClearDropTarget();

        if (e.Data.Get("ExplorerNodes") is not List<ExplorerNode> draggedNodes)
            return;

        var targetNode = FindNodeAtPosition(e.GetPosition(_tree));
        var dropFolder = ResolveDropFolder(targetNode);

        if (!IsValidDrop(draggedNodes, dropFolder, targetNode))
            return;

        foreach (var node in draggedNodes)
        {
            if (node.IsFolder)
            {
                if (!MoveFolderTo(node, dropFolder))
                    return; // user cancelled
            }
            else if (node.Distribution is not null)
                MoveDistributionTo(node.Distribution.Name, dropFolder);
        }

        _host.SaveFolders();
        _host.RefreshTree();
    }

    // ── Drop resolution ──

    private ExplorerNode? ResolveDropFolder(ExplorerNode? targetNode)
    {
        if (targetNode is null) return null;
        if (targetNode.IsFolder) return targetNode;
        return FindParentFolderNode(targetNode, _host.RootNodes);
    }

    private static ExplorerNode? FindParentFolderNode(
        ExplorerNode target, ObservableCollection<ExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder) continue;
            if (node.Children.Contains(target)) return node;
            var found = FindParentFolderNode(target, node.Children);
            if (found is not null) return found;
        }
        return null;
    }

    private static bool IsValidDrop(List<ExplorerNode> dragged, ExplorerNode? dropFolder, ExplorerNode? targetNode)
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

    private static bool IsDescendantOf(ExplorerNode candidate, ExplorerNode ancestor)
    {
        foreach (var child in ancestor.Children)
        {
            if (child == candidate) return true;
            if (child.IsFolder && IsDescendantOf(candidate, child))
                return true;
        }
        return false;
    }

    // ── Move operations ──

    private void MoveDistributionTo(string distName, ExplorerNode? targetFolderNode)
    {
        var targetDef = targetFolderNode is not null ? _host.FindFolderDefinition(targetFolderNode).folder : null;
        FolderService.MoveDistribution(distName, targetDef, _host.Folders);
    }

    private bool MoveFolderTo(ExplorerNode folderNode, ExplorerNode? targetFolderNode)
    {
        if (!_host.ShowMoveFolderConfirmation(folderNode.Name))
            return false;

        var (folderDef, oldParentList) = _host.FindFolderDefinition(folderNode);
        if (folderDef is null) return true;

        var targetDef = targetFolderNode is not null ? _host.FindFolderDefinition(targetFolderNode).folder : null;
        FolderService.MoveFolder(folderDef, oldParentList, targetDef, _host.Folders);
        return true;
    }

    // ── Visual helpers ──

    private ExplorerNode? FindNodeAtPosition(Point pos)
    {
        var hit = _tree.InputHitTest(pos);
        if (hit is not Visual visual) return null;
        var tvi = visual.FindAncestorOfType<TreeViewItem>();
        return tvi?.DataContext as ExplorerNode;
    }

    private TreeViewItem? FindTreeViewItemForNode(ExplorerNode node)
    {
        return FindTreeViewItemRecursive(_tree, node);
    }

    private static TreeViewItem? FindTreeViewItemRecursive(ItemsControl parent, ExplorerNode node)
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
