using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public partial class MapPage : UserControl
{
    private MapBook _book;
    private MapDoc _doc;
    private MapView _view;
    private readonly List<string> _undo = new();
    public Action<string, string>? OpenSheet;

    private bool _syncing;

    private static readonly string[] TokenColors = { "#d99b4a", "#7c2a2c", "#70a3a2", "#7fa66e", "#8a6fa8", "#8d7a66", "#c9a227" };

    public MapPage()
    {
        _book = MapStore.Load();
        _doc = _book.Maps[_book.Active];
        _view = new MapView(_doc) { DoubleClick = () => OpenCurrent() };
        InitializeComponent();

        if (Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_PREVIEW") == "1")
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var path = System.IO.Path.Combine(AppContext.BaseDirectory, "preview_test.png");
                    if (!System.IO.File.Exists(path)) path = "";
                    var bmp = string.IsNullOrEmpty(path) ? null : ImageCache.Get(path, 4000);
                    if (bmp != null)
                        new BackgroundPreviewDialog(path, ImageSize(path), _doc).Show();
                }
                catch { }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        if (Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_UNDO") == "1")
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    int before = _doc.Props.Count;
                    Snapshot();
                    _doc.Props.Add(new Prop { Name = "Тест отмены", X = 2, Y = 2, Cells = 1 });
                    UndoLast();
                    int after = _doc.Props.Count;
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(AppContext.BaseDirectory, "undo_test.txt"),
                        $"props: {before} -> после отмены {after}; ok={before == after}");
                }
                catch (Exception ex)
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(AppContext.BaseDirectory, "undo_test.txt"), "ошибка: " + ex.Message);
                }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        if (Environment.GetEnvironmentVariable("EMBERKEEPER_TEST_BATTLE") == "1")
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var order = _doc.Tokens.Select(t => new Combatant
                    {
                        Name = t.Name,
                        Init = t.Init,
                        Hp = t.Hp,
                        MaxHp = t.MaxHp,
                        Ac = t.Ac,
                        IsPc = t.Type == "player",
                    }).OrderByDescending(c => c.Init).ToList();
                    BattleState.Set(order, 1, 3);
                    _view.ActiveName = BattleState.CurrentName;
                    _view.InvalidateVisual();
                    OpenPlayerWindow();
                }
                catch { }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        CbProp.ItemsSource = new[] { "Сундук", "Дверь", "Лодка", "Костёр", "Статуя", "Дерево", "Кровать", "Гроб" };
        CbProp.SelectedIndex = 0;

        if (Environment.GetEnvironmentVariable("EMBERKEEPER_MAP_PLAYER") == "1")
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try { OpenPlayerWindow(); } catch { }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
        Live.AttachDoc(_doc);
        _view.Save = SaveAll;
        _view.BeforeChange = Snapshot;
        _view.Undo = UndoLast;
        _view.Changed += Sync;
        BattleState.Changed += OnBattleChanged;
        OnBattleChanged();

        BuildColorPalette();
        RefreshLocations();
        Host.Content = _view;
        Loaded += (_, _) => _view.ResetView(_view.Bounds.Width, _view.Bounds.Height);
        Sync();
        SaveAll();
    }

    /// <summary>Снимок текущей карты для отмены (Ctrl+Z).</summary>
    private void Snapshot()
    {
        try
        {
            _undo.Add(System.Text.Json.JsonSerializer.Serialize(_doc));
            while (_undo.Count > 40) _undo.RemoveAt(0);
        }
        catch { }
    }

    private void UndoLast()
    {
        if (_undo.Count == 0) return;
        var json = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        try
        {
            var d = System.Text.Json.JsonSerializer.Deserialize<MapDoc>(json);
            if (d == null) return;
            _doc.Cols = d.Cols;
            _doc.Rows = d.Rows;
            _doc.GridSize = d.GridSize;
            _doc.FeetPerCell = d.FeetPerCell;
            _doc.Tokens = d.Tokens ?? new List<Token>();
            _doc.Walls = d.Walls ?? new List<Wall>();
            _doc.Props = d.Props ?? new List<Prop>();
            _doc.Templates = d.Templates ?? new List<Template>();
            _doc.Fog = d.Fog ?? new List<bool>();
            _doc.Explored = d.Explored ?? new List<bool>();
            _doc.GridVisible = d.GridVisible;
            _doc.Snap = d.Snap;
            _doc.GridShape = d.GridShape;
            _doc.GridOffX = d.GridOffX;
            _doc.GridOffY = d.GridOffY;
            _doc.FogOn = d.FogOn;
            _doc.VisionMode = d.VisionMode;
            _doc.Background = d.Background;
            _doc.EnsureVision();
            _view.SetSelected(null);
            SaveAll();
            Sync();
            _view.InvalidateVisual();
        }
        catch { }
    }

    private void SaveAll()
    {
        MapStore.Save(_book);
        Live.RaiseMapChanged();
    }

    private void OnBattleChanged()
    {
        if (_view == null) return;
        _view.ActiveName = BattleState.CurrentName;
        _view.InvalidateVisual();
    }

    /* ---------------- палитра цветов токена ---------------- */

    private void BuildColorPalette()
    {
        ColorHost.Children.Clear();
        foreach (var c in TokenColors)
        {
            var hex = c;
            var b = new Button
            {
                Width = 26, Height = 26,
                Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.Parse(hex)),
                BorderBrush = new SolidColorBrush(Color.Parse("#4a3728")),
                BorderThickness = new Thickness(1),
            };
            b.Click += (_, _) =>
            {
                if (_view.Selected == null) return;
                _view.Selected.Color = hex;
                SaveAll();
                _view.InvalidateVisual();
            };
            ColorHost.Children.Add(b);
        }
    }

    /* ---------------- локации ---------------- */

    private void RefreshLocations()
    {
        _syncing = true;
        LocCombo.ItemsSource = _book.Maps.Select(m => m.Name).ToList();
        LocCombo.SelectedIndex = _book.Active;
        _syncing = false;
    }

    private void BindMap(MapDoc doc)
    {
        if (_view != null) _view.Changed -= Sync;

        _doc = doc;
        var tool = _view?.Tool ?? "select";
        var tpl = _view?.TplKind ?? "circle";
        var prop = _view?.PropIcon ?? "box";

        var view = new MapView(doc) { DoubleClick = () => OpenCurrent() };
        view.Save = SaveAll;
        view.BeforeChange = Snapshot;
        view.Undo = UndoLast;
        view.FogBrush = Math.Clamp((int)(EdFogBrush?.Value ?? 1), 1, 6);
        view.Changed += Sync;
        view.Tool = tool;
        view.TplKind = tpl;
        view.PropIcon = prop;

        Live.AttachDoc(doc);
        _view = view;
        Host.Content = view;
        view.ResetView(Math.Max(200, view.Bounds.Width), Math.Max(200, view.Bounds.Height));
        view.ActiveName = BattleState.CurrentName;
        Sync();
        view.InvalidateVisual();
    }

    private void OnLocationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || LocCombo.SelectedIndex < 0) return;
        if (LocCombo.SelectedIndex == _book.Active && _view != null) return;

        SaveAll();
        _book.Active = LocCombo.SelectedIndex;
        BindMap(_book.Maps[_book.Active]);
        SaveAll();
    }

    private void OnAddLocation(object? sender, RoutedEventArgs e)
    {
        SaveAll();
        var doc = MapStore.NewMap("Локация " + (_book.Maps.Count + 1));
        _book.Maps.Add(doc);
        _book.Active = _book.Maps.Count - 1;
        RefreshLocations();
        BindMap(doc);
        SaveAll();
    }

    private async void OnDeleteLocation(object? sender, RoutedEventArgs e)
    {
        if (_book.Maps.Count <= 1) { Notify("Нельзя удалить последнюю локацию."); return; }
        if (!await Confirm($"Удалить локацию «{_doc.Name}»?")) return;

        _book.Maps.RemoveAt(_book.Active);
        if (_book.Active >= _book.Maps.Count) _book.Active = _book.Maps.Count - 1;
        RefreshLocations();
        BindMap(_book.Maps[_book.Active]);
        SaveAll();
    }

    private async void OnRenameLocation(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var name = await Prompt("Название локации", _doc.Name, owner);
        if (string.IsNullOrWhiteSpace(name)) return;
        _doc.Name = name.Trim();
        RefreshLocations();
        SaveAll();
    }

    /* ---------------- синхронизация панели ---------------- */

    private void Sync()
    {
        _syncing = true;
        var sel = _view.Selected;

        TokenList.ItemsSource = _doc.Tokens.ToList();
        TokenList.ItemTemplate = new FuncDataTemplate<Token>((t, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(8, 6) };
            var dot = new Border
            {
                Width = 14, Height = 14, CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.Parse(t.Color)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            grid.Children.Add(dot);

            var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = t.Name, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Pal.B("#f6e7cf") });
            info.Children.Add(new TextBlock { Text = $"{t.Hp}/{t.MaxHp} · {TypeLabel(t.Type)}", FontSize = 10.5, Foreground = Pal.B("#9c8b78") });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            var hp = new TextBlock { Text = t.Hp.ToString(), FontSize = 11, Foreground = Pal.B("#c8b49b"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hp, 2);
            grid.Children.Add(hp);
            return grid;
        }, true);

        // карта
        EdGridPx.Value = _doc.GridSize;
        EdFeet.Value = _doc.FeetPerCell;
        EdGridType.SelectedIndex = _doc.GridShape == "hex" ? 1 : 0;
        CbGridVisible.IsChecked = _doc.GridVisible;
        CbSnap.IsChecked = _doc.Snap;
        CbFog.IsChecked = _doc.FogOn;
        CbVisionMode.IsChecked = _doc.VisionMode;
        if (CbLayerWalls != null)
        {
            CbLayerWalls.IsChecked = _doc.ShowWalls;
            CbLayerProps.IsChecked = _doc.ShowProps;
            CbLayerTemplates.IsChecked = _doc.ShowTemplates;
            CbLayerTokens.IsChecked = _doc.ShowTokens;
        }
        EdGridOffX.Value = (decimal)_doc.GridOffX;
        EdGridOffY.Value = (decimal)_doc.GridOffY;

        // токен
        if (sel != null)
        {
            EdName.Text = sel.Name;
            EdSize.Value = sel.Cells;
            EdHp.Value = Math.Clamp(sel.Hp, -999, 9999);
            EdMaxHp.Value = Math.Clamp(Math.Max(sel.MaxHp, 1), 1, 9999);
            CbAura.IsChecked = sel.AuraOn;
            EdAuraFt.Value = Math.Clamp((int)Math.Round(sel.AuraFt), 5, 200);
            CbLight.IsChecked = sel.LightOn;
            EdLightFt.Value = Math.Clamp((int)Math.Round(sel.LightFt), 5, 200);
            EdVisionFt.Value = (decimal)Math.Clamp(sel.VisionFt, 0, 300);
            CbHidden.IsChecked = sel.Hidden;
            EdType.SelectedIndex = sel.Type switch { "player" => 0, "npc" => 1, _ => 2 };
            EdName.IsEnabled = true;
            EdSize.IsEnabled = true;
            EdHp.IsEnabled = true;
            EdMaxHp.IsEnabled = true;
            CbAura.IsEnabled = true;
            EdAuraFt.IsEnabled = true;
            CbLight.IsEnabled = true;
            EdLightFt.IsEnabled = true;
            EdVisionFt.IsEnabled = true;
            CbHidden.IsEnabled = true;
            EdType.IsEnabled = true;
            UpdateOpenButton();
        }
        else
        {
            EdName.IsEnabled = false;
            EdSize.IsEnabled = false;
            EdHp.IsEnabled = false;
            EdMaxHp.IsEnabled = false;
            CbAura.IsEnabled = false;
            EdAuraFt.IsEnabled = false;
            CbLight.IsEnabled = false;
            EdLightFt.IsEnabled = false;
            EdVisionFt.IsEnabled = false;
            CbHidden.IsEnabled = false;
            EdType.IsEnabled = false;
            OpenBtn.Content = "Выберите токен";
        }
        _syncing = false;
    }

    private static string TypeLabel(string t) => t switch { "player" => "игрок", "npc" => "НПС", _ => "моб" };

    private void UpdateOpenButton()
    {
        var t = _view.Selected;
        if (t == null) { OpenBtn.Content = "Выберите токен"; return; }
        OpenBtn.Content = t.Type switch
        {
            "player" => "Открыть лист героя",
            "npc" => "Открыть лист НПС",
            _ => "Открыть бестиарий",
        };
    }

    /* ---------------- свойства токена ---------------- */

    private void OnNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.Name = EdName.Text ?? "";
        SaveAll();
        _view.InvalidateVisual();
        RefreshList();
    }

    private void OnSizeChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.Cells = (int)(EdSize.Value ?? 1);
        SaveAll();
        _view.InvalidateVisual();
        RefreshList();
    }

    private void OnHpChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.Hp = (int)(EdHp.Value ?? 0);
        SaveAll();
        RefreshList();
    }

    private void OnMaxHpChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.MaxHp = (int)(EdMaxHp.Value ?? 1);
        SaveAll();
        RefreshList();
    }

    private void OnAuraChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.AuraOn = CbAura.IsChecked == true;
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnAuraFtChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.AuraFt = (double)(EdAuraFt.Value ?? 10);
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnLightChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.LightOn = CbLight.IsChecked == true;
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnVisionFtChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.VisionFt = Math.Max(0, (double)(EdVisionFt.Value ?? 0));
        SaveAll();
    }

    private void OnRevealAll(object? sender, RoutedEventArgs e)
    {
        for (int c = 0; c < _doc.Cols; c++)
            for (int r = 0; r < _doc.Rows; r++) _doc.SetFog(c, r, true);
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnHideAll(object? sender, RoutedEventArgs e)
    {
        for (int c = 0; c < _doc.Cols; c++)
            for (int r = 0; r < _doc.Rows; r++) _doc.SetFog(c, r, false);
        _doc.ClearExplored();
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnLightFtChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.LightFt = (double)(EdLightFt.Value ?? 30);
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnHiddenChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _view.Selected == null) return;
        _view.Selected.Hidden = CbHidden.IsChecked == true;
        SaveAll();
    }

    private void OnTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _view.Selected == null || EdType.SelectedItem is not ComboBoxItem it) return;
        _view.Selected.Type = it.Tag?.ToString() ?? "mob";
        SaveAll();
        UpdateOpenButton();
        RefreshList();
    }

    /* ---------------- настройки карты ---------------- */

    private void OnGridPxChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing) return;
        _doc.GridSize = (int)(EdGridPx.Value ?? 64);
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnFeetChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing) return;
        _doc.FeetPerCell = (int)(EdFeet.Value ?? 5);
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnGridTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || sender is not ComboBox cb || cb.SelectedItem is not ComboBoxItem it) return;
        _doc.GridShape = it.Tag?.ToString() ?? "square";
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnGridVisibleChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _doc.GridVisible = CbGridVisible.IsChecked == true;
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnSnapChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _doc.Snap = CbSnap.IsChecked == true;
        SaveAll();
    }

    private void OnFogChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _doc.FogOn = CbFog.IsChecked == true;
        SaveAll();
        _view.InvalidateVisual();
    }

    /* ---------------- инструменты ---------------- */

    private void OnPropPick(object? sender, SelectionChangedEventArgs e)
    {
        if (_view == null) return;
        _view.PropIcon = CbProp.SelectedIndex switch
        {
            0 => "box",
            1 => "door",
            2 => "boat",
            3 => "fire",
            4 => "statue",
            5 => "tree",
            6 => "bed",
            7 => "coffin",
            _ => "box",
        };
        _view.Tool = "prop";
    }

    private void OnLayerToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _doc.ShowWalls = CbLayerWalls.IsChecked == true;
        _doc.ShowProps = CbLayerProps.IsChecked == true;
        _doc.ShowTemplates = CbLayerTemplates.IsChecked == true;
        _doc.ShowTokens = CbLayerTokens.IsChecked == true;
        SaveAll();
        _view?.InvalidateVisual();
    }

    private void OnVisionModeChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _doc.VisionMode = CbVisionMode.IsChecked == true;
        SaveAll();
        _view?.InvalidateVisual();
    }

    private void OnTool(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tool) return;
        _view.Tool = tool;
        ToolSelect.Classes.Set("on", tool == "select");
        ToolWall.Classes.Set("on", tool == "wall");
        ToolFog.Classes.Set("on", tool == "fog");
        ToolTemplate.Classes.Set("on", tool == "template");
        ToolProp.Classes.Set("on", tool == "prop");
        ToolRuler.Classes.Set("on", tool == "ruler");
    }

    private void OnAddToken(object? sender, RoutedEventArgs e)
    {
        var t = new Token
        {
            Name = "Новый",
            X = 5 + _doc.Tokens.Count % 10,
            Y = 5 + _doc.Tokens.Count / 10,
            Color = "#70a3a2",
        };
        _doc.Tokens.Add(t);
        SaveAll();
        _view.SetSelected(t);
        Sync();
        _view.InvalidateVisual();
    }

    private async void OnTokenImage(object? sender, RoutedEventArgs e)
    {
        if (_view?.Selected == null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        var path = await FilePickerDialog.Pick(owner, "Картинка токена", "*.png;*.jpg;*.jpeg;*.bmp;*.webp");
        if (string.IsNullOrEmpty(path)) return;
        _view.Selected.ImagePath = path;
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnRotateToken(object? sender, RoutedEventArgs e)
    {
        if (_view?.Selected == null) return;
        _view.Selected.Rotation = (_view.Selected.Rotation + 90) % 360;
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnFlipTokenX(object? sender, RoutedEventArgs e)
    {
        if (_view?.Selected == null) return;
        _view.Selected.FlipX = !_view.Selected.FlipX;
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnFlipTokenY(object? sender, RoutedEventArgs e)
    {
        if (_view?.Selected == null) return;
        _view.Selected.FlipY = !_view.Selected.FlipY;
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnDeleteToken(object? sender, RoutedEventArgs e)
    {
        if (_view.Selected == null) return;
        _doc.Tokens.Remove(_view.Selected);
        _view.SetSelected(null);
        SaveAll();
        Sync();
        _view.InvalidateVisual();
    }

    private void OnTokenSelect(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        if (TokenList.SelectedItem is Token t)
        {
            _view.SetSelected(t);
            _view.InvalidateVisual();
            Sync();
        }
    }

    private void RefreshList() => TokenList.ItemsSource = _doc.Tokens.ToList();

    private void OnFogBrushChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        _view.FogBrush = Math.Clamp((int)(EdFogBrush.Value ?? 1), 1, 6);
    }

    private void OnGridOffX(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing) return;
        _doc.GridOffX = (double)(EdGridOffX.Value ?? 0);
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnGridOffY(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncing) return;
        _doc.GridOffY = (double)(EdGridOffY.Value ?? 0);
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnUndoWall(object? sender, RoutedEventArgs e)
    {
        if (_doc.Walls.Count == 0) return;
        _doc.Walls.RemoveAt(_doc.Walls.Count - 1);
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnClearWalls(object? sender, RoutedEventArgs e)
    {
        _doc.Walls.Clear();
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnClearTemplates(object? sender, RoutedEventArgs e)
    {
        _doc.Templates.Clear();
        SaveAll();
        _view.InvalidateVisual();
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e) => _view.ZoomBy(1.15, new Point(_view.Bounds.Width / 2, _view.Bounds.Height / 2));
    private void OnZoomOut(object? sender, RoutedEventArgs e) => _view.ZoomBy(1 / 1.15, new Point(_view.Bounds.Width / 2, _view.Bounds.Height / 2));

    private void OnFit(object? sender, RoutedEventArgs e)
    {
        if (_view.Bounds.Width > 1 && _view.Bounds.Height > 1)
            _view.ResetView(_view.Bounds.Width, _view.Bounds.Height);
    }

    /* ---------------- открытие листов ---------------- */

    private void OnOpenSheet(object? sender, RoutedEventArgs e) => OpenCurrent();

    private void OpenCurrent()
    {
        var t = _view.Selected;
        if (t == null) return;
        if (t.Type == "mob")
        {
            var sheet = new MobSheet(t, () =>
            {
                SaveAll();
                _view.InvalidateVisual();
                Sync();
            });
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null) sheet.ShowDialog(owner); else sheet.Show();
            return;
        }
        OpenSheet?.Invoke(t.Type, t.Ref.Length > 0 ? t.Ref : t.Name);
    }

    public void SelectToken(string name)
    {
        var t = _doc.Tokens.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (t == null) return;
        _view.SetSelected(t);
        Sync();
        _view.InvalidateVisual();
    }

    public void AddHero(Hero h)
    {
        var existing = _doc.Tokens.FirstOrDefault(t =>
            t.Type == "player" && (t.Ref == h.Id || string.Equals(t.Name, h.Name, StringComparison.OrdinalIgnoreCase)));
        if (existing != null)
        {
            _view.SetSelected(existing);
            Sync();
            _view.InvalidateVisual();
            return;
        }

        var token = new Token
        {
            Name = h.Name,
            Type = "player",
            Ref = h.Id,
            Color = "#d99b4a",
            X = 5 + _doc.Tokens.Count % 10,
            Y = 5 + _doc.Tokens.Count / 10,
            Hp = h.Hp,
            MaxHp = h.MaxHp,
            Ac = h.Ac,
            Init = h.Init,
            VisionFt = h.Darkvision,
            Notes = string.Join(" · ", new[] { h.Class, h.Race }.Where(x => !string.IsNullOrWhiteSpace(x))),
        };
        _doc.Tokens.Add(token);
        SaveAll();
        _view.SetSelected(token);
        Sync();
        _view.InvalidateVisual();
    }

    public void AddFromBestiary(string name, string cr, string mtype)
    {
        var t = new Token
        {
            Name = name,
            Type = "mob",
            Ref = name,
            Color = "#7c2a2c",
            X = 5 + _doc.Tokens.Count % 10,
            Y = 5 + _doc.Tokens.Count / 10,
            Notes = (string.IsNullOrEmpty(cr) ? "" : "Опасность: " + cr + "\n") + (string.IsNullOrEmpty(mtype) ? "" : "Тип: " + mtype),
        };
        _doc.Tokens.Add(t);
        SaveAll();
        _view.SetSelected(t);
        Sync();
        _view.InvalidateVisual();
    }

    /// <summary>Ставит на текущую карту всех противников из генератора стычек.</summary>
    public void PlaceEncounter(EncounterSpec enc)
    {
        int total = 0;
        foreach (var g in enc.Groups) total += g.Count;
        if (total == 0) return;

        int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(total)));
        int startX = Math.Max(1, _doc.Cols / 2 - cols / 2);
        int startY = Math.Max(1, _doc.Rows / 2 - Math.Max(1, total / cols) / 2);
        int idx = 0;
        var notes = "Стычка: " + enc.Terrain + (string.IsNullOrWhiteSpace(enc.Activity) ? "" : "\n" + enc.Activity)
                    + (string.IsNullOrWhiteSpace(enc.Twist) ? "" : "\n" + enc.Twist);

        foreach (var g in enc.Groups)
        {
            for (int i = 0; i < g.Count; i++)
            {
                _doc.Tokens.Add(new Token
                {
                    Name = g.Count > 1 ? $"{g.Name} {i + 1}" : g.Name,
                    Type = "mob",
                    Ref = g.Name,
                    Color = g.Color,
                    X = startX + idx % cols,
                    Y = startY + idx / cols,
                    Hp = g.Hp,
                    MaxHp = g.Hp,
                    Ac = g.Ac,
                    Notes = notes,
                });
                idx++;
            }
        }

        SaveAll();
        Sync();
        _view.InvalidateVisual();
    }

    /* ---------------- фон / экран игроков ---------------- */

    private async void OnBackground(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var path = await FilePickerDialog.Pick(owner, "Выбрать фон карты", "*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif");
        if (string.IsNullOrEmpty(path)) return;

        PixelSize orig = ImageSize(path);
        if (ImageCache.Get(path, 4000) != null)
        {
            var dlg = new BackgroundPreviewDialog(path, orig, _doc);
            bool ok = owner != null ? await dlg.ShowDialog<bool>(owner) : false;
            if (!ok) return;
            if (dlg.FitMap) FitMapToImage(orig);
        }

        _view.SetBackground(path);
        SaveAll();
        Sync();
        _view.InvalidateVisual();
    }

    /// <summary>Настоящий размер картинки в пикселях (не масштабированный).</summary>
    private static PixelSize ImageSize(string path)
    {
        try
        {
            using var probe = new Avalonia.Media.Imaging.Bitmap(path);
            return probe.PixelSize;
        }
        catch { return new PixelSize(1280, 720); }
    }

    /// <summary>Размер карты под картинку: клетки считаются от её пикселей.</summary>
    private void FitMapToImage(PixelSize size)
    {
        int cell = Math.Max(32, _doc.GridSize);
        int cols = Math.Clamp((int)Math.Round(size.Width / (double)cell), 5, 300);
        int rows = Math.Clamp((int)Math.Round(size.Height / (double)cell), 5, 300);
        _doc.Cols = cols;
        _doc.Rows = rows;
        _doc.EnsureVision();

        double w = _view.Bounds.Width > 10 ? _view.Bounds.Width : 900;
        double h = _view.Bounds.Height > 10 ? _view.Bounds.Height : 600;
        _view.ResetView(w, h);
    }

    private void OnClearBackground(object? sender, RoutedEventArgs e)
    {
        _view.SetBackground("");
        SaveAll();
    }

    public void ShowPlayerView() => OpenPlayerWindow();

    private void OnPlayerView(object? sender, RoutedEventArgs e) => OpenPlayerWindow();

    private void OpenPlayerWindow()
    {
        Live.AttachDoc(_doc);
        if (Live.EnsureWindow()) Live.ShowMap();
    }

    /* ---------------- диалоги ---------------- */

    private void Notify(string text)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var win = DialogBase("Сообщение", 380, 170);
        var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 16, 16, 8), Foreground = Pal.B("#efe3d2") };
        var ok = new Button { Content = "ОК", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 16, 12), Classes = { "goldbtn" } };
        ok.Click += (_, _) => win.Close();
        var sp = new StackPanel();
        sp.Children.Add(tb);
        sp.Children.Add(ok);
        win.Content = sp;
        if (owner != null) win.ShowDialog(owner); else win.Show();
    }

    private async System.Threading.Tasks.Task<bool> Confirm(string text)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        bool result = false;
        var win = DialogBase("Подтверждение", 380, 170);
        var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 16, 16, 8), Foreground = Pal.B("#efe3d2") };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 16, 12), Spacing = 8 };
        var yes = new Button { Content = "Удалить", Classes = { "goldbtn" } };
        var no = new Button { Content = "Отмена" };
        yes.Click += (_, _) => { result = true; win.Close(); };
        no.Click += (_, _) => { result = false; win.Close(); };
        row.Children.Add(no);
        row.Children.Add(yes);
        var sp = new StackPanel();
        sp.Children.Add(tb);
        sp.Children.Add(row);
        win.Content = sp;
        if (owner != null) await win.ShowDialog(owner); else win.Show();
        return result;
    }

    private async System.Threading.Tasks.Task<string?> Prompt(string title, string initial, Window? owner)
    {
        var win = DialogBase(title, 400, 180);
        var box = new TextBox
        {
            Text = initial, Margin = new Thickness(16, 16, 16, 8),
            Background = Pal.B("#1c1714"), Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 7),
        };
        var ok = new Button { Content = "ОК", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 16, 14), Classes = { "goldbtn" } };
        string? result = null;
        ok.Click += (_, _) => { result = box.Text; win.Close(); };
        var sp = new StackPanel();
        sp.Children.Add(box);
        sp.Children.Add(ok);
        win.Content = sp;
        if (owner != null) await win.ShowDialog(owner); else win.Show();
        return result;
    }

    private static Window DialogBase(string title, double w, double h) => new()
    {
        Title = title,
        Width = w,
        Height = h,
        Background = Pal.B("#100d0b"),
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };
}
