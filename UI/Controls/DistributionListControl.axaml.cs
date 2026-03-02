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

public partial class DistributionListControl : UserControl, ITreeDragDropHost
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
    private TreeDragDropHandler? _dragDropHandler;

    // Inline rename state
    private ExplorerNode? _renamingNode;
    private bool _isCreatingNewFolder;
    private ExplorerNode? _newFolderParent; // null = root, set for subfolders

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
        _dragDropHandler = new TreeDragDropHandler(DistTree, this);
        _dragDropHandler.Attach();

        // Double-click opens a tab
        DistTree.DoubleTapped += OnTreeDoubleTapped;
    }

    public void SetSettings(UserSettings settings)
    {
        _settings = settings;
        _folders = FolderService.DeepCopy(FolderSettings.Load());
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

    private void BuildTree(List<Distribution> filtered)
    {
        FolderTreeBuilder.Build(_rootNodes, _folders, filtered, HasAnyActiveFilter());
        DistTree.ItemsSource = _rootNodes;
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

    // ── ITreeDragDropHost implementation ──

    ObservableCollection<ExplorerNode> ITreeDragDropHost.RootNodes => _rootNodes;
    List<FolderDefinition> ITreeDragDropHost.Folders => _folders;
    void ITreeDragDropHost.SaveFolders() => SaveFolders();
    void ITreeDragDropHost.RefreshTree() => RefreshTree();

    (FolderDefinition? folder, List<FolderDefinition> parentList) ITreeDragDropHost.FindFolderDefinition(
        ExplorerNode node) => FindFolderDefinition(node);

    /// <summary>
    /// Shows a confirmation dialog before moving a folder. Returns true if the user confirmed.
    /// </summary>
    bool ITreeDragDropHost.ShowMoveFolderConfirmation(string folderName) =>
        ShowMoveFolderConfirmation(folderName);

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
        return FolderService.FindFolderContaining(node.Distribution.Name, _folders);
    }

    private FolderDefinition? FindFolderByPath(string path) =>
        FolderService.FindFolderByPath(path, _folders);

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
            return FolderService.FindByNodePath(path, _folders);
        // Fallback: name-only search (for cases where node isn't in tree yet)
        return FolderService.FindByName(node.Name, _folders);
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
            FolderService.RemoveDistFromAllFolders(node.Distribution.Name, _folders);
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
            FolderService.RemoveDistFromAllFolders(node.Distribution.Name, _folders);
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
        FolderSettings.Save(FolderService.DeepCopy(_folders));
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
