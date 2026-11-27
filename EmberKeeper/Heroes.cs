using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EmberKeeper;

public sealed class Hero
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Герой";
    public string Race { get; set; } = "";
    public string Class { get; set; } = "";
    public string Subclass { get; set; } = "";
    public string Background { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Portrait { get; set; } = "";
    public string Level { get; set; } = "1";
    public int Ac { get; set; } = 10;
    public int Hp { get; set; } = 10;
    public int MaxHp { get; set; } = 10;
    public int TempHp { get; set; }
    public int Init { get; set; }
    public int Speed { get; set; } = 30;
    public int Darkvision { get; set; }
    public int Pp { get; set; }
    public string HitDice { get; set; } = "";
    public int HitDiceCurrent { get; set; }
    public int HitDiceMax { get; set; }
    public int Str { get; set; } = 10;
    public int Dex { get; set; } = 10;
    public int Con { get; set; } = 10;
    public int Int { get; set; } = 10;
    public int Wis { get; set; } = 10;
    public int Cha { get; set; } = 10;
    public string Saves { get; set; } = "";
    public string Skills { get; set; } = "";
    public int SpellDc { get; set; } = 13;
    public int SpellAtk { get; set; } = 5;
    public List<int> Slots { get; set; } = new() { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    public string Attacks { get; set; } = "";
    public string Spells { get; set; } = "";
    public string Equipment { get; set; } = "";
    public string Features { get; set; } = "";
    public string Feats { get; set; } = "";
    public string Allies { get; set; } = "";
    public string Goals { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Traits { get; set; } = "";

    // фильтры
    public string Faction { get; set; } = "";
    public string City { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Campaign { get; set; } = "";
    public string Tags { get; set; } = "";

    public int LevelNum => int.TryParse(Level, out var v) ? Math.Clamp(v, 1, 20) : 1;
    public int Proficiency => 2 + Math.Max(0, (LevelNum - 1) / 4);
    public int Mod(int score) => (int)Math.Floor((score - 10) / 2.0);

    public static readonly string[] AbilKeys = { "str", "dex", "con", "int", "wis", "cha" };
    public static readonly string[] AbilNames = { "Сила", "Ловкость", "Телосложение", "Интеллект", "Мудрость", "Харизма" };

    public static readonly (string Key, string Name, string Abil)[] SkillsList =
    {
        ("acrobatics", "Акробатика", "dex"),
        ("animal", "Обращение с животными", "wis"),
        ("arcana", "Магия", "int"),
        ("athletics", "Атлетика", "str"),
        ("deception", "Обман", "cha"),
        ("history", "История", "int"),
        ("insight", "Проницательность", "wis"),
        ("intimidation", "Запугивание", "cha"),
        ("investigation", "Расследование", "int"),
        ("medicine", "Медицина", "wis"),
        ("nature", "Природа", "int"),
        ("perception", "Восприятие", "wis"),
        ("performance", "Выступление", "cha"),
        ("persuasion", "Убеждение", "cha"),
        ("religion", "Религия", "int"),
        ("sleight", "Ловкость рук", "dex"),
        ("stealth", "Скрытность", "dex"),
        ("survival", "Выживание", "wis"),
    };

    public int Ability(string key) => key switch
    {
        "str" => Str, "dex" => Dex, "con" => Con, "int" => Int, "wis" => Wis, _ => Cha
    };

    public bool HasSkill(string key) => Skills.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(key);
    public bool HasSave(string key) => Saves.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(key);

    public int SaveMod(string key) => Mod(Ability(key)) + (HasSave(key) ? Proficiency : 0);
    public int SkillMod(string skillKey)
    {
        var abil = SkillsList.FirstOrDefault(s => s.Key == skillKey).Abil ?? "dex";
        return Mod(Ability(abil)) + (HasSkill(skillKey) ? Proficiency : 0);
    }

    public void ToggleSave(string key, bool on)
    {
        var set = Saves.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        set.Remove(key);
        if (on) set.Add(key);
        Saves = string.Join(",", set);
    }

    public void ToggleSkill(string key, bool on)
    {
        var set = Skills.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        set.Remove(key);
        if (on) set.Add(key);
        Skills = string.Join(",", set);
    }
}

public static class HeroStore
{
    public static string Path => AppPaths.File("heroes.json");

    public static List<Hero> Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var list = JsonSerializer.Deserialize<List<Hero>>(File.ReadAllText(Path)) ?? new();
                foreach (var h in list) Normalize(h);
                return list;
            }
        }
        catch { }
        return new();
    }

    public static void Save(List<Hero> list)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Dir);
            File.WriteAllText(Path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static void Normalize(Hero h)
    {
        if (h.Slots == null || h.Slots.Count != 9)
        {
            var slots = new List<int>(new int[9]);
            if (h.Slots != null)
                for (int i = 0; i < Math.Min(9, h.Slots.Count); i++) slots[i] = h.Slots[i];
            h.Slots = slots;
        }
        if (h.MaxHp < h.Hp) h.MaxHp = h.Hp;
        if (string.IsNullOrWhiteSpace(h.Name)) h.Name = "Герой";
    }
}
