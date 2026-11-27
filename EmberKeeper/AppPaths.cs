using System;
using System.IO;

namespace EmberKeeper;

/// <summary>
/// Каталог пользовательских данных кампании. По умолчанию — папка campaign рядом с exe
/// (портативно, легко бэкапить). Если она недоступна на запись — %LOCALAPPDATA%\EmberKeeper.
/// </summary>
public static class AppPaths
{
    public static string Dir { get; } = Resolve();

    private static string Resolve()
    {
        // переменная окружения позволяет увести данные в сторону (тесты, другой профиль)
        var custom = Environment.GetEnvironmentVariable("EMBERKEEPER_DATA_DIR")
                     ?? Environment.GetEnvironmentVariable("DM_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(custom))
        {
            try
            {
                Directory.CreateDirectory(custom);
                return custom;
            }
            catch { }
        }

        var portable = Path.Combine(AppContext.BaseDirectory, "campaign");
        if (IsWritable(portable)) return portable;

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EmberKeeper");
        try { Directory.CreateDirectory(fallback); } catch { }
        return fallback;
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".write-probe");
            System.IO.File.WriteAllText(probe, "ok");
            System.IO.File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    public static string File(string name) => Path.Combine(Dir, name);
}
