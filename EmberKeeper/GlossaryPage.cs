using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public sealed class GlossaryPage : UserControl
{
    private static readonly (string Term, string Desc)[] Entries =
    {
        ("Ослеплён", "Существо не может видеть. Броски атаки по нему совершаются с преимуществом, а его атаки — с помехой."),
        ("Очарован", "Не может атаковать очаровавшего. Очаровавший имеет преимущество на проверки взаимодействия."),
        ("Оглохший", "Не слышит. Проваливает проверки, основанные на слухе."),
        ("Испуган", "Помеха на проверки и атаки, пока источник страха в поле зрения. Не может добровольно приближаться к нему."),
        ("Схвачен", "Скорость 0. Захват прекращается, если нападавший недееспособен или уведён."),
        ("Недееспособен", "Не может совершать действия и реакции."),
        ("Невидимый", "Невозможен для взгляда. Атаки по нему с помехой, его атаки с преимуществом."),
        ("Парализован", "Недееспособен, не может двигаться и говорить. Атаки в упор — критические."),
        ("Окаменевший", "Превращён в камень. Недееспособен, сопротивляемость ко всем уронам."),
        ("Отравлен", "Помеха на броски атаки и проверки характеристик."),
        ("Сбит с ног", "Единственный способ передвижения — ползание. Атаки в упор с преимуществом, дальние — с помехой."),
        ("Опутан", "Скорость 0. Помеха на атаки и спасброски Ловкости."),
        ("Оглушён", "Недееспособен, не может двигаться, говорит невнятно."),
        ("Бессознательный", "Недееспособен, падает, скорость 0. Броски атаки в упор с преимуществом и критические."),
    };

    private readonly TextBox _search;
    private readonly StackPanel _list;
    private readonly TextBlock _desc;

    public GlossaryPage()
    {
        var root = new Grid { Margin = new Thickness(22, 18, 22, 12) };
        root.RowDefinitions = RowDefinitions.Parse("Auto,Auto,*,Auto");

        var title = new TextBlock
        {
            Text = "Справочник",
            FontFamily = new FontFamily(Fonts.Heading),
            FontSize = 28,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"),
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        _search = new TextBox
        {
            Watermark = "Поиск состояния…",
            Background = Pal.B("#1c1714"),
            Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#2a211a"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9),
            Margin = new Thickness(0, 12, 0, 10),
        };
        _search.TextChanged += (_, _) => RenderList();
        Grid.SetRow(_search, 1);
        root.Children.Add(_search);

        var split = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*") };
        Grid.SetRow(split, 2);
        root.Children.Add(split);

        var listScroll = new ScrollViewer();
        _list = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 12, 0) };
        listScroll.Content = _list;
        split.Children.Add(listScroll);

        _desc = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14.5,
            LineHeight = 24,
            Foreground = Pal.B("#c9b8a4"),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var descBorder = new Border
        {
            Background = Pal.B("#1c1714"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18),
            Child = _desc,
        };
        Grid.SetColumn(descBorder, 1);
        split.Children.Add(descBorder);

        var hint = new TextBlock { Text = "состояния D&D 5e", Foreground = Pal.B("#83725f"), FontSize = 11.5, Margin = new Thickness(0, 10, 0, 0) };
        Grid.SetRow(hint, 3);
        root.Children.Add(hint);

        Content = root;
        RenderList();
    }

    private void RenderList()
    {
        _list.Children.Clear();
        string q = _search.Text?.Trim().ToLowerInvariant() ?? "";
        var found = Entries.Where(e => e.Term.ToLowerInvariant().Contains(q) || e.Desc.ToLowerInvariant().Contains(q)).ToList();

        if (found.Count == 0)
        {
            _desc.Text = "Ничего не найдено. Попробуйте другой запрос.";
            return;
        }

        // по умолчанию показываем первое состояние, чтобы правая часть не была пустой
        _desc.Text = found[0].Desc;

        bool first = true;
        foreach (var (term, desc) in found)
        {
            var d = desc;
            bool isFirst = first;
            first = false;
            var btn = new Button
            {
                Content = term,
                Background = Pal.B("#1c1714"),
                Foreground = Pal.B("#efe3d2"),
                BorderBrush = Pal.B("#2a211a"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9),
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            if (isFirst)
            {
                btn.Background = Pal.B("#2a211a");
                btn.BorderBrush = Pal.B("#d99b4a");
            }
            btn.Click += (_, _) => _desc.Text = d;
            _list.Children.Add(btn);
        }
    }
}
