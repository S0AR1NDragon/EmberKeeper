using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public partial class MainWindow : Window
{
    // индексы разделов (совпадают с порядком Nav)
    private const int IxHome = 0, IxMap = 1, IxInit = 2, IxSlides = 3, IxHeroes = 4, IxLib = 5, IxNpc = 6, IxRel = 7, IxCodex = 8;

    private static readonly (string Icon, string Label, string Hint)[] Nav =
    {
        ("home", "Главная", "Обзор и последняя сессия"),
        ("map", "Карта", "Локации и туман"),
        ("bolt", "Инициатива", "Ход боя"),
        ("slides", "Слайды", "Презентация"),
        ("person", "Герои", "Листы персонажей"),
        ("book", "Библиотека", "Знания мастера"),
        ("users", "НПС", "Персонажи мастера"),
        ("link", "Связи", "Отношения и фракции"),
        ("sparkle", "Справочник", "Состояния"),
    };

    private readonly HomePage _home;
    private readonly LibraryView _lib;
    private readonly MapPage _map;
    private readonly InitPage _init;
    private readonly SlidesPage _slides;
    private readonly HeroesPage _heroes;
    private readonly NpcPage _npcs;
    private readonly RelationsPage _relations;
    private readonly GlossaryPage _glossary;

    private readonly List<Border> _navItems = new();
    private readonly List<TextBlock> _navLabels = new();
    private readonly List<TextBlock> _navHints = new();
    private readonly List<Avalonia.Controls.Shapes.Path> _navIcons = new();

    private int _current = -1;
    private bool _sidebarVisible = true;

    public MainWindow()
    {
        InitializeComponent();

        _home = new HomePage();
        _lib = new LibraryView();
        _map = new MapPage();
        _init = new InitPage();
        _slides = new SlidesPage();
        _heroes = new HeroesPage();
        _npcs = new NpcPage();
        _relations = new RelationsPage();
        _glossary = new GlossaryPage();

        _home.Navigate = ShowTab;
        _home.OnExport = ExportCampaign;
        _home.OnImport = ImportCampaign;
        _home.OnPause = () => { if (Live.Window != null) Live.ShowBlank(); };
        _home.OnUpdates = () => { var w = new UpdateWindow(); _ = w.ShowDialog(this); };
        _home.OnFinish = FinishSession;
        _map.OpenSheet = OpenSheet;
        _heroes.ToMap = h => { _map.AddHero(h); ShowTab(IxMap); };
        _heroes.ToNpc = h => { _npcs.AddFromHero(h); ShowTab(IxNpc); };
        _npcs.ToHero = n => { _heroes.AddFromNpc(n); ShowTab(IxHeroes); };
        _lib.AddToMap = (name, cr, mt) => { _map.AddFromBestiary(name, cr, mt); ShowTab(IxMap); };
        _lib.CreateHero = (klass, sub, hitDie) => { _heroes.CreateFromClass(klass, sub, hitDie); ShowTab(IxHeroes); };

        SetupTopbar();
        Background = Pal.B(Pal.Dark ? "#100d0b" : "#f7f1e6");

        KeyDown += OnHotkey;
        Opened += (_, _) => FitToScreen();
        BuildNav();
        var start = Settings.LoadStartTab();
        ShowTab(start >= 0 && start < Nav.Length ? start : IxHome);
    }

    /// <summary>Окно не должно вылезать за пределы экрана: иначе правая панель скрывается.</summary>
    private void FitToScreen()
    {
        try
        {
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen == null) return;

            double scale = RenderScaling <= 0 ? 1 : RenderScaling;
            double w = screen.WorkingArea.Width / scale;
            double h = screen.WorkingArea.Height / scale;
            if (w < 300 || h < 300) return;

            // если окно 1600x1000 не помещается — разворачиваем на весь экран
            if (w < 1620 || h < 1020)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                Width = Math.Min(1600, w - 24);
                Height = Math.Min(1000, h - 56);
            }
        }
        catch { }
    }

    private void SetupTopbar()
    {
        BrandIcon.Content = Icons.Make("sparkle", 26, "#d99b4a");
        MenuBtn.Content = Icons.Make(_sidebarVisible ? "close" : "menu", 16, "#efe3d2");
        ThemeBtn.Content = Icons.Make(Pal.Dark ? "sun" : "moon", 16, "#efe3d2");
        SearchBtn.Content = Icons.Row("search", "Поиск", 15, "#efe3d2", "#efe3d2");
        ScreenBtn.Content = Icons.Row("screen", "Экран", 15, "#efe3d2", "#efe3d2");
        GenBtn.Content = Icons.Row("sparkles", "Генераторы", 15, "#efe3d2", "#efe3d2");
    }

    private void BuildNav()
    {
        NavHost.Children.Clear();
        _navItems.Clear();
        _navLabels.Clear();
        _navHints.Clear();
        _navIcons.Clear();

        for (int i = 0; i < Nav.Length; i++)
        {
            int idx = i;
            var (icon, label, hint) = Nav[i];

            var iconTb = Icons.Make(icon, 20, "#b98f52");
            var labelTb = new TextBlock { Text = label, FontSize = 13.5, FontWeight = FontWeight.Bold, Foreground = Pal.B("#b7a794"), Margin = new Thickness(2, 0, 0, 0) };
            var hintTb = new TextBlock { Text = hint, FontSize = 10.5, Foreground = Pal.B("#83725f") };

            var copy = new StackPanel { Spacing = 0 };
            copy.Children.Add(labelTb);
            copy.Children.Add(hintTb);

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*") };
            grid.Children.Add(iconTb);
            Grid.SetColumn(copy, 1);
            grid.Children.Add(copy);

            var border = new Border
            {
                Child = grid,
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            border.PointerEntered += (_, _) => { if (!IsActive(idx)) border.Background = Pal.B("#1f1915"); };
            border.PointerExited += (_, _) => { if (!IsActive(idx)) border.Background = Brushes.Transparent; };
            border.PointerPressed += (_, _) => ShowTab(idx);

            _navItems.Add(border);
            _navLabels.Add(labelTb);
            _navHints.Add(hintTb);
            _navIcons.Add(iconTb);
            NavHost.Children.Add(border);
        }
    }

    private bool IsActive(int i) => i == _current;

    private void OpenSheet(string type, string reference)
    {
        switch (type)
        {
            case "mob":
                _lib.OpenBestiary(reference);
                ShowTab(IxLib);
                break;
            case "player":
                _heroes.OpenByName(reference);
                ShowTab(IxHeroes);
                break;
            case "npc":
                ShowTab(IxNpc);
                break;
        }
    }

    private void ShowTab(int idx)
    {
        _current = idx;

        for (int i = 0; i < _navItems.Count; i++)
        {
            bool on = i == idx;
            _navItems[i].Background = on
                ? new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Pal.C("#2d1f14"), 0),
                        new GradientStop(Pal.C("#1a120d"), 1),
                    },
                }
                : Brushes.Transparent;
            _navItems[i].BorderBrush = on ? Pal.B("#4a3728") : Brushes.Transparent;
            _navLabels[i].Foreground = Pal.B(on ? "#f3e6d4" : "#b7a794");
            _navHints[i].Foreground = Pal.B("#83725f");
            Icons.Recolor(_navIcons[i], on ? "#f0c27b" : "#b98f52");
        }

        _sidebarVisible = idx == IxHome;
        Crumbs.Text = Nav[idx].Label.ToUpperInvariant();

        Host.Content = idx switch
        {
            IxHome => (Control)_home,
            IxMap => _map,
            IxInit => _init,
            IxSlides => _slides,
            IxHeroes => _heroes,
            IxLib => _lib,
            IxNpc => _npcs,
            IxRel => _relations,
            IxCodex => _glossary,
            _ => (Control)_lib,
        };

        ApplySidebar();
    }

    private void ApplySidebar()
    {
        Sidebar.IsVisible = _sidebarVisible;
        MenuBtn.Content = Icons.Make(_sidebarVisible ? "close" : "menu", 16, "#efe3d2");
    }

    private void OnToggleSidebar(object? sender, RoutedEventArgs e)
    {
        _sidebarVisible = !_sidebarVisible;
        ApplySidebar();
    }

    private async void FinishSession()
    {
        // сохраняем всё, что открыто, и гасим экран игроков
        MapStore.Save(MapStore.Load());
        Live.ShowBlank();
        var path = Campaign.Export();
        await ShowInfo(path == null
            ? "Сессия завершена."
            : "Сессия завершена. Резервная копия:\n" + path);
    }

    private async void ExportCampaign()
    {
        var path = Campaign.Export();
        await ShowInfo(path == null
            ? "Не удалось сохранить кампанию."
            : "Кампания сохранена:\n" + path);
    }

    private async void ImportCampaign()
    {
        var path = await FilePickerDialog.Pick(this, "Импорт кампании", "*.json");
        if (string.IsNullOrEmpty(path)) return;
        var error = Campaign.Import(path);
        if (error != null)
        {
            await ShowInfo("Не удалось импортировать:\n" + error);
            return;
        }
        var next = new MainWindow { WindowState = WindowState };
        next.Show();
        Close();
        await ShowInfo("Кампания загружена.");
    }

    private async System.Threading.Tasks.Task ShowInfo(string text)
    {
        var win = new Window
        {
            Title = "Кампания",
            Width = 460,
            Height = 200,
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
        await win.ShowDialog(this);
    }

    private void OnToggleTheme(object? sender, RoutedEventArgs e)
    {
        var dark = !Pal.Dark;
        Settings.SaveDark(dark);
        Pal.Set(dark);

        var next = new MainWindow { WindowState = WindowState };
        next.Show();
        Close();
    }

    private void OnHotkey(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) == 0) return;
        if (e.Key == Avalonia.Input.Key.D)
        {
            e.Handled = true;
            OnDice(null, new RoutedEventArgs());
        }
        else if (e.Key == Avalonia.Input.Key.P)
        {
            e.Handled = true;
            _map.ShowPlayerView();
        }
        else if (e.Key == Avalonia.Input.Key.M)
        {
            e.Handled = true;
            OnToggleSidebar(null, new RoutedEventArgs());
        }
        else if (e.Key == Avalonia.Input.Key.F)
        {
            e.Handled = true;
            OnGlobalSearch(null, new RoutedEventArgs());
        }
    }

    private void OnPlayerScreen(object? sender, RoutedEventArgs e) => _map.ShowPlayerView();

    private async void OnGlobalSearch(object? sender, RoutedEventArgs e)
    {
        var win = new SearchWindow();
        await win.ShowDialog(this);
        var hit = win.Selected;
        if (hit == null) return;

        switch (hit.Kind)
        {
            case "hero":
                _heroes.OpenByName(hit.Query);
                ShowTab(IxHeroes);
                break;
            case "npc":
                _npcs.SelectByName(hit.Query);
                ShowTab(IxNpc);
                break;
            case "scene":
                _slides.SelectByName(hit.Query);
                ShowTab(IxSlides);
                break;
            case "token":
                _map.SelectToken(hit.Query);
                ShowTab(IxMap);
                break;
            default:
                _lib.SearchIn(hit.Edition, hit.Cat, hit.Query);
                ShowTab(IxLib);
                break;
        }
    }

    /// <summary>Служебная проверка: сгенерировать стычку и поставить её на карту.</summary>
    public string RunEncounterDemo()
    {
        try
        {
            var enc = RandomTables.Encounter().Encounter;
            if (enc == null) return "нет стычки";
            ShowTab(IxMap);
            _map.PlaceEncounter(enc);
            int total = 0;
            foreach (var g in enc.Groups) total += g.Count;
            return $"ok: {enc.Terrain}, групп {enc.Groups.Count}, противников {total}";
        }
        catch (Exception ex) { return "FAIL: " + ex; }
    }

    private void OnGenerators(object? sender, RoutedEventArgs e)
    {
        var win = new GeneratorsWindow(
            enc => { _map.PlaceEncounter(enc); ShowTab(IxMap); },
            (a, b, label, note) => { _relations.AddPair(a, b, label, note); ShowTab(IxRel); });
        _ = win.ShowDialog(this);
    }

    private void OnDice(object? sender, RoutedEventArgs e)
    {
        var win = new DiceWindow();
        win.ShowDialog(this);
    }
}
