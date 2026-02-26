using System;
using System.Collections.Generic;

namespace UI.UndoRedo;

/// <summary>
///     Two-stack undo/redo manager.
///     IsReplaying is set to true for the duration of Execute/Undo calls so that
///     callers can guard against re-recording changes made by the undo system itself.
///     Subscribe to StateChanged to update button enabled states / tooltips.
/// </summary>
public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    public bool CanUndo { get; private set; }
    public bool CanRedo { get; private set; }
    public bool IsReplaying { get; private set; }

    public string? NextUndoDescription { get; private set; }
    public string? NextRedoDescription { get; private set; }

    /// <summary>Fired after every Push / Undo / Redo / Clear that changes state.</summary>
    public event Action? StateChanged;

    /// <summary>Records a new action and immediately executes it. Clears redo stack.</summary>
    public void Push(IUndoableAction action)
    {
        _redoStack.Clear();
        _undoStack.Push(action);
        IsReplaying = true;
        try { action.Execute(); }
        finally { IsReplaying = false; }
        Refresh();
    }

    public void Undo()
    {
        if (!_undoStack.TryPop(out var action)) return;
        IsReplaying = true;
        try { action.Undo(); }
        finally { IsReplaying = false; }
        _redoStack.Push(action);
        Refresh();
    }

    public void Redo()
    {
        if (!_redoStack.TryPop(out var action)) return;
        IsReplaying = true;
        try { action.Execute(); }
        finally { IsReplaying = false; }
        _undoStack.Push(action);
        Refresh();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Refresh();
    }

    private void Refresh()
    {
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
        NextUndoDescription = _undoStack.TryPeek(out var u) ? u.Description : null;
        NextRedoDescription = _redoStack.TryPeek(out var r) ? r.Description : null;
        StateChanged?.Invoke();
    }
}
