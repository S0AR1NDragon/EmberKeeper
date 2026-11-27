using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EmberKeeper;

/// <summary>Монстр из личного бестиария мастера.</summary>
public sealed class PersonalMonster
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Монстр";
    public string Cr { get; set; } = "";
    public string Type { get; set; } = "";
    public int Hp { get; set; } = 10;
    public int Ac { get; set; } = 10;
    public int Init { get; set; }
    public string Notes { get; set; } = "";
}

/// <summary>Личный бестиарий: сохраняется рядом с кампанией.</summary>
public static class PersonalBestiary
{
    public static string Path => AppPaths.File("personal-bestiary.json");

    public static List<PersonalMonster> Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<List<PersonalMonster>>(File.ReadAllText(Path)) ?? new List<PersonalMonster>();
        }
        catch { }
        return new List<PersonalMonster>();
    }

    public static void Save(List<PersonalMonster> list)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>Добавляет или обновляет монстра; возвращает его запись.</summary>
    public static PersonalMonster Register(string name, string cr, string type, int hp, int ac, int init, string notes)
    {
        var list = Load();
        var m = list.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (m == null)
        {
            m = new PersonalMonster { Name = string.IsNullOrWhiteSpace(name) ? "Новый монстр" : name };
            list.Add(m);
        }
        m.Hp = hp;
        m.Ac = ac;
        m.Init = init;
        if (!string.IsNullOrWhiteSpace(cr)) m.Cr = cr;
        if (!string.IsNullOrWhiteSpace(type)) m.Type = type;
        if (!string.IsNullOrWhiteSpace(notes)) m.Notes = notes;
        Save(list);
        return m;
    }

    public static void Update(string id, string name, int hp, int ac, int init)
    {
        var list = Load();
        var m = list.FirstOrDefault(x => x.Id == id);
        if (m == null) return;
        if (!string.IsNullOrWhiteSpace(name)) m.Name = name;
        m.Hp = hp;
        m.Ac = ac;
        m.Init = init;
        Save(list);
    }
}

/// <summary>Связь листа героя и токена на карте.</summary>
public static class TokenSync
{
    public static event Action? TokensUpdated;

    private static DateTime _lastSave = DateTime.MinValue;

    /// <summary>Обновить токены героя (ночное зрение, ХП, КД, имя) после правки листа.</summary>
    public static void SyncHero(Hero h)
    {
        try
        {
            bool changed = false;

            if (Live.Doc != null)
            {
                changed = Apply(Live.Doc, h);
            }
            else
            {
                var book = MapStore.Load();
                foreach (var m in book.Maps)
                    if (Apply(m, h)) changed = true;
                if (changed && (DateTime.UtcNow - _lastSave).TotalSeconds > 1.5)
                {
                    _lastSave = DateTime.UtcNow;
                    MapStore.Save(book);
                }
            }

            if (changed)
            {
                Live.RaiseMapChanged();
                TokensUpdated?.Invoke();
            }
        }
        catch { }
    }

    private static bool Apply(MapDoc doc, Hero h)
    {
        bool changed = false;
        foreach (var t in doc.Tokens)
        {
            if (t.Type != "player") continue;
            if (t.Ref != h.Id && !string.Equals(t.Name, h.Name, StringComparison.CurrentCultureIgnoreCase)) continue;
            t.Ref = h.Id;
            t.Name = h.Name;
            t.VisionFt = h.Darkvision;
            t.Hp = h.Hp;
            t.MaxHp = h.MaxHp;
            t.Ac = h.Ac;
            t.Init = h.Init;
            changed = true;
        }
        return changed;
    }
}
