using System.Drawing.Drawing2D;

namespace KiotVietLabelPrinter.UI;

// Small set of hand-drawn line icons (thin stroke, single color) so every
// screen uses the same icon language instead of mismatched emoji glyphs.
// Everything is authored on a 24x24 grid (same convention as most icon
// libraries) and scaled/centered into whatever rectangle is requested.
public static class IconGlyphs
{
    public enum Kind
    {
        Tag,
        Barcode,
        Glasses,
        Document,
        Folder,
        Settings,
        Clock,
        Eye,
        ShieldCheck,
        Code,
        Printer,
        ArrowLeft,
        ArrowRight,
        Trash,
        Refresh,
        Plus,
        Check
    }

    public static void Draw(Graphics g, Kind kind, RectangleF bounds, Color color, float strokeWidth = 1.75f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || color.A == 0)
            return;

        float size = Math.Min(bounds.Width, bounds.Height);
        float scale = size / 24f;

        if (scale <= 0)
            return;

        float originX = bounds.X + ((bounds.Width - size) / 2f);
        float originY = bounds.Y + ((bounds.Height - size) / 2f);

        GraphicsState state = g.Save();

        try
        {
            g.Transform = new Matrix(scale, 0, 0, scale, originX, originY);

            using Pen pen = new(color, strokeWidth / scale)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using SolidBrush brush = new(color);

            switch (kind)
            {
                case Kind.Tag: DrawTag(g, pen); break;
                case Kind.Barcode: DrawBarcode(g, brush); break;
                case Kind.Glasses: DrawGlasses(g, pen); break;
                case Kind.Document: DrawDocument(g, pen); break;
                case Kind.Folder: DrawFolder(g, pen); break;
                case Kind.Settings: DrawSettings(g, pen); break;
                case Kind.Clock: DrawClock(g, pen); break;
                case Kind.Eye: DrawEye(g, pen, brush); break;
                case Kind.ShieldCheck: DrawShieldCheck(g, pen); break;
                case Kind.Code: DrawCode(g, pen); break;
                case Kind.Printer: DrawPrinter(g, pen); break;
                case Kind.ArrowLeft: DrawArrowLeft(g, pen); break;
                case Kind.ArrowRight: DrawArrowRight(g, pen); break;
                case Kind.Trash: DrawTrash(g, pen); break;
                case Kind.Refresh: DrawRefresh(g, pen); break;
                case Kind.Plus: DrawPlus(g, pen); break;
                case Kind.Check: DrawCheck(g, pen); break;
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static void DrawTag(Graphics g, Pen pen)
    {
        using GraphicsPath path = new();
        path.AddLines(new[]
        {
            new PointF(12, 3), new PointF(4, 3), new PointF(4, 11),
            new PointF(12, 19), new PointF(19, 12)
        });
        path.CloseFigure();
        g.DrawPath(pen, path);
        g.DrawEllipse(pen, 6.2f, 5.2f, 2.4f, 2.4f);
    }

    private static void DrawBarcode(Graphics g, SolidBrush brush)
    {
        float[] bars = { 3f, 1.6f, 5.6f, 1f, 7.6f, 2.2f, 11f, 1f, 13f, 1.8f, 15.8f, 1f, 17.8f, 2.2f, 21f, 1f };

        for (int i = 0; i < bars.Length; i += 2)
            g.FillRectangle(brush, bars[i], 4f, bars[i + 1], 16f);
    }

    private static void DrawGlasses(Graphics g, Pen pen)
    {
        g.DrawEllipse(pen, 3.5f, 10.5f, 7f, 7f);
        g.DrawEllipse(pen, 13.5f, 10.5f, 7f, 7f);
        g.DrawLine(pen, 10.5f, 13f, 13.5f, 13f);
        g.DrawLine(pen, 3.7f, 11.5f, 1.5f, 8f);
        g.DrawLine(pen, 20.3f, 11.5f, 22.5f, 8f);
    }

    private static void DrawDocument(Graphics g, Pen pen)
    {
        using GraphicsPath path = AppTheme.RoundedRect(new RectangleF(6, 3, 12, 18), 2f);
        g.DrawPath(pen, path);
        g.DrawLine(pen, 9, 8, 15, 8);
        g.DrawLine(pen, 9, 12, 15, 12);
        g.DrawLine(pen, 9, 16, 13, 16);
    }

    private static void DrawFolder(Graphics g, Pen pen)
    {
        using GraphicsPath path = new();
        path.AddLines(new[]
        {
            new PointF(4, 19), new PointF(4, 6), new PointF(10, 6),
            new PointF(12, 8), new PointF(20, 8), new PointF(20, 19)
        });
        path.CloseFigure();
        g.DrawPath(pen, path);
    }

    private static void DrawSettings(Graphics g, Pen pen)
    {
        g.DrawEllipse(pen, 5f, 5f, 14f, 14f);
        g.DrawEllipse(pen, 9.5f, 9.5f, 5f, 5f);

        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            g.DrawLine(pen, 12 + (7 * cos), 12 + (7 * sin), 12 + (9.6f * cos), 12 + (9.6f * sin));
        }
    }

    private static void DrawClock(Graphics g, Pen pen)
    {
        g.DrawEllipse(pen, 4f, 4f, 16f, 16f);
        g.DrawLine(pen, 12, 12, 12, 7);
        g.DrawLine(pen, 12, 12, 16, 14);
    }

    private static void DrawEye(Graphics g, Pen pen, SolidBrush brush)
    {
        g.DrawArc(pen, 4f, 6f, 16f, 12f, 200, 140);
        g.DrawArc(pen, 4f, 6f, 16f, 12f, 20, 140);
        g.FillEllipse(brush, 10.6f, 10.6f, 2.8f, 2.8f);
    }

    private static void DrawShieldCheck(Graphics g, Pen pen)
    {
        using GraphicsPath path = new();
        path.AddLines(new[]
        {
            new PointF(12, 3), new PointF(19, 6), new PointF(19, 12),
            new PointF(12, 21), new PointF(5, 12), new PointF(5, 6)
        });
        path.CloseFigure();
        g.DrawPath(pen, path);
        g.DrawLines(pen, new[] { new PointF(8.5f, 12.5f), new PointF(11f, 15f), new PointF(16f, 9f) });
    }

    private static void DrawCode(Graphics g, Pen pen)
    {
        g.DrawLines(pen, new[] { new PointF(9, 7), new PointF(4, 12), new PointF(9, 17) });
        g.DrawLines(pen, new[] { new PointF(15, 7), new PointF(20, 12), new PointF(15, 17) });
    }

    private static void DrawPrinter(Graphics g, Pen pen)
    {
        using GraphicsPath body = AppTheme.RoundedRect(new RectangleF(4, 9, 16, 7), 1.5f);
        g.DrawPath(pen, body);
        g.DrawRectangle(pen, 8f, 3f, 8f, 6f);
        g.DrawRectangle(pen, 7f, 16f, 10f, 5f);
    }

    private static void DrawArrowLeft(Graphics g, Pen pen)
    {
        g.DrawLine(pen, 20, 12, 5, 12);
        g.DrawLines(pen, new[] { new PointF(10, 7), new PointF(5, 12), new PointF(10, 17) });
    }

    private static void DrawArrowRight(Graphics g, Pen pen)
    {
        g.DrawLine(pen, 4, 12, 19, 12);
        g.DrawLines(pen, new[] { new PointF(14, 7), new PointF(19, 12), new PointF(14, 17) });
    }

    private static void DrawTrash(Graphics g, Pen pen)
    {
        g.DrawLine(pen, 4, 7, 20, 7);
        g.DrawLines(pen, new[] { new PointF(9, 7), new PointF(9, 4), new PointF(15, 4), new PointF(15, 7) });

        using GraphicsPath body = AppTheme.RoundedRect(new RectangleF(6, 7, 12, 13), 1.5f);
        g.DrawPath(pen, body);
        g.DrawLine(pen, 10, 10, 10, 17.5f);
        g.DrawLine(pen, 14, 10, 14, 17.5f);
    }

    private static void DrawRefresh(Graphics g, Pen pen)
    {
        g.DrawArc(pen, 4f, 4f, 16f, 16f, -40, 300);
        g.DrawLines(pen, new[] { new PointF(15f, 3.5f), new PointF(19f, 5.5f), new PointF(16.5f, 9f) });
    }

    private static void DrawPlus(Graphics g, Pen pen)
    {
        g.DrawLine(pen, 12, 5, 12, 19);
        g.DrawLine(pen, 5, 12, 19, 12);
    }

    private static void DrawCheck(Graphics g, Pen pen)
    {
        g.DrawLines(pen, new[] { new PointF(5, 13), new PointF(10, 18), new PointF(19, 6) });
    }
}
