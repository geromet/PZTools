using Avalonia.Controls;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

public partial class DistributionDetailControl : UserControl
{
    public DistributionDetailControl()
    {
        InitializeComponent();
    }

    public void Load(Distribution d, UndoRedoStack undoRedo)
    {
        EmptyPanel.IsVisible = false;
        DetailPanel.IsVisible = true;

        HeaderName.Text = d.Name;
        HeaderType.Text = d.Type.ToString();
        ShopBadge.IsVisible = d.IsShop;
        NoAmmoBadge.IsVisible = d.DontSpawnAmmo;
        HeaderRolls.Text = d.ItemRolls.ToString();
        HeaderContainerCount.Text = d.Containers.Count.ToString();

        // Show distribution-level items (common for procedural distributions which have
        // items/junk directly on the distribution rather than in named sub-containers).
        bool hasDirectItems = d.ItemChances.Count > 0 || d.JunkChances.Count > 0;
        DirectItemsPanel.IsVisible = hasDirectItems;
        HeaderDirectItems.IsVisible = hasDirectItems;
        if (hasDirectItems)
        {
            DirectRollsBadge.Text = $"↻ {d.ItemRolls}";
            DirectCountBadge.Text = $"⊞ {d.ItemChances.Count}";
            HeaderDirectItemCount.Text = d.ItemChances.Count.ToString();
            DistItemsControl.Load(d.ItemChances, undoRedo, $"{d.Name}.items");
            JunkTab.IsVisible = d.JunkChances.Count > 0;
            if (d.JunkChances.Count > 0)
                DistJunkControl.Load(d.JunkChances, undoRedo, $"{d.Name}.junk");
        }

        ContainersPanel.Children.Clear();
        foreach (var container in d.Containers)
        {
            var ctrl = new ContainerControl();
            ctrl.Load(container, undoRedo);
            ContainersPanel.Children.Add(ctrl);
        }
    }

    public void ShowEmpty()
    {
        EmptyPanel.IsVisible = true;
        DetailPanel.IsVisible = false;
        ContainersPanel.Children.Clear();
    }
}
