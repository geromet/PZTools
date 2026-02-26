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
/// Shared logic for generating editable Item rows (name + chance TextBoxes) into a StackPanel.
/// Used by both ContainerControl and ItemListControl to avoid duplication.
/// </summary>
internal static class ItemRowHelper
{
    public static void Populate(
        StackPanel panel,
        List<Item> items,
        UndoRedoStack undoRedo,
        string context)
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

            nameBox.LostFocus += (_, _) =>
            {
                var newName = nameBox.Text ?? string.Empty;
                var current = items[idx];
                if (newName == current.Name) return;
                var old = current;
                var updated = new Item(newName, current.Chance);
                undoRedo.Push(new PropertyChangeAction<Item>(
                    $"{context}[{idx}].Name: {old.Name}→{newName}",
                    v => { items[idx] = v; nameBox.Text = v.Name; chanceBox.Text = FormatChance(v.Chance); },
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
                    $"{context}[{idx}].Chance: {old.Chance}→{newChance}",
                    v => { items[idx] = v; nameBox.Text = v.Name; chanceBox.Text = FormatChance(v.Chance); },
                    old, updated));
            };

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,65") };
            row.Children.Add(nameBox);
            row.Children.Add(chanceBox);
            panel.Children.Add(row);
        }
    }

    public static string FormatChance(double chance) =>
        chance == Math.Floor(chance) ? ((int)chance).ToString() : chance.ToString("G");
}
