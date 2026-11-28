using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EmberKeeperLicenseGen;

/// <summary>Консольный режим генератора (--issue / --verify) для автоматизации.</summary>
internal static class Cli
{
    public static int Run(string[] args)
    {
        try
        {
            if (Has(args, "--verify")) { Verify(Value(args, "--verify") ?? ""); return 0; }
            if (Has(args, "--issue")) { Issue(args); return 0; }
            Console.WriteLine("--issue --name <имя> --machine <id|*> --days <n> [--out файл]");
            Console.WriteLine("--verify <ключ или файл>");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ОШИБКА: " + ex.Message);
            return 1;
        }
    }

    internal static string BuildKey(string name, string machine, int days)
    {
        var payload = new
        {
            name,
            machine,
            expires = days > 0 ? DateTime.Today.AddDays(days).ToString("yyyy-MM-dd") : "",
            edition = "full",
            issued = DateTime.Today.ToString("yyyy-MM-dd"),
        };
        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(Keys.PrivatePem());
        var sig = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Keys.Base64Url(payloadBytes) + "." + Keys.Base64Url(sig);
    }

    private static void Issue(string[] args)
    {
        var name = Value(args, "--name") ?? "Лицензиат";
        var machine = Value(args, "--machine") ?? "*";
        if (machine.Length == 0) machine = "*";
        int days = 0;
        if (int.TryParse(Value(args, "--days") ?? "0", out var d)) days = Math.Max(0, d);
        var outFile = Value(args, "--out");
        var key = BuildKey(name, machine, days);
        if (!string.IsNullOrWhiteSpace(outFile))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(outFile!));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outFile!, key);
            Console.WriteLine("Лицензия записана: " + Path.GetFullPath(outFile!));
        }
        else Console.WriteLine(key);
        Console.WriteLine($"Кому: {name} · Машина: {machine} · До: {(days > 0 ? DateTime.Today.AddDays(days).ToString("yyyy-MM-dd") : "бессрочно")}");
    }

    private static void Verify(string input)
    {
        var key = File.Exists(input) ? File.ReadAllText(input).Trim() : input.Trim();
        var parts = key.Split('.');
        if (parts.Length != 2) throw new InvalidOperationException("неверный формат ключа");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(Keys.PublicPem());
        var payload = Keys.FromBase64Url(parts[0]);
        var sig = Keys.FromBase64Url(parts[1]);
        bool ok = rsa.VerifyData(payload, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Console.WriteLine("Подпись: " + (ok ? "ВЕРНА" : "НЕВЕРНА"));
        Console.WriteLine("Данные: " + Encoding.UTF8.GetString(payload));
    }

    private static bool Has(string[] args, string flag)
    {
        foreach (var a in args) if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string? Value(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}

/// <summary>Встроенные ключи подписи.</summary>
internal static class Keys
{
    internal static string PrivatePem() => Load("private.pem");
    internal static string PublicPem() => Load("public.pem");

    private static string Load(string name)
    {
        var local = Path.Combine(AppContext.BaseDirectory, "keys", name);
        if (File.Exists(local)) return File.ReadAllText(local);
        var asm = typeof(Keys).Assembly;
        using var s = asm.GetManifestResourceStream(name)
                      ?? throw new InvalidOperationException("нет встроенного ключа " + name);
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }

    internal static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64Url(string s)
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
