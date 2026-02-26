using Avalonia.ReactiveUI;
using UI.ViewModels;

namespace UI.Views;

public partial class EmptyStateView : ReactiveUserControl<EmptyStateViewModel>
{
    public EmptyStateView() => InitializeComponent();
}
