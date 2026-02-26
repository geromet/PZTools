using Microsoft.Win32;
using System.Windows;
using DataOld.Models;
using DataOld;
using OldUI.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace OldUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dictionary<object, object>[] _distributions = new Dictionary<object, object>[2];
        private string folderPath;
        public DataProcessor dataProcessor = new();
        private LayoutAnchorable _distributionList;


        public MainWindow()
        {
            InitializeComponent();
            try
            {
                dataProcessor.ParseData();
                DataContext = dataProcessor;
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

        private void Distributions_Click(object sender, RoutedEventArgs e)
        {
            distributionList.IsVisible = !distributionList.IsVisible;


        }
        private void Errors_Click(object sender, RoutedEventArgs e) 
        {
            errorList.IsVisible = !errorList.IsVisible;
        }
        private void Properties_Click(object sender, RoutedEventArgs e) 
        {
            properties.IsVisible = !properties.IsVisible;
        }
        private void Code_Click(object sender, RoutedEventArgs e) 
        {
           
        }





    }
}