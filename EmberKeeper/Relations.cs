using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EmberKeeper;

public sealed class RelNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Персонаж";
    public string Kind { get; set; } = "npc"; // hero | npc | place | faction
    public double X { get; set; } = 120;
    public double Y { get; set; } = 120;
    public string Note { get; set; } = "";
}

public sealed class RelLink
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "ally"; // ally | enemy | family | debt
}

public sealed class RelationMap
{
    public List<RelNode> Nodes { get; set; } = new();
    public List<RelLink> Links { get; set; } = new();
}

public static class RelationStore
{
    public static string Path => AppPaths.File("relations.json");

    public static RelationMap Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var m = JsonSerializer.Deserialize<RelationMap>(File.ReadAllText(Path));
                if (m != null) return m;
            }
        }
        catch { }
        return new RelationMap();
    }

    public static void Save(RelationMap map)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Dir);
            File.WriteAllText(Path, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
