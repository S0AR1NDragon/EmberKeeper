using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EmberKeeper;

public sealed class Npc
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "НПС";
    public string Race { get; set; } = "";
    public string Class { get; set; } = "";
    public int Hp { get; set; } = 10;
    public int Ac { get; set; } = 10;
    public string Tags { get; set; } = "";
    public string Faction { get; set; } = "";
    public string City { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Campaign { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Music { get; set; } = "";
}

public static class NpcStore
{
    public static string Path => AppPaths.File("npcs.json");

    public static List<Npc> Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<List<Npc>>(File.ReadAllText(Path)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(List<Npc> list)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
