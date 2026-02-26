using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DataInput.Errors;

namespace UI.Controls;

public partial class ErrorListControl : UserControl
{
    private record ErrorDisplayRow(ParseError Source, string DisplayFile)
    {
        public string Type    => Source.IsFatal ? "Error" : "Warning";
        public string Code    => Source.Code.ToString();
        public string Context => Source.Context?.Trim() ?? string.Empty;
        public string Message => Source.Message.Trim();
    }

    private List<ErrorDisplayRow> _all = [];

    public ErrorListControl()
    {
        InitializeComponent();
        ErrorGrid.KeyDown += ErrorGrid_KeyDown;
    }

    public void Load(IReadOnlyList<ParseError> errors, string rootFolder = "")
    {
        _all = errors
            .Select(e => new ErrorDisplayRow(e, MakeRelative(e.SourceFile, rootFolder)))
            .ToList();
        UpdateCounts();
        ApplyFilter();
    }

    private static string MakeRelative(string path, string root)
    {
        if (string.IsNullOrEmpty(root))
            return Path.GetFileName(path);
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return path[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(path);
    }

    private void ApplyFilter()
    {
        bool showErrors   = ShowErrorsBtn.IsChecked == true;
        bool showWarnings = ShowWarningsBtn.IsChecked == true;

        var filtered = _all.Where(r => r.Source.IsFatal ? showErrors : showWarnings).ToList();
        ErrorGrid.ItemsSource = filtered;
    }

    private void UpdateCounts()
    {
        int errorCount = _all.Count(r => r.Source.IsFatal);
        int warnCount  = _all.Count - errorCount;
        ErrorCountText.Text   = $"{errorCount} errors";
        WarningCountText.Text = $"{warnCount} warnings";
    }

    private void ShowErrors_Click(object? sender, RoutedEventArgs e)   => ApplyFilter();
    private void ShowWarnings_Click(object? sender, RoutedEventArgs e) => ApplyFilter();

    private void ErrorGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control)
        {
            CopySelected();
            e.Handled = true;
        }
    }

    private void CopyRow_Click(object? sender, RoutedEventArgs e) => CopySelected();

    private void CopyAll_Click(object? sender, RoutedEventArgs e)
    {
        if (ErrorGrid.ItemsSource is not IEnumerable<ErrorDisplayRow> visible) return;
        SetClipboard(FormatRows(visible.Select(r => r.Source)));
    }

    private void CopySelected()
    {
        var selected = ErrorGrid.SelectedItems.OfType<ErrorDisplayRow>().ToList();
        if (selected.Count == 0) return;
        SetClipboard(FormatRows(selected.Select(r => r.Source)));
    }

    private static string FormatRows(IEnumerable<ParseError> errors)
    {
        var sb = new StringBuilder();
        foreach (var err in errors)
        {
            var sev  = err.IsFatal ? "ERROR" : "WARN ";
            var file = err.SourceFile;
            var ctx  = err.Context ?? string.Empty;
            sb.AppendLine($"[{sev}] {err.Code} | {file} | {ctx} | {err.Message}");
        }
        return sb.ToString();
    }

    private void SetClipboard(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        clipboard?.SetTextAsync(text);
    }
}
