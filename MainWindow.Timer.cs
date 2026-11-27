using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static DynamicIsland.Motion;

namespace DynamicIsland;

public partial class MainWindow
{
    const int MaxMinutes = 99;
    const int UrgentSeconds = 10;
    const double BellPivotY = -8;
    const double PausedOpacity = 0.5;
    const double RingNoticeSeconds = 12;
    static readonly TimeSpan RingPeriod = TimeSpan.FromSeconds(1.5);
    static readonly TimeSpan RingSwingTime = TimeSpan.FromSeconds(0.8);

    readonly Countdown _countdown = new();
    readonly Alarm _alarm = new();
    readonly SolidColorBrush _timerTint;
    readonly ScaleTransform _ringPulse = new(1, 1), _bigTimerPulse = new(1, 1);
    bool _ringing, _urgent;
    bool _pauseIconShown = true;
    int _setupMinutes = 25, _shownSeconds = -1;
    bool _bubbleHovered, _bubblePressed;

    void ApplyTimerTint()
    {
        TimerRing.Stroke = BubbleRing.Stroke = BigRing.Stroke = _timerTint;
        TimerText.Foreground = BubbleText.Foreground = BigTimer.Foreground = BigTimerLabel.Foreground = MenuTimer.Foreground = _timerTint;
        TimerDisc.Fill = _timerTint;
        TimerPauseIcon.Fill = TimerPauseIcon.Stroke = TimerPlayIcon.Fill = TimerPlayIcon.Stroke = _timerTint;
        TimerRing.RenderTransformOrigin = BubbleRing.RenderTransformOrigin = BigRing.RenderTransformOrigin = new Point(0.5, 0.5);
        TimerRing.RenderTransform = BubbleRing.RenderTransform = BigRing.RenderTransform = _ringPulse;
        BigTimer.RenderTransformOrigin = new Point(1, 0.5);
        BigTimer.RenderTransform = _bigTimerPulse;
    }

    void StartTimer(TimeSpan total)
    {
        _countdown.Start(total);
        _timerBubbleWidth.Target = total.TotalHours >= 1 ? 98 : TimerBubbleWidth;
        _timerBubbleWidth.Snap(_timerBubbleWidth.Target);
        BigTimerLabel.Text = "Таймер · " + DescribeDuration(total);
        _panel = Panel.None;
        SyncTimer();
        UpdateView();
        UpdateTargets();
    }

    void StopTimer()
    {
        _countdown.Stop();
        MenuTimer.Text = "";
        SetUrgent(false);
    }

    static string DescribeDuration(TimeSpan t)
    {
        if (t.TotalHours >= 1)
        {
            int h = (int)t.TotalHours;
            int m = t.Minutes;
            return m > 0 ? $"{h} ч {m} мин" : $"{h} ч";
        }
        return t.TotalSeconds >= 60 ? (int)Math.Round(t.TotalMinutes) + " мин" : (int)t.TotalSeconds + " с";
    }

    void SyncTimer()
    {
        bool running = _countdown.IsRunning;
        if (running != _pauseIconShown)
        {
            _pauseIconShown = running;
            SwapIcons(running ? TimerPlayIcon : TimerPauseIcon, running ? TimerPauseIcon : TimerPlayIcon, TimerBigView.IsVisible);
        }
        TimerText.Opacity = BubbleText.Opacity = BigTimer.Opacity = running ? 1 : PausedOpacity;
        _shownSeconds = -1;
        UpdateTimer();
    }

    static string FormatTimerSeconds(int seconds)
    {
        if (seconds >= 3600)
            return $"{seconds / 3600}:{(seconds % 3600) / 60:00}:{seconds % 60:00}";
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    void UpdateTimer()
    {
        if (!_countdown.IsActive) return;
        TimeSpan left = _countdown.Remaining;
        if (left <= TimeSpan.Zero)
        {
            OnTimerFinished();
            return;
        }

        double share = _countdown.RemainingShare;
        double targetW = left.TotalHours >= 1 ? 98 : TimerBubbleWidth;
        if (_timerBubbleWidth.Target != targetW)
        {
            _timerBubbleWidth.Target = targetW;
            if (_bubbleSplit.Value < BarelyVisible) _timerBubbleWidth.Snap(targetW);
        }
        TimerRing.Progress = BubbleRing.Progress = share;
        BigRing.BeginAnimation(Ring.ProgressProperty, new DoubleAnimation(share, TimerBigView.IsVisible ? TickInterval : TimeSpan.Zero));
        int seconds = (int)Math.Ceiling(left.TotalSeconds);
        if (seconds == _shownSeconds) return;
        _shownSeconds = seconds;
        TimerText.Text = BubbleText.Text = BigTimer.Text = MenuTimer.Text = FormatTimerSeconds(seconds);

        SetUrgent(seconds <= UrgentSeconds);
        if (_urgent && _countdown.IsRunning) PulseTimer();
    }

    void SetUrgent(bool on)
    {
        if (on == _urgent) return;
        _urgent = on;
        _timerTint.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((on ? _red : _orange).Color, Ms(300)));
        SyncRim();
    }

    void PulseTimer()
    {
        Pulse(_ringPulse, 1.24);
        Pulse(_bigTimerPulse, 1.05);
    }

    static void Pulse(ScaleTransform scale, double to)
    {
        var beat = new DoubleAnimationUsingKeyFrames { Duration = Ms(460) };
        beat.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110)),
            new CubicEase { EasingMode = EasingMode.EaseOut }));
        beat.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(460)),
            new SineEase { EasingMode = EasingMode.EaseInOut }));
        scale.AnimateScale(beat);
    }

    void OnTimerFinished()
    {
        string total = DescribeDuration(_countdown.Total);

        if (Settings.TimerIntervalMode)
        {
            _countdown.Start(_countdown.Total);
            _alarm.Ring();
            Notify(Glyph.Bell, _orange, "Интервал таймера", "Новый цикл · " + total, 4, true);
            return;
        }

        StopTimer();
        _ringing = true;
        _alarm.Ring();

        Notify(Glyph.Bell, _orange, "Таймер", "Время вышло · " + total, RingNoticeSeconds, true);
        NoticeTurn.CenterY = BellPivotY;
        NoticeTurn.BeginAnimation(RotateTransform.AngleProperty, Rounds(Sway(TimeSpan.Zero, RingSwingTime, 24, -22, 17, -13, 8, -4, 0)));
        RootMove.BeginAnimation(TranslateTransform.XProperty, Rounds(Sway(TimeSpan.Zero, RingSwingTime * 0.7, -3.5, 3.5, -3, 3, -2, 1.5, -1, 0)));
        Body.StartFlashing(_orange.Color, RingPeriod);
        UpdateTargets();

        static DoubleAnimationUsingKeyFrames Rounds(DoubleAnimationUsingKeyFrames once)
        {
            once.Duration = RingPeriod;
            once.RepeatBehavior = RepeatBehavior.Forever;
            return once;
        }
    }

    void SilenceAlarm()
    {
        if (!_ringing) return;
        _ringing = false;
        _alarm.Stop();
        Body.StopFlashing();
        NoticeTurn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, Ms(160)));
        RootMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, Ms(160)));
    }

    void SetMinutes(int minutes)
    {
        _setupMinutes = Math.Clamp(minutes, 1, MaxMinutes);
        if (_setupMinutes >= 60)
        {
            int h = _setupMinutes / 60;
            int m = _setupMinutes % 60;
            SetupText.Text = $"{h}:{m:00}:00";
            SetupText.FontSize = 32;
            SetupDurationText.Text = m > 0 ? $"{h} ч {m} мин" : $"{h} ч";
            SetupDurationText.Visibility = Visibility.Visible;
        }
        else
        {
            SetupText.Text = _setupMinutes + ":00";
            SetupText.FontSize = 38;
            SetupDurationText.Text = $"{_setupMinutes} мин";
            SetupDurationText.Visibility = Visibility.Collapsed;
        }
        SyncTimerPresets();
    }

    void SyncTimerPresets()
    {
        string active = _setupMinutes.ToString();
        Brush activeBack = Shades.Frozen(new SolidColorBrush(_orange.Color.WithAlpha(0.24)));
        foreach (Button chip in TimerPresets.Children.OfType<Button>())
        {
            bool picked = chip.Tag as string == active;
            if (picked) chip.Background = activeBack;
            else chip.ClearValue(BackgroundProperty);
            chip.Foreground = picked ? _orange : Brushes.White;
            chip.FontWeight = picked ? FontWeights.SemiBold : FontWeights.Normal;
        }

        TimerIntervalText.Text = Settings.TimerIntervalMode ? "Повтор: Вкл" : "Повтор: Выкл";
        TimerIntervalText.Foreground = Settings.TimerIntervalMode ? _orange : (Brush)FindResource("Dim");
        TimerOvertimeText.Text = Settings.TimerOvertime ? "Овертайм: Вкл" : "Овертайм: Выкл";
        TimerOvertimeText.Foreground = Settings.TimerOvertime ? _orange : (Brush)FindResource("Dim");
    }

    void TimerInterval_Click(object sender, RoutedEventArgs e)
    {
        Settings.TimerIntervalMode = !Settings.TimerIntervalMode;
        SyncTimerPresets();
    }

    void TimerOvertime_Click(object sender, RoutedEventArgs e)
    {
        Settings.TimerOvertime = !Settings.TimerOvertime;
        SyncTimerPresets();
    }

    void TimerRow_Click(object sender, RoutedEventArgs e) => ShowPanel(_countdown.IsActive ? Panel.Timer : Panel.TimerSet);

    void TimerLess_Click(object sender, RoutedEventArgs e) => SetMinutes(_setupMinutes - 1);

    void TimerMore_Click(object sender, RoutedEventArgs e) => SetMinutes(_setupMinutes + 1);

    void TimerPreset_Click(object sender, RoutedEventArgs e) => SetMinutes(int.Parse((string)((Button)sender).Tag));

    void TimerStart_Click(object sender, RoutedEventArgs e) => StartTimer(TimeSpan.FromMinutes(_setupMinutes));

    void TimerToggle_Click(object sender, RoutedEventArgs e)
    {
        _countdown.Toggle();
        SyncTimer();
    }

    void TimerCancel_Click(object sender, RoutedEventArgs e)
    {
        StopTimer();
        ShowPanel(Panel.None);
        UpdateTargets();
    }

    Panel BubblePanelAt(double x)
    {
        var parts = new List<(double Width, Panel Panel)>();
        if (_bubbleRecord.Target > 0) parts.Add((_recordBubbleWidth.Target, Panel.Record));
        if (_bubbleTimer.Target > 0) parts.Add((_timerBubbleWidth.Target, Panel.Timer));
        if (_bubbleShelf.Target > 0) parts.Add((_shelfBubbleWidth.Value, Panel.Shelf));
        double end = 0;
        foreach ((double width, Panel panel) in parts.SkipLast(1))
        {
            end += width - BubbleOverlap;
            if (x < end + BubbleOverlap / 2) return panel;
        }
        return parts.Count > 0 ? parts[^1].Panel : Panel.None;
    }

    void Bubble_MouseEnter(object sender, MouseEventArgs e)
    {
        _bubbleHovered = true;
        UpdateTargets();
    }

    void Bubble_MouseLeave(object sender, MouseEventArgs e)
    {
        _bubbleHovered = _bubblePressed = false;
        UpdateTargets();
    }

    void Bubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _bubblePressed = true;
        UpdateTargets();
    }

    void Bubble_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_bubblePressed) return;
        e.Handled = true;
        _bubblePressed = false;
        SelectPanel(BubblePanelAt(e.GetPosition(Bubble).X));
        UpdateView();
        UpdateTargets();
        _collapseTimeout.Start(LongCollapseDelay);
    }
}
