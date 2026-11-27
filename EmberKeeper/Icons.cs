using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>
/// Набор тонких векторных иконок в одном стиле (линии 24×24, скруглённые концы).
/// Никаких эмодзи — только Path, поэтому иконки выглядят одинаково и чётко.
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Data = new()
    {
        ["home"] = "M4 10.5 L12 4 L20 10.5 V20 H15 V14.5 H9 V20 H4 Z",
        ["map"] = "M9 4 L3 6 V20 L9 18 L15 20 L21 18 V4 L15 6 L9 4 Z M9 4 V18 M15 6 V20",
        ["bolt"] = "M13 3 L5 13.5 H11 L10 21 L19 10 H12.5 Z",
        ["slides"] = "M3 4 H21 V16 H3 Z M8.5 20 H15.5 M12 16 V20",
        ["person"] = "M12 11.5 A3.75 3.75 0 1 0 12 4 A3.75 3.75 0 1 0 12 11.5 Z M4.5 20.5 V19 A5.5 5.5 0 0 1 10 13.5 H14 A5.5 5.5 0 0 1 19.5 19 V20.5",
        ["book"] = "M5 19.5 A2.5 2.5 0 0 1 7.5 17 H19 M5 19.5 V5.5 A2.5 2.5 0 0 1 7.5 3 H19 V22 H7.5 A2.5 2.5 0 0 1 5 19.5 Z",
        ["users"] = "M9 11 A3.5 3.5 0 1 0 9 4 A3.5 3.5 0 1 0 9 11 Z M2.5 20 V18.5 A4.5 4.5 0 0 1 7 14 H11 A4.5 4.5 0 0 1 15.5 18.5 V20 M16.5 4.5 A3.5 3.5 0 0 1 16.5 11.5 M21.5 20 V18.5 A4.5 4.5 0 0 0 18 14.2",
        ["link"] = "M10.5 13.5 A4.5 4.5 0 0 0 16.8 13.5 L19.5 10.8 A4.5 4.5 0 0 0 13.2 4.5 L11.8 5.9 M13.5 10.5 A4.5 4.5 0 0 0 7.2 10.5 L4.5 13.2 A4.5 4.5 0 0 0 10.8 19.5 L12.2 18.1",
        ["sparkle"] = "M12 3 L13.7 8.3 L19 10 L13.7 11.7 L12 17 L10.3 11.7 L5 10 L10.3 8.3 Z",
        ["sparkles"] = "M10.5 3 L11.8 7.2 L16 8.5 L11.8 9.8 L10.5 14 L9.2 9.8 L5 8.5 L9.2 7.2 Z M17.5 13.5 L18.4 16.1 L21 17 L18.4 17.9 L17.5 20.5 L16.6 17.9 L14 17 L16.6 16.1 Z",
        ["search"] = "M11 18.5 A7.5 7.5 0 1 0 11 3.5 A7.5 7.5 0 1 0 11 18.5 Z M20.5 20.5 L16.3 16.3",
        ["sun"] = "M12 16.5 A4.5 4.5 0 1 0 12 7.5 A4.5 4.5 0 1 0 12 16.5 Z M12 1.5 V4 M12 20 V22.5 M4.2 4.2 L6 6 M18 18 L19.8 19.8 M1.5 12 H4 M20 12 H22.5 M4.2 19.8 L6 18 M18 6 L19.8 4.2",
        ["moon"] = "M20.5 13 A8.5 8.5 0 1 1 11 3.5 A6.5 6.5 0 0 0 20.5 13 Z",
        ["screen"] = "M2.5 4.5 H21.5 V15.5 H2.5 Z M8 20 H16 M12 15.5 V20",
        ["dice"] = "M12 2.5 L20.5 7 V17 L12 21.5 L3.5 17 V7 Z M3.5 7 L12 11.5 L20.5 7 M12 11.5 V21.5",
        ["menu"] = "M4 7 H20 M4 12 H20 M4 17 H20",
        ["close"] = "M6 6 L18 18 M18 6 L6 18",
        ["plus"] = "M12 5.5 V18.5 M5.5 12 H18.5",
        ["minus"] = "M5.5 12 H18.5",
        ["download"] = "M12 3.5 V15 M7 10.5 L12 15.5 L17 10.5 M4.5 20.5 H19.5",
        ["upload"] = "M12 20.5 V9 M7 13.5 L12 8.5 L17 13.5 M4.5 3.5 H19.5",
        ["pause"] = "M9 5 V19 M15 5 V19",
        ["flag"] = "M6 21 V4 M6 4 H17 L15.5 8 L17 12 H6",
        ["refresh"] = "M20 12 A8 8 0 1 1 17.7 6.3 M20 3.5 V7.5 H16",
        ["play"] = "M8.5 4.5 L19.5 12 L8.5 19.5 Z",
        ["chevron-up"] = "M6.5 14.5 L12 9 L17.5 14.5",
        ["chevron-down"] = "M6.5 9.5 L12 15 L17.5 9.5",
        ["shield"] = "M12 3 L19.5 6 V12 C19.5 16.8 16.3 19.6 12 21 C7.7 19.6 4.5 16.8 4.5 12 V6 Z",
        ["token"] = "M12 21 A9 9 0 1 0 12 3 A9 9 0 1 0 12 21 Z M12 16 A4 4 0 1 0 12 8 A4 4 0 1 0 12 16 Z",
        ["star"] = "M12 3 L14.5 9 L21 9.6 L16 14 L17.6 20.5 L12 17 L6.4 20.5 L8 14 L3 9.6 L9.5 9 Z",
        ["skull"] = "M12 3 A7.5 7.5 0 0 0 4.5 10.5 V14 L6.5 16 V20 H17.5 V16 L19.5 14 V10.5 A7.5 7.5 0 0 0 12 3 Z M9.5 11.5 A1 1 0 1 0 9.5 9.5 A1 1 0 1 0 9.5 11.5 Z M14.5 11.5 A1 1 0 1 0 14.5 9.5 A1 1 0 1 0 14.5 11.5 Z",
        ["diamond"] = "M12 3 L20.5 9 L12 21 L3.5 9 Z",
        ["pencil"] = "M4 20 H8 L20 8 L16 4 L4 16 Z M13 7 L17 11",
        ["wall"] = "M3 5 H21 V19 H3 Z M3 12 H21 M9 5 V12 M15 12 V19",
        ["fog"] = "M3 9 Q6 6 9 9 T15 9 T21 9 M3 15 Q6 12 9 15 T15 15 T21 15",
        ["template"] = "M4 4 H9 M13 4 H20 M20 4 V9 M20 13 V20 M20 20 H15 M11 20 H4 M4 20 V15 M4 11 V4",
        ["prop"] = "M3.5 8 L12 3.5 L20.5 8 V16 L12 20.5 L3.5 16 Z M3.5 8 L12 12.5 L20.5 8 M12 12.5 V20.5",
        ["ruler"] = "M3 16 L16 3 L21 8 L8 21 Z M7 12 L9.5 14.5 M10.5 8.5 L13 11 M14 5 L16.5 7.5",
        ["image"] = "M3.5 4.5 H20.5 V19.5 H3.5 Z M3.5 15 L8.5 10 L13 14.5 L16 11.5 L20.5 16 M8 8.6 A1.2 1.2 0 1 0 8 8.5 Z",
        ["fit"] = "M4 9 V4 H9 M20 9 V4 H15 M4 15 V20 H9 M20 15 V20 H15",
        ["trash"] = "M4.5 7 H19.5 M9.5 7 V4.5 H14.5 V7 M6.5 7 L7.5 20.5 H16.5 L17.5 7 M10 11 V16.5 M14 11 V16.5",
        ["dot"] = "M12 16 A4 4 0 1 0 12 8 A4 4 0 1 0 12 16 Z",
        ["leaf"] = "M4 20 C4 11 11 4 20 4 C20 13 13 20 4 20 Z M4 20 L13 11",
        ["bolt2"] = "M13 3 L5 13.5 H11 L10 21 L19 10 H12.5 Z",
    };

    private static readonly HashSet<string> Filled = new()
    {
        "bolt", "bolt2", "play", "sparkle", "sparkles", "star", "flag", "dot",
    };

    /// <summary>Иконка нужного размера и цвета.</summary>
    public static Path Make(string name, double size = 18, string? hex = null, double thickness = 1.6)
    {
        var brush = Pal.B(hex ?? "#d99b4a");
        var key = Data.ContainsKey(name) ? name : "dot";
        var path = new Path
        {
            Data = Geometry.Parse(Data[key]),
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Filled.Contains(key))
        {
            path.Fill = brush;
            path.Stroke = null;
        }
        else
        {
            path.Stroke = brush;
            path.StrokeThickness = thickness;
            path.StrokeLineCap = PenLineCap.Round;
            path.StrokeJoin = PenLineJoin.Round;
            path.Fill = null;
        }

        return path;
    }

    /// <summary>Сменить цвет иконки (для подсветки активного пункта меню).</summary>
    public static void Recolor(Path icon, string hex)
    {
        var brush = Pal.B(hex);
        if (icon.Fill != null) icon.Fill = brush;
        else icon.Stroke = brush;
    }

    /// <summary>Содержимое кнопки: иконка + текст.</summary>
    public static Control Row(string name, string text, double size = 15, string? iconHex = null,
                              string? textHex = null, double gap = 7)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = gap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(Make(name, size, iconHex));
        if (!string.IsNullOrEmpty(text))
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = textHex == null ? null : Pal.B(textHex),
            });
        }
        return panel;
    }
}
