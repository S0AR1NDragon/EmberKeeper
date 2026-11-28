using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace EmberKeeperLicenseGen;

/// <summary>Окно генератора лицензий (без системной рамки).</summary>
public sealed class LicenseWindow : Window
{
    private readonly TextBox _name;
    private readonly TextBox _machine;
    private readonly TextBox _days;
    private readonly CheckBox _anyPc;
    private readonly SelectableTextBlock _result;
    private readonly TextBlock _status;

    public LicenseWindow()
    {
        Title = "EmberKeeper — генератор лицензий";
        Width = 760;
        Height = 780;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var shell = new Border
        {
            Background = LgTheme.BrBg,
            BorderBrush = LgTheme.BrLine,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            ClipToBounds = true,
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };

        /* ----- своя рамка ----- */
        var header = new Border
        {
            Background = LgTheme.BrBg2,
            BorderBrush = LgTheme.BrLine,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 13),
        };
        var hrow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        brand.Children.Add(new Border
        {
            Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
            Background = LgTheme.BrPanel, BorderBrush = LgTheme.BrGold, BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "E", FontFamily = new FontFamily("Georgia"), FontSize = 15, FontWeight = FontWeight.Bold,
                Foreground = LgTheme.BrGold, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });
        brand.Children.Add(new StackPanel
        {
            Spacing = 0,
            Children =
            {
                new TextBlock
                {
                    Text = "Генератор лицензий", FontFamily = new FontFamily("Georgia"),
                    FontSize = 17, FontWeight = FontWeight.Bold, Foreground = LgTheme.BrInk,
                },
                new TextBlock
                {
                    Text = "используется только владельцем программы", FontSize = 10,
                    Foreground = LgTheme.BrMuted,
                },
            },
        });
        hrow.Children.Add(brand);
        var close = new Button
        {
            Content = "✕", Width = 30, Height = 26, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(7), FontSize = 12,
            Background = LgTheme.BrPanel, Foreground = LgTheme.BrInk,
            BorderBrush = LgTheme.BrLine, BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 3);
        hrow.Children.Add(close);
        header.Child = hrow;
        header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        /* ----- содержимое ----- */
        var content = new StackPanel { Margin = new Thickness(24, 18, 24, 20), Spacing = 14 };

        // темы
        content.Children.Add(Label("ОФОРМЛЕНИЕ"));
        var themes = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        foreach (var (id, label, hue) in LgTheme.Styles)
        {
            bool selected = LgTheme.Style == id;
            var cell = new Border
            {
                Width = 86, Height = 64, CornerRadius = new CornerRadius(12),
                Background = LgTheme.BrPanel,
                BorderBrush = selected ? LgTheme.BrAccent : LgTheme.BrLine,
                BorderThickness = new Thickness(selected ? 2 : 1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var inner = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            inner.Children.Add(new Border
            {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(LgTheme.SwatchColor(hue)),
            });
            inner.Children.Add(new TextBlock
            {
                Text = label, FontSize = 10.5,
                Foreground = selected ? LgTheme.BrInk : LgTheme.BrMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            cell.Child = inner;
            cell.PointerPressed += (_, _) => SwitchTheme(LgTheme.Dark, id);
            themes.Children.Add(cell);
        }
        content.Children.Add(themes);

        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        modeRow.Children.Add(ModeBtn("Тёмная", true));
        modeRow.Children.Add(ModeBtn("Светлая", false));
        content.Children.Add(modeRow);

        content.Children.Add(new Border { Height = 1, Background = LgTheme.BrLine, Margin = new Thickness(0, 2) });

        // поля
        content.Children.Add(Label("ДАННЫЕ ЛИЦЕНЗИИ"));
        content.Children.Add(Field("Имя или название", "Иван / Гильдия «Стальной клинок»", out _name));
        content.Children.Add(Field("Код компьютера клиента", "клиент видит код в окне активации", out _machine));
        _anyPc = new CheckBox
        {
            Content = "Лицензия на любой компьютер (без привязки)",
            Foreground = LgTheme.BrInk, FontSize = 12,
        };
        _anyPc.IsCheckedChanged += (_, _) =>
        {
            _machine.IsEnabled = _anyPc.IsChecked != true;
        };
        content.Children.Add(_anyPc);
        content.Children.Add(Field("Срок в днях (0 — бессрочно)", "0", out _days));

        // выпуск
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var make = new Button
        {
            Content = "Выпустить лицензию", Padding = new Thickness(18, 10), CornerRadius = new CornerRadius(10),
            Background = LgTheme.BrAccent, Foreground = LgTheme.BrAccentText, BorderBrush = LgTheme.BrGold,
            BorderThickness = new Thickness(1), FontWeight = FontWeight.Bold, FontSize = 13,
        };
        make.Click += (_, _) => Generate();
        actions.Children.Add(make);
        var copy = new Button
        {
            Content = "Копировать ключ", Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10),
            Background = LgTheme.BrPanel, Foreground = LgTheme.BrInk, BorderBrush = LgTheme.BrLine,
            BorderThickness = new Thickness(1), FontSize = 12.5,
        };
        copy.Click += async (_, _) => await CopyKeyAsync();
        actions.Children.Add(copy);
        var save = new Button
        {
            Content = "Сохранить .key…", Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10),
            Background = LgTheme.BrPanel, Foreground = LgTheme.BrInk, BorderBrush = LgTheme.BrLine,
            BorderThickness = new Thickness(1), FontSize = 12.5,
        };
        save.Click += async (_, _) => await SaveKeyAsync();
        actions.Children.Add(save);
        content.Children.Add(actions);

        _status = new TextBlock
        {
            Text = "Заполните данные и нажмите «Выпустить лицензию».",
            FontSize = 12, Foreground = LgTheme.BrMuted, TextWrapping = TextWrapping.Wrap,
        };
        content.Children.Add(_status);

        _result = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = LgTheme.BrInk,
            Background = Brushes.Transparent,
            Text = "",
        };
        var resultBorder = new Border
        {
            Height = 120,
            Background = LgTheme.BrPanel,
            BorderBrush = LgTheme.BrLine,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8),
            Child = new ScrollViewer { Content = _result },
        };
        content.Children.Add(resultBorder);

        var scroll = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        shell.Child = root;
        Content = shell;

        if (Environment.GetEnvironmentVariable("LICENSEGEN_TEST") == "1")
        {
            Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                _name.Text = "Тестовый клиент";
                _machine.Text = "7916D46F35318F03";
                _days.Text = "365";
                Generate();
            }, TimeSpan.FromMilliseconds(500));
        }
    }

    private Control Label(string text) => new TextBlock
    {
        Text = text, FontSize = 10.5, LetterSpacing = 1.1, FontWeight = FontWeight.Bold,
        Foreground = LgTheme.BrGold,
    };

    private Control Field(string label, string watermark, out TextBox box)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 10.5, Foreground = LgTheme.BrMuted });
        box = new TextBox
        {
            Watermark = watermark, FontSize = 13, Height = 36,
            Background = LgTheme.BrPanel, Foreground = LgTheme.BrInk,
            BorderBrush = LgTheme.BrLine, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6),
        };
        sp.Children.Add(box);
        return sp;
    }

    private Button ModeBtn(string label, bool dark)
    {
        bool selected = LgTheme.Dark == dark;
        var b = new Button
        {
            Content = label, Padding = new Thickness(16, 7), CornerRadius = new CornerRadius(9),
            FontSize = 12, FontWeight = selected ? FontWeight.Bold : FontWeight.Normal,
            Background = selected ? LgTheme.BrAccent : LgTheme.BrPanel,
            Foreground = selected ? LgTheme.BrAccentText : LgTheme.BrInk,
            BorderBrush = selected ? LgTheme.BrGold : LgTheme.BrLine,
            BorderThickness = new Thickness(1),
        };
        b.Click += (_, _) => SwitchTheme(dark, LgTheme.Style);
        return b;
    }

    private void SwitchTheme(bool dark, string style)
    {
        LgTheme.Dark = dark;
        LgTheme.Style = style;
        SaveTheme();

        var next = new LicenseWindow();
        next.Show();
        Close();
    }

    private void SaveTheme()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "licensegen-settings.json");
            File.WriteAllText(path, "{\"dark\":" + (LgTheme.Dark ? "true" : "false") + ",\"style\":\"" + LgTheme.Style + "\"}");
        }
        catch { }
    }

    private void Generate()
    {
        var name = (_name.Text ?? "").Trim();
        if (name.Length == 0)
        {
            _status.Text = "Укажите имя или название.";
            return;
        }
        var machine = _anyPc.IsChecked == true ? "*" : (_machine.Text ?? "").Trim();
        if (machine.Length == 0) machine = "*";
        var days = 0;
        if (int.TryParse((_days.Text ?? "0").Trim(), out var d)) days = Math.Max(0, d);

        try
        {
            var key = Cli.BuildKey(name, machine, days);
            _result.Text = key;
            string saved = "";
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "licenses");
                Directory.CreateDirectory(dir);
                var safe = SafeName(name);
                var path = Path.Combine(dir, safe + ".key");
                File.WriteAllText(path, key);
                saved = path;
            }
            catch { }

            var until = days > 0 ? DateTime.Today.AddDays(days).ToString("dd.MM.yyyy") : "бессрочно";
            _status.Text = "Лицензия выпущена · до: " + until + (saved.Length > 0 ? " · сохранена: " + saved : "");
        }
        catch (Exception ex)
        {
            _status.Text = "Ошибка: " + ex.Message;
        }
    }

    private static string SafeName(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in name)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
        var s = sb.ToString().Trim().Trim('_');
        return s.Length == 0 ? "license-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") : s;
    }

    private async Task CopyKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(_result.Text)) { _status.Text = "Сначала выпустите лицензию."; return; }
        var clip = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clip != null) await clip.SetTextAsync(_result.Text!);
        _status.Text = "Ключ скопирован в буфер обмена.";
    }

    private async Task SaveKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(_result.Text)) { _status.Text = "Сначала выпустите лицензию."; return; }
        var dlg = new SaveFileDialog
        {
            Title = "Сохранить лицензию",
            DefaultExtension = "key",
            InitialFileName = "license.key",
            Filters = { new FileDialogFilter { Name = "Лицензия", Extensions = { "key", "txt" } } },
        };
        try
        {
            var file = await dlg.ShowAsync(this);
            if (string.IsNullOrEmpty(file)) return;
            await File.WriteAllTextAsync(file, _result.Text!);
            _status.Text = "Сохранено: " + file;
        }
        catch (Exception ex) { _status.Text = "Ошибка сохранения: " + ex.Message; }
    }
}
