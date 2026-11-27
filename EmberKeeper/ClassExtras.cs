using System;
using System.Collections.Generic;
using System.Linq;

namespace EmberKeeper;

/// <summary>
/// Дополнительные вкладки классов: списки опций (воззвания колдуна, боевые стили,
/// метамагия чародея, инфузии изобретателя и т.п.), разобранные из текста dnd.su.
/// </summary>
public static class ClassExtras
{
    public sealed class Option
    {
        [System.Text.Json.Serialization.JsonPropertyName("n")] public string Name { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("h")] public string Text { get; set; } = "";
    }

    public sealed class Section
    {
        public string Tab { get; set; } = "";
        public List<Option> Items { get; set; } = new();
    }

    private sealed record Spec(string Tab, string[] Keywords);

    private sealed class FileEntry
    {
        public string Edition { get; set; } = "";
        public string Cls { get; set; } = "";
        public string Tab { get; set; } = "";
        public List<Option> Items { get; set; } = new();
    }

    private static List<FileEntry>? _file;

    /// <summary>Разделы, выгруженные с dnd.su (data/class-options.json).</summary>
    private static List<FileEntry> FileData()
    {
        if (_file != null) return _file;
        _file = new List<FileEntry>();
        try
        {
            var path = System.IO.Path.Combine(DataStore.Dir, "class-options.json");
            if (System.IO.File.Exists(path))
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<FileEntry>>(System.IO.File.ReadAllText(path),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (list != null) _file = list;
            }
        }
        catch { }
        return _file;
    }

    private static readonly Dictionary<string, Spec[]> Map = new(StringComparer.CurrentCultureIgnoreCase)
    {
        ["Колдун"] = new[] { new Spec("Воззвания", new[] { "таинственные воззвания", "воззвания" }) },
        ["Чародей"] = new[] { new Spec("Метамагия", new[] { "метамагия", "варианты метамагии" }) },
        ["Воин"] = new[] { new Spec("Боевые стили", new[] { "боевой стиль", "боевые стили" }) },
        ["Паладин"] = new[] { new Spec("Боевые стили", new[] { "боевой стиль", "боевые стили" }) },
        ["Следопыт"] = new[] { new Spec("Боевые стили", new[] { "боевой стиль", "боевые стили" }) },
        ["Изобретатель"] = new[] { new Spec("Инфузии", new[] { "инфузии", "предметные инфузии" }) },
        ["Артефактор"] = new[] { new Spec("Инфузии", new[] { "инфузии", "предметные инфузии" }) },
        ["Монах"] = new[] { new Spec("Дисциплина", new[] { "дисциплина ки", "ци и магия" }) },
    };

    /// <summary>Боевые стили 2024 — это отдельные черты, берём их из библиотеки черт.</summary>
    private static readonly HashSet<string> FightingStyleNames = new(StringComparer.CurrentCultureIgnoreCase)
    {
        "Оборона", "Дуэлянт", "Защита", "Перехват", "Сражение без оружия",
        "Сражение большим оружием", "Сражение вслепую", "Сражение двумя оружиями",
        "Сражение метательным оружием", "Стрельба", "Воин-маг",
    };

    /// <summary>Все дополнительные разделы класса.</summary>
    public static List<Section> For(LibEntry x, string edition)
    {
        var res = new List<Section>();

        // сначала разделы, скачанные с dnd.su
        var name = x.N.Trim();
        foreach (var fe in FileData())
        {
            if (!string.Equals(fe.Edition, edition, StringComparison.Ordinal)) continue;
            if (!string.Equals(fe.Cls, name, StringComparison.CurrentCultureIgnoreCase)) continue;
            if (fe.Items.Count < 2) continue;
            res.Add(new Section { Tab = fe.Tab, Items = fe.Items });
        }

        if (!Map.TryGetValue(name, out var specs)) return res;

        var text = x.H ?? "";
        foreach (var spec in specs)
        {
            // боевые стили 2024 — отдельные черты
            if (spec.Tab == "Боевые стили" && edition == "2024")
            {
                var feats = DataStore.Load("2024", "feats");
                var items = feats
                    .Where(f => FightingStyleNames.Contains(f.N.Trim()))
                    .Select(f => new Option { Name = f.N, Text = f.H })
                    .ToList();
                if (items.Count > 0) res.Add(new Section { Tab = spec.Tab, Items = items });
                continue;
            }

            if (res.Any(r => r.Tab == spec.Tab)) continue;
            var parsed = ParseSection(text, spec.Keywords);
            if (parsed.Count >= 2) res.Add(new Section { Tab = spec.Tab, Items = parsed });
        }
        return res;
    }

    /// <summary>Разбор раздела «заголовок + список название/описание».</summary>
    private static List<Option> ParseSection(string text, string[] keywords)
    {
        var result = new List<Option>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();
        int start = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || line.Length > 90) continue;
            var low = line.ToLowerInvariant();
            bool hit = keywords.Any(k => low.Contains(k));
            if (!hit) continue;
            // пропускаем упоминания в таблицах/сводках (строки с цифрами — это строки таблиц)
            if (low.Contains("таблиц") || low.Contains("перечислен")) continue;
            if (char.IsDigit(line[0])) continue;
            int digits = line.Count(char.IsDigit);
            if (digits > 2 || line.Contains("----")) continue;
            int next = NextNonEmpty(lines, i + 1);
            if (next < 0) continue;
            var nextLine = lines[next];
            // строки-таблицы: «1+212211», «2+23232» и т.п.
            if (System.Text.RegularExpressions.Regex.IsMatch(nextLine, @"^[\d+\-\s]+$")) continue;
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^[\d+\-\s,.]+$")) continue;
            bool titleNext = !nextLine.EndsWith('.') && !nextLine.EndsWith(',') && !nextLine.EndsWith(';') && nextLine.Length <= 90;
            if (IsHeading(line) || titleNext)
            {
                start = next;
                break;
            }
        }
        if (start < 0) return result;

        string? currentName = null;
        var buffer = new System.Text.StringBuilder();

        void Flush()
        {
            if (currentName == null) return;
            var body = buffer.ToString().Trim();
            if (body.Length > 0) result.Add(new Option { Name = currentName, Text = body });
            currentName = null;
            buffer.Clear();
        }

        for (int i = start; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0) { buffer.AppendLine(); continue; }
            if (line.Length > 200) { buffer.AppendLine(line); continue; }

            // конец раздела: общий заголовок вида «УМЕНИЯ КЛАССА» или «N-й уровень, умение…»
            if (IsHeading(line) && currentName != null && line.All(c => !char.IsLetter(c) || char.IsUpper(c)) && line.Length > 12)
            {
                Flush();
                break;
            }
            if (line.StartsWith("Уровень ", StringComparison.OrdinalIgnoreCase) ||
                (line.Length > 3 && char.IsDigit(line[0]) && line.Contains("уровень", StringComparison.OrdinalIgnoreCase)))
            {
                Flush();
                if (result.Count >= 2) break;
                continue;
            }

            bool looksLikeTitle = line.Length <= 70 &&
                                  !line.EndsWith('.') && !line.EndsWith(',') && !line.EndsWith(';') &&
                                  char.IsUpper(line[0]) &&
                                  !line.StartsWith("•") && !line.StartsWith("- ") &&
                                  !line.Contains("  ");

            if (looksLikeTitle && (currentName == null || buffer.Length > 0))
            {
                Flush();
                currentName = line;
                continue;
            }

            if (currentName != null) buffer.AppendLine(line);
        }
        Flush();
        return result;
    }

    private static int NextNonEmpty(List<string> lines, int from)
    {
        for (int i = from; i < lines.Count; i++)
            if (lines[i].Length > 0) return i;
        return -1;
    }

    private static bool IsHeading(string line)
    {
        if (line.Length < 3 || line.Length > 90) return false;
        if (line.EndsWith('.') || line.EndsWith(',')) return false;
        int letters = 0, upper = 0;
        foreach (var ch in line)
        {
            if (!char.IsLetter(ch)) continue;
            letters++;
            if (char.IsUpper(ch)) upper++;
        }
        return letters >= 3 && upper == letters;
    }
}
