using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland;

public sealed class Scrubber : FrameworkElement
{
    const double KnobWidth = 22, KnobHeight = 14;
    const double PressedWider = 0.2, PressedTaller = 0.32;
    const double StretchSpeed = 420, StretchWider = 0.32, StretchFlatter = 0.1;
    const double LensZoom = 0.22, LensZoomTaller = 1.08;
    const double GlassClearing = 0.9, GlassShown = 0.01;
    const double RimThickness = 0.8;
    const double SettledShare = 0.05;
    const double ShadowLayers = 3, ShadowSpread = 0.6, PressedShadowSpread = 1.5;
    const double ShadowDrop = 0.6, PressedShadowDrop = 1.0, ShadowAlpha = 0.1, PressedShadowAlpha = 0.05;
    const double TrackHeight = 5, TrackInset = (KnobWidth - TrackHeight) / 2;
    const double TrackOpacity = 0.14, FillOpacity = 0.9;
    const double KnobGrab = 14, ReachBeyondEnds = 8;
    static readonly TimeSpan ShortestPress = TimeSpan.FromMilliseconds(140);

    readonly Spring _share = new(0, 520, 30);
    readonly Spring _press = new(0, 900, 38);
    readonly FrameLoop _loop;
    ButtonBase? _row;
    bool _dragging;
    long _pressedAt;
    double _grabbedShare, _grabbedAt, _travelOnScreen;

    public Scrubber()
    {
        _loop = new FrameLoop(Advance);
        Focusable = false;
        Loaded += (_, _) => AttachToRow();
        PreviewMouseLeftButtonDown += OnDirectMouseDown;
        PreviewMouseMove += OnDirectMouseMove;
        PreviewMouseLeftButtonUp += OnDirectMouseUp;
        LostMouseCapture += (_, _) => _dragging = false;
        PreviewMouseWheel += OnDirectMouseWheel;
    }

    public event EventHandler? Changed;

    public double Minimum { get; set; }

    public double Maximum { get; set; } = 1;

    public double Step { get; set; } = 1;

    public double Value { get; private set; }

    double EffectiveStep() => (Native.IsShiftDown || (Keyboard.Modifiers & ModifierKeys.Shift) != 0) ? 1.0 : Step;

    public void Set(double value, bool animate)
    {
        Value = value;
        if (animate)
        {
            _share.Target = ShareOf(value);
            _loop.Start();
        }
        else
        {
            _share.Snap(ShareOf(value));
            InvalidateVisual();
        }
    }

    void AttachToRow()
    {
        if (_row != null) return;
        for (DependencyObject? node = VisualTreeHelper.GetParent(this); node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not ButtonBase row) continue;
            _row = row;
            row.PreviewMouseLeftButtonDown += OnRowDown;
            row.PreviewMouseMove += OnRowMove;
            row.PreviewMouseLeftButtonUp += (_, _) => { _dragging = false; ReleaseMouseCapture(); };
            row.LostMouseCapture += (_, _) => _dragging = false;
            return;
        }
    }

    void OnDirectMouseDown(object sender, MouseButtonEventArgs e) => OnRowDown(sender, e);
    void OnDirectMouseMove(object sender, MouseEventArgs e) => OnRowMove(sender, e);
    void OnDirectMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    void OnDirectMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double step = EffectiveStep();
        double delta = e.Delta > 0 ? step : -step;
        double newValue = Math.Clamp(Value + delta, Minimum, Maximum);
        if (newValue != Value)
        {
            Set(newValue, true);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        e.Handled = true;
    }

    void OnRowDown(object sender, MouseButtonEventArgs e)
    {
        Point p = e.GetPosition(this);
        double x = p.X, y = p.Y, travel = Travel();
        if (x < -ReachBeyondEnds || x > ActualWidth + ReachBeyondEnds || y < -10 || y > ActualHeight + 10) return;

        CaptureMouse();
        bool onKnob = Math.Abs(x - KnobCenter(_share.Target)) <= KnobGrab;
        _dragging = true;
        _grabbedShare = onKnob ? _share.Target : Math.Clamp((x - KnobWidth / 2) / travel, 0, 1);
        _grabbedAt = OnScreen(x);
        _travelOnScreen = Math.Max(OnScreen(travel) - OnScreen(0), 1);
        _pressedAt = Stopwatch.GetTimestamp();
        _press.Tune(900, 38);
        _press.Target = 1;
        MoveTo(_grabbedShare);
        _loop.Start();
        e.Handled = true;
    }

    void OnRowMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
        MoveTo(_grabbedShare + (OnScreen(e.GetPosition(this).X) - _grabbedAt) / _travelOnScreen);
        e.Handled = true;
    }

    void MoveTo(double share)
    {
        double step = EffectiveStep();
        double value = Minimum + Math.Round(Math.Clamp(share, 0, 1) * (Maximum - Minimum) / step) * step;
        if (value == Value) return;

        Set(value, true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    double OnScreen(double x) => PointToScreen(new Point(x, 0)).X;

    double ShareOf(double value) => Math.Clamp((value - Minimum) / (Maximum - Minimum), 0, 1);

    double Travel() => Math.Max(1, ActualWidth - KnobWidth);

    double KnobCenter(double share) => KnobWidth / 2 + Travel() * share;

    bool Advance(double dt)
    {
        bool settled = Math.Abs(_share.Value - _share.Target) < SettledShare;
        if (!_dragging && _press.Target == 1 && settled && Stopwatch.GetElapsedTime(_pressedAt) >= ShortestPress)
        {
            _press.Tune(420, 17);
            _press.Target = 0;
        }

        bool moving = _share.Advance(dt) | _press.Advance(dt);
        InvalidateVisual();
        return moving || _press.Target == 1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double press = Math.Max(0, _press.Value);
        double stretch = Math.Min(1, Math.Abs(_share.Velocity) * Travel() / StretchSpeed);
        double knobWidth = KnobWidth * (1 + PressedWider * press) * (1 + StretchWider * stretch);
        double knobHeight = KnobHeight * (1 + PressedTaller * press) * (1 - StretchFlatter * stretch);
        double cx = KnobCenter(_share.Value), cy = h / 2, top = cy - TrackHeight / 2;

        var track = new RectangleGeometry(new Rect(TrackInset, top, w - TrackInset * 2, TrackHeight), TrackHeight / 2, TrackHeight / 2);
        var fill = new RectangleGeometry(new Rect(TrackInset, top, Math.Max(cx + TrackHeight / 2 - TrackInset, 0), TrackHeight), TrackHeight / 2, TrackHeight / 2);
        var knobRect = new Rect(cx - knobWidth / 2, cy - knobHeight / 2, knobWidth, knobHeight);
        var knob = new RectangleGeometry(knobRect, knobHeight / 2, knobHeight / 2);
        bool glass = press > GlassShown;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        dc.DrawGeometry(Shades.White(TrackOpacity), null, Geometry.Combine(track, knob, GeometryCombineMode.Exclude, null));
        dc.DrawGeometry(Shades.White(FillOpacity), null, Geometry.Combine(fill, knob, GeometryCombineMode.Exclude, null));
        DrawShadow(dc, knobRect, knob, press);

        dc.PushClip(knob);
        double zoom = 1 + LensZoom * (0.35 + 0.65 * press);
        dc.PushTransform(new ScaleTransform(zoom, zoom * LensZoomTaller, cx, cy));
        dc.DrawGeometry(Shades.White(TrackOpacity), null, track);
        dc.DrawGeometry(Shades.White(FillOpacity), null, fill);
        dc.Pop();

        // Liquid glass body fill (frosted pearlescent white)
        dc.DrawRectangle(new SolidColorBrush(White(0.88 - 0.45 * press)), null, knobRect);
        // Specular top light reflection
        dc.DrawRectangle(Vertical((0, 0.45 + 0.35 * press), (0.45, 0.08), (1, 0.20 + 0.15 * press)), null, knobRect);
        dc.Pop();

        // Specular bevel rim
        var rim = new Pen(Vertical((0, 0.75 + 0.20 * press), (0.5, 0.25 + 0.15 * press), (1, 0.50 + 0.25 * press)), RimThickness);
        var rimRect = Rect.Inflate(knobRect, -RimThickness / 2, -RimThickness / 2);
        dc.DrawRoundedRectangle(null, rim, rimRect, rimRect.Height / 2, rimRect.Height / 2);
    }

    static void DrawShadow(DrawingContext dc, Rect knobRect, Geometry knob, double press)
    {
        double spread = ShadowSpread + PressedShadowSpread * press, drop = ShadowDrop + PressedShadowDrop * press;
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * (ShadowAlpha + PressedShadowAlpha * press)), 0, 0, 0));
        for (int layer = 1; layer <= ShadowLayers; layer++)
        {
            Rect rect = Rect.Inflate(knobRect, spread * layer, spread * layer);
            rect.Offset(0, drop);
            var halo = new RectangleGeometry(rect, rect.Height / 2, rect.Height / 2);
            dc.DrawGeometry(brush, null, Geometry.Combine(halo, knob, GeometryCombineMode.Exclude, null));
        }
    }

    static LinearGradientBrush Vertical(params (double Offset, double Alpha)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        foreach ((double offset, double alpha) in stops) brush.GradientStops.Add(new GradientStop(White(alpha), offset));
        return brush;
    }

    static Color White(double alpha) => Color.FromArgb((byte)Math.Round(255 * Math.Clamp(alpha, 0, 1)), 255, 255, 255);
}
