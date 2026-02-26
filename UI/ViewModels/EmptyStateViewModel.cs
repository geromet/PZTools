using ReactiveUI;

namespace UI.ViewModels;

/// <summary>
/// Placeholder routable VM shown in the center panel when no distribution is selected.
/// Registered in Splat alongside its corresponding EmptyStateView.
/// </summary>
public sealed class EmptyStateViewModel : ViewModelBase, IRoutableViewModel
{
    public string  UrlPathSegment => "empty";
    public IScreen HostScreen     { get; }

    public EmptyStateViewModel(IScreen screen) => HostScreen = screen;
}
