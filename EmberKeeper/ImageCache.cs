using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace EmberKeeper;

/// <summary>
/// Кэш картинок: декодирует файл один раз (и с ограничением по ширине),
/// чтобы превью, миниатюры и экран игроков не создавали битмапы заново.
/// </summary>
public static class ImageCache
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? Get(string? path, int maxWidth = 1920)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var key = maxWidth + "|" + path;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        Bitmap? bmp = null;
        try
        {
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                bmp = Bitmap.DecodeToWidth(stream, maxWidth);
            }
        }
        catch
        {
            bmp = null;
        }

        Cache[key] = bmp;
        return bmp;
    }

    public static void Forget(string path)
    {
        var keys = new List<string>();
        foreach (var k in Cache.Keys)
            if (k.EndsWith("|" + path, StringComparison.OrdinalIgnoreCase)) keys.Add(k);
        foreach (var k in keys) Cache.Remove(k);
    }

    public static void Clear() => Cache.Clear();
}
