using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace EmberKeeper;

/// <summary>
/// Экран игроков: карта с инициативой, сцена или пустой экран.
/// Все элементы интерфейса создаются заново при каждом переключении —
/// так не бывает конфликтов «контрол уже имеет родителя».
/// </summary>
public sealed class PlayerWindow : Window
{
    private const double StageW = 1280;
    private const double StageH = 720;

    private readonly MapDoc _doc;
    private readonly ContentControl _host = new();
    private readonly DispatcherTimer _timer;

    // текущие элементы панели инициативы (пересоздаются в BuildMap)
    private MapView? _map;
    private int _fittedCols = -1;
    private int _fittedRows = -1;
    private StackPanel? _initHost;
    private TextBlock? _roundText;
    private TextBlock? _nowText;
    private TextBlock? _condText;

    public PlayerWindow(MapDoc doc)
    {
        _doc = doc;
        Live.AttachDoc(doc);
        Live.Window = this;

        Title = "Экран игроков";
        Background = Pal.B("#0b0908");
        Width = 1280;
        Height = 800;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = _host;

        BattleState.Changed += RefreshMap;
        Live.MapChanged += RefreshMap;
        Live.ScreenChanged += Apply;

        Closed += (_, _) =>
        {
            BattleState.Changed -= RefreshMap;
            Live.MapChanged -= RefreshMap;
            Live.ScreenChanged -= Apply;
            _timer.Stop();
            if (ReferenceEquals(Live.Window, this)) Live.Window = null;
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => RefreshMap();
        _timer.Start();

        Apply();
        Opened += (_, _) => Apply();
    }

    private void Apply()
    {
        try
        {
            _host.Content = Live.Screen switch
            {
                PlayerScreen.Scene => BuildScene(Live.Scene),
                PlayerScreen.Blank => BuildBlank(),
                _ => BuildMap(),
            };
            RefreshMap();
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "error.log"), "PlayerWindow.Apply: " + ex);
            }
            catch { }
        }
    }

    /* ---------------- карта + инициатива ---------------- */

    private Control BuildMap()
    {
        _map = new MapView(_doc)
        {
            IsEnabled = false,
            PlayerMode = true,
            ActiveName = BattleState.CurrentName,
        };
        _map.Loaded += (_, _) => FitPlayerMap();

        _roundText = new TextBlock { Text = "РАУНД 1", FontSize = 10, LetterSpacing = 1.4, Foreground = Pal.B("#9c8b78") };
        _nowText = new TextBlock
        {
            Text = "—",
            FontFamily = new FontFamily(Fonts.Heading),
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"),
            TextWrapping = TextWrapping.Wrap,
        };
        _condText = new TextBlock { Text = "", FontSize = 10.5, Foreground = Pal.B("#9c8b78") };
        _initHost = new StackPanel { Spacing = 4 };

        var mapHost = new Border
        {
            Background = Pal.B("#0d0b08"),
            ClipToBounds = true,
            Child = _map,
        };

        var nowBlock = new Border
        {
            Background = Pal.B("#221b16"),
            BorderBrush = Pal.B("#8c7030"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Child = new StackPanel { Spacing = 4, Children = { _roundText, _nowText, _condText } },
        };

        var side = new Border
        {
            Background = Pal.B("#15110e"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(16),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = new StackPanel { Spacing = 12, Children = { nowBlock, _initHost } },
            },
        };

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("*,320") };
        root.Children.Add(mapHost);
        Grid.SetColumn(side, 1);
        root.Children.Add(side);

        var view = _map;
        Dispatcher.UIThread.Post(() =>
        {
            if (view.Bounds.Width > 1) view.ResetView(view.Bounds.Width, view.Bounds.Height);
        }, DispatcherPriority.Background);

        return root;
    }

    /// <summary>Показать игрокам всю карту целиком.</summary>
    private void FitPlayerMap()
    {
        if (_map == null || _map.Bounds.Width <= 10 || _map.Bounds.Height <= 10) return;
        _map.ResetView(_map.Bounds.Width, _map.Bounds.Height);
        _fittedCols = _doc.Cols;
        _fittedRows = _doc.Rows;
    }

    private void RefreshMap()
    {
        if (Live.Screen != PlayerScreen.Map) return;
        try
        {
            if (_map == null || _initHost == null || _nowText == null || _roundText == null || _condText == null) return;

            var order = BattleState.Order;
            _roundText.Text = $"РАУНД {BattleState.Round}";
            _nowText.Text = BattleState.CurrentName ?? "—";

            _map.ActiveName = BattleState.CurrentName;
            if (_map.Bounds.Width > 10 && (_fittedCols != _doc.Cols || _fittedRows != _doc.Rows))
                FitPlayerMap();
            _map.InvalidateVisual();

            _initHost.Children.Clear();
            if (order.Count == 0)
            {
                _condText.Text = "";
                _initHost.Children.Add(new TextBlock
                {
                    Text = "Инициатива ещё не брошена",
                    FontSize = 12,
                    Foreground = Pal.B("#9c8b78"),
                    TextWrapping = TextWrapping.Wrap,
                });
                return;
            }

            int turn = Math.Clamp(BattleState.Turn, 0, order.Count - 1);
            _condText.Text = $"{order.Count} участников";

            for (int i = 0; i < order.Count; i++)
            {
                var c = order[i];
                bool current = i == turn;
                bool down = c.Hp <= 0;

                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*") };
                grid.Children.Add(new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontFamily = new FontFamily(Fonts.Heading),
                    FontSize = 13,
                    Foreground = current ? Pal.B("#f0c27b") : Pal.B("#8d7a66"),
                    VerticalAlignment = VerticalAlignment.Center,
                });

                var info = new StackPanel { Spacing = 1 };
                info.Children.Add(new TextBlock
                {
                    Text = c.Name,
                    FontSize = 12.5,
                    FontWeight = current ? FontWeight.Bold : FontWeight.Normal,
                    Foreground = current ? Pal.B("#f6e7cf") : Pal.B("#c9b8a4"),
                });
                info.Children.Add(new TextBlock
                {
                    Text = (c.IsPc ? "Герой" : "Враг") + $" · {c.Hp}/{c.MaxHp} ХП",
                    FontSize = 10.5,
                    Foreground = Pal.B("#8d7a66"),
                });
                Grid.SetColumn(info, 1);
                grid.Children.Add(info);

                _initHost.Children.Add(new Border
                {
                    Background = current ? Pal.B("#3a2d24") : Pal.B("#1c1714"),
                    BorderBrush = current ? Pal.B("#8c7030") : Pal.B("#2a211a"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 6),
                    Opacity = down ? 0.55 : 1.0,
                    Child = grid,
                });
            }
        }
        catch { }
    }

    /* ---------------- сцена ---------------- */

    private static Control BuildScene(Scene? scene)
    {
        var canvas = new Canvas { Width = StageW, Height = StageH };

        var bg = new Border { Width = StageW, Height = StageH };
        var img = ImageCache.Get(scene?.Image, 1920);
        bg.Background = img == null ? Brushes.Black : new ImageBrush(img) { Stretch = Stretch.UniformToFill };
        canvas.Children.Add(bg);

        if (scene != null)
        {
            foreach (var el in scene.Elements)
            {
                var bmp = ImageCache.Get(el.Image, 1280);
                if (bmp == null) continue;
                var image = new Image
                {
                    Source = bmp,
                    Stretch = Stretch.Uniform,
                    Width = StageW * el.W / 100.0,
                    Height = StageH * el.H / 100.0,
                    Opacity = Math.Clamp(el.Opacity, 0.1, 1),
                    RenderTransform = new RotateTransform(el.Rotation),
                    RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                };
                Canvas.SetLeft(image, StageW * el.X / 100.0);
                Canvas.SetTop(image, StageH * el.Y / 100.0);
                canvas.Children.Add(image);
            }

            if (!string.IsNullOrWhiteSpace(scene.Subtitle))
            {
                var sub = new TextBlock { Text = scene.Subtitle, FontSize = 20, Foreground = Pal.B("#f0c27b") };
                Canvas.SetLeft(sub, 70); Canvas.SetTop(sub, 460);
                canvas.Children.Add(sub);
            }

            var title = new TextBlock
            {
                Text = scene.Name,
                FontFamily = new FontFamily(Fonts.Heading),
                FontSize = 52,
                FontWeight = FontWeight.Bold,
                Foreground = Pal.B("#f6e7cf"),
            };
            Canvas.SetLeft(title, 70); Canvas.SetTop(title, 492);
            canvas.Children.Add(title);

            if (!string.IsNullOrWhiteSpace(scene.Text))
            {
                var text = new TextBlock
                {
                    Text = scene.Text,
                    FontSize = 20,
                    Foreground = Pal.B("#c9b8a4"),
                    MaxWidth = 900,
                    TextWrapping = TextWrapping.Wrap,
                };
                Canvas.SetLeft(text, 70); Canvas.SetTop(text, 572);
                canvas.Children.Add(text);
            }
        }

        return new Border
        {
            Background = Brushes.Black,
            Child = new Viewbox { Child = canvas, Stretch = Stretch.Uniform },
        };
    }

    private static Control BuildBlank()
    {
        var sp = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
        };
        sp.Children.Add(new TextBlock
        {
            Text = "EMBERKEEP",
            FontFamily = new FontFamily(Fonts.Heading),
            FontSize = 44,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f0c27b"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        sp.Children.Add(new TextBlock
        {
            Text = "Ожидание мастера…",
            FontSize = 16,
            Foreground = Pal.B("#9c8b78"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var root = new Grid { Background = Pal.B("#070605") };
        root.Children.Add(sp);
        return root;
    }
}
