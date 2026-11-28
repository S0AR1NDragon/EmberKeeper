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
    public static string Variant { get; } =
        (Environment.GetEnvironmentVariable("EMBERKEEPER_DESIGN") ?? "classic").Trim().ToLowerInvariant();

    public static bool Modern => Variant is "a" or "b" or "c";

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
            res["AppSidebar"] = new SolidColorBrush(Pal.C("#15110e"));
            res["AppHeader"] = new SolidColorBrush(Pal.C("#15110e"));
            res["AppCard"] = new SolidColorBrush(panel);
            res["AppCardHover"] = new SolidColorBrush(Pal.C("#221b16"));
            res["AppCardLine"] = new SolidColorBrush(Pal.C("#352a21"));
            res["AppShadow"] = default(BoxShadows);
            res["AppRadius"] = new CornerRadius(12);
            res["AppGlow2"] = Brushes.Transparent;
            res["AppVignette"] = Brushes.Transparent;
            return;
        }

        switch (Variant)
        {
            case "a": // «Аврора»: диагональная цветная заливка + два свечения
                res["AppBackdrop"] = Gradient(A(accent, 20), bg, 135);
                res["AppGlow"] = Radial(A(accent, 85), A(accent, 0), new RelativePoint(0.88, 0.03, RelativeUnit.Relative));
                res["AppGlow2"] = Radial(A(accent, 40), A(accent, 0), new RelativePoint(0.05, 0.97, RelativeUnit.Relative));
                res["AppVignette"] = Brushes.Transparent;
                res["AppSidebar"] = new SolidColorBrush(A("#100d0b", 165));
                res["AppHeader"] = new SolidColorBrush(A("#100d0b", 135));
                res["AppCard"] = new SolidColorBrush(A("#1c1714", 200));
                res["AppCardHover"] = new SolidColorBrush(A("#2e241c", 225));
                res["AppCardLine"] = new SolidColorBrush(A(accent, 95));
                res["AppShadow"] = BoxShadows.Parse("0 16 44 0 #78000000");
                res["AppRadius"] = new CornerRadius(16);
                break;
            case "b": // «Угли»: тёплое свечение снизу, градиентные карточки
                res["AppBackdrop"] = Gradient(bg2, Pal.C("#0b0806"), 90);
                res["AppGlow"] = Radial(A(accent, 90), A(accent, 0), new RelativePoint(0.5, 1.12, RelativeUnit.Relative));
                res["AppGlow2"] = Radial(A(accent, 45), A(accent, 0), new RelativePoint(0.02, 0.05, RelativeUnit.Relative));
                res["AppVignette"] = Brushes.Transparent;
                res["AppSidebar"] = new SolidColorBrush(A("#15110e", 150));
                res["AppHeader"] = new SolidColorBrush(A("#15110e", 120));
                res["AppCard"] = Gradient(A("#2c2218", 248), A("#1a1411", 242), 90);
                res["AppCardHover"] = new SolidColorBrush(A("#3a2d21", 250));
                res["AppCardLine"] = new SolidColorBrush(A(accent, 110));
                res["AppShadow"] = BoxShadows.Parse("0 20 52 0 #88000000");
                res["AppRadius"] = new CornerRadius(14);
                break;
            default: // "c" — «Стекло»: прозрачные карточки, светлая кромка, туманное свечение
                res["AppBackdrop"] = Gradient(A(Pal.C("#b7a794"), 22), Pal.C("#0b0908"), 155);
                res["AppGlow"] = Radial(A(Pal.C("#efe3d2"), 40), A(Pal.C("#efe3d2"), 0), new RelativePoint(0.5, 0.32, RelativeUnit.Relative));
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
                res["AppShadow"] = BoxShadows.Parse("0 24 64 0 #95000000");
                res["AppRadius"] = new CornerRadius(18);
                break;
        }
    }
}
