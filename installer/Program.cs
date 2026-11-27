using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EmberKeeperSetup;

internal static class Program
{
    private const string AppName = "EmberKeeper";
    private const string Version = "1.0.1";
    private const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EmberKeeper";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x0;
    private const uint MB_OKCANCEL = 0x1;
    private const uint MB_YESNO = 0x4;
    private const uint MB_ICONERROR = 0x10;
    private const uint MB_ICONQUESTION = 0x20;
    private const uint MB_ICONINFORMATION = 0x40;
    private const uint MB_YESNO_ICONQUESTION = MB_YESNO | MB_ICONQUESTION;

    private static bool _silent;

    private static int Main(string[] args)
    {
        try
        {
            bool silent = Has(args, "/silent") || Has(args, "/S");
            _silent = silent;
            bool uninstall = Has(args, "/uninstall");
            bool noShortcuts = Has(args, "/noshortcuts");
            bool noRegistry = Has(args, "/noregistry");
            string? dirArg = Value(args, "/dir=");

            if (uninstall)
            {
                Uninstall(silent, noShortcuts, noRegistry);
                return 0;
            }

            Install(silent, dirArg, noShortcuts, noRegistry, Value(args, "/key="), Has(args, "/relaunch") || !Has(args, "/norelaunch"));
            return 0;
        }
        catch (Exception ex)
        {
            LogError(ex);
            if (!_silent)
                MessageBoxW(IntPtr.Zero, "Не удалось завершить установку:\n\n" + ex.Message, AppName, MB_OK | MB_ICONERROR);
            return 1;
        }
    }

    /* ---------------- установка ---------------- */

    private static void TryLog(string text)
    {
        try
        {
            var dir = Environment.GetEnvironmentVariable("EMBERKEEPER_LOG_DIR");
            if (string.IsNullOrWhiteSpace(dir)) return;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "installer.log"),
                DateTime.Now.ToString("HH:mm:ss") + "  " + text + "\n");
        }
        catch { }
    }

    private static void LogError(Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
            File.AppendAllText(Path.Combine(dir, "setup-error.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + "\n\n");
        }
        catch { }
    }

    private static void Install(bool silent, string? customDir, bool noShortcuts, bool noRegistry, string? licenseKey, bool relaunch)
    {
        var dir = string.IsNullOrWhiteSpace(customDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName)
            : customDir!;

        if (!silent)
        {
            var q = MessageBoxW(IntPtr.Zero,
                $"Установить {AppName} {Version}?\n\nПапка установки:\n{dir}\n\n" +
                "Программа устанавливается только для текущего пользователя и не требует прав администратора.",
                "Установка " + AppName,
                MB_OKCANCEL | MB_ICONQUESTION);
            if (q != 1) return; // IDOK
        }

        CloseRunningApp();

        var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
                      ?? throw new InvalidOperationException("Внутри установщика нет файлов программы (payload.zip).");

        Directory.CreateDirectory(dir);
        ExtractPayload(payload, dir);

        var exePath = Path.Combine(dir, AppName + ".exe");
        if (!File.Exists(exePath))
            throw new InvalidOperationException("После распаковки не найден " + exePath);

        // копия установщика для удаления программы
        var selfPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(selfPath) && File.Exists(selfPath))
        {
            var uninstaller = Path.Combine(dir, "Uninstall.exe");
            try
            {
                if (!string.Equals(selfPath, uninstaller, StringComparison.OrdinalIgnoreCase))
                    File.Copy(selfPath, uninstaller, true);
            }
            catch { }
        }

        // лицензия: параметр /key=... или файл license.key рядом с установщиком
        try
        {
            var key = licenseKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                var setupDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
                var keyFile = Path.Combine(setupDir, "license.key");
                if (File.Exists(keyFile)) key = File.ReadAllText(keyFile).Trim();
            }
            if (!string.IsNullOrWhiteSpace(key))
                File.WriteAllText(Path.Combine(dir, "license.key"), key.Trim());
        }
        catch { }

        if (!noShortcuts) CreateShortcuts(dir);
        if (!noRegistry) WriteRegistry(dir);

        if (silent)
        {
            if (relaunch)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exePath)
                    {
                        WorkingDirectory = dir,
                        UseShellExecute = true,
                    });
                    TryLog("relaunch: " + exePath);
                }
                catch (Exception ex) { TryLog("relaunch FAIL: " + ex.Message); }
            }
            return;
        }

        var run = MessageBoxW(IntPtr.Zero,
            $"{AppName} {Version} установлен.\n\nЗапустить сейчас?",
            "Готово", MB_YESNO_ICONQUESTION);
        if (run == 6) // IDYES
        {
            try { Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = dir, UseShellExecute = true }); }
            catch { }
        }
        else
        {
            MessageBoxW(IntPtr.Zero,
                $"Готово!\n\nИщите «{AppName}» в меню «Пуск» или на рабочем столе.\n" +
                $"Папка: {dir}\n\nДанные кампании хранятся в подпапке campaign рядом с программой.",
                AppName, MB_OK | MB_ICONINFORMATION);
        }
    }

    private static void ExtractPayload(Stream payload, string dir)
    {
        // payload.zip содержит файлы приложения в корне
        using var zip = new ZipArchive(payload, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/")) continue;

            var target = Path.Combine(dir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (string.IsNullOrEmpty(entry.Name)) continue; // каталог
            using var src = entry.Open();
            using var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            src.CopyTo(dst);
        }
    }

    private static void CloseRunningApp()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(AppName))
            {
                try { p.Kill(true); p.WaitForExit(3000); } catch { }
            }
        }
        catch { }
    }

    /* ---------------- ярлыки ---------------- */

    private static void CreateShortcuts(string dir)
    {
        var exe = Path.Combine(dir, AppName + ".exe");
        var programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs");
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        try { Directory.CreateDirectory(programs); } catch { }

        CreateShortcut(Path.Combine(programs, AppName + ".lnk"), exe, dir);
        CreateShortcut(Path.Combine(desktop, AppName + ".lnk"), exe, dir);
    }

    private static void CreateShortcut(string lnkPath, string target, string workDir)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return;
            dynamic? shell = Activator.CreateInstance(type);
            if (shell == null) return;
            dynamic sc = shell.CreateShortcut(lnkPath);
            sc.TargetPath = target;
            sc.WorkingDirectory = workDir;
            sc.IconLocation = target + ",0";
            sc.Description = AppName + " — рабочий стол мастера";
            sc.Save();
        }
        catch { }
    }

    /* ---------------- реестр ---------------- */

    private static void WriteRegistry(string dir)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegKey);
            if (key == null) return;
            key.SetValue("DisplayName", AppName + " — рабочий стол мастера");
            key.SetValue("DisplayVersion", Version);
            key.SetValue("Publisher", AppName);
            key.SetValue("InstallLocation", dir);
            key.SetValue("DisplayIcon", Path.Combine(dir, AppName + ".exe"));
            key.SetValue("UninstallString", "\"" + Path.Combine(dir, "Uninstall.exe") + "\" /uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch { }
    }

    /* ---------------- удаление ---------------- */

    private static void Uninstall(bool silent, bool noShortcuts, bool noRegistry)
    {
        var dir = AppDir();
        if (string.IsNullOrWhiteSpace(dir)) return;

        if (!silent)
        {
            var q = MessageBoxW(IntPtr.Zero,
                $"Удалить {AppName}?\n\nБудут удалены файлы программы:\n{dir}\n\n" +
                "Папка campaign с вашей кампанией будет сохранена в «Документы\\EmberKeeper-campaign».",
                "Удаление " + AppName, MB_OKCANCEL | MB_ICONQUESTION);
            if (q != 1) return;
        }

        CloseRunningApp();

        TryLog("uninstall start, dir=" + dir + ", exists=" + Directory.Exists(dir));
        var savedTo = SaveCampaign(dir);
        TryLog("campaign saved to: " + (savedTo ?? "нет"));

        if (!noShortcuts) RemoveShortcuts();
        if (!noRegistry) RemoveRegistry();

        // удалить папку после выхода установщика
        var cmd = "ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + dir + "\"";
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch { }

        if (!silent)
        {
            var text = AppName + " удалён.";
            if (savedTo != null) text += "\n\nКампания сохранена:\n" + savedTo;
            MessageBoxW(IntPtr.Zero, text, AppName, MB_OK | MB_ICONINFORMATION);
        }
    }

    /// <summary>Сохраняет папку campaign в «Документы», чтобы удаление программы не стёрло кампанию.</summary>
    private static string? SaveCampaign(string dir)
    {
        try
        {
            var campaign = Path.Combine(dir, "campaign");
            TryLog("SaveCampaign: campaign exists=" + Directory.Exists(campaign));
            if (!Directory.Exists(campaign)) return null;

            var docs = Environment.GetEnvironmentVariable("EMBERKEEPER_CAMPAIGN_SAVE");
            if (string.IsNullOrWhiteSpace(docs))
                docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs))
                docs = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Directory.CreateDirectory(docs);

            var target = Path.Combine(docs, "EmberKeeper-campaign");
            if (Directory.Exists(target))
                target = Path.Combine(docs, "EmberKeeper-campaign-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));

            Directory.Move(campaign, target);
            TryLog("SaveCampaign: moved to " + target);
            return target;
        }
        catch (Exception ex) { TryLog("SaveCampaign FAIL: " + ex.Message); return null; }
    }

    private static string? AppDir()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey);
            var loc = key?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(loc)) return loc;
        }
        catch { }

        var exe = Environment.ProcessPath;
        return string.IsNullOrEmpty(exe) ? null : Path.GetDirectoryName(exe);
    }

    private static void RemoveShortcuts()
    {
        try
        {
            var programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            foreach (var p in new[]
                     {
                         Path.Combine(programs, AppName + ".lnk"),
                         Path.Combine(desktop, AppName + ".lnk"),
                     })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { }
            }
        }
        catch { }
    }

    private static void RemoveRegistry()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(RegKey, false); } catch { }
    }

    /* ---------------- аргументы ---------------- */

    private static bool Has(string[] args, string flag)
    {
        foreach (var a in args)
            if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string? Value(string[] args, string prefix)
    {
        foreach (var a in args)
            if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return a.Substring(prefix.Length).Trim('"');
        return null;
    }
}
