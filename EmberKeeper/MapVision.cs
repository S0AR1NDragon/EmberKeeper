using System;
using Avalonia;
using System.Collections.Generic;
using System.Linq;

namespace EmberKeeper;

/// <summary>
/// Обзор карты: что видят игроки. Туман = темнота.
/// Зона освещена, если её освещает источник света (факел, лампа) или если персонаж
/// видит её за счёт ночного зрения/чувств. Стены перекрывают обзор.
/// </summary>
public static class MapVision
{
    /// <summary>Видимость по клеткам: true — игроки видят клетку прямо сейчас.</summary>
    public static bool[,] Compute(MapDoc d)
    {
        int cols = Math.Max(1, d.Cols), rows = Math.Max(1, d.Rows);
        var vis = new bool[cols, rows];

        // туман выключен — всё открыто
        if (!d.FogOn || !d.VisionMode)
        {
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++) vis[c, r] = true;
            return vis;
        }

        // вручную открытые клетки (ластик тумана) тоже видны
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                if (d.FogAt(c, r)) vis[c, r] = true;

        foreach (var t in d.Tokens)
        {
            if (t.Hidden) continue;
            bool observer = t.Type == "player" || t.LightOn;
            if (!observer) continue;

            double cx = t.X + t.Cells / 2.0;
            double cy = t.Y + t.Cells / 2.0;
            double lightFt = t.LightOn ? Math.Max(0, t.LightFt) : 0;
            double darkFt = Math.Max(0, t.VisionFt);
            double radiusFt = Math.Max(lightFt, darkFt);
            if (radiusFt <= 0) continue;

            double radiusCells = radiusFt / Math.Max(1, d.FeetPerCell);
            if (radiusCells > 200) radiusCells = 200;

            var center = new Point(cx, cy);
            int c0 = Math.Max(0, (int)Math.Floor(cx - radiusCells));
            int c1 = Math.Min(cols - 1, (int)Math.Ceiling(cx + radiusCells));
            int r0 = Math.Max(0, (int)Math.Floor(cy - radiusCells));
            int r1 = Math.Min(rows - 1, (int)Math.Ceiling(cy + radiusCells));

            for (int c = c0; c <= c1; c++)
            {
                for (int r = r0; r <= r1; r++)
                {
                    if (vis[c, r]) continue;
                    var target = new Point(c + 0.5, r + 0.5);
                    double distCells = Math.Sqrt((target.X - cx) * (target.X - cx) + (target.Y - cy) * (target.Y - cy));
                    if (distCells > radiusCells) continue;

                    double distFt = distCells * d.FeetPerCell;
                    bool withinLight = lightFt > 0 && distFt <= lightFt;
                    bool withinDark = darkFt > 0 && distFt <= darkFt;
                    if (!withinLight && !withinDark) continue;

                    if (Blocked(d, center, target)) continue;
                    vis[c, r] = true;
                }
            }
        }
        return vis;
    }

    /// <summary>Есть ли стена между двумя точками (в координатах клеток).</summary>
    public static bool Blocked(MapDoc d, Point a, Point b)
    {
        foreach (var w in d.Walls)
        {
            // открытая дверь, решётка и перепад высот не мешают обзору
            if (w.Kind == "grate" || w.Kind == "elev") continue;
            if (w.Kind == "door" && w.Open) continue;
            if (SegmentsIntersect(a, b, new Point(w.X1, w.Y1), new Point(w.X2, w.Y2)))
                return true;
        }
        return false;
    }

    private static bool SegmentsIntersect(Point p1, Point p2, Point p3, Point p4)
    {
        double d1 = Cross(p3, p4, p1);
        double d2 = Cross(p3, p4, p2);
        double d3 = Cross(p1, p2, p3);
        double d4 = Cross(p1, p2, p4);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        return false;
    }

    private static double Cross(Point o, Point a, Point b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    /// <summary>Обзор в подсеточном разрешении: круглые границы вместо клетчатых.</summary>
    public static bool[,] ComputeFine(MapDoc d, int sub)
    {
        int subW = Math.Max(1, d.Cols) * sub;
        int subH = Math.Max(1, d.Rows) * sub;
        var vis = new bool[subW, subH];

        if (!d.FogOn || !d.VisionMode)
        {
            for (int x = 0; x < subW; x++)
                for (int y = 0; y < subH; y++) vis[x, y] = true;
            return vis;
        }

        for (int c = 0; c < d.Cols; c++)
            for (int r = 0; r < d.Rows; r++)
                if (d.FogAt(c, r))
                    for (int x = 0; x < sub; x++)
                        for (int y = 0; y < sub; y++)
                            vis[c * sub + x, r * sub + y] = true;

        foreach (var t in d.Tokens)
        {
            if (t.Hidden) continue;
            if (!(t.Type == "player" || t.LightOn)) continue;

            double cx = t.X + t.Cells / 2.0;
            double cy = t.Y + t.Cells / 2.0;
            double lightFt = t.LightOn ? Math.Max(0, t.LightFt) : 0;
            double darkFt = Math.Max(0, t.VisionFt);
            double radiusFt = Math.Max(lightFt, darkFt);
            if (radiusFt <= 0) continue;

            double radiusCells = radiusFt / Math.Max(1, d.FeetPerCell);
            if (radiusCells > 200) radiusCells = 200;

            var center = new Point(cx, cy);
            int x0 = Math.Max(0, (int)Math.Floor((cx - radiusCells) * sub));
            int x1 = Math.Min(subW - 1, (int)Math.Ceiling((cx + radiusCells) * sub));
            int y0 = Math.Max(0, (int)Math.Floor((cy - radiusCells) * sub));
            int y1 = Math.Min(subH - 1, (int)Math.Ceiling((cy + radiusCells) * sub));

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (vis[x, y]) continue;
                    var target = new Point((x + 0.5) / sub, (y + 0.5) / sub);
                    double distCells = Math.Sqrt((target.X - cx) * (target.X - cx) + (target.Y - cy) * (target.Y - cy));
                    if (distCells > radiusCells) continue;
                    double distFt = distCells * d.FeetPerCell;
                    bool ok = (lightFt > 0 && distFt <= lightFt) || (darkFt > 0 && distFt <= darkFt);
                    if (!ok) continue;
                    if (Blocked(d, center, target)) continue;
                    vis[x, y] = true;
                }
            }
        }
        return vis;
    }
}
