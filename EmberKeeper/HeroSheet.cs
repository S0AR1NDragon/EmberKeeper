using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace EmberKeeper;

/// <summary>Лист персонажа в стиле веб-версии: вкладки Основное / Бой и магия / Заметки.</summary>
public sealed class HeroSheet : Window
{
    private readonly Hero _h;
    private readonly Action _save;
    private readonly Action<Hero>? _toMap;

    private static Color Bg => Pal.C("#100d0b");
    private static Color Panel => Pal.C("#1c1714");
    private static Color Panel2 => Pal.C("#221b16");
    private static Color Line => Pal.C("#352a21");
    private static Color Gold => Pal.C("#f0c27b");
    private static Color GoldDim => Pal.C("#d99b4a");
    private static Color Ink => Pal.C("#efe3d2");
    private static Color Muted => Pal.C("#9c8b78");

    private readonly List<TextBlock> _abilMods = new();
    private readonly List<TextBlock> _saveMods = new();
    private readonly List<TextBlock> _skillMods = new();
    private readonly List<CheckBox> _saveChecks = new();
    private readonly List<CheckBox> _skillChecks = new();
    private readonly List<NumericUpDown> _abilInputs = new();

    private readonly StackPanel _mainPane = new() { Spacing = 14 };
    private readonly StackPanel _combatPane = new() { Spacing = 14, IsVisible = false };
    private readonly StackPanel _notesPane = new() { Spacing = 14, IsVisible = false };
    private readonly List<Button> _tabButtons = new();

    public HeroSheet(Hero h, Action save, Action<Hero>? toMap = null)
    {
        _h = h;
        _save = save;
        _toMap = toMap;

        HeroStore.Normalize(h);

        Title = "Лист персонажа — " + h.Name;
        Width = 980;
        Height = 900;
        Background = new SolidColorBrush(Bg);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        BuildMain();
        BuildCombat();
        BuildNotes();

        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 0, 12) };
        var names = new[] { "Основное", "Бой и магия", "Заметки" };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var b = TabButton(names[i], i == 0);
            b.Click += (_, _) => SwitchTab(idx);
            _tabButtons.Add(b);
            tabs.Children.Add(b);
        }

        var content = new StackPanel { Spacing = 0 };
        content.Children.Add(tabs);
        content.Children.Add(_mainPane);
        content.Children.Add(_combatPane);
        content.Children.Add(_notesPane);

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        var scroll = new ScrollViewer { Content = content, Margin = new Thickness(22, 18, 22, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        root.Children.Add(scroll);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(22, 12, 22, 18) };
        var saveBtn = Accent("Сохранить и закрыть");
        saveBtn.Click += (_, _) => { RefreshMods(); _save(); Close(); };
        footer.Children.Add(saveBtn);

        if (_toMap != null)
        {
            var mapBtn = Btn("На карту");
            mapBtn.Click += (_, _) => { _save(); _toMap(_h); };
            footer.Children.Add(mapBtn);
        }

        var close = Btn("Закрыть");
        close.Click += (_, _) => Close();
        footer.Children.Add(close);

        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        Content = root;

        RefreshMods();
    }

    private void SwitchTab(int idx)
    {
        _mainPane.IsVisible = idx == 0;
        _combatPane.IsVisible = idx == 1;
        _notesPane.IsVisible = idx == 2;
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            bool on = i == idx;
            _tabButtons[i].Background = (on ? Pal.B("#4a3728") : Brushes.Transparent);
            _tabButtons[i].Foreground = new SolidColorBrush(on ? Gold : Muted);
            _tabButtons[i].BorderBrush = (on ? Pal.B("#8C7030") : Brushes.Transparent);
        }
    }

    /* ---------------- Основное ---------------- */

    private void BuildMain()
    {
        // шапка с портретом
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 0, 0, 4) };

        var portrait = new Border
        {
            Width = 76, Height = 76,
            CornerRadius = new CornerRadius(38),
            Background = new SolidColorBrush(Panel2),
            BorderBrush = new SolidColorBrush(Pal.C("#4a3728")),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 16, 0),
        };
        var portraitInner = new Grid();
        portraitInner.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(_h.Name) ? "?" : _h.Name.Substring(0, 1).ToUpperInvariant(),
            FontFamily = new FontFamily("Georgia"),
            FontSize = 32,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Gold),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        TryLoadPortrait(portraitInner, _h.Portrait);
        portrait.Child = portraitInner;
        head.Children.Add(portrait);

        var id = new StackPanel { Spacing = 6 };
        var nameRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var nameBox = Field(_h.Name, "Имя");
        nameBox.FontSize = 22;
        nameBox.FontFamily = new FontFamily("Georgia");
        nameBox.FontWeight = FontWeight.Bold;
        nameBox.TextChanged += (_, _) => { _h.Name = nameBox.Text ?? ""; Title = "Лист персонажа — " + _h.Name; Save(); };
        nameRow.Children.Add(nameBox);
        var lvl = Num(_h.LevelNum, 1, 20, 64);
        lvl.ValueChanged += (_, _) => { _h.Level = ((int)(lvl.Value ?? 1)).ToString(); Save(); RefreshMods(); };
        var lvlBox = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(8, 0, 0, 0) };
        lvlBox.Children.Add(new TextBlock { Text = "ур.", Foreground = new SolidColorBrush(Muted), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        lvlBox.Children.Add(lvl);
        Grid.SetColumn(lvlBox, 1);
        nameRow.Children.Add(lvlBox);
        id.Children.Add(nameRow);

        var row1 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,80") };
        var race = Field(_h.Race, "вид"); race.TextChanged += (_, _) => { _h.Race = race.Text ?? ""; Save(); };
        var cls = Field(_h.Class, "класс"); cls.TextChanged += (_, _) => { _h.Class = cls.Text ?? ""; Save(); };
        var sub = Field(_h.Subclass, "подкласс"); sub.TextChanged += (_, _) => { _h.Subclass = sub.Text ?? ""; Save(); };
        row1.Children.Add(race);
        Grid.SetColumn(cls, 1); cls.Margin = new Thickness(6, 0); row1.Children.Add(cls);
        Grid.SetColumn(sub, 2); row1.Children.Add(sub);
        id.Children.Add(row1);

        var row2 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var bg = Field(_h.Background, "предыстория"); bg.TextChanged += (_, _) => { _h.Background = bg.Text ?? ""; Save(); };
        var player = Field(_h.PlayerName, "игрок"); player.TextChanged += (_, _) => { _h.PlayerName = player.Text ?? ""; Save(); };
        row2.Children.Add(bg);
        Grid.SetColumn(player, 1); player.Margin = new Thickness(6, 0); row2.Children.Add(player);
        id.Children.Add(row2);

        // статы
        var stats = new UniformGrid { Columns = 5, Margin = new Thickness(0, 6, 0, 0) };
        stats.Children.Add(StatBox("КД", _h.Ac, v => { _h.Ac = v; Save(); }));
        stats.Children.Add(StatBox("Хиты", _h.Hp, v => { _h.Hp = v; if (_h.MaxHp < v) _h.MaxHp = v; Save(); }));
        stats.Children.Add(StatBox("Макс.", _h.MaxHp, v => { _h.MaxHp = v; Save(); }));
        stats.Children.Add(StatBox("Иниц.", _h.Init, v => { _h.Init = v; Save(); }));
        stats.Children.Add(StatBox("Пам.", _h.Pp, v => { _h.Pp = v; Save(); }));
        id.Children.Add(stats);

        Grid.SetColumn(id, 1);
        head.Children.Add(id);
        _mainPane.Children.Add(Card(head));

        // характеристики
        var abil = new UniformGrid { Columns = 6 };
        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            var cell = new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 6, 0) };
            cell.Children.Add(new TextBlock { Text = Hero.AbilNames[i].ToUpperInvariant(), FontSize = 9.5, Foreground = new SolidColorBrush(Muted), HorizontalAlignment = HorizontalAlignment.Center });
            var n = Num(_h.Ability(Hero.AbilKeys[i]), 1, 30, 70);
            n.HorizontalAlignment = HorizontalAlignment.Center;
            n.ValueChanged += (_, _) =>
            {
                int v = (int)(n.Value ?? 0);
                switch (idx)
                {
                    case 0: _h.Str = v; break;
                    case 1: _h.Dex = v; break;
                    case 2: _h.Con = v; break;
                    case 3: _h.Int = v; break;
                    case 4: _h.Wis = v; break;
                    default: _h.Cha = v; break;
                }
                Save(); RefreshMods();
            };
            _abilInputs.Add(n);
            cell.Children.Add(n);
            var mod = new TextBlock { Text = Fmt(_h.Mod(_h.Ability(Hero.AbilKeys[i]))), FontFamily = new FontFamily("Georgia"), FontSize = 16, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Gold), HorizontalAlignment = HorizontalAlignment.Center };
            _abilMods.Add(mod);
            cell.Children.Add(mod);
            abil.Children.Add(cell);
        }
        _mainPane.Children.Add(Card(abil));

        // спасброски + навыки
        var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };

        var saves = new StackPanel { Spacing = 4 };
        saves.Children.Add(Header("Спасброски"));
        for (int i = 0; i < 6; i++)
        {
            string key = Hero.AbilKeys[i];
            var rowG = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 1) };
            var cb = new CheckBox { IsChecked = _h.HasSave(key), Margin = new Thickness(0, 0, 8, 0), MinWidth = 0 };
            cb.Click += (_, _) => { _h.ToggleSave(key, cb.IsChecked == true); Save(); RefreshMods(); };
            _saveChecks.Add(cb);
            rowG.Children.Add(cb);
            var lbl = new TextBlock { Text = Hero.AbilNames[i], FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 1);
            rowG.Children.Add(lbl);
            var mod = new TextBlock { Text = Fmt(_h.SaveMod(key)), FontSize = 13, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Gold), VerticalAlignment = VerticalAlignment.Center };
            _saveMods.Add(mod);
            Grid.SetColumn(mod, 2);
            rowG.Children.Add(mod);
            saves.Children.Add(rowG);
        }
        cols.Children.Add(Card(saves));

        var skills = new StackPanel { Spacing = 2 };
        skills.Children.Add(Header("Навыки"));
        foreach (var s in Hero.SkillsList)
        {
            string key = s.Key;
            var rowG = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0) };
            var cb = new CheckBox { IsChecked = _h.HasSkill(key), Margin = new Thickness(0, 0, 8, 0), MinWidth = 0 };
            cb.Click += (_, _) => { _h.ToggleSkill(key, cb.IsChecked == true); Save(); RefreshMods(); };
            _skillChecks.Add(cb);
            rowG.Children.Add(cb);
            var lbl = new TextBlock { Text = $"{s.Name}  ({Short(s.Abil)})", FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 1);
            rowG.Children.Add(lbl);
            var mod = new TextBlock { Text = Fmt(_h.SkillMod(key)), FontSize = 13, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Gold), VerticalAlignment = VerticalAlignment.Center };
            _skillMods.Add(mod);
            Grid.SetColumn(mod, 2);
            rowG.Children.Add(mod);
            skills.Children.Add(rowG);
        }
        var skCard = Card(skills);
        Grid.SetColumn(skCard, 1);
        skCard.Margin = new Thickness(14, 0, 0, 0);
        cols.Children.Add(skCard);

        _mainPane.Children.Add(cols);

        // снаряжение и умения
        var textCols = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        textCols.Children.Add(Card(AreaBlock("Снаряжение", _h.Equipment, 6, v => { _h.Equipment = v; Save(); })));
        var features = Card(AreaBlock("Умения и особенности", _h.Features, 6, v => { _h.Features = v; Save(); }));
        Grid.SetColumn(features, 1);
        features.Margin = new Thickness(14, 0, 0, 0);
        textCols.Children.Add(features);
        _mainPane.Children.Add(textCols);
    }

    /* ---------------- Бой и магия ---------------- */

    private void BuildCombat()
    {
        var stats = new UniformGrid { Columns = 4 };
        stats.Children.Add(StatBox("Скорость", _h.Speed, v => { _h.Speed = v; Save(); }));
        stats.Children.Add(StatBox("Тёмное зрение", _h.Darkvision, v => { _h.Darkvision = v; Save(); }));
        stats.Children.Add(StatBox("Сл закл.", _h.SpellDc, v => { _h.SpellDc = v; Save(); }));
        stats.Children.Add(StatBox("Атака закл.", _h.SpellAtk, v => { _h.SpellAtk = v; Save(); }));
        _combatPane.Children.Add(Card(stats));

        var hd = new UniformGrid { Columns = 3 };
        hd.Children.Add(StatBox("Костей осталось", _h.HitDiceCurrent, v => { _h.HitDiceCurrent = v; Save(); }));
        hd.Children.Add(StatBox("Костей всего", _h.HitDiceMax, v => { _h.HitDiceMax = v; Save(); }));
        var dieBox = new StackPanel { Spacing = 3 };
        dieBox.Children.Add(new TextBlock { Text = "КОСТЬ", FontSize = 9.5, Foreground = new SolidColorBrush(Muted) });
        var die = Field(_h.HitDice, "d8");
        die.TextChanged += (_, _) => { _h.HitDice = die.Text ?? ""; Save(); };
        dieBox.Children.Add(die);
        hd.Children.Add(dieBox);
        _combatPane.Children.Add(Card(hd));

        var slots = new UniformGrid { Columns = 9 };
        for (int i = 1; i <= 9; i++)
        {
            int idx = i - 1;
            var cell = new StackPanel { Spacing = 2 };
            cell.Children.Add(new TextBlock { Text = i.ToString(), FontSize = 11, Foreground = new SolidColorBrush(Muted), HorizontalAlignment = HorizontalAlignment.Center });
            var n = Num(_h.Slots[idx], 0, 20, 52);
            n.HorizontalAlignment = HorizontalAlignment.Center;
            n.ValueChanged += (_, _) => { _h.Slots[idx] = (int)(n.Value ?? 0); Save(); };
            cell.Children.Add(n);
            slots.Children.Add(cell);
        }
        var slotBlock = new StackPanel { Spacing = 6 };
        slotBlock.Children.Add(Header("Ячейки заклинаний"));
        slotBlock.Children.Add(slots);
        _combatPane.Children.Add(Card(slotBlock));

        _combatPane.Children.Add(Card(AreaBlock("Заклинания", _h.Spells, 12, v => { _h.Spells = v; Save(); })));
        _combatPane.Children.Add(Card(AreaBlock("Атаки и боевые заговоры", _h.Attacks, 6, v => { _h.Attacks = v; Save(); })));
    }

    /* ---------------- Заметки ---------------- */

    private void BuildNotes()
    {
        var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var left = new StackPanel { Spacing = 14 };
        left.Children.Add(Card(AreaBlock("Черты", _h.Feats, 5, v => { _h.Feats = v; Save(); })));
        left.Children.Add(Card(AreaBlock("Союзники и организации", _h.Allies, 4, v => { _h.Allies = v; Save(); })));
        cols.Children.Add(left);

        var right = new StackPanel { Spacing = 14 };
        right.Children.Add(Card(AreaBlock("Цели и задачи", _h.Goals, 4, v => { _h.Goals = v; Save(); })));
        right.Children.Add(Card(AreaBlock("Заметки", _h.Notes, 6, v => { _h.Notes = v; Save(); })));
        var rc = Card(right);
        Grid.SetColumn(rc, 1);
        rc.Margin = new Thickness(14, 0, 0, 0);
        cols.Children.Add(rc);
        _notesPane.Children.Add(cols);

        // фильтры
        var filters = new StackPanel { Spacing = 8 };
        filters.Children.Add(Header("Фильтры (фракции, города, кампании)"));
        var f1 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        f1.Children.Add(FilterField("Фракция", _h.Faction, v => { _h.Faction = v; Save(); }));
        var city = FilterField("Город", _h.City, v => { _h.City = v; Save(); });
        Grid.SetColumn(city, 1); city.Margin = new Thickness(8, 0, 0, 0);
        f1.Children.Add(city);
        filters.Children.Add(f1);

        var f2 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        f2.Children.Add(FilterField("Организация", _h.Organization, v => { _h.Organization = v; Save(); }));
        var camp = FilterField("Кампания", _h.Campaign, v => { _h.Campaign = v; Save(); });
        Grid.SetColumn(camp, 1); camp.Margin = new Thickness(8, 0, 0, 0);
        f2.Children.Add(camp);
        filters.Children.Add(f2);

        filters.Children.Add(FilterField("Теги (через запятую)", _h.Tags, v => { _h.Tags = v; Save(); }));
        _notesPane.Children.Add(Card(filters));
    }

    private Control FilterField(string label, string value, Action<string> onChange)
    {
        var sp = new StackPanel { Spacing = 3 };
        sp.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), FontSize = 9.5, Foreground = new SolidColorBrush(Muted) });
        var box = Field(value, label);
        box.TextChanged += (_, _) => onChange(box.Text ?? "");
        sp.Children.Add(box);
        return sp;
    }

    /* ---------------- обновление модификаторов ---------------- */

    private void RefreshMods()
    {
        for (int i = 0; i < 6; i++)
            _abilMods[i].Text = Fmt(_h.Mod(_h.Ability(Hero.AbilKeys[i])));

        for (int i = 0; i < 6; i++)
            _saveMods[i].Text = Fmt(_h.SaveMod(Hero.AbilKeys[i])) + $"  (БМ {_h.Proficiency})";

        for (int i = 0; i < Hero.SkillsList.Length && i < _skillMods.Count; i++)
            _skillMods[i].Text = Fmt(_h.SkillMod(Hero.SkillsList[i].Key));
    }

    private void Save() => _save();

    /* ---------------- хелперы UI ---------------- */

    private static string Fmt(int v) => (v >= 0 ? "+" : "") + v;
    private static string Short(string abil) => abil switch
    {
        "str" => "Сил", "dex" => "Лов", "con" => "Тел", "int" => "Инт", "wis" => "Мдр", _ => "Хар"
    };

    private static void TryLoadPortrait(Grid host, string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            host.Children.Add(new Image { Source = new Bitmap(path), Stretch = Stretch.UniformToFill });
        }
        catch { }
    }

    private static TextBlock Header(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 10,
        LetterSpacing = 1.4,
        Foreground = new SolidColorBrush(Muted),
        Margin = new Thickness(0, 0, 0, 6),
    };

    private static Border Card(Control content) => new()
    {
        Background = new SolidColorBrush(Panel),
        BorderBrush = new SolidColorBrush(Line),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(14),
        Child = content,
    };

    private Control StatBox(string label, int value, Action<int> onChange)
    {
        var sp = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 8, 0) };
        sp.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), FontSize = 9.5, Foreground = new SolidColorBrush(Muted) });
        var n = Num(value, -999, 9999, 88);
        n.ValueChanged += (_, _) => onChange((int)(n.Value ?? 0));
        sp.Children.Add(n);
        return sp;
    }

    private static TextBox Field(string value, string watermark) => new()
    {
        Text = value,
        Watermark = watermark,
        Background = new SolidColorBrush(Panel2),
        Foreground = new SolidColorBrush(Ink),
        BorderBrush = new SolidColorBrush(Line),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10, 6),
        FontSize = 12.5,
    };

    private static NumericUpDown Num(int value, decimal min, decimal max, double width) => new()
    {
        Value = Math.Clamp(value, (int)min, (int)max),
        Minimum = min,
        Maximum = max,
        Width = width,
        Background = new SolidColorBrush(Panel2),
        Foreground = new SolidColorBrush(Ink),
        BorderBrush = new SolidColorBrush(Line),
        FontSize = 13,
    };

    private static Control AreaBlock(string label, string value, int rows, Action<string> onChange)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(Header(label));
        var box = new TextBox
        {
            Text = value,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = Math.Max(70, rows * 20),
            Background = new SolidColorBrush(Panel2),
            Foreground = new SolidColorBrush(Ink),
            BorderBrush = new SolidColorBrush(Line),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8),
            FontSize = 12.5,
        };
        box.TextChanged += (_, _) => onChange(box.Text ?? "");
        sp.Children.Add(box);
        return sp;
    }

    private static Button TabButton(string text, bool on) => new()
    {
        Content = text,
        Background = (on ? Pal.B("#4a3728") : Brushes.Transparent),
        Foreground = new SolidColorBrush(on ? Gold : Muted),
        BorderBrush = (on ? Pal.B("#8C7030") : Brushes.Transparent),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(14, 7),
        FontSize = 12.5,
    };

    private static Button Btn(string text) => new()
    {
        Content = text,
        Background = new SolidColorBrush(Panel2),
        Foreground = new SolidColorBrush(Ink),
        BorderBrush = new SolidColorBrush(Line),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(9),
        Padding = new Thickness(14, 8),
        FontSize = 12.5,
    };

    private static Button Accent(string text) => new()
    {
        Content = text,
        Background = new SolidColorBrush(Pal.C("#cf9245")),
        Foreground = new SolidColorBrush(Pal.C("#241505")),
        BorderBrush = new SolidColorBrush(Pal.C("#e0b072")),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(9),
        Padding = new Thickness(16, 8),
        FontSize = 13,
        FontWeight = FontWeight.Bold,
    };
}
