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
using Avalonia.Media.Imaging;

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
    /// <summary>Ночное зрение/чувства, футов (0 — нет).</summary>
    public double VisionFt { get; set; }
    public int Hp { get; set; } = 10;
    public int MaxHp { get; set; } = 10;
    public int Init { get; set; }
    public int Ac { get; set; }
    public string Speed { get; set; } = "";
    public string Notes { get; set; } = "";
    /// <summary>Поворот картинки токена: 0/90/180/270.</summary>
    public int Rotation { get; set; }
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    /// <summary>Картинка токена (если пусто — буква на жетоне).</summary>
    public string ImagePath { get; set; } = "";
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
    /// <summary>Виден игрокам даже вне обзора.</summary>
    public bool AlwaysVisible { get; set; }
    /// <summary>Полностью скрыт от игроков.</summary>
    public bool Hidden { get; set; }
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
    /// <summary>Считать обзор по свету и ночному зрению (иначе туман только вручную).</summary>
    public bool VisionMode { get; set; } = true;
    public List<bool> Fog { get; set; } = new();
    /// <summary>Игроки уже видели эту клетку.</summary>
    public List<bool> Explored { get; set; } = new();
    public string Background { get; set; } = "";
    public string GridShape { get; set; } = "square";
    /// <summary>Сдвиг сетки относительно фона, в клетках (калибровка).</summary>
    public double GridOffX { get; set; }
    public double GridOffY { get; set; }
    public bool GridVisible { get; set; } = true;
    // видимость слоёв
    public bool ShowWalls { get; set; } = true;
    public bool ShowProps { get; set; } = true;
    public bool ShowTemplates { get; set; } = true;
    public bool ShowTokens { get; set; } = true;
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
            d.Tokens.Add(new Token { Name = "Герой", X = 4, Y = 4, Color = "#d99b4a", Type = "player", Ref = "hero", LightOn = true, LightFt = 30 });
            d.Tokens.Add(new Token { Name = "Гоблин", X = 9, Y = 5, Color = "#7c2a2c", Hp = 7, MaxHp = 7, Init = 12, Type = "mob", Ref = "goblin" });
            d.Tokens.Add(new Token { Name = "Гоблин", X = 9, Y = 7, Color = "#7c2a2c", Hp = 7, MaxHp = 7, Init = 9, Type = "mob", Ref = "goblin" });
        }

        d.EnsureVision();
        return d;
    }

    /// <summary>Выровнять массивы тумана и памяти под размер карты.</summary>
    public void EnsureVision()
    {
        int n = Math.Max(1, Cols) * Math.Max(1, Rows);
        if (Fog == null || Fog.Count != n) Fog = new List<bool>(new bool[n]);
        if (Explored == null || Explored.Count != n) Explored = new List<bool>(new bool[n]);
    }

    public void ClearExplored()
    {
        if (Explored == null) return;
        for (int i = 0; i < Explored.Count; i++) Explored[i] = false;
    }

    public bool ExploredAt(int col, int row)
    {
        int idx = row * Cols + col;
        if (Explored == null || idx < 0 || idx >= Explored.Count) return false;
        return Explored[idx];
    }

    public void SetExplored(int col, int row)
    {
        int idx = row * Cols + col;
        if (Explored == null || idx < 0 || idx >= Explored.Count) return;
        Explored[idx] = true;
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
    /// <summary>Перед изменением документа — снимок для отмены.</summary>
    public Action? BeforeChange;
    /// <summary>Запрошена отмена (Ctrl+Z).</summary>
    public Action? Undo;
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
    /// <summary>Выбранный предмет.</summary>
    public Prop? SelectedProp { get; set; }
    /// <summary>Предмет выбран — обновить панель.</summary>
    public Action? PropPicked;
    private Point? _tplStart;
    private Point? _rulerStart;
    private Point _rulerEnd;
    private bool _fogErase;
    private Prop? _propHit;
    private bool[,]? _vis;
    private DateTime _lastExploredSave = DateTime.MinValue;
    private DateTime _visTime = DateTime.MinValue;
    private bool[,]? _visFine;
    private DateTime _visFineTime = DateTime.MinValue;
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
    private Point ToMap(Point p) => new((p.X - _off.X) / (Doc.GridSize * Zoom) - Doc.GridOffX, (p.Y - _off.Y) / (Doc.GridSize * Zoom) - Doc.GridOffY);
    private Point ToScreen(double cellX, double cellY) => new(_off.X + (cellX + Doc.GridOffX) * Doc.GridSize * Zoom, _off.Y + (cellY + Doc.GridOffY) * Doc.GridSize * Zoom);

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

        double oxG = ox + Doc.GridOffX * g;
        double oyG = oy + Doc.GridOffY * g;

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
                ctx.DrawLine(gridPen, new Point(oxG + c * g, oyG), new Point(oxG + c * g, oyG + Doc.Rows * g));
            for (int r = 0; r <= Doc.Rows; r++)
                ctx.DrawLine(gridPen, new Point(oxG, oyG + r * g), new Point(oxG + Doc.Cols * g, oyG + r * g));
        }

        // шаблоны
        foreach (var tp in Doc.Templates)
        {
            if (!Doc.ShowTemplates) break;
            if (PlayerMode && Doc.FogOn && !CellSeen(tp.X, tp.Y)) continue;
            DrawTemplate(ctx, tp, oxG, oyG, g);
        }

        // у мастера — радиусы обзора выделенного токена
        if (!PlayerMode && Selected != null)
        {
            var sel = Selected;
            var sc = CellCenter(sel.X + sel.Cells / 2.0, sel.Y + sel.Cells / 2.0);
            if (sel.LightOn && sel.LightFt > 0)
            {
                double lr = sel.LightFt / Math.Max(1, Doc.FeetPerCell) * g;
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(200, 240, 194, 123)), 1.6,
                    new DashStyle(new double[] { 5, 4 }, 0)), sc, lr, lr);
            }
            if (sel.VisionFt > 0)
            {
                double vr = sel.VisionFt / Math.Max(1, Doc.FeetPerCell) * g;
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(190, 126, 170, 210)), 1.4,
                    new DashStyle(new double[] { 3, 4 }, 0)), sc, vr, vr);
            }
        }

        // СЃС‚РµРЅС‹
        if (!PlayerMode && Doc.ShowWalls)
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
            if (!Doc.ShowTokens) break;
            if (PlayerMode && t.Hidden) continue;
            if (PlayerMode && Doc.FogOn && _vis != null && !TokenVisible(t)) continue;
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
            if (!Doc.ShowTokens) break;
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

            // картинка токена (если задана) или буква
            Bitmap? portrait = null;
            if (!string.IsNullOrEmpty(t.ImagePath) && File.Exists(t.ImagePath))
                portrait = ImageCache.Get(t.ImagePath, 512);

            if (portrait != null)
            {
                var square = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
                var clip = new RoundedRect(square, radius, radius);
                if (t.Rotation != 0 || t.FlipX || t.FlipY)
                {
                    double ang = t.Rotation * Math.PI / 180.0;
                    var m = Matrix.CreateTranslation(-center.X, -center.Y) *
                            Matrix.CreateRotation(ang) *
                            Matrix.CreateScale(t.FlipX ? -1 : 1, t.FlipY ? -1 : 1) *
                            Matrix.CreateTranslation(center.X, center.Y);
                    using (ctx.PushTransform(m))
                    using (ctx.PushClip(clip))
                        ctx.DrawImage(portrait, square);
                }
                else
                {
                    using (ctx.PushClip(clip))
                        ctx.DrawImage(portrait, square);
                }
            }
            else
            {
                string letter = string.IsNullOrEmpty(t.Name) ? "?" : t.Name.Substring(0, 1).ToUpperInvariant();
                var ft = new FormattedText(letter, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold), radius * 1.05, Brushes.White);
                ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
            }

            // предупреждение мастеру: токен стоит в стене
            if (!PlayerMode && TokenInWall(t))
            {
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(230, 210, 80, 68)), 2,
                    new DashStyle(new double[] { 3, 3 }, 0)), center, radius + 3, radius + 3);
            }

            // ореол активного хода
            if (!string.IsNullOrEmpty(ActiveName) && string.Equals(ActiveName, t.Name, StringComparison.OrdinalIgnoreCase))
            {
                ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(38, 240, 194, 123)),
                    new Pen(new SolidColorBrush(Color.FromArgb(220, 240, 194, 123)), 3), center, radius + 6, radius + 6);
            }

            var nt = new FormattedText(t.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, new SolidColorBrush(Pal.C("#efe3d2")));
            ctx.DrawText(nt, new Point(center.X - nt.Width / 2, center.Y + radius + 2));

            // полоска хитов (у раненых и у выделенного)
            if (t.MaxHp > 0 && (t.Hp < t.MaxHp || on))
            {
                double bw = Math.Max(20, size * 0.72), bh = Math.Max(4, size * 0.08);
                double bx = center.X - bw / 2, by = center.Y + radius + 16;
                ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 18, 13, 10)),
                    new Rect(bx, by, bw, bh), 2);
                double frac = Math.Clamp(t.Hp / (double)Math.Max(1, t.MaxHp), 0, 1);
                Color hpCol = frac > 0.5 ? Color.FromArgb(235, 108, 158, 98)
                    : frac > 0.25 ? Color.FromArgb(235, 208, 158, 78)
                    : Color.FromArgb(235, 192, 78, 68);
                if (frac > 0) ctx.FillRectangle(new SolidColorBrush(hpCol), new Rect(bx, by, Math.Max(1, bw * frac), bh), 2);
            }
        }

        // пропсы — маленькие векторные иконки (обычные: скрываются туманом)
        if (Doc.ShowProps)
        foreach (var pr in Doc.Props)
        {
            if (pr.AlwaysVisible) continue;
            if (PlayerMode && pr.Hidden) continue;
            if (PlayerMode && Doc.FogOn && !CellSeen(pr.X, pr.Y)) continue;
            DrawProp(ctx, pr, oxG, oyG, g);
        }

        DrawFog(ctx, oxG, oyG, g);

        // предметы, видимые всегда (поверх тумана)
        if (Doc.ShowProps)
        foreach (var pr in Doc.Props)
        {
            if (!pr.AlwaysVisible) continue;
            if (PlayerMode && pr.Hidden) continue;
            DrawProp(ctx, pr, oxG, oyG, g);
        }

        // подсветка выбранного предмета у мастера
        if (!PlayerMode && SelectedProp != null && Doc.Props.Contains(SelectedProp))
        {
            var sp = SelectedProp;
            double ssz = sp.Cells * g;
            var screct = new Rect(oxG + sp.X * g - 2, oyG + sp.Y * g - 2, ssz + 4, ssz + 4);
            ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(230, 240, 194, 123)), 2), screct);
        }
        if (!PlayerMode) DrawMinimap(ctx);

// линейка
        if (_rulerStart is Point rs)
        {
            var a = ToScreen(rs.X, rs.Y);
            var b = _rulerEnd;
            ctx.DrawLine(new Pen(new SolidColorBrush(Pal.C("#f0c27b")), 2), a, b);
            double distFt = Math.Sqrt(Math.Pow(_rulerEnd.X - a.X, 2) + Math.Pow(_rulerEnd.Y - a.Y, 2)) / g * Doc.FeetPerCell;
            double cells = distFt / Math.Max(1, Doc.FeetPerCell);
            string labelText = $"{distFt:F0} фт · {cells:F1} кл.";
            // если между точками стена — помечаем
            if (MapVision.Blocked(Doc, rs, ToMap(_rulerEnd))) labelText += " · сквозь стену";
            var label = new FormattedText(labelText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
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

    /// <summary>Виден ли токен игрокам (хотя бы одна клетка под ним в обзоре).</summary>
    private bool TokenVisible(Token t)
    {
        if (_vis == null) return true;
        int c0 = Math.Max(0, (int)Math.Floor(t.X));
        int r0 = Math.Max(0, (int)Math.Floor(t.Y));
        int c1 = Math.Min(Doc.Cols - 1, (int)Math.Ceiling(t.X + t.Cells) - 1);
        int r1 = Math.Min(Doc.Rows - 1, (int)Math.Ceiling(t.Y + t.Cells) - 1);
        for (int c = c0; c <= c1; c++)
            for (int r = r0; r <= r1; r++)
                if (_vis[c, r]) return true;
        return false;
    }

    /// <summary>Стоит ли токен на стене (предупреждение мастеру).</summary>
    private bool TokenInWall(Token t)
    {
        var c = new Point(t.X + t.Cells / 2.0, t.Y + t.Cells / 2.0);
        foreach (var w in Doc.Walls)
        {
            if (w.Kind == "door" && w.Open) continue;
            if (w.Kind == "elev") continue;
            if (DistToSegment(c, new Point(w.X1, w.Y1), new Point(w.X2, w.Y2)) < 0.3) return true;
        }
        return false;
    }

    /// <summary>Видна ли игрокам клетка с этой позиции.</summary>
    private bool CellSeen(double x, double y)
    {
        if (_vis == null) return true;
        int c = (int)Math.Floor(x), r = (int)Math.Floor(y);
        if (c < 0 || r < 0 || c >= Doc.Cols || r >= Doc.Rows) return false;
        return _vis[c, r];
    }

    /// <summary>Ближайшая стена рядом с точкой (для дверей).</summary>
    private Wall? WallNear(Point m, double tol)
    {
        Wall? best = null;
        double bestD = tol;
        foreach (var w in Doc.Walls)
        {
            double d = DistToSegment(m, new Point(w.X1, w.Y1), new Point(w.X2, w.Y2));
            if (d < bestD) { bestD = d; best = w; }
        }
        return best;
    }

    private static double DistToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-9) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        double px = a.X + t * dx, py = a.Y + t * dy;
        return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
    }

    /// <summary>Иконка предмета на карте.</summary>
    private void DrawProp(DrawingContext ctx, Prop pr, double ox, double oy, double g)
    {
        double size = pr.Cells * g;
        var center = new Point(ox + (pr.X + pr.Cells / 2.0) * g, oy + (pr.Y + pr.Cells / 2.0) * g);
        double half = Math.Max(9, size * 0.36);
        var box = new Rect(center.X - half, center.Y - half, half * 2, half * 2);

        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(225, 38, 30, 22)), box);
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(190, 140, 112, 48)), 1.3), box);

        var gold = new SolidColorBrush(Pal.C("#f0c27b"));
        var pen = new Pen(gold, Math.Max(1.2, half * 0.10));
        string icon = string.IsNullOrEmpty(pr.Icon) ? "box" : pr.Icon;
        double k = half;

        switch (icon)
        {
            case "door":
                ctx.DrawRectangle(null, pen, new Rect(center.X - k * 0.55, center.Y - k * 0.75, k * 1.1, k * 1.5));
                ctx.DrawEllipse(gold, null, new Point(center.X + k * 0.25, center.Y), k * 0.12, k * 0.12);
                break;
            case "boat":
            {
                var g1 = new Avalonia.Media.StreamGeometry();
                using (var gc = g1.Open())
                {
                    gc.BeginFigure(new Point(center.X - k * 0.8, center.Y + k * 0.1), false);
                    gc.LineTo(new Point(center.X + k * 0.8, center.Y + k * 0.1));
                    gc.LineTo(new Point(center.X + k * 0.45, center.Y + k * 0.7));
                    gc.LineTo(new Point(center.X - k * 0.45, center.Y + k * 0.7));
                    gc.EndFigure(true);
                }
                ctx.DrawGeometry(null, pen, g1);
                ctx.DrawLine(pen, new Point(center.X, center.Y + k * 0.1), new Point(center.X, center.Y - k * 0.75));
                break;
            }
            case "fire":
            {
                var g2 = new Avalonia.Media.StreamGeometry();
                using (var gc = g2.Open())
                {
                    gc.BeginFigure(new Point(center.X, center.Y - k * 0.8), false);
                    gc.LineTo(new Point(center.X + k * 0.55, center.Y + k * 0.15));
                    gc.LineTo(new Point(center.X + k * 0.2, center.Y + k * 0.75));
                    gc.LineTo(new Point(center.X - k * 0.2, center.Y + k * 0.75));
                    gc.LineTo(new Point(center.X - k * 0.55, center.Y + k * 0.15));
                    gc.EndFigure(true);
                }
                ctx.DrawGeometry(null, pen, g2);
                break;
            }
            case "statue":
                ctx.DrawEllipse(null, pen, new Point(center.X, center.Y - k * 0.35), k * 0.3, k * 0.3);
                ctx.DrawRectangle(null, pen, new Rect(center.X - k * 0.55, center.Y + k * 0.15, k * 1.1, k * 0.6));
                break;
            case "tree":
            {
                var g3 = new Avalonia.Media.StreamGeometry();
                using (var gc = g3.Open())
                {
                    gc.BeginFigure(new Point(center.X, center.Y - k * 0.85), false);
                    gc.LineTo(new Point(center.X + k * 0.6, center.Y + k * 0.25));
                    gc.LineTo(new Point(center.X - k * 0.6, center.Y + k * 0.25));
                    gc.EndFigure(true);
                }
                ctx.DrawGeometry(null, pen, g3);
                ctx.DrawLine(pen, new Point(center.X, center.Y + k * 0.25), new Point(center.X, center.Y + k * 0.8));
                break;
            }
            case "bed":
                ctx.DrawRectangle(null, pen, new Rect(center.X - k * 0.8, center.Y - k * 0.5, k * 1.6, k * 1.0));
                ctx.DrawRectangle(gold, null, new Rect(center.X - k * 0.65, center.Y - k * 0.35, k * 0.5, k * 0.7));
                break;
            case "coffin":
            {
                var g4 = new Avalonia.Media.StreamGeometry();
                using (var gc = g4.Open())
                {
                    gc.BeginFigure(new Point(center.X, center.Y - k * 0.85), false);
                    gc.LineTo(new Point(center.X + k * 0.5, center.Y - k * 0.4));
                    gc.LineTo(new Point(center.X + k * 0.5, center.Y + k * 0.8));
                    gc.LineTo(new Point(center.X - k * 0.5, center.Y + k * 0.8));
                    gc.LineTo(new Point(center.X - k * 0.5, center.Y - k * 0.4));
                    gc.EndFigure(true);
                }
                ctx.DrawGeometry(null, pen, g4);
                break;
            }
            case "mark-excl":
            case "mark-quest":
            {
                string mark = icon == "mark-excl" ? "!" : "?";
                var ft = new FormattedText(mark, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Georgia", FontStyle.Normal, FontWeight.Bold), k * 1.9, gold);
                ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2 - k * 0.05));
                break;
            }
            default:
                ctx.DrawRectangle(null, pen, new Rect(center.X - k * 0.75, center.Y - k * 0.55, k * 1.5, k * 1.1));
                ctx.DrawLine(pen, new Point(center.X - k * 0.75, center.Y - k * 0.15), new Point(center.X + k * 0.75, center.Y - k * 0.15));
                ctx.DrawEllipse(gold, null, new Point(center.X, center.Y + k * 0.2), k * 0.1, k * 0.1);
                break;
        }

        var pn = new FormattedText(pr.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 10, new SolidColorBrush(Pal.C("#b7a794")));
        ctx.DrawText(pn, new Point(center.X - pn.Width / 2, center.Y + half + 3));
    }

    /// <summary>Туман поверх карты: круглые границы, токены в темноте обрезаются.</summary>
    private void DrawFog(DrawingContext ctx, double ox, double oy, double g)
    {
        const int sub = 6;
        int subW = Doc.Cols * sub, subH = Doc.Rows * sub;
        if (subW <= 0 || subH <= 0) return;

        if (_visFine == null || (DateTime.UtcNow - _visFineTime).TotalMilliseconds > 200)
        {
            _visFine = MapVision.ComputeFine(Doc, sub);
            _visFineTime = DateTime.UtcNow;
            if (_vis == null || (DateTime.UtcNow - _visTime).TotalMilliseconds > 600)
            {
                _vis = MapVision.Compute(Doc);
                _visTime = DateTime.UtcNow;
            }
            // запоминаем разведанное
            if (_vis != null)
            {
                bool changed = false;
                for (int c = 0; c < Doc.Cols; c++)
                    for (int r = 0; r < Doc.Rows; r++)
                        if (_vis[c, r] && !Doc.ExploredAt(c, r)) { Doc.SetExplored(c, r); changed = true; }
                if (changed && (DateTime.UtcNow - _lastExploredSave).TotalSeconds > 2)
                {
                    _lastExploredSave = DateTime.UtcNow;
                    Save?.Invoke();
                }
            }
        }

        bool[,] vis = _visFine;
        var darkBrush = new SolidColorBrush(Color.FromArgb(255, 6, 5, 4));
        var memBrush = new SolidColorBrush(Color.FromArgb((byte)(PlayerMode ? 250 : 130), 9, 7, 6));
        double s = g / sub;

        // дистанция до ближайшей видимой подсетки — для плавного края
        var dist = new int[subW, subH];
        for (int x = 0; x < subW; x++)
            for (int y = 0; y < subH; y++)
                dist[x, y] = vis[x, y] ? 0 : 99;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int x = 0; x < subW; x++)
            {
                for (int y = 0; y < subH; y++)
                {
                    if (dist[x, y] == 0) continue;
                    int best = dist[x, y];
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= subW || ny >= subH) continue;
                            best = Math.Min(best, dist[nx, ny] + 1);
                        }
                    dist[x, y] = Math.Min(best, 3);
                }
            }
        }

        for (int r = 0; r < subH; r++)
        {
            int c = 0;
            while (c < subW)
            {
                int d = dist[c, r];
                int c2 = c + 1;
                while (c2 < subW && dist[c2, r] == d) c2++;
                if (d > 0)
                {
                    bool mem = Doc.ExploredAt(Math.Min(c / sub, Doc.Cols - 1), Math.Min(r / sub, Doc.Rows - 1));
                    SolidColorBrush brush;
                    if (mem) brush = memBrush;
                    else
                    {
                        byte a = (byte)(d == 1 ? 175 : d == 2 ? 235 : 255);
                        brush = new SolidColorBrush(Color.FromArgb(a, 6, 5, 4));
                    }
                    ctx.FillRectangle(brush, new Rect(ox + c * s, oy + r * s, (c2 - c) * s + 0.5, s + 0.5));
                }
                c = c2;
            }
        }

        // у мастера: силуэты скрытых токенов, чтобы вести бой
        if (!PlayerMode)
        {
            var ghost = new SolidColorBrush(Color.FromArgb(70, 239, 227, 210));
            foreach (var t in Doc.Tokens)
            {
                if (t.Hidden) continue;
                if (_vis != null && _vis[(int)Math.Clamp(t.X, 0, Doc.Cols - 1), (int)Math.Clamp(t.Y, 0, Doc.Rows - 1)]) continue;
                var cc = CellCenter(t.X + t.Cells / 2.0, t.Y + t.Cells / 2.0);
                ctx.DrawEllipse(ghost, null, cc, Math.Max(4, t.Cells * g * 0.35), Math.Max(4, t.Cells * g * 0.35));
            }
        }
    }

    /// <summary>Мини-карта «как видят игроки» (для мастера).</summary>
    private void DrawMinimap(DrawingContext ctx)
    {
        if (Doc.Cols <= 0 || Doc.Rows <= 0) return;

        const double maxW = 230, maxH = 160;
        double mw = maxW, mh = mw * Doc.Rows / Doc.Cols;
        if (mh > maxH) { mh = maxH; mw = mh * Doc.Cols / Doc.Rows; }
        if (mw < 80) return;

        var rect = new Rect(Bounds.Width - mw - 18, Bounds.Height - mh - 18, mw, mh);
        var bg = new SolidColorBrush(Color.FromArgb(225, 14, 11, 9));
        ctx.FillRectangle(bg, rect);
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Pal.C("#4a3728")), 1), rect);

        double sx = mw / Doc.Cols, sy = mh / Doc.Rows;

        // видимая игрокам зона (по подсетке — форма как в основном виде)
        if (Doc.FogOn && _visFine != null)
        {
            const int msub = 6;
            int mw2 = Doc.Cols * msub, mh2 = Doc.Rows * msub;
            if (mw2 == _visFine.GetLength(0) && mh2 == _visFine.GetLength(1))
            {
                var visBrush = new SolidColorBrush(Color.FromArgb(95, 240, 194, 123));
                double mx = mw / (double)mw2, my = mh / (double)mh2;
                for (int c = 0; c < mw2; c += 2)
                    for (int r = 0; r < mh2; r += 2)
                        if (_visFine[c, r])
                            ctx.FillRectangle(visBrush, new Rect(rect.X + c * mx, rect.Y + r * my, mx * 2 + 0.4, my * 2 + 0.4));
            }
        }

        // стены
        if (Doc.ShowWalls)
        {
            var wpen = new Pen(new SolidColorBrush(Color.FromArgb(200, 138, 122, 94)), 1);
            foreach (var w in Doc.Walls)
                ctx.DrawLine(wpen,
                    new Point(rect.X + w.X1 * sx, rect.Y + w.Y1 * sy),
                    new Point(rect.X + w.X2 * sx, rect.Y + w.Y2 * sy));
        }

        // токены (только видимые игрокам)
        foreach (var t in Doc.Tokens)
        {
            if (t.Hidden) continue;
            bool seen = !Doc.FogOn || _vis == null || _vis[(int)Math.Clamp(t.X, 0, Doc.Cols - 1), (int)Math.Clamp(t.Y, 0, Doc.Rows - 1)];
            if (!seen) continue;
            var cc = new Point(rect.X + (t.X + t.Cells / 2.0) * sx, rect.Y + (t.Y + t.Cells / 2.0) * sy);
            double rr = Math.Max(2.5, t.Cells * sx * 0.4);
            ctx.DrawEllipse(new SolidColorBrush(Pal.C(t.Color)), null, cc, rr, rr);
        }

        // рамка видимой области экрана
        var vx1 = rect.X + (-_off.X / (Doc.GridSize * Zoom)) * sx;
        var vy1 = rect.Y + (-_off.Y / (Doc.GridSize * Zoom)) * sy;
        var vx2 = rect.X + ((Bounds.Width - _off.X) / (Doc.GridSize * Zoom)) * sx;
        var vy2 = rect.Y + ((Bounds.Height - _off.Y) / (Doc.GridSize * Zoom)) * sy;
        var view = new Rect(vx1, vy1, Math.Max(1, vx2 - vx1), Math.Max(1, vy2 - vy1)).Intersect(rect);
        if (view.Width > 2 && view.Height > 2)
            ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(180, 239, 227, 210)), 1), view);

        var label = new FormattedText("экран игроков", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 9.5, new SolidColorBrush(Pal.C("#9c8b78")));
        ctx.DrawText(label, new Point(rect.X + 8, rect.Y + 4));
    }

    /// <summary>Размер кисти тумана в клетках (радиус).</summary>
    public int FogBrush { get; set; } = 1;

    private void PaintFog(Point m)
    {
        int c = (int)Math.Floor(m.X), r = (int)Math.Floor(m.Y);
        if (c < 0 || r < 0 || c >= Doc.Cols || r >= Doc.Rows) return;
        for (int dc = -FogBrush; dc <= FogBrush; dc++)
            for (int dr = -FogBrush; dr <= FogBrush; dr++)
                Doc.SetFog(c + dc, r + dr, _fogErase);
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
        "mark-excl" => "Метка «!»",
        "mark-quest" => "Метка «?»",
        _ => "Объект",
    };

    private Point _lastPointer;
    private bool _shiftHeld;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (PlayerMode) return;

        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Undo?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None)
        {
            string? t = e.Key switch
            {
                Key.V => "select",
                Key.S => "wall",
                Key.F => "fog",
                Key.T => "template",
                Key.P => "prop",
                Key.R => "ruler",
                Key.Escape => "select",
                _ => null,
            };
            if (t != null)
            {
                Tool = t;
                if (e.Key == Key.Escape) Selected = null;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Delete && Selected != null)
        {
            BeforeChange?.Invoke();
            Doc.Tokens.Remove(Selected);
            Selected = null;
            Save?.Invoke();
            Changed?.Invoke();
            InvalidateVisual();
            e.Handled = true;
        }
    }

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

        BeforeChange?.Invoke();

        if (Tool == "wall")
        {
            _wallStart = new Point(Math.Round(m.X * 2) / 2, Math.Round(m.Y * 2) / 2);
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

        if (Tool == "select")
        {
            var picked = Doc.Props.LastOrDefault(pr =>
                m.X >= pr.X && m.X <= pr.X + pr.Cells && m.Y >= pr.Y && m.Y <= pr.Y + pr.Cells);
            if (picked != null)
            {
                SelectedProp = picked;
                PropPicked?.Invoke();
                _propHit = picked;
                _propDragStart = p;
                _propTokenStart = new Point(picked.X, picked.Y);
                e.Pointer.Capture(this);
                InvalidateVisual();
                return;
            }
            SelectedProp = null;
            PropPicked?.Invoke();
        }

        if (Tool == "select" && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            var victim = Doc.Props.FirstOrDefault(pr =>
                m.X >= pr.X && m.X <= pr.X + pr.Cells && m.Y >= pr.Y && m.Y <= pr.Y + pr.Cells);
            if (victim != null)
            {
                Doc.Props.Remove(victim);
                Save?.Invoke();
                InvalidateVisual();
                return;
            }

            var tplVictim = Doc.Templates.FirstOrDefault(tp =>
            {
                double rr = Math.Max(0.6, tp.SizeFt / Math.Max(1, Doc.FeetPerCell));
                return Math.Sqrt((m.X - tp.X) * (m.X - tp.X) + (m.Y - tp.Y) * (m.Y - tp.Y)) <= rr;
            });
            if (tplVictim != null)
            {
                Doc.Templates.Remove(tplVictim);
                Save?.Invoke();
                InvalidateVisual();
                return;
            }
        }

        if (Tool == "select" && WallNear(m, 0.35) is { Kind: "door" } doorHit)
        {
            doorHit.Open = !doorHit.Open;
            Save?.Invoke();
            InvalidateVisual();
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
                Doc.Props.Add(new Prop
            {
                X = Math.Floor(m.X),
                Y = Math.Floor(m.Y),
                Icon = PropIcon,
                Name = PropName(PropIcon),
                AlwaysVisible = PropIcon is "mark-excl" or "mark-quest",
            });
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

        if ((Tool == "prop" || Tool == "select") && _propHit != null)
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
                double sx = Math.Round(x2 * 2) / 2, sy = Math.Round(y2 * 2) / 2;
            if (Math.Abs(sx - ws.X) < 0.5 && Math.Abs(sy - ws.Y) < 0.5) return;
            Doc.Walls.Add(new Wall { X1 = ws.X, Y1 = ws.Y, X2 = sx, Y2 = sy });
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

        if ((Tool == "prop" || Tool == "select") && _propHit != null)
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


