using System.Reactive.Linq;
using DataInput.Data;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using UI.UndoRedo;

namespace UI.ViewModels;

/// <summary>
/// Wraps a <see cref="Container"/> and exposes its fields as reactive, undoable properties.
///
/// Undo/redo pattern used throughout this class:
///   1. [Reactive] attribute (Fody) generates RaiseAndSetIfChanged setters.
///   2. WhenAnyValue watches each property.
///   3. Scan accumulates (previous, current) pairs.
///   4. Where(!IsReplaying) prevents the undo system from recording its own replays.
///   5. Subscribe creates a PropertyChangeAction and hands it to the shared UndoRedoStack.
///   6. The action's setter updates BOTH the VM property AND the underlying model,
///      keeping them in sync during undo/redo replay.
/// </summary>
public sealed class ContainerViewModel : ViewModelBase
{
    private readonly Container    _model;
    private readonly UndoRedoStack _undoRedo;

    // ── Undoable reactive properties ─────────────────────────────────────────
    // Fody weaves these into proper RaiseAndSetIfChanged setters.
    [Reactive] public string Name         { get; set; }
    [Reactive] public int    ItemRolls    { get; set; }
    [Reactive] public int    JunkRolls    { get; set; }
    [Reactive] public bool   FillRand     { get; set; }
    [Reactive] public bool   Procedural   { get; set; }
    [Reactive] public bool   DontSpawnAmmo { get; set; }

    // Read-only passthroughs — items/proclists are not editable in this version
    public IReadOnlyList<Item>          ItemChances     => _model.ItemChances;
    public IReadOnlyList<Item>          JunkChances     => _model.JunkChances;
    public IReadOnlyList<ProcListEntry> ProcListEntries => _model.ProcListEntries;

    public ContainerViewModel(Container model, UndoRedoStack undoRedo)
    {
        _model    = model;
        _undoRedo = undoRedo;

        // Initialise from model — these assignments do NOT push undo actions
        // because subscriptions are set up AFTER this block.
        Name          = model.Name;
        ItemRolls     = model.ItemRolls;
        JunkRolls     = model.JunkRolls;
        FillRand      = model.FillRand;
        Procedural    = model.Procedural;
        DontSpawnAmmo = model.DontSpawnAmmo;

        // Now wire up undo tracking for each editable property.
        Track(this.WhenAnyValue(x => x.Name),
            "Name", v => { Name = v; _model.Name = v; });

        Track(this.WhenAnyValue(x => x.ItemRolls),
            "ItemRolls", v => { ItemRolls = v; _model.ItemRolls = v; });

        Track(this.WhenAnyValue(x => x.JunkRolls),
            "JunkRolls", v => { JunkRolls = v; _model.JunkRolls = v; });

        Track(this.WhenAnyValue(x => x.FillRand),
            "FillRand", v => { FillRand = v; _model.FillRand = v; });

        Track(this.WhenAnyValue(x => x.Procedural),
            "Procedural", v => { Procedural = v; _model.Procedural = v; });

        Track(this.WhenAnyValue(x => x.DontSpawnAmmo),
            "DontSpawnAmmo", v => { DontSpawnAmmo = v; _model.DontSpawnAmmo = v; });
    }

    // ── Undo tracking helper ──────────────────────────────────────────────────

    /// <summary>
    /// Wires a WhenAnyValue observable to push a PropertyChangeAction every time
    /// the value changes through user interaction (not during undo/redo replay).
    ///
    /// Scan(seed, accumulator) tracks (previousValue, currentValue) pairs:
    ///   seed     = (initialVal, initialVal) so first real emission gives correct diff
    ///   acc.Item2 = the value from the previous emission (becomes "previous")
    ///   current  = the new value (becomes "current")
    /// </summary>
    private void Track<T>(IObservable<T> property, string fieldName, Action<T> setter)
    {
        var initVal = property.FirstAsync().Wait();

        property
            .Skip(1)                                     // skip the initial subscription emission
            .DistinctUntilChanged()                      // no-op changes don't create undo entries
            .Where(_ => !_undoRedo.IsReplaying)          // don't record the undo system's own writes
            .Scan(
                seed:        (Prev: initVal, Curr: initVal),
                accumulator: (acc, curr) => (acc.Curr, curr)) // (previous, current) sliding window
            .Subscribe(pair =>
            {
                // NOTE: the setter already ran (the [Reactive] property was set by the user).
                // We only need to sync the model here — then record the undo action.
                setter(pair.Curr);  // sync model

                _undoRedo.Push(new PropertyChangeAction<T>(
                    description: $"{_model.Name}.{fieldName}: {pair.Prev} → {pair.Curr}",
                    setter:      setter,
                    oldValue:    pair.Prev,
                    newValue:    pair.Curr));
            })
            .DisposeWith(Disposables);
    }
}
