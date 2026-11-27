using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace EmberKeeper;

/// <summary>Предпросмотр фона карты: показать картинку и решить, подгонять ли размер карты.</summary>
public sealed class BackgroundPreviewDialog : Window
{
    public bool FitMap { get; private set; } = true;

    public BackgroundPreviewDialog(string path, PixelSize size, MapDoc doc)
    {
        Title = "Фон карты";
        Width = 720;
        Height = 640;
        Background = Pal.B("#100d0b");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(18), Spacing = 12 };

        root.Children.Add(new TextBlock
        {
            Text = "Предпросмотр фона",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"),
        });

        var img = ImageCache.Get(path, 1600);
        var preview = new Border
        {
            Background = Pal.B("#15110e"),
            BorderBrush = Pal.B("#352a21"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Height = 380,
            ClipToBounds = true,
            Child = img == null
                ? new TextBlock { Text = "Не удалось прочитать изображение", Foreground = Pal.B("#c96a5a"), Margin = new Thickness(12) }
                : new Image { Source = img, Stretch = Stretch.Uniform },
        };
        root.Children.Add(preview);

        int cell = Math.Max(32, doc.GridSize);
        int cols = Math.Clamp((int)Math.Round(size.Width / (double)cell), 5, 300);
        int rows = Math.Clamp((int)Math.Round(size.Height / (double)cell), 5, 300);

        root.Children.Add(new TextBlock
        {
            Text = $"Картинка: {size.Width}×{size.Height} px. " +
                   $"При сетке {cell} px это {cols}×{rows} клеток " +
                   $"({cols * doc.FeetPerCell}×{rows * doc.FeetPerCell} футов).",
            FontSize = 12.5,
            Foreground = Pal.B("#9c8b78"),
            TextWrapping = TextWrapping.Wrap,
        });

        var fit = new CheckBox
        {
            IsChecked = true,
            Content = "Подогнать размер карты под картинку (без растяжения)",
            Foreground = Pal.B("#efe3d2"),
            FontSize = 12.5,
        };
        fit.IsCheckedChanged += (_, _) => FitMap = fit.IsChecked == true;
        root.Children.Add(fit);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
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
        ok.Click += (_, _) => Close(true);
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
