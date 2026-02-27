using Avalonia.Controls;
using Avalonia.Interactivity;
using DataInput.Data;

namespace UI.Controls;

public class NavigateToDistributionEventArgs(RoutedEvent routedEvent, Distribution distribution)
    : RoutedEventArgs(routedEvent)
{
    public Distribution Distribution { get; } = distribution;
}

public partial class ProcListEntryControl : UserControl
{
    public static readonly RoutedEvent<NavigateToDistributionEventArgs> NavigateRequestedEvent =
        RoutedEvent.Register<ProcListEntryControl, NavigateToDistributionEventArgs>(
            nameof(NavigateRequested), RoutingStrategies.Bubble);

    public event EventHandler<NavigateToDistributionEventArgs>? NavigateRequested
    {
        add => AddHandler(NavigateRequestedEvent, value);
        remove => RemoveHandler(NavigateRequestedEvent, value);
    }

    public ProcListEntryControl()
    {
        InitializeComponent();
    }

    private void DistributionName_Click(object? sender, RoutedEventArgs e)
    {
        var entry = DataContext as ProcListEntry;
        if (entry?.ResolvedDistribution is null) return;
        RaiseEvent(new NavigateToDistributionEventArgs(NavigateRequestedEvent, entry.ResolvedDistribution));
    }
}