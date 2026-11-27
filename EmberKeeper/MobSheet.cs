using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public sealed class MobSheet : Window
{
    private readonly Token _t;
    private readonly Action _save;
    private readonly TextBox _name, _notes, _speed;
    private readonly NumericUpDown _hp, _maxHp, _ac, _init;

    public MobSheet(Token t, Action save)
    {
        _t = t;
        _save = save;
        Width = 460;
        Height = 620;
        Title = "Лист моба — " + t.Name;
        Background = Pal.B("#1c1714");

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions = RowDefinitions.Parse("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,*");

        var title = new TextBlock
        {
            Text = t.Name,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"),
            Margin = new Thickness(0, 0, 0, 16),
        };
        root.Children.Add(title);
        Grid.SetRow(title, 0);

        _name = Field("Имя", t.Name); _hp = Num("Хиты", t.Hp, 0, 9999);
        _maxHp = Num("Макс. хиты", t.MaxHp, 0, 9999); _ac = Num("КД", t.Ac, 0, 40);
        _init = Num("Инициатива", t.Init, -20, 99); _speed = Field("Скорость", t.Speed);
        _notes = Area("Заметки / способности", t.Notes);

        AddRow(root, 1, new Control[] { Label("Имя"), _name });
        AddRow(root, 2, new Control[] { Label("Хиты"), _hp, Label("Макс."), _maxHp });
        AddRow(root, 3, new Control[] { Label("КД"), _ac, Label("Иниц."), _init });
        AddRow(root, 4, new Control[] { Label("Скорость"), _speed });
        AddRow(root, 5, new Control[] { Label("Заметки"), new TextBlock() });
        root.Children.Add(_notes);
        Grid.SetRow(_notes, 6);
        Grid.SetRowSpan(_notes, 3);

        var saveBtn = new Button
        {
            Content = "Сохранить и закрыть",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 40,
            Background = Pal.B("#4a3728"),
            Foreground = Pal.B("#f0c27b"),
            BorderBrush = Pal.B("#4a3728"),
            CornerRadius = new CornerRadius(10),
            FontWeight = FontWeight.SemiBold,
        };
        saveBtn.Click += (_, _) => { Apply(); Close(); };
        root.Children.Add(saveBtn);
        Grid.SetRow(saveBtn, 9);

        Content = root;
    }

    private void Apply()
    {
        _t.Name = _name.Text ?? _t.Name;
        _t.Hp = (int)_hp.Value;
        _t.MaxHp = (int)_maxHp.Value;
        _t.Ac = (int)_ac.Value;
        _t.Init = (int)_init.Value;
        _t.Speed = _speed.Text ?? "";
        _t.Notes = _notes.Text ?? "";
        _save();
    }

    private static TextBlock Label(string s) => new()
    {
        Text = s,
        Foreground = Pal.B("#9c8b78"),
        FontSize = 12.5,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
    };

    private static TextBox Field(string hint, string value) => new()
    {
        Text = value,
        Watermark = hint,
        Background = Pal.B("#1c1714"),
        Foreground = Pal.B("#efe3d2"),
        BorderBrush = Pal.B("#2a211a"),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10, 6),
    };

    private static NumericUpDown Num(string hint, int value, decimal min, decimal max) => new()
    {
        Value = value,
        Minimum = min,
        Maximum = max,
        MinWidth = 90,
    };

    private static TextBox Area(string hint, string value) => new()
    {
        Text = value,
        Watermark = hint,
        Background = Pal.B("#1c1714"),
        Foreground = Pal.B("#efe3d2"),
        BorderBrush = Pal.B("#2a211a"),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10, 6),
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
    };

    private static void AddRow(Grid grid, int row, Control[] cells)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4) };
        foreach (var c in cells) stack.Children.Add(c);
        grid.Children.Add(stack);
        Grid.SetRow(stack, row);
    }
}
