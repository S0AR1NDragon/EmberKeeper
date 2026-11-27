using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace EmberKeeper;

/// <summary>Окно проверки и установки обновлений с GitHub Releases.</summary>
public sealed class UpdateWindow : Window
{
    private readonly TextBlock _status;
    private readonly TextBlock _notes;
    private readonly ProgressBar _progress;
    private readonly Button _checkBtn;
    private readonly Button _installBtn;
    private readonly TextBlock _licenseText;

    private UpdateInfo? _update;
    private CancellationTokenSource? _cts;

    public UpdateWindow()
    {
        Title = "Обновления EmberKeeper";
        Width = 620;
        Height = 560;
        CanResize = false;
        Background = Pal.B("#100d0b");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(26), Spacing = 12 };

        root.Children.Add(new TextBlock
        {
            Text = "Обновления",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f0c27b"),
        });

        root.Children.Add(new TextBlock
        {
            Text = "Текущая версия: " + Updater.Version,
            FontSize = 13,
            Foreground = Pal.B("#efe3d2"),
        });

        _licenseText = new TextBlock
        {
            Text = "Лицензия: " + License.Describe(),
            FontSize = 11.5,
            Foreground = Pal.B("#9c8b78"),
            TextWrapping = TextWrapping.Wrap,
        };
        root.Children.Add(_licenseText);

        var channel = Updater.Channel;
        root.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(channel.Repo)
                ? "Канал обновлений не настроен (update.json)"
                : "Канал: github.com/" + channel.Repo,
            FontSize = 11,
            Foreground = Pal.B("#8d7a66"),
            TextWrapping = TextWrapping.Wrap,
        });

        _status = new TextBlock
        {
            Text = "",
            FontSize = 12.5,
            Foreground = Pal.B("#efe3d2"),
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 20,
            Margin = new Thickness(0, 4, 0, 0),
        };
        root.Children.Add(_status);

        _notes = new TextBlock
        {
            Text = "",
            FontSize = 12,
            Foreground = Pal.B("#c9b8a4"),
            TextWrapping = TextWrapping.Wrap,
        };
        var notesScroll = new ScrollViewer
        {
            Content = _notes,
            Height = 180,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Background = Pal.B("#15110e"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10),
            IsVisible = false,
        };
        root.Children.Add(notesScroll);
        _notesScroll = notesScroll;

        _progress = new ProgressBar { Height = 8, Minimum = 0, Maximum = 1, IsVisible = false };
        root.Children.Add(_progress);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        _checkBtn = Btn("Проверить обновления", async () => await CheckAsync(), true);
        _installBtn = Btn("Скачать и установить", async () => await DownloadAndInstallAsync(), true);
        _installBtn.IsVisible = false;
        buttons.Children.Add(_checkBtn);
        buttons.Children.Add(_installBtn);
        buttons.Children.Add(Btn("Закрыть", Close, false));
        root.Children.Add(buttons);

        Content = new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    private ScrollViewer _notesScroll = null!;

    private async Task CheckAsync()
    {
        _checkBtn.IsEnabled = false;
        _installBtn.IsVisible = false;
        _notesScroll.IsVisible = false;
        _status.Text = "Проверяю GitHub…";
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            var (info, error) = await Updater.CheckAsync(_cts.Token);
            if (!string.IsNullOrEmpty(error)) { _status.Text = error; return; }
            if (info == null)
            {
                _status.Text = $"У вас последняя версия ({Updater.Version}).";
                return;
            }

            _update = info;
            _status.Text = $"Доступна новая версия {info.Version}.";
            _notes.Text = string.IsNullOrWhiteSpace(info.Notes) ? "Список изменений не указан." : info.Notes;
            _notesScroll.IsVisible = true;
            _installBtn.IsVisible = true;
        }
        finally
        {
            _checkBtn.IsEnabled = true;
        }
    }

    private async Task DownloadAndInstallAsync()
    {
        if (_update == null) return;
        _installBtn.IsVisible = false;
        _progress.IsVisible = true;
        _progress.Value = 0;
        _status.Text = "Загружаю обновление…";

        var progress = new Progress<double>(p =>
        {
            _progress.Value = p;
            _status.Text = $"Загружено {p * 100:0}%";
        });

        var (path, error) = await Updater.DownloadAsync(_update, progress);
        if (path == null)
        {
            _status.Text = error;
            _progress.IsVisible = false;
            _installBtn.IsVisible = true;
            return;
        }

        var installDir = AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar);
        string runError;

        if (_update.AssetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            _status.Text = "Распаковываю обновление…";
            if (!await Updater.ApplyZipAsync(path, installDir, "EmberKeeper.exe", m => _status.Text = m))
            {
                _status.Text = "Не удалось подготовить обновление. Скачайте установщик с GitHub вручную.";
                _installBtn.IsVisible = true;
                _progress.IsVisible = false;
                return;
            }
            _status.Text = "Программа закроется и запустится снова с новой версией…";
            await Task.Delay(1200);
        }
        else if (!Updater.RunInstaller(path, installDir, out runError))
        {
            _status.Text = "Не удалось запустить установщик: " + runError;
            _installBtn.IsVisible = true;
            return;
        }
        else
        {
            _status.Text = "Устанавливаю — программа закроется и запустится снова…";
            await Task.Delay(1500);
        }
        if (Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life)
            life.Shutdown();
    }

    private static Button Btn(string text, Func<Task> onClick, bool accent)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(9),
            FontSize = 12.5,
            Background = accent ? Pal.B("#cf9245") : Pal.B("#221b16"),
            Foreground = accent ? Pal.B("#241505") : Pal.B("#efe3d2"),
            BorderBrush = accent ? Pal.B("#e0b072") : Pal.B("#352a21"),
            BorderThickness = new Thickness(1),
            FontWeight = accent ? FontWeight.Bold : FontWeight.Normal,
        };
        b.Click += async (_, _) => await onClick();
        return b;
    }

    private static Button Btn(string text, Action onClick, bool accent)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(9),
            FontSize = 12.5,
            Background = accent ? Pal.B("#cf9245") : Pal.B("#221b16"),
            Foreground = accent ? Pal.B("#241505") : Pal.B("#efe3d2"),
            BorderBrush = accent ? Pal.B("#e0b072") : Pal.B("#352a21"),
            BorderThickness = new Thickness(1),
            FontWeight = accent ? FontWeight.Bold : FontWeight.Normal,
        };
        b.Click += (_, _) => onClick();
        return b;
    }
}
