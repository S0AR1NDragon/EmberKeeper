using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EmberKeeper;

/// <summary>Экспорт/импорт всей кампании одним JSON-файлом.</summary>
public sealed class CampaignBundle
{
    public int Version { get; set; } = 1;
    public string SavedAt { get; set; } = "";
    public MapBook? Maps { get; set; }
    public List<Hero>? Heroes { get; set; }
    public List<Npc>? Npcs { get; set; }
    public List<Scene>? Scenes { get; set; }
    public List<Combatant>? Init { get; set; }
}

public static class Campaign
{
    public static string ExportDir => Path.Combine(AppPaths.Dir, "exports");

    public static string? Export()
    {
        try
        {
            var bundle = new CampaignBundle
            {
                Version = 1,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Maps = MapStore.Load(),
                Heroes = HeroStore.Load(),
                Npcs = NpcStore.Load(),
                Scenes = SceneStore.Load(),
                Init = InitStore.Load(),
            };

            Directory.CreateDirectory(ExportDir);
            var path = Path.Combine(ExportDir, $"campaign-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true }));
            return path;
        }
        catch { return null; }
    }

    public static string? Import(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var bundle = JsonSerializer.Deserialize<CampaignBundle>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (bundle == null) return "Файл не распознан как кампания";

            if (bundle.Maps?.Maps is { Count: > 0 } maps)
            {
                if (bundle.Maps.Active < 0 || bundle.Maps.Active >= maps.Count) bundle.Maps.Active = 0;
                foreach (var m in maps) MapStore.Normalize(m);
                MapStore.Save(bundle.Maps);
            }
            if (bundle.Heroes != null) HeroStore.Save(bundle.Heroes);
            if (bundle.Npcs != null) NpcStore.Save(bundle.Npcs);
            if (bundle.Scenes != null) SceneStore.Save(bundle.Scenes);
            if (bundle.Init != null) InitStore.Save(bundle.Init);

            return null; // успех
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
