using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace EmberKeeperLicenseGen;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        LoadTheme();
    }

    private static void LoadTheme()
    {
        try
        {
            var path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "licensegen-settings.json");
            if (!System.IO.File.Exists(path)) return;
            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.TryGetProperty("dark", out var d) && d.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                LgTheme.Dark = d.GetBoolean();
            if (root.TryGetProperty("style", out var st) && st.ValueKind == System.Text.Json.JsonValueKind.String)
                LgTheme.Style = st.GetString() ?? "amber";
        }
        catch { }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new LicenseWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
