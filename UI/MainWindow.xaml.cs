using Microsoft.Win32;
using System.Windows;
using DataInput.Models;
using DataInput;

namespace UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dictionary<object, object>[] _distributions = new Dictionary<object, object>[2];
        private string folderPath;
        public DataProcessor dataProcessor = new();


        public MainWindow()
        {
            InitializeComponent();
            Rooms.SelectionChanged += Rooms_SelectionChanged;
            Containers.SelectionChanged += Containers_SelectionChanged;
            try
            {
                dataProcessor.ParseData();
                DataContext = dataProcessor.Distributions.OrderBy(d => d.Name);
            }
            catch
            {
            }
        }
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Folder",
                CheckFileExists = false,
                FileName = "Select Folder",
                Filter = "Folders|*.",
                InitialDirectory = folderPath // Set initial directory if needed
            };

            if (openFileDialog.ShowDialog() == true)
            {
                folderPath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
                dataProcessor.ParseData();
            }
           
        }
        private void Rooms_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Rooms.SelectedItem is Distribution selectedDistribution)
            {
                List<object> list = new List<object>();
                list.Add(selectedDistribution.ToFullString());
                list.AddRange(selectedDistribution.Containers);
                Containers.ItemsSource = list;
                Items.ItemsSource = null; // Clear the third ListBox when a new room is selected
            }
        }

        private void Containers_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Containers.SelectedItem is Container selectedContainer)
            {
                List<object> list = new List<object>();
                list.Add(selectedContainer.ToFullString());
                Items.ItemsSource = list;
            }
        }






    }
}