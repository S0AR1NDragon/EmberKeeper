using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Ручная настройка сетки: клетка, сдвиг, количество клеток.</summary>
public sealed class GridSetupDialog : Window
{
    public int CellPx { get; private set; }
    public double OffX { get; private set; }
    public double OffY { get; private set; }
    public int Cols { get; private set; }
    public int Rows { get; private set; }
    public bool FitToImage { get; private set; }

    public GridSetupDialog(MapDoc doc)
    {
        Title = "Настройка сетки вручную";
        Width = 460;
        Height = 420;
        Background = Pal.B("#100d0b");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(18), Spacing = 10 };

        root.Children.Add(new TextBlock
        {
            Text = "Настройка сетки",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"),
        });

        NumericUpDown Num(string label, double value, double min, double max, double inc)
        {
            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Pal.B("#9c8b78") });
            var n = new NumericUpDown
            {
                Value = (decimal)value,
                Minimum = (decimal)min,
                Maximum = (decimal)max,
                Increment = (decimal)inc,
                Width = 150,
                Background = Pal.B("#15110e"),
                Foreground = Pal.B("#efe3d2"),
                BorderBrush = Pal.B("#352a21"),
                BorderThickness = new Thickness(1),
            };
            sp.Children.Add(n);
            root.Children.Add(sp);
            return n;
        }

        var cell = Num("РАЗМЕР КЛЕТКИ, PX", doc.GridSize, 16, 300, 1);
        var offX = Num("СДВИГ ПО X, КЛЕТОК", doc.GridOffX, -2, 2, 0.05);
        var offY = Num("СДВИГ ПО Y, КЛЕТОК", doc.GridOffY, -2, 2, 0.05);
        var cols = Num("КЛЕТОК ПО ШИРИНЕ", doc.Cols, 3, 400, 1);
        var rows = Num("КЛЕТОК ПО ВЫСОТЕ", doc.Rows, 3, 400, 1);

        var fit = new CheckBox
        {
            Content = "Подгонять количество клеток под картинку",
            Foreground = Pal.B("#efe3d2"),
            FontSize = 12,
            IsChecked = !string.IsNullOrEmpty(doc.Background),
        };
        root.Children.Add(fit);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var ok = new Button
        {
            Content = "Применить",
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(9),
            Background = Pal.B("#cf9245"),
            Foreground = Pal.B("#241505"),
            BorderBrush = Pal.B("#e0b072"),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.Bold,
        };
        ok.Click += (_, _) =>
        {
            CellPx = (int)Math.Clamp((double)(cell.Value ?? 64), 16, 300);
            OffX = Math.Clamp((double)(offX.Value ?? 0), -2, 2);
            OffY = Math.Clamp((double)(offY.Value ?? 0), -2, 2);
            Cols = (int)Math.Clamp((double)(cols.Value ?? 20), 3, 400);
            Rows = (int)Math.Clamp((double)(rows.Value ?? 14), 3, 400);
            FitToImage = fit.IsChecked == true;
            Close(true);
        };
        var cancel = new Button
        {
            Content = "Отмена",
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(9),
            Background = Pal.B("#221b16"),
            Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"),
            BorderThickness = new Thickness(1),
        };
        cancel.Click += (_, _) => Close(false);
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        root.Children.Add(buttons);

        Content = root;
    }
}
