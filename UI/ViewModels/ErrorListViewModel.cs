using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DataInput.Errors;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace UI.ViewModels;

/// <summary>
/// Manages the bottom error panel.
/// Same DynamicData filter pattern as DistributionListViewModel —
/// incremental updates, no full collection rebuild on toggle.
/// </summary>
public sealed class ErrorListViewModel : ViewModelBase
{
    private readonly SourceList<ParseError> _source = new();

    [Reactive] public bool ShowErrors   { get; set; } = true;
    [Reactive] public bool ShowWarnings { get; set; } = true;

    [Reactive] public int ErrorCount   { get; private set; }
    [Reactive] public int WarningCount { get; private set; }

    private readonly ReadOnlyObservableCollection<ParseError> _filtered;
    public ReadOnlyObservableCollection<ParseError> FilteredErrors => _filtered;

    public ErrorListViewModel()
    {
        var filterPredicate = this
            .WhenAnyValue(x => x.ShowErrors, x => x.ShowWarnings)
            .Select(args => BuildFilter(args.Item1, args.Item2));

        _source.Connect()
            .Filter(filterPredicate)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _filtered)
            .Subscribe()
            .DisposeWith(Disposables);

        // Counts from the unfiltered source
        _source.Connect()
            .Filter(e => e.IsFatal)
            .Count()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(c => ErrorCount = c)
            .DisposeWith(Disposables);

        _source.Connect()
            .Filter(e => !e.IsFatal)
            .Count()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(c => WarningCount = c)
            .DisposeWith(Disposables);
    }

    public void Load(IEnumerable<ParseError> errors)
    {
        _source.Edit(list =>
        {
            list.Clear();
            list.AddRange(errors);
        });
    }

    private static Func<ParseError, bool> BuildFilter(bool showErrors, bool showWarnings) =>
        e => e.IsFatal ? showErrors : showWarnings;
}
