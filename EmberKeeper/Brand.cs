using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace EmberKeeper;

/// <summary>Логотип программы (векторный PNG из ресурсов).</summary>
public static class Brand
{
    private const string LogoUri = "avares://EmberKeeper/Assets/Brand/logo.png";
    private const string IcoUri = "avares://EmberKeeper/emberkeeper.ico";

    public static IImage? Load()
    {
        try { return new Bitmap(AssetLoader.Open(new Uri(LogoUri))); }
        catch { return null; }
    }

    /// <summary>Картинка-логотип нужного размера.</summary>
    public static Control Image(double size)
    {
        var bmp = Load();
        if (bmp == null) return Icons.Make("sparkle", size, "#d99b4a");
        return new Image
        {
            Source = bmp,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
        };
    }

    public static WindowIcon? WindowIcon()
    {
        try { return new WindowIcon(AssetLoader.Open(new Uri(IcoUri))); }
        catch { return null; }
    }
}
