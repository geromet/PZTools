using Avalonia.Controls;
using Data.Data;
using UI.UndoRedo;

namespace UI.Controls;

public static class UndoHelper
{
    public static void PushItemInsert(
        UndoRedoStack undoRedo, ItemParent owner,
        List<Item> items, StackPanel panel, string context)
    {
        var newItem = new Item("NewItem", 1);
        var index = items.Count;
        undoRedo.Push(new ListInsertAction<Item>(
            $"{context}: add '{newItem.Name}'",
            items, index, newItem,
            () => { ItemRowHelper.Populate(panel, items, undoRedo, context, owner); owner.IsDirty = true; }));
    }

    public static void PushIntChange(
        UndoRedoStack undoRedo, IDirtyEntry model, TextBox box,
        string propName, int oldVal, Action<int> setModel,
        Action<int>? extraApply = null)
    {
        if (!int.TryParse(box.Text, out var newVal))
        {
            box.Text = oldVal.ToString();
            return;
        }
        if (newVal == oldVal) return;
        undoRedo.Push(new PropertyChangeAction<int>(
            $"{model.Name}.{propName}: {oldVal}\u2192{newVal}",
            v =>
            {
                setModel(v);
                box.Text = v.ToString();
                extraApply?.Invoke(v);
                model.IsDirty = true;
            },
            oldVal, newVal));
    }

    public static void PushBoolChange(
        UndoRedoStack undoRedo, IDirtyEntry model, CheckBox check,
        string propName, bool oldVal, Action<bool> setModel,
        Action<bool>? extraApply = null)
    {
        var newVal = check.IsChecked == true;
        if (newVal == oldVal) return;
        undoRedo.Push(new PropertyChangeAction<bool>(
            $"{model.Name}.{propName}: {oldVal}\u2192{newVal}",
            v =>
            {
                setModel(v);
                check.IsChecked = v;
                extraApply?.Invoke(v);
                model.IsDirty = true;
            },
            oldVal, newVal));
    }

    public static void PushStringChange(
        UndoRedoStack undoRedo, IDirtyEntry model, TextBox box,
        string propName, string? oldVal, Action<string?> setModel)
    {
        var newVal = box.Text ?? string.Empty;
        var old = oldVal ?? string.Empty;
        if (newVal == old) return;
        undoRedo.Push(new PropertyChangeAction<string>(
            $"{model.Name}.{propName}",
            v =>
            {
                setModel(string.IsNullOrEmpty(v) ? null : v);
                box.Text = v;
                model.IsDirty = true;
            },
            old, newVal));
    }
}
