using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace EmberKeeper;

public sealed class Token
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Токен";
    public double X { get; set; }
    public double Y { get; set; }
    public int Cells { get; set; } = 1;
    public string Color { get; set; } = "#d99b4a";
    public string Type { get; set; } = "mob";
    public string Ref { get; set; } = "";
    public bool AuraOn { get; set; }
    public double AuraFt { get; set; } = 10;
    public string AuraColor { get; set; } = "#70a3a2";
    public bool LightOn { get; set; }
    public double LightFt { get; set; } = 30;
    public int Hp { get; set; } = 10;
    public int MaxHp { get; set; } = 10;
    public int Init { get; set; }
    public int Ac { get; set; }
    public string Speed { get; set; } = "";
    public string Notes { get; set; } = "";
    /// <summary>Токен не виден на экране игроков.</summary>
    public bool Hidden { get; set; }
}

public sealed class Wall
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string Kind { get; set; } = "wall";
    public bool Open { get; set; }
}

public sealed class Template
{
    public string Kind { get; set; } = "circle"; // circle | cone | line | square
    public double X { get; set; }
    public double Y { get; set; }
    public double SizeFt { get; set; } = 30;
    public double Angle { get; set; }
}

public sealed class Prop
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Cells { get; set; } = 1;
    public string Icon { get; set; } = "box";
    public string Name { get; set; } = "Сундук";
}

public sealed class MapDoc
{
    public string Name { get; set; } = "Локация";
    public int GridSize { get; set; } = 64;
    public int FeetPerCell { get; set; } = 5;
    public int Cols { get; set; } = 30;
    public int Rows { get; set; } = 20;
    public List<Token> Tokens { get; set; } = new();
    public List<Wall> Walls { get; set; } = new();
    public List<Template> Templates { get; set; } = new();
    public List<Prop> Props { get; set; } = new();
    public bool FogOn { get; set; } = true;
    public List<bool> Fog { get; set; } = new();
    public string Background { get; set; } = "";
    public string GridShape { get; set; } = "square";
    public bool GridVisible { get; set; } = true;
    public bool Snap { get; set; } = true;

    public static string Path => AppPaths.File("map.json");

    public static MapDoc Load()
    {
        MapDoc d;
        try
        {
            if (File.Exists(Path))
                d = JsonSerializer.Deserialize<MapDoc>(File.ReadAllText(Path)) ?? new MapDoc();
            else
                d = new MapDoc();
        }
        catch { d = new MapDoc(); }

        if (d.Tokens.Count == 0)
        {
            d.Tokens.Add(new Token { Name = "Герой", X = 4, Y = 4, Color = "#d99b4a", Type = "player", Ref = "hero" });
            d.Tokens.Add(new Token { Name = "Гоблин", X = 9, Y = 5, Color = "#7c2a2c", Hp = 7, MaxHp = 7, Init = 12, Type = "mob", Ref = "goblin" });
            d.Tokens.Add(new Token { Name = "Гоблин", X = 9, Y = 7, Color = "#7c2a2c", Hp = 7, MaxHp = 7, Init = 9, Type = "mob", Ref = "goblin" });
        }

        int n = d.Cols * d.Rows;
        if (d.Fog == null || d.Fog.Count != n)
        {
            d.Fog = new List<bool>(new bool[n]);
            for (int i = 0; i < n; i++) d.Fog[i] = true;
        }
        return d;
    }

    public bool FogAt(int col, int row)
    {
        int idx = row * Cols + col;
        if (Fog == null || idx < 0 || idx >= Fog.Count) return true;
        return Fog[idx];
    }

    public void SetFog(int col, int row, bool visible)
    {
        int idx = row * Cols + col;
        if (Fog == null || idx < 0 || idx >= Fog.Count) return;
        Fog[idx] = visible;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public sealed class MapView : Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>Карта рисуется вручную, поэтому попадание по ней считаем всегда внутри границ.</summary>
    public bool HitTest(Point point) => true;

    public MapDoc Doc { get; }
    public double Zoom { get; private set; } = 1;
    public string Tool { get; set; } = "select";
    public Token? Selected { get; private set; }
    public Action? Changed;
    public Action? Save;
    public Action? DoubleClick;

    private Point _off = new(40, 40);
    private bool _panning;
    private Point _panStart;
    private Point _offStart;
    private Token? _drag;
    private Point _dragStart;
    private Point _dragTokenStart;
    private Point? _wallStart;
    private DateTime _lastClick = DateTime.MinValue;
    private Point _lastClickPos;
    private Token? _lastClickToken;
    private Avalonia.Media.Imaging.Bitmap? _bg;
    private string _bgPath = "";

    public string TplKind { get; set; } = "circle";

    /// <summary>Имя токена, который сейчас ходит (подсвечивается золотым ореолом).</summary>
    public string? ActiveName { get; set; }

    /// <summary>Экран игроков: скрытые токены не рисуются.</summary>
    public bool PlayerMode { get; set; }
    public double TplAngle { get; set; }
    public string PropIcon { get; set; } = "box";
    private Point? _tplStart;
    private Point? _rulerStart;
    private Point _rulerEnd;
    private bool _fogErase;
    private Prop? _propHit;
    private Point _propDragStart;
    private Point _propTokenStart;

    public MapView(MapDoc doc)
    {
        Doc = doc;
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public void SetSelected(Token? t)
    {
        Selected = t;
        InvalidateVisual();
    }

    public void SetBackground(string path)
    {
        Doc.Background = path;
        _bgPath = path;
        ImageCache.Forget(path ?? "");
        _bg = ImageCache.Get(path, 2400);
        Save?.Invoke();
        InvalidateVisual();
    }

    public void ZoomBy(double factor, Point? center = null)
    {
        double old = Zoom;
        Zoom = Math.Clamp(Zoom * factor, 0.25, 3.0);
        if (center is Point c)
        {
            _off = new Point(c.X - (c.X - _off.X) * (Zoom / old), c.Y - (c.Y - _off.Y) * (Zoom / old));
        }
        InvalidateVisual();
    }

    public void ResetView(double w, double h)
    {
        double grid = Doc.GridSize;
        double fit = Math.Min(w / (Doc.Cols * grid + 80), h / (Doc.Rows * grid + 80));
        Zoom = Math.Clamp(fit, 0.25, 3.0);
        _off = new Point((w - Doc.Cols * grid * Zoom) / 2, (h - Doc.Rows * grid * Zoom) / 2);
        InvalidateVisual();
    }

    private static Color Alpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    // ВАЖНО: и ToMap, и ToScreen работают в КЛЕТКАХ (1 = длина клетки),
    // поэтому все объекты карты (токены, стены, пропсы, шаблоны, туман) живут в одной системе координат.
    private Point ToMap(Point p) => new((p.X - _off.X) / (Doc.GridSize * Zoom), (p.Y - _off.Y) / (Doc.GridSize * Zoom));
    private Point ToScreen(double cellX, double cellY) => new(_off.X + cellX * Doc.GridSize * Zoom, _off.Y + cellY * Doc.GridSize * Zoom);

    private double HexR => Doc.GridSize * Zoom / 2.0;
    private double HexH => HexR * Math.Sqrt(3);

    /// <summary>Координаты в клетках (0 = левый/верхний край) -> точка на экране.</summary>
    private Point CellCenter(double cellX, double cellY)
    {
        if (Doc.GridShape != "hex")
            return new Point(_off.X + cellX * Doc.GridSize * Zoom, _off.Y + cellY * Doc.GridSize * Zoom);

        // гекс: берём центр ближайшей гекс-клетки
        int col = (int)Math.Floor(cellX);
        int row = (int)Math.Floor(cellY);
        double r = HexR, h = HexH;
        double cx = _off.X + r + col * 1.5 * r;
        double cy = _off.Y + h / 2 + row * h + (col % 2) * h / 2;
        return new Point(cx, cy);
    }

    private void DrawHexGrid(DrawingContext ctx, double g)
    {
        var pen = new Pen(new SolidColorBrush(Pal.C("#2a211a")), 1);
        double r = HexR, h = HexH;
        for (int row = 0; row < Doc.Rows; row++)
            for (int col = 0; col < Doc.Cols; col++)
            {
                var c = CellCenter(col, row);
                var path = new Avalonia.Media.StreamGeometry();
                using (var gc = path.Open())
                {
                    var pts = new Avalonia.Point[6];
                    for (int k = 0; k < 6; k++)
                    {
                        double ang = Math.PI / 180 * (60 * k - 30);
                        pts[k] = new Avalonia.Point(c.X + Math.Cos(ang) * r, c.Y + Math.Sin(ang) * r);
                    }
                    gc.BeginFigure(pts[0], true);
                    for (int k = 1; k < 6; k++) gc.LineTo(pts[k]);
                    gc.EndFigure(true);
                }
                ctx.DrawGeometry(null, pen, path);
            }
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Pal.C("#100d0b")), new Rect(Bounds.Size));

        double g = Doc.GridSize * Zoom;
        double ox = _off.X, oy = _off.Y;

        // фон карты
        if (_bg == null && !string.IsNullOrEmpty(Doc.Background) && File.Exists(Doc.Background))
        {
            _bg = ImageCache.Get(Doc.Background, 2400);
        }

        var mapRect = new Rect(ox, oy, Doc.Cols * g, Doc.Rows * g);
        if (_bg != null)
            ctx.DrawImage(_bg, mapRect);
        else
            ctx.FillRectangle(new SolidColorBrush(Pal.C("#1c1714")), mapRect);

        // СЃРµС‚РєР°
        if (!Doc.GridVisible)
        {
            // сетка выключена
        }
        else if (Doc.GridShape == "hex")
        {
            DrawHexGrid(ctx, g);
        }
        else
        {
            var gridPen = new Pen(new SolidColorBrush(Pal.C("#2a211a")), 1);
            for (int c = 0; c <= Doc.Cols; c++)
                ctx.DrawLine(gridPen, new Point(ox + c * g, oy), new Point(ox + c * g, oy + Doc.Rows * g));
            for (int r = 0; r <= Doc.Rows; r++)
                ctx.DrawLine(gridPen, new Point(ox, oy + r * g), new Point(ox + Doc.Cols * g, oy + r * g));
        }

        // туман войны
        if (Doc.FogOn)
        {
            var fogBrush = new SolidColorBrush(Color.FromArgb(220, 8, 6, 5));
            for (int r = 0; r < Doc.Rows; r++)
                for (int c = 0; c < Doc.Cols; c++)
                    if (!Doc.FogAt(c, r))
                        ctx.FillRectangle(fogBrush, new Rect(ox + c * g, oy + r * g, g, g));
        }

        // шаблоны
        foreach (var tp in Doc.Templates)
            DrawTemplate(ctx, tp, ox, oy, g);

        // СЃС‚РµРЅС‹
        foreach (var w in Doc.Walls)
        {
            var a = ToScreen(w.X1, w.Y1);
            var b = ToScreen(w.X2, w.Y2);
            Color col = w.Kind switch
            {
                "door" => Pal.C(w.Open ? "#7fa66e" : "#B08948"),
                "grate" => Pal.C("#6E93A8"),
                "elev" => Pal.C("#B7863F"),
                _ => Pal.C("#8A7A5E"),
            };
            ctx.DrawLine(new Pen(new SolidColorBrush(col), Math.Max(2, 4 * Zoom)), a, b);
        }

        // Р°СѓСЂС‹ Рё СЃРІРµС‚
        foreach (var t in Doc.Tokens)
        {
            if (PlayerMode && t.Hidden) continue;
            var cc = CellCenter(t.X + t.Cells / 2.0, t.Y + t.Cells / 2.0);
            double cx = cc.X, cy = cc.Y;
            if (t.AuraOn)
            {
                double r = t.AuraFt / Doc.FeetPerCell * g;
                ctx.DrawEllipse(new SolidColorBrush(Alpha(Pal.C(t.AuraColor), 48)),
                    new Pen(new SolidColorBrush(Alpha(Pal.C(t.AuraColor), 140)), 1.5), new Point(cx, cy), r, r);
            }
            if (t.LightOn)
            {
                double r = t.LightFt / Doc.FeetPerCell * g;
                ctx.DrawEllipse(new SolidColorBrush(Alpha(Pal.C("#FFD98A"), 34)), null, new Point(cx, cy), r, r);
                ctx.DrawEllipse(new SolidColorBrush(Alpha(Pal.C("#FFE7B0"), 46)), null, new Point(cx, cy), r * 0.5, r * 0.5);
            }
        }

        // С‚РѕРєРµРЅС‹
        foreach (var t in Doc.Tokens)
        {
            if (PlayerMode && t.Hidden) continue;
            double size = t.Cells * g;
            var center = CellCenter(t.X + t.Cells / 2.0, t.Y + t.Cells / 2.0);
            double radius = Math.Max(6, size / 2 - 3);
            var accent = Pal.C(t.Color);
            bool on = ReferenceEquals(t, Selected);

            // тень
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null,
                new Point(center.X, center.Y + 3), radius, radius);

            // круглый жетон
            ctx.DrawEllipse(new SolidColorBrush(Alpha(accent, 235)),
                new Pen(new SolidColorBrush(on ? Pal.C("#f0c27b") : Alpha(accent, 255)), on ? 3 : 1.5),
                center, radius, radius);

            // буква на жетоне
            string letter = string.IsNullOrEmpty(t.Name) ? "?" : t.Name.Substring(0, 1).ToUpperInvariant();
            var ft = new FormattedText(letter, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold), radius * 1.05, Brushes.White);
            ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));

            // ореол активного хода
            if (!string.IsNullOrEmpty(ActiveName) && string.Equals(ActiveName, t.Name, StringComparison.OrdinalIgnoreCase))
            {
                ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(38, 240, 194, 123)),
                    new Pen(new SolidColorBrush(Color.FromArgb(220, 240, 194, 123)), 3), center, radius + 6, radius + 6);
            }

            var nt = new FormattedText(t.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, new SolidColorBrush(Pal.C("#efe3d2")));
            ctx.DrawText(nt, new Point(center.X - nt.Width / 2, center.Y + radius + 2));
        }

        // пропсы
        foreach (var pr in Doc.Props)
        {
            double size = pr.Cells * g;
            var center = new Point(ox + (pr.X + pr.Cells / 2.0) * g, oy + (pr.Y + pr.Cells / 2.0) * g);
            double r = Math.Max(6, size * 0.34);
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(215, 44, 34, 26)),
                new Pen(new SolidColorBrush(Pal.C("#8c7030")), 1.4), center, r, r);
            var letter = new FormattedText(
                (pr.Name.Length > 0 ? pr.Name.Substring(0, 1) : "?").ToUpperInvariant(),
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Georgia"), Math.Max(10, r * 1.1), new SolidColorBrush(Pal.C("#f0c27b")));
            ctx.DrawText(letter, new Point(center.X - letter.Width / 2, center.Y - letter.Height / 2));
            var pn = new FormattedText(pr.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 10, new SolidColorBrush(Pal.C("#b7a794")));
            ctx.DrawText(pn, new Point(center.X - pn.Width / 2, center.Y + size / 2 + 2));
        }

        // линейка
        if (_rulerStart is Point rs)
        {
            var a = ToScreen(rs.X, rs.Y);
            var b = _rulerEnd;
            ctx.DrawLine(new Pen(new SolidColorBrush(Pal.C("#f0c27b")), 2), a, b);
            double distFt = Math.Sqrt(Math.Pow(_rulerEnd.X - a.X, 2) + Math.Pow(_rulerEnd.Y - a.Y, 2)) / g * Doc.FeetPerCell;
            var label = new FormattedText($"{distFt:F0} фт", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, new SolidColorBrush(Pal.C("#f6e7cf")));
            var bg = new Rect(b.X + 8, b.Y + 8, label.Width + 12, label.Height + 6);
            ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(200, 20, 16, 12)), bg);
            ctx.DrawText(label, new Point(bg.X + 6, bg.Y + 3));
        }

        // С‡РµСЂРЅРѕРІРёРє СЃС‚РµРЅС‹
        if (_wallStart is Point ws)
        {
            ctx.DrawLine(new Pen(new SolidColorBrush(Pal.C("#d99b4a")), 2, new DashStyle(new double[] { 4, 3 }, 0)),
                ToScreen(ws.X, ws.Y), _lastPointer);
        }

        // предпросмотр шаблона
        if (_tplStart is Point ts)
        {
            var tmp = new Template { Kind = TplKind, X = ts.X, Y = ts.Y, SizeFt = TemplateSize(ts, ToMap(_lastPointer)), Angle = TplAngle };
            DrawTemplate(ctx, tmp, ox, oy, g);
        }
    }

    private void DrawTemplate(DrawingContext ctx, Template tp, double ox, double oy, double g)
    {
        var brush = new SolidColorBrush(Alpha(Pal.C("#d99b4a"), 40));
        var pen = new Pen(new SolidColorBrush(Alpha(Pal.C("#d99b4a"), 160)), 1.5);
        double r = tp.SizeFt / Doc.FeetPerCell * g;
        var c = new Point(ox + (tp.X + 0.5) * g, oy + (tp.Y + 0.5) * g);

        switch (tp.Kind)
        {
            case "circle":
                ctx.DrawEllipse(brush, pen, c, r, r);
                break;
            case "square":
                ctx.DrawRectangle(brush, pen, new Rect(c.X - r, c.Y - r, r * 2, r * 2), 6, 6);
                break;
            case "line":
                double rad = tp.Angle * Math.PI / 180;
                var end = new Point(c.X + Math.Cos(rad) * r, c.Y + Math.Sin(rad) * r);
                ctx.DrawLine(pen, c, end);
                break;
            case "cone":
                double rad2 = tp.Angle * Math.PI / 180;
                var p1 = new Point(c.X + Math.Cos(rad2 - 0.5) * r, c.Y + Math.Sin(rad2 - 0.5) * r);
                var p2 = new Point(c.X + Math.Cos(rad2 + 0.5) * r, c.Y + Math.Sin(rad2 + 0.5) * r);
                var geo = new Avalonia.Media.StreamGeometry();
                using (var gc = geo.Open())
                {
                    gc.BeginFigure(c, true);
                    gc.LineTo(p1);
                    gc.LineTo(p2);
                    gc.EndFigure(true);
                }
                ctx.DrawGeometry(brush, pen, geo);
                break;
        }
    }

    private double TemplateSize(Point a, Point b)
    {
        double cells = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        return Math.Max(5, cells * Doc.FeetPerCell);
    }

    private void PaintFog(Point m)
    {
        int c = (int)Math.Floor(m.X), r = (int)Math.Floor(m.Y);
        if (c < 0 || r < 0 || c >= Doc.Cols || r >= Doc.Rows) return;
        Doc.SetFog(c, r, _fogErase);
        InvalidateVisual();
    }

    private static string PropName(string icon) => icon switch
    {
        "box" => "Сундук",
        "door" => "Дверь",
        "boat" => "Лодка",
        "fire" => "Костёр",
        "statue" => "Статуя",
        "tree" => "Дерево",
        "bed" => "Кровать",
        "coffin" => "Гроб",
        _ => "Объект",
    };

    private Point _lastPointer;
    private bool _shiftHeld;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var p = e.GetPosition(this);
        _lastPointer = p;
        var m = ToMap(p);
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsMiddleButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            _panning = true;
            _panStart = p;
            _offStart = _off;
            e.Pointer.Capture(this);
            return;
        }

        if (Tool == "wall")
        {
            _wallStart = m;
            e.Pointer.Capture(this);
            return;
        }

        if (Tool == "fog")
        {
            _fogErase = Doc.FogAt((int)Math.Floor(m.X), (int)Math.Floor(m.Y));
            PaintFog(m);
            e.Pointer.Capture(this);
            return;
        }

        if (Tool == "template")
        {
            _tplStart = new Point(Math.Floor(m.X), Math.Floor(m.Y));
            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }

        if (Tool == "ruler")
        {
            _rulerStart = new Point(m.X, m.Y);
            _rulerEnd = p;
            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }

        if (Tool == "prop")
        {
            var ph = Doc.Props.LastOrDefault(pr =>
                m.X >= pr.X && m.X <= pr.X + pr.Cells && m.Y >= pr.Y && m.Y <= pr.Y + pr.Cells);
            if (ph != null)
            {
                _propHit = ph;
                _propDragStart = p;
                _propTokenStart = new Point(ph.X, ph.Y);
            }
            else
            {
                Doc.Props.Add(new Prop { X = Math.Floor(m.X), Y = Math.Floor(m.Y), Icon = PropIcon, Name = PropName(PropIcon) });
                Save?.Invoke();
            }
            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }

        var hit = Doc.Tokens.LastOrDefault(t =>
            m.X >= t.X && m.X <= t.X + t.Cells && m.Y >= t.Y && m.Y <= t.Y + t.Cells);

        if (hit != null)
        {
            Selected = hit;

            // двойной клик — открыть лист/карточку
            bool dbl = (DateTime.Now - _lastClick).TotalMilliseconds < 400
                       && Math.Abs(_lastClickPos.X - p.X) < 6
                       && Math.Abs(_lastClickPos.Y - p.Y) < 6
                       && ReferenceEquals(_lastClickToken, hit);
            _lastClick = DateTime.Now;
            _lastClickPos = p;
            _lastClickToken = hit;

            if (dbl)
            {
                _drag = null;
                Changed?.Invoke();
                DoubleClick?.Invoke();
                return;
            }

            _drag = hit;
            _dragStart = p;
            _dragTokenStart = new Point(hit.X, hit.Y);
            e.Pointer.Capture(this);
            InvalidateVisual();
            Changed?.Invoke();
        }
        else
        {
            Selected = null;
            InvalidateVisual();
            Changed?.Invoke();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        _lastPointer = p;
        _shiftHeld = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        if (_panning)
        {
            _off = new Point(_offStart.X + (p.X - _panStart.X), _offStart.Y + (p.Y - _panStart.Y));
            InvalidateVisual();
            return;
        }

        if (_wallStart != null)
        {
            InvalidateVisual();
            return;
        }

        if (Tool == "fog")
        {
            PaintFog(ToMap(p));
            return;
        }

        if (Tool == "template" && _tplStart != null)
        {
            InvalidateVisual();
            return;
        }

        if (Tool == "ruler" && _rulerStart != null)
        {
            _rulerEnd = p;
            InvalidateVisual();
            return;
        }

        if (Tool == "prop" && _propHit != null)
        {
            var g = Doc.GridSize;
            double dx = (p.X - _propDragStart.X) / (g * Zoom);
            double dy = (p.Y - _propDragStart.Y) / (g * Zoom);
            _propHit.X = Math.Round(_propTokenStart.X + dx, 1);
            _propHit.Y = Math.Round(_propTokenStart.Y + dy, 1);
            InvalidateVisual();
            return;
        }

        if (_drag != null)
        {
            var g = Doc.GridSize;
            double dx = (p.X - _dragStart.X) / (g * Zoom);
            double dy = (p.Y - _dragStart.Y) / (g * Zoom);
            double nx = _dragTokenStart.X + dx;
            double ny = _dragTokenStart.Y + dy;
            bool snap = Doc.Snap || _shiftHeld;
            _drag.X = snap ? Math.Round(nx) : Math.Round(nx, 2);
            _drag.Y = snap ? Math.Round(ny) : Math.Round(ny, 2);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panning) { _panning = false; return; }

        if (_wallStart is Point ws && Tool == "wall")
        {
            var m = ToMap(e.GetPosition(this));
            double dx = m.X - ws.X, dy = m.Y - ws.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 0.2)
            {
                double x2 = Math.Abs(dx) > Math.Abs(dy) ? m.X : ws.X;
                double y2 = Math.Abs(dx) > Math.Abs(dy) ? ws.Y : m.Y;
                Doc.Walls.Add(new Wall { X1 = ws.X, Y1 = ws.Y, X2 = x2, Y2 = y2 });
                Save?.Invoke();
            }
            _wallStart = null;
            InvalidateVisual();
            Changed?.Invoke();
            return;
        }

        if (_drag != null)
        {
            if (Doc.Snap || _shiftHeld) { _drag.X = Math.Round(_drag.X); _drag.Y = Math.Round(_drag.Y); }
            _drag = null;
            Save?.Invoke();
            Changed?.Invoke();
        }

        if (Tool == "template" && _tplStart is Point ts)
        {
            var m = ToMap(e.GetPosition(this));
            Doc.Templates.Add(new Template { Kind = TplKind, X = ts.X, Y = ts.Y, SizeFt = TemplateSize(ts, m), Angle = TplAngle });
            Save?.Invoke();
            _tplStart = null;
            InvalidateVisual();
            Changed?.Invoke();
            return;
        }

        if (Tool == "ruler")
        {
            _rulerStart = null;
            InvalidateVisual();
            return;
        }

        if (Tool == "prop" && _propHit != null)
        {
            _propHit.X = Math.Round(_propHit.X);
            _propHit.Y = Math.Round(_propHit.Y);
            _propHit = null;
            Save?.Invoke();
            InvalidateVisual();
            return;
        }

        if (Tool == "fog")
        {
            Save?.Invoke();
            return;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Tool == "template")
        {
            TplAngle = (TplAngle + (e.Delta.Y > 0 ? 15 : -15) + 360) % 360;
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        ZoomBy(e.Delta.Y > 0 ? 1.12 : 1 / 1.12, e.GetPosition(this));
        e.Handled = true;
    }
}


