using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DataInput.Data;
using UI.UndoRedo;

namespace UI.Controls;

/// <summary>
/// Shared logic for generating editable Item rows (name + chance TextBoxes + delete button)
/// into a StackPanel. Used by both ContainerControl and ItemListControl to avoid duplication.
/// </summary>
internal static class ItemRowHelper
{
    public static void Populate(
        StackPanel panel,
        List<Item> items,
        UndoRedoStack undoRedo,
        string context,
        ItemParent owner)
    {
        panel.Children.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            var idx = i;

            var nameBox = new TextBox
            {
                Text = items[i].Name,
                FontSize = 11,
                Padding = new Thickness(4, 1),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(nameBox, 0);

            var chanceBox = new TextBox
            {
                Text = FormatChance(items[i].Chance),
                FontSize = 11,
                Padding = new Thickness(4, 1),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 65,
            };
            Grid.SetColumn(chanceBox, 1);

            var deleteBtn = new Button
            {
                Content = "\u00d7",
                FontSize = 11,
                Padding = new Thickness(2, 0),
                MinWidth = 20,
                MinHeight = 0,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Gray,
            };
            Grid.SetColumn(deleteBtn, 2);

            nameBox.LostFocus += (_, _) =>
            {
                var newName = nameBox.Text ?? string.Empty;
                var current = items[idx];
                if (newName == current.Name) return;
                var old = current;
                var updated = new Item(newName, current.Chance);
                undoRedo.Push(new PropertyChangeAction<Item>(
                    $"{context}[{idx}].Name: {old.Name}\u2192{newName}",
                    v => { items[idx] = v; nameBox.Text = v.Name; chanceBox.Text = FormatChance(v.Chance); owner.IsDirty = true; },
                    old, updated));
            };

            chanceBox.LostFocus += (_, _) =>
            {
                if (!double.TryParse(chanceBox.Text, out var newChance))
                {
                    chanceBox.Text = FormatChance(items[idx].Chance);
                    return;
                }
                var current = items[idx];
                if (newChance == current.Chance) return;
                var old = current;
                var updated = new Item(current.Name, newChance);
                undoRedo.Push(new PropertyChangeAction<Item>(
                    $"{context}[{idx}].Chance: {old.Chance}\u2192{newChance}",
                    v => { items[idx] = v; nameBox.Text = v.Name; chanceBox.Text = FormatChance(v.Chance); owner.IsDirty = true; },
                    old, updated));
            };

            deleteBtn.Click += (_, _) =>
            {
                var item = items[idx];
                undoRedo.Push(new ListRemoveAction<Item>(
                    $"{context}: remove '{item.Name}'",
                    items, idx, item,
                    () => { Populate(panel, items, undoRedo, context, owner); owner.IsDirty = true; }));
            };

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,65,20") };
            row.Children.Add(nameBox);
            row.Children.Add(chanceBox);
            row.Children.Add(deleteBtn);
            panel.Children.Add(row);
        }
    }

    public static string FormatChance(double chance) =>
        chance == Math.Floor(chance) ? ((int)chance).ToString() : chance.ToString("G");
}
