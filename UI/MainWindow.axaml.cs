using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using UI.ViewModels;

namespace UI;

/// <summary>
/// ReactiveWindow binds the window's DataContext strongly to MainViewModel
/// and wires keyboard shortcuts (Ctrl+Z / Ctrl+Y) to the VM's undo/redo commands.
/// The folder picker must live here because StorageProvider requires a Window reference.
/// </summary>
public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        // Keyboard shortcuts — handled at window level so they work everywhere
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;

        if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
        {
            ViewModel.UndoCommand.Execute().Subscribe();
            e.Handled = true;
        }
        else if ((e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control)
              || (e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)))
        {
            ViewModel.RedoCommand.Execute().Subscribe();
            e.Handled = true;
        }
    }

    private async void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select game or mod folder" });

        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (path is null || ViewModel is null) return;

        ViewModel.ParseCommand.Execute(path).Subscribe();
    }
}
