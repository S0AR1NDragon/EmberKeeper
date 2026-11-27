using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EmberKeeperLicenseGen;

/// <summary>
/// Инструмент лицензирования EmberKeeper (только для владельца программы).
///   --genkeys &lt;папка&gt;                            — создать пару ключей (public/private pem)
///   --issue --name "Иван" --machine ABC123 --days 365 [--out file]  — выпустить лицензию
///   --verify &lt;файл или ключ&gt;                      — проверить лицензию публичным ключом
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (Has(args, "--genkeys")) { GenKeys(Value(args, "--genkeys") ?? "."); return 0; }
            if (Has(args, "--verify")) { Verify(Value(args, "--verify") ?? ""); return 0; }
            if (Has(args, "--issue")) { Issue(args); return 0; }

            Console.WriteLine("EmberKeeper license tool");
            Console.WriteLine("  --genkeys <папка>                       создать ключи");
            Console.WriteLine("  --issue --name <имя> --machine <id|*> --days <n> [--out file]");
            Console.WriteLine("  --verify <ключ или файл>                проверить лицензию");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ОШИБКА: " + ex.Message);
            return 1;
        }
    }

    private static void GenKeys(string dir)
    {
        Directory.CreateDirectory(dir);
        using var rsa = RSA.Create(2048);
        var priv = Path.Combine(dir, "private.pem");
        var pub = Path.Combine(dir, "public.pem");
        File.WriteAllText(priv, rsa.ExportRSAPrivateKeyPem());
        File.WriteAllText(pub, rsa.ExportRSAPublicKeyPem());
        Console.WriteLine("Приватный ключ: " + priv + "  (НИКОМУ НЕ ПОКАЗЫВАТЬ)");
        Console.WriteLine("Публичный ключ: " + pub + "  (встраивается в программу)");
    }

    private static void Issue(string[] args)
    {
        var name = Value(args, "--name") ?? "Лицензиат";
        var machine = Value(args, "--machine") ?? "*";
        var daysText = Value(args, "--days") ?? "0";
        int days = int.TryParse(daysText, out int d) ? d : 0;
        var outFile = Value(args, "--out");
        var keyDir = Value(args, "--keys") ?? FindKeysDir();

        var privatePem = Path.Combine(keyDir, "private.pem");
        if (!File.Exists(privatePem))
            throw new InvalidOperationException("не найден приватный ключ: " + privatePem + " (сначала --genkeys)");

        var expires = days > 0 ? DateTime.Today.AddDays(days).ToString("yyyy-MM-dd") : "";

        var payload = new
        {
            name,
            machine,
            expires,
            edition = "full",
            issued = DateTime.Today.ToString("yyyy-MM-dd"),
        };
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(privatePem));
        var sig = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var key = Base64Url(payloadBytes) + "." + Base64Url(sig);

        if (!string.IsNullOrWhiteSpace(outFile))
        {
            File.WriteAllText(outFile!, key);
            Console.WriteLine("Лицензия записана: " + outFile);
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(key);
            Console.WriteLine();
        }
        Console.WriteLine($"Кому: {name} · Машина: {machine} · До: {(expires == "" ? "бессрочно" : expires)}");
    }

    private static void Verify(string input)
    {
        var key = File.Exists(input) ? File.ReadAllText(input).Trim() : input.Trim();
        var keyDir = FindKeysDir();
        var publicPem = Path.Combine(keyDir, "public.pem");
        if (!File.Exists(publicPem)) throw new InvalidOperationException("не найден public.pem в " + keyDir);

        var parts = key.Split('.');
        if (parts.Length != 2) throw new InvalidOperationException("неверный формат ключа");

        var payload = FromBase64Url(parts[0]);
        var sig = FromBase64Url(parts[1]);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(publicPem));
        bool ok = rsa.VerifyData(payload, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Console.WriteLine("Подпись: " + (ok ? "ВЕРНА" : "НЕВЕРНА"));
        Console.WriteLine("Данные: " + Encoding.UTF8.GetString(payload));
    }

    private static string FindKeysDir()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "keys"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "keys"),
            Path.Combine(Directory.GetCurrentDirectory(), "keys"),
        };
        foreach (var c in candidates)
            if (File.Exists(Path.Combine(c, "public.pem"))) return Path.GetFullPath(c);
        return Path.Combine(Directory.GetCurrentDirectory(), "keys");
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

    private static bool Has(string[] args, string flag)
    {
        foreach (var a in args)
            if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string? Value(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}
