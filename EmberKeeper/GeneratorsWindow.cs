using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Окно генераторов: связи, характер НПС, стычки, добыча, таверны, тайны и прочее.</summary>
public sealed class GeneratorsWindow : Window
{
    private readonly Action<EncounterSpec>? _placeEncounter;
    private readonly Action<string, string, string, string>? _addRelation;

    private readonly ListBox _list = new();
    private readonly StackPanel _result = new() { Spacing = 10 };
    private readonly TextBlock _title = new();
    private readonly TextBlock _hint = new();
    private readonly Button _toMap;
    private readonly Button _toRel;

    private GenResult? _cur;
    private int _index;

    private sealed class GenItem
    {
        public string Name { get; init; } = "";
        public string Hint { get; init; } = "";
        public Func<GenResult> Gen { get; init; } = RandomTables.Bonds;
        public override string ToString() => Name;
    }

    private static readonly (string Name, string Hint, Func<GenResult> Gen)[] Gens =
    {
        ("Связи персонажей", "семья · война · тайна", RandomTables.Bonds),
        ("Характер НПС", "внешность · мотив · тайна", RandomTables.NpcPersonality),
        ("Случайная стычка", "сразу на карту", RandomTables.Encounter),
        ("Колода вещей", "добыча и безделушки", RandomTables.Loot),
        ("Колода судьбы", "карты с последствиями", RandomTables.FateDeck),
        ("Таверна", "хозяин · слух · тайна", RandomTables.Tavern),
        ("Слух", "правда или ложь", RandomTables.Rumor),
        ("Зацепка", "с чего начать", RandomTables.Hook),
        ("Имена", "люди · дварфы · эльфы", RandomTables.Names),
        ("Поселение", "проблема и приметы", RandomTables.Settlement),
        ("Тайна и улика", "расследование", RandomTables.Mystery),
        ("Проклятие", "магия и цена", RandomTables.Curse),
        ("Погода", "атмосфера сцены", RandomTables.Weather),
        ("Сундук", "ловушка и добыча", RandomTables.Chest),
        ("Что за неделю", "заставка сессии", RandomTables.Downtime),
    };

    public GeneratorsWindow(Action<EncounterSpec>? placeEncounter = null,
                            Action<string, string, string, string>? addRelation = null)
    {
        _placeEncounter = placeEncounter;
        _addRelation = addRelation;

        Title = "Генераторы EmberKeeper";
        Width = 1040;
        Height = 760;
        MinWidth = 900;
        MinHeight = 600;
        Background = Pal.B("#100d0b");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _list.Background = Brushes.Transparent;
        _list.BorderThickness = new Thickness(0);
        _list.ItemTemplate = new FuncDataTemplate<GenItem>((g, _) =>
        {
            var sp = new StackPanel { Margin = new Thickness(10, 7), Spacing = 1 };
            sp.Children.Add(new TextBlock { Text = g.Name, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Pal.B("#f6e7cf") });
            sp.Children.Add(new TextBlock { Text = g.Hint, FontSize = 10.5, Foreground = Pal.B("#9c8b78") });
            return sp;
        }, true);
        _list.ItemsSource = Gens.Select(g => new GenItem { Name = g.Name, Hint = g.Hint, Gen = g.Gen }).ToList();
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedIndex >= 0 && _list.SelectedIndex < Gens.Length)
            {
                _index = _list.SelectedIndex;
                Generate();
            }
        };

        var left = new Border
        {
            Background = Pal.B("#1c1714"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children =
                {
                    new TextBlock
                    {
                        Text = "ГЕНЕРАТОРЫ",
                        FontSize = 10,
                        LetterSpacing = 1.4,
                        Foreground = Pal.B("#9c8b78"),
                        Margin = new Thickness(14, 14, 14, 8),
                    },
                },
            },
        };
        var leftGrid = (Grid)left.Child!;
        var listScroll = new ScrollViewer
        {
            Content = _list,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 0, 0, 10),
        };
        Grid.SetRow(listScroll, 1);
        leftGrid.Children.Add(listScroll);

        _title.FontFamily = new FontFamily("Georgia");
        _title.FontSize = 24;
        _title.FontWeight = FontWeight.Bold;
        _title.Foreground = Pal.B("#f6e7cf");
        _title.TextWrapping = TextWrapping.Wrap;

        _hint.FontSize = 11.5;
        _hint.Foreground = Pal.B("#9c8b78");
        _hint.Margin = new Thickness(0, 2, 0, 0);
        _hint.TextWrapping = TextWrapping.Wrap;

        var head = new StackPanel { Spacing = 2, Children = { _title, _hint } };

        var resultScroll = new ScrollViewer
        {
            Content = _result,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 10, 0, 8),
        };

        _toMap = ActionBtn("→ На карту", PlaceOnMap, true);
        _toMap.IsVisible = false;
        _toRel = ActionBtn("→ В Связи", PlaceInRelations);
        _toRel.IsVisible = false;

        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(ActionBtn("Сгенерировать", Generate, true));
        buttons.Children.Add(ActionBtn("×3", () => GenerateMany(3)));
        buttons.Children.Add(ActionBtn("Копировать", Copy));
        buttons.Children.Add(_toMap);
        buttons.Children.Add(_toRel);

        var right = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        right.Children.Add(head);
        Grid.SetRow(resultScroll, 1);
        right.Children.Add(resultScroll);
        Grid.SetRow(buttons, 2);
        right.Children.Add(buttons);

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("270,*"), Margin = new Thickness(18) };
        root.Children.Add(left);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        Content = root;

        _list.SelectedIndex = 0;
        Opened += (_, _) => Generate();
    }

    /// <summary>Выбрать набор по индексу (служебные проверки).</summary>
    public void SelectIndex(int i)
    {
        if (i >= 0 && i < Gens.Length) _list.SelectedIndex = i;
    }

    /* ---------------- генерация ---------------- */

    private void Generate()
    {
        try
        {
            _cur = Gens[_index].Gen();
            Show(new List<GenResult> { _cur });
        }
        catch (Exception ex)
        {
            _title.Text = "Ошибка генератора";
            _hint.Text = ex.Message;
            _result.Children.Clear();
        }
    }

    private void GenerateMany(int count)
    {
        var list = new List<GenResult>();
        for (int i = 0; i < count; i++)
        {
            var r = Gens[_index].Gen();
            list.Add(r);
            if (i == 0) _cur = r;
        }
        Show(list);
    }

    private void Show(List<GenResult> results)
    {
        _result.Children.Clear();
        var first = results[0];
        _title.Text = results.Count > 1 ? first.Title + " ×" + results.Count : first.Title;
        _hint.Text = first.Caption;

        _toMap.IsVisible = _cur?.Encounter != null;
        _toRel.IsVisible = _cur?.BondA != null;

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var card = new Border
            {
                Background = Pal.B("#1c1714"),
                BorderBrush = Pal.B("#2a211a"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 14),
            };

            var sp = new StackPanel { Spacing = 8 };

            if (results.Count > 1)
                sp.Children.Add(new TextBlock { Text = (i + 1).ToString(), FontSize = 10, Foreground = Pal.B("#8d7a66") });

            sp.Children.Add(new TextBlock
            {
                Text = r.Main,
                FontFamily = new FontFamily("Georgia"),
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Pal.B("#f0c27b"),
                TextWrapping = TextWrapping.Wrap,
            });

            if (results.Count > 1 && !string.IsNullOrWhiteSpace(r.Caption))
                sp.Children.Add(new TextBlock { Text = r.Caption, FontSize = 12, Foreground = Pal.B("#9c8b78"), TextWrapping = TextWrapping.Wrap });

            foreach (var row in r.Rows)
            {
                var line = new StackPanel { Spacing = 1 };
                line.Children.Add(new TextBlock
                {
                    Text = row.Label.ToUpperInvariant(),
                    FontSize = 9.5,
                    LetterSpacing = 0.6,
                    Foreground = Pal.B("#8d7a66"),
                });
                line.Children.Add(new TextBlock
                {
                    Text = row.Value,
                    FontSize = 13,
                    Foreground = Pal.B("#efe3d2"),
                    TextWrapping = TextWrapping.Wrap,
                });
                sp.Children.Add(line);
            }

            card.Child = sp;
            _result.Children.Add(card);
        }
    }

    /* ---------------- действия ---------------- */

    private void Copy()
    {
        if (_cur == null) return;
        var text = _cur.Title + Environment.NewLine + _cur.Main + Environment.NewLine;
        foreach (var r in _cur.Rows) text += r.Label + ": " + r.Value + Environment.NewLine;
        var clip = Clipboard;
        if (clip != null) _ = clip.SetTextAsync(text);
    }

    private void PlaceOnMap()
    {
        if (_cur?.Encounter == null || _placeEncounter == null) return;
        _placeEncounter(_cur.Encounter);
        Close();
    }

    private void PlaceInRelations()
    {
        if (_cur?.BondA == null || _cur.BondB == null || _addRelation == null) return;
        _addRelation(_cur.BondA, _cur.BondB, _cur.BondLabel ?? "связь", _cur.BondNote ?? "");
        Close();
    }

    private static Button ActionBtn(string text, Action onClick, bool accent = false)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(14, 8),
            Margin = new Thickness(0, 0, 8, 0),
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
