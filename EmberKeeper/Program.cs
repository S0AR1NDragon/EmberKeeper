using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;

namespace EmberKeeper;

class Program
{
    static System.Threading.Mutex? _single;

    static string LogPath => Path.Combine(AppContext.BaseDirectory, "error.log");

    static void Log(string msg)
    {
        try { File.WriteAllText(LogPath, msg); } catch { }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest")
        {
            SelfTest();
            return;
        }

        // защита от второго экземпляра: две копии могут одновременно писать данные
        _single = new System.Threading.Mutex(true, "EmberKeeper_SingleInstance_7f21", out bool createdNew);
        if (!createdNew) return;

        AppDomain.CurrentDomain.UnhandledException += (s, e) => Log("Unhandled: " + e.ExceptionObject);
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            throw;
        }
    }

    /// <summary>Проверка хранилища без графики: сохранение/загрузка нескольких локаций.</summary>
    static void SelfTest()
    {
        // тест никогда не должен трогать боевые данные пользователя
        var testDir = Path.Combine(AppContext.BaseDirectory, "selftest-data");
        try { Directory.CreateDirectory(testDir); } catch { }
        Environment.SetEnvironmentVariable("EMBERKEEPER_DATA_DIR", testDir);

        var sb = new System.Text.StringBuilder();
        try
        {
            var book = MapStore.Load();
            sb.AppendLine($"load ok, maps={book.Maps.Count}, dir={MapStore.Dir}");

            var doc = MapStore.NewMap("Тест-локация");
            doc.Tokens.Add(new Token { Name = "Тест", X = 3, Y = 5 });
            book.Maps.Add(doc);
            book.Active = book.Maps.Count - 1;
            MapStore.Save(book);
            sb.AppendLine($"save ok, exists={File.Exists(MapStore.Path)}");

            var again = MapStore.Load();
            sb.AppendLine($"reload: maps={again.Maps.Count}, active={again.Active}, last={again.Maps.Last().Name}, tokens={again.Maps.Last().Tokens.Count}");

            // проверка остальных хранилищ
            HeroStore.Save(new System.Collections.Generic.List<Hero> { new Hero { Name = "Тест-герой" } });
            sb.AppendLine($"heroes={HeroStore.Load().Count}");
            NpcStore.Save(new System.Collections.Generic.List<Npc> { new Npc { Name = "Тест-нпс" } });
            sb.AppendLine($"npcs={NpcStore.Load().Count}");
            SceneStore.Save(new System.Collections.Generic.List<Scene> { new Scene { Name = "Тест-сцена" } });
            sb.AppendLine($"scenes={SceneStore.Load().Count}");
            InitStore.Save(new System.Collections.Generic.List<Combatant> { new Combatant { Name = "Тест-боец" } });
            sb.AppendLine($"init={InitStore.Load().Count}");

            var sample = "{\"name\":\"Тестовый\",\"level\":5,\"abilities\":{\"str\":16,\"dex\":14,\"con\":12},\"ac\":15,\"hp\":40,\"tags\":\"орден\",\"faction\":\"Стража\",\"city\":\"Нордвик\"}";
            var imps = HeroImport.FromJsonAny(sample);
            var ih = imps.FirstOrDefault();
            sb.AppendLine($"import: count={imps.Count}, name={ih?.Name}, lvl={ih?.Level}, str={ih?.Str}, ac={ih?.Ac}, faction={ih?.Faction}, city={ih?.City}");
            var exp = Campaign.Export();
            sb.AppendLine($"export: {(exp != null ? "ok" : "fail")} {exp}");
            if (exp != null)
            {
                var err = Campaign.Import(exp);
                sb.AppendLine($"import: {err ?? "ok"}");
            }
            // проверка картинок сцен и карт (декодирование с ограничением ширины)
            foreach (var sc in SceneStore.Load())
            {
                var t0 = DateTime.Now;
                var bmp = ImageCache.Get(sc.Image, 1280);
                sb.AppendLine($"scene '{sc.Name}': bg={(bmp != null ? bmp.PixelSize.Width + "x" + bmp.PixelSize.Height : "none")} elements={sc.Elements.Count} ms={(int)(DateTime.Now - t0).TotalMilliseconds}");
                foreach (var el in sc.Elements)
                {
                    var eb = ImageCache.Get(el.Image, 1280);
                    sb.AppendLine($"   el: {(eb != null ? eb.PixelSize.Width + "x" + eb.PixelSize.Height : "NO IMAGE")} {System.IO.Path.GetFileName(el.Image)}");
                }
            }
            foreach (var mp in MapStore.Load().Maps)
            {
                var mb = ImageCache.Get(mp.Background, 2400);
                sb.AppendLine($"map '{mp.Name}': bg={(mb != null ? mb.PixelSize.Width + "x" + mb.PixelSize.Height : "none")}");
            }
            // прогон всех генераторов по 40 раз
            var gens = new (string Name, Func<GenResult> Gen)[]
            {
                ("bonds", RandomTables.Bonds), ("npc", RandomTables.NpcPersonality),
                ("encounter", RandomTables.Encounter), ("loot", RandomTables.Loot),
                ("fate", RandomTables.FateDeck), ("tavern", RandomTables.Tavern),
                ("rumor", RandomTables.Rumor), ("hook", RandomTables.Hook),
                ("names", RandomTables.Names), ("settlement", RandomTables.Settlement),
                ("mystery", RandomTables.Mystery), ("curse", RandomTables.Curse),
                ("weather", RandomTables.Weather), ("chest", RandomTables.Chest),
                ("downtime", RandomTables.Downtime),
            };
            int bad = 0;
            foreach (var (name, gen) in gens)
            {
                try
                {
                    for (int i = 0; i < 40; i++)
                    {
                        var r = gen();
                        if (string.IsNullOrWhiteSpace(r.Main)) throw new Exception("пустой результат");
                        if (r.Rows.Count == 0) throw new Exception("нет строк");
                        if (name == "encounter")
                        {
                            var e = r.Encounter ?? throw new Exception("нет EncounterSpec");
                            int total = 0;
                            foreach (var g in e.Groups)
                            {
                                if (g.Count < 1) throw new Exception("Count<1");
                                if (string.IsNullOrWhiteSpace(g.Name)) throw new Exception("пустое имя группы");
                                total += g.Count;
                            }
                            if (total < 1) throw new Exception("нет противников");
                        }
                        if (name == "bonds" && (string.IsNullOrEmpty(r.BondA) || string.IsNullOrEmpty(r.BondB)))
                            throw new Exception("нет узлов связи");
                    }
                    sb.AppendLine($"gen {name}: ok");
                }
                catch (Exception ex)
                {
                    bad++;
                    sb.AppendLine($"gen {name}: FAIL {ex.Message}");
                }
            }
            sb.AppendLine("генераторы с ошибками: " + bad);

            sb.AppendLine("SELFTEST OK");
        }
        catch (Exception ex)
        {
            sb.AppendLine("SELFTEST FAILED: " + ex);
        }
        try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selftest.log"), sb.ToString()); } catch { }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
