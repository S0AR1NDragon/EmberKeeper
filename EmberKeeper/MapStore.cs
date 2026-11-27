using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmberKeeper;

/// <summary>Набор локаций (несколько карт) с активной локацией.</summary>
public sealed class MapBook
{
    public List<MapDoc> Maps { get; set; } = new();
    public int Active { get; set; }
}

public static class MapStore
{
    public static string Dir => AppPaths.Dir;

    public static string Path => AppPaths.File("maps.json");

    public static MapBook Load()
    {
        MapBook book;
        try
        {
            if (File.Exists(Path))
                book = JsonSerializer.Deserialize<MapBook>(File.ReadAllText(Path)) ?? new MapBook();
            else
                book = new MapBook();
        }
        catch { book = new MapBook(); }

        // миграция старой одиночной карты
        if (book.Maps.Count == 0)
        {
            var legacy = MapDoc.Load();
            legacy.Name = "Локация 1";
            book.Maps.Add(legacy);
            book.Active = 0;
        }

        foreach (var m in book.Maps) Normalize(m);
        if (book.Active < 0 || book.Active >= book.Maps.Count) book.Active = 0;
        return book;
    }

    public static void Save(MapBook book)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path, JsonSerializer.Serialize(book, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "save_errors.log"), DateTime.Now + " " + ex + "\n\n"); } catch { }
        }
    }

    public static MapDoc NewMap(string name)
    {
        var d = new MapDoc { Name = name };
        Normalize(d);
        return d;
    }

    public static void Normalize(MapDoc d)
    {
        if (string.IsNullOrWhiteSpace(d.Name)) d.Name = "Локация";
        int n = d.Cols * d.Rows;
        if (d.Fog == null || d.Fog.Count != n)
        {
            // новая карта открыта: мастер сам включит темноту кнопкой «Скрыть всё»
            d.Fog = new List<bool>(new bool[n]);
            for (int i = 0; i < n; i++) d.Fog[i] = true;
        }
        if (d.Explored == null || d.Explored.Count != n)
            d.Explored = new List<bool>(new bool[n]);
    }
}
