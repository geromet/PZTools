using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace UI.UndoRedo;

/// <summary>
/// Two-stack undo/redo manager exposed as a ReactiveObject so CanUndo/CanRedo
/// can drive command enablement and UI labels reactively.
///
/// IsReplaying is set to true for the duration of Execute/Undo calls so that
/// ViewModels can guard against re-recording changes made by the undo system itself.
///
/// Usage in a ViewModel:
///   this.WhenAnyValue(x => x.SomeProp)
///       .Skip(1)
///       .Where(_ => !_undoRedo.IsReplaying)
///       .Scan(...)
///       .Subscribe(pair => _undoRedo.Push(new PropertyChangeAction&lt;T&gt;(...)));
/// </summary>
public sealed class UndoRedoStack : ReactiveObject
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    [Reactive] public bool CanUndo     { get; private set; }
    [Reactive] public bool CanRedo     { get; private set; }
    [Reactive] public bool IsReplaying { get; private set; }

    // The most recent action's label, for "Undo: Change rolls 2→4" style toolbar tips
    [Reactive] public string? NextUndoDescription { get; private set; }
    [Reactive] public string? NextRedoDescription { get; private set; }

    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    public UndoRedoStack()
    {
        UndoCommand = ReactiveCommand.Create(
            Undo,
            this.WhenAnyValue(x => x.CanUndo));

        RedoCommand = ReactiveCommand.Create(
            Redo,
            this.WhenAnyValue(x => x.CanRedo));
    }

    /// <summary>
    /// Records a new action and immediately executes it.
    /// Pushing a new action always clears the redo stack — just like any text editor.
    /// </summary>
    public void Push(IUndoableAction action)
    {
        _redoStack.Clear();
        _undoStack.Push(action);
        IsReplaying = true;
        try   { action.Execute(); }
        finally { IsReplaying = false; }
        Refresh();
    }

    private void Undo()
    {
        if (!_undoStack.TryPop(out var action)) return;
        IsReplaying = true;
        try   { action.Undo(); }
        finally { IsReplaying = false; }
        _redoStack.Push(action);
        Refresh();
    }

    private void Redo()
    {
        if (!_redoStack.TryPop(out var action)) return;
        IsReplaying = true;
        try   { action.Execute(); }
        finally { IsReplaying = false; }
        _undoStack.Push(action);
        Refresh();
    }

    private void Refresh()
    {
        CanUndo              = _undoStack.Count > 0;
        CanRedo              = _redoStack.Count > 0;
        NextUndoDescription  = _undoStack.TryPeek(out var u) ? u.Description : null;
        NextRedoDescription  = _redoStack.TryPeek(out var r) ? r.Description : null;
    }
}
