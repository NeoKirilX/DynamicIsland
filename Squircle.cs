using System.Windows;
using System.Windows.Media;

namespace DynamicIsland;

static class Squircle
{
    const double Smoothing = 0.6;

    public static Geometry Of(Rect rect, double radius)
    {
        double room = Math.Min(rect.Width, rect.Height) / 2;
        double r = Math.Min(radius, room);
        if (r <= 0.01) return new RectangleGeometry(rect);

        double smooth = Math.Clamp(room / r - 1, 0, Smoothing);
        double reach = Math.Min((1 + smooth) * r, room);
        double sweep = Math.PI / 2 * (1 - smooth);
        double arc = Math.Sin(sweep / 2) * r * Math.Sqrt(2);
        double lean = Math.PI / 4 * smooth;
        double c = r * Math.Tan(lean / 2) * Math.Cos(lean), d = c * Math.Tan(lean);
        double b = (reach - arc - c - d) / 3, a = 2 * b;

        var start = new Point(rect.Left + rect.Width / 2, rect.Top);
        var outline = new StreamGeometry();
        using (StreamGeometryContext g = outline.Open())
        {
            g.BeginFigure(start, true, true);
            Corner(new Point(rect.Right - reach, rect.Top), new Vector(1, 0), new Vector(0, 1));
            Corner(new Point(rect.Right, rect.Bottom - reach), new Vector(0, 1), new Vector(-1, 0));
            Corner(new Point(rect.Left + reach, rect.Bottom), new Vector(-1, 0), new Vector(0, -1));
            Corner(new Point(rect.Left, rect.Top + reach), new Vector(0, -1), new Vector(1, 0));
            g.LineTo(start, true, false);

            void Corner(Point from, Vector along, Vector turn)
            {
                Point At(Point p, double x, double y) => p + along * x + turn * y;

                g.LineTo(from, true, false);
                Point p1 = At(from, a + b + c, d), p2 = At(p1, arc, arc);
                if (smooth > 0.001) g.BezierTo(At(from, a, 0), At(from, a + b, 0), p1, true, true);
                g.ArcTo(p2, new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);
                if (smooth > 0.001) g.BezierTo(At(p2, d, c), At(p2, d, b + c), At(p2, d, a + b + c), true, true);
            }
        }
        outline.Freeze();
        return outline;
    }

    public static Geometry Notch(Rect rect, double radius, double earSize = 16)
    {
        double x = rect.X;
        double y = rect.Y;
        double w = rect.Width;
        double h = rect.Height;
        if (w <= 0.01 || h <= 0.01) return Geometry.Empty;

        // Authentic iPhone notch proportions:
        // - Wide top cutout flush with screen edge
        // - Organic Bezier ear curves sweeping outward and upward at the top corners
        // - Smooth bottom corners with soft radii
        double earW = Math.Max(earSize, 16.0);
        double earH = Math.Min(h * 0.55, 15.0);
        double bottomR = Math.Min(radius > 0 ? radius : 20.0, Math.Min(w / 3.0, h * 0.7));
        double topBleed = 6.0; // Ensure it bleeds off the top screen edge cleanly
        double topY = y - topBleed;

        var outline = new StreamGeometry();
        using (StreamGeometryContext g = outline.Open())
        {
            // Start at top-left ear outer extension
            g.BeginFigure(new Point(x - earW, topY), true, true);
            // Top flush edge across entire notch width
            g.LineTo(new Point(x + w + earW, topY), true, false);

            // Right ear: S-curve transition from top bezel down into vertical notch wall
            g.BezierTo(
                new Point(x + w + earW * 0.42, topY),
                new Point(x + w, y + earH * 0.38),
                new Point(x + w, y + earH),
                true, true
            );

            // Right vertical notch edge
            g.LineTo(new Point(x + w, y + h - bottomR), true, false);

            // Bottom-right rounded corner (squircle-like smooth arc)
            g.ArcTo(new Point(x + w - bottomR, y + h), new Size(bottomR, bottomR), 0, false, SweepDirection.Clockwise, true, true);

            // Bottom horizontal edge
            g.LineTo(new Point(x + bottomR, y + h), true, false);

            // Bottom-left rounded corner
            g.ArcTo(new Point(x, y + h - bottomR), new Size(bottomR, bottomR), 0, false, SweepDirection.Clockwise, true, true);

            // Left vertical notch edge
            g.LineTo(new Point(x, y + earH), true, false);

            // Left ear: S-curve transition from vertical notch wall up into top bezel
            g.BezierTo(
                new Point(x, y + earH * 0.38),
                new Point(x - earW * 0.42, topY),
                new Point(x - earW, topY),
                true, true
            );
        }
        outline.Freeze();
        return outline;
    }
}
