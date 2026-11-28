using System;
using Avalonia.Media;

namespace EmberKeeperLicenseGen;

/// <summary>Темы окна генератора: цветовой стиль × тёмная/светлая.</summary>
internal static class LgTheme
{
    public static bool Dark { get; set; } = true;
    public static string Style { get; set; } = "amber";

    public static readonly (string Id, string Label, double Hue)[] Styles =
    {
        ("amber", "Янтарь", 35),
        ("green", "Зелень", 145),
        ("blue", "Синева", 215),
        ("purple", "Фиолет", 280),
        ("teal", "Бирюза", 175),
        ("wine", "Вино", 350),
    };

    public static double Hue
    {
        get
        {
            foreach (var s in Styles) if (s.Id == Style) return s.Hue;
            return 35;
        }
    }

    private static Color Hsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        if (s < 0) s = 0; if (s > 1) s = 1;
        if (l < 0) l = 0; if (l > 1) l = 1;
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        double Hue2(double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
        h /= 360.0;
        return Color.FromRgb((byte)Math.Round(Hue2(h + 1.0 / 3) * 255),
                             (byte)Math.Round(Hue2(h) * 255),
                             (byte)Math.Round(Hue2(h - 1.0 / 3) * 255));
    }

    private static Color Bg => Hsl(Hue, Dark ? 0.22 : 0.40, Dark ? 0.06 : 0.955);
    private static Color Bg2 => Hsl(Hue, Dark ? 0.20 : 0.38, Dark ? 0.09 : 0.925);
    private static Color Panel => Hsl(Hue, Dark ? 0.16 : 0.30, Dark ? 0.13 : 0.985);
    private static Color Line => Hsl(Hue, Dark ? 0.16 : 0.28, Dark ? 0.22 : 0.855);
    private static Color Ink => Hsl(Hue, Dark ? 0.10 : 0.20, Dark ? 0.92 : 0.16);
    private static Color Muted => Hsl(Hue, Dark ? 0.13 : 0.22, Dark ? 0.62 : 0.42);
    private static Color Accent => Hsl(Hue, Dark ? 0.55 : 0.55, Dark ? 0.58 : 0.44);
    private static Color AccentText => Hsl(Hue, 0.65, Dark ? 0.13 : 0.99);
    private static Color Gold => Hsl(Hue + 6, Dark ? 0.60 : 0.60, Dark ? 0.74 : 0.36);

    public static IBrush BrBg => new SolidColorBrush(Bg);
    public static IBrush BrBg2 => new SolidColorBrush(Bg2);
    public static IBrush BrPanel => new SolidColorBrush(Panel);
    public static IBrush BrLine => new SolidColorBrush(Line);
    public static IBrush BrInk => new SolidColorBrush(Ink);
    public static IBrush BrMuted => new SolidColorBrush(Muted);
    public static IBrush BrAccent => new SolidColorBrush(Accent);
    public static IBrush BrAccentText => new SolidColorBrush(AccentText);
    public static IBrush BrGold => new SolidColorBrush(Gold);

    public static Color SwatchColor(double hue) => Hsl(hue, 0.55, 0.60);
}
