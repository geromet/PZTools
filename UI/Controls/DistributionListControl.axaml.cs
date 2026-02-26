using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using UI.ViewModels;

namespace UI.Controls;

/// <summary>
/// ReactiveUserControl so DataContext is typed to DistributionListViewModel.
/// Filter pill clicks delegate to the VM's SetFilter method; the code-behind
/// only handles the CSS class toggle (active/inactive visual state).
/// </summary>
public partial class DistributionListControl : ReactiveUserControl<DistributionListViewModel>
{
    public DistributionListControl() => InitializeComponent();

    private void FilterPill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || ViewModel is null) return;

        var filter = btn.Tag as string; // null = "All"
        ViewModel.SetFilter(filter);
        UpdatePillStyles(ViewModel.ActiveFilter);
    }

    private void UpdatePillStyles(string? activeFilter)
    {
        foreach (var child in FilterPills.Children)
        {
            if (child is not Button btn) continue;
            var tag = btn.Tag as string; // null = "All"

            bool isActive = activeFilter == tag;   // null == null for "All" pill
            if (isActive) btn.Classes.Add("active");
            else          btn.Classes.Remove("active");
        }
    }
}
