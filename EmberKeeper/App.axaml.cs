using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace EmberKeeper;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // подписка на ошибки UI — только после того, как платформа инициализирована
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (s, e) =>
        {
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "error.log"),
                    "UI: " + e.Exception);
            }
            catch { }
            e.Handled = true;
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Pal.Set(Settings.LoadDark(), Settings.LoadStyle());

            // лицензионная защита: без действующей лицензии программа не запускается
            var testKey = Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_LICENSE");
            if (!string.IsNullOrWhiteSpace(testKey))
            {
                License.Activate(testKey, out var keyError);
                try
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(AppContext.BaseDirectory, "license-test.log"),
                        "env test: " + (License.Licensed ? "ok: " + License.Describe() : "fail: " + keyError));
                }
                catch { }
            }
            else
            {
                License.Load(out _);
            }

            if (!License.Licensed)
            {
                var activation = new ActivationWindow();
                desktop.MainWindow = activation;
                activation.Closed += (_, _) =>
                {
                    if (License.Licensed)
                    {
                        var main = new MainWindow();
                        desktop.MainWindow = main;
                        main.Show();
                    }
                    else
                    {
                        desktop.Shutdown();
                    }
                };
                base.OnFrameworkInitializationCompleted();
                return;
            }

            var mainWin = new MainWindow();
            desktop.MainWindow = mainWin;

            if (int.TryParse(Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_TAB"), out var testTab))
            {
                Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                {
                    try { mainWin.SelectTab(testTab); } catch { }
                }, TimeSpan.FromMilliseconds(700));
            }

            if (Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_SETTINGS") == "1")
            {
                Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                {
                    try
                    {
                        var sw = new SettingsWindow();
                        sw.Closed += (_, _) => { };
                        sw.Show();
                    }
                    catch (Exception ex)
                    {
                        try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "settings-test.log"), ex.ToString()); } catch { }
                    }
                }, TimeSpan.FromMilliseconds(800));
            }

            var updRepo = Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_UPDATE");
            if (!string.IsNullOrWhiteSpace(updRepo))
            {
                Avalonia.Threading.DispatcherTimer.RunOnce(async () =>
                {
                    var log = System.IO.Path.Combine(AppContext.BaseDirectory, "update-test.log");
                    try
                    {
                        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "update.json");
                        System.IO.File.WriteAllText(path, "{ \"repo\": \"" + updRepo + "\" }");
                        var (info, error) = await Updater.CheckAsync();
                        var text = info != null
                            ? $"update: {info.Version}, asset={info.AssetName}, size={info.Size}, url={info.Url}"
                            : "no update. error=" + error;
                        System.IO.File.WriteAllText(log, text);
                    }
                    catch (Exception ex)
                    {
                        try { System.IO.File.WriteAllText(log, "ERR: " + ex); } catch { }
                    }
                }, TimeSpan.FromSeconds(2));
            }

            if (Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_ENC") == "1")
            {
                Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                {
                    try
                    {
                        var log = System.IO.Path.Combine(AppContext.BaseDirectory, "enc-test.log");
                        var res = (desktop.MainWindow as MainWindow)?.RunEncounterDemo() ?? "нет окна";
                        System.IO.File.WriteAllText(log, res);
                    }
                    catch (Exception ex)
                    {
                        try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "enc-test.log"), "ERR: " + ex); } catch { }
                    }
                }, TimeSpan.FromSeconds(2));
            }

            var genMode = Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_GEN");
            if (!string.IsNullOrEmpty(genMode))
            {
                Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                {
                    try
                    {
                        var w = new GeneratorsWindow();
                        w.Show();
                        if (int.TryParse(genMode, out int idx) && idx > 0) w.SelectIndex(idx - 1);
                    }
                    catch (Exception ex)
                    {
                        try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "gen-test.log"), "FAIL: " + ex); } catch { }
                    }
                }, TimeSpan.FromSeconds(2));
            }

            // служебная проверка экрана игроков (включается переменной окружения)
            if (Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_PRESENT") == "1")
            {
                Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                {
                    var log = System.IO.Path.Combine(AppContext.BaseDirectory, "present-test.log");
                    try
                    {
                        var scenes = SceneStore.Load();
                        var scene = scenes.Count > 0 ? scenes[0] : new Scene { Name = "Тест" };
                        for (int i = 0; i < 3; i++)
                        {
                            Live.EnsureWindow();
                            Live.ShowScene(scene);
                            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                            Live.ShowMap();
                            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        }
                        Live.ShowBlank();
                        System.IO.File.WriteAllText(log, "OK: переключения экрана прошли");
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.WriteAllText(log, "FAIL: " + ex);
                    }
                }, TimeSpan.FromSeconds(2));
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
