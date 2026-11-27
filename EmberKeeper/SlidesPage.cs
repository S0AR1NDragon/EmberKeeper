using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace EmberKeeper;

/// <summary>
/// Сцены: список сцен, фон, текст и перетаскиваемые элементы с размером/поворотом/прозрачностью.
/// </summary>
public sealed class SlidesPage : UserControl
{
    private const double StageW = 1280;
    private const double StageH = 720;

    private readonly List<Scene> _scenes;
    private Scene? _current;
    private SceneElement? _selected;
    private bool _loading;

    private readonly ListBox _list = new();
    private readonly TextBox _name, _subtitle, _text, _notes;
    private readonly Canvas _stage = new() { Width = StageW, Height = StageH, Background = Brushes.Black };
    private readonly Border _bg;
    private readonly TextBlock _stageTitle, _stageSub, _stageText;
    private readonly TextBlock _status;
    private readonly StackPanel _elementBar = new() { Spacing = 6, IsVisible = false };
    private readonly TextBlock _elementTitle = new();

    private NumericUpDown? _elW, _elH, _elRot, _elOpacity;
    private Border? _elementBarHost;

    public SlidesPage()
    {
        _scenes = SceneStore.Load();
        if (_scenes.Count == 0)
        {
            _scenes.Add(new Scene { Name = "Сцена 1" });
            SceneStore.Save(_scenes);
        }

        _name = Field("Название сцены");
        _subtitle = Field("Подзаголовок");
        _text = Area("Текст на сцене", 64);
        _notes = Area("Заметки мастера", 80);
        _status = new TextBlock { FontSize = 11.5, Foreground = Pal.B("#9c8b78"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };

        _bg = new Border { Width = StageW, Height = StageH };
        _stage.Children.Add(_bg);

        _stageSub = new TextBlock { FontSize = 20, Foreground = Pal.B("#f0c27b"), TextWrapping = TextWrapping.Wrap, MaxWidth = 1100 };
        _stageTitle = new TextBlock { FontFamily = new FontFamily("Georgia"), FontSize = 52, FontWeight = FontWeight.Bold, Foreground = Pal.B("#f6e7cf"), TextWrapping = TextWrapping.Wrap, MaxWidth = 1100 };
        _stageText = new TextBlock { FontSize = 20, Foreground = Pal.B("#c9b8a4"), TextWrapping = TextWrapping.Wrap, MaxWidth = 900 };
        Canvas.SetLeft(_stageSub, 70); Canvas.SetTop(_stageSub, 460);
        Canvas.SetLeft(_stageTitle, 70); Canvas.SetTop(_stageTitle, 492);
        Canvas.SetLeft(_stageText, 70); Canvas.SetTop(_stageText, 572);
        _stage.Children.Add(_stageSub);
        _stage.Children.Add(_stageTitle);
        _stage.Children.Add(_stageText);

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*"), Margin = new Thickness(22, 18, 22, 14) };
        var left = BuildLeft();
        var right = BuildRight();
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        root.Children.Add(left);
        root.Children.Add(right);
        Content = root;

        BindFields();
        RefreshList();
    }

    /* ---------------- левая колонка ---------------- */

    private Control BuildLeft()
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(14, 12, 14, 8) };
        head.Children.Add(new Border { Width = 4, Height = 22, CornerRadius = new CornerRadius(2), Background = Pal.B("#d99b4a") });
        head.Children.Add(new TextBlock { Text = "Сцены", FontFamily = new FontFamily("Georgia"), FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Pal.B("#f6e7cf") });
        head.Children.Add(Btn("+", AddScene));
        head.Children.Add(Btn("Удалить", DeleteScene));

        _list.Background = Brushes.Transparent;
        _list.BorderThickness = new Thickness(0);
        _list.ItemTemplate = new FuncDataTemplate<Scene>((s, _) =>
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("60,*"), Margin = new Thickness(8, 5) };

            var thumb = new Border
            {
                Width = 52, Height = 36,
                CornerRadius = new CornerRadius(5),
                Background = Pal.B("#15110e"),
                BorderBrush = Pal.B("#352a21"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                ClipToBounds = true,
            };
            var img = TryImage(s.Image);
            if (img != null) thumb.Child = new Image { Source = img, Stretch = Stretch.UniformToFill };
            row.Children.Add(thumb);

            var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = s.Name, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Pal.B("#f6e7cf"), TextWrapping = TextWrapping.Wrap });
            var extra = new List<string>();
            if (s.Elements.Count > 0) extra.Add("элементов: " + s.Elements.Count);
            if (!string.IsNullOrWhiteSpace(s.Subtitle)) extra.Add(s.Subtitle);
            if (extra.Count > 0)
                info.Children.Add(new TextBlock { Text = string.Join(" · ", extra), FontSize = 10, Foreground = Pal.B("#9c8b78"), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(info, 1);
            row.Children.Add(info);
            return row;
        }, true);
        _list.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            if (_list.SelectedItem is Scene s) LoadScene(s);
        };

        var inner = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        inner.Children.Add(head);
        var scroll = new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        inner.Children.Add(scroll);

        return new Border
        {
            Background = Pal.B("#1c1714"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 14, 0),
            Child = inner,
        };
    }

    /* ---------------- правая колонка ---------------- */

    private Control BuildRight()
    {
        var root = new StackPanel { Spacing = 10 };

        var stageBorder = new Border
        {
            MaxHeight = 430,
            Background = Pal.B("#0d0b08"),
            BorderBrush = Pal.B("#4a3728"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = new Viewbox { Child = _stage, Stretch = Stretch.Uniform },
        };
        root.Children.Add(stageBorder);

        var bar = new WrapPanel();
        bar.Children.Add(Btn("Фон…", PickBackground));
        bar.Children.Add(Btn("Убрать фон", ClearBackground));
        bar.Children.Add(Btn("+ Элемент…", AddElement));
        bar.Children.Add(Btn("Картинка", SetElementImage));
        bar.Children.Add(Btn("Показать", Present, true));
        bar.Children.Add(_status);
        root.Children.Add(bar);

        root.Children.Add(BuildElementBar());

        var fields = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10 };
        fields.Children.Add(Labeled("НАЗВАНИЕ", _name));
        var sub = Labeled("ПОДЗАГОЛОВОК", _subtitle);
        Grid.SetColumn(sub, 1);
        fields.Children.Add(sub);
        root.Children.Add(fields);

        var textRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10 };
        textRow.Children.Add(Labeled("ТЕКСТ НА СЦЕНЕ", _text));
        var notes = Labeled("ЗАМЕТКИ МАСТЕРА", _notes);
        Grid.SetColumn(notes, 1);
        textRow.Children.Add(notes);
        root.Children.Add(textRow);

        return new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private Control BuildElementBar()
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _elementTitle.Text = "ЭЛЕМЕНТ";
        _elementTitle.FontSize = 9.5;
        _elementTitle.Foreground = Pal.B("#8d7a66");
        head.Children.Add(_elementTitle);
        head.Children.Add(Btn("Дублировать", DuplicateElement));
        head.Children.Add(Btn("Наверх", () => { if (_selected != null) Render(); }));
        head.Children.Add(Btn("Удалить", DeleteElement));
        _elementBar.Children.Add(head);

        var row = new WrapPanel();
        _elW = Num("Ширина, %", 2, 100, 84);
        _elH = Num("Высота, %", 2, 100, 84);
        _elRot = Num("Поворот°", -180, 180, 76);
        _elOpacity = Num("Прозрачн.", 10, 100, 84);

        row.Children.Add(Labeled("ШИРИНА, %", _elW));
        row.Children.Add(Labeled("ВЫСОТА, %", _elH));
        row.Children.Add(Labeled("ПОВОРОТ°", _elRot));
        row.Children.Add(Labeled("ПРОЗРАЧНОСТЬ, %", _elOpacity));

        _elW.ValueChanged += (_, _) => ApplyElement(applyW: true);
        _elH.ValueChanged += (_, _) => ApplyElement(applyH: true);
        _elRot.ValueChanged += (_, _) => ApplyElement(applyRot: true);
        _elOpacity.ValueChanged += (_, _) => ApplyElement(applyOpacity: true);

        _elementBar.Children.Add(row);

        _elementBarHost = new Border
        {
            Background = Pal.B("#1c1714"),
            BorderBrush = Pal.B("#2a211a"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8),
            IsVisible = false,
            Child = _elementBar,
        };
        return _elementBarHost;
    }

    private NumericUpDown Num(string hint, decimal min, decimal max, double width) => new()
    {
        Minimum = min,
        Maximum = max,
        Width = width,
        Background = Pal.B("#15110e"),
        Foreground = Pal.B("#efe3d2"),
        BorderBrush = Pal.B("#352a21"),
        FontSize = 12,
        Margin = new Thickness(0, 0, 10, 6),
    };

    private static Control Labeled(string label, Control control)
    {
        var sp = new StackPanel { Spacing = 3 };
        sp.Children.Add(new TextBlock { Text = label, FontSize = 9.5, Foreground = Pal.B("#8d7a66") });
        sp.Children.Add(control);
        return sp;
    }

    /* ---------------- поля ---------------- */

    private void BindFields()
    {
        _name.TextChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            _current.Name = _name.Text ?? "";
            Save();
            RefreshList();
            Render();
        };
        _subtitle.TextChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            _current.Subtitle = _subtitle.Text ?? "";
            Save();
            Render();
        };
        _text.TextChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            _current.Text = _text.Text ?? "";
            Save();
            Render();
        };
        _notes.TextChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            _current.Notes = _notes.Text ?? "";
            Save();
        };
    }

    private void LoadScene(Scene s)
    {
        _current = s;
        _selected = null;
        _loading = true;
        _name.Text = s.Name;
        _subtitle.Text = s.Subtitle;
        _text.Text = s.Text;
        _notes.Text = s.Notes;
        _loading = false;
        UpdateElementBar();
        Render();
    }

    /* ---------------- список ---------------- */

    private void RefreshList()
    {
        _loading = true;
        var ordered = _scenes.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        _list.ItemsSource = ordered;
        if (_current != null)
        {
            int idx = ordered.IndexOf(_current);
            if (idx >= 0) _list.SelectedIndex = idx;
        }
        _loading = false;

        if (_current == null && ordered.Count > 0)
        {
            _list.SelectedIndex = 0;
            LoadScene(ordered[0]);
        }
    }

    /// <summary>Выбрать сцену по названию (из глобального поиска).</summary>
    public void SelectByName(string name)
    {
        var s = _scenes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (s == null) return;
        _current = s;
        RefreshList();
        LoadScene(s);
    }

    private void AddScene()
    {
        var s = new Scene { Name = "Сцена " + (_scenes.Count + 1) };
        _scenes.Add(s);
        Save();
        _current = s;
        RefreshList();
        LoadScene(s);
        _status.Text = "Сцена создана";
    }

    private void DeleteScene()
    {
        if (_current == null) return;
        if (_scenes.Count <= 1) { _status.Text = "Последнюю сцену удалить нельзя"; return; }

        _scenes.Remove(_current);
        _current = null;
        _selected = null;
        Save();
        RefreshList();
    }

    /* ---------------- сцена: отрисовка ---------------- */

    private void Render()
    {
        var img = TryImage(_current?.Image);
        _bg.Background = img == null ? Pal.B("#15110e") : new ImageBrush(img) { Stretch = Stretch.UniformToFill };

        _stageSub.Text = _current?.Subtitle ?? "";
        _stageTitle.Text = _current?.Name ?? "";
        _stageText.Text = _current?.Text ?? "";

        // элементы перерисовываем
        foreach (var child in _stage.Children.OfType<Image>().ToList())
            _stage.Children.Remove(child);

        if (_current == null) return;

        foreach (var el in _current.Elements)
        {
            var e = el;
            var bmp = TryImage(e.Image);
            if (bmp == null) continue;

            var image = new Image
            {
                Source = bmp,
                Stretch = Stretch.Uniform,
                Width = StageW * e.W / 100.0,
                Height = StageH * e.H / 100.0,
                Opacity = Math.Clamp(e.Opacity, 0.1, 1),
                Cursor = new Cursor(StandardCursorType.SizeAll),
                RenderTransform = new RotateTransform(e.Rotation),
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            };
            Canvas.SetLeft(image, StageW * e.X / 100.0);
            Canvas.SetTop(image, StageH * e.Y / 100.0);

            Point dragStart = default;
            Point posStart = default;
            bool dragging = false;

            image.PointerPressed += (_, ev) =>
            {
                _selected = e;
                dragging = true;
                dragStart = ev.GetPosition(_stage);
                posStart = new Point(e.X, e.Y);
                ev.Pointer.Capture(image);
                ev.Handled = true;
                UpdateElementBar();
            };
            image.PointerMoved += (_, ev) =>
            {
                if (!dragging) return;
                var p = ev.GetPosition(_stage);
                e.X = Math.Clamp(posStart.X + (p.X - dragStart.X) / StageW * 100, -30, 100);
                e.Y = Math.Clamp(posStart.Y + (p.Y - dragStart.Y) / StageH * 100, -30, 100);
                Canvas.SetLeft(image, StageW * e.X / 100.0);
                Canvas.SetTop(image, StageH * e.Y / 100.0);
            };
            image.PointerReleased += (_, _) =>
            {
                if (!dragging) return;
                dragging = false;
                Save();
            };

            _stage.Children.Add(image);
        }
    }

    private void UpdateElementBar()
    {
        bool on = _selected != null;
        if (_elementBarHost != null) _elementBarHost.IsVisible = on;

        if (_selected == null) return;
        _loading = true;
        if (_elW != null) _elW.Value = (decimal)Math.Round(_selected.W);
        if (_elH != null) _elH.Value = (decimal)Math.Round(_selected.H);
        if (_elRot != null) _elRot.Value = (decimal)Math.Round(_selected.Rotation);
        if (_elOpacity != null) _elOpacity.Value = (decimal)Math.Round(_selected.Opacity * 100);
        _elementTitle.Text = "ЭЛЕМЕНТ: " + Path.GetFileName(_selected.Image);
        _loading = false;
    }

    private void ApplyElement(bool applyW = false, bool applyH = false, bool applyRot = false, bool applyOpacity = false)
    {
        if (_loading || _selected == null) return;
        if (applyW && _elW != null) _selected.W = Math.Clamp((double)(_elW.Value ?? 22), 2, 100);
        if (applyH && _elH != null) _selected.H = Math.Clamp((double)(_elH.Value ?? 22), 2, 100);
        if (applyRot && _elRot != null) _selected.Rotation = (double)(_elRot.Value ?? 0);
        if (applyOpacity && _elOpacity != null) _selected.Opacity = Math.Clamp((double)(_elOpacity.Value ?? 100), 10, 100) / 100.0;
        Save();
        Render();
    }

    /* ---------------- элементы ---------------- */

    private async void AddElement()
    {
        if (_current == null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        var path = await FilePickerDialog.Pick(owner, "Картинка элемента", "*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif");
        if (string.IsNullOrEmpty(path)) return;

        var el = new SceneElement { Image = path, X = 40, Y = 40, W = 22, H = 22 };
        _current.Elements.Add(el);
        _selected = el;
        Save();
        Render();
        UpdateElementBar();
        _status.Text = "Элемент добавлен — перетащите его мышью";
    }

    private async void SetElementImage()
    {
        if (_current == null || _selected == null) { _status.Text = "Сначала выберите элемент на сцене"; return; }
        var owner = TopLevel.GetTopLevel(this) as Window;
        var path = await FilePickerDialog.Pick(owner, "Картинка элемента", "*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif");
        if (string.IsNullOrEmpty(path)) return;
        _selected.Image = path;
        Save();
        Render();
        UpdateElementBar();
    }

    private void DuplicateElement()
    {
        if (_current == null || _selected == null) return;
        var copy = new SceneElement
        {
            Image = _selected.Image,
            X = Math.Clamp(_selected.X + 5, -20, 100),
            Y = Math.Clamp(_selected.Y + 5, -20, 100),
            W = _selected.W,
            H = _selected.H,
            Rotation = _selected.Rotation,
            Opacity = _selected.Opacity,
        };
        _current.Elements.Add(copy);
        _selected = copy;
        Save();
        Render();
        UpdateElementBar();
    }

    private void DeleteElement()
    {
        if (_current == null || _selected == null) { _status.Text = "Сначала выберите элемент на сцене"; return; }
        _current.Elements.RemoveAll(e => e.Id == _selected.Id);
        _selected = null;
        Save();
        Render();
        UpdateElementBar();
        _status.Text = "Элемент удалён";
    }

    /* ---------------- фон ---------------- */

    private async void PickBackground()
    {
        if (_current == null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        var path = await FilePickerDialog.Pick(owner, "Фон сцены", "*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif");
        if (string.IsNullOrEmpty(path)) return;
        _current.Image = path;
        Save();
        Render();
        RefreshList();
        _status.Text = "Фон: " + Path.GetFileName(path);
    }

    private void ClearBackground()
    {
        if (_current == null) return;
        _current.Image = "";
        Save();
        Render();
        RefreshList();
        _status.Text = "Фон убран";
    }

    /* ---------------- показ ---------------- */

    private void Present()
    {
        if (_current == null) return;
        try
        {
            if (Live.EnsureWindow())
            {
                Live.ShowScene(_current);
                _status.Text = "Сцена показана на втором экране";
                return;
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Не удалось открыть экран игроков: " + ex.Message;
            return;
        }

        // резервный вариант — отдельное окно
        var canvas = new Canvas { Width = StageW, Height = StageH };
        var bg = new Border { Width = StageW, Height = StageH };
        var img = TryImage(_current.Image);
        bg.Background = img == null ? Brushes.Black : new ImageBrush(img) { Stretch = Stretch.UniformToFill };
        canvas.Children.Add(bg);

        foreach (var el in _current.Elements)
        {
            var bmp = TryImage(el.Image);
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

        var sub = new TextBlock { Text = _current.Subtitle, FontSize = 20, Foreground = Pal.B("#f0c27b") };
        Canvas.SetLeft(sub, 70); Canvas.SetTop(sub, 460);
        canvas.Children.Add(sub);

        var title = new TextBlock { Text = _current.Name, FontFamily = new FontFamily("Georgia"), FontSize = 52, FontWeight = FontWeight.Bold, Foreground = Pal.B("#f6e7cf") };
        Canvas.SetLeft(title, 70); Canvas.SetTop(title, 492);
        canvas.Children.Add(title);

        var text = new TextBlock { Text = _current.Text, FontSize = 20, Foreground = Pal.B("#c9b8a4"), MaxWidth = 900, TextWrapping = TextWrapping.Wrap };
        Canvas.SetLeft(text, 70); Canvas.SetTop(text, 572);
        canvas.Children.Add(text);

        new Window
        {
            Title = _current.Name,
            Background = Brushes.Black,
            WindowState = WindowState.Maximized,
            Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform },
        }.Show();
    }

    /* ---------------- хелперы ---------------- */

    private static Bitmap? TryImage(string? path) => ImageCache.Get(path, 1280);

    private void Save() => SceneStore.Save(_scenes);

    private static TextBox Field(string watermark) => new()
    {
        Watermark = watermark,
        Background = Pal.B("#15110e"),
        Foreground = Pal.B("#efe3d2"),
        BorderBrush = Pal.B("#352a21"),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10, 6),
        FontSize = 12.5,
    };

    private static TextBox Area(string watermark, double height) => new()
    {
        Watermark = watermark,
        Height = height,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        Background = Pal.B("#15110e"),
        Foreground = Pal.B("#efe3d2"),
        BorderBrush = Pal.B("#352a21"),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10, 6),
        FontSize = 12.5,
    };

    private static Button Btn(string text, Action onClick, bool accent = false)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(12, 7),
            Margin = new Thickness(0, 0, 8, 6),
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
