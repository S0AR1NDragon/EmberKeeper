using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EmberKeeper;

public sealed class Combatant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Боец";
    public int Init { get; set; }
    public int Hp { get; set; } = 10;
    public int MaxHp { get; set; } = 10;
    public int Ac { get; set; } = 10;
    public bool IsPc { get; set; }
}

public static class InitStore
{
    public static string Path => AppPaths.File("init.json");

    public static List<Combatant> Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<List<Combatant>>(File.ReadAllText(Path)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(List<Combatant> list)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
