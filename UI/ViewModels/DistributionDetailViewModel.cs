using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DataInput.Data;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using UI.UndoRedo;

namespace UI.ViewModels;

/// <summary>
/// Routable ViewModel for a single Distribution.
/// Pushed onto MainViewModel.Router when the user selects a distribution.
///
/// Wraps the distribution's editable scalar fields as reactive, undoable properties.
/// Container children are wrapped in their own ContainerViewModels which share
/// the same UndoRedoStack, so undo/redo works across the entire selection.
/// </summary>
public sealed class DistributionDetailViewModel : ViewModelBase, IRoutableViewModel
{
    private readonly Distribution  _model;
    private readonly UndoRedoStack _undoRedo;

    // IRoutableViewModel
    public string  UrlPathSegment => $"distribution/{_model.Name}";
    public IScreen HostScreen     { get; }

    // ── Editable reactive properties ─────────────────────────────────────────
    [Reactive] public bool IsShop        { get; set; }
    [Reactive] public bool DontSpawnAmmo { get; set; }
    [Reactive] public int  ItemRolls     { get; set; }
    [Reactive] public int? MaxMap        { get; set; }
    [Reactive] public int? StashChance   { get; set; }

    // ── Read-only display properties ─────────────────────────────────────────
    public string           Name { get; } // names are identifiers — not editable
    public DistributionType Type { get; }
    public IReadOnlyList<Item> ItemChances => _model.ItemChances;
    public IReadOnlyList<Item> JunkChances => _model.JunkChances;

    public ReadOnlyObservableCollection<ContainerViewModel> Containers { get; }
    private readonly ObservableCollection<ContainerViewModel> _containers = new();

    public DistributionDetailViewModel(
        Distribution   model,
        IScreen        hostScreen,
        UndoRedoStack  undoRedo)
    {
        _model    = model;
        _undoRedo = undoRedo;
        HostScreen = hostScreen;

        Name = model.Name;
        Type = model.Type;

        // Initialise from model before subscriptions are wired up
        IsShop        = model.IsShop;
        DontSpawnAmmo = model.DontSpawnAmmo;
        ItemRolls     = model.ItemRolls;
        MaxMap        = model.MaxMap;
        StashChance   = model.StashChance;

        // Wrap each container — they all share the same undo stack
        foreach (var c in model.Containers)
            _containers.Add(new ContainerViewModel(c, undoRedo));
        Containers = new ReadOnlyObservableCollection<ContainerViewModel>(_containers);

        // Wire undo tracking for this VM's own editable fields
        Track(this.WhenAnyValue(x => x.IsShop),
            "IsShop", v => { IsShop = v; _model.IsShop = v; });

        Track(this.WhenAnyValue(x => x.DontSpawnAmmo),
            "DontSpawnAmmo", v => { DontSpawnAmmo = v; _model.DontSpawnAmmo = v; });

        Track(this.WhenAnyValue(x => x.ItemRolls),
            "ItemRolls", v => { ItemRolls = v; _model.ItemRolls = v; });

        Track(this.WhenAnyValue(x => x.MaxMap),
            "MaxMap", v => { MaxMap = v; _model.MaxMap = v; });

        Track(this.WhenAnyValue(x => x.StashChance),
            "StashChance", v => { StashChance = v; _model.StashChance = v; });
    }

    private void Track<T>(IObservable<T> property, string fieldName, Action<T> setter)
    {
        var initVal = property.FirstAsync().Wait();

        property
            .Skip(1)
            .DistinctUntilChanged()
            .Where(_ => !_undoRedo.IsReplaying)
            .Scan(
                seed:        (Prev: initVal, Curr: initVal),
                accumulator: (acc, curr) => (acc.Curr, curr))
            .Subscribe(pair =>
            {
                setter(pair.Curr);
                _undoRedo.Push(new PropertyChangeAction<T>(
                    description: $"{Name}.{fieldName}: {pair.Prev} → {pair.Curr}",
                    setter:      setter,
                    oldValue:    pair.Prev,
                    newValue:    pair.Curr));
            })
            .DisposeWith(Disposables);
    }
}
