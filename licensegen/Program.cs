using System;
using Avalonia;

namespace EmberKeeperLicenseGen;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // CLI-режим (для автоматизации): без GUI
        if (args.Length > 0) return Cli.Run(args);

        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Crash(e.ExceptionObject?.ToString() ?? "unknown");
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Crash(ex.ToString());
            return 1;
        }
    }

    private static void Crash(string text)
    {
        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "licensegen-crash.log"), text);
        }
        catch { }
    }
}
