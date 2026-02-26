using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace UI.Controls;

public partial class ProcListListControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ProcListListControl, IEnumerable?>(nameof(ItemsSource));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ProcListListControl()
    {
        InitializeComponent();
        this.GetObservable(ItemsSourceProperty)
            .Subscribe(src => ItemsHost.ItemsSource = src);
    }
}
