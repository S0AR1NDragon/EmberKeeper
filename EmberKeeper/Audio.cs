using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace EmberKeeper;

/// <summary>Простое воспроизведение аудио через MCI (mp3/wav/wma) без внешнего плеера.</summary>
public static class Audio
{
    private const string Alias = "dm_audio";

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, StringBuilder? ret, int retLen, IntPtr hwnd);

    public static string? CurrentPath { get; private set; }

    public static bool Play(string path)
    {
        try
        {
            Stop();
            var open = mciSendString($"open \"{path}\" type mpegvideo alias {Alias}", null, 0, IntPtr.Zero);
            if (open != 0)
                open = mciSendString($"open \"{path}\" alias {Alias}", null, 0, IntPtr.Zero);
            if (open != 0) return false;

            mciSendString($"play {Alias}", null, 0, IntPtr.Zero);
            CurrentPath = path;
            return true;
        }
        catch { return false; }
    }

    public static void Stop()
    {
        try
        {
            mciSendString($"stop {Alias}", null, 0, IntPtr.Zero);
            mciSendString($"close {Alias}", null, 0, IntPtr.Zero);
            CurrentPath = null;
        }
        catch { }
    }
}

public enum PlayerScreen
{
    Map,
    Scene,
    Blank,
}

/// <summary>События реального времени для второго экрана.</summary>
public static class Live
{
    /// <summary>Карта изменилась (токены, стены, туман, сетка).</summary>
    public static event Action? MapChanged;
    public static void RaiseMapChanged() => MapChanged?.Invoke();

    public static PlayerScreen Screen { get; private set; } = PlayerScreen.Map;
    public static Scene? Scene { get; private set; }
    public static MapDoc? Doc { get; private set; }
    public static PlayerWindow? Window { get; set; }

    public static event Action? ScreenChanged;

    public static void AttachDoc(MapDoc doc) => Doc = doc;

    public static void ShowMap()
    {
        Screen = PlayerScreen.Map;
        ScreenChanged?.Invoke();
        Window?.Activate();
    }

    public static void ShowScene(Scene scene)
    {
        Scene = scene;
        Screen = PlayerScreen.Scene;
        ScreenChanged?.Invoke();
        Window?.Activate();
    }

    public static void ShowBlank()
    {
        Screen = PlayerScreen.Blank;
        ScreenChanged?.Invoke();
        Window?.Activate();
    }

    public static bool EnsureWindow()
    {
        if (Window != null) return true;
        if (Doc == null)
        {
            try
            {
                var book = MapStore.Load();
                Doc = book.Maps[Math.Clamp(book.Active, 0, book.Maps.Count - 1)];
            }
            catch { }
        }
        if (Doc == null) return false;
        Window = new PlayerWindow(Doc);
        Window.Show();
        return true;
    }
}
