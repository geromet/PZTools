using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace UI.Controls;

/// <summary>Common UI helpers shared by explorer list controls.</summary>
internal static class TreeControlHelper
{
    public static bool ShowMoveFolderConfirmation(string folderName)
    {
        var dialog = new Window
        {
            Title = "Move Folder", Width = 320, Height = 100,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false
        };
        var result = false;
        var yesBtn = new Button { Content = "Yes", Margin = new Thickness(0, 0, 8, 0) };
        var noBtn  = new Button { Content = "No" };
        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click  += (_, _) => dialog.Close();
        yesBtn.IsDefault = true;
        noBtn.IsCancel   = true;

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(10), Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Are you sure you want to move the folder \"{folderName}\"?",
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { yesBtn, noBtn }
                }
            }
        };

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            dialog.ShowDialog(desktop.MainWindow);
        return result;
    }
}
