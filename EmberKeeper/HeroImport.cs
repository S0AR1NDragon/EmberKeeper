using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EmberKeeper;

/// <summary>Импорт персонажей из JSON: свой формат, Long Story Short и произвольный JSON листа.</summary>
public static class HeroImport
{
    public static List<Hero> FromJsonAny(string json)
    {
        var result = new List<Hero>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray())
                {
                    var h = FromElement(el);
                    if (h != null) result.Add(h);
                }
            }
            else
            {
                var h = FromElement(root);
                if (h != null) result.Add(h);
            }
        }
        catch { }
        return result;
    }

    private static Hero? FromElement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        try
        {
            if (IsLss(root)) return FromLss(root);

            // наш собственный формат (есть Str/Dex и Slots) — просто десериализуем
            if (root.TryGetProperty("Str", out _) || root.TryGetProperty("AbilKeys", out _) ||
                (root.TryGetProperty("Saves", out var sv) && sv.ValueKind == JsonValueKind.String && root.TryGetProperty("Slots", out _)))
            {
                var h = JsonSerializer.Deserialize<Hero>(root.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (h != null) { HeroStore.Normalize(h); return h; }
            }

            return FromGeneric(root);
        }
        catch { return null; }
    }

    /* ---------------- generic ---------------- */

    private static Hero FromGeneric(JsonElement j)
    {
        var h = new Hero
        {
            Name = Str(j, "name", "Name", "имя", "Имя", "charName") ?? "Безымянный",
            Race = Str(j, "race", "Race", "вид", "Вид"),
            Class = Str(j, "class", "Class", "klass", "класс", "Класс"),
            Subclass = Str(j, "subclass", "Subclass", "подкласс"),
            Background = Str(j, "background", "Background", "предыстория"),
            PlayerName = Str(j, "playerName", "player", "игрок"),
            Portrait = Str(j, "portrait", "Portrait", "avatar", "image"),
            Notes = Str(j, "notes", "Notes", "заметки"),
            Equipment = Str(j, "equipment", "Equipment", "снаряжение"),
            Spells = Str(j, "spells", "Spells", "заклинания"),
            Attacks = Str(j, "attacks", "Attacks", "атаки"),
            Features = Str(j, "features", "Features", "умения", "особенности"),
            Feats = Str(j, "feats", "Feats", "черты"),
            Allies = Str(j, "allies", "Allies", "союзники"),
            Goals = Str(j, "goals", "Goals", "цели"),
            Traits = Str(j, "traits", "Traits"),
            Faction = Str(j, "faction", "Faction", "фракция"),
            City = Str(j, "city", "City", "город"),
            Organization = Str(j, "organization", "Organization", "орден", "организация"),
            Campaign = Str(j, "campaign", "Campaign", "кампания"),
            Tags = Str(j, "tags", "Tags", "теги"),
        };

        int lvl = Num(j, 1, "level", "Level", "lvl", "уровень");
        h.Level = Math.Clamp(lvl, 1, 20).ToString();
        h.Ac = Num(j, 10, "ac", "AC", "armor", "кд", "КД");
        h.MaxHp = Math.Max(1, Num(j, 10, "maxHp", "maxhp", "hpMax", "hitPoints", "hp", "хиты"));
        h.Hp = Math.Max(1, Num(j, h.MaxHp, "hp", "Hp", "currentHp", "хиты"));
        h.Init = Num(j, 0, "init", "Init", "initiative", "инициатива");
        h.Speed = Num(j, 30, "speed", "Speed", "скорость");
        h.Darkvision = Num(j, 0, "darkvision", "Darkvision", "темноеЗрение");
        h.SpellDc = Num(j, 0, "spellDC", "spellDc", "spellSaveDC", "сл");
        h.SpellAtk = Num(j, 0, "spellAttack", "spellAtk", "spellAttackBonus");

        // характеристики
        var ab = DigFirst(j, "abilities", "stats", "характеристики", "attributes");
        if (ab is JsonElement a && a.ValueKind == JsonValueKind.Object)
        {
            h.Str = AbilityFrom(a, "str", "strength", "сила");
            h.Dex = AbilityFrom(a, "dex", "dexterity", "ловкость");
            h.Con = AbilityFrom(a, "con", "constitution", "телосложение");
            h.Int = AbilityFrom(a, "int", "intelligence", "интеллект");
            h.Wis = AbilityFrom(a, "wis", "wisdom", "мудрость");
            h.Cha = AbilityFrom(a, "cha", "charisma", "харизма");
        }
        else
        {
            h.Str = Num(j, 10, "str", "Str", "strength", "сила");
            h.Dex = Num(j, 10, "dex", "Dex", "dexterity", "ловкость");
            h.Con = Num(j, 10, "con", "Con", "constitution", "телосложение");
            h.Int = Num(j, 10, "int", "Int", "intelligence", "интеллект");
            h.Wis = Num(j, 10, "wis", "Wis", "wisdom", "мудрость");
            h.Cha = Num(j, 10, "cha", "Cha", "charisma", "харизма");
        }

        // спасброски и навыки
        var savesEl = DigFirst(j, "saves", "savingThrows", "спасброски");
        if (savesEl is JsonElement sEl)
        {
            if (sEl.ValueKind == JsonValueKind.Object)
            {
                var on = new List<string>();
                foreach (var k in Hero.AbilKeys)
                    if (sEl.TryGetProperty(k, out var v) && Truthy(v)) on.Add(k);
                h.Saves = string.Join(",", on);
            }
            else if (sEl.ValueKind == JsonValueKind.String)
                h.Saves = sEl.GetString() ?? "";
        }

        var skillsEl = DigFirst(j, "skills", "навыки");
        if (skillsEl is JsonElement kEl)
        {
            if (kEl.ValueKind == JsonValueKind.Object)
            {
                var on = new List<string>();
                foreach (var s in Hero.SkillsList)
                {
                    if (!kEl.TryGetProperty(s.Key, out var v)) continue;
                    bool proficient = v.ValueKind == JsonValueKind.Object
                        ? Truthy(DigFirst(v, "isProf", "proficient", "prof") ?? default)
                        : Truthy(v);
                    if (proficient) on.Add(s.Key);
                }
                h.Skills = string.Join(",", on);
            }
            else if (kEl.ValueKind == JsonValueKind.String)
                h.Skills = kEl.GetString() ?? "";
        }

        // ячейки
        var slotsEl = DigFirst(j, "slots", "spellSlots", "ячейки");
        if (slotsEl is JsonElement sl && sl.ValueKind == JsonValueKind.Object)
        {
            for (int i = 1; i <= 9; i++)
            {
                if (sl.TryGetProperty(i.ToString(), out var v) && v.ValueKind == JsonValueKind.Number)
                    h.Slots[i - 1] = v.GetInt32();
            }
        }

        HeroStore.Normalize(h);
        return h;
    }

    private static int AbilityFrom(JsonElement obj, params string[] names)
    {
        foreach (var n in names)
        {
            if (!obj.TryGetProperty(n, out var v)) continue;
            if (v.ValueKind == JsonValueKind.Number) return Math.Clamp(v.GetInt32(), 1, 30);
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int p)) return Math.Clamp(p, 1, 30);
            if (v.ValueKind == JsonValueKind.Object)
            {
                var inner = DigFirst(v, "score", "value");
                if (inner is JsonElement ie && ie.ValueKind == JsonValueKind.Number) return Math.Clamp(ie.GetInt32(), 1, 30);
            }
        }
        return 10;
    }

    /* ---------------- Long Story Short ---------------- */

    private static bool IsLss(JsonElement j)
    {
        if (j.TryGetProperty("jsonType", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "character") return true;
        if (j.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.String && (d.GetString() ?? "").Contains("\"charClass\"")) return true;
        return false;
    }

    private static Hero? FromLss(JsonElement raw)
    {
        JsonElement d = raw;
        if (raw.TryGetProperty("data", out var dataEl))
        {
            if (dataEl.ValueKind == JsonValueKind.String)
            {
                try { using var inner = JsonDocument.Parse(dataEl.GetString() ?? "{}"); d = inner.RootElement.Clone(); }
                catch { }
            }
            else if (dataEl.ValueKind == JsonValueKind.Object) d = dataEl;
        }

        if (!d.TryGetProperty("info", out var info)) return null;

        var h = new Hero
        {
            Name = LssVal(d, "name") ?? "Безымянный",
            Race = LssVal(info, "race"),
            Class = LssVal(info, "charClass"),
            Subclass = LssVal(info, "charSubclass"),
            Background = LssVal(info, "background"),
            PlayerName = LssVal(info, "playerName"),
        };

        int level = ToInt(LssVal(info, "level"), 1);
        h.Level = Math.Clamp(level, 1, 20).ToString();

        if (d.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
        {
            h.Str = LssScore(stats, "str");
            h.Dex = LssScore(stats, "dex");
            h.Con = LssScore(stats, "con");
            h.Int = LssScore(stats, "int");
            h.Wis = LssScore(stats, "wis");
            h.Cha = LssScore(stats, "cha");
        }

        if (d.TryGetProperty("saves", out var saves) && saves.ValueKind == JsonValueKind.Object)
        {
            var on = new List<string>();
            foreach (var k in Hero.AbilKeys)
                if (saves.TryGetProperty(k, out var s) && Truthy(DigFirst(s, "isProf") ?? default)) on.Add(k);
            h.Saves = string.Join(",", on);
        }

        if (d.TryGetProperty("skills", out var skills) && skills.ValueKind == JsonValueKind.Object)
        {
            var on = new List<string>();
            foreach (var s in Hero.SkillsList)
            {
                if (!skills.TryGetProperty(s.Key, out var v)) continue;
                if (Truthy(DigFirst(v, "isProf") ?? default)) on.Add(s.Key);
            }
            h.Skills = string.Join(",", on);
        }

        var vit = DigFirst(d, "vitality") ?? default;
        if (vit.ValueKind == JsonValueKind.Object)
        {
            h.Ac = ToInt(LssVal(vit, "ac"), 10);
            h.MaxHp = Math.Max(1, ToInt(LssVal(vit, "hp-max"), 10));
            h.Hp = h.MaxHp;
            h.Init = ToInt(LssVal(vit, "initiative"), 0);
            h.Speed = ToInt(LssVal(vit, "speed"), 30);
            h.Darkvision = ToInt(LssVal(vit, "darkvision"), 0);
            h.HitDice = LssVal(vit, "hit-die") ?? "";
            h.HitDiceCurrent = ToInt(LssVal(vit, "hp-dice-current"), level);
            h.HitDiceMax = level;
        }

        if (d.TryGetProperty("spellsInfo", out var si))
        {
            if (si.TryGetProperty("save", out var save) && save.TryGetProperty("customModifier", out var sm) && sm.ValueKind == JsonValueKind.Number)
                h.SpellDc = sm.GetInt32();
            if (si.TryGetProperty("mod", out var mod) && mod.TryGetProperty("customModifier", out var mm) && mm.ValueKind == JsonValueKind.Number)
                h.SpellAtk = mm.GetInt32();
        }

        if (d.TryGetProperty("spells", out var sp) && sp.ValueKind == JsonValueKind.Object)
        {
            for (int i = 1; i <= 9; i++)
                if (sp.TryGetProperty("slots-" + i, out var slot))
                    h.Slots[i - 1] = ToInt(LssVal(slot, "value"), 0);
        }

        if (d.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.Object)
        {
            h.Notes = LssText(text, "notes-1");
            h.Equipment = LssText(text, "prof");
            h.Features = LssText(text, "traits");
            h.Attacks = LssText(text, "attacks");
            h.Feats = LssText(text, "feats");
            h.Allies = LssText(text, "allies");
            h.Goals = LssText(text, "goals");
            h.Traits = h.Features;

            var spells = new StringBuilder();
            for (int i = 0; i <= 9; i++)
            {
                var body = LssText(text, "spells-level-" + i);
                if (!string.IsNullOrWhiteSpace(body)) spells.AppendLine(body);
            }
            h.Spells = spells.ToString().Trim();
        }

        if (d.TryGetProperty("avatar", out var av) && av.ValueKind == JsonValueKind.Object)
        {
            if (av.TryGetProperty("jpeg", out var jpeg) && jpeg.ValueKind == JsonValueKind.String) h.Portrait = jpeg.GetString() ?? "";
            else if (av.TryGetProperty("webp", out var webp) && webp.ValueKind == JsonValueKind.String) h.Portrait = webp.GetString() ?? "";
        }

        // пассивное восприятие
        h.Pp = 10 + h.Mod(h.Wis) + (h.HasSkill("perception") ? h.Proficiency : 0);

        HeroStore.Normalize(h);
        return h;
    }

    private static int LssScore(JsonElement stats, string key)
    {
        if (!stats.TryGetProperty(key, out var s)) return 10;
        var v = DigFirst(s, "score");
        return v is JsonElement e && e.ValueKind == JsonValueKind.Number ? Math.Clamp(e.GetInt32(), 1, 30) : 10;
    }

    private static string? LssVal(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("value", out var inner)) v = inner;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }

    private static string LssText(JsonElement text, string key)
    {
        var v = LssVal(text, key);
        return StripHtml(v ?? "");
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var s = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "</(p|li|div|h\\d)>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<li[^>]*>", "• ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<[^>]+>", "");
        s = s.Replace("&nbsp;", " ").Replace("&laquo;", "«").Replace("&raquo;", "»")
             .Replace("&mdash;", "—").Replace("&amp;", "&").Replace("&quot;", "\"")
             .Replace("&lt;", "<").Replace("&gt;", ">");
        s = Regex.Replace(s, "[ \t]+", " ");
        s = Regex.Replace(s, "\n{3,}", "\n\n");
        return s.Trim();
    }

    /* ---------------- helpers ---------------- */

    private static JsonElement? DigFirst(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        foreach (var n in names)
            if (obj.TryGetProperty(n, out var v)) return v;
        return null;
    }

    private static string Str(JsonElement j, params string[] names)
    {
        var v = DigFirst(j, names);
        if (v is not JsonElement e) return "";
        return e.ValueKind switch
        {
            JsonValueKind.String => e.GetString() ?? "",
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.Object => DigFirst(e, "value", "name") is JsonElement inner && inner.ValueKind == JsonValueKind.String ? inner.GetString() ?? "" : "",
            _ => "",
        };
    }

    private static int Num(JsonElement j, int def, params string[] names)
    {
        var v = DigFirst(j, names);
        if (v is not JsonElement e) return def;
        return e.ValueKind switch
        {
            JsonValueKind.Number => (int)Math.Round(e.GetDouble()),
            JsonValueKind.String => int.TryParse(e.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : def,
            JsonValueKind.Object => DigFirst(e, "value", "score") is JsonElement inner && inner.ValueKind == JsonValueKind.Number ? (int)Math.Round(inner.GetDouble()) : def,
            _ => def,
        };
    }

    private static bool Truthy(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => e.GetDouble() != 0,
            JsonValueKind.String => e.GetString() is "1" or "true" or "True" or "да",
            _ => false,
        };
    }

    private static int ToInt(string? s, int def) => int.TryParse(s, out var v) ? v : def;
}
