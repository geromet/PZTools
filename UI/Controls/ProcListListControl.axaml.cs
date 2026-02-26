using System.Collections;
using Avalonia.Controls;

namespace UI.Controls;

public partial class ProcListListControl : UserControl
{
    public ProcListListControl()
    {
        InitializeComponent();
    }

    public void SetItems(IEnumerable? items)
    {
        ItemsHost.ItemsSource = items;
    }
}
