using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EmberKeeper;

public sealed class LicenseData
{
    public string Name { get; set; } = "";
    public string Machine { get; set; } = "*";
    public string Expires { get; set; } = "";
    public string Edition { get; set; } = "full";
    public string Issued { get; set; } = "";

    public bool IsPerpetual => string.IsNullOrWhiteSpace(Expires);
}

/// <summary>
/// Офлайн-лицензия: ключ подписан приватным ключом владельца программы.
/// В приложении лежит только публичный ключ, поэтому подделать лицензию нельзя.
/// </summary>
public static class License
{
    public static LicenseData? Current { get; private set; }
    public static string? LastError { get; private set; }
    public static bool Licensed => Current != null;

    private static string LocalPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EmberKeeper");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, "license.key");
        }
    }

    private static string PortablePath => Path.Combine(AppContext.BaseDirectory, "license.key");

    /// <summary>Идентификатор компьютера (не меняется при переустановке программы).</summary>
    public static string MachineId()
    {
        string guid = "";
        try
        {
            guid = Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string ?? "";
        }
        catch { }

        var raw = guid + "|" + Environment.MachineName;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var sb = new StringBuilder();
        for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("X2"));
        return sb.ToString();
    }

    private static string PublicKeyPem()
    {
        try
        {
            using var stream = typeof(License).Assembly.GetManifestResourceStream("public.pem");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
        catch { }
        return "";
    }

    /// <summary>Проверяет строку ключа. Возвращает данные лицензии или null и текст ошибки.</summary>
    private static LicenseData? Validate(string key, out string error)
    {
        error = "";
        var parts = (key ?? "").Trim().Split('.');
        if (parts.Length != 2) { error = "Неверный формат ключа."; return null; }

        byte[] payload, sig;
        try
        {
            payload = FromBase64Url(parts[0]);
            sig = FromBase64Url(parts[1]);
        }
        catch { error = "Ключ повреждён."; return null; }

        var pem = PublicKeyPem();
        if (string.IsNullOrWhiteSpace(pem)) { error = "В программе нет публичного ключа."; return null; }

        bool signatureOk;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            signatureOk = rsa.VerifyData(payload, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch { error = "Ошибка проверки подписи."; return null; }

        if (!signatureOk) { error = "Подпись недействительна — лицензия не выдана этой программой."; return null; }

        LicenseData? data;
        try { data = JsonSerializer.Deserialize<LicenseData>(Encoding.UTF8.GetString(payload)); }
        catch { error = "Данные лицензии не читаются."; return null; }
        if (data == null) { error = "Данные лицензии пусты."; return null; }

        // привязка к компьютеру
        if (!string.Equals(data.Machine, "*", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(data.Machine, MachineId(), StringComparison.OrdinalIgnoreCase))
            {
                error = "Лицензия выдана для другого компьютера.";
                return null;
            }
        }

        // срок действия
        if (!data.IsPerpetual)
        {
            if (!DateTime.TryParse(data.Expires, out var expires) || DateTime.Today > expires.Date)
            {
                error = "Срок действия лицензии истёк.";
                return null;
            }
        }

        return data;
    }

    /// <summary>Загружает лицензию из стандартных мест.</summary>
    public static bool Load(out string error)
    {
        error = "";
        LastError = null;
        Current = null;

        foreach (var path in new[] { LocalPath, PortablePath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                var text = File.ReadAllText(path);
                var data = Validate(text, out var err);
                if (data != null) { Current = data; return true; }
                error = err;
                LastError = err;
                return false;
            }
            catch (Exception ex) { error = ex.Message; }
        }

        error = "Лицензия не найдена.";
        return false;
    }

    /// <summary>Проверяет и сохраняет ключ.</summary>
    public static bool Activate(string key, out string error)
    {
        var data = Validate(key, out error);
        if (data == null) return false;

        var saved = false;
        foreach (var path in new[] { LocalPath, PortablePath })
        {
            try
            {
                File.WriteAllText(path, key.Trim());
                saved = true;
                break;
            }
            catch { }
        }

        if (!saved) { error = "Не удалось сохранить лицензию на диск."; return false; }
        Current = data;
        LastError = null;
        return true;
    }

    public static string Describe() =>
        Current == null ? "не активирована"
            : $"{Current.Name} · до {Current.Expires}" + (Current.IsPerpetual ? " (бессрочно)" : "");

    /* ---------- base64url ---------- */

    public static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
