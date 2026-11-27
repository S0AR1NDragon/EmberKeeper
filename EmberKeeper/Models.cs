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
    [JsonPropertyName("sub")] public List<LibSub>? Subs { get; set; }

    public string LevelText => Lvl == null ? "" : (Lvl == 0 ? "Заговор" : $"{Lvl} ур.");
    public string Sub => string.IsNullOrEmpty(S) ? E : S;
}

public sealed class LibSub
{
    [JsonPropertyName("n")] public string N { get; set; } = "";
    [JsonPropertyName("h")] public string H { get; set; } = "";
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
            {
                list = JsonSerializer.Deserialize<List<LibEntry>>(File.ReadAllText(path)) ?? new();
                foreach (var e in list) Clean(e);
            }
        }
        catch { }
        Cache[key] = list;
        return list;
    }

    private static readonly Dictionary<string, string> Entities = new()
    {
        ["&thinsp;"] = " ", ["&nbsp;"] = " ", ["&amp;"] = "&", ["&quot;"] = "\"",
        ["&lt;"] = "<", ["&gt;"] = ">", ["&mdash;"] = "—", ["&ndash;"] = "–",
        ["&laquo;"] = "«", ["&raquo;"] = "»", ["&hellip;"] = "…", ["&#39;"] = "'",
        ["&apos;"] = "'", ["&times;"] = "×", ["&middot;"] = "·", ["&deg;"] = "°",
        ["&minus;"] = "−", ["&plusmn;"] = "±", ["&rsquo;"] = "’", ["&lsquo;"] = "‘",
        ["&rdquo;"] = "”", ["&ldquo;"] = "“", ["&bdquo;"] = "„", ["&frac12;"] = "½",
        ["&frac14;"] = "¼", ["&sup2;"] = "²", ["&sup3;"] = "³",
    };

    /// <summary>Убирает HTML-сущности и теги, оставляя читаемый текст.</summary>
    private static string CleanText(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var s = raw.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n")
                   .Replace("</p>", "\n").Replace("</li>", "\n").Replace("<li>", "• ");
        foreach (var kv in Entities) s = s.Replace(kv.Key, kv.Value);
        s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]{1,80}>", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"&#(\d{1,5});", m =>
        {
            int code = int.Parse(m.Groups[1].Value);
            return code < 0x10000 ? ((char)code).ToString() : "";
        });
        s = s.Replace("&nbsp;", " ").Replace("&thinsp;", " ");
        while (s.Contains("\n\n\n")) s = s.Replace("\n\n\n", "\n\n");
        return s.Trim();
    }

    private static void Clean(LibEntry e)
    {
        e.N = CleanText(e.N);
        e.E = CleanText(e.E);
        e.S = CleanText(e.S);
        e.H = CleanText(e.H);
        for (int i = 0; i < e.P.Count; i++) e.P[i] = CleanText(e.P[i]);
        if (e.Tb != null)
            for (int r = 0; r < e.Tb.Count; r++)
                for (int c = 0; c < e.Tb[r].Count; c++) e.Tb[r][c] = CleanText(e.Tb[r][c]);
        if (e.Subs != null)
            foreach (var sb in e.Subs) { sb.N = CleanText(sb.N); sb.H = CleanText(sb.H); }
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
