using Avalonia.ReactiveUI;
using UI.ViewModels;

namespace UI.Controls;

/// <summary>
/// No logic needed here — everything is driven by ErrorListViewModel via bindings.
/// ToggleButton.IsChecked binds two-way to ShowErrors/ShowWarnings in the VM,
/// which DynamicData picks up automatically to re-filter the collection.
/// </summary>
public partial class ErrorListControl : ReactiveUserControl<ErrorListViewModel>
{
    public ErrorListControl() => InitializeComponent();
}
