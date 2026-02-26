using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DataInput.Data;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using UI.UndoRedo;

namespace UI.ViewModels;

/// <summary>
/// Manages the left-panel distribution list.
///
/// DynamicData pipeline:
///   SourceList → Filter (type + search) → Sort → Bind → ReadOnlyObservableCollection
///
/// When the user selects a distribution the VM navigates the router to a fresh
/// DistributionDetailViewModel, ensuring a clean undo scope per selection.
///
/// Filter changes patch the existing collection incrementally — no full rebuild,
/// no ListBox flicker, no redundant re-renders even on 50k-row tables.
/// </summary>
public sealed class DistributionListViewModel : ViewModelBase
{
    // ── DynamicData source — populated after each parse ───────────────────────
    private readonly SourceList<Distribution> _source = new();

    // ── Reactive filter state ─────────────────────────────────────────────────
    [Reactive] public string  SearchQuery  { get; set; } = string.Empty;
    [Reactive] public string? ActiveFilter { get; set; } // null = All

    // ── Selection ─────────────────────────────────────────────────────────────
    [Reactive] public Distribution? SelectedDistribution { get; set; }

    // ── Filtered + sorted output, bound directly to the ListBox ──────────────
    private readonly ReadOnlyObservableCollection<Distribution> _filtered;
    public ReadOnlyObservableCollection<Distribution> FilteredDistributions => _filtered;

    // ── Stats for display ─────────────────────────────────────────────────────
    [Reactive] public int TotalCount    { get; private set; }
    [Reactive] public int FilteredCount { get; private set; }

    public DistributionListViewModel(IScreen screen, UndoRedoStack undoRedo)
    {
        // Build the filter predicate as an observable so DynamicData re-filters
        // automatically whenever SearchQuery or ActiveFilter changes.
        var filterPredicate = this
            .WhenAnyValue(x => x.SearchQuery, x => x.ActiveFilter)
            .Throttle(TimeSpan.FromMilliseconds(120), RxApp.TaskpoolScheduler) // debounce typing
            .Select(args => BuildFilter(args.Item1, args.Item2));

        _source.Connect()
            .Filter(filterPredicate)       // incremental diff — only changed rows are updated
            .Sort(SortExpressionComparer<Distribution>.Ascending(d => d.Name))
            .ObserveOn(RxApp.MainThreadScheduler) // UI thread for binding
            .Bind(out _filtered)
            .Subscribe()
            .DisposeWith(Disposables);

        // Keep FilteredCount in sync via the same pipeline (Count operator)
        _source.Connect()
            .Filter(filterPredicate)
            .Count()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(c => FilteredCount = c)
            .DisposeWith(Disposables);

        // Track total separately (no filter)
        _source.CountChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(c => TotalCount = c)
            .DisposeWith(Disposables);

        // Navigate to detail when user selects a distribution
        this.WhenAnyValue(x => x.SelectedDistribution)
            .Where(d => d is not null)
            .Select(d => d!)
            .Subscribe(d => screen.Router.Navigate.Execute(
                new DistributionDetailViewModel(d, screen, undoRedo)))
            .DisposeWith(Disposables);
    }

    // ── Public API called by MainViewModel ────────────────────────────────────

    public void Load(IEnumerable<Distribution> distributions)
    {
        _source.Edit(list =>
        {
            list.Clear();
            list.AddRange(distributions);
        });
        // Reset selection and filter on new data
        SelectedDistribution = null;
        ActiveFilter         = null;
        SearchQuery          = string.Empty;
    }

    public void SetFilter(string? filter)
    {
        // Toggle: clicking the active filter clears it
        ActiveFilter = filter == ActiveFilter ? null : filter;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Func<Distribution, bool> BuildFilter(string query, string? typeFilter)
    {
        var q = query.Trim();
        return d =>
        {
            if (typeFilter is not null && d.Type.ToString() != typeFilter)
                return false;
            if (q.Length > 0 && !d.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        };
    }
}
