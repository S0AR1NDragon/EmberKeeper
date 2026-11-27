using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>
/// Темы оформления. Все цвета в интерфейсе берутся через Theme.C / Theme.B,
/// поэтому переключение светлой/тёмной темы меняет всю палитру разом.
/// </summary>
public static class Pal
{
    public static bool Dark { get; private set; } = true;

    public static event Action? Changed;

    // тёмный цвет -> светлый эквивалент
    private static readonly Dictionary<string, string> LightMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#100d0b"] = "#f7f1e6", // bg
        ["#0b0908"] = "#fbf6ee",
        ["#0d0b08"] = "#f9f3e9",
        ["#15110e"] = "#efe7d9", // bg2
        ["#14100d"] = "#f4ecdf",
        ["#120e0b"] = "#f5ede0",
        ["#1a1411"] = "#f7f0e4",
        ["#13100d"] = "#f2ebde",
        ["#1c1714"] = "#fffaf3", // panel
        ["#221b16"] = "#f5ecdd", // panel2
        ["#241d18"] = "#f0e5d2",
        ["#1f1915"] = "#f0e6d6",
        ["#2e241c"] = "#eadfcb",
        ["#352a21"] = "#d9cab4", // line
        ["#2a211a"] = "#e5d7c3", // line soft
        ["#4a3728"] = "#c6ac8b",
        ["#3a2d24"] = "#e9dcc8",
        ["#2a1b14"] = "#fdf8ef",
        ["#2d1f14"] = "#f4e6d1",
        ["#1a120d"] = "#fbf3e6",
        ["#efe3d2"] = "#2b2119", // ink
        ["#f6e7cf"] = "#1d150e",
        ["#f3e6d4"] = "#1d150e",
        ["#d7c6b2"] = "#4a3a29",
        ["#c8b49b"] = "#7b6650",
        ["#b7a794"] = "#5f4c38",
        ["#9c8b78"] = "#7b6650",
        ["#8d7a66"] = "#8a7458",
        ["#83725f"] = "#9a8468",
        ["#7f6f5e"] = "#9a8468",
        ["#7d6d5c"] = "#8a7458",
        ["#7a6754"] = "#9a8468",
        ["#171410"] = "#f2e9d8",
        ["#b4a492"] = "#5f4c38",
        ["#8a7a5e"] = "#8a7458",
        ["#0d0b08"] = "#f9f3e9",
        ["#d99b4a"] = "#a86a24", // gold
        ["#f0c27b"] = "#8f5715", // gold soft
        ["#e3c46a"] = "#8f5715",
        ["#d8b95a"] = "#a86a24",
        ["#e0b072"] = "#b0763a",
        ["#cf9245"] = "#c98a2e", // accent
        ["#8C7030"] = "#b0763a",
        ["#f5e3a6"] = "#a86a24",
        ["#241505"] = "#2b1a06",
        ["#2b1a06"] = "#2b1a06",
        ["#7c2a2c"] = "#8a2b3a", // blood
        ["#7fa66e"] = "#4f7a42", // green
        ["#70a3a2"] = "#3f7a79", // teal
        ["#0b0806"] = "#fbf6ee",
        ["#070605"] = "#fbf6ee",
        ["#c9b8a4"] = "#4a3a29",
        ["#d6cbb6"] = "#4a3a29",
        ["#a08d78"] = "#7b6650",
        ["#9a8874"] = "#7b6650",
    };

    public static Color C(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Colors.Transparent;
        var key = hex.Trim();
        if (Dark) return Color.Parse(key);
        return Color.Parse(LightMap.TryGetValue(key, out var light) ? light : key);
    }

    public static IBrush B(string hex) => new SolidColorBrush(C(hex));

    public static void Set(bool dark)
    {
        Dark = dark;
        UpdateResources();
        Changed?.Invoke();
    }

    /// <summary>Палитра для XAML-стилей (DynamicResource).</summary>
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
    }
}

/// <summary>Пользовательские настройки (тема).</summary>
public static class Settings
{
    private sealed class Data
    {
        public bool Dark { get; set; } = true;
        public int StartTab { get; set; }
    }

    private static string Path => AppPaths.File("settings.json");

    public static bool LoadDark() => Load().Dark;

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

    public static void SaveDark(bool dark)
    {
        var d = Load();
        d.Dark = dark;
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
