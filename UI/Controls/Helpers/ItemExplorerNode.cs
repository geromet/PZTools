using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UI.Controls;

public class ItemExplorerNode : IExplorerNode, INotifyPropertyChanged
{
    private string _name = "";
    private bool _isExpanded = true;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool IsFolder { get; }
    public bool IsNotFolder => !IsFolder;
    public string? ItemName { get; }
    public ObservableCollection<ItemExplorerNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public IEnumerable<IExplorerNode> ChildrenBase => Children;

    public string ChildCountText => IsFolder ? $"({Children.Count})" : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private ItemExplorerNode(bool isFolder, string? itemName)
    {
        IsFolder = isFolder;
        ItemName = itemName;
    }

    public static ItemExplorerNode CreateFolder(string name)
    {
        return new ItemExplorerNode(isFolder: true, itemName: null) { Name = name };
    }

    public static ItemExplorerNode CreateItem(string itemName)
    {
        return new ItemExplorerNode(isFolder: false, itemName: itemName) { Name = itemName };
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
