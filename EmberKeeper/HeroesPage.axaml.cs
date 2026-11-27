using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public partial class HeroesPage : UserControl
{
    private List<Hero> _heroes = new();
    private List<Hero> _view = new();
    private string _q = "";
    public Action<Hero>? ToMap;
    public Action<Hero>? ToNpc;

    private readonly Dictionary<string, ComboBox> _filters = new();

    private static readonly (string Key, string Label)[] FilterDefs =
    {
        ("faction", "Фракция"),
        ("city", "Город"),
        ("org", "Организация"),
        ("campaign", "Кампания"),
        ("class", "Класс"),
        ("tags", "Теги"),
    };

    public HeroesPage()
    {
        InitializeComponent();
        _heroes = HeroStore.Load();
        BuildFilters();
        Refresh();
    }

    private void BuildFilters()
    {
        foreach (var (key, label) in FilterDefs)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 6, 6) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Pal.B("#9c8b78"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            var cb = new ComboBox { Width = 130, Background = Pal.B("#15110e"), Foreground = Pal.B("#efe3d2"), BorderBrush = Pal.B("#352a21"), CornerRadius = new CornerRadius(8), FontSize = 12 };
            cb.SelectionChanged += (_, _) => ApplyFilter();
            _filters[key] = cb;
            sp.Children.Add(cb);
            FilterHost.Children.Add(sp);
        }
    }

    private void RefreshFilterOptions()
    {
        foreach (var (key, _) in FilterDefs)
        {
            var cb = _filters[key];
            var current = cb.SelectedItem as string ?? "все";
            var values = new List<string> { "все" };
            foreach (var h in _heroes)
            {
                var vals = key switch
                {
                    "faction" => new[] { h.Faction },
                    "city" => new[] { h.City },
                    "org" => new[] { h.Organization },
                    "campaign" => new[] { h.Campaign },
                    "class" => new[] { h.Class },
                    "tags" => h.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    _ => Array.Empty<string>(),
                };
                foreach (var v in vals)
                {
                    var t = v.Trim();
                    if (t.Length > 0 && !values.Contains(t)) values.Add(t);
                }
            }
            cb.ItemsSource = values;
            cb.SelectedItem = values.Contains(current) ? current : "все";
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<Hero> src = _heroes;

        if (!string.IsNullOrWhiteSpace(_q))
        {
            var q = _q.Trim().ToLowerInvariant();
            src = src.Where(h => $"{h.Name} {h.Class} {h.Race} {h.Subclass} {h.Tags} {h.Faction} {h.City} {h.Organization} {h.Campaign} {h.PlayerName}".ToLowerInvariant().Contains(q));
        }

        foreach (var (key, _) in FilterDefs)
        {
            var sel = _filters[key].SelectedItem as string;
            if (string.IsNullOrEmpty(sel) || sel == "все") continue;
            src = key switch
            {
                "faction" => src.Where(h => h.Faction == sel),
                "city" => src.Where(h => h.City == sel),
                "org" => src.Where(h => h.Organization == sel),
                "campaign" => src.Where(h => h.Campaign == sel),
                "class" => src.Where(h => h.Class == sel),
                "tags" => src.Where(h => h.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(sel)),
                _ => src,
            };
        }

        _view = src.OrderBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        HeroList.ItemsSource = _view;
        if (EmptyHint != null) EmptyHint.IsVisible = _view.Count == 0;
        HeroList.ItemTemplate = new FuncDataTemplate<Hero>((h, _) => Row(h), true);
        CountText.Text = $"{_view.Count} из {_heroes.Count}";
        if (_view.Count > 0 && HeroList.SelectedIndex < 0) HeroList.SelectedIndex = 0;
    }

    private Control Row(Hero h)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(10, 8) };

        var portrait = new Border
        {
            Width = 40, Height = 40,
            CornerRadius = new CornerRadius(8),
            Background = Pal.B("#221b16"),
            BorderBrush = Pal.B("#4a3728"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(h.Name) ? "?" : h.Name.Substring(0, 1).ToUpperInvariant(),
                FontFamily = new FontFamily("Georgia"),
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Pal.B("#f0c27b"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        grid.Children.Add(portrait);

        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = h.Name, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Pal.B("#f6e7cf") });
        var sub = $"{h.Class} {h.Level} · {h.Hp}/{h.MaxHp} ХП · КД {h.Ac}";
        info.Children.Add(new TextBlock { Text = sub, FontSize = 11.5, Foreground = Pal.B("#9c8b78") });
        var tags = string.Join(" · ", new[] { h.Faction, h.City, h.Organization, h.Campaign }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(tags))
            info.Children.Add(new TextBlock { Text = tags, FontSize = 10.5, Foreground = Pal.B("#83725f") });

        Grid.SetColumn(info, 1);
        grid.Children.Add(info);
        return grid;
    }

    private void Refresh()
    {
        RefreshFilterOptions();
        ApplyFilter();
    }

    private Hero? Cur => HeroList.SelectedItem as Hero;

    public void OpenByName(string name)
    {
        var h = _heroes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (h == null) { if (_heroes.Count == 0) return; h = _heroes[0]; }
        HeroList.SelectedItem = h;
        OpenSheet(h);
    }

    public void AddFromNpc(Npc n)
    {
        var h = _heroes.FirstOrDefault(x => string.Equals(x.Name, n.Name, StringComparison.OrdinalIgnoreCase)) ?? new Hero { Name = n.Name };
        if (!_heroes.Contains(h)) _heroes.Add(h);
        if (string.IsNullOrEmpty(h.Class)) h.Class = n.Class;
        if (string.IsNullOrEmpty(h.Race)) h.Race = n.Race;
        if (h.MaxHp == 0 || h.MaxHp == 10 && n.Hp != 10) { h.Hp = n.Hp; h.MaxHp = n.Hp; }
        if (h.Ac == 10 && n.Ac != 10) h.Ac = n.Ac;
        if (!string.IsNullOrEmpty(n.Notes)) h.Notes = (h.Notes + "\n" + n.Notes).Trim();
        if (!string.IsNullOrEmpty(n.Tags)) h.Tags = n.Tags;
        if (string.IsNullOrEmpty(h.Faction)) h.Faction = n.Faction;
        if (string.IsNullOrEmpty(h.City)) h.City = n.City;
        if (string.IsNullOrEmpty(h.Organization)) h.Organization = n.Organization;
        if (string.IsNullOrEmpty(h.Campaign)) h.Campaign = n.Campaign;
        HeroStore.Save(_heroes);
        Refresh();
        HeroList.SelectedItem = h;
    }

    /// <summary>Создать персонажа из библиотеки: класс и подкласс уже заполнены.</summary>
    public void CreateFromClass(string klass, string subclass, int hitDie)
    {
        var h = new Hero
        {
            Name = "Новый " + (string.IsNullOrWhiteSpace(klass) ? "герой" : klass.ToLowerInvariant()),
            Class = klass,
            Subclass = subclass,
            Level = "1",
            Hp = Math.Max(1, hitDie),
            MaxHp = Math.Max(1, hitDie),
            HitDice = "1к" + hitDie,
            HitDiceCurrent = 1,
            HitDiceMax = 1,
        };
        _heroes.Add(h);
        HeroStore.Save(_heroes);
        Refresh();
        OpenSheet(h);
    }

    private void OpenSheet(Hero h)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var sheet = new HeroSheet(h, () => { HeroStore.Save(_heroes); Refresh(); }, hero =>
        {
            HeroStore.Save(_heroes);
            ToMap?.Invoke(hero);
        });
        if (owner != null) sheet.ShowDialog(owner); else sheet.Show();
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        var h = new Hero { Name = "Новый герой" };
        _heroes.Add(h);
        HeroStore.Save(_heroes);
        Refresh();
        HeroList.SelectedItem = h;
        OpenSheet(h);
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (Cur is Hero h) OpenSheet(h);
    }

    private void OnToMap(object? sender, RoutedEventArgs e)
    {
        if (Cur is Hero h) ToMap?.Invoke(h);
    }

    private void OnToNpc(object? sender, RoutedEventArgs e)
    {
        if (Cur is Hero h) ToNpc?.Invoke(h);
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (Cur is Hero h) { _heroes.Remove(h); HeroStore.Save(_heroes); Refresh(); }
    }

    private void OnSelect(object? sender, SelectionChangedEventArgs e) { }

    private void OnSearch(object? sender, TextChangedEventArgs e)
    {
        _q = SearchBox.Text ?? "";
        ApplyFilter();
    }

    private void OnResetFilters(object? sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        _q = "";
        foreach (var (_, cb) in _filters.Select(kv => (kv.Key, kv.Value)))
            if (cb.ItemCount > 0) cb.SelectedIndex = 0;
        ApplyFilter();
    }

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var path = await FilePickerDialog.Pick(owner, "Импорт персонажа из JSON", "*.json");
        if (string.IsNullOrEmpty(path)) return;

        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception ex) { Notify(owner, "Не удалось прочитать файл:\n" + ex.Message); return; }

        var imported = HeroImport.FromJsonAny(text);
        if (imported.Count == 0) { Notify(owner, "Не удалось распознать персонажа в файле.\nПоддерживаются: свой JSON, Long Story Short, JSON с полями персонажа."); return; }

        foreach (var h in imported)
        {
            h.Id = Guid.NewGuid().ToString("N");
            _heroes.Add(h);
        }
        HeroStore.Save(_heroes);
        Refresh();
        HeroList.SelectedIndex = _heroes.Count - 1;
        Notify(owner, imported.Count == 1 ? $"Импортирован: {imported[0].Name}" : $"Импортировано персонажей: {imported.Count}");
    }

    private void Notify(Window? owner, string text)
    {
        var win = new Window
        {
            Title = "Импорт",
            Width = 420, Height = 190,
            Background = Pal.B("#100d0b"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18, 18, 18, 8), Foreground = Pal.B("#efe3d2") };
        var ok = new Button { Content = "ОК", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 18, 14), Classes = { "goldbtn" } };
        ok.Click += (_, _) => win.Close();
        var sp = new StackPanel();
        sp.Children.Add(tb);
        sp.Children.Add(ok);
        win.Content = sp;
        if (owner != null) win.ShowDialog(owner); else win.Show();
    }
}
