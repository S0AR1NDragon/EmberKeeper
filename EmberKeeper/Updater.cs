using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EmberKeeper;

public sealed class UpdateInfo
{
    public string Version { get; set; } = "";
    public string Notes { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string Url { get; set; } = "";
    public long Size { get; set; }
}

/// <summary>Обновления через GitHub Releases: проверка, загрузка установщика, запуск.</summary>
public static class Updater
{
    public static string Version { get; } = AppInfo.Version;

    private sealed class Config
    {
        public string Repo { get; set; } = "";
        public string Asset { get; set; } = "";
    }

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("EmberKeeper-Updater/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>Настройки канала обновлений из update.json рядом с программой.</summary>
    public static (string Repo, string Asset) Channel
    {
        get
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "update.json");
                if (File.Exists(path))
                {
                    var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(path),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cfg != null) return (cfg.Repo?.Trim() ?? "", cfg.Asset?.Trim() ?? "");
                }
            }
            catch { }
            return ("", "");
        }
    }

    public static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(DownloadDir);
            File.AppendAllText(Path.Combine(DownloadDir, "update.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + "\n");
        }
        catch { }
    }

    public static string DownloadDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EmberKeeper", "updates");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }
    }

    /// <summary>Проверяет последний релиз. Возвращает null, если версия актуальна.</summary>
    public static async Task<(UpdateInfo? Info, string Error)> CheckAsync(CancellationToken ct = default)
    {
        var (repo, assetFilter) = Channel;
        if (string.IsNullOrWhiteSpace(repo))
            return (null, "Канал обновлений не настроен.\n\nСоздайте рядом с программой файл update.json:\n" +
                          "{ \"repo\": \"владелец/репозиторий\" }");

        try
        {
            var url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var resp = await Http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (null, "Репозиторий обновлений не найден: " + repo);
            if (!resp.IsSuccessStatusCode)
                return (null, "GitHub ответил: " + (int)resp.StatusCode);

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tg) ? tg.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var bd) ? bd.GetString() ?? "" : "";
            var latest = tag.TrimStart('v', 'V').Trim();

            if (!IsNewer(latest, Version))
                return (null, "");

            UpdateInfo? installer = null;
            UpdateInfo? portable = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var dl = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(dl)) continue;
                    bool isZip = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                    bool isExe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                    if (!isZip && !isExe) continue;

                    bool matches = string.IsNullOrWhiteSpace(assetFilter) ||
                                   name.Contains(assetFilter, StringComparison.OrdinalIgnoreCase);
                    if (!matches) continue;

                    var cand = new UpdateInfo
                    {
                        Version = latest,
                        Notes = notes,
                        AssetName = name,
                        Url = dl,
                        Size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0,
                    };
                    if (isZip) portable ??= cand;
                    else installer ??= cand;
                }
            }
            var best = installer ?? portable;

            if (best == null)
                return (null, $"Новая версия {latest} есть, но в релизе нет .exe-файла.\nПрикрепите установщик EmberKeeperSetup.");

            return (best, "");
        }
        catch (OperationCanceledException) { return (null, "Проверка отменена."); }
        catch (Exception ex) { return (null, "Не удалось связаться с GitHub: " + ex.Message); }
    }

    /// <summary>Скачивает установщик обновления во временную папку.</summary>
    public static async Task<(string? Path, string Error)> DownloadAsync(
        UpdateInfo info, IProgress<double>? progress, CancellationToken ct = default)
    {
        try
        {
            var target = Path.Combine(DownloadDir, info.AssetName);
            using var resp = await Http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return (null, "Не удалось скачать файл: " + (int)resp.StatusCode);

            var total = resp.Content.Headers.ContentLength ?? info.Size;
            if (info.Size > 0 && total > 0 && total != info.Size)
                return (null, "Размер загрузки не совпадает: " + total + " вместо " + info.Size + ".");
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0) progress?.Report((double)read / total);
            }

            var fi = new FileInfo(target);
            if (fi.Length < 1024) return (null, "Скачанный файл слишком мал — вероятно, ошибка GitHub.");
            if (info.AssetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                using var chk = File.OpenRead(target);
                if (chk.ReadByte() != 0x4D || chk.ReadByte() != 0x5A)
                    return (null, "Скачанный файл не является исполняемым (.exe повреждён).");
            }
            return (target, "");
        }
        catch (OperationCanceledException) { return (null, "Загрузка отменена."); }
        catch (Exception ex) { return (null, "Ошибка загрузки: " + ex.Message); }
    }

    /// <summary>Запускает установщик в тихом режиме с перезапуском программы.</summary>
    public static bool RunInstaller(string setupPath, string installDir, out string error)
    {
        error = "";
        try
        {
            if (!File.Exists(setupPath)) { error = "Файл обновления не найден."; return false; }
            var psi = new System.Diagnostics.ProcessStartInfo(setupPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(setupPath) ?? AppContext.BaseDirectory,
                Arguments = "/silent /relaunch /dir=\"" + installDir.TrimEnd('\\') + "\"",
            };
            try
            {
                var logDir = Path.Combine(DownloadDir, "log");
                Directory.CreateDirectory(logDir);
                psi.EnvironmentVariables["EMBERKEEPER_LOG_DIR"] = logDir;
            }
            catch { }
            var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) { error = "Не удалось запустить установщик."; return false; }
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    /// <summary>Распаковывает zip-обновление и запускает скрипт замены после закрытия программы.</summary>
    public static async Task<bool> ApplyZipAsync(string zipPath, string targetDir, string exeName, Action<string> log)
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var work = Path.Combine(DownloadDir, "apply-" + stamp);
            var files = Path.Combine(work, "files");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(files);
            log("распаковка архива…");
            await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, files, true));

            var script = Path.Combine(work, "apply.cmd");
            var exe = Path.Combine(targetDir, exeName);
            string q(string x) => "\"" + x.TrimEnd('\\') + "\"";
            var content = string.Join("\r\n", new[]
            {
                "@echo off",
                "setlocal",
                ":wait",
                "tasklist /FI \"IMAGENAME eq " + exeName + "\" 2>nul | find /I \"" + exeName + "\" >nul",
                "if not errorlevel 1 (",
                "  timeout /t 1 /nobreak >nul",
                "  goto wait",
                ")",
                "robocopy " + q(files) + " " + q(targetDir) + " /E /NFL /NDL /NJH /NJS /NP /XF license.key update.json setup-error.log error.log /XD campaign >nul",
                "start \"\" " + q(exe),
                "timeout /t 2 /nobreak >nul",
                "rd /s /q " + q(work) + " >nul 2>nul",
                "del /q \"%~f0\" >nul 2>nul",
            }) + "\r\n";
            File.WriteAllText(script, content, System.Text.Encoding.Default);

            var info = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = work,
            };
            var proc = System.Diagnostics.Process.Start(info);
            if (proc == null) { log("не удалось запустить скрипт обновления"); return false; }
            return true;
        }
        catch (Exception ex) { log("ошибка обновления: " + ex.Message); return false; }
    }

    private static bool IsNewer(string latest, string current)
    {
        if (!System.Version.TryParse(Normalize(latest), out var l) ||
            !System.Version.TryParse(Normalize(current), out var c))
            return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
        return l > c;
    }

    private static string Normalize(string v)
    {
        var parts = v.Split('.');
        var list = new System.Collections.Generic.List<string>();
        foreach (var p in parts)
        {
            var digits = new string(Array.FindAll(p.ToCharArray(), char.IsDigit));
            list.Add(string.IsNullOrEmpty(digits) ? "0" : digits);
        }
        while (list.Count < 3) list.Add("0");
        return string.Join(".", list.GetRange(0, 3));
    }
}

public static class AppInfo
{
    public const string ProductName = "EmberKeeper";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "1.0.5";
}
