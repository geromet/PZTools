namespace UI.UndoRedo;

/// <summary>
///     Represents a reversible user action.
///     Execute applies the change; Undo reverses it.
///     Both must leave the application in a consistent state.
/// </summary>
public interface IUndoableAction
{
    /// <summary>Human-readable label shown in an undo/redo history list.</summary>
    string Description { get; }

    void Execute();
    void Undo();
}