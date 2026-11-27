using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>
/// Свой выбор файлов без системного диалога — обходит падающее расширение оболочки
/// (BX ShellExt / GetTempFileName failed) и работает с любым окружением.
/// </summary>
public sealed class FilePickerDialog : Window
{
    private readonly string[] _patterns;
    private string _current = "";
    private readonly TextBox _pathBox;
    private readonly ListBox _folders;
    private readonly ListBox _files;
    private readonly TextBlock _status;
    private readonly List<string> _driveRoots = new();

    private static Color Bg => Pal.C("#100d0b");
    private static Color Panel => Pal.C("#1c1714");
    private static Color Panel2 => Pal.C("#221b16");
    private static Color Line => Pal.C("#352a21");
    private static Color Gold => Pal.C("#f0c27b");
    private static Color Ink => Pal.C("#efe3d2");
    private static Color Muted => Pal.C("#9c8b78");

    public string? SelectedPath { get; private set; }

    public FilePickerDialog(string title, string patterns)
    {
        _patterns = patterns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Title = title;
        Width = 860;
        Height = 620;
        Background = new SolidColorBrush(Bg);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(16) };

        // путь
        var pathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        _pathBox = new TextBox
        {
            Background = new SolidColorBrush(Panel2),
            Foreground = new SolidColorBrush(Ink),
            BorderBrush = new SolidColorBrush(Line),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7),
            FontSize = 13,
        };
        _pathBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) Navigate(_pathBox.Text ?? _current); };
        var up = Button("↑ Вверх", () => Navigate(ParentOf(_current)));
        var go = Button("Перейти", () => Navigate(_pathBox.Text ?? _current));
        pathRow.Children.Add(up);
        Grid.SetColumn(_pathBox, 1);
        _pathBox.Margin = new Thickness(8, 0);
        pathRow.Children.Add(_pathBox);
        Grid.SetColumn(go, 2);
        pathRow.Children.Add(go);
        Grid.SetRow(pathRow, 0);
        root.Children.Add(pathRow);

        // быстрые кнопки
        var quick = new WrapPanel { Margin = new Thickness(0, 10, 0, 10) };
        quick.Children.Add(Button("🏠 Домашняя", () => Navigate(Safe(Environment.SpecialFolder.UserProfile))));
        quick.Children.Add(Button("🖥 Рабочий стол", () => Navigate(Safe(Environment.SpecialFolder.Desktop))));
        quick.Children.Add(Button("⬇ Загрузки", () => Navigate(Path.Combine(Safe(Environment.SpecialFolder.UserProfile), "Downloads"))));
        quick.Children.Add(Button("🖼 Изображения", () => Navigate(Safe(Environment.SpecialFolder.MyPictures))));
        quick.Children.Add(Button("🎵 Музыка", () => Navigate(Safe(Environment.SpecialFolder.MyMusic))));

        var driveCombo = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 6, 6), Background = new SolidColorBrush(Panel2), Foreground = new SolidColorBrush(Ink), BorderBrush = new SolidColorBrush(Line) };
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                _driveRoots.Add(d.RootDirectory.FullName);
                driveCombo.Items.Add(d.Name.TrimEnd('\\'));
            }
            catch { }
        }
        driveCombo.SelectionChanged += (_, _) =>
        {
            int i = driveCombo.SelectedIndex;
            if (i >= 0 && i < _driveRoots.Count) Navigate(_driveRoots[i]);
        };
        quick.Children.Add(driveCombo);
        Grid.SetRow(quick, 1);
        root.Children.Add(quick);

        // списки
        var lists = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*") };
        _folders = new ListBox { Background = new SolidColorBrush(Panel), BorderThickness = new Thickness(0) };
        _folders.DoubleTapped += (_, _) =>
        {
            int i = _folders.SelectedIndex;
            if (i >= 0 && i < _folderPaths.Count) Navigate(_folderPaths[i]);
        };
        var foldersScroll = new ScrollViewer { Content = _folders };
        var fBorder = new Border
        {
            Background = new SolidColorBrush(Panel),
            BorderBrush = new SolidColorBrush(Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10, 0, 0, 10),
            Child = foldersScroll,
        };
        lists.Children.Add(fBorder);

        _files = new ListBox { Background = new SolidColorBrush(Panel), BorderThickness = new Thickness(0) };
        _files.DoubleTapped += (_, _) => Confirm();
        _files.SelectionChanged += (_, _) =>
        {
            int i = _files.SelectedIndex;
            if (i >= 0 && i < _filePaths.Count) _pathBox.Text = _filePaths[i];
        };
        var filesScroll = new ScrollViewer { Content = _files };
        var fBorder2 = new Border
        {
            Background = new SolidColorBrush(Panel),
            BorderBrush = new SolidColorBrush(Line),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(0, 10, 10, 0),
            Child = filesScroll,
        };
        Grid.SetColumn(fBorder2, 1);
        lists.Children.Add(fBorder2);
        Grid.SetRow(lists, 2);
        root.Children.Add(lists);

        // низ
        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, 12, 0, 0) };
        _status = new TextBlock { Text = "выберите папку или файл", Foreground = new SolidColorBrush(Muted), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        bottom.Children.Add(_status);
        var choose = Button("Выбрать", Confirm);
        choose.Background = new SolidColorBrush(Pal.C("#cf9245"));
        choose.Foreground = new SolidColorBrush(Pal.C("#241505"));
        choose.BorderBrush = new SolidColorBrush(Pal.C("#e0b072"));
        choose.FontWeight = FontWeight.Bold;
        Grid.SetColumn(choose, 1);
        bottom.Children.Add(choose);
        var cancel = Button("Отмена", () => Close());
        Grid.SetColumn(cancel, 2);
        bottom.Children.Add(cancel);
        Grid.SetRow(bottom, 3);
        root.Children.Add(bottom);

        Content = root;

        Navigate(Safe(Environment.SpecialFolder.MyPictures));
    }

    private static string Safe(Environment.SpecialFolder folder)
    {
        try { return Environment.GetFolderPath(folder); } catch { return ""; }
    }

    private static string ParentOf(string dir)
    {
        try
        {
            var parent = Directory.GetParent(dir);
            return parent?.FullName ?? dir;
        }
        catch { return dir; }
    }

    private Button Button(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text,
            Background = new SolidColorBrush(Panel2),
            Foreground = new SolidColorBrush(Ink),
            BorderBrush = new SolidColorBrush(Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(11, 6),
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 6, 6),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private void Navigate(string dir)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
            _current = Path.GetFullPath(dir);
            _pathBox.Text = _current;

            var folders = new List<string>();
            var files = new List<string>();

            foreach (var d in Directory.GetDirectories(_current).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                folders.Add(d);

            foreach (var f in Directory.GetFiles(_current).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
            {
                if (Matches(f)) files.Add(f);
            }

            _folders.ItemsSource = folders.Select(d => new FolderItem(d)).ToList();
            _folders.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FolderItem>((fi, _) =>
                new TextBlock
                {
                    Text = "📁  " + Path.GetFileName(fi.FullPath),
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Ink),
                    Margin = new Thickness(8, 6),
                }, true);

            _files.ItemsSource = files.Select(f => new FileItem(f)).ToList();
            _files.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FileItem>((fi, _) =>
            {
                var sp = new StackPanel { Margin = new Thickness(8, 6), Spacing = 1 };
                sp.Children.Add(new TextBlock { Text = "🖼  " + Path.GetFileName(fi.FullPath), FontSize = 13, Foreground = new SolidColorBrush(Ink) });
                sp.Children.Add(new TextBlock { Text = SizeLabel(fi.FullPath), FontSize = 10.5, Foreground = new SolidColorBrush(Muted) });
                return sp;
            }, true);

            // для обработки выбора элемента списка папок нужен путь
            _folderPaths = folders.ToList();
            _filePaths = files.ToList();

            _status.Text = $"папок: {folders.Count} · подходящих файлов: {files.Count}";
        }
        catch (Exception ex)
        {
            _status.Text = "не удалось открыть: " + ex.Message;
        }
    }

    private List<string> _folderPaths = new();
    private List<string> _filePaths = new();

    private bool Matches(string path)
    {
        var ext = Path.GetExtension(path);
        foreach (var p in _patterns)
        {
            var pp = p.Trim();
            if (pp.StartsWith("*.", StringComparison.Ordinal))
            {
                if (string.Equals(ext, pp.Substring(1), StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (string.Equals(Path.GetFileName(path), pp, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return _patterns.Length == 0;
    }

    private static string SizeLabel(string path)
    {
        try
        {
            var len = new FileInfo(path).Length;
            if (len > 1024 * 1024) return (len / 1024.0 / 1024.0).ToString("0.0") + " МБ";
            return Math.Max(1, len / 1024) + " КБ";
        }
        catch { return ""; }
    }

    private void Confirm()
    {
        string? path = null;
        if (_files.SelectedIndex >= 0 && _files.SelectedIndex < _filePaths.Count)
            path = _filePaths[_files.SelectedIndex];
        else if (!string.IsNullOrWhiteSpace(_pathBox.Text) && File.Exists(_pathBox.Text))
            path = _pathBox.Text;

        if (path == null || !File.Exists(path))
        {
            _status.Text = "выберите файл (двойной клик)";
            return;
        }
        SelectedPath = path;
        Close();
    }

    /// <summary>Показать диалог и вернуть выбранный путь (или null).</summary>
    public static async Task<string?> Pick(Window? owner, string title, string patterns)
    {
        var dlg = new FilePickerDialog(title, patterns);
        if (owner != null) await dlg.ShowDialog(owner);
        else dlg.Show();
        return dlg.SelectedPath;
    }

    private sealed record FolderItem(string FullPath);
    private sealed record FileItem(string FullPath);
}
