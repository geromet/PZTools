using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DataInput.Data;

namespace UI.Controls;

public class ExplorerNode : INotifyPropertyChanged
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
    public Distribution? Distribution { get; }
    public ObservableCollection<ExplorerNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public DistributionType? TypeBadge => Distribution?.Type;
    public string ChildCountText => IsFolder ? $"({Children.Count})" : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private ExplorerNode(bool isFolder, Distribution? distribution)
    {
        IsFolder = isFolder;
        Distribution = distribution;
    }

    public static ExplorerNode CreateFolder(string name)
    {
        return new ExplorerNode(isFolder: true, distribution: null) { Name = name };
    }

    public static ExplorerNode CreateDistribution(Distribution distribution)
    {
        return new ExplorerNode(isFolder: false, distribution: distribution) { Name = distribution.Name };
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? FontWeight.SemiBold : FontWeight.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
