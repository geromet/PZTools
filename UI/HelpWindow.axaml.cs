using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace UI;

public partial class HelpWindow : Window
{
    private static readonly IBrush TextPrimary = SolidColorBrush.Parse("#E4E4EA");
    private static readonly IBrush TextSecondary = SolidColorBrush.Parse("#8A8A98");
    private static readonly IBrush TextMuted = SolidColorBrush.Parse("#5A5A6A");
    private static readonly IBrush Accent = SolidColorBrush.Parse("#4A7FA8");
    private static readonly IBrush BgElevated = SolidColorBrush.Parse("#26262E");
    private static readonly IBrush BgInput = SolidColorBrush.Parse("#2C2C36");
    private static readonly IBrush BorderSubtle = SolidColorBrush.Parse("#2E2E3A");
    private static readonly IBrush AccentMuted = SolidColorBrush.Parse("#2A4F6A");
    private static readonly IBrush IncludeBg = SolidColorBrush.Parse("#2A4F6A");
    private static readonly IBrush IncludeFg = SolidColorBrush.Parse("#5A93BF");
    private static readonly IBrush ExcludeBg = SolidColorBrush.Parse("#3A2020");
    private static readonly IBrush ExcludeFg = SolidColorBrush.Parse("#C84B4B");

    public HelpWindow()
    {
        InitializeComponent();
        TopicList.SelectedIndex = 0;
    }

    private void TopicList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TopicList.SelectedItem is not ListBoxItem item) return;
        var tag = item.Tag as string ?? "welcome";
        ShowTopic(tag);
    }

    private void ShowTopic(string topic)
    {
        ContentPanel.Children.Clear();
        var nodes = GetTopic(topic);
        foreach (var node in nodes)
            ContentPanel.Children.Add(node);
    }

    // ── Topic content builders ──

    private List<Control> GetTopic(string topic) => topic switch
    {
        "welcome" => BuildWelcome(),
        "getting-started" => BuildGettingStarted(),
        "dist-list" => BuildDistList(),
        "filters" => BuildFilters(),
        "detail-view" => BuildDetailView(),
        "containers" => BuildContainers(),
        "editing" => BuildEditing(),
        "saving" => BuildSaving(),
        "error-list" => BuildErrorList(),
        "shortcuts" => BuildShortcuts(),
        _ => BuildWelcome(),
    };

    // ── Welcome ──

    private List<Control> BuildWelcome()
    {
        var c = new List<Control>();
        c.Add(H1("PZTools"));
        c.Add(Para("A desktop tool for viewing and editing Project Zomboid loot distribution tables."));
        c.Add(Spacer());
        c.Add(Para("PZTools parses the game's Distributions.lua and ProceduralDistributions.lua files into a browsable, editable interface. You can inspect every room, container, item list, and procedural reference \u2014 then save your changes back to disk."));
        c.Add(Spacer());
        c.Add(H2("Layout"));
        c.Add(Para("The window is divided into four panels:"));
        c.Add(Bullet("Distribution Explorer", "Left panel. Browse, search, and filter distributions. Organize them into collapsible folders with drag-and-drop."));
        c.Add(Bullet("Detail View", "Center panel. Inspect and edit the selected distribution and its containers."));
        c.Add(Bullet("Properties", "Right panel. Shows a read-only detail view when you click a procedural reference link."));
        c.Add(Bullet("Error List", "Bottom panel. Displays parse warnings and errors from the loaded files."));
        c.Add(Spacer());
        c.Add(Hint("All panels can be closed with their \u00d7 button or toggled from the View menu, and resized with splitters."));
        return c;
    }

    // ── Getting Started ──

    private List<Control> BuildGettingStarted()
    {
        var c = new List<Control>();
        c.Add(H1("Getting Started"));
        c.Add(Spacer());
        c.Add(H2("Opening a Folder"));
        c.Add(Step("1", "Click Open Folder in the toolbar."));
        c.Add(Step("2", "Navigate to your Project Zomboid installation or a mod folder."));
        c.Add(Step("3", "PZTools looks for Lua files at the fixed paths:\nmedia/lua/server/Items/Distributions.lua\nmedia/lua/server/Items/ProceduralDistributions.lua"));
        c.Add(Step("4", "Distributions appear in the left panel. Parsing issues appear in the error list."));
        c.Add(Spacer());
        c.Add(H2("Saving Changes"));
        c.Add(Para("After editing, the title bar shows an asterisk (*) to indicate unsaved changes. Click Save or press Ctrl+S to write modified files back to disk. Only files with changes are rewritten."));
        c.Add(Spacer());
        c.Add(H2("Reloading"));
        c.Add(Para("Click Reload to re-parse the current folder from disk. You'll be warned if you have unsaved changes."));
        return c;
    }

    // ── Distribution List ──

    private List<Control> BuildDistList()
    {
        var c = new List<Control>();
        c.Add(H1("Distribution Explorer"));
        c.Add(Para("The left panel shows all parsed distributions in a tree view. Distributions are sorted alphabetically. A count badge shows filtered/total (e.g. \"42 / 180\")."));
        c.Add(Spacer());
        c.Add(H2("Folders"));
        c.Add(Para("You can organize distributions into collapsible folders for easier navigation. Folders are UI-only \u2014 they don't change the underlying data files."));
        c.Add(Bullet("Create folder", "Right-click \u2192 New Folder. Type a name and press Enter."));
        c.Add(Bullet("Create subfolder", "Right-click a folder \u2192 New Subfolder. Folders can be nested to any depth."));
        c.Add(Bullet("Rename folder", "Right-click a folder \u2192 Rename Folder. Edit the name inline and press Enter."));
        c.Add(Bullet("Delete folder", "Right-click a folder \u2192 Delete Folder. Distributions inside return to the root level."));
        c.Add(Para("Folders and their contents are saved automatically and persist across sessions."));
        c.Add(Spacer());
        c.Add(H2("Drag and Drop"));
        c.Add(Para("Drag distributions or folders to reorganize them:"));
        c.Add(Bullet("Move to folder", "Drag a distribution onto a folder to move it inside."));
        c.Add(Bullet("Move to root", "Drag a distribution outside any folder to return it to the root level."));
        c.Add(Bullet("Move folders", "Drag a folder onto another folder to nest it. A folder cannot be dropped into itself or its own descendants."));
        c.Add(Bullet("Multi-select", "Select multiple distributions (Ctrl+click or Shift+click), then drag them all at once."));
        c.Add(Spacer());
        c.Add(H2("Context Menu"));
        c.Add(Para("Right-click any distribution or folder for additional actions:"));
        c.Add(Bullet("Move to Folder", "Submenu listing all available folders. Select one to move the selected distributions there."));
        c.Add(Bullet("Remove from Folder", "Move selected distributions back to the root level."));
        c.Add(Spacer());
        c.Add(H2("Search"));
        c.Add(Para("Type in the search box to filter by name. Supports regular expressions \u2014 if the pattern is invalid it falls back to a plain substring match. Folders with no matching children are hidden automatically."));
        c.Add(Spacer());
        c.Add(H2("Type Filter"));
        c.Add(Para("The type pills along the top select a single distribution type:"));
        c.Add(Bullet("All", "Show every distribution (default)."));
        c.Add(Bullet("Rooms", "Room-level distributions (e.g. kitchen, bathroom)."));
        c.Add(Bullet("Caches", "Cache-type distributions."));
        c.Add(Bullet("Professions", "Profession starter kits."));
        c.Add(Bullet("Procedural", "Procedural distribution tables."));
        c.Add(Bullet("Items", "Item/bag-level distributions."));
        c.Add(Para("Click an active pill again to deselect it and show all types."));
        c.Add(Spacer());
        c.Add(H2("Content Filters"));
        c.Add(Para("These tri-state pills filter distributions by what their containers contain. See the Filters topic for details on tri-state behavior."));
        c.Add(Spacer());
        c.Add(H2("Structure Filters"));
        c.Add(Bullet("No Content", "Distributions with zero containers, items, and junk."));
        c.Add(Bullet("Invalid", "Containers that are empty, have rolls but no items/junk, or have the procedural flag but no proc list entries."));
        return c;
    }

    // ── Filters ──

    private List<Control> BuildFilters()
    {
        var c = new List<Control>();
        c.Add(H1("Filters"));
        c.Add(Para("PZTools uses tri-state filter pills throughout the UI. Understanding how they work is key to navigating large distribution sets."));
        c.Add(Spacer());
        c.Add(H2("Tri-State Behavior"));
        c.Add(Para("Each filter pill cycles through three states:"));
        c.Add(Spacer());
        c.Add(FilterPillDemo("Ignored", "Default. Filter is inactive \u2014 everything passes.", BgInput, TextSecondary));
        c.Add(FilterPillDemo("Include", "Left-click to activate. Only show items that have this property.", IncludeBg, IncludeFg));
        c.Add(FilterPillDemo("Exclude", "Right-click to activate. Hide items that have this property.", ExcludeBg, ExcludeFg));
        c.Add(Spacer());
        c.Add(Para("Left-click cycles: Ignored \u2192 Include \u2192 Ignored"));
        c.Add(Para("Right-click cycles: Ignored \u2192 Exclude \u2192 Ignored"));
        c.Add(Para("Multiple filters are combined with AND logic \u2014 an item must match every active filter to be shown."));
        c.Add(Spacer());
        c.Add(H2("Distribution List Filters"));
        c.Add(Para("Content filter pills (Proc List, Rolls, Items, Junk, Procedural) check whether at least one container in the distribution matches. Distribution-level items are also checked as a virtual container."));
        c.Add(Spacer());
        c.Add(H2("Container Filters"));
        c.Add(Para("The detail view toolbar has its own set of tri-state pills that filter which containers are visible within the selected distribution."));
        c.Add(Spacer());
        c.Add(H2("Auto Filter"));
        c.Add(Para("The Auto Filter button in the detail toolbar mirrors the content filters from the distribution list onto the container filters. This is useful when you've already narrowed the list and want the same criteria applied to containers. Any manual change to container filters disables auto-filter."));
        return c;
    }

    // ── Detail View ──

    private List<Control> BuildDetailView()
    {
        var c = new List<Control>();
        c.Add(H1("Detail View"));
        c.Add(Para("The center panel shows the selected distribution's header, toolbar, and container list. Close it with the \u00d7 button or toggle it from the View menu \u2014 hiding the detail view speeds up folder organization since distributions aren't loaded on every click."));
        c.Add(Spacer());
        c.Add(H2("Distribution Header"));
        c.Add(Para("Shows the distribution name, type badge, and editable settings:"));
        c.Add(Bullet("Rolls", "How many times the game rolls on this distribution's item table."));
        c.Add(Bullet("Shop", "Whether this distribution is a shop inventory."));
        c.Add(Bullet("No Ammo", "Prevents ammo from spawning."));
        c.Add(Bullet("MaxMap", "Maximum map index (only shown if set in source)."));
        c.Add(Bullet("Stash%", "Stash chance percentage (only shown if set in source)."));
        c.Add(Spacer());
        c.Add(H2("Toolbar"));
        c.Add(Bullet("Expand All / Collapse All", "Toggle all container expanders at once."));
        c.Add(Bullet("Show Empty", "When active, displays Items, Junk, and Proc List columns on every container even if they are empty. Useful for preparing to add content."));
        c.Add(Bullet("Auto Filter", "Mirrors the distribution list's content filters to the container filter pills."));
        c.Add(Bullet("Clear", "Resets all container filters and disables auto-filter."));
        c.Add(Spacer());
        c.Add(H2("Distribution Items"));
        c.Add(Para("Some distributions (mainly procedural) have items directly on the distribution rather than inside named containers. These appear in a collapsible section at the top with Items and Junk tabs."));
        c.Add(Spacer());
        c.Add(H2("Properties Panel"));
        c.Add(Para("The right panel shows a read-only detail view. Click a procedural reference link (the blue distribution name in a Proc List entry) to load that distribution into the Properties panel for side-by-side comparison."));
        return c;
    }

    // ── Containers ──

    private List<Control> BuildContainers()
    {
        var c = new List<Control>();
        c.Add(H1("Containers"));
        c.Add(Para("Each container represents a shelf, counter, or named sub-section within a distribution. Containers are collapsible \u2014 click the header to expand."));
        c.Add(Spacer());
        c.Add(H2("Header Badges"));
        c.Add(Bullet("\u21bb N", "Item roll count."));
        c.Add(Bullet("\u229e N", "Unique item count."));
        c.Add(Bullet("PROC", "Shown when the Procedural flag is set (yellow badge)."));
        c.Add(Spacer());
        c.Add(H2("Column Layout"));
        c.Add(Para("Each container's content is split into resizable columns:"));
        c.Add(Bullet("Settings", "Editable properties: Rolls, Junk Rolls, Fill Rand, Procedural, No Ammo."));
        c.Add(Bullet("Items", "The item table \u2014 each row has a name and spawn chance."));
        c.Add(Bullet("Junk", "Same as Items but for junk rolls. Only visible when the container has junk items (or Show Empty is on)."));
        c.Add(Bullet("Proc Lists", "Procedural distribution references with min/max, weight, and optional force fields. Only visible when the container has proc entries (or Show Empty is on)."));
        c.Add(Spacer());
        c.Add(H2("Resizable Columns"));
        c.Add(Para("Drag the splitters between columns to resize. All containers share the same column proportions \u2014 drag one splitter and every container updates. Proportions are remembered when switching between distributions."));
        return c;
    }

    // ── Editing ──

    private List<Control> BuildEditing()
    {
        var c = new List<Control>();
        c.Add(H1("Editing"));
        c.Add(Para("All numeric and text fields are editable. Changes are recorded for undo/redo."));
        c.Add(Spacer());
        c.Add(H2("How Edits Work"));
        c.Add(Step("1", "Click a field and type a new value."));
        c.Add(Step("2", "Press Tab or click elsewhere to confirm (changes apply on focus loss)."));
        c.Add(Step("3", "Invalid input (e.g. letters in a number field) reverts to the previous value."));
        c.Add(Para("Each edit is pushed onto the undo stack immediately."));
        c.Add(Spacer());
        c.Add(H2("Undo / Redo"));
        c.Add(Para("Use the toolbar buttons or keyboard shortcuts to step through your edit history. The undo/redo stack is cleared when you select a different distribution or open a new folder."));
        c.Add(Bullet("Undo", "Ctrl+Z  \u2014  Reverts the last change."));
        c.Add(Bullet("Redo", "Ctrl+Y or Ctrl+Shift+Z  \u2014  Re-applies the last undone change."));
        c.Add(Para("Hover over the Undo/Redo buttons to see a description of the next action."));
        c.Add(Spacer());
        c.Add(H2("Item Lists"));
        c.Add(Bullet("Add Item", "Click the + Add Item button below any item list to append a new entry."));
        c.Add(Bullet("Delete Item", "Click the \u00d7 button on the right side of any item row to remove it."));
        c.Add(Para("Both add and delete are undoable."));
        c.Add(Spacer());
        c.Add(H2("Proc List Fields"));
        c.Add(Bullet("Min / Max", "The range of times this procedural reference can appear."));
        c.Add(Bullet("Weight", "Relative weight for random selection."));
        c.Add(Bullet("Force Tiles / Rooms / Items", "Constraint strings (only shown if set in source)."));
        c.Add(Para("Click the blue distribution name to open it in the Properties panel."));
        c.Add(Spacer());
        c.Add(H2("Dirty State"));
        c.Add(Para("When you have unsaved changes the title bar shows an asterisk (*) and the Save button becomes enabled. Opening a new folder or reloading will warn you before discarding changes."));
        return c;
    }

    // ── Saving ──

    private List<Control> BuildSaving()
    {
        var c = new List<Control>();
        c.Add(H1("Saving"));
        c.Add(Para("PZTools writes your changes back to the original Lua files. The saved output is functionally identical to the original, but there are minor formatting differences compared to the vanilla files:"));
        c.Add(Spacer());
        c.Add(H2("Known Formatting Differences"));
        c.Add(Bullet("Inline comments", "Vanilla sometimes has comments to the right of code (e.g. -- comment after a line). PZTools preserves these comments but places them on the line above instead."));
        c.Add(Bullet("Name sorting", "Distributions are sorted alphabetically on save. The vanilla files are mostly alphabetical but have occasional inconsistencies in ordering."));
        c.Add(Bullet("Property ordering", "Properties within a distribution or container (rolls, items, junk, etc.) are written in a consistent order. Vanilla files are not always consistent about property ordering."));
        c.Add(Bullet("Indentation", "PZTools uses consistent tab indentation throughout. Vanilla files occasionally have incorrect or inconsistent indentation on some properties."));
        c.Add(Bullet("Trailing whitespace", "Vanilla files sometimes have trailing spaces at the end of lines. PZTools does not write trailing whitespace."));
        c.Add(Spacer());
        c.Add(H2("Empty Elements"));
        c.Add(Para("PZTools currently writes empty elements (e.g. items = {}, rolls = 0) on containers to ensure broad compatibility with the game's Java item picker. This adds slightly to the file size compared to vanilla. A future update will minimize unnecessary empty elements."));
        c.Add(Spacer());
        c.Add(Hint("These differences are cosmetic only. The game reads the saved files identically to the originals. Your loot tables will work exactly as intended."));
        return c;
    }

    // ── Error List ──

    private List<Control> BuildErrorList()
    {
        var c = new List<Control>();
        c.Add(H1("Error List"));
        c.Add(Para("The bottom panel displays parsing issues found when loading distribution files."));
        c.Add(Spacer());
        c.Add(H2("Columns"));
        c.Add(Bullet("Type", "Error or Warning severity level."));
        c.Add(Bullet("Code", "Machine-readable error code."));
        c.Add(Bullet("Context", "Which distribution or container the issue was found in."));
        c.Add(Bullet("Message", "Human-readable description of the problem."));
        c.Add(Bullet("File", "Relative path to the source Lua file."));
        c.Add(Spacer());
        c.Add(H2("Filtering"));
        c.Add(Para("Use the toggle buttons in the error list header to show/hide errors and warnings independently."));
        c.Add(Spacer());
        c.Add(H2("Copying"));
        c.Add(Bullet("Ctrl+C", "Copy selected rows to clipboard."));
        c.Add(Bullet("Right-click", "Context menu with Copy Selected and Copy All options."));
        return c;
    }

    // ── Keyboard Shortcuts ──

    private List<Control> BuildShortcuts()
    {
        var c = new List<Control>();
        c.Add(H1("Keyboard Shortcuts"));
        c.Add(Spacer());
        c.Add(Shortcut("Ctrl+S", "Save modified files to disk."));
        c.Add(Shortcut("Ctrl+Z", "Undo the last edit."));
        c.Add(Shortcut("Ctrl+Y", "Redo the last undone edit."));
        c.Add(Shortcut("Ctrl+Shift+Z", "Redo (alternative)."));
        c.Add(Shortcut("Ctrl+W", "Close the active tab."));
        c.Add(Shortcut("Ctrl+C", "Copy selected error list rows."));
        c.Add(Spacer());
        c.Add(H2("Mouse"));
        c.Add(Shortcut("Double-click dist.", "Open distribution in a new tab (or activate existing tab)."));
        c.Add(Shortcut("Left-click pill", "Cycle: Ignored \u2192 Include \u2192 Ignored"));
        c.Add(Shortcut("Right-click pill", "Cycle: Ignored \u2192 Exclude \u2192 Ignored"));
        c.Add(Shortcut("Drag splitter", "Resize adjacent panels or columns."));
        c.Add(Shortcut("Drag distribution", "Move it into or out of a folder in the explorer."));
        c.Add(Shortcut("Drag folder", "Nest it inside another folder."));
        c.Add(Shortcut("Right-click explorer", "Open the context menu for folder management."));
        return c;
    }

    // ── Rendering helpers ──

    private static TextBlock H1(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeight.SemiBold,
        Foreground = TextPrimary,
        Margin = new Thickness(0, 0, 0, 4),
    };

    private static TextBlock H2(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = FontWeight.SemiBold,
        Foreground = TextPrimary,
        Margin = new Thickness(0, 12, 0, 4),
    };

    private static TextBlock Para(string text) => new()
    {
        Text = text,
        FontSize = 12.5,
        Foreground = TextSecondary,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = 20,
        Margin = new Thickness(0, 2, 0, 2),
    };

    private static Control Spacer() => new Border { Height = 8 };

    private static Control Bullet(string label, string description)
    {
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Margin = new Thickness(8, 3, 0, 3),
        };
        panel.Children.Add(new TextBlock
        {
            Text = "\u2022  ",
            FontSize = 12.5,
            Foreground = TextMuted,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        });
        var text = new TextBlock
        {
            FontSize = 12.5,
            Foreground = TextSecondary,
            TextWrapping = TextWrapping.Wrap,
        };
        text.Inlines?.Add(new Avalonia.Controls.Documents.Run(label) { FontWeight = FontWeight.SemiBold, Foreground = TextPrimary });
        text.Inlines?.Add(new Avalonia.Controls.Documents.Run($"  \u2014  {description}"));
        panel.Children.Add(text);
        return panel;
    }

    private static Control Step(string number, string text)
    {
        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("28,*"),
            Margin = new Thickness(4, 4, 0, 4),
        };
        var badge = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = AccentMuted,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            Child = new TextBlock
            {
                Text = number,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = IncludeFg,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(badge, 0);
        grid.Children.Add(badge);

        var para = new TextBlock
        {
            Text = text,
            FontSize = 12.5,
            Foreground = TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        Grid.SetColumn(para, 1);
        grid.Children.Add(para);
        return grid;
    }

    private static Control FilterPillDemo(string label, string description, IBrush bg, IBrush fg)
    {
        var row = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Margin = new Thickness(8, 4, 0, 4),
            Spacing = 10,
        };
        var pill = new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = fg,
            },
        };
        row.Children.Add(pill);
        row.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12.5,
            Foreground = TextSecondary,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });
        return row;
    }

    private static Control Shortcut(string key, string description)
    {
        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("140,*"),
            Margin = new Thickness(0, 3, 0, 3),
        };
        var keyBorder = new Border
        {
            Background = BgElevated,
            BorderBrush = BorderSubtle,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = key,
                FontSize = 11.5,
                FontWeight = FontWeight.Medium,
                Foreground = TextPrimary,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            },
        };
        Grid.SetColumn(keyBorder, 0);
        grid.Children.Add(keyBorder);

        var desc = new TextBlock
        {
            Text = description,
            FontSize = 12.5,
            Foreground = TextSecondary,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(desc, 1);
        grid.Children.Add(desc);
        return grid;
    }

    private static Control Hint(string text)
    {
        return new Border
        {
            Background = BgElevated,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 4, 0, 4),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyle.Italic,
                LineHeight = 18,
            },
        };
    }
}
