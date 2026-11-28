using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>
/// Темы оформления: цветовой стиль (янтарь/зелень/синева/фиолет/бирюза/вино) × тёмная или светлая.
/// Все цвета интерфейса берутся через Pal.C / Pal.B, поэтому тема меняет всю палитру разом.
/// </summary>
public static class Pal
{
    public static bool Dark { get; private set; } = true;

    /// <summary>Цветовой стиль: amber, green, blue, purple, teal, wine.</summary>
    public static string Style { get; private set; } = "amber";

    public static event Action? Changed;

    /// <summary>Доступные цветовые стили: id, подпись, целевой оттенок (0–360).</summary>
    public static readonly (string Id, string Label, double Hue)[] Styles =
    {
        ("amber", "Янтарь", 35),
        ("green", "Зелень", 145),
        ("blue", "Синева", 215),
        ("purple", "Фиолет", 280),
        ("teal", "Бирюза", 175),
        ("wine", "Вино", 350),
    };

    private static double StyleHue(string id) => Styles.FirstOrDefault(s => s.Id == id).Hue is var h && h > 0 ? h : 35;

    // тёмный цвет -> светлый эквивалент
    private static readonly Dictionary<string, string> LightMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#100d0b"] = "#f7f1e6", ["#0b0908"] = "#fbf6ee", ["#0d0b08"] = "#f9f3e9",
        ["#15110e"] = "#efe7d9", ["#14100d"] = "#f4ecdf", ["#120e0b"] = "#f5ede0",
        ["#1a1411"] = "#f7f0e4", ["#13100d"] = "#f2ebde", ["#1c1714"] = "#fffaf3",
        ["#221b16"] = "#f5ecdd", ["#241d18"] = "#f0e5d2", ["#1f1915"] = "#f0e6d6",
        ["#2e241c"] = "#eadfcb", ["#352a21"] = "#d9cab4", ["#2a211a"] = "#e5d7c3",
        ["#4a3728"] = "#c6ac8b", ["#3a2d24"] = "#e9dcc8", ["#2a1b14"] = "#fdf8ef",
        ["#2d1f14"] = "#f4e6d1", ["#1a120d"] = "#fbf3e6", ["#efe3d2"] = "#2b2119",
        ["#f6e7cf"] = "#1d150e", ["#f3e6d4"] = "#1d150e", ["#d7c6b2"] = "#4a3a29",
        ["#c8b49b"] = "#7b6650", ["#b7a794"] = "#5f4c38", ["#9c8b78"] = "#7b6650",
        ["#8d7a66"] = "#8a7458", ["#83725f"] = "#9a8468", ["#7f6f5e"] = "#9a8468",
        ["#7d6d5c"] = "#8a7458", ["#7a6754"] = "#9a8468", ["#171410"] = "#f2e9d8",
        ["#b4a492"] = "#5f4c38", ["#8a7a5e"] = "#8a7458", ["#f0c27b"] = "#8f5715",
        ["#e3c46a"] = "#8f5715", ["#d8b95a"] = "#a86a24", ["#e0b072"] = "#b0763a",
        ["#cf9245"] = "#c98a2e", ["#8C7030"] = "#b0763a", ["#f5e3a6"] = "#a86a24",
        ["#241505"] = "#2b1a06", ["#2b1a06"] = "#2b1a06", ["#0b0806"] = "#fbf6ee",
        ["#070605"] = "#fbf6ee", ["#c9b8a4"] = "#4a3a29", ["#d6cbb6"] = "#4a3a29",
        ["#a08d78"] = "#7b6650", ["#9a8874"] = "#7b6650", ["#f6e7cf"] = "#1d150e",
    };

    // цвета-статусы, которые не перекрашиваем стилем
    private static readonly HashSet<string> Semantic = new(StringComparer.OrdinalIgnoreCase)
    {
        "#7c2a2c", "#8a2b3a", "#7fa66e", "#70a3a2",
    };

    public static Color C(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Colors.Transparent;
        var key = hex.Trim();
        var baseHex = Dark ? key : (LightMap.TryGetValue(key, out var light) ? light : InvertLight(key));
        var c = Color.Parse(baseHex);
        if (Style == "amber" || Semantic.Contains(key)) return c;
        return ShiftHue(c, StyleHue(Style) - 35);
    }

    /// <summary>Произвольный цвет в светлой теме — инвертируем светлоту.</summary>
    private static string InvertLight(string hex)
    {
        try
        {
            var c = Color.Parse(hex);
            DoubleToHsl(c.R / 255.0, c.G / 255.0, c.B / 255.0, out var h, out var s, out var l);
            // тёмный цвет в светлой теме осветляем, и без того светлый — не затемняем
            if (l >= 0.55) { l = Math.Min(0.985, l * 0.99 + 0.015); s *= 0.55; }
            else { l = 1.0 - l * 0.92; s *= 0.75; }
            var (r, g, b) = HslToRgb(h, s, l);
            return $"#{(int)Math.Round(r * 255):x2}{(int)Math.Round(g * 255):x2}{(int)Math.Round(b * 255):x2}";
        }
        catch { return hex; }
    }

    private static Color ShiftHue(Color c, double delta)
    {
        if (c == Colors.Transparent) return c;
        DoubleToHsl(c.R / 255.0, c.G / 255.0, c.B / 255.0, out var h, out var s, out var l);
        if (s < 0.04) return c; // серые не трогаем
        h = (h + delta + 360) % 360;
        var (r, g, b) = HslToRgb(h, Math.Min(1, s * 1.02), l);
        return Color.FromArgb(c.A, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    private static void DoubleToHsl(double r, double g, double b, out double h, out double s, out double l)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2;
        if (Math.Abs(max - min) < 1e-6) { h = 0; s = 0; return; }
        double d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;
        h *= 60;
    }

    private static (double r, double g, double b) HslToRgb(double h, double s, double l)
    {
        if (s == 0) return (l, l, l);
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        double Hue(double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
        h /= 360.0;
        return (Hue(h + 1.0 / 3), Hue(h), Hue(h - 1.0 / 3));
    }

    public static IBrush B(string hex) => new SolidColorBrush(C(hex));

    public static void Set(bool dark, string? style = null)
    {
        Dark = dark;
        if (!string.IsNullOrWhiteSpace(style) && Styles.Any(s => s.Id == style)) Style = style;
        UpdateResources();
        Changed?.Invoke();
    }

    public static string StyleLabel(string id) => Styles.FirstOrDefault(s => s.Id == id).Label ?? "Янтарь";

    /// <summary>Цвета, которые в XAML берутся через {DynamicResource C_xxxxxx}.</summary>
    private static readonly string[] XamlColors = { "0b0908", "100d0b", "15110e", "1c1714", "221b16", "241505", "2a211a", "352a21", "3a2d24", "4a3728", "83725f", "8c7030", "8d7a66", "9c8b78", "b7a794", "cf9245", "d99b4a", "e0b072", "e60b0908", "efe3d2", "f0c27b", "f6e7cf" };

    public static void UpdateResources()
    {
        var res = Application.Current?.Resources;
        if (res == null) return;
        res["AppBg"] = B("#100d0b");
        res["AppBg2"] = B("#15110e");
        res["AppPanel"] = B("#1c1714");
        res["AppPanel2"] = B("#221b16");
        res["AppLine"] = B("#352a21");
        res["AppInk"] = B("#efe3d2");
        res["AppMuted"] = B("#9c8b78");
        res["AppGold"] = B("#f0c27b");
        res["AppAccent"] = B("#cf9245");
        res["AppAccentText"] = B("#241505");
        foreach (var hx in XamlColors)
            res["C_" + hx] = B("#" + hx);
        Design.ApplyResources();
    }
}

/// <summary>Пользовательские настройки (тема оформления, стартовая вкладка).</summary>
public static class Settings
{
    private sealed class Data
    {
        public bool Dark { get; set; } = true;
        public string Style { get; set; } = "amber";
        public string Design { get; set; } = "b";
        public string Layout { get; set; } = "rail";
        public string Fonts { get; set; } = "f5";
        public int StartTab { get; set; }
    }

    private static string Path => AppPaths.File("settings.json");

    public static bool LoadDark() => Load().Dark;
    public static string LoadStyle() => Load().Style;
    public static string LoadDesign() => Load().Design;
    public static string LoadLayout() => Load().Layout;
    public static string LoadFonts() => Load().Fonts;
    public static int LoadStartTab() => Load().StartTab;

    private static Data Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var d = JsonSerializer.Deserialize<Data>(File.ReadAllText(Path));
                if (d != null) return d;
            }
        }
        catch { }
        return new Data();
    }

    public static void SaveTheme(bool dark, string style)
    {
        var d = Load();
        d.Dark = dark;
        d.Style = string.IsNullOrWhiteSpace(style) ? "amber" : style;
        Save(d);
    }

    public static void SaveDark(bool dark) => SaveTheme(dark, Load().Style);

    public static void SaveInterface(string design, string layout, string fonts)
    {
        var d = Load();
        d.Design = design;
        d.Layout = layout;
        d.Fonts = fonts;
        Save(d);
    }

    private static void Save(Data d)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Dir);
            File.WriteAllText(Path, JsonSerializer.Serialize(d));
        }
        catch { }
    }
}
