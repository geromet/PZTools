#pragma warning disable CS0618 // DragDrop old API (DataObject, DoDragDrop, DragEventArgs.Data) — new DataTransfer API not worth the complexity for internal drag
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Core.Filtering;
using Core.Folders;
using Data.Data;

namespace UI.Controls;

public partial class DistributionListControl : UserControl
{
    private List<Distribution> _all = [];
    private string? _activeTypeFilter; // null = "All"

    // Excluded Tri-State to handle funny stuff
    private TriState _defaultFilter;
    // Tri-state content filters (per-container, conjunctive)
    private TriState _procListFilter;
    private TriState _rollsFilter;
    private TriState _itemsFilter;
    private TriState _junkFilter;
    private TriState _proceduralFilter;

    // Tri-state structural filters (distribution-level)
    private TriState _noContentFilter;
    private TriState _invalidFilter;
    private TriState _distributionItemsFilter;

    // Folder state
    private List<FolderDefinition> _folders = [];
    private readonly ObservableCollection<ExplorerNode> _rootNodes = [];
    private UserSettings? _settings;

    // Inline rename state
    private ExplorerNode? _renamingNode;
    private bool _isCreatingNewFolder;
    private ExplorerNode? _newFolderParent; // null = root, set for subfolders

    // Drag-drop state
    private Point _dragStartPoint;
    private bool _dragStartPending;
    private List<ExplorerNode> _draggedNodesSnapshot = [];
    private TreeViewItem? _currentDropTarget;
    private const double DragThreshold = 6;

    // Cached filter result for tree-only rebuilds
    private List<Distribution> _lastFiltered = [];

    public event Action<Distribution?>? SelectionChanged;
    public event Action<Distribution>? OpenRequested;
    public event Action<List<Distribution>>? OpenMultipleRequested;

    /// <summary>Exposes current content filter state so the detail panel can mirror it.</summary>
    public (TriState ProcList, TriState Rolls, TriState Items, TriState Junk, TriState Procedural, TriState Invalid, TriState DistributionItemsFilter) ContentFilters
        => (_procListFilter, _rollsFilter, _itemsFilter, _junkFilter, _proceduralFilter, _invalidFilter, _distributionItemsFilter);

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SaveExpansionState();
    }

    public DistributionListControl()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => ApplyFilter();

        // Wire right-click on content filter pills
        foreach (var child in ContentFilterPills.Children)
        {
            if (child is Button btn)
                btn.PointerPressed += ContentFilterPill_PointerPressed;
        }
        foreach (var child in StructureFilterPills.Children)
        {
            if (child is Button btn)
                btn.PointerPressed += StructureFilterPill_PointerPressed;
        }

        // Wire drag-drop
        DistTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
        DistTree.AddHandler(PointerMovedEvent, OnTreePointerMoved, RoutingStrategies.Tunnel);
        DistTree.AddHandler(PointerReleasedEvent, OnTreePointerReleased, RoutingStrategies.Tunnel);
        DistTree.AddHandler(DragDrop.DragOverEvent, OnTreeDragOver);
        DistTree.AddHandler(DragDrop.DragLeaveEvent, OnTreeDragLeave);
        DistTree.AddHandler(DragDrop.DropEvent, OnTreeDrop);

        // Double-click opens a tab
        DistTree.DoubleTapped += OnTreeDoubleTapped;
    }

    public void SetSettings(UserSettings settings)
    {
        _settings = settings;
        _folders = DeepCopyFolders(FolderSettings.Load());
    }

    public void Load(IReadOnlyList<Distribution> distributions)
    {
        // Sync expansion state from current tree before rebuilding
        if (_rootNodes.Count > 0)
            SyncExpansionState(_rootNodes, _folders);

        _all = [.. distributions];
        _activeTypeFilter = null;
        _procListFilter = TriState.Ignored;
        _rollsFilter = TriState.Ignored;
        _itemsFilter = TriState.Ignored;
        _junkFilter = TriState.Ignored;
        _proceduralFilter = TriState.Ignored;
        _noContentFilter = TriState.Ignored;
        _invalidFilter = TriState.Ignored;
        _distributionItemsFilter = TriState.Ignored;
        _defaultFilter = TriState.Ignored;
        SearchBox.Text = string.Empty;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private FilterCriteria BuildFilterCriteria()
    {
        return new FilterCriteria(
            _activeTypeFilter, _procListFilter, _rollsFilter,
            _itemsFilter, _junkFilter, _proceduralFilter,
            _noContentFilter, _invalidFilter, _distributionItemsFilter,
            SearchBox.Text?.Trim() ?? string.Empty);
    }

    private void ApplyFilter()
    {
        var criteria = BuildFilterCriteria();
        _lastFiltered = DistributionFilter.Apply(_all, criteria);
        BuildTree(_lastFiltered);
        CountText.Text = $"{_lastFiltered.Count} / {_all.Count}";
    }

    // ── Tree building (recursive for nested folders) ──

    private void BuildTree(List<Distribution> filtered)
    {
        var filteredSet = new HashSet<string>(filtered.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hideEmpty = HasAnyActiveFilter();

        _rootNodes.Clear();

        // Build folder nodes recursively
        foreach (var folder in _folders)
        {
            var folderNode = BuildFolderNode(folder, filtered, filteredSet, assigned, hideEmpty);
            if (folderNode is not null)
                _rootNodes.Add(folderNode);
        }

        // Add unfoldered distributions at root level
        foreach (var dist in filtered)
        {
            if (assigned.Contains(dist.Name)) continue;
            _rootNodes.Add(ExplorerNode.CreateDistribution(dist));
        }

        DistTree.ItemsSource = _rootNodes;
    }

    private ExplorerNode? BuildFolderNode(
        FolderDefinition folder,
        List<Distribution> filtered,
        HashSet<string> filteredSet,
        HashSet<string> assigned,
        bool hideEmpty)
    {
        var folderNode = ExplorerNode.CreateFolder(folder.Name);
        folderNode.IsExpanded = folder.IsExpanded;

        // Add child subfolders recursively
        if (folder.Children is not null)
        {
            foreach (var child in folder.Children)
            {
                var childNode = BuildFolderNode(child, filtered, filteredSet, assigned, hideEmpty);
                if (childNode is not null)
                    folderNode.Children.Add(childNode);
            }
        }

        // Add distributions belonging to this folder
        foreach (var distName in folder.DistributionNames)
        {
            if (!filteredSet.Contains(distName)) continue;

            var dist = filtered.FirstOrDefault(d =>
                string.Equals(d.Name, distName, StringComparison.OrdinalIgnoreCase));
            if (dist is null) continue;

            folderNode.Children.Add(ExplorerNode.CreateDistribution(dist));
            assigned.Add(distName);
        }

        // Hide empty folders when filters are active
        if (hideEmpty && folderNode.Children.Count == 0)
            return null;

        return folderNode;
    }

    private bool HasAnyActiveFilter() => DistributionFilter.HasAnyActiveFilter(BuildFilterCriteria());

    /// <summary>
    /// Rebuilds the tree from the cached filter result, preserving selection and scroll position.
    /// Expansion state is preserved via TwoWay binding on ExplorerNode.IsExpanded → FolderDefinition.
    /// Use this for folder-only operations that don't change filter criteria.
    /// </summary>
    private void RefreshTree()
    {
        // Capture selection (distribution names)
        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (DistTree.SelectedItems is not null)
        {
            foreach (var item in DistTree.SelectedItems)
            {
                if (item is ExplorerNode { Distribution: not null } node)
                    selectedNames.Add(node.Distribution.Name);
            }
        }

        // Capture scroll offset
        var scrollViewer = FindScrollViewer(DistTree);
        var scrollOffset = scrollViewer?.Offset ?? default;

        // Sync expansion state from current nodes to FolderDefinitions before rebuilding
        SyncExpansionState(_rootNodes, _folders);

        // Rebuild tree using cached filter result
        BuildTree(_lastFiltered);

        // Restore selection
        if (selectedNames.Count > 0)
            RestoreSelection(selectedNames, _rootNodes);

        // Restore scroll offset
        if (scrollViewer is not null)
            scrollViewer.Offset = scrollOffset;
    }

    private void RestoreSelection(HashSet<string> names, ObservableCollection<ExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder && node.Distribution is not null && names.Contains(node.Distribution.Name))
                DistTree.SelectedItems.Add(node);
            if (node.IsFolder && node.Children.Count > 0)
                RestoreSelection(names, node.Children);
        }
    }

    private static ScrollViewer? FindScrollViewer(Visual visual)
    {
        if (visual is ScrollViewer sv) return sv;
        foreach (var child in visual.GetVisualChildren())
        {
            var found = FindScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }

    // ── Tree selection (multi-select, but detail shows last-clicked distribution) ──

    private void DistTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Find the most recently added distribution node
        foreach (var item in e.AddedItems)
        {
            if (item is ExplorerNode { IsFolder: false } node)
            {
                SelectionChanged?.Invoke(node.Distribution);
                return;
            }
        }

        // If only folders were added/changed, don't update the detail panel
    }

    private void OnTreeDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (e.Source is not Avalonia.Visual visual) return;
        var tvi = visual.FindAncestorOfType<TreeViewItem>();
        if (tvi?.DataContext is ExplorerNode { IsFolder: false, Distribution: not null } node)
        {
            OpenRequested?.Invoke(node.Distribution);
            e.Handled = true;
        }
    }

    private void OpenDistribution_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is ExplorerNode { IsFolder: false, Distribution: not null } node)
            OpenRequested?.Invoke(node.Distribution);
    }

    private void OpenSelectedDistributions_Click(object? sender, RoutedEventArgs e)
    {
        var dists = GetSelectedDistributionNodes()
            .Where(n => n.Distribution is not null)
            .Select(n => n.Distribution!)
            .ToList();
        if (dists.Count > 0)
            OpenMultipleRequested?.Invoke(dists);
    }

    // ── Drag-and-drop ──

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<ScrollBar>() is not null) return;
        if (!e.GetCurrentPoint(DistTree).Properties.IsLeftButtonPressed) return;
        _dragStartPoint = e.GetPosition(DistTree);

        // Snapshot selection now, before the TreeView processes the click and potentially resets it
        _draggedNodesSnapshot = [];
        if (DistTree.SelectedItems is not null)
        {
            foreach (var item in DistTree.SelectedItems)
            {
                if (item is ExplorerNode node)
                    _draggedNodesSnapshot.Add(node);
            }
        }

        _dragStartPending = true;
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStartPending = false;
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<ScrollBar>() is not null) return;
        if (!_dragStartPending) return;

        var pos = e.GetPosition(DistTree);
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

    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("ExplorerNodes"))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var targetNode = FindNodeAtPosition(e.GetPosition(DistTree));
        var draggedNodes = e.Data.Get("ExplorerNodes") as List<ExplorerNode>;

        // Find the effective drop folder (if target is a distribution in a folder, use that folder)
        var dropFolder = ResolveDropFolder(targetNode);

        // Validate drop
        if (draggedNodes is not null && !IsValidDrop(draggedNodes, dropFolder, targetNode))
        {
            e.DragEffects = DragDropEffects.None;
            ClearDropTarget();
            return;
        }

        e.DragEffects = DragDropEffects.Move;

        // Visual feedback on the folder being targeted
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

    private void OnTreeDragLeave(object? sender, DragEventArgs e)
    {
        ClearDropTarget();
    }

    private void OnTreeDrop(object? sender, DragEventArgs e)
    {
        ClearDropTarget();

        if (e.Data.Get("ExplorerNodes") is not List<ExplorerNode> draggedNodes)
            return;

        var targetNode = FindNodeAtPosition(e.GetPosition(DistTree));
        var dropFolder = ResolveDropFolder(targetNode);

        if (!IsValidDrop(draggedNodes, dropFolder, targetNode))
            return;

        // Perform the move (MoveFolderTo shows its own confirmation dialog)
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

        SaveFolders();
        RefreshTree();
    }

    /// <summary>
    /// Resolves the effective drop folder. If the target is itself a folder, return it.
    /// If the target is a distribution inside a folder, return that parent folder.
    /// If the target is a root distribution or null, return null (root level).
    /// </summary>
    private ExplorerNode? ResolveDropFolder(ExplorerNode? targetNode)
    {
        if (targetNode is null) return null;
        if (targetNode.IsFolder) return targetNode;

        // Check if this distribution node lives inside a folder in the current tree
        return FindParentFolderNode(targetNode, _rootNodes);
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

    private bool IsValidDrop(List<ExplorerNode> dragged, ExplorerNode? dropFolder, ExplorerNode? targetNode)
    {
        foreach (var node in dragged)
        {
            // Can't drop on itself
            if (node == targetNode) return false;
            if (node == dropFolder) return false;

            // Can't drop a folder into its own descendant (cycle prevention)
            if (node.IsFolder && dropFolder is not null && IsDescendantOf(dropFolder, node))
                return false;
        }
        return true;
    }

    /// <summary>Returns true if `candidate` is nested inside `ancestor`.</summary>
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

    private void MoveDistributionTo(string distName, ExplorerNode? targetFolderNode)
    {
        // Remove from all folders first
        RemoveDistFromAllFolders(distName, _folders);

        if (targetFolderNode is null) return; // move to root = just remove from folders

        // Find the target FolderDefinition and add
        var (targetDef, _) = FindFolderDefinition(targetFolderNode);
        targetDef?.DistributionNames.Add(distName);
    }

    /// <summary>
    /// Moves a folder to a new parent. Shows a confirmation dialog first.
    /// Returns false if the user cancelled.
    /// </summary>
    private bool MoveFolderTo(ExplorerNode folderNode, ExplorerNode? targetFolderNode)
    {
        if (!ShowMoveFolderConfirmation(folderNode.Name))
            return false;

        // Find and remove the folder from its current location
        var (folderDef, oldParentList) = FindFolderDefinition(folderNode);
        if (folderDef is null) return true; // nothing to move, but not cancelled

        oldParentList.Remove(folderDef);

        if (targetFolderNode is null)
        {
            // Move to root
            _folders.Add(folderDef);
        }
        else
        {
            // Nest inside target folder
            var (targetDef, _) = FindFolderDefinition(targetFolderNode);
            if (targetDef is not null)
            {
                targetDef.Children ??= [];
                targetDef.Children.Add(folderDef);
            }
            else
            {
                // Fallback: back to root
                _folders.Add(folderDef);
            }
        }
        return true;
    }

    // ── Drag visual helpers ──

    private ExplorerNode? FindNodeAtPosition(Point pos)
    {
        var hit = DistTree.InputHitTest(pos);
        if (hit is not Visual visual) return null;

        var tvi = visual.FindAncestorOfType<TreeViewItem>();
        return tvi?.DataContext as ExplorerNode;
    }

    private TreeViewItem? FindTreeViewItemForNode(ExplorerNode node)
    {
        return FindTreeViewItemRecursive(DistTree, node);
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
    /// <summary>
    /// Shows a confirmation dialog before moving a folder. Returns true if the user confirmed.
    /// </summary>
    private bool ShowMoveFolderConfirmation(string folderName)
    {
        var dialog = new Window
        {
            Title = "Move Folder",
            Width = 320,
            Height = 100,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        var result = false;
        var yesBtn = new Button { Content = "Yes", Margin = new Thickness(0, 0, 8, 0) };
        var noBtn = new Button { Content = "No" };
        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click += (_, _) => { dialog.Close(); };
        yesBtn.IsDefault = true;
        noBtn.IsCancel = true;

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(10),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Are you sure you want to move the folder \"{folderName}\"?",
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { yesBtn, noBtn }
                }
            }
        };
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            dialog.ShowDialog(desktop.MainWindow);
            return result;
        }

        return result;
    }
    
    // ── Context menu ──

    private void TreeContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var selected = DistTree.SelectedItem as ExplorerNode;
        var selectedDistNodes = GetSelectedDistributionNodes();
        bool hasDistSelection = selectedDistNodes.Count > 0;
        bool hasSingleDist = selected is { IsFolder: false, Distribution: not null };
        bool anyInFolder = selectedDistNodes.Any(n => FindParentFolder(n) is not null);

        // "Open distribution" / "Open selected distributions"
        OpenDistItem.IsVisible = hasSingleDist;
        OpenSelectedDistsItem.IsVisible = selectedDistNodes.Count > 1;

        // "New Subfolder" — only when a folder is selected
        NewSubfolderItem.IsVisible = selected is { IsFolder: true };

        // "Move to Folder" — build nested submenu of all folders
        MoveToFolderMenu.Items.Clear();
        BuildMoveToFolderMenu(MoveToFolderMenu, _folders, "");
        MoveToFolderMenu.IsVisible = hasDistSelection && _folders.Count > 0;

        // "Remove from Folder" — only if any selected distribution is inside a folder
        RemoveFromFolderItem.IsVisible = hasDistSelection && anyInFolder;

        // Folder-specific items
        bool isFolder = selected is { IsFolder: true };
        RenameFolderItem.IsVisible = isFolder;
        DeleteFolderItem.IsVisible = isFolder;
        FolderExpandSeparator.IsVisible = isFolder;
        ExpandFolderItem.IsVisible = isFolder;
        CollapseFolderItem.IsVisible = isFolder;
    }

    private static void SetExpandedRecursive(IEnumerable<ExplorerNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder) continue;
            node.IsExpanded = expanded;
            if (node.Children.Count > 0)
                SetExpandedRecursive(node.Children, expanded);
        }
    }

    private void ExpandAll_Click(object? sender, RoutedEventArgs e)
    {
        SetExpandedRecursive(_rootNodes, true);
        SaveExpansionState();
    }

    private void CollapseAll_Click(object? sender, RoutedEventArgs e)
    {
        SetExpandedRecursive(_rootNodes, false);
        SaveExpansionState();
    }

    private void ExpandFolderAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is ExplorerNode { IsFolder: true } folder)
        {
            folder.IsExpanded = true;
            SetExpandedRecursive(folder.Children, true);
            SaveExpansionState();
        }
    }

    private void CollapseFolderAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is ExplorerNode { IsFolder: true } folder)
        {
            folder.IsExpanded = false;
            SetExpandedRecursive(folder.Children, false);
            SaveExpansionState();
        }
    }

    private List<ExplorerNode> GetSelectedDistributionNodes()
    {
        var result = new List<ExplorerNode>();
        if (DistTree.SelectedItems is null) return result;
        foreach (var item in DistTree.SelectedItems)
        {
            if (item is ExplorerNode { IsFolder: false, Distribution: not null } node)
                result.Add(node);
        }
        return result;
    }

    private void BuildMoveToFolderMenu(MenuItem parent, List<FolderDefinition> folders, string pathPrefix)
    {
        foreach (var folder in folders)
        {
            var path = string.IsNullOrEmpty(pathPrefix) ? folder.Name : $"{pathPrefix}/{folder.Name}";
            var item = new MenuItem { Header = folder.Name, Tag = path };
            item.Click += MoveToFolder_Click;
            parent.Items.Add(item);

            // Add subfolders as nested menu items
            if (folder.Children is { Count: > 0 })
            {
                BuildMoveToFolderMenu(item, folder.Children, path);
            }
        }
    }

    /// <summary>
    /// Finds the FolderDefinition that directly contains the given distribution node's name.
    /// Searches the entire folder tree recursively.
    /// </summary>
    private FolderDefinition? FindParentFolder(ExplorerNode node)
    {
        if (node.Distribution is null) return null;
        return FindFolderContaining(node.Distribution.Name, _folders);
    }

    private static FolderDefinition? FindFolderContaining(string distName, List<FolderDefinition> folders)
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

    /// <summary>
    /// Finds a FolderDefinition by slash-separated path (e.g. "Food/Restaurants").
    /// </summary>
    private FolderDefinition? FindFolderByPath(string path)
    {
        var parts = path.Split('/');
        var current = _folders;
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

    /// <summary>
    /// Builds the path of folder names from root to the given node by searching the tree.
    /// Returns null if the node is not found.
    /// </summary>
    private List<string>? GetNodePath(ExplorerNode target)
    {
        var path = new List<string>();
        if (FindNodePath(target, _rootNodes, path))
            return path;
        return null;
    }

    private static bool FindNodePath(ExplorerNode target, ObservableCollection<ExplorerNode> nodes, List<string> path)
    {
        foreach (var node in nodes)
        {
            if (node == target)
            {
                path.Add(node.Name);
                return true;
            }
            if (node.IsFolder)
            {
                path.Add(node.Name);
                if (FindNodePath(target, node.Children, path))
                    return true;
                path.RemoveAt(path.Count - 1);
            }
        }
        return false;
    }

    /// <summary>
    /// Finds the FolderDefinition matching a given ExplorerNode by its full path in the tree.
    /// Falls back to name-only search if path is null. Returns the definition and its parent list.
    /// </summary>
    private (FolderDefinition? folder, List<FolderDefinition> parentList) FindFolderDefinition(
        ExplorerNode node)
    {
        var path = GetNodePath(node);
        if (path is not null)
            return FindFolderByNodePath(path);
        // Fallback: name-only search (for cases where node isn't in tree yet)
        return FindFolderDefinitionByName(node.Name);
    }

    private (FolderDefinition? folder, List<FolderDefinition> parentList) FindFolderByNodePath(
        List<string> path)
    {
        var currentList = _folders;
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

    private (FolderDefinition? folder, List<FolderDefinition> parentList) FindFolderDefinitionByName(
        string folderName, List<FolderDefinition>? folders = null)
    {
        folders ??= _folders;
        foreach (var f in folders)
        {
            if (string.Equals(f.Name, folderName, StringComparison.OrdinalIgnoreCase))
                return (f, folders);

            if (f.Children is not null)
            {
                var found = FindFolderDefinitionByName(folderName, f.Children);
                if (found.folder is not null) return found;
            }
        }
        return (null, folders);
    }

    /// <summary>
    /// Removes a distribution name from all folders recursively.
    /// </summary>
    private static void RemoveDistFromAllFolders(string distName, List<FolderDefinition> folders)
    {
        foreach (var f in folders)
        {
            f.DistributionNames.RemoveAll(n =>
                string.Equals(n, distName, StringComparison.OrdinalIgnoreCase));
            if (f.Children is not null)
                RemoveDistFromAllFolders(distName, f.Children);
        }
    }

    // ── Folder CRUD ──

    private void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        _isCreatingNewFolder = true;
        _renamingNode = null;
        _newFolderParent = null; // root level
        ShowRenameOverlay("New Folder");
    }

    private void NewSubfolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } parentNode) return;
        _isCreatingNewFolder = true;
        _renamingNode = null;
        _newFolderParent = parentNode;
        ShowRenameOverlay("New Folder");
    }

    private void RenameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } node) return;
        _isCreatingNewFolder = false;
        _renamingNode = node;
        _newFolderParent = null;
        ShowRenameOverlay(node.Name);
    }

    private void DeleteFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } node) return;

        var (folder, parentList) = FindFolderDefinition(node);
        if (folder is not null)
        {
            parentList.Remove(folder);
            SaveFolders();
            RefreshTree();
        }
    }

    private void MoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true; // Stop bubbling to parent menu items
        if (sender is not MenuItem menuItem) return;
        var folderPath = menuItem.Tag as string;
        if (folderPath is null) return;

        var folder = FindFolderByPath(folderPath);
        if (folder is null) return;

        var selectedDists = GetSelectedDistributionNodes();
        if (selectedDists.Count == 0) return;

        foreach (var node in selectedDists)
        {
            if (node.Distribution is null) continue;
            // Remove from any existing folder first
            RemoveDistFromAllFolders(node.Distribution.Name, _folders);
            folder.DistributionNames.Add(node.Distribution.Name);
        }

        SaveFolders();
        RefreshTree();
    }

    private void RemoveFromFolder_Click(object? sender, RoutedEventArgs e)
    {
        var selectedDists = GetSelectedDistributionNodes();
        if (selectedDists.Count == 0) return;

        foreach (var node in selectedDists)
        {
            if (node.Distribution is null) continue;
            RemoveDistFromAllFolders(node.Distribution.Name, _folders);
        }

        SaveFolders();
        RefreshTree();
    }

    // ── Inline rename ──

    private void ShowRenameOverlay(string currentName)
    {
        RenameBox.Text = currentName;
        RenameOverlay.IsVisible = true;
        RenameBox.Focus();
        RenameBox.SelectAll();
    }

    private void CommitRename()
    {
        RenameOverlay.IsVisible = false;
        var newName = RenameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) return;

        if (_isCreatingNewFolder)
        {
            // Determine target list (root or inside a parent folder)
            List<FolderDefinition> targetList;
            if (_newFolderParent is not null)
            {
                var (parentDef, _) = FindFolderDefinition(_newFolderParent);
                if (parentDef is null) return;
                parentDef.Children ??= [];
                targetList = parentDef.Children;
            }
            else
            {
                targetList = _folders;
            }

            // Don't create duplicate folder names at the same level
            if (targetList.Any(f => string.Equals(f.Name, newName, StringComparison.OrdinalIgnoreCase)))
                return;

            targetList.Add(new FolderDefinition { Name = newName });
        }
        else if (_renamingNode is not null)
        {
            var (folder, _) = FindFolderDefinition(_renamingNode);
            if (folder is not null)
                folder.Name = newName;
        }

        _renamingNode = null;
        _isCreatingNewFolder = false;
        _newFolderParent = null;
        SaveFolders();
        RefreshTree();
    }

    private void CancelRename()
    {
        RenameOverlay.IsVisible = false;
        _renamingNode = null;
        _isCreatingNewFolder = false;
        _newFolderParent = null;
    }

    private void RenameBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelRename();
            e.Handled = true;
        }
    }

    private void RenameBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (RenameOverlay.IsVisible)
            CommitRename();
    }

    // ── Persistence ──

    private void SaveFolders()
    {
        // Sync expansion state from current tree nodes
        SyncExpansionState(_rootNodes, _folders);
        FolderSettings.Save(DeepCopyFolders(_folders));
    }

    /// <summary>
    /// Lightweight save that only syncs expansion state to the existing folder
    /// definitions and writes them directly — no deep copy needed since the
    /// folder structure itself hasn't changed.
    /// </summary>
    private void SaveExpansionState()
    {
        SyncExpansionState(_rootNodes, _folders);
        FolderSettings.Save(_folders);
    }

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

    private static List<FolderDefinition> DeepCopyFolders(List<FolderDefinition> source)
    {
        return source.Select(f => new FolderDefinition
        {
            Name = f.Name,
            DistributionNames = [.. f.DistributionNames],
            Children = f.Children is not null ? DeepCopyFolders(f.Children) : null,
            IsExpanded = f.IsExpanded,
        }).ToList();
    }

    // ── Type filter pills (row 1) — single-select toggle ──

    private void TypeFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string;
        _activeTypeFilter = (_activeTypeFilter == tag) ? null : tag;
        if (tag is null) _activeTypeFilter = null;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    // ── Content filter pills — tri-state ──

    private void ContentFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string;
        ref var state = ref GetContentFilterRef(tag);
        state = state == TriState.Include ? TriState.Ignored : TriState.Include;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private void ContentFilterPill_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button btn) return;
        var point = e.GetCurrentPoint(btn);
        if (!point.Properties.IsRightButtonPressed) return;

        var tag = btn.Tag as string;
        ref var state = ref GetContentFilterRef(tag);
        state = state == TriState.Exclude ? TriState.Ignored : TriState.Exclude;
        UpdateAllPillStyles();
        ApplyFilter();
        e.Handled = true;
    }

    private ref TriState GetContentFilterRef(string? tag)
    {
        if (tag == "Rolls") return ref _rollsFilter;
        if (tag == "Items") return ref _itemsFilter;
        if (tag == "Junk") return ref _junkFilter;
        if (tag == "Procedural") return ref _proceduralFilter;
        if (tag == "DistributionItems") return ref _distributionItemsFilter;
        return ref _procListFilter;
    }

    // ── Structure filter pills — tri-state ──

    private void StructureFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string;
        ref var state = ref GetStructureFilterRef(tag);
        state = state == TriState.Include ? TriState.Ignored : TriState.Include;
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private void StructureFilterPill_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button btn) return;
        var point = e.GetCurrentPoint(btn);
        if (!point.Properties.IsRightButtonPressed) return;

        var tag = btn.Tag as string;
        ref var state = ref GetStructureFilterRef(tag);
        state = state == TriState.Exclude ? TriState.Ignored : TriState.Exclude;
        UpdateAllPillStyles();
        ApplyFilter();
        e.Handled = true;
    }

    private ref TriState GetStructureFilterRef(string? tag)
    {
        switch (tag)
        {
            case "Invalid" :return ref _invalidFilter;
            case "NoContent" :return ref _noContentFilter;
            case "DistributionItems" :return ref _distributionItemsFilter;
            default: return ref _defaultFilter;
        }
    }

    private void UpdateAllPillStyles()
    {
        // Type pills — single-select with "active" class
        foreach (var child in TypeFilterPills.Children)
        {
            if (child is not Button btn) continue;
            var tag = btn.Tag as string;
            if (_activeTypeFilter == tag)
                btn.Classes.Add("active");
            else
                btn.Classes.Remove("active");
        }

        // Content pills — tri-state with "include"/"exclude" classes
        foreach (var child in ContentFilterPills.Children)
        {
            if (child is not Button btn) continue;
            var tag = btn.Tag as string;
            var state = GetContentFilterRef(tag);
            btn.Classes.Remove("include");
            btn.Classes.Remove("exclude");
            if (state == TriState.Include)
                btn.Classes.Add("include");
            else if (state == TriState.Exclude)
                btn.Classes.Add("exclude");
        }

        // Structure pills — tri-state
        foreach (var child in StructureFilterPills.Children)
        {
            if (child is not Button btn) continue;
            var tag = btn.Tag as string;
            var state = GetStructureFilterRef(tag);
            btn.Classes.Remove("include");
            btn.Classes.Remove("exclude");
            if (state == TriState.Include)
                btn.Classes.Add("include");
            else if (state == TriState.Exclude)
                btn.Classes.Add("exclude");
        }
    }
}
