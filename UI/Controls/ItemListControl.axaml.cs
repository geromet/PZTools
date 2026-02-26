using System.Collections.Generic;
using Avalonia.Controls;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class ItemListControl : UserControl
{
    public ItemListControl()
    {
        InitializeComponent();
    }

    public void Load(List<Item> items, UndoRedoStack undoRedo, string context)
    {
        ItemRowHelper.Populate(RowsPanel, items, undoRedo, context);
    }
}
