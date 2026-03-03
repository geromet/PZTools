using Avalonia.Interactivity;
using Data.Data;

namespace UI.Controls.Helpers;

public class NavigateToDistributionEventArgs(RoutedEvent routedEvent, Distribution distribution)
    : RoutedEventArgs(routedEvent)
{
    public Distribution Distribution { get; } = distribution;
}