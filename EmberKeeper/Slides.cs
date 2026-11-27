using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EmberKeeper;

public sealed class SceneElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Image { get; set; } = "";
    public double X { get; set; } = 40;   // проценты от ширины сцены
    public double Y { get; set; } = 40;
    public double W { get; set; } = 22;
    public double H { get; set; } = 22;
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1.0;
}

public sealed class Scene
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Сцена";
    public string Subtitle { get; set; } = "";
    public string Text { get; set; } = "";
    public string Image { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<SceneElement> Elements { get; set; } = new();
}

public static class SceneStore
{
    public static string Path => AppPaths.File("scenes.json");

    public static List<Scene> Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var list = JsonSerializer.Deserialize<List<Scene>>(File.ReadAllText(Path)) ?? new();
                foreach (var s in list) s.Elements ??= new List<SceneElement>();
                return list;
            }
        }
        catch { }
        return new();
    }

    public static void Save(List<Scene> list)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Dir);
            File.WriteAllText(Path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
