using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Связи кампании: граф персонажей, НПС, мест и фракций с линиями отношений.</summary>
public sealed class RelationsPage : UserControl
{
    private const double NodeW = 136;
    private const double NodeH = 54;

    private RelationMap _map;
    private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    private readonly LinkLayer _links;
    private readonly Dictionary<string, Border> _nodeViews = new();
    private readonly ComboBox _charBox;
    private readonly ComboBox _linkKind;
    private readonly TextBlock _status;
    private readonly ToggleButton _linkMode;

    private RelNode? _selected;
    private RelNode? _drag;
    private Point _dragStart;
    private Point _nodeStart;
    private string? _linkFrom;

    private static readonly (string Key, string Label, string Color)[] LinkKinds =
    {
        ("ally", "союзник", "#7fa66e"),
        ("enemy", "враг", "#7c2a2c"),
        ("family", "семья", "#d99b4a"),
        ("debt", "долг", "#70a3a2"),
    };

    public RelationsPage()
    {
        _map = RelationStore.Load();
        _links = new LinkLayer(_map, () => _nodeViews);

        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 16, 22, 10) };

        _charBox = new ComboBox
        {
            Width = 240,
            Background = Pal.B("#221b16"),
            Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"),
            CornerRadius = new CornerRadius(8),
            MinHeight = 32,
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 8, 6),
        };
        ReloadCharacters();
        toolbar.Children.Add(_charBox);

        toolbar.Children.Add(Btn("＋ Добавить", AddFromList, true));
        toolbar.Children.Add(Btn("＋ Свой узел", AddCustom));
        toolbar.Children.Add(Btn("Удалить", DeleteSelected));

        _linkKind = new ComboBox
        {
            Width = 130,
            Background = Pal.B("#221b16"),
            Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"),
            CornerRadius = new CornerRadius(8),
            MinHeight = 32,
            FontSize = 12.5,
            Margin = new Thickness(8, 0, 8, 6),
        };
        foreach (var k in LinkKinds) _linkKind.Items.Add(k.Label);
        _linkKind.SelectedIndex = 0;
        toolbar.Children.Add(_linkKind);

        _linkMode = new ToggleButton
        {
            Content = "Связать",
            Padding = new Thickness(12, 7),
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(0, 0, 8, 6),
            Background = Pal.B("#221b16"),
            Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"),
            BorderThickness = new Thickness(1),
            FontSize = 12.5,
        };
        _linkMode.IsCheckedChanged += (_, _) =>
        {
            _linkFrom = null;
            SetStatus(_linkMode.IsChecked == true
                ? "Режим связи: выберите первый узел"
                : "Режим связи выключен");
        };
        toolbar.Children.Add(_linkMode);

        _status = new TextBlock
        {
            Text = "Перетаскивайте узлы мышью. Для связи нажмите «Связать» и кликните два узла.",
            Foreground = Pal.B("#9c8b78"),
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 6),
        };
        toolbar.Children.Add(_status);

        var canvasHost = new Border
        {
            Background = Pal.B("#15110e"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(22, 0, 22, 16),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = _canvas,
            },
        };

        _canvas.Width = 1600;
        _canvas.Height = 900;
        _canvas.Children.Add(_links);
        Canvas.SetLeft(_links, 0);
        Canvas.SetTop(_links, 0);
        _links.Width = _canvas.Width;
        _links.Height = _canvas.Height;
        _links.IsHitTestVisible = false;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var head = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(22, 18, 22, 0),
            Children =
            {
                new Border { Width = 4, Height = 26, CornerRadius = new CornerRadius(2), Background = Pal.B("#d99b4a") },
                new TextBlock { Text = "Связи", FontFamily = new FontFamily(Fonts.Heading), FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Pal.B("#f6e7cf") },
            },
        };
        root.Children.Add(head);

        var body = new StackPanel();
        body.Children.Add(toolbar);
        body.Children.Add(canvasHost);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        Content = root;

        RebuildNodes();
        _links.InvalidateVisual();
    }

    private void ReloadCharacters()
    {
        var names = new List<string>();
        names.AddRange(HeroStore.Load().Select(h => "Герой: " + h.Name));
        names.AddRange(NpcStore.Load().Select(n => "НПС: " + n.Name));
        if (names.Count == 0) names.Add("(нет героев и НПС)");
        _charBox.ItemsSource = names;
        _charBox.SelectedIndex = 0;
    }

    private void SetStatus(string text) => _status.Text = text;

    private Button Btn(string text, Action onClick, bool accent = false)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(12, 7),
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(0, 0, 8, 6),
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

    /* ---------------- узлы ---------------- */

    /// <summary>Добавить готовую пару узлов со связью (из генератора связей).</summary>
    public void AddPair(string a, string b, string label, string note)
    {
        double x = 80 + (_map.Nodes.Count % 3) * 60;
        double y = 80 + (_map.Nodes.Count % 4) * 40;

        var na = new RelNode { Name = a, Kind = "npc", X = x, Y = y, Note = note };
        var nb = new RelNode { Name = b, Kind = "npc", X = x + 300, Y = y + 60, Note = note };
        _map.Nodes.Add(na);
        _map.Nodes.Add(nb);
        _map.Links.Add(new RelLink { From = na.Id, To = nb.Id, Label = label, Kind = "ally" });
        RelationStore.Save(_map);
        RebuildNodes();
        _links.InvalidateVisual();
        SetStatus("Добавлена связь: " + a + " — " + b);
    }

    private void AddFromList()
    {
        var raw = _charBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("(")) return;

        bool isHero = raw.StartsWith("Герой: ", StringComparison.Ordinal);
        bool isNpc = raw.StartsWith("НПС: ", StringComparison.Ordinal);
        var name = isHero ? raw.Substring(7).Trim() : isNpc ? raw.Substring(5).Trim() : raw.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_map.Nodes.Any(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase))) return;

        var node = new RelNode
        {
            Name = name,
            Kind = isHero ? "hero" : "npc",
            X = 60 + (_map.Nodes.Count % 5) * 180,
            Y = 60 + (_map.Nodes.Count / 5) * 110,
        };
        _map.Nodes.Add(node);
        RelationStore.Save(_map);
        RebuildNodes();
        _links.InvalidateVisual();
        SetStatus("Добавлен узел: " + name);
    }

    private async void AddCustom()
    {
        var name = await Prompt("Новый узел", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        _map.Nodes.Add(new RelNode
        {
            Name = name.Trim(),
            Kind = "place",
            X = 60 + (_map.Nodes.Count % 5) * 180,
            Y = 60 + (_map.Nodes.Count / 5) * 110,
        });
        RelationStore.Save(_map);
        RebuildNodes();
        _links.InvalidateVisual();
    }

    private void DeleteSelected()
    {
        if (_selected == null) return;
        var id = _selected.Id;
        _map.Nodes.RemoveAll(n => n.Id == id);
        _map.Links.RemoveAll(l => l.From == id || l.To == id);
        _selected = null;
        RelationStore.Save(_map);
        RebuildNodes();
        _links.InvalidateVisual();
        SetStatus("Узел удалён");
    }

    private void RebuildNodes()
    {
        foreach (var view in _nodeViews.Values)
            _canvas.Children.Remove(view);
        _nodeViews.Clear();

        foreach (var node in _map.Nodes)
        {
            var n = node;
            var border = new Border
            {
                Width = NodeW,
                MinHeight = NodeH,
                CornerRadius = new CornerRadius(10),
                Background = Pal.B(NodeColor(n.Kind)),
                BorderBrush = Pal.B("#4a3728"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = n.Name,
                            FontFamily = new FontFamily(Fonts.Heading),
                            FontSize = 12,
                            FontWeight = FontWeight.Bold,
                            Foreground = Pal.B("#f6e7cf"),
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock { Text = KindLabel(n.Kind), FontSize = 9.5, Foreground = Pal.B("#9c8b78") },
                    },
                },
            };

            border.PointerPressed += (_, e) =>
            {
                if (_linkMode.IsChecked == true)
                {
                    HandleLinkClick(n);
                    e.Handled = true;
                    return;
                }
                _selected = n;
                HighlightSelection();
                _drag = n;
                _dragStart = e.GetPosition(_canvas);
                _nodeStart = new Point(n.X, n.Y);
                e.Pointer.Capture(border);
                e.Handled = true;
            };
            border.DoubleTapped += async (_, e) =>
            {
                e.Handled = true;
                var name = await Prompt("Имя узла", n.Name);
                if (string.IsNullOrWhiteSpace(name)) return;
                n.Name = name.Trim();
                RelationStore.Save(_map);
                RebuildNodes();
                _links.InvalidateVisual();
            };

            border.PointerMoved += (_, e) =>
            {
                if (_drag == null || !ReferenceEquals(_drag, n)) return;
                var p = e.GetPosition(_canvas);
                n.X = Math.Max(0, _nodeStart.X + (p.X - _dragStart.X));
                n.Y = Math.Max(0, _nodeStart.Y + (p.Y - _dragStart.Y));
                Canvas.SetLeft(border, n.X);
                Canvas.SetTop(border, n.Y);
                _links.InvalidateVisual();
            };
            border.PointerReleased += (_, _) =>
            {
                if (_drag != null)
                {
                    _drag = null;
                    RelationStore.Save(_map);
                }
            };

            Canvas.SetLeft(border, n.X);
            Canvas.SetTop(border, n.Y);
            _canvas.Children.Add(border);
            _nodeViews[n.Id] = border;
        }

        HighlightSelection();
    }

    private void HandleLinkClick(RelNode n)
    {
        if (_linkFrom == null)
        {
            _linkFrom = n.Id;
            SetStatus("Первый узел: " + n.Name + ". Выберите второй.");
            return;
        }

        if (_linkFrom == n.Id) { _linkFrom = null; SetStatus("Отменено"); return; }

        var kind = LinkKinds[Math.Max(0, _linkKind.SelectedIndex)];
        if (_map.Links.Any(l => (l.From == _linkFrom && l.To == n.Id) || (l.From == n.Id && l.To == _linkFrom)))
        {
            _linkFrom = null;
            SetStatus("Связь уже существует");
            return;
        }

        _map.Links.Add(new RelLink { From = _linkFrom, To = n.Id, Kind = kind.Key, Label = kind.Label });
        RelationStore.Save(_map);
        _links.InvalidateVisual();
        _linkFrom = null;
        SetStatus("Связь добавлена: " + kind.Label);
    }

    private void HighlightSelection()
    {
        foreach (var kv in _nodeViews)
        {
            bool on = _selected != null && kv.Key == _selected.Id;
            kv.Value.BorderBrush = on ? Pal.B("#f0c27b") : Pal.B("#4a3728");
            kv.Value.BorderThickness = new Thickness(on ? 2 : 1);
        }
    }

    private static string NodeColor(string kind) => kind switch
    {
        "hero" => "#2a2114",
        "npc" => "#241d18",
        "place" => "#1f1a26",
        _ => "#221b16",
    };

    private static string KindLabel(string kind) => kind switch
    {
        "hero" => "герой",
        "npc" => "НПС",
        "place" => "место",
        _ => "фракция",
    };

    /* ---------------- диалог ---------------- */

    private async System.Threading.Tasks.Task<string?> Prompt(string title, string initial)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var win = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            Background = Pal.B("#100d0b"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var box = new TextBox
        {
            Text = initial,
            Margin = new Thickness(16, 16, 16, 8),
            Background = Pal.B("#1c1714"),
            Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7),
        };
        var ok = new Button
        {
            Content = "ОК",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 16, 14),
            Padding = new Thickness(16, 7),
            Background = Pal.B("#cf9245"),
            Foreground = Pal.B("#241505"),
            BorderBrush = Pal.B("#e0b072"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
        };
        string? result = null;
        ok.Click += (_, _) => { result = box.Text; win.Close(); };
        var sp = new StackPanel();
        sp.Children.Add(box);
        sp.Children.Add(ok);
        win.Content = sp;
        if (owner != null) await win.ShowDialog(owner); else win.Show();
        return result;
    }

    /* ---------------- слой линий ---------------- */

    private sealed class LinkLayer : Control
    {
        private readonly RelationMap _map;
        private readonly Func<Dictionary<string, Border>> _views;

        public LinkLayer(RelationMap map, Func<Dictionary<string, Border>> views)
        {
            _map = map;
            _views = views;
        }

        public override void Render(DrawingContext ctx)
        {
            var views = _views();
            foreach (var link in _map.Links)
            {
                if (!views.TryGetValue(link.From, out var a) || !views.TryGetValue(link.To, out var b)) continue;

                double ax = a.Bounds.X + NodeW / 2, ay = a.Bounds.Y + NodeH / 2;
                double bx = b.Bounds.X + NodeW / 2, by = b.Bounds.Y + NodeH / 2;

                var color = link.Kind switch
                {
                    "enemy" => Pal.C("#7c2a2c"),
                    "family" => Pal.C("#d99b4a"),
                    "debt" => Pal.C("#70a3a2"),
                    _ => Pal.C("#7fa66e"),
                };

                var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)), 2);
                ctx.DrawLine(pen, new Point(ax, ay), new Point(bx, by));

                if (!string.IsNullOrWhiteSpace(link.Label))
                {
                    var ft = new FormattedText(link.Label, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10.5,
                        new SolidColorBrush(Pal.C("#c9b8a4")));
                    var mx = (ax + bx) / 2 - ft.Width / 2;
                    var my = (ay + by) / 2 - ft.Height / 2;
                    ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 16, 13, 11)), new Rect(mx - 4, my - 2, ft.Width + 8, ft.Height + 4));
                    ctx.DrawText(ft, new Point(mx, my));
                }
            }
        }
    }
}
