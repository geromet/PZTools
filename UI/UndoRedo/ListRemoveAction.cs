using System;
using System.Collections.Generic;

namespace UI.UndoRedo;

/// <summary>
/// Undoable action that removes an item from a list at a given index.
/// After execute/undo, calls refreshUi to rebuild the visual rows.
/// </summary>
public sealed class ListRemoveAction<T> : IUndoableAction
{
    private readonly List<T> _list;
    private readonly int _index;
    private readonly T _item;
    private readonly Action _refreshUi;

    public ListRemoveAction(string description, List<T> list, int index, T item, Action refreshUi)
    {
        Description = description;
        _list = list;
        _index = index;
        _item = item;
        _refreshUi = refreshUi;
    }

    public string Description { get; }

    public void Execute()
    {
        _list.RemoveAt(_index);
        _refreshUi();
    }

    public void Undo()
    {
        _list.Insert(_index, _item);
        _refreshUi();
    }
}
