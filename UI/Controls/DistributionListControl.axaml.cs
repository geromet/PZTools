using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
    private readonly RenameState _rename = new();
    private readonly DistributionListState _state = new();

    public DistributionListControl()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) => ApplyFilter();
        DistTree.ItemsSource = _state.RootNodes;

        FilterPillHelper.WireTriStatePills(ContentFilterPills, _state.Filter, OnFilterChanged);
        FilterPillHelper.WireTriStatePills(StructureFilterPills, _state.Filter, OnFilterChanged);

        var dragDrop = new TreeDragDropHandler(DistTree, this);
        dragDrop.Attach();

        DistTree.DoubleTapped += OnTreeDoubleTapped;
    }

    public ContentFilterSet ContentFilters => _state.Filter.Content;

    public event Action<Distribution?>? SelectionChanged;
    public event Action<Distribution>? OpenRequested;
    public event Action<List<Distribution>>? OpenMultipleRequested;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _state.SaveExpansionState();
    }

    public void SetSettings(UserSettings settings)
    {
        _state.LoadFolders();
    }

    public void Load(IReadOnlyList<Distribution> distributions)
    {
        _state.LoadDistributions(distributions);
        SearchBox.Text = string.Empty;
        UpdateAllPillStyles();
        ApplyFilter();
    }
    
    #region Filtering

    private string SearchQuery => SearchBox.Text?.Trim() ?? string.Empty;

    private void ApplyFilter()
    {
        _state.ApplyFilter(SearchQuery);
        CountText.Text = $"{_state.LastFiltered.Count} / {_state.TotalCount}";
    }

    private void RefreshTree()
    {
        var selectedNames = CaptureSelectedNames();
        var scrollViewer = FindScrollViewer(DistTree);
        var scrollOffset = scrollViewer?.Offset ?? default;

        _state.RebuildTree(SearchQuery, true);

        if (selectedNames.Count > 0)
            RestoreSelection(selectedNames, _state.RootNodes);
        if (scrollViewer is not null)
            scrollViewer.Offset = scrollOffset;
    }

    private HashSet<string> CaptureSelectedNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (DistTree.SelectedItems is not null)
            foreach (var item in DistTree.SelectedItems)
                if (item is ExplorerNode { Distribution: not null } node)
                    names.Add(node.Distribution.Name);
        return names;
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

    #endregion

    #region Tree selection

    private void DistTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var item in e.AddedItems)
            if (item is ExplorerNode { IsFolder: false } node)
            {
                SelectionChanged?.Invoke(node.Distribution);
                return;
            }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual visual) return;
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

    private List<ExplorerNode> GetSelectedDistributionNodes()
    {
        var result = new List<ExplorerNode>();
        if (DistTree.SelectedItems is null) return result;
        foreach (var item in DistTree.SelectedItems)
            if (item is ExplorerNode { IsFolder: false, Distribution: not null } node)
                result.Add(node);
        return result;
    }

    #endregion

    #region ITreeDragDropHost

    ObservableCollection<ExplorerNode> ITreeDragDropHost.RootNodes => _state.RootNodes;
    List<FolderDefinition> ITreeDragDropHost.Folders => _state.Folders;

    void ITreeDragDropHost.SaveFolders()
    {
        _state.SaveFolders();
    }

    void ITreeDragDropHost.RefreshTree()
    {
        RefreshTree();
    }

    (FolderDefinition? folder, List<FolderDefinition> parentList) ITreeDragDropHost.FindFolderDefinition(
        ExplorerNode node)
    {
        return _state.FindFolderDefinition(node);
    }

    bool ITreeDragDropHost.ShowMoveFolderConfirmation(string folderName)
    {
        return ShowMoveFolderConfirmation(folderName);
    }

    private bool ShowMoveFolderConfirmation(string folderName)
    {
        var dialog = new Window
        {
            Title = "Move Folder", Width = 320, Height = 100,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false
        };
        var result = false;
        var yesBtn = new Button { Content = "Yes", Margin = new Thickness(0, 0, 8, 0) };
        var noBtn = new Button { Content = "No" };
        yesBtn.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        noBtn.Click += (_, _) => { dialog.Close(); };
        yesBtn.IsDefault = true;
        noBtn.IsCancel = true;

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(10), Spacing = 16,
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
            dialog.ShowDialog(desktop.MainWindow);
        return result;
    }

    #endregion

    #region Context menu

    private void TreeContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        var selected = DistTree.SelectedItem as ExplorerNode;
        var selectedDistNodes = GetSelectedDistributionNodes();
        var hasDistSelection = selectedDistNodes.Count > 0;
        var hasSingleDist = selected is { IsFolder: false, Distribution: not null };
        var anyInFolder = selectedDistNodes.Any(n =>
            n.Distribution is not null &&
            FolderService.FindFolderContaining(n.Distribution.Name, _state.Folders) is not null);
        var isFolder = selected is { IsFolder: true };

        OpenDistItem.IsVisible = hasSingleDist;
        OpenSelectedDistsItem.IsVisible = selectedDistNodes.Count > 1;
        NewSubfolderItem.IsVisible = isFolder;

        MoveToFolderMenu.Items.Clear();
        BuildMoveToFolderMenu(MoveToFolderMenu, _state.Folders, "");
        MoveToFolderMenu.IsVisible = hasDistSelection && _state.Folders.Count > 0;
        RemoveFromFolderItem.IsVisible = hasDistSelection && anyInFolder;

        RenameFolderItem.IsVisible = isFolder;
        DeleteFolderItem.IsVisible = isFolder;
        FolderExpandSeparator.IsVisible = isFolder;
        ExpandFolderItem.IsVisible = isFolder;
        CollapseFolderItem.IsVisible = isFolder;
    }

    private void BuildMoveToFolderMenu(MenuItem parent, List<FolderDefinition> folders, string pathPrefix)
    {
        foreach (var folder in folders)
        {
            var path = string.IsNullOrEmpty(pathPrefix) ? folder.Name : $"{pathPrefix}/{folder.Name}";
            var item = new MenuItem { Header = folder.Name, Tag = path };
            item.Click += MoveToFolder_Click;
            parent.Items.Add(item);
            if (folder.Children is { Count: > 0 })
                BuildMoveToFolderMenu(item, folder.Children, path);
        }
    }

    #endregion

    #region Expand / collapse

    private void ExpandAll_Click(object? sender, RoutedEventArgs e)
    {
        SetExpandAll(true);
    }

    private void CollapseAll_Click(object? sender, RoutedEventArgs e)
    {
        SetExpandAll(false);
    }

    private void ExpandFolderAll_Click(object? sender, RoutedEventArgs e)
    {
        SetExpandSelectedFolder(true);
    }

    private void CollapseFolderAll_Click(object? sender, RoutedEventArgs e)
    {
        SetExpandSelectedFolder(false);
    }

    private void SetExpandAll(bool expanded)
    {
        DistributionListState.SetExpandedRecursive(_state.RootNodes, expanded);
        _state.SaveExpansionState();
    }

    private void SetExpandSelectedFolder(bool expanded)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } folder) return;
        folder.IsExpanded = expanded;
        DistributionListState.SetExpandedRecursive(folder.Children, expanded);
        _state.SaveExpansionState();
    }

    #endregion

    #region Folder CRUD

    private void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        _rename.BeginCreate(null);
        ShowRenameOverlay("New Folder");
    }

    private void NewSubfolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } parentNode) return;
        _rename.BeginCreate(parentNode);
        ShowRenameOverlay("New Folder");
    }

    private void RenameFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } node) return;
        _rename.BeginRename(node);
        ShowRenameOverlay(node.Name);
    }

    private void DeleteFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DistTree.SelectedItem is not ExplorerNode { IsFolder: true } node) return;
        _state.DeleteFolder(node);
        _state.SaveFolders();
        RefreshTree();
    }

    private void MoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not MenuItem { Tag: string folderPath }) return;
        var distNames = GetSelectedDistributionNodes()
            .Where(n => n.Distribution is not null)
            .Select(n => n.Distribution!.Name)
            .ToList();
        if (distNames.Count == 0) return;

        _state.MoveDistributionsToFolder(distNames, folderPath);
        _state.SaveFolders();
        RefreshTree();
    }

    private void RemoveFromFolder_Click(object? sender, RoutedEventArgs e)
    {
        var distNames = GetSelectedDistributionNodes()
            .Where(n => n.Distribution is not null)
            .Select(n => n.Distribution!.Name)
            .ToList();

        _state.RemoveDistributionsFromFolders(distNames);
        _state.SaveFolders();
        RefreshTree();
    }

    #endregion

    #region Inline rename

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
        if (string.IsNullOrEmpty(newName))
        {
            _rename.Reset();
            return;
        }

        bool changed;
        if (_rename.IsCreating)
            changed = _state.CommitCreate(newName, _rename.NewFolderParent);
        else if (_rename.RenamingNode is not null)
            changed = _state.CommitRenameFolder(_rename.RenamingNode, newName);
        else
            changed = false;

        _rename.Reset();
        if (!changed) return;
        _state.SaveFolders();
        RefreshTree();
    }

    private void CancelRename()
    {
        RenameOverlay.IsVisible = false;
        _rename.Reset();
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
        if (RenameOverlay.IsVisible) CommitRename();
    }

    #endregion

    #region Filter pills

    private void TypeFilterPill_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _state.Filter.ToggleTypeFilter(btn.Tag as string);
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private void OnFilterChanged()
    {
        UpdateAllPillStyles();
        ApplyFilter();
    }

    private void UpdateAllPillStyles()
    {
        foreach (var child in TypeFilterPills.Children)
        {
            if (child is not Button btn) continue;
            if (_state.Filter.ActiveTypeFilter == btn.Tag as string)
                btn.Classes.Add("active");
            else
                btn.Classes.Remove("active");
        }

        FilterPillHelper.ApplyTriStateStyles(ContentFilterPills, _state.Filter);
        FilterPillHelper.ApplyTriStateStyles(StructureFilterPills, _state.Filter);
    }

    #endregion
}