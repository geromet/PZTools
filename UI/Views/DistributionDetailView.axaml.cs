using Avalonia.ReactiveUI;
using UI.ViewModels;

namespace UI.Views;

/// <summary>
/// Registered with Splat in App.axaml.cs as IViewFor&lt;DistributionDetailViewModel&gt;.
/// RoutedViewHost instantiates it when DistributionDetailViewModel is on top of the Router.
/// ReactiveUserControl&lt;T&gt; sets DataContext = ViewModel automatically.
/// </summary>
public partial class DistributionDetailView : ReactiveUserControl<DistributionDetailViewModel>
{
    public DistributionDetailView() => InitializeComponent();
}
