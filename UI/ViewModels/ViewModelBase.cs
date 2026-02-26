using System.Reactive.Disposables;
using ReactiveUI;

namespace UI.ViewModels;

/// <summary>
/// Base class for all ViewModels.
/// Provides a CompositeDisposable that is disposed when the VM is disposed —
/// all WhenAnyValue / observable subscriptions should be added to Disposables
/// so they are cleaned up automatically when the VM is no longer needed.
/// </summary>
public abstract class ViewModelBase : ReactiveObject, IDisposable
{
    protected readonly CompositeDisposable Disposables = new();

    public void Dispose() => Disposables.Dispose();
}
