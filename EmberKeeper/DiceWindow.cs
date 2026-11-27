using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Бросок кубиков: любой тип, количество, преимущество/помеха для d20.</summary>
public sealed class DiceWindow : Window
{
    private static readonly int[] DiceTypes = { 2, 3, 4, 6, 8, 10, 12, 20, 100 };

    private readonly Random _rng = new();
    private int _sides = 20;
    private int _mode; // 0 обычный, 1 преимущество, 2 помеха

    private readonly WrapPanel _diceRow = new() { Orientation = Orientation.Horizontal };
    private readonly NumericUpDown _countBox;
    private readonly StackPanel _modeRow = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly TextBlock _sumText;
    private readonly TextBlock _detailText;
    private readonly WrapPanel _rolls = new() { Orientation = Orientation.Horizontal };
    private readonly List<Button> _diceButtons = new();
    private readonly List<Button> _modeButtons = new();
    private readonly StackPanel _history = new() { Spacing = 2 };
    private readonly List<string> _log = new();

    private static Color Bg => Pal.C("#100d0b");
    private static Color Panel => Pal.C("#1c1714");
    private static Color Panel2 => Pal.C("#221b16");
    private static Color Line => Pal.C("#352a21");
    private static Color Gold => Pal.C("#f0c27b");
    private static Color Ink => Pal.C("#efe3d2");
    private static Color Muted => Pal.C("#9c8b78");

    public DiceWindow()
    {
        Title = "Кости";
        Width = 560;
        Height = 620;
        Background = new SolidColorBrush(Bg);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(22), Spacing = 14 };

        root.Children.Add(new TextBlock
        {
            Text = "Кости",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Pal.C("#f6e7cf")),
        });

        // выбор типа кубика
        foreach (var s in DiceTypes)
        {
            int sides = s;
            var b = MakeButton($"d{s}", false);
            b.MinWidth = 54;
            b.Margin = new Thickness(0, 0, 6, 6);
            b.Click += (_, _) => { _sides = sides; RefreshDiceStyles(); };
            _diceButtons.Add(b);
            _diceRow.Children.Add(b);
        }
        root.Children.Add(new TextBlock { Text = "Тип кубика", FontSize = 10.5, Foreground = new SolidColorBrush(Muted) });
        root.Children.Add(_diceRow);

        // количество
        _countBox = new NumericUpDown
        {
            Minimum = 1, Maximum = 50, Value = 1, Width = 90,
            Background = new SolidColorBrush(Panel2), Foreground = new SolidColorBrush(Ink),
            BorderBrush = new SolidColorBrush(Line),
        };
        var countRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        countRow.Children.Add(new TextBlock { Text = "Количество", FontSize = 12, Foreground = new SolidColorBrush(Muted), VerticalAlignment = VerticalAlignment.Center });
        countRow.Children.Add(_countBox);
        var plus = MakeButton("＋", false); plus.Width = 36;
        plus.Click += (_, _) => { _countBox.Value = Math.Min(50m, (_countBox.Value ?? 1m) + 1m); };
        var minus = MakeButton("－", false); minus.Width = 36;
        minus.Click += (_, _) => { _countBox.Value = Math.Max(1m, (_countBox.Value ?? 1m) - 1m); };
        countRow.Children.Add(minus);
        countRow.Children.Add(plus);
        root.Children.Add(countRow);

        // режим d20
        var modeLabels = new[] { "обычный", "с преимуществом", "с помехой" };
        for (int i = 0; i < modeLabels.Length; i++)
        {
            int m = i;
            var b = MakeButton(modeLabels[i], false);
            b.FontSize = 11.5;
            b.Margin = new Thickness(0, 0, 6, 0);
            b.Click += (_, _) => { _mode = m; RefreshModeStyles(); };
            _modeButtons.Add(b);
            _modeRow.Children.Add(b);
        }
        _modeRow.IsVisible = true;
        root.Children.Add(new TextBlock { Text = "d20 — режим броска", FontSize = 10.5, Foreground = new SolidColorBrush(Muted) });
        root.Children.Add(_modeRow);

        // кнопка броска
        var rollBtn = new Button
        {
            Content = "Бросить",
            Height = 46,
            Background = new SolidColorBrush(Pal.C("#cf9245")),
            Foreground = new SolidColorBrush(Pal.C("#241505")),
            BorderBrush = new SolidColorBrush(Pal.C("#e0b072")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        rollBtn.Click += (_, _) => Roll();
        root.Children.Add(rollBtn);

        // результат
        var resultPanel = new Border
        {
            Background = new SolidColorBrush(Panel),
            BorderBrush = new SolidColorBrush(Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            MinHeight = 190,
        };
        var resultStack = new StackPanel { Spacing = 8 };
        _sumText = new TextBlock
        {
            Text = "—",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 54,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Gold),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _detailText = new TextBlock
        {
            Text = "нажмите «Бросить»",
            FontSize = 12,
            Foreground = new SolidColorBrush(Muted),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        resultStack.Children.Add(_detailText);
        resultStack.Children.Add(_sumText);
        resultStack.Children.Add(_rolls);
        resultPanel.Child = resultStack;
        root.Children.Add(resultPanel);

        // своя формула
        root.Children.Add(new TextBlock { Text = "Своя формула (NdN[+M])", FontSize = 10.5, Foreground = new SolidColorBrush(Muted) });
        var formulaRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var formulaBox = new TextBox
        {
            Text = "2d6+3",
            Width = 160,
            Background = new SolidColorBrush(Panel2),
            Foreground = new SolidColorBrush(Ink),
            BorderBrush = new SolidColorBrush(Line),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6),
        };
        var formulaBtn = MakeButton("Бросить формулу", false);
        formulaBtn.Click += (_, _) => RollFormula(formulaBox.Text ?? "");
        formulaRow.Children.Add(formulaBox);
        formulaRow.Children.Add(formulaBtn);
        root.Children.Add(formulaRow);

        // история
        root.Children.Add(new TextBlock { Text = "История бросков", FontSize = 10.5, Foreground = new SolidColorBrush(Muted) });
        var historyBorder = new Border
        {
            Background = new SolidColorBrush(Panel),
            BorderBrush = new SolidColorBrush(Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8),
            Child = new ScrollViewer { Content = _history, MaxHeight = 160, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
        };
        root.Children.Add(historyBorder);

        Content = new ScrollViewer { Content = root };

        RefreshDiceStyles();
        RefreshModeStyles();
    }

    private Button MakeButton(string text, bool active)
    {
        var b = new Button
        {
            Content = text,
            Background = new SolidColorBrush(active ? Pal.C("#4a3728") : Panel2),
            Foreground = new SolidColorBrush(active ? Gold : Pal.C("#b7a794")),
            BorderBrush = new SolidColorBrush(active ? Pal.C("#8C7030") : Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(11, 6),
            FontSize = 12.5,
        };
        return b;
    }

    private void RefreshDiceStyles()
    {
        for (int i = 0; i < _diceButtons.Count; i++)
        {
            bool on = DiceTypes[i] == _sides;
            _diceButtons[i].Background = new SolidColorBrush(on ? Pal.C("#4a3728") : Panel2);
            _diceButtons[i].Foreground = new SolidColorBrush(on ? Gold : Pal.C("#b7a794"));
            _diceButtons[i].BorderBrush = new SolidColorBrush(on ? Pal.C("#8C7030") : Line);
            _diceButtons[i].FontWeight = on ? FontWeight.Bold : FontWeight.Normal;
        }
        _modeRow.IsVisible = _sides == 20;
        if (_sides != 20) _mode = 0;
        RefreshModeStyles();
    }

    private void RefreshModeStyles()
    {
        for (int i = 0; i < _modeButtons.Count; i++)
        {
            bool on = i == _mode;
            _modeButtons[i].Background = new SolidColorBrush(on ? Pal.C("#4a3728") : Panel2);
            _modeButtons[i].Foreground = new SolidColorBrush(on ? Gold : Pal.C("#b7a794"));
            _modeButtons[i].BorderBrush = new SolidColorBrush(on ? Pal.C("#8C7030") : Line);
        }
    }

    private void AddHistory(string entry)
    {
        _log.Insert(0, entry);
        if (_log.Count > 20) _log.RemoveAt(_log.Count - 1);
        _history.Children.Clear();
        foreach (var line in _log)
            _history.Children.Add(new TextBlock { Text = line, FontSize = 11.5, Foreground = new SolidColorBrush(Muted) });
    }

    private void RollFormula(string formula)
    {
        var m = System.Text.RegularExpressions.Regex.Match(formula.Trim(), @"^(\d*)d(\d+)([+-]\d+)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            _detailText.Text = "Формат: NdN[+M], например 2d6+3";
            return;
        }

        int count = m.Groups[1].Value.Length == 0 ? 1 : int.Parse(m.Groups[1].Value);
        int sides = int.Parse(m.Groups[2].Value);
        int mod = m.Groups[3].Value.Length == 0 ? 0 : int.Parse(m.Groups[3].Value);
        if (count < 1) count = 1;
        if (count > 50) count = 50;
        if (sides < 2) sides = 2;

        var rolls = new List<int>();
        for (int i = 0; i < count; i++) rolls.Add(_rng.Next(1, sides + 1));
        int sum = rolls.Sum() + mod;

        _sumText.Text = sum.ToString();
        _detailText.Text = formula + (mod != 0 ? "" : "");
        _rolls.Children.Clear();
        foreach (var v in rolls)
            _rolls.Children.Add(new Border
            {
                Background = new SolidColorBrush(Panel2),
                BorderBrush = new SolidColorBrush(Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 6, 6),
                Child = new TextBlock { Text = v.ToString(), FontSize = 14, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Ink) },
            });
        AddHistory($"{formula}: [{string.Join(", ", rolls)}]" + (mod != 0 ? $" {(mod > 0 ? "+" : "")}{mod}" : "") + $" = {sum}");
    }

    private void Roll()
    {
        int count = (int)Math.Clamp((double)_countBox.Value, 1, 50);
        var rolls = new List<(int value, int a, int b)>();

        for (int i = 0; i < count; i++)
        {
            if (_sides == 20 && _mode != 0)
            {
                int a = _rng.Next(1, 21);
                int b = _rng.Next(1, 21);
                int chosen = _mode == 1 ? Math.Max(a, b) : Math.Min(a, b);
                rolls.Add((chosen, a, b));
            }
            else
            {
                int v = _rng.Next(1, _sides + 1);
                rolls.Add((v, v, 0));
            }
        }

        int sum = rolls.Sum(r => r.value);
        _sumText.Text = sum.ToString();
        _detailText.Text = count == 1
            ? $"d{_sides}"
            : $"{count}d{_sides} · сумма {sum}";

        AddHistory($"{count}d{_sides}{( _mode == 1 ? " (преим.)" : _mode == 2 ? " (помеха)" : "")}: [{string.Join(", ", rolls.Select(r => r.value))}] = {sum}");
        _rolls.Children.Clear();
        foreach (var (value, a, b) in rolls)
        {
            bool crit = _sides == 20 && value == 20;
            bool miss = _sides == 20 && value == 1;
            var chip = new Border
            {
                Background = new SolidColorBrush(crit ? Pal.C("#4a3728") : Panel2),
                BorderBrush = new SolidColorBrush(crit ? Gold : (miss ? Pal.C("#7c2a2c") : Line)),
                BorderThickness = new Thickness(crit ? 2 : 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 6, 6),
                Child = new TextBlock
                {
                    Text = (b != 0 && _mode == 1 ? $"{value}▲" : (b != 0 && _mode == 2 ? $"{value}▼" : value.ToString())),
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(crit ? Gold : Ink),
                },
            };
            _rolls.Children.Add(chip);
        }
    }
}
