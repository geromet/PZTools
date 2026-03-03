using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using UI.Assets;

namespace UI.Controls.Helpers;

public static class TabHeaderHelper
{
    private static readonly IBrush DirtyDotBrush = SolidColorBrush.Parse("#C88B2A");
    private static readonly IBrush TabNameBrush  = SolidColorBrush.Parse("#8A8A98");
    private static readonly IBrush TabCloseBrush = SolidColorBrush.Parse("#5A5A6A");
    private static readonly IBrush PinIconBrush  = SolidColorBrush.Parse("#8A8A98");

    public static StackPanel Create(
        TabState state,
        Func<TabState, Task> onClose,
        Func<Task> onCloseAll,
        Func<TabState, Task> onCloseOthers,
        Func<TabState, bool, Task> onCloseToSide)
    {
        var pinIcon = new TextBlock
        {
            Text = "\ud83d\udccc", FontSize = 10, Tag = "pin",
            Foreground = PinIconBrush, IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        var dirtyDot = new Border
        {
            Width = 6, Height = 6, Tag = "dirty",
            CornerRadius = new CornerRadius(3), Background = DirtyDotBrush,
            IsVisible = false, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        var nameBlock = new TextBlock
        {
            Text = state.Distribution.Name, Foreground = TabNameBrush,
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center
        };
        var closeBtn = new Button
        {
            Content = "\u00d7", Width = 18, Height = 18,
            Padding = new Thickness(0), FontSize = 14,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = TabCloseBrush, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0), Cursor = new Cursor(StandardCursorType.Hand)
        };
        closeBtn.Click += (_, _) => _ = onClose(state);

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2,
            Children = { pinIcon, dirtyDot, nameBlock, closeBtn }
        };
        header.ContextMenu = BuildContextMenu(state, onClose, onCloseAll, onCloseOthers, onCloseToSide);
        return header;
    }

    public static void RefreshPinIcon(TabState state)
    {
        if (state.TabItem.Header is not StackPanel header) return;
        foreach (var child in header.Children)
            if (child is TextBlock { Tag: "pin" } pin)
                pin.IsVisible = state.IsPinned;
        if (header.ContextMenu?.Items[0] is MenuItem m)
            m.Header = state.IsPinned ? Strings.TabUnpin : Strings.TabPin;
    }

    public static void RefreshDirtyDot(TabState state, bool dirty)
    {
        if (state.TabItem.Header is not StackPanel header) return;
        foreach (var child in header.Children)
            if (child is Border { Tag: "dirty" } dot)
                dot.IsVisible = dirty;
    }

    private static ContextMenu BuildContextMenu(
        TabState state,
        Func<TabState, Task> onClose,
        Func<Task> onCloseAll,
        Func<TabState, Task> onCloseOthers,
        Func<TabState, bool, Task> onCloseToSide)
    {
        var pinItem = new MenuItem { Header = state.IsPinned ? Strings.TabUnpin : Strings.TabPin };
        pinItem.Click += (_, _) => { state.IsPinned = !state.IsPinned; RefreshPinIcon(state); };

        var close      = Item(Strings.TabClose,        () => onClose(state));
        var closeAll   = Item(Strings.TabCloseAll,     onCloseAll);
        var closeOther = Item(Strings.TabCloseOthers,  () => onCloseOthers(state));
        var closeLeft  = Item(Strings.TabCloseLeft,    () => onCloseToSide(state, true));
        var closeRight = Item(Strings.TabCloseRight,   () => onCloseToSide(state, false));

        return new ContextMenu
        {
            Items = { pinItem, new Separator(), close, closeAll, closeOther, new Separator(), closeLeft, closeRight }
        };
    }

    public static StackPanel CreateForItem(
        ItemTabState state,
        Func<ItemTabState, Task> onClose,
        Func<Task> onCloseAll,
        Func<ItemTabState, Task> onCloseOthers,
        Func<ItemTabState, bool, Task> onCloseToSide)
    {
        var pinIcon = new TextBlock
        {
            Text = "\ud83d\udccc", FontSize = 10, Tag = "pin",
            Foreground = PinIconBrush, IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        var dirtyDot = new Border
        {
            Width = 6, Height = 6, Tag = "dirty",
            CornerRadius = new CornerRadius(3), Background = DirtyDotBrush,
            IsVisible = false, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        var nameBlock = new TextBlock
        {
            Text = state.ItemName, Foreground = TabNameBrush,
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center
        };
        var closeBtn = new Button
        {
            Content = "\u00d7", Width = 18, Height = 18,
            Padding = new Thickness(0), FontSize = 14,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = TabCloseBrush, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0), Cursor = new Cursor(StandardCursorType.Hand)
        };
        closeBtn.Click += (_, _) => _ = onClose(state);

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2,
            Children = { pinIcon, dirtyDot, nameBlock, closeBtn }
        };
        header.ContextMenu = BuildItemContextMenu(state, onClose, onCloseAll, onCloseOthers, onCloseToSide);
        return header;
    }

    public static void RefreshPinIconForItem(ItemTabState state)
    {
        if (state.TabItem.Header is not StackPanel header) return;
        foreach (var child in header.Children)
            if (child is TextBlock { Tag: "pin" } pin)
                pin.IsVisible = state.IsPinned;
        if (header.ContextMenu?.Items[0] is MenuItem m)
            m.Header = state.IsPinned ? Strings.TabUnpin : Strings.TabPin;
    }

    public static void RefreshDirtyDotForItem(ItemTabState state, bool dirty)
    {
        if (state.TabItem.Header is not StackPanel header) return;
        foreach (var child in header.Children)
            if (child is Border { Tag: "dirty" } dot)
                dot.IsVisible = dirty;
    }

    private static ContextMenu BuildItemContextMenu(
        ItemTabState state,
        Func<ItemTabState, Task> onClose,
        Func<Task> onCloseAll,
        Func<ItemTabState, Task> onCloseOthers,
        Func<ItemTabState, bool, Task> onCloseToSide)
    {
        var pinItem = new MenuItem { Header = state.IsPinned ? Strings.TabUnpin : Strings.TabPin };
        pinItem.Click += (_, _) => { state.IsPinned = !state.IsPinned; RefreshPinIconForItem(state); };

        var close      = Item(Strings.TabClose,       () => onClose(state));
        var closeAll   = Item(Strings.TabCloseAll,    onCloseAll);
        var closeOther = Item(Strings.TabCloseOthers, () => onCloseOthers(state));
        var closeLeft  = Item(Strings.TabCloseLeft,   () => onCloseToSide(state, true));
        var closeRight = Item(Strings.TabCloseRight,  () => onCloseToSide(state, false));

        return new ContextMenu
        {
            Items = { pinItem, new Separator(), close, closeAll, closeOther, new Separator(), closeLeft, closeRight }
        };
    }

    private static MenuItem Item(string header, Func<Task> action)
    {
        var m = new MenuItem { Header = header };
        m.Click += (_, _) => _ = action();
        return m;
    }
}
