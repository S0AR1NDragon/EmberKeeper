using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public partial class LibraryView : UserControl
{
    private static readonly (string Key, string Title, string Icon)[] Cats =
    {
        ("classes", "Классы", "⚔"),
        ("races", "Виды", "✦"),
        ("backgrounds", "Предыстории", "❖"),
        ("feats", "Черты", "✧"),
        ("spells", "Заклинания", "✴"),
        ("items", "Предметы", "◈"),
        ("bestiary", "Бестиарий", "☠"),
    };

    private string _edition = "2014";
    private int _cat = 4;
    private string _q = "";
    private Dictionary<string, int> _counts = new();
    private List<LibEntry> _all = new();
    private List<LibEntry> _view = new();
    private readonly List<Button> _chips = new();

    private string _fLevel = "все", _fSchool = "все", _fClass = "все", _fRarity = "все", _fType = "все", _fCr = "все", _fMtype = "все";

    public Action<string, string, string>? AddToMap;

    public LibraryView()
    {
        InitializeComponent();
        DataStore.Dir = System.IO.Path.Combine(AppContext.BaseDirectory, "data");
        _counts = DataStore.LoadCounts();
        BuildChips();
        Reload();
    }

    public void OpenBestiary(string search)
    {
        _edition = "2014";
        _cat = 6;
        _q = search;
        SearchBox.Text = search;
        Reload();
        Ed2014.Classes.Set("on", true);
        Ed2024.Classes.Set("on", false);
    }

    public void SearchIn(string edition, string cat, string query)
    {
        _edition = edition == "2024" ? "2024" : "2014";
        Ed2014.Classes.Set("on", _edition == "2014");
        Ed2024.Classes.Set("on", _edition == "2024");

        int idx = Array.FindIndex(Cats, c => c.Key == cat);
        if (idx >= 0) _cat = idx;

        SearchBox.Text = query;
        _q = query;
        Reload();
    }

    private void BuildChips()
    {
        Chips.Children.Clear();
        _chips.Clear();
        for (int i = 0; i < Cats.Length; i++)
        {
            int idx = i;
            var b = new Button { Classes = { "chip" } };
            b.Click += (_, _) => { _cat = idx; Reload(); };
            _chips.Add(b);
            Chips.Children.Add(b);
        }
        UpdateChips();
    }

    private void UpdateChips()
    {
        for (int i = 0; i < _chips.Count; i++)
        {
            var (key, title, icon) = Cats[i];
            _counts.TryGetValue($"{_edition}-{key}", out int cnt);
            _chips[i].Content = cnt > 0 ? $"{icon}  {title}  {cnt}" : $"{icon}  {title}";
            _chips[i].Classes.Set("on", i == _cat);
        }
    }

    private void Reload()
    {
        _all = DataStore.Load(_edition, Cats[_cat].Key);
        _fLevel = _fSchool = _fClass = _fRarity = _fType = _fCr = _fMtype = "все";
        UpdateChips();
        BuildFilters();
        ApplyFilter();
        StatusText.Text = $"{_edition} · {Cats[_cat].Title} · {_all.Count} записей";
    }

    private void ApplyFilter()
    {
        IEnumerable<LibEntry> q = _all;
        var s = _q.Trim().ToLowerInvariant();
        if (s.Length > 0)
            q = q.Where(x => $"{x.N} {x.E} {x.S} {x.H}".ToLowerInvariant().Contains(s));

        if (_cat == 4)
        {
            if (_fLevel != "все")
            {
                int lv = _fLevel == "Заговор" ? 0 : int.Parse(_fLevel.Split(' ')[0], CultureInfo.InvariantCulture);
                q = q.Where(x => x.Lvl == lv);
            }
            if (_fSchool != "все") q = q.Where(x => x.Sch == _fSchool);
            if (_fClass != "все") q = q.Where(x => x.Cls.Contains(_fClass));
        }
        else if (_cat == 5)
        {
            if (_fRarity != "все") q = q.Where(x => x.Rar == _fRarity);
            if (_fType != "все") q = q.Where(x => x.Typ == _fType);
        }
        else if (_cat == 6)
        {
            if (_fCr != "все") q = q.Where(x => x.Cr == _fCr);
            if (_fMtype != "все") q = q.Where(x => x.Mt == _fMtype);
        }

        _view = q.Take(1500).ToList();
        CountText.Text = $"{_view.Count} / {_all.Count}";

        LibList.ItemsSource = _view;
        LibList.ItemTemplate = new FuncDataTemplate<LibEntry>((e, _) => Row(e), true);
        if (_view.Count > 0) LibList.SelectedIndex = 0;
        else RenderDetail(null);
    }

    private Control Row(LibEntry e)
    {
        var name = new TextBlock { Text = e.N, FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = Pal.B("#f6e7cf") };
        var sub = new TextBlock { Text = e.Sub, FontSize = 12, Foreground = Pal.B("#9c8b78"), Margin = new Thickness(0, 3, 0, 0) };
        var left = new StackPanel();
        left.Children.Add(name);
        left.Children.Add(sub);

        var badge = new Border
        {
            Background = Pal.B("#3a2d24"),
            BorderBrush = Pal.B("#4a3728"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = e.LevelText, FontSize = 11, Foreground = Pal.B("#d99b4a") },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(10, 8) };
        grid.Children.Add(left);
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);
        return grid;
    }

    private void BuildFilters()
    {
        Filters.Children.Clear();
        if (_cat == 4)
        {
            AddFilter("Уровень", _all.Select(x => x.Lvl == null ? "" : (x.Lvl == 0 ? "Заговор" : $"{x.Lvl} ур.")), v => _fLevel = v, 104);
            AddFilter("Школа", _all.Select(x => x.Sch), v => _fSchool = v, 126);
            AddFilter("Класс", _all.SelectMany(x => x.Cls), v => _fClass = v, 126);
        }
        else if (_cat == 5)
        {
            AddFilter("Редкость", _all.Select(x => x.Rar), v => _fRarity = v, 120);
            AddFilter("Тип", _all.Select(x => x.Typ), v => _fType = v, 120);
        }
        else if (_cat == 6)
        {
            AddFilter("Опасность", _all.Select(x => x.Cr), v => _fCr = v, 100);
            AddFilter("Тип", _all.Select(x => x.Mt), v => _fMtype = v, 120);
        }
    }

    private void AddFilter(string title, IEnumerable<string> values, Action<string> set, double width)
    {
        var opts = new List<string> { "все" };
        opts.AddRange(values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v, StringComparer.CurrentCulture));
        if (opts.Count < 3) return;

        var combo = new ComboBox { Width = width, ItemsSource = opts, SelectedIndex = 0 };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string s) { set(s); ApplyFilter(); }
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = title, Foreground = Pal.B("#9c8b78"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        stack.Children.Add(combo);
        Filters.Children.Add(stack);
    }

    private void OnEdition(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string ed)
        {
            _edition = ed;
            Ed2014.Classes.Set("on", ed == "2014");
            Ed2024.Classes.Set("on", ed == "2024");
            Reload();
        }
    }

    private void OnSearch(object? sender, TextChangedEventArgs e)
    {
        _q = SearchBox.Text ?? "";
        ApplyFilter();
    }

    private void OnSelect(object? sender, SelectionChangedEventArgs e)
    {
        RenderDetail(LibList.SelectedItem as LibEntry);
    }

    private void RenderDetail(LibEntry? x)
    {
        DetailHost.Children.Clear();
        if (x == null) return;

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        head.Children.Add(new Border { Width = 3, Height = 34, CornerRadius = new CornerRadius(2), Background = Pal.B("#d99b4a") });
        var titles = new StackPanel();
        titles.Children.Add(new TextBlock { Text = x.N, FontFamily = new FontFamily("Georgia"), FontSize = 30, FontWeight = FontWeight.Bold, Foreground = Pal.B("#f6e7cf") });
        if (!string.IsNullOrEmpty(x.E))
            titles.Children.Add(new TextBlock { Text = x.E, FontStyle = FontStyle.Italic, Foreground = Pal.B("#9c8b78") });
        head.Children.Add(titles);
        DetailHost.Children.Add(head);

        if (!string.IsNullOrEmpty(x.S))
            DetailHost.Children.Add(new TextBlock { Text = x.S, Foreground = Pal.B("#d99b4a"), FontSize = 14, Margin = new Thickness(13, 0, 0, 0) });

        if (x.P.Count > 0)
        {
            var rows = new StackPanel();
            bool alt = false;
            foreach (var line in x.P)
            {
                int i = line.IndexOf(':');
                string k = i > 0 ? line[..i].Trim() : "•";
                string v = i > 0 ? line[(i + 1)..].Trim() : line;

                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("205,*") };
                var keyBorder = new Border
                {
                    BorderBrush = Pal.B("#2a211a"),
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    Padding = new Thickness(14, 10),
                    Child = new TextBlock { Text = k, Foreground = Pal.B("#9c8b78"), FontSize = 13 },
                };
                var val = new TextBlock { Text = v, Margin = new Thickness(14, 10), FontSize = 13, TextWrapping = TextWrapping.Wrap };
                grid.Children.Add(keyBorder);
                Grid.SetColumn(val, 1);
                grid.Children.Add(val);

                rows.Children.Add(new Border
                {
                    Background = alt ? Pal.B("#15110e") : Brushes.Transparent,
                    BorderBrush = Pal.B("#2a211a"),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = grid,
                });
                alt = !alt;
            }

            DetailHost.Children.Add(new Border
            {
                Background = Pal.B("#1c1714"),
                BorderBrush = Pal.B("#2a211a"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(13, 10, 0, 0),
                Child = rows,
            });
        }

        DetailHost.Children.Add(new TextBlock
        {
            Text = x.H,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14.5,
            LineHeight = 24,
            Foreground = Pal.B("#c9b8a4"),
            Margin = new Thickness(13, 16, 0, 0),
        });

        var btn = new Button
        {
            Content = "→ на карту",
            Background = Pal.B("#4a3728"),
            Foreground = Pal.B("#f0c27b"),
            BorderBrush = Pal.B("#4a3728"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 9),
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(13, 18, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var name = x.N;
        var cr = x.Cr;
        var mt = x.Mt;
        btn.Click += (_, _) => AddToMap?.Invoke(name, cr, mt);
        DetailHost.Children.Add(btn);
    }
}
