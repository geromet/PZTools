using DataInput.Models;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace UI.Controls
{
    public partial class DistributionListControl : UserControl
    {
        private List<Distribution> allDistributions = new List<Distribution>();

        public DistributionListControl()
        {
            InitializeComponent();
        }

        private void FilterButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string filter)
            {
                if (allDistributions.Count == 0)
                {
                    foreach (Distribution distribution in Distributions.ItemsSource)
                    {
                        allDistributions.Add(distribution);
                    }
                }
               
                FilterListBoxItems(filter);
            }
        }

        private void FilterListBoxItems(string filter)
        {
            if (filter == "all")
            {
                Distributions.ItemsSource = allDistributions;
                return;
            }

            var filtered = allDistributions.FindAll(d => d.DistributionType == filter);
            Distributions.ItemsSource = filtered;
        }
        private void Distributions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Distributions.SelectedItem != null)
            {
                var selectedDistribution = Distributions.SelectedItem as Distribution;

                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.Properties.SelectedObject = selectedDistribution;
                }
            }
        }
        
    }
}