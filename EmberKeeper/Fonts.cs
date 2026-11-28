using System;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Шрифтовые пары: заголовки + основной текст. Выбор хранится в настройках.</summary>
public static class Fonts
{
    public static readonly (string Id, string Label, string Heading, string Body)[] Presets =
    {
        ("f1", "Классика", "Georgia", "Inter, Segoe UI"),
        ("f2", "Современный", "Bahnschrift, Segoe UI", "Inter, Segoe UI"),
        ("f3", "Книжный", "Constantia, Georgia", "Segoe UI"),
        ("f4", "Академический", "Palatino Linotype, Book Antiqua", "Segoe UI"),
    };

    public static string Current { get; private set; } =
        (Environment.GetEnvironmentVariable("EMBERKEEPER_FONTS") ?? Settings.LoadFonts()).Trim().ToLowerInvariant();

    public static string Heading => Preset().Heading;
    public static string Body => Preset().Body;

    private static (string Id, string Label, string Heading, string Body) Preset()
    {
        foreach (var p in Presets) if (p.Id == Current) return p;
        return Presets[1];
    }

    public static string Label => Preset().Label;

    public static void Set(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        Current = id;
        Design.ApplyResources();
    }

    public static void ApplyResources()
    {
        var res = Avalonia.Application.Current?.Resources;
        if (res == null) return;
        res["AppBodyFont"] = new FontFamily(Body);
        res["AppHeadingFont"] = new FontFamily(Heading);
    }
}
