using System;

namespace UI.UndoRedo;

/// <summary>
///     An undoable action that swaps a value between two states via a setter delegate.
///     The setter is responsible for updating both the ViewModel property AND the
///     underlying model, so both stay in sync during undo/redo replay.
/// </summary>
public sealed class PropertyChangeAction<T> : IUndoableAction
{
    private readonly T _newValue;
    private readonly T _oldValue;
    private readonly Action<T> _setter;

    /// <param name="description">Label for history UI (e.g. "BathroomCabinet.rolls: 2 → 4")</param>
    /// <param name="setter">Applies a value — must update VM property AND underlying model.</param>
    /// <param name="oldValue">Value before the change (Undo restores this).</param>
    /// <param name="newValue">Value after the change (Execute applies this).</param>
    public PropertyChangeAction(string description, Action<T> setter, T oldValue, T newValue)
    {
        Description = description;
        _setter = setter;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public string Description { get; }

    public void Execute()
    {
        _setter(_newValue);
    }

    public void Undo()
    {
        _setter(_oldValue);
    }
}