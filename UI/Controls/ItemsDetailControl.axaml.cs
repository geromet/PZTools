using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Core.Items;
using Data.Data;
using UI.Assets;
using UI.UndoRedo;

namespace UI.Controls;

public partial class ItemsDetailControl : UserControl
{
    private static readonly IBrush AccentBrush = SolidColorBrush.Parse("#7B8CDE");
    private static readonly IBrush MutedBrush  = SolidColorBrush.Parse("#6A6A7A");

    private string? _itemName;
    private ItemIndex? _itemIndex;
    private UndoRedoStack? _undoRedo;
    private bool _autoFilter;

    public Func<IReadOnlyList<Distribution>>? GetAllDistributions { get; set; }
    public Func<ItemFilterContext>? GetCurrentFilters { get; set; }
    public event Action<Distribution>? OpenDistributionRequested;
    public event Action<Distribution, ItemParent>? DistributionModified;

    public ItemsDetailControl()
    {
        InitializeComponent();
    }

    public void Load(string itemName, ItemIndex index, UndoRedoStack undoRedo)
    {
        _itemName  = itemName;
        _itemIndex = index;
        _undoRedo  = undoRedo;

        ItemNameText.Text = itemName;
        PopulateAddDistBox();
        Refresh();
    }

    public void Refresh()
    {
        if (_itemName is null || _itemIndex is null || _undoRedo is null) return;

        var occs = _itemIndex.GetOccurrences(_itemName);
        OccurrenceCountText.Text = string.Format(Strings.IDOccurrences, occs.Count);

        IReadOnlyList<ItemOccurrence> visible = occs;
        if (_autoFilter && GetCurrentFilters is not null)
        {
            var ctx = GetCurrentFilters();
            if (ctx.IsActive)
                visible = [.. occs.Where(ctx.Matches)];
        }

        RebuildRows(visible);
    }

    public void RefreshIfAutoFilter()
    {
        if (_autoFilter) Refresh();
    }

    private void AutoFilter_Click(object? sender, RoutedEventArgs e)
    {
        _autoFilter = !_autoFilter;
        AutoFilterBtn.Classes.Set("active", _autoFilter);
        Refresh();
    }

    #region Row building

    private void RebuildRows(IReadOnlyList<ItemOccurrence> occs)
    {
        RowsPanel.Children.Clear();

        for (var i = 0; i < occs.Count; i++)
        {
            var occ = occs[i];
            RowsPanel.Children.Add(BuildRow(occ));
        }
    }

    private Control BuildRow(ItemOccurrence occ)
    {
        // Distribution nav link
        var distLink = new Button
        {
            Content = new TextBlock
            {
                Text = occ.Distribution.Name,
                Foreground = AccentBrush,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(10, 0, 4, 0),
        };
        distLink.Click += (_, _) => OpenDistributionRequested?.Invoke(occ.Distribution);

        // Container name
        var containerText = new TextBlock
        {
            Text = occ.Container?.Name ?? Strings.IDDistLevel,
            FontSize = 11,
            Foreground = occ.Container is null ? MutedBrush : (IBrush)SolidColorBrush.Parse("#C8C8D8"),
            FontStyle = occ.Container is null ? FontStyle.Italic : FontStyle.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0),
        };

        // Kind badge: "Item" or "Junk"
        var kindBrush = occ.IsJunk
            ? SolidColorBrush.Parse("#8B6914")   // amber for junk
            : SolidColorBrush.Parse("#2E6E3E");  // green for items
        var kindBadge = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2),
            Margin = new Thickness(4, 2),
            Background = kindBrush,
            Child = new TextBlock
            {
                Text = occ.IsJunk ? Strings.ILFilterJunk : Strings.ILFilterItems,
                FontSize = 10,
                Foreground = Brushes.White,
            },
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Dist type badge
        var typeBadge = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2),
            Margin = new Thickness(4, 2),
            Background = new SolidColorBrush(Color.Parse("#2A2A3A")),
            Child = new TextBlock
            {
                Text = occ.Distribution.Type.ToString(),
                FontSize = 10,
                Foreground = MutedBrush,
            },
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Chance TextBox
        var parent = (ItemParent?)(occ.Container) ?? occ.Distribution;
        var list   = occ.IsJunk ? parent.JunkChances : parent.ItemChances;
        var chance = occ.Index < list.Count ? list[occ.Index].Chance : 0;

        var chanceBox = new TextBox
        {
            Text = ItemRowHelper.FormatChance(chance),
            Width = 60,
            FontSize = 11,
            Padding = new Thickness(4, 1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(2),
        };

        // Capture occ for closures (occ is a readonly record struct so captured by value is fine)
        var capturedOcc = occ;
        chanceBox.LostFocus += (_, _) => OnChanceLostFocus(chanceBox, capturedOcc);

        // Delete button
        var deleteBtn = new Button
        {
            Content = "\u00d7",
            FontSize = 12,
            Padding = new Thickness(2, 0),
            MinWidth = 22,
            MinHeight = 0,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = MutedBrush,
        };
        deleteBtn.Click += (_, _) => OnDeleteClick(capturedOcc);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,120,50,80,65,26"),
            MinHeight = 24,
        };
        row.Children.Add(distLink);
        row.Children.Add(containerText);
        row.Children.Add(kindBadge);
        row.Children.Add(typeBadge);
        row.Children.Add(chanceBox);
        row.Children.Add(deleteBtn);
        Grid.SetColumn(distLink,      0);
        Grid.SetColumn(containerText, 1);
        Grid.SetColumn(kindBadge,     2);
        Grid.SetColumn(typeBadge,     3);
        Grid.SetColumn(chanceBox,     4);
        Grid.SetColumn(deleteBtn,     5);

        return row;
    }

    #endregion

    #region Chance edit

    private void OnChanceLostFocus(TextBox chanceBox, ItemOccurrence occ)
    {
        if (_undoRedo is null || _itemName is null) return;
        if (!double.TryParse(chanceBox.Text, out var newChance))
        {
            // Restore
            var parent2 = (ItemParent?)(occ.Container) ?? occ.Distribution;
            var list2   = occ.IsJunk ? parent2.JunkChances : parent2.ItemChances;
            chanceBox.Text = occ.Index < list2.Count
                ? ItemRowHelper.FormatChance(list2[occ.Index].Chance)
                : "0";
            return;
        }

        var parent = (ItemParent?)(occ.Container) ?? occ.Distribution;
        var list   = occ.IsJunk ? parent.JunkChances : parent.ItemChances;
        if (occ.Index >= list.Count) return;

        var oldItem = list[occ.Index];
        if (newChance == oldItem.Chance) return;
        var newItem = oldItem with { Chance = newChance };

        _undoRedo.Push(new PropertyChangeAction<Item>(
            $"{_itemName} chance: {oldItem.Chance}\u2192{newChance}",
            v =>
            {
                list[occ.Index] = v;
                parent.IsDirty  = true;
                Refresh();
                DistributionModified?.Invoke(occ.Distribution, parent);
            },
            oldItem, newItem));
    }

    #endregion

    #region Delete

    private void OnDeleteClick(ItemOccurrence occ)
    {
        if (_undoRedo is null || _itemName is null) return;

        var parent = (ItemParent?)(occ.Container) ?? occ.Distribution;
        var list   = occ.IsJunk ? parent.JunkChances : parent.ItemChances;
        if (occ.Index >= list.Count) return;

        var item = list[occ.Index];
        _undoRedo.Push(new ListRemoveAction<Item>(
            $"{_itemName}: remove from {occ.Distribution.Name}",
            list, occ.Index, item,
            () => { parent.IsDirty = true; Refresh(); DistributionModified?.Invoke(occ.Distribution, parent); }));
    }

    #endregion

    #region Add row

    private void PopulateAddDistBox()
    {
        // Initial population with all names sorted alphabetically.
        UpdateAddDistSuggestions(string.Empty);
    }

    private void AddDistBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateAddDistSuggestions(AddDistBox.Text ?? string.Empty);
    }

    private void AddDistBox_DropDownOpened(object? sender, EventArgs e)
    {
        // Scroll the dropdown list back to the top each time it opens.
        Dispatcher.UIThread.Post(() =>
        {
            var lb = AddDistBox.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
            if (lb?.ItemCount > 0) lb.ScrollIntoView(lb.Items[0]!);
        }, DispatcherPriority.Loaded);
    }

    private void UpdateAddDistSuggestions(string query)
    {
        if (GetAllDistributions is null) return;
        var suggestions = SearchHelper.SortedByRelevance(
            GetAllDistributions().Select(d => d.Name), query);
        // Defer the assignment: setting ItemsSource synchronously inside the
        // AutoCompleteBox selection-change event clears the dropdown's internal
        // list while the selection model is still mid-commit → crash.
        Dispatcher.UIThread.Post(
            () => AddDistBox.ItemsSource = suggestions,
            DispatcherPriority.Background);
    }

    private void AddDistBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AddContainerBox.Items.Clear();

        var distName = AddDistBox.Text;
        if (string.IsNullOrWhiteSpace(distName)) return;

        var dist = GetAllDistributions?.Invoke()
            .FirstOrDefault(d => string.Equals(d.Name, distName, StringComparison.OrdinalIgnoreCase));
        if (dist is null) return;

        AddContainerBox.Items.Add(Strings.IDDistLevel);
        foreach (var c in dist.Containers)
            AddContainerBox.Items.Add(c.Name);

        if (AddContainerBox.Items.Count > 0)
            AddContainerBox.SelectedIndex = 0;
    }

    private void AddBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_undoRedo is null || _itemName is null) return;

        var distName = AddDistBox.Text?.Trim();
        if (string.IsNullOrEmpty(distName)) return;

        var dist = GetAllDistributions?.Invoke()
            .FirstOrDefault(d => string.Equals(d.Name, distName, StringComparison.OrdinalIgnoreCase));
        if (dist is null) return;

        if (!double.TryParse(AddChanceBox.Text, out var chance))
            chance = 1;

        var selectedContainer = AddContainerBox.SelectedItem as string;
        ItemParent parent;
        if (selectedContainer is null || selectedContainer == Strings.IDDistLevel)
        {
            parent = dist;
        }
        else
        {
            var container = dist.Containers.FirstOrDefault(c =>
                string.Equals(c.Name, selectedContainer, StringComparison.OrdinalIgnoreCase));
            parent = (ItemParent?)container ?? dist;
        }

        var newItem = new Item(_itemName, chance);
        var list    = parent.ItemChances; // Add to items list by default
        var idx     = list.Count;

        _undoRedo.Push(new ListInsertAction<Item>(
            $"{_itemName}: add to {dist.Name}",
            list, idx, newItem,
            () => { parent.IsDirty = true; Refresh(); DistributionModified?.Invoke(dist, parent); }));

        // Reset add form
        AddChanceBox.Text = "1";
    }

    #endregion
}
