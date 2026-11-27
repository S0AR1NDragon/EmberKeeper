using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Главное меню: краткая информация о программе, последняя сессия, быстрый переход.</summary>
public sealed class HomePage : UserControl
{
    public Action<int>? Navigate;
    public Action? OnExport;
    public Action? OnImport;
    public Action? OnPause;
    public Action? OnFinish;
    public Action? OnUpdates;

    private static Color Gold => Pal.C("#f0c27b");
    private static Color Ink => Pal.C("#efe3d2");
    private static Color Muted => Pal.C("#9c8b78");
    private static Color Line => Pal.C("#352a21");
    private static Color Panel => Pal.C("#1c1714");

    public HomePage()
    {
        Content = new ScrollViewer { Content = BuildBody(), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    private Control BuildBody()
    {
        var root = new StackPanel { Margin = new Thickness(26, 22, 26, 30), Spacing = 16 };

        // шапка
        var head = new StackPanel { Spacing = 4 };
        head.Children.Add(new TextBlock
        {
            Text = "✦ EMBERKEEPER",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 34,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Gold),
        });
        head.Children.Add(new TextBlock
        {
            Text = "Рабочий стол мастера: карта, инициатива, слайды, листы и библиотека — в одном окне.",
            FontSize = 14,
            Foreground = new SolidColorBrush(Muted),
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(head);

        // последняя сессия + счётчики
        root.Children.Add(Section("Последняя сессия"));
        root.Children.Add(SessionCard());

        // модули
        root.Children.Add(Section("Модули"));
        root.Children.Add(ModuleGrid());

        // подсказки
        root.Children.Add(Section("Подсказки"));
        root.Children.Add(Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Hint("Карта — тащите токены мышью, Alt+ЛКМ панорама, колесо масштаб, двойной клик открывает лист."),
                Hint("Несколько локаций — переключатель «Локация» сверху карты, у каждой свой фон и свой набор токенов."),
                Hint("Кости — кнопка «d20 +» справа сверху: любые кубики, количество и преимущество/помеха."),
                Hint("Экран игроков: «▣ Экран игроков» или Ctrl+P. Двойной клик по токену открывает лист."),
                Hint("Скрытые токены: флажок «Скрыт от игроков» — на втором экране их не видно."),
                Hint("Горячие клавиши: Ctrl+D — кости, Ctrl+P — экран игроков, Ctrl+F — поиск, Ctrl+M — меню."),
                Hint("Сцены: «＋ Элемент…» добавляет картинку на сцену, её можно перетаскивать мышью."),
                Hint("Связи: раздел «⛓ Связи» — граф персонажей, включая режим «⇢ Связать»."),
                Hint("Генераторы (✧ сверху): связи, характер НПС, стычки, добыча, таверны, тайны, погода — 15 наборов."),
                Hint("Случайная стычка: «→ На карту» сразу расставит противников токенами на текущей локации."),
            },
        }));

        return root;
    }

    private static TextBlock Hint(string text) => new()
    {
        Text = "•  " + text,
        FontSize = 12.5,
        LineHeight = 20,
        Foreground = new SolidColorBrush(Pal.C("#c9b8a4")),
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBlock Section(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 10.5,
        LetterSpacing = 1.6,
        Foreground = new SolidColorBrush(Pal.C("#8d7a66")),
        Margin = new Thickness(0, 8, 0, 0),
    };

    private static Border Card(Control content) => new()
    {
        Background = new SolidColorBrush(Panel),
        BorderBrush = new SolidColorBrush(Line),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(16),
        Child = content,
    };

    private Control SessionCard()
    {
        var stack = new StackPanel { Spacing = 14 };

        var last = LastSession();
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        line.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Pal.C("#7fa66e")), VerticalAlignment = VerticalAlignment.Center });
        line.Children.Add(new TextBlock
        {
            Text = last,
            FontSize = 13.5,
            Foreground = new SolidColorBrush(Ink),
            VerticalAlignment = VerticalAlignment.Center,
        });
        stack.Children.Add(line);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
        var exportBtn = new Button { Content = "⤓ Экспорт кампании" };
        exportBtn.Click += (_, _) => OnExport?.Invoke();
        var importBtn = new Button { Content = "⤒ Импорт кампании" };
        importBtn.Click += (_, _) => OnImport?.Invoke();
        actions.Children.Add(exportBtn);
        actions.Children.Add(importBtn);

        var pauseBtn = new Button { Content = "⏸ Пауза" };
        pauseBtn.Click += (_, _) => OnPause?.Invoke();
        var finishBtn = new Button { Content = "🏁 Завершить сессию" };
        finishBtn.Click += (_, _) => OnFinish?.Invoke();
        var updateBtn = new Button { Content = "⟳ Обновления" };
        updateBtn.Click += (_, _) => OnUpdates?.Invoke();
        actions.Children.Add(updateBtn);
        actions.Children.Add(pauseBtn);
        actions.Children.Add(finishBtn);
        stack.Children.Add(actions);

        stack.Children.Add(new TextBlock
        {
            Text = "EmberKeeper " + Updater.Version + " · Лицензия: " + License.Describe(),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Muted),
            Margin = new Thickness(2, 0, 0, 2),
        });

        var stats = new UniformGrid { Columns = 5, Margin = new Thickness(0, 4, 0, 0) };
        stats.Children.Add(Stat("Локации", MapCount()));
        stats.Children.Add(Stat("Герои", HeroStore.Load().Count));
        stats.Children.Add(Stat("НПС", NpcStore.Load().Count));
        stats.Children.Add(Stat("Сцены", SceneStore.Load().Count));
        stats.Children.Add(Stat("Инициатива", InitStore.Load().Count));
        stack.Children.Add(stats);

        return Card(stack);
    }

    private static int MapCount()
    {
        try { return MapStore.Load().Maps.Count; } catch { return 0; }
    }

    private static Control Stat(string label, int value)
    {
        var sp = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 10, 0) };
        sp.Children.Add(new TextBlock
        {
            Text = value.ToString(),
            FontFamily = new FontFamily("Georgia"),
            FontSize = 30,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Gold),
        });
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11.5, Foreground = new SolidColorBrush(Muted) });
        return sp;
    }

    private static string LastSession()
    {
        try
        {
            var dir = MapStore.Dir;
            if (!Directory.Exists(dir)) return "Сессий ещё не было — начните с карты или библиотеки.";

            DateTime? newest = null;
            foreach (var f in Directory.GetFiles(dir, "*.json"))
            {
                var t = File.GetLastWriteTime(f);
                if (newest == null || t > newest) newest = t;
            }
            if (newest == null) return "Сессий ещё не было — начните с карты или библиотеки.";
            return "Последнее сохранение: " + newest.Value.ToString("dd.MM.yyyy · HH:mm");
        }
        catch { return "Не удалось определить время последней сессии."; }
    }

    private Control ModuleGrid()
    {
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 0) };

        var items = new (int idx, string icon, string title, string desc)[]
        {
            (1, "♜", "Карта", "Локации, токены, туман, стены, шаблоны"),
            (2, "⚔", "Инициатива", "Бойцы, броски, ходы и раунды"),
            (3, "◈", "Слайды", "Сцены и показ на втором экране"),
            (4, "✧", "Герои", "Листы: характеристики, спасброски, навыки"),
            (5, "⌕", "Библиотека", "Заклинания, предметы, бестиарий 2014/2024"),
            (6, "☗", "НПС", "Карточки, теги, музыка"),
            (7, "⛓", "Связи", "Отношения, фракции, города"),
            (8, "✦", "Справочник", "Состояния и правила"),
        };

        foreach (var (idx, icon, title, desc) in items)
        {
            int i = idx;
            var card = new Border
            {
                MinWidth = 180,
                Margin = new Thickness(0, 0, 12, 12),
                Background = new SolidColorBrush(Panel),
                BorderBrush = new SolidColorBrush(Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            var sp = new StackPanel { Spacing = 6 };
            var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            top.Children.Add(new TextBlock { Text = icon, FontSize = 18, Foreground = new SolidColorBrush(Gold), VerticalAlignment = VerticalAlignment.Center });
            top.Children.Add(new TextBlock { Text = title, FontFamily = new FontFamily("Georgia"), FontSize = 16, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Ink), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(top);
            sp.Children.Add(new TextBlock { Text = desc, FontSize = 11.5, Foreground = new SolidColorBrush(Muted), TextWrapping = TextWrapping.Wrap });
            card.Child = sp;

            card.PointerEntered += (_, _) => card.Background = new SolidColorBrush(Pal.C("#221b16"));
            card.PointerExited += (_, _) => card.Background = new SolidColorBrush(Panel);
            card.PointerPressed += (_, _) => Navigate?.Invoke(i);

            grid.Children.Add(card);
        }

        return new ScrollViewer
        {
            Content = grid,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }
}
