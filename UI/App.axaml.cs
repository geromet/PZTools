using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Splat;
using UI.ViewModels;
using UI.Views;

namespace UI;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Register all routable views with Splat so RoutedViewHost can find them.
        // Add new views here as the app grows — this is the only place routing
        // registration lives.
        Locator.CurrentMutable.Register(
            () => new DistributionDetailView(),
            typeof(IViewFor<DistributionDetailViewModel>));

        Locator.CurrentMutable.Register(
            () => new EmptyStateView(),
            typeof(IViewFor<EmptyStateViewModel>));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();

            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
