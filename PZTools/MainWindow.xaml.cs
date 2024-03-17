using System.Text;
using System.Windows;
using Data;
using Data.Models.Items.Distributions;
using Microsoft.Win32;

namespace PZTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string folderPath;
        DistributionContext dbContext = new();
       

        public MainWindow()
        {
            InitializeComponent();
            
            Rooms.SelectionChanged += Rooms_SelectionChanged;
            Containers.SelectionChanged += Containers_SelectionChanged;
        }
        private void InitializeDistributions()
        {
            foreach (var item in dbContext.Distributions)
            {
                Rooms.Items.Add(item.Name);
            }
        }
        private void CreateDB_Click(object sender, RoutedEventArgs e)
        {
            Data.DataBase.CreateDB();
            
        }
        private void LoadDB_Click(Object sender, RoutedEventArgs e)
        {
            InitializeDistributions();
        }
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Folder",
                CheckFileExists = false,
                FileName = "Select Folder",
                Filter = "Folders|*.",
                InitialDirectory = folderPath
            };

            if (openFileDialog.ShowDialog() == true)
            {
                folderPath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
            }
        }
        private void Rooms_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Containers.Items.Clear();
            Items.Items.Clear();
            var selectedItem = Rooms.SelectedItem;
            Distribution selectedDistribution = dbContext.Distributions.FirstOrDefault(x => x.Name==selectedItem.ToString());
            if (selectedDistribution == null) 
            { 
                return; }
            if(selectedDistribution.IsShop==true) 
            {
                Containers.Items.Add("IsShop");
            }
            if (selectedDistribution.DontSpawnAmmo == true)
            {
                Containers.Items.Add("DontSpawnAmmo");
            }
            if(selectedDistribution.MaxMap != null) 
            {
                Containers.Items.Add("MaxMap = "+ selectedDistribution.MaxMap);
            }
            if (selectedDistribution.StashChance != null)
            {
                Containers.Items.Add("StashChance = " + selectedDistribution.StashChance);
            }
            if (selectedDistribution.FillRand != null)
            {
                Containers.Items.Add("FillRand = " + selectedDistribution.FillRand);
            }
            if (selectedDistribution.ItemRolls != null)
            {
                Containers.Items.Add("ItemRolls = " + selectedDistribution.ItemRolls);
            }
            if (selectedDistribution.JunkRolls != null)
            {
                Containers.Items.Add("JunkRolls = " + selectedDistribution.JunkRolls);
            }
            if (selectedDistribution.Containers != null)
            {
                foreach(Container container in dbContext.Containers.Where(c => c.DistributionId == selectedDistribution.Id))
                {
                    Containers.Items.Add(container.Name);
                }
            }
            if (selectedDistribution.ItemChances != null)
            {
                foreach (Item item in dbContext.Items.Where(i => i.DistributionId == selectedDistribution.Id))
                {
                    Containers.Items.Add(item.Name + " " + item.Chance);
                }
            }

            
        }
        private void Containers_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Items.Items.Clear();
            var selectedItem = Containers.SelectedItem;
            if (selectedItem == null) { return; }
            Container? selectedContainer = dbContext.Containers.FirstOrDefault(x => x.Name == selectedItem.ToString());
            if(selectedContainer == null)
            {
                return;
            }
            if (selectedContainer.Procedural == true)
            {
                Items.Items.Add("Procedural");
            }
            if (selectedContainer.DontSpawnAmmo == true)
            {
                Items.Items.Add("DontSpawnAmmo");
            }
            if (selectedContainer.FillRand != null)
            {
                Items.Items.Add("FillRand = " + selectedContainer.FillRand);
            }
            if (selectedContainer.ItemRolls != null)
            {
                Items.Items.Add("ItemRolls = " + selectedContainer.ItemRolls);
            }
            if (selectedContainer.JunkRolls != null)
            {
                Items.Items.Add("JunkRolls = " + selectedContainer.JunkRolls);
            }
            if (selectedContainer.ProcListEntries != null)
            {
                foreach (ProcListEntry procListEntry in dbContext.ProcListEntries.Where(p => p.ContainerId == selectedContainer.Id))
                {
                    Items.Items.Add(procListEntry.Name);
                    Items.Items.Add(procListEntry.Min);
                    Items.Items.Add(procListEntry.Max);
                    Items.Items.Add(procListEntry.WheightChance);
                    Items.Items.Add(procListEntry.ForceForZones);
                    Items.Items.Add(procListEntry.ForceForTiles);
                    Items.Items.Add(procListEntry.ForceForRooms);
                    Items.Items.Add(procListEntry.ForceForItems);
                    //Todo:Add The ItemChances from the procListEntry. Needs parsing of proceduralDistributions, then seed Db.
                }
            }
            if (selectedContainer.ItemChances != null)
            {
                foreach (Item item in dbContext.Items.Where(i => i.ContainerId == selectedContainer.Id))
                {
                    Items.Items.Add(item.Name + " " + item.Chance);
                }
            }
        }

    }
}