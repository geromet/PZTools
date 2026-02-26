using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace UI.Controls;

public partial class ItemListControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ItemListControl, IEnumerable?>(nameof(ItemsSource));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ItemListControl()
    {
        InitializeComponent();
        this.GetObservable(ItemsSourceProperty)
            .Subscribe(src => ItemsHost.ItemsSource = src);
    }
}
