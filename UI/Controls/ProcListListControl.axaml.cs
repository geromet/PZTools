using System.Collections.Generic;
using Avalonia.Controls;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class ProcListListControl : UserControl
{
    public ProcListListControl()
    {
        InitializeComponent();
    }

    public void Load(List<ProcListEntry> entries, UndoRedoStack undoRedo)
    {
        EntriesPanel.Children.Clear();
        foreach (var entry in entries)
        {
            var ctrl = new ProcListEntryControl();
            ctrl.Load(entry, undoRedo);
            EntriesPanel.Children.Add(ctrl);
        }
    }
}
