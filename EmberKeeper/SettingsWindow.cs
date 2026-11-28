using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Окно настроек: темы оформления, лицензия, сведения о программе.</summary>
public sealed class SettingsWindow : Window
{
    private static readonly (string Id, string Label, string Color)[] Swatches =
    {
        ("amber", "Янтарь", "#d99b4a"),
        ("green", "Зелень", "#79b06e"),
        ("blue", "Синева", "#6d9fe0"),
        ("purple", "Фиолет", "#a07ce0"),
        ("teal", "Бирюза", "#58b3ae"),
        ("wine", "Вино", "#cf6a7d"),
    };

    public SettingsWindow()
    {
        Title = "EmberKeeper — настройки";
        Width = 660;
        Height = 880;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var shell = new Border
        {
            Background = Pal.B("#100d0b"),
            BorderBrush = Pal.B("#352a21"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };

        /* ---------- своя рамка-заголовок ---------- */
        var header = new Border
        {
            Background = Pal.B("#15110e"),
            BorderBrush = Pal.B("#352a21"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 12),
        };
        var hrow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        brand.Children.Add(new Border
        {
            Width = 26, Height = 26, CornerRadius = new CornerRadius(13),
            Background = Pal.B("#221b16"),
            BorderBrush = Pal.B("#e0b072"), BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "E", FontFamily = new FontFamily(Fonts.Heading), FontSize = 14, FontWeight = FontWeight.Bold,
                Foreground = Pal.B("#f0c27b"), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });
        brand.Children.Add(new TextBlock
        {
            Text = "Настройки", FontFamily = new FontFamily(Fonts.Heading), FontSize = 17, FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"), VerticalAlignment = VerticalAlignment.Center,
        });
        hrow.Children.Add(brand);
        var close = TitleButton("✕");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 2);
        hrow.Children.Add(close);
        header.Child = hrow;
        header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        /* ---------- содержимое ---------- */
        var content = new StackPanel { Margin = new Thickness(22, 18, 22, 20), Spacing = 18 };

        content.Children.Add(Section("ОФОРМЛЕНИЕ"));
        content.Children.Add(new TextBlock
        {
            Text = "Цветовой стиль", FontSize = 11, LetterSpacing = 0.6, Foreground = Pal.B("#9c8b78"),
            Margin = new Thickness(0, -8, 0, 0),
        });

        var swatches = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 10, LineSpacing = 10 };
        foreach (var (id, label, color) in Swatches)
        {
            bool selected = Pal.Style == id;
            var cell = new Border
            {
                Width = 96, Height = 76, CornerRadius = new CornerRadius(12),
                Background = Pal.B("#1c1714"),
                BorderBrush = selected ? Pal.B("#cf9245") : Pal.B("#352a21"),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var inner = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            inner.Children.Add(new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(Color.Parse(color)),
                BorderBrush = Pal.B("#0d0b08"), BorderThickness = new Thickness(1),
            });
            inner.Children.Add(new TextBlock
            {
                Text = label, FontSize = 10.5,
                Foreground = selected ? Pal.B("#f6e7cf") : Pal.B("#b7a794"),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            cell.Child = inner;
            cell.PointerPressed += (_, _) => ApplyPalette(!Pal.Dark, id);
            swatches.Children.Add(cell);
        }
        content.Children.Add(swatches);

        content.Children.Add(new TextBlock
        {
            Text = "Освещённость", FontSize = 11, LetterSpacing = 0.6, Foreground = Pal.B("#9c8b78"),
            Margin = new Thickness(0, 2, 0, 0),
        });
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        modes.Children.Add(ModeButton("Тёмная", true));
        modes.Children.Add(ModeButton("Светлая", false));
        content.Children.Add(modes);

        content.Children.Add(new Border { Height = 1, Background = Pal.B("#2a211a"), Margin = new Thickness(0, 4) });

        /* ---------- интерфейс ---------- */
        content.Children.Add(Section("ИНТЕРФЕЙС"));
        content.Children.Add(PillGroup("Раскладка", new[]
        {
            ("rail", "Колонка иконок"), ("top", "Меню сверху"), ("classic", "Классика"),
        }, () => Design.Layout, v => ApplyInterface(Design.Variant, v, Fonts.Current)));

        content.Children.Add(PillGroup("Оформление", new[]
        {
            ("classic", "Классика"), ("a", "Аврора"), ("b", "Угли"), ("c", "Стекло"),
        }, () => Design.Variant, v => ApplyInterface(v, Design.Layout, Fonts.Current)));

        content.Children.Add(PillGroup("Шрифт", new[]
        {
            ("f5", "Свэг"), ("f7", "Дружелюбный"), ("f4", "Академический"), ("f2", "Современный"), ("f1", "Классика"),
        }, () => Fonts.Current, v => ApplyInterface(Design.Variant, Design.Layout, v)));

        content.Children.Add(new Border { Height = 1, Background = Pal.B("#2a211a"), Margin = new Thickness(0, 4) });

        /* ---------- порядок разделов ---------- */
        content.Children.Add(Section("РАЗДЕЛЫ МЕНЮ"));
        content.Children.Add(new TextBlock
        {
            Text = "Стрелками меняйте порядок кнопок в меню.", FontSize = 11, Foreground = Pal.B("#8d7a66"),
        });
        var orderHost = new StackPanel { Spacing = 6 };
        content.Children.Add(orderHost);

        var order = Settings.LoadNavOrder();
        var nav = MainWindow.Nav;
        if (order == null || order.Count != nav.Length || order.Distinct().Count() != nav.Length)
            order = Enumerable.Range(0, nav.Length).ToList();

        void RefreshOrder()
        {
            orderHost.Children.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                int pos = i;
                int navIdx = order[i];
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Height = 34 };
                var lbl = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
                lbl.Children.Add(Icons.Make(nav[navIdx].Icon, 16, "#b98f52"));
                lbl.Children.Add(new TextBlock { Text = nav[navIdx].Label, FontSize = 12.5, Foreground = Pal.B("#efe3d2"), VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(lbl);

                Button Arrow(string glyph, int delta)
                {
                    var b = new Button
                    {
                        Content = glyph, Width = 26, Height = 26, Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(7), FontSize = 12,
                        Background = Pal.B("#221b16"), Foreground = Pal.B("#efe3d2"),
                        BorderBrush = Pal.B("#352a21"), BorderThickness = new Thickness(1),
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        IsEnabled = delta < 0 ? pos > 0 : pos < order!.Count - 1,
                    };
                    b.Click += (_, _) =>
                    {
                        int j = pos + delta;
                        if (j < 0 || j >= order!.Count) return;
                        (order[pos], order[j]) = (order[j], order[pos]);
                        Settings.SaveNavOrder(order);
                        (Owner as MainWindow)?.ApplyNavOrderNow();
                        RefreshOrder();
                    };
                    return b;
                }
                var up = Arrow("▲", -1);
                var down = Arrow("▼", 1);
                Grid.SetColumn(up, 1);
                Grid.SetColumn(down, 2);
                up.Margin = new Thickness(0, 0, 6, 0);
                row.Children.Add(up);
                row.Children.Add(down);
                orderHost.Children.Add(row);
            }
        }
        RefreshOrder();

        content.Children.Add(new Border { Height = 1, Background = Pal.B("#2a211a"), Margin = new Thickness(0, 4) });

        /* ---------- лицензия ---------- */
        content.Children.Add(Section("ЛИЦЕНЗИЯ"));
        var lic = License.Current;
        var info = new StackPanel { Spacing = 8 };
        info.Children.Add(Row("Имя", string.IsNullOrWhiteSpace(lic?.Name) ? "—" : lic!.Name));
        info.Children.Add(Row("Статус", lic != null ? "активна" : "нет лицензии"));
        info.Children.Add(Row("Срок", lic == null ? "—" : (lic.IsPerpetual ? "бессрочно" : lic.Expires)));
        info.Children.Add(Row("Редакция", lic?.Edition ?? "—"));
        content.Children.Add(info);

        content.Children.Add(new TextBlock
        {
            Text = "Код этого компьютера", FontSize = 11, LetterSpacing = 0.6, Foreground = Pal.B("#9c8b78"),
        });
        var codeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        codeRow.Children.Add(new SelectableTextBlock
        {
            Text = License.MachineId(), FontFamily = new FontFamily("Consolas"), FontSize = 17,
            Foreground = Pal.B("#efe3d2"), VerticalAlignment = VerticalAlignment.Center,
        });
        var copy = new Button
        {
            Content = "Копировать", Padding = new Thickness(10, 5), CornerRadius = new CornerRadius(8),
            Background = Pal.B("#221b16"), Foreground = Pal.B("#efe3d2"), BorderBrush = Pal.B("#352a21"),
            BorderThickness = new Thickness(1), FontSize = 11.5,
        };
        copy.Click += async (_, _) =>
        {
            var clip = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clip != null) await clip.SetTextAsync(License.MachineId());
        };
        codeRow.Children.Add(copy);
        var activate = new Button
        {
            Content = "Активировать лицензию…", Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(9),
            Background = Pal.B("#221b16"), Foreground = Pal.B("#f6e7cf"), BorderBrush = Pal.B("#e0b072"),
            BorderThickness = new Thickness(1), FontSize = 12,
        };
        activate.Click += async (_, _) =>
        {
            var w = new ActivationWindow();
            await w.ShowDialog(this);
        };
        codeRow.Children.Add(activate);
        content.Children.Add(codeRow);

        content.Children.Add(new Border { Height = 1, Background = Pal.B("#2a211a"), Margin = new Thickness(0, 4) });

        /* ---------- о программе ---------- */
        content.Children.Add(Section("О ПРОГРАММЕ"));
        var ver = Updater.Version + (Updater.Version.StartsWith("1.0") ? " demo" : "");
        content.Children.Add(new TextBlock
        {
            Text = "EmberKeeper " + ver + " · рабочий стол мастера",
            FontSize = 12.5, Foreground = Pal.B("#d7c6b2"),
        });
        content.Children.Add(new TextBlock
        {
            Text = "Все версии до 1.1.0 помечаются как demo. Данные кампании хранятся рядом с программой.",
            FontSize = 11.5, Foreground = Pal.B("#8d7a66"), TextWrapping = TextWrapping.Wrap,
        });

        var updBtn = new Button
        {
            Content = Icons.Row("refresh", "Проверить обновления", 14, "#efe3d2", "#efe3d2"),
            Padding = new Thickness(13, 7), CornerRadius = new CornerRadius(9),
            Background = Pal.B("#221b16"), Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"), BorderThickness = new Thickness(1),
            FontSize = 12, Margin = new Thickness(0, 4, 0, 0),
        };
        updBtn.Click += (_, _) => { var w = new UpdateWindow(); _ = w.ShowDialog(this); };
        content.Children.Add(updBtn);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0),
        };
        var done = new Button
        {
            Content = "Готово", Padding = new Thickness(18, 9), CornerRadius = new CornerRadius(10),
            Background = Pal.B("#cf9245"), Foreground = Pal.B("#241505"), BorderBrush = Pal.B("#e0b072"),
            BorderThickness = new Thickness(1), FontWeight = FontWeight.Bold, FontSize = 12.5,
        };
        done.Click += (_, _) => Close();
        footer.Children.Add(done);
        content.Children.Add(footer);

        var scroll = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        shell.Child = root;
        Content = shell;
    }

    private Control PillGroup(string label, (string Id, string Label)[] items, Func<string> current, Action<string> pick)
    {
        var root = new StackPanel { Spacing = 6 };
        root.Children.Add(new TextBlock
        {
            Text = label, FontSize = 11, LetterSpacing = 0.6, Foreground = Pal.B("#9c8b78"),
        });
        var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        foreach (var (id, text) in items)
        {
            bool on = current() == id;
            var b = new Button
            {
                Content = text, Padding = new Thickness(13, 6), CornerRadius = new CornerRadius(9),
                FontSize = 11.5, FontWeight = on ? FontWeight.Bold : FontWeight.Normal,
                Background = on ? Pal.B("#cf9245") : Pal.B("#221b16"),
                Foreground = on ? Pal.B("#241505") : Pal.B("#efe3d2"),
                BorderBrush = on ? Pal.B("#e0b072") : Pal.B("#352a21"),
                BorderThickness = new Thickness(1),
            };
            var captured = id;
            b.Click += (_, _) => pick(captured);
            row.Children.Add(b);
        }
        root.Children.Add(row);
        return root;
    }

    private void ApplyInterface(string design, string layout, string fonts)
    {
        Design.Set(design, layout);
        Fonts.Set(fonts);
        Pal.Set(Settings.LoadDark(), Settings.LoadStyle());

        if (Owner is MainWindow main)
        {
            var state = main.WindowState;
            var next = new MainWindow { WindowState = state };
            next.Show();
            main.Close();
        }
        Close();
    }

    private Button TitleButton(string text) => new()
    {
        Content = text, Width = 30, Height = 26, Padding = new Thickness(0),
        CornerRadius = new CornerRadius(7), FontSize = 12,
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        Background = Pal.B("#221b16"), Foreground = Pal.B("#d7c6b2"), BorderBrush = Pal.B("#352a21"),
        BorderThickness = new Thickness(1),
    };

    private Control Section(string text) => new StackPanel
    {
        Children =
        {
            new TextBlock
            {
                Text = text, FontSize = 10.5, LetterSpacing = 1.1, FontWeight = FontWeight.Bold,
                Foreground = Pal.B("#f0c27b"),
            },
        },
    };

    private Control Row(string label, string value)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*") };
        g.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12, Foreground = Pal.B("#8d7a66"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var v = new TextBlock
        {
            Text = value, FontSize = 12.5, Foreground = Pal.B("#efe3d2"),
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        return g;
    }

    private Button ModeButton(string label, bool dark)
    {
        bool selected = Pal.Dark == dark;
        var b = new Button
        {
            Content = label, Padding = new Thickness(16, 7), CornerRadius = new CornerRadius(9),
            FontSize = 12, FontWeight = selected ? FontWeight.Bold : FontWeight.Normal,
            Background = selected ? Pal.B("#cf9245") : Pal.B("#221b16"),
            Foreground = selected ? Pal.B("#241505") : Pal.B("#efe3d2"),
            BorderBrush = selected ? Pal.B("#e0b072") : Pal.B("#352a21"),
            BorderThickness = new Thickness(1),
        };
        b.Click += (_, _) => ApplyPalette(dark, Pal.Style);
        return b;
    }

    private void ApplyPalette(bool dark, string style)
    {
        Pal.Set(dark, style);
        Settings.SaveTheme(dark, style);

        if (Owner is MainWindow main)
        {
            var state = main.WindowState;
            var next = new MainWindow { WindowState = state };
            next.Show();
            main.Close();
        }
        Close();
    }
}
