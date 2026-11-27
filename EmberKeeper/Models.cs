using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmberKeeper;

public sealed class LibEntry
{
    [JsonPropertyName("n")] public string N { get; set; } = "";
    [JsonPropertyName("e")] public string E { get; set; } = "";
    [JsonPropertyName("s")] public string S { get; set; } = "";
    [JsonPropertyName("h")] public string H { get; set; } = "";
    [JsonPropertyName("p")] public List<string> P { get; set; } = new();
    [JsonPropertyName("tb")] public List<List<string>>? Tb { get; set; }
    [JsonPropertyName("lvl")] public int? Lvl { get; set; }
    [JsonPropertyName("sch")] public string Sch { get; set; } = "";
    [JsonPropertyName("cls")] public List<string> Cls { get; set; } = new();
    [JsonPropertyName("rar")] public string Rar { get; set; } = "";
    [JsonPropertyName("typ")] public string Typ { get; set; } = "";
    [JsonPropertyName("cr")] public string Cr { get; set; } = "";
    [JsonPropertyName("mt")] public string Mt { get; set; } = "";
    [JsonPropertyName("at")] public int At { get; set; }
    [JsonPropertyName("src")] public string Src { get; set; } = "";

    public string LevelText => Lvl == null ? "" : (Lvl == 0 ? "Заговор" : $"{Lvl} ур.");
    public string Sub => string.IsNullOrEmpty(S) ? E : S;
}

public static class DataStore
{
    public static string Dir = System.IO.Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly Dictionary<string, List<LibEntry>> Cache = new();

    public static List<LibEntry> Load(string edition, string cat)
    {
        string key = $"{edition}-{cat}";
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var list = new List<LibEntry>();
        try
        {
            string path = System.IO.Path.Combine(Dir, key + ".json");
            if (File.Exists(path))
                list = JsonSerializer.Deserialize<List<LibEntry>>(File.ReadAllText(path)) ?? new();
        }
        catch { }
        Cache[key] = list;
        return list;
    }

    public static Dictionary<string, int> LoadCounts()
    {
        var res = new Dictionary<string, int>();
        try
        {
            string path = System.IO.Path.Combine(Dir, "manifest.json");
            if (!File.Exists(path)) return res;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var year in new[] { "2014", "2024" })
            {
                if (!doc.RootElement.TryGetProperty(year, out var arr)) continue;
                foreach (var it in arr.EnumerateArray())
                {
                    res[it.GetProperty("key").GetString() ?? ""] = it.GetProperty("count").GetInt32();
                }
            }
        }
        catch { }
        return res;
    }
}
