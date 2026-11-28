using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace EmberKeeper;

/// <summary>
/// Логотип программы. Внутри интерфейса эмблема перекрашивается под цвет темы
/// (маска из формы логотипа + акцентная заливка), а иконка приложения всегда «огненная».
/// </summary>
public static class Brand
{
    private const string LogoUri = "avares://EmberKeeper/Assets/Brand/logo-1.png";
    private const string MaskUri = "avares://EmberKeeper/Assets/Brand/logo-mask.png";
    private const string IcoUri = "avares://EmberKeeper/emberkeeper.ico";

    private static Bitmap? _mask;
    private static readonly Dictionary<string, Bitmap> Cache = new();

    public static IImage? Load()
    {
        try { return new Bitmap(AssetLoader.Open(new Uri(LogoUri))); }
        catch { return null; }
    }

    private static Bitmap? Mask()
    {
        if (_mask != null) return _mask;
        try { _mask = new Bitmap(AssetLoader.Open(new Uri(MaskUri))); }
        catch { _mask = null; }
        return _mask;
    }

    /// <summary>Логотип нужного размера, перекрашенный под текущую тему.</summary>
    public static Control Image(double size)
    {
        var bmp = Themed(size);
        if (bmp == null) return Icons.Make("sparkle", size, "#d99b4a");
        return new Image { Source = bmp, Width = size, Height = size, Stretch = Stretch.Uniform };
    }

    private static Bitmap? Themed(double size)
    {
        try
        {
            int px = (int)Math.Max(8, Math.Round(size * 2));
            var accent = Pal.C("#cf9245");
            var top = Pal.C("#f0c27b");
            string key = px + ":" + accent + ":" + top;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var mask = Mask();
            if (mask == null) return null;

            var rtb = new RenderTargetBitmap(new PixelSize(px, px));
            using (var ctx = rtb.CreateDrawingContext())
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(top, 0),
                        new GradientStop(accent, 1),
                    },
                };
                using (ctx.PushOpacityMask(new ImageBrush(mask) { Stretch = Stretch.Uniform }, new Rect(0, 0, px, px)))
                    ctx.FillRectangle(brush, new Rect(0, 0, px, px));
            }
            Cache[key] = rtb;
            return rtb;
        }
        catch { return null; }
    }

    public static WindowIcon? WindowIcon()
    {
        try { return new WindowIcon(AssetLoader.Open(new Uri(IcoUri))); }
        catch { return null; }
    }
}
