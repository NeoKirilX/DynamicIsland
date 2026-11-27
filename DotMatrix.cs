using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIsland;

public sealed class DotMatrix : FrameworkElement
{
    const int ColorSteps = 24;
    int Rows => Math.Clamp(Settings.MatrixRows, 4, 20);
    double ColumnPitch => Settings.MatrixDensity switch
    {
        MatrixDensity.Sparse => 10.0,
        MatrixDensity.Dense => 6.5,
        _ => 8.0
    };
    const double RowPitch = 6.5, DotRadius = 1.5, Bottom = 7, SideInset = 6;
    const double EdgeClearance = 3;
    const double LitReach = 1.1;
    const double UnlitOpacity = 0.08, HeadWhiteness = 0.5;
    double LitOpacity => Math.Clamp(Settings.MatrixOpacity / 100.0, 0.3, 1.0);
    const double Absent = 0.01;

    readonly record struct Column(double X, double Share, int LowestRow);

    readonly SolidColorBrush[] _brushes = new SolidColorBrush[ColorSteps];
    readonly Shades[] _shades = new Shades[ColorSteps];
    SpectrumLevels? _levels;
    Column[] _columns = [];
    Geometry? _unlit;
    Size _laidOut;
    double _rounding;

    public DotMatrix()
    {
        for (int step = 0; step < ColorSteps; step++)
        {
            _brushes[step] = new SolidColorBrush(Colors.White);
            _shades[step] = new Shades();
        }
        _brushes[0].Changed += (_, _) => InvalidateVisual();
    }

    public double Rounding
    {
        get => _rounding;
        set
        {
            if (value == _rounding) return;
            _rounding = value;
            _unlit = null;
            InvalidateVisual();
        }
    }

    public void Tint(IReadOnlyList<Color> colors, Duration time)
    {
        for (int step = 0; step < ColorSteps; step++)
            _brushes[step].BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Along(colors, (double)step / (ColorSteps - 1)), time));
    }

    public bool Tick(SpectrumLevels levels)
    {
        _levels = levels;
        if (levels.Moved) InvalidateVisual();
        return levels.Moved;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        dc.DrawGeometry(Shades.White(UnlitOpacity), null, LayOut(w, h));
        if (_levels == null) return;

        foreach (Column column in _columns)
        {
            double lit = _levels.At(column.Share) * Rows * LitReach;
            int step = (int)Math.Round(column.Share * (ColorSteps - 1));
            Color color = _brushes[step].Color;
            int rows = Rows;
            for (int row = column.LowestRow; row < rows && lit - row > Absent; row++)
            {
                var center = new Point(column.X, h - Bottom - row * RowPitch);
                double on = Math.Clamp(lit - row, 0, 1), head = on * (1 - Math.Clamp(lit - row - 1, 0, 1));
                double fade = Settings.MatrixFadeStrength > 0 ? Math.Clamp(1.0 - ((double)row / Math.Max(rows - 1, 1)) * (Settings.MatrixFadeStrength / 100.0), 0, 1) : 1.0;

                dc.DrawEllipse(_shades[step].Of(color, LitOpacity * on * fade), null, center, DotRadius, DotRadius);
                if (head > Absent) dc.DrawEllipse(Shades.White(HeadWhiteness * head * fade), null, center, DotRadius, DotRadius);
            }
        }
    }

    Geometry LayOut(double w, double h)
    {
        var size = new Size(w, h);
        if (_unlit != null && _laidOut == size) return _unlit;

        int count = (int)((w - 2 * SideInset) / ColumnPitch) + 1;
        if (count % 2 == 0) count--;
        double left = (w - (count - 1) * ColumnPitch) / 2, middle = (count - 1) / 2.0;
        Geometry outline = Squircle.Of(new Rect(0, 0, w, h), _rounding);

        var columns = new List<Column>();
        var dots = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (StreamGeometryContext outlines = dots.Open())
        {
            for (int i = 0; i < count; i++)
            {
                double x = left + i * ColumnPitch;
                int lowest = Rows;
                for (int row = Rows - 1; row >= 0; row--)
                {
                    var center = new Point(x, h - Bottom - row * RowPitch);
                    var room = new EllipseGeometry(center, DotRadius + EdgeClearance, DotRadius + EdgeClearance);
                    if (outline.FillContainsWithDetail(room) != IntersectionDetail.FullyContains) break;
                    AddDot(outlines, center);
                    lowest = row;
                }
                if (lowest < Rows) columns.Add(new Column(x, Math.Abs(i - middle) / middle, lowest));
            }
        }
        dots.Freeze();

        _columns = [.. columns];
        _laidOut = size;
        return _unlit = dots;
    }

    static void AddDot(StreamGeometryContext outlines, Point center)
    {
        var size = new Size(DotRadius, DotRadius);
        Point left = center - new Vector(DotRadius, 0), right = center + new Vector(DotRadius, 0);
        outlines.BeginFigure(left, true, true);
        outlines.ArcTo(right, size, 0, false, SweepDirection.Clockwise, true, false);
        outlines.ArcTo(left, size, 0, false, SweepDirection.Clockwise, true, false);
    }

    static Color Along(IReadOnlyList<Color> colors, double share)
    {
        if (colors.Count == 1) return colors[0];
        double at = share * (colors.Count - 1);
        int low = Math.Min((int)at, colors.Count - 2);
        Color from = colors[low], to = colors[low + 1];
        double part = at - low;
        return Color.FromRgb(Mix(from.R, to.R, part), Mix(from.G, to.G, part), Mix(from.B, to.B, part));
    }

    static byte Mix(byte from, byte to, double share) => (byte)Math.Round(from + (to - from) * share);
}
