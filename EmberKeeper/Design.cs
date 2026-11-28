using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmberKeeper;

/// <summary>
/// Варианты современного оформления (градиенты, свечения, тени).
/// Выбирается переменной окружения EMBERKEEPER_DESIGN: a / b / c; без неё — классика.
/// </summary>
public static class Design
{
    public static string Variant { get; private set; } =
        (Environment.GetEnvironmentVariable("EMBERKEEPER_DESIGN") ?? Settings.LoadDesign()).Trim().ToLowerInvariant();

    public static bool Modern => Variant is "a" or "b" or "c";

    /// <summary>Раскладка: classic / top (меню сверху) / rail (узкая колонка иконок).</summary>
    public static string Layout { get; private set; } =
        (Environment.GetEnvironmentVariable("EMBERKEEPER_LAYOUT") ?? Settings.LoadLayout()).Trim().ToLowerInvariant();

    public static bool LayoutModern => Layout is "top" or "rail";

    public static void Set(string design, string layout)
    {
        if (design is "a" or "b" or "c" or "classic") Variant = design;
        if (layout is "classic" or "top" or "rail") Layout = layout;
        Settings.SaveInterface(Variant, Layout, Fonts.Current);
        ApplyResources();
    }

    public static double Radius => Variant switch { "a" => 16, "b" => 14, "c" => 18, _ => 12 };

    private static Color A(string hex, byte alpha) => Color.FromArgb(alpha, Pal.C(hex).R, Pal.C(hex).G, Pal.C(hex).B);

    private static Color A(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    private static IBrush Gradient(Color c1, Color c2, double angleDeg)
    {
        var b = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        };
        b.GradientStops.Add(new GradientStop(c1, 0));
        b.GradientStops.Add(new GradientStop(c2, 1));
        return b;
    }

    private static IBrush Radial(Color center, Color edge, RelativePoint pos)
    {
        var b = new RadialGradientBrush
        {
            Center = pos,
            GradientOrigin = pos,
            Radius = 0.9,
        };
        b.GradientStops.Add(new GradientStop(center, 0));
        b.GradientStops.Add(new GradientStop(edge, 1));
        return b;
    }

    public static void ApplyResources()
    {
        var res = Application.Current?.Resources;
        if (res == null) return;

        var bg = Pal.C("#100d0b");
        var bg2 = Pal.C("#1c1714");
        var accent = Pal.C("#cf9245");
        var panel = Pal.C("#1c1714");

        if (!Modern)
        {
            res["AppBackdrop"] = new SolidColorBrush(bg);
            res["AppGlow"] = Brushes.Transparent;
            res["AppGlow2"] = Brushes.Transparent;
            res["AppVignette"] = Brushes.Transparent;
            res["AppSidebar"] = new SolidColorBrush(Pal.C("#15110e"));
            res["AppHeader"] = new SolidColorBrush(Pal.C("#15110e"));
            res["AppCard"] = new SolidColorBrush(panel);
            res["AppCardHover"] = new SolidColorBrush(Pal.C("#221b16"));
            res["AppCardLine"] = new SolidColorBrush(Pal.C("#352a21"));
            res["AppShadow"] = default(BoxShadows);
            res["AppRadius"] = new CornerRadius(12);
            return;
        }

        bool light = !Pal.Dark;

        // мягкая тень: в светлой теме почти прозрачная и тёплая
        string softShadow = light ? "0 6 20 0 #1e000000" : "0 10 30 0 #55000000";
        string deepShadow = light ? "0 10 30 0 #2a000000" : "0 16 44 0 #70000000";

        if (!light)
        {
            switch (Variant)
            {
                case "a":
                    res["AppBackdrop"] = Gradient(A(accent, 20), bg, 135);
                    res["AppGlow"] = Radial(A(accent, 85), A(accent, 0), new RelativePoint(0.88, 0.03, RelativeUnit.Relative));
                    res["AppGlow2"] = Radial(A(accent, 40), A(accent, 0), new RelativePoint(0.05, 0.97, RelativeUnit.Relative));
                    res["AppVignette"] = Brushes.Transparent;
                    res["AppSidebar"] = new SolidColorBrush(A("#100d0b", 165));
                    res["AppHeader"] = new SolidColorBrush(A("#100d0b", 135));
                    res["AppCard"] = new SolidColorBrush(A("#1c1714", 200));
                    res["AppCardHover"] = new SolidColorBrush(A("#2e241c", 225));
                    res["AppCardLine"] = new SolidColorBrush(A(accent, 95));
                    res["AppShadow"] = BoxShadows.Parse(deepShadow);
                    res["AppRadius"] = new CornerRadius(16);
                    break;
                case "b":
                    res["AppBackdrop"] = Gradient(bg2, Pal.C("#0b0806"), 90);
                    res["AppGlow"] = Radial(A(accent, 90), A(accent, 0), new RelativePoint(0.5, 1.12, RelativeUnit.Relative));
                    res["AppGlow2"] = Radial(A(accent, 45), A(accent, 0), new RelativePoint(0.02, 0.05, RelativeUnit.Relative));
                    res["AppVignette"] = Brushes.Transparent;
                    res["AppSidebar"] = new SolidColorBrush(A("#15110e", 150));
                    res["AppHeader"] = new SolidColorBrush(A("#15110e", 120));
                    res["AppCard"] = Gradient(A("#2c2218", 248), A("#1a1411", 242), 90);
                    res["AppCardHover"] = new SolidColorBrush(A("#3a2d21", 250));
                    res["AppCardLine"] = new SolidColorBrush(A(accent, 110));
                    res["AppShadow"] = BoxShadows.Parse(softShadow);
                    res["AppRadius"] = new CornerRadius(14);
                    break;
                default:
                    res["AppBackdrop"] = Gradient(Pal.C("#0d0b08"), Pal.C("#171410"), 160);
                    res["AppGlow"] = Radial(A("#efe3d2", 30), A("#efe3d2", 0), new RelativePoint(0.5, 0.10, RelativeUnit.Relative));
                    res["AppGlow2"] = Radial(A(accent, 55), A(accent, 0), new RelativePoint(0.95, 0.95, RelativeUnit.Relative));
                    var vig = new RadialGradientBrush
                    {
                        Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                        GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                        Radius = 1.2,
                    };
                    vig.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.4));
                    vig.GradientStops.Add(new GradientStop(Color.FromArgb(170, 0, 0, 0), 1));
                    res["AppVignette"] = vig;
                    res["AppSidebar"] = new SolidColorBrush(A("#13100d", 110));
                    res["AppHeader"] = new SolidColorBrush(A("#13100d", 90));
                    res["AppCard"] = new SolidColorBrush(A("#1c1714", 140));
                    res["AppCardHover"] = new SolidColorBrush(A("#2a211a", 190));
                    res["AppCardLine"] = new SolidColorBrush(A("#efe3d2", 105));
                    res["AppShadow"] = BoxShadows.Parse(deepShadow);
                    res["AppRadius"] = new CornerRadius(18);
                    break;
            }
        }
        else
        {
            // светлая тема: мягкие тёплые градиенты, без чёрных теней
            var l1 = Pal.C("#fdf8ef");
            var l2 = Pal.C("#eee3d2");
            switch (Variant)
            {
                case "a":
                    res["AppBackdrop"] = Gradient(l1, l2, 135);
                    res["AppGlow"] = Radial(A(accent, 60), A(accent, 0), new RelativePoint(0.88, 0.03, RelativeUnit.Relative));
                    res["AppGlow2"] = Radial(A(accent, 30), A(accent, 0), new RelativePoint(0.05, 0.97, RelativeUnit.Relative));
                    res["AppVignette"] = Brushes.Transparent;
                    res["AppSidebar"] = new SolidColorBrush(A("#fbf6ee", 235));
                    res["AppHeader"] = new SolidColorBrush(A("#fbf6ee", 220));
                    res["AppCard"] = new SolidColorBrush(A("#fffdf8", 250));
                    res["AppCardHover"] = new SolidColorBrush(A("#f7efe2", 255));
                    res["AppCardLine"] = new SolidColorBrush(A(accent, 70));
                    res["AppShadow"] = BoxShadows.Parse(softShadow);
                    res["AppRadius"] = new CornerRadius(16);
                    break;
                case "b":
                    res["AppBackdrop"] = Gradient(Pal.C("#fbf4e8"), Pal.C("#eadfcd"), 90);
                    res["AppGlow"] = Radial(A(accent, 70), A(accent, 0), new RelativePoint(0.5, 1.1, RelativeUnit.Relative));
                    res["AppGlow2"] = Radial(A(accent, 35), A(accent, 0), new RelativePoint(0.02, 0.05, RelativeUnit.Relative));
                    res["AppVignette"] = Brushes.Transparent;
                    res["AppSidebar"] = new SolidColorBrush(A("#fbf6ee", 230));
                    res["AppHeader"] = new SolidColorBrush(A("#fbf6ee", 210));
                    res["AppCard"] = Gradient(A("#ffffff", 255), A("#f4e9d8", 252), 90);
                    res["AppCardHover"] = new SolidColorBrush(A("#fff6e7", 255));
                    res["AppCardLine"] = new SolidColorBrush(A(accent, 80));
                    res["AppShadow"] = BoxShadows.Parse(softShadow);
                    res["AppRadius"] = new CornerRadius(14);
                    break;
                default:
                    res["AppBackdrop"] = Gradient(Pal.C("#f7f2e8"), Pal.C("#e6dbc9"), 160);
                    res["AppGlow"] = Radial(A("#ffffff", 120), A("#ffffff", 0), new RelativePoint(0.5, 0.1, RelativeUnit.Relative));
                    res["AppGlow2"] = Radial(A(accent, 45), A(accent, 0), new RelativePoint(0.95, 0.95, RelativeUnit.Relative));
                    res["AppVignette"] = Brushes.Transparent;
                    res["AppSidebar"] = new SolidColorBrush(A("#faf5ea", 200));
                    res["AppHeader"] = new SolidColorBrush(A("#faf5ea", 185));
                    res["AppCard"] = new SolidColorBrush(A("#ffffff", 190));
                    res["AppCardHover"] = new SolidColorBrush(A("#ffffff", 240));
                    res["AppCardLine"] = new SolidColorBrush(A("#efe3d2", 120));
                    res["AppShadow"] = BoxShadows.Parse(softShadow);
                    res["AppRadius"] = new CornerRadius(18);
                    break;
            }
        }
    }
}
