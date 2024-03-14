using PZViewer;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NLua;
using Microsoft.Win32;

namespace PZTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dictionary<object, object>[] _distributions = new Dictionary<object, object>[2];
        private string folderPath;

        public MainWindow()
        {
            InitializeComponent();
            Rooms.SelectionChanged += Rooms_SelectionChanged;
            Containers.SelectionChanged += Containers_SelectionChanged;
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
                // Call your method that needs the folderPath, e.g., GetDistributions(folderPath);
                GetDistributions(folderPath);
            }
        }
        private void GetDistributions(string folderPath)
        {
            _distributions = LuaParser.GetDistributions(folderPath);
            foreach (var key in _distributions[0].Keys)
            {
                Rooms.Items.Add(key);
            }
        }
        private void Rooms_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Containers.Items.Clear();
            Items.Items.Clear();
            var selectedKey = Rooms.SelectedItem;
            if (selectedKey != null && _distributions[0].ContainsKey(selectedKey))
            {
                var selectedDict = (Dictionary<object, object>)_distributions[0][selectedKey];
                foreach (var kvp in selectedDict)
                {
                    var key = kvp.Key.ToString();
                    Containers.Items.Add(key);
                }
            }
        }
        private void Containers_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selectedKey = Containers.SelectedItem?.ToString();
            var topLevelKey = Rooms.SelectedItem?.ToString();

            if (selectedKey != null && topLevelKey != null)
            {
                var topLevelDict = (Dictionary<object, object>)_distributions[0][topLevelKey];

                if (topLevelDict.ContainsKey(selectedKey))
                {
                    var selectedValue = topLevelDict[selectedKey];

                    if (selectedValue is Dictionary<object, object>)
                    {
                        Items.Items.Clear();
                        var nestedDict = (Dictionary<object, object>)selectedValue;
                        foreach (var kvp in nestedDict)
                        {
                            Items.Items.Add($"{kvp.Key} = {kvp.Value}");
                        }
                    }
                    else if (selectedValue is List<object>)
                    {
                        Items.Items.Clear();
                        var procList = (List<object>)selectedValue;
                        foreach (var item in procList)
                        {
                            if (item is Dictionary<object, object>)
                            {
                                var nestedDict = (Dictionary<object, object>)item;
                                var itemStr = string.Join(", ", nestedDict.Select(kvp => $"{kvp.Key} = {kvp.Value}"));
                                Items.Items.Add(itemStr);
                            }
                        }
                    }
                    if (selectedValue is Dictionary<object, object> selectedDict && selectedDict.ContainsKey("procList"))
                    {
                        selectedDict.TryGetValue("procList", out object v);

                        foreach (var item in (Dictionary<object, object>)v)
                            {
                                Dictionary<object, object> itemValue = (Dictionary<object, object>)item.Value;
                                foreach (var value in itemValue.Values)
                                {
                                if (_distributions[1].ContainsKey(value))
                                {
                                    var itemValues = (Dictionary<object, object>)_distributions[1][value];
                                    StringBuilder itemsStringBuilder = new StringBuilder();
                                    itemsStringBuilder.Append("items = {\n");
                                    foreach (var kvp in itemValues)
                                    {
                                        if (kvp.Value is Dictionary<object, object>)
                                        {
                                            var nestedDict = (Dictionary<object, object>)kvp.Value;
                                            foreach (var nestedKvp in nestedDict)
                                            {
                                                itemsStringBuilder.Append($"    \"{nestedKvp.Key}\", {nestedKvp.Value},\n");
                                            }
                                        }
                                    }

                                    itemsStringBuilder.Append("},");
                                    Items.Items.Add(itemsStringBuilder.ToString());
                                }

                            }

                        }
                        
                    }
                }
            }
        }






    }
}