using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmberKeeper;

/// <summary>
/// Справочная информация по классам: базовые данные (кость хитов, спасброски, снаряжение)
/// и разбор умений класса/подкласса из текста dnd.su.
/// </summary>
public static class ClassInfo
{
    public sealed record Base(string HitDie, string Saves, string Skills, string Armor, string Weapons, string Equipment);

    /// <summary>Базовые данные классов (2014): на dnd.su это отдельная боковая таблица, в тексте её нет.</summary>
    private static readonly Dictionary<string, Base> Base2014 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Бард"] = new("к8", "Ловкость, Харизма", "Три навыка на выбор",
            "Лёгкие доспехи", "Простое оружие, длинные мечи, короткие мечи, рапиры, ручные арбалеты",
            "Кожаный доспех, кинжал, музыкальный инструмент, набор путешественника, 5 зм"),
        ["Боец"] = new("к10", "Сила, Телосложение", "Два навыка на выбор",
            "Все доспехи, щиты", "Простое и воинское оружие",
            "Кольчуга или кожаный доспех, воинское оружие и щит, арбалет и 20 болтов, набор исследователя, 10 зм"),
        ["Воин"] = new("к10", "Сила, Телосложение", "Два навыка на выбор",
            "Все доспехи, щиты", "Простое и воинское оружие",
            "Кольчуга или кожаный доспех, воинское оружие и щит, арбалет и 20 болтов, набор исследователя, 10 зм"),
        ["Варвар"] = new("к12", "Сила, Телосложение", "Два навыка на выбор",
            "Лёгкие и средние доспехи, щиты", "Простое и воинское оружие",
            "Боевой топор или воинское оружие, два ручных топора, набор исследователя, 10 зм"),
        ["Волшебник"] = new("к6", "Интеллект, Мудрость", "Два навыка на выбор",
            "Нет", "Кинжалы, дротики, пращи, боевые посохи, лёгкие арбалеты",
            "Боевой посох или кинжал, книга заклинаний, набор учёного, 10 зм"),
        ["Друид"] = new("к8", "Интеллект, Мудрость", "Два навыка на выбор",
            "Лёгкие и средние доспехи, щиты (без металла)", "Дубинки, кинжалы, дротики, копья, пращи, серпы, боевые посохи",
            "Кожаный доспех, щит, серп или другое простое оружие, набор путешественника, 10 зм"),
        ["Жрец"] = new("к8", "Мудрость, Харизма", "Два навыка на выбор",
            "Лёгкие и средние доспехи, щиты", "Простое оружие",
            "Щит, священный символ, набор священника, 15 зм"),
        ["Изобретатель"] = new("к8", "Интеллект, Телосложение", "Два навыка на выбор",
            "Лёгкие и средние доспехи, щиты", "Простое оружие",
            "Кожаный доспех, простое оружие, набор учёного, 10 зм"),
        ["Колдун"] = new("к8", "Мудрость, Харизма", "Два навыка на выбор",
            "Лёгкие доспехи", "Простое оружие",
            "Кожаный доспех, простое оружие, набор учёного, 10 зм"),
        ["Монах"] = new("к8", "Сила, Ловкость", "Два навыка на выбор",
            "Нет", "Простое оружие, короткие мечи",
            "Десять дротиков, набор путешественника, 5 зм"),
        ["Паладин"] = new("к10", "Мудрость, Харизма", "Два навыка на выбор",
            "Все доспехи, щиты", "Простое и воинское оружие",
            "Воинское оружие и щит, пять метательных копий, набор священника, 10 зм"),
        ["Плут"] = new("к8", "Ловкость, Интеллект", "Четыре навыка на выбор",
            "Лёгкие доспехи", "Простое оружие, короткие мечи, ручные арбалеты",
            "Короткий меч или рапира, короткий лук и колчан, набор взломщика, 15 зм"),
        ["Следопыт"] = new("к10", "Сила, Ловкость", "Три навыка на выбор",
            "Лёгкие и средние доспехи, щиты", "Простое и воинское оружие",
            "Чешуйчатый доспех, два коротких меча или оружие, набор исследователя, 10 зм"),
        ["Чародей"] = new("к6", "Телосложение, Харизма", "Два навыка на выбор",
            "Нет", "Кинжалы, дротики, пращи, боевые посохи, лёгкие арбалеты",
            "Кинжал, набор исследователя, 5 зм"),
        ["Эксперт"] = new("к8", "Ловкость, Интеллект", "Два навыка на выбор",
            "Лёгкие доспехи", "Простое оружие",
            "Простое оружие, набор путешественника, 10 зм"),
        ["Заклинатель"] = new("к8", "Мудрость, Харизма", "Два навыка на выбор",
            "Лёгкие доспехи", "Простое оружие",
            "Простое оружие, компонентная сумка, набор учёного, 10 зм"),
    };

    /// <summary>Базовые характеристики класса для таблицы.</summary>
    public static List<(string Label, string Value)> Basics(LibEntry x, string edition)
    {
        var res = new List<(string, string)>();

        // 2024: данные лежат в тексте («Ключевые атрибуты»)
        var parsed = ParseKeyAttributes(x.H);
        if (parsed.Count > 0)
        {
            res.AddRange(parsed);
            return res;
        }

        var b = FindBase(x.N);
        if (b == null) return res;
        res.Add(("Кость хитов", b.HitDie));
        res.Add(("Спасброски", b.Saves));
        res.Add(("Навыки", b.Skills));
        res.Add(("Доспехи", b.Armor));
        res.Add(("Оружие", b.Weapons));
        res.Add(("Начальное снаряжение", b.Equipment));
        return res;
    }

    private static Base? FindBase(string name)
    {
        foreach (var kv in Base2014)
            if (name.StartsWith(kv.Key, StringComparison.CurrentCultureIgnoreCase) ||
                name.Contains(kv.Key, StringComparison.CurrentCultureIgnoreCase))
                return kv.Value;
        return null;
    }

    private static bool IsKnownLabel(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 60) return false;
        return line.StartsWith("Кость хитов", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Владение", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Основная характеристика", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Хиты за уровень", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Снаряжение", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Начальное снаряжение", StringComparison.OrdinalIgnoreCase) ||
               line.Equals("Умения", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Ключевые атрибуты", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Разбор «Ключевые атрибуты» из текста 2024.</summary>
    private static List<(string, string)> ParseKeyAttributes(string text)
    {
        var res = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(text)) return res;

        int at = text.IndexOf("Ключевые атрибуты", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return res;
        var tail = text.Substring(at);
        var lines = tail.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count > 60) lines = lines.Take(60).ToList();

        for (int i = 0; i + 1 < lines.Count; i++)
        {
            if (!IsKnownLabel(lines[i])) continue;
            var label = lines[i];
            var value = lines[i + 1];
            int j = i + 2;

            // многострочные значения: снаряжение, навыки, владения
            bool multi = label.Contains("снаряжение", StringComparison.OrdinalIgnoreCase) ||
                         label.Contains("навык", StringComparison.OrdinalIgnoreCase) ||
                         label.Contains("оруж", StringComparison.OrdinalIgnoreCase) ||
                         label.Contains("доспех", StringComparison.OrdinalIgnoreCase);
            if (multi)
            {
                var sb = new System.Text.StringBuilder(value);
                int added = 0;
                while (j < lines.Count && added < 6 && !IsKnownLabel(lines[j]))
                {
                    var nxt = lines[j];
                    if (sb.Length + nxt.Length > 420) break;
                    sb.Append(" ").Append(nxt);
                    j++;
                    added++;
                }
                value = sb.ToString();
            }

            if (value.Length > 500) value = value[..500];
            res.Add((label.Replace("Владение ", ""), value));
            i = j - 1;
        }
        return res;
    }

    /// <summary>Общие умения класса из таблицы прогрессии (колонка «Умения»).</summary>
    public static List<(int Level, string Title)> ClassFeatures(LibEntry x)
    {
        var res = new List<(int, string)>();
        if (x.Tb == null || x.Tb.Count < 3) return res;

        // шапка может начинаться с пустых ячеек — данные сдвинуты влево на их число
        var header = x.Tb[0];
        int offset = 0;
        while (offset < header.Count && string.IsNullOrWhiteSpace(header[offset])) offset++;

        int featureCol = -1;
        int levelCol = -1;
        for (int c = 0; c < header.Count; c++)
        {
            var v = header[c] ?? "";
            if (featureCol < 0 && (v.Contains("Умения", StringComparison.OrdinalIgnoreCase) || v.Contains("Классовые умения", StringComparison.OrdinalIgnoreCase))) featureCol = c - offset;
            if (levelCol < 0 && v.Trim().Equals("Уровень", StringComparison.OrdinalIgnoreCase)) levelCol = c - offset;
        }
        if (featureCol < 0) return res;
        if (levelCol < 0) levelCol = 0;

        foreach (var row in x.Tb)
        {
            if (row == null) continue;
            if (featureCol >= row.Count) continue;
            var lvText = levelCol < row.Count ? (row[levelCol] ?? "").Trim() : "";
            if (!int.TryParse(lvText, out int lv) || lv < 1 || lv > 20) continue;
            var feat = (row[featureCol] ?? "").Trim();
            if (feat.Length == 0 || feat == "-") continue;
            res.Add((lv, feat));
        }
        return res;
    }

    /// <summary>Текст класса без описаний подклассов (на dnd.su они идут следом).</summary>
    public static string ClassText(LibEntry x)
    {
        var h = x.H ?? "";
        // описание заканчивается там, где начинаются умения по уровням
        var m = Regex.Match(h, @"^\s*Уровень\s*1\s*:", RegexOptions.Multiline);
        if (m.Success && m.Index > 200) return h[..m.Index].TrimEnd();

        int cut = -1;
        foreach (var sub in x.Subs ?? new List<LibSub>())
        {
            if (string.IsNullOrWhiteSpace(sub.N) || sub.N.Length < 4) continue;
            int idx = h.IndexOf(sub.N, StringComparison.OrdinalIgnoreCase);
            if (idx > 200 && (cut < 0 || idx < cut)) cut = idx;
        }
        return cut > 0 ? h[..cut].TrimEnd() : h;
    }

    /// <summary>Умения класса из текста: «Уровень 3: Название» (стиль 2024).</summary>
    public static List<(int Level, string Title, string Body)> FeaturesFromText(string text)
    {
        var res = new List<(int, string, string)>();
        if (string.IsNullOrWhiteSpace(text)) return res;
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            var m = Regex.Match(lines[i], @"^Уровень\s*(\d{1,2})\s*[:\-–]\s*(.+)$", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            int level = int.Parse(m.Groups[1].Value);
            var title = m.Groups[2].Value.Trim();
            var body = new System.Text.StringBuilder();
            int j = i + 1;
            while (j < lines.Count && lines[j].Length > 0 && !Regex.IsMatch(lines[j], @"^Уровень\s*\d", RegexOptions.IgnoreCase))
            {
                body.AppendLine(lines[j]);
                j++;
            }
            res.Add((level, title, body.ToString().Trim()));
        }

        // умения класса идут по возрастанию уровней; подклассы начинаются заново с 3-го
        var ordered = new List<(int, string, string)>();
        int prev = 0;
        foreach (var item in res)
        {
            if (item.Item1 < prev) break;
            ordered.Add(item);
            prev = item.Item1;
        }
        return ordered;
    }

    /// <summary>Все умения: класса по уровням, плюс подкласса (или заглушки «Умение подкласса»).</summary>
    public static List<(int Level, string Title, string Body, bool IsSub)> AllFeatures(LibEntry x, LibSub? sub)
    {
        var list = new List<(int, string, string, bool)>();

        var fromTable = ClassFeatures(x);
        if (fromTable.Count > 0)
        {
            foreach (var (level, title) in fromTable) list.Add((level, title, "", false));
        }
        else
        {
            foreach (var (level, title, body) in FeaturesFromText(ClassText(x))) list.Add((level, title, body, false));
        }

        if (sub != null)
        {
            foreach (var (level, title, body) in SubclassFeatures(sub.H)) list.Add((level, title, body, true));
        }
        else if (x.Subs is { Count: > 0 })
        {
            var levels = new SortedSet<int>();
            foreach (var s in x.Subs)
                foreach (var f in SubclassFeatures(s.H)) levels.Add(f.Level);
            if (levels.Count == 0) { levels.Add(3); levels.Add(6); levels.Add(10); levels.Add(14); }
            foreach (var lv in levels) list.Add((lv, "", "", true));
        }

        return list.OrderBy(f => f.Item1).ThenBy(f => f.Item4 ? 1 : 0).ToList();
    }

    /// <summary>Умения подкласса, разобранные из текста (заголовки + уровни).</summary>
    public static List<(int Level, string Title, string Body)> SubclassFeatures(string text)
    {
        var res = new List<(int, string, string)>();
        if (string.IsNullOrWhiteSpace(text)) return res;

        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();
        int i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (line.Length == 0) { i++; continue; }

            int level = 0;
            string title = "";

            // «Уровень 3: Инструменты специализации»
            var m1 = Regex.Match(line, @"^Уровень\s*(\d{1,2})\s*[:\-–]\s*(.+)$", RegexOptions.IgnoreCase);
            if (m1.Success)
            {
                level = int.Parse(m1.Groups[1].Value);
                title = m1.Groups[2].Value.Trim();
                i++;
            }
            else if (IsHeading(line))
            {
                title = line;
                // ищем уровень в следующей строке: «3-й уровень, умение коллегии доблести»
                int j = i + 1;
                if (j < lines.Count)
                {
                    var m2 = Regex.Match(lines[j], @"(\d{1,2})\s*[-‑–]?\s*[йяо]?\s*уровень", RegexOptions.IgnoreCase);
                    if (m2.Success)
                    {
                        level = int.Parse(m2.Groups[1].Value);
                        j++;
                    }
                }
                i = j;
            }
            else { i++; continue; }

            var body = new System.Text.StringBuilder();
            while (i < lines.Count && lines[i].Length > 0 && !IsHeading(lines[i]) &&
                   !Regex.IsMatch(lines[i], @"^Уровень\s*\d", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(lines[i], @"^\d{1,2}\s*[-‑–]?\s*[йяо]?\s*уровень", RegexOptions.IgnoreCase)) { i++; continue; }
                body.AppendLine(lines[i]);
                i++;
            }

            var textBody = body.ToString().Trim();
            if (title.Length > 0 && (textBody.Length > 0 || level > 0))
                res.Add((level, ToTitleCase(title), textBody));
        }
        return res;
    }

    private static bool IsHeading(string line)
    {
        if (line.Length < 3 || line.Length > 80) return false;
        if (line.EndsWith('.') || line.EndsWith(',') || line.EndsWith(':')) return false;
        if (line.Any(char.IsDigit)) return false;
        int letters = 0, upper = 0;
        foreach (var ch in line)
        {
            if (!char.IsLetter(ch)) continue;
            letters++;
            if (char.IsUpper(ch)) upper++;
        }
        return letters >= 3 && upper == letters;
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        s = s.Trim();
        if (s.All(char.IsUpper) || s.All(c => !char.IsLetter(c) || char.IsUpper(c)))
        {
            var lower = s.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower[1..];
        }
        return s;
    }

    /// <summary>Кость хитов для создания персонажа.</summary>
    public static int HitDie(LibEntry x, string edition)
    {
        var basics = Basics(x, edition);
        foreach (var (label, value) in basics)
        {
            if (!label.Contains("Кость хитов", StringComparison.OrdinalIgnoreCase)) continue;
            var m = Regex.Match(value, @"[кКkK]?\s*(\d{1,2})");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int d) && d >= 4 && d <= 20) return d;
        }
        return 8;
    }
}
