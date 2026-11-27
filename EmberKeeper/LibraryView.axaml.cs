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
    private static readonly (string Key, string Title)[] Cats =
    {
        ("classes", "Классы"),
        ("races", "Виды"),
        ("backgrounds", "Предыстории"),
        ("feats", "Черты"),
        ("spells", "Заклинания"),
        ("items", "Предметы"),
        ("bestiary", "Бестиарий"),
    };

    private string _edition = "2014";
    private int _cat = 4;
    private string _q = "";
    private Dictionary<string, int> _counts = new();
    private List<LibEntry> _all = new();
    private List<LibEntry> _view = new();
    private readonly List<Button> _chips = new();

    private string _fLevel = "все", _fSchool = "все", _fClass = "все", _fRarity = "все", _fType = "все", _fCr = "все", _fMtype = "все";

    // состояние статьи
    private LibEntry? _cur;
    private string _tab = "main";
    private int? _sub;

    public Action<string, string, string>? AddToMap;
    public Action<string, string, int>? CreateHero;

    public LibraryView()
    {
        InitializeComponent();
        DataStore.Dir = System.IO.Path.Combine(AppContext.BaseDirectory, "data");
        _counts = DataStore.LoadCounts();
        var libTest = Environment.GetEnvironmentVariable("EMBERKEEPER_LIB_TEST");
        if (!string.IsNullOrEmpty(libTest)) _cat = 0;
        BuildChips();
        Reload();
        if (libTest == "subs") { _tab = "subs"; RenderDetail(); }
        else if (libTest == "sub") { _sub = 0; _tab = "features"; RenderDetail(); }
        else if (libTest == "table") { _tab = "table"; RenderDetail(); }
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

    /* ==================== список ==================== */

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
            var (key, title) = Cats[i];
            _counts.TryGetValue($"{_edition}-{key}", out int cnt);
            _chips[i].Content = cnt > 0 ? $"{title}  {cnt}" : title;
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
                q = q.Where(x => (x.Lvl ?? -99) == lv);
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
        else { _cur = null; RenderDetail(); }
    }

    private Control Row(LibEntry e)
    {
        var name = new TextBlock { Text = e.N, FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = Pal.B("#f6e7cf"), TextWrapping = TextWrapping.Wrap };
        var subText = e.S == "" ? e.E : e.S;
        var sub = new TextBlock { Text = subText, FontSize = 11.5, Foreground = Pal.B("#9c8b78"), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
        var left = new StackPanel();
        left.Children.Add(name);
        if (!string.IsNullOrWhiteSpace(subText)) left.Children.Add(sub);

        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(e.LevelText))
            badges.Children.Add(Badge(e.LevelText, "#d99b4a"));
        if (e.Subs is { Count: > 0 })
            badges.Children.Add(Badge(e.Subs.Count + " подкл.", "#9c8b78"));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(10, 9) };
        grid.Children.Add(left);
        Grid.SetColumn(badges, 1);
        grid.Children.Add(badges);
        return grid;
    }

    private static Border Badge(string text, string hex) => new()
    {
        Background = Pal.B("#3a2d24"),
        BorderBrush = Pal.B("#4a3728"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(9),
        Padding = new Thickness(8, 3),
        Child = new TextBlock { Text = text, FontSize = 10.5, Foreground = Pal.B(hex) },
    };

    /* ==================== фильтры ==================== */

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

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 14, 6) };
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
        _cur = LibList.SelectedItem as LibEntry;
        _tab = "main";
        _sub = null;
        RenderDetail();
    }

    /* ==================== статья ==================== */

    private void RenderDetail()
    {
        DetailHost.Children.Clear();
        if (_cur == null) return;
        var x = _cur;
        if (_cat == 0) { RenderClass(x); return; }

        // заголовок
        var titles = new StackPanel { Spacing = 2 };
        titles.Children.Add(new TextBlock
        {
            Text = x.N,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 30,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"),
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(x.E))
            titles.Children.Add(new TextBlock { Text = x.E, FontStyle = FontStyle.Italic, FontSize = 13, Foreground = Pal.B("#9c8b78") });

        var headRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        headRow.Children.Add(new Border { Width = 3, CornerRadius = new CornerRadius(2), Background = Pal.B("#d99b4a"), Margin = new Thickness(0, 3, 12, 3) });
        Grid.SetColumn(titles, 1);
        headRow.Children.Add(titles);

        var copyBtn = new Button
        {
            Content = "Копировать",
            Classes = { "chip" },
            Padding = new Thickness(12, 6),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Top,
        };
        copyBtn.Click += async (_, _) =>
        {
            var clip = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clip != null) await clip.SetTextAsync(x.N + "\n\n" + x.H);
        };
        Grid.SetColumn(copyBtn, 2);
        headRow.Children.Add(copyBtn);
        DetailHost.Children.Add(headRow);

        // бейджи: редакция, категория, источник
        var meta = new WrapPanel { Orientation = Orientation.Horizontal };
        meta.Children.Add(Badge(_edition, "#d99b4a"));
        meta.Children.Add(Spacer());
        meta.Children.Add(Badge(Cats[_cat].Title, "#b7a794"));
        if (!string.IsNullOrWhiteSpace(x.Cr)) { meta.Children.Add(Spacer()); meta.Children.Add(Badge("ОП " + x.Cr, "#c98a6a")); }
        if (!string.IsNullOrWhiteSpace(x.Rar)) { meta.Children.Add(Spacer()); meta.Children.Add(Badge(x.Rar, "#b58fc9")); }
        foreach (var prm in x.P.Where(p => p.StartsWith("Источник", StringComparison.OrdinalIgnoreCase)))
        {
            meta.Children.Add(Spacer());
            meta.Children.Add(new TextBlock { Text = prm, FontSize = 11.5, Foreground = Pal.B("#8d7a66"), VerticalAlignment = VerticalAlignment.Center });
        }
        DetailHost.Children.Add(meta);

        // вкладки
        var tabs = new List<(string Key, string Title)>();
        tabs.Add(("main", "Описание"));
        if (x.Subs is { Count: > 0 }) tabs.Add(("subs", $"Подклассы ({x.Subs.Count})"));
        if (x.Tb is { Count: > 0 }) tabs.Add(("table", "Таблица"));

        if (tabs.Count > 1)
        {
            var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 2) };
            foreach (var (key, title) in tabs)
            {
                var b = new Button { Content = title, Classes = { "tab" } };
                b.Classes.Set("on", _tab == key);
                b.Click += (_, _) => { _tab = key; RenderDetail(); };
                tabRow.Children.Add(b);
            }
            DetailHost.Children.Add(new Border
            {
                BorderBrush = Pal.B("#2a211a"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 0, 0, 6),
                Child = tabRow,
            });
        }

        if (_tab == "table" && x.Tb is { Count: > 0 }) { DetailHost.Children.Add(DataTable(x.Tb)); return; }
        if (_tab == "subs" && x.Subs is { Count: > 0 }) { RenderSubclasses(x); return; }

        // параметры таблицей
        var prms = x.P.Where(p => !p.StartsWith("Источник", StringComparison.OrdinalIgnoreCase)).ToList();
        if (prms.Count > 0) DetailHost.Children.Add(ParamTable(prms));

        // текст статьи по абзацам
        if (!string.IsNullOrWhiteSpace(x.H))
            foreach (var paragraph in SplitParagraphs(x.H))
                DetailHost.Children.Add(ParagraphBlock(paragraph));

        // остальные данные — показать таблицу сразу, если она есть и текст пустой
        if (string.IsNullOrWhiteSpace(x.H) && x.Tb is { Count: > 0 })
            DetailHost.Children.Add(DataTable(x.Tb));

        if (_cat == 6)
        {
            var btn = new Button
            {
                Content = "→ на карту",
                Background = Pal.B("#4a3728"),
                Foreground = Pal.B("#f0c27b"),
                BorderBrush = Pal.B("#4a3728"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 9),
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var n = x.N; var cr = x.Cr; var mt = x.Mt;
            btn.Click += (_, _) => AddToMap?.Invoke(n, cr, mt);
            DetailHost.Children.Add(btn);
        }
    }

    /* ==================== класс: подклассы, умения, персонаж ==================== */

    private void RenderClass(LibEntry x)
    {
        // заголовок + кнопки
        var titles = new StackPanel { Spacing = 2 };
        titles.Children.Add(new TextBlock
        {
            Text = x.N,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 30,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"),
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(x.E))
            titles.Children.Add(new TextBlock { Text = x.E, FontStyle = FontStyle.Italic, FontSize = 13, Foreground = Pal.B("#9c8b78") });

        var headRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        headRow.Children.Add(new Border { Width = 3, CornerRadius = new CornerRadius(2), Background = Pal.B("#d99b4a"), Margin = new Thickness(0, 3, 12, 3) });
        Grid.SetColumn(titles, 1);
        headRow.Children.Add(titles);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Top };
        var createBtn = new Button
        {
            Content = "Создать персонажа",
            Background = Pal.B("#cf9245"),
            Foreground = Pal.B("#241505"),
            BorderBrush = Pal.B("#e0b072"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(14, 8),
            FontWeight = FontWeight.SemiBold,
            FontSize = 12.5,
        };
        var newSub = _sub != null && x.Subs != null && _sub < x.Subs.Count ? x.Subs[_sub.Value].N : "";
        var klassName = x.N;
        createBtn.Click += (_, _) => CreateHero?.Invoke(klassName, newSub, ClassInfo.HitDie(x, _edition));
        actions.Children.Add(createBtn);

        var copyBtn = new Button { Content = "Копировать", Classes = { "chip" }, Padding = new Thickness(12, 6), FontSize = 12 };
        copyBtn.Click += async (_, _) =>
        {
            var clip = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clip != null) await clip.SetTextAsync(x.N + Environment.NewLine + Environment.NewLine + x.H);
        };
        actions.Children.Add(copyBtn);
        Grid.SetColumn(actions, 2);
        headRow.Children.Add(actions);
        DetailHost.Children.Add(headRow);

        var meta = new WrapPanel { Orientation = Orientation.Horizontal };
        meta.Children.Add(Badge(_edition, "#d99b4a"));
        meta.Children.Add(Spacer());
        meta.Children.Add(Badge("Классы", "#b7a794"));
        if (x.Subs is { Count: > 0 }) { meta.Children.Add(Spacer()); meta.Children.Add(Badge(x.Subs.Count + " подклассов", "#b58fc9")); }
        DetailHost.Children.Add(meta);

        // инфо-таблица класса
        var basics = ClassInfo.Basics(x, _edition);
        if (basics.Count > 0)
            DetailHost.Children.Add(ParamTable(basics.Select(b => b.Label + ": " + b.Value).ToList()));

        // выбор подкласса сверху
        var subs = x.Subs ?? new List<LibSub>();
        if (subs.Count > 0)
        {
            DetailHost.Children.Add(new TextBlock
            {
                Text = "ПОДКЛАСС",
                FontSize = 9.5,
                LetterSpacing = 0.8,
                Foreground = Pal.B("#8d7a66"),
                Margin = new Thickness(0, 8, 0, 0),
            });
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            wrap.Children.Add(SubChip("Не выбран", _sub == null, () => { _sub = null; RenderDetail(); }));
            for (int i = 0; i < subs.Count; i++)
            {
                int idx = i;
                wrap.Children.Add(SubChip(subs[i].N, _sub == idx, () => { _sub = idx; RenderDetail(); }));
            }
            DetailHost.Children.Add(wrap);
        }

        // вкладки
        var tabs = new List<(string Key, string Title)>
        {
            ("features", "Умения"),
            ("desc", "Описание"),
        };
        if (x.Tb is { Count: > 0 }) tabs.Add(("table", "Таблица"));

        var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 2) };
        foreach (var (key, title) in tabs)
        {
            var b = new Button { Content = title, Classes = { "tab" } };
            b.Classes.Set("on", _tab == key || (_tab == "main" && key == "features"));
            b.Click += (_, _) => { _tab = key; RenderDetail(); };
            tabRow.Children.Add(b);
        }
        DetailHost.Children.Add(new Border
        {
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 0, 0, 6),
            Child = tabRow,
        });

        if (_tab == "table" && x.Tb is { Count: > 0 }) { DetailHost.Children.Add(DataTable(x.Tb)); return; }
        if (_tab == "desc")
        {
            foreach (var paragraph in SplitParagraphs(x.H))
                DetailHost.Children.Add(ParagraphBlock(paragraph));
            return;
        }

        // УМЕНИЯ: общие из таблицы прогрессии
        var classFeats = ClassInfo.ClassFeatures(x);
        if (classFeats.Count > 0)
        {
            DetailHost.Children.Add(SectionTitle("Умения класса"));
            foreach (var (level, title) in classFeats)
                DetailHost.Children.Add(FeatureRow(level, title, ""));
        }

        // умения подкласса
        DetailHost.Children.Add(SectionTitle("Умения подкласса"));
        if (_sub == null)
        {
            DetailHost.Children.Add(new Border
            {
                Background = Pal.B("#15110e"),
                BorderBrush = Pal.B("#352a21"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12),
                Child = new TextBlock
                {
                    Text = "Выберите подкласс выше — здесь появятся его умения по уровням. Без подкласса персонаж играется только с базовыми умениями класса.",
                    FontSize = 13,
                    Foreground = Pal.B("#9c8b78"),
                    TextWrapping = TextWrapping.Wrap,
                },
            });
            return;
        }

        var sub = subs[_sub.Value];
        var feats = ClassInfo.SubclassFeatures(sub.H);
        if (feats.Count == 0)
        {
            foreach (var paragraph in SplitParagraphs(sub.H))
                DetailHost.Children.Add(ParagraphBlock(paragraph));
            return;
        }

        foreach (var (level, title, body) in feats)
            DetailHost.Children.Add(FeatureRow(level, title, body));
    }

    private static Control SectionTitle(string text) => new TextBlock
    {
        Text = text,
        FontFamily = new FontFamily("Georgia"),
        FontSize = 18,
        FontWeight = FontWeight.Bold,
        Foreground = Pal.B("#f0c27b"),
        Margin = new Thickness(0, 14, 0, 2),
    };

    private static Control FeatureRow(int level, string title, string body)
    {
        var sp = new StackPanel { Spacing = 3 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (level > 0)
            head.Children.Add(new Border
            {
                Background = Pal.B("#2a211a"),
                BorderBrush = Pal.B("#4a3728"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2),
                Child = new TextBlock { Text = level + " ур.", FontSize = 10.5, Foreground = Pal.B("#d99b4a") },
            });
        head.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Pal.B("#f6e7cf"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        sp.Children.Add(head);
        if (!string.IsNullOrWhiteSpace(body))
            sp.Children.Add(new TextBlock
            {
                Text = body,
                FontSize = 13,
                LineHeight = 20,
                Foreground = Pal.B("#c9b8a4"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });

        return new Border
        {
            BorderBrush = Pal.B("#241d18"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 8),
            Child = sp,
        };
    }

    private void RenderSubclasses(LibEntry x)
    {
        var subs = x.Subs ?? new List<LibSub>();
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 4) };
        var none = SubChip("Без подкласса", _sub == null, () => { _sub = null; RenderDetail(); });
        wrap.Children.Add(none);
        for (int i = 0; i < subs.Count; i++)
        {
            int idx = i;
            wrap.Children.Add(SubChip(subs[i].N, _sub == idx, () => { _sub = idx; RenderDetail(); }));
        }
        DetailHost.Children.Add(wrap);

        if (_sub == null)
        {
            DetailHost.Children.Add(new TextBlock
            {
                Text = "Выберите подкласс — покажу его умения и описание. Без выбора класс играется только с базовыми умениями.",
                FontSize = 13,
                Foreground = Pal.B("#8d7a66"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
            });
            return;
        }

        var sub = subs[_sub.Value];
        DetailHost.Children.Add(new TextBlock
        {
            Text = sub.N,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f0c27b"),
            Margin = new Thickness(0, 10, 0, 4),
        });
        foreach (var paragraph in SplitParagraphs(sub.H))
            DetailHost.Children.Add(ParagraphBlock(paragraph));
    }

    private static Button SubChip(string text, bool on, Action click)
    {
        var b = new Button { Content = text, Classes = { "subchip" } };
        b.Classes.Set("on", on);
        b.Click += (_, _) => click();
        return b;
    }

    private static Border Spacer() => new() { Width = 8 };

    /// <summary>Таблица параметров (ключ — значение) в виде аккуратной таблицы.</summary>
    private static Control ParamTable(List<string> lines)
    {
        var rows = new StackPanel();
        bool alt = false;
        foreach (var line in lines)
        {
            int i = line.IndexOf(':');
            string k = i > 0 ? line[..i].Trim() : "";
            string v = i > 0 ? line[(i + 1)..].Trim() : line;

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(k.Length > 0 ? "190,*" : "*") };
            if (k.Length > 0)
            {
                grid.Children.Add(new Border
                {
                    BorderBrush = Pal.B("#2a211a"),
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    Padding = new Thickness(12, 9),
                    Child = new TextBlock { Text = k, Foreground = Pal.B("#9c8b78"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap },
                });
                var val = new TextBlock { Text = v, Margin = new Thickness(12, 9), FontSize = 13.5, TextWrapping = TextWrapping.Wrap, Foreground = Pal.B("#efe3d2") };
                Grid.SetColumn(val, 1);
                grid.Children.Add(val);
            }
            else
            {
                grid.Children.Add(new TextBlock { Text = v, Margin = new Thickness(12, 9), FontSize = 13.5, TextWrapping = TextWrapping.Wrap, Foreground = Pal.B("#c9b8a4") });
            }

            rows.Children.Add(new Border
            {
                Background = alt ? Pal.B("#15110e") : Brushes.Transparent,
                BorderBrush = Pal.B("#2a211a"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = grid,
            });
            alt = !alt;
        }

        return new Border
        {
            Background = Pal.B("#1c1714"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 8, 0, 0),
            ClipToBounds = true,
            Child = rows,
        };
    }

    /// <summary>Таблица из данных dnd.su: шапка (иногда двухуровневая) + строки, прокрутка по горизонтали.</summary>
    private static Control DataTable(List<List<string>> tb)
    {
        int cols = 0;
        foreach (var r in tb) cols = Math.Max(cols, r.Count);

        // убираем колонки, пустые во всех строках
        var keep = new List<int>();
        for (int c = 0; c < cols; c++)
            if (tb.Any(r => c < r.Count && !string.IsNullOrWhiteSpace(r[c]))) keep.Add(c);
        if (keep.Count == 0) return new TextBlock { Text = "Таблица пуста", Foreground = Pal.B("#8d7a66") };

        // шапка: одна строка, иногда две (вторая — подписи уровней: «1 2 3 …»)
        int headerRows = 1;
        if (tb.Count > 2)
        {
            var second = tb[1];
            bool shortCells = second.Where(c => !string.IsNullOrWhiteSpace(c)).All(c => c.Trim().Length <= 4);
            if (shortCells) headerRows = 2;
        }

        var grid = new Grid();
        foreach (var _ in keep) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        // объединённая шапка
        for (int ci = 0; ci < keep.Count; ci++)
        {
            int c = keep[ci];
            var parts = new List<string>();
            for (int hr = 0; hr < headerRows && hr < tb.Count; hr++)
                if (c < tb[hr].Count && !string.IsNullOrWhiteSpace(tb[hr][c])) parts.Add(tb[hr][c].Trim());
            var head = new Border
            {
                Background = Pal.B("#2a211a"),
                BorderBrush = Pal.B("#2a211a"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(10, 6),
                Child = new TextBlock
                {
                    Text = string.Join("\n", parts),
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    Foreground = Pal.B("#f0c27b"),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 240,
                },
            };
            Grid.SetRow(head, 0);
            Grid.SetColumn(head, ci);
            grid.Children.Add(head);
        }

        int rowIndex = 1;
        for (int r = headerRows; r < tb.Count; r++)
        {
            var row = tb[r];
            for (int ci = 0; ci < keep.Count; ci++)
            {
                int c = keep[ci];
                string text = c < row.Count ? row[c] : "";
                var cell = new Border
                {
                    Background = rowIndex % 2 == 0 ? Pal.B("#15110e") : Brushes.Transparent,
                    BorderBrush = Pal.B("#2a211a"),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(10, 6),
                    MinWidth = 52,
                    Child = new TextBlock
                    {
                        Text = text,
                        FontSize = 12.5,
                        Foreground = Pal.B("#efe3d2"),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 240,
                    },
                };
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, ci);
                grid.Children.Add(cell);
            }
            rowIndex++;
        }

        for (int i = 0; i < rowIndex; i++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        return new Border
        {
            Background = Pal.B("#1c1714"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(1),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = grid,
            },
        };
    }

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        var parts = text.Replace("\r\n", "\n").Split('\n');
        var buffer = "";
        foreach (var raw in parts)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (buffer.Length > 0) { yield return buffer.Trim(); buffer = ""; }
                continue;
            }
            buffer += (buffer.Length > 0 ? " " : "") + line.Trim();
        }
        if (buffer.Length > 0) yield return buffer.Trim();
    }

    private static Control ParagraphBlock(string text)
    {
        bool bullet = text.StartsWith("•", StringComparison.Ordinal) || text.StartsWith("-", StringComparison.Ordinal);
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 23,
            Foreground = Pal.B(bullet ? "#c9b8a4" : "#e9dccb"),
            Margin = new Thickness(bullet ? 16 : 0, 3, 0, 3),
        };
    }
}
