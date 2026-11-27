using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public sealed class SearchHit
{
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Sub { get; init; } = "";
    public string Query { get; init; } = "";
    public string Cat { get; init; } = "";
    public string Edition { get; init; } = "";
}

/// <summary>Глобальный поиск по героям, НПС, сценам, токенам и библиотеке.</summary>
public sealed class SearchWindow : Window
{
    private readonly TextBox _query;
    private readonly ListBox _results = new();
    private readonly TextBlock _status;
    private List<SearchHit> _hits = new();

    public SearchHit? Selected { get; private set; }

    private static readonly string[] LibCats = { "spells", "items", "feats", "classes", "races", "backgrounds" };

    public SearchWindow()
    {
        Title = "Поиск по программе";
        Width = 720;
        Height = 620;
        Background = Pal.B("#100d0b");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(18) };

        _query = new TextBox
        {
            Watermark = "Имя героя, НПС, токена, сцены или заклинания…",
            Background = Pal.B("#1c1714"),
            Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9),
            FontSize = 14,
        };
        _query.TextChanged += (_, _) => Run();
        _query.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Accept();
            if (e.Key == Key.Down && _results.ItemCount > 0)
            {
                _results.SelectedIndex = Math.Min(_results.ItemCount - 1, _results.SelectedIndex + 1);
                e.Handled = true;
            }
        };
        root.Children.Add(_query);

        _status = new TextBlock { FontSize = 11.5, Foreground = Pal.B("#9c8b78"), Margin = new Thickness(2, 8, 0, 8) };
        Grid.SetRow(_status, 1);
        root.Children.Add(_status);

        _results.Background = Pal.B("#1c1714");
        _results.BorderThickness = new Thickness(0);
        _results.ItemTemplate = new FuncDataTemplate<SearchHit>((h, _) =>
        {
            var sp = new StackPanel { Margin = new Thickness(10, 7), Spacing = 1 };
            sp.Children.Add(new TextBlock { Text = Icon(h.Kind) + "  " + h.Title, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Pal.B("#f6e7cf") });
            if (!string.IsNullOrWhiteSpace(h.Sub))
                sp.Children.Add(new TextBlock { Text = h.Sub, FontSize = 10.5, Foreground = Pal.B("#9c8b78") });
            return sp;
        }, true);
        _results.DoubleTapped += (_, _) => Accept();

        var listBorder = new Border
        {
            Background = Pal.B("#1c1714"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = _results,
        };
        Grid.SetRow(listBorder, 2);
        root.Children.Add(listBorder);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        var open = new Button { Content = "Открыть", Padding = new Thickness(16, 8), CornerRadius = new CornerRadius(9), Background = Pal.B("#cf9245"), Foreground = Pal.B("#241505"), BorderBrush = Pal.B("#e0b072"), BorderThickness = new Thickness(1), FontWeight = FontWeight.Bold };
        open.Click += (_, _) => Accept();
        var close = new Button { Content = "Закрыть", Padding = new Thickness(16, 8), CornerRadius = new CornerRadius(9) };
        close.Click += (_, _) => Close();
        footer.Children.Add(close);
        footer.Children.Add(open);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;
        Opened += (_, _) => _query.Focus();
        Run();
    }

    private static string Icon(string kind) => kind switch
    {
        "hero" => "🛡",
        "npc" => "☗",
        "token" => "♜",
        "scene" => "◈",
        "spell" => "✴",
        "item" => "◈",
        "feat" => "✧",
        "class" => "⚔",
        "race" => "✦",
        _ => "⌕",
    };

    private void Run()
    {
        var q = (_query.Text ?? "").Trim();
        _hits = new List<SearchHit>();

        if (q.Length < 2)
        {
            _results.ItemsSource = null;
            _status.Text = "Введите минимум 2 символа";
            return;
        }

        var lower = q.ToLowerInvariant();

        void Add(string kind, string title, string sub, string query, string cat = "", string edition = "")
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            if (!title.ToLowerInvariant().Contains(lower) && !(sub ?? "").ToLowerInvariant().Contains(lower)) return;
            _hits.Add(new SearchHit { Kind = kind, Title = title, Sub = sub, Query = query, Cat = cat, Edition = edition });
        }

        // герои
        foreach (var h in HeroStore.Load())
            Add("hero", h.Name, $"{h.Class} {h.Level} · {h.Faction} {h.City}".Trim(), h.Name);

        // НПС
        try
        {
            foreach (var n in NpcStore.Load())
                Add("npc", n.Name, $"{n.Class} · {n.City} {n.Faction}".Trim(), n.Name);
        }
        catch { }

        // сцены
        try
        {
            foreach (var s in SceneStore.Load())
                Add("scene", s.Name, s.Subtitle, s.Name);
        }
        catch { }

        // токены на картах
        try
        {
            var book = MapStore.Load();
            foreach (var m in book.Maps)
                foreach (var t in m.Tokens)
                    Add("token", t.Name, $"{m.Name} · {t.Hp}/{t.MaxHp} ХП", t.Name);
        }
        catch { }

        // библиотека (только названия, чтобы было быстро)
        foreach (var edition in new[] { "2014", "2024" })
        {
            foreach (var cat in LibCats)
            {
                try
                {
                    foreach (var e in DataStore.Load(edition, cat))
                    {
                        if (e.N.ToLowerInvariant().Contains(lower) || (e.E ?? "").ToLowerInvariant().Contains(lower))
                            _hits.Add(new SearchHit
                            {
                                Kind = cat.TrimEnd('s') switch { "spell" => "spell", "item" => "item", "feat" => "feat", "class" => "class", "race" => "race", _ => "npc" },
                                Title = e.N,
                                Sub = edition + " · " + e.Sub,
                                Query = e.N,
                                Cat = cat,
                                Edition = edition,
                            });
                    }
                }
                catch { }
            }
        }

        var list = _hits.Take(80).ToList();
        _results.ItemsSource = list;
        if (list.Count > 0) _results.SelectedIndex = 0;
        _status.Text = _hits.Count == 0
            ? "Ничего не найдено"
            : $"Найдено: {_hits.Count}" + (_hits.Count > 80 ? " (показаны первые 80)" : "");
    }

    private void Accept()
    {
        Selected = _results.SelectedItem as SearchHit ?? _hits.FirstOrDefault();
        if (Selected != null) Close();
    }
}
