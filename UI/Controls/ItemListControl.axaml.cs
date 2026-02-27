using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class ItemListControl : UserControl
{
    private List<Item>? _items;
    private UndoRedoStack? _undoRedo;
    private string _context = string.Empty;
    private ItemParent? _owner;

    public ItemListControl()
    {
        InitializeComponent();
    }

    public void Load(List<Item> items, UndoRedoStack undoRedo, string context, ItemParent owner)
    {
        _items = items;
        _undoRedo = undoRedo;
        _context = context;
        _owner = owner;
        ItemRowHelper.Populate(RowsPanel, items, undoRedo, context, owner);
    }

    private void AddItemBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_items is null || _undoRedo is null || _owner is null) return;
        var newItem = new Item("NewItem", 1);
        var index = _items.Count;
        _undoRedo.Push(new ListInsertAction<Item>(
            $"{_context}: add '{newItem.Name}'",
            _items, index, newItem,
            () => { ItemRowHelper.Populate(RowsPanel, _items, _undoRedo, _context, _owner); _owner.IsDirty = true; }));
    }
}
