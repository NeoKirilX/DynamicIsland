using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static DynamicIsland.Motion;

namespace DynamicIsland;

public partial class MainWindow
{
    const double UpdatePagePadding = 14, UpdateNotesWheelStep = 54;
    const int MegabyteShift = 20;
    const double LookHeight = 430, LookTilesHeight = 72, LookWheelStep = 80;
    const double LookEdgeZone = 70, LookEdgeScale = 0.8;
    const int LargestScale = 130, ScaleStep = 5;
    const int LargestGap = 24, GapStep = 2;

    const int PointerLyricOff = 0, FirstPointerLyricShape = 1;

    Border? _openLookTiles;
    readonly Spring _settingsScroll = new(0, 260, 30);
    readonly Spring _lookScroll = new(0, 260, 30);
    readonly Spring _updateNotesScroll = new(0, 260, 30);

    void SettingsRow_Click(object sender, RoutedEventArgs e) => ShowPanelAndCheckUpdate(Panel.Settings);

    void UpdateRow_Click(object sender, RoutedEventArgs e) => ShowPanelAndCheckUpdate(Panel.Update);

    void UpdateBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Settings);

    void LookRow_Click(object sender, RoutedEventArgs e)
    {
        _lookScroll.Snap(0);
        ShowPanel(Panel.Look);
    }

    void SettingsBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Menu);

    void Exit_Click(object sender, RoutedEventArgs e) => Exit();

    void ShowPanelAndCheckUpdate(Panel panel)
    {
        ShowPanel(panel);
        _ = _updater.CheckAsync();
    }

    void Autostart_Click(object sender, RoutedEventArgs e)
    {
        try { Autostart.Set(!Autostart.Enabled); }
        catch (Exception ex) { App.Log(ex); }
        UpdateSwitches(true);
    }

    void Lyrics_Click(object sender, RoutedEventArgs e)
    {
        Settings.Lyrics = !Settings.Lyrics;
        UpdateSwitches(true);
        TrackLyrics();
    }

    void LyricsRow_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Lyrics);

    void LyricsBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Settings);

    void NotificationsRow_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Notifications);

    void NotificationsBack_Click(object sender, RoutedEventArgs e) => ShowPanel(Panel.Settings);

    void InterceptNotifs_Click(object sender, RoutedEventArgs e)
    {
        Settings.InterceptWindowsNotifications = !Settings.InterceptWindowsNotifications;
        UpdateSwitches(true);
        if (Settings.InterceptWindowsNotifications)
        {
            _notificationInterceptor ??= new NotificationInterceptor(Dispatcher, (title, body) =>
            {
                if (!Settings.InterceptWindowsNotifications) return;
                Notify(Glyph.Bell, Brushes.White, string.IsNullOrEmpty(title) ? "Уведомление" : title, body);
            });
            _ = _notificationInterceptor.StartAsync();
        }
    }

    void NotifyDnd_Click(object sender, RoutedEventArgs e)
    {
        Settings.NotifyDnd = !Settings.NotifyDnd;
        UpdateSwitches(true);
    }

    void NotifyHeadphones_Click(object sender, RoutedEventArgs e)
    {
        Settings.NotifyHeadphones = !Settings.NotifyHeadphones;
        UpdateSwitches(true);
    }

    void NotifyBattery_Click(object sender, RoutedEventArgs e)
    {
        Settings.NotifyBattery = !Settings.NotifyBattery;
        UpdateSwitches(true);
    }

    void NotifyVpn_Click(object sender, RoutedEventArgs e)
    {
        Settings.NotifyVpn = !Settings.NotifyVpn;
        UpdateSwitches(true);
    }

    void NotifyDrive_Click(object sender, RoutedEventArgs e)
    {
        Settings.NotifyDrive = !Settings.NotifyDrive;
        UpdateSwitches(true);
    }

    void NotifyTimer_Click(object sender, RoutedEventArgs e)
    {
        Settings.NotifyTimer = !Settings.NotifyTimer;
        UpdateSwitches(true);
    }

    void NotifyObs_Click(object sender, RoutedEventArgs e)
    {
        Settings.NotifyObs = !Settings.NotifyObs;
        UpdateSwitches(true);
    }

    void NotifyShelf_Click(object sender, RoutedEventArgs e)
    {
        Settings.NotifyShelf = !Settings.NotifyShelf;
        UpdateSwitches(true);
    }

    void Source_Click(object sender, RoutedEventArgs e)
    {
        var picked = (LyricSource)(1 << SourceChips.Children.IndexOf((UIElement)sender));
        LyricSource left = Settings.LyricSources ^ picked;
        if (left != 0)
        {
            Settings.LyricSources = left;
            TrackLyrics();
        }
        UpdateSwitches(true);
    }

    void LyricEffects_Click(object sender, RoutedEventArgs e)
    {
        Settings.LyricEffects = !Settings.LyricEffects;
        UpdateSwitches(true);
        RebuildPlayerLyrics();
    }

    void LyricParting_Click(object sender, RoutedEventArgs e)
    {
        Settings.LyricParting = !Settings.LyricParting;
        UpdateSwitches(true);
        StirLyrics();
    }

    void Rim_Click(object sender, RoutedEventArgs e)
    {
        Settings.Rim = !Settings.Rim;
        UpdateSwitches(true);
        RefreshLookPage();
        SyncRim();
    }

    void AppVolume_Click(object sender, RoutedEventArgs e)
    {
        Settings.AppVolume = !Settings.AppVolume;
        UpdateSwitches(true);
    }

    void Network_Click(object sender, RoutedEventArgs e)
    {
        Settings.Network = !Settings.Network;
        UpdateSwitches(true);
    }

    void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        Settings.HideFullscreen = !Settings.HideFullscreen;
        UpdateSwitches(true);
        CheckFullscreen();
    }

    void Shadow_Click(object sender, RoutedEventArgs e)
    {
        Settings.Shadow = !Settings.Shadow;
        UpdateSwitches(true);
        StartShapeLoop();
    }

    void SystemStats_Click(object sender, RoutedEventArgs e)
    {
        Settings.SystemStats = !Settings.SystemStats;
        UpdateSwitches(true);
    }

    void ClickLock_Click(object sender, RoutedEventArgs e)
    {
        Settings.ClickLock = !Settings.ClickLock;
        UpdateSwitches(true);
    }

    void Capitalize_Click(object sender, RoutedEventArgs e)
    {
        Settings.CapitalizeTitle = !Settings.CapitalizeTitle;
        UpdateSwitches(true);
    }

    void Obs_Click(object sender, RoutedEventArgs e)
    {
        Settings.Obs = !Settings.Obs;
        UpdateSwitches(true);
    }

    void Language_Click(object sender, RoutedEventArgs e)
    {
        OpenLanguageModal();
    }

    void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        ConfigService.ExportWithDialog();
    }

    void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigService.ImportWithDialog())
        {
            UpdateSwitches(true);
            RefreshLookPage();
            ApplyLook();
        }
    }

    void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string cacheDir = System.IO.Path.Combine(appData, "DynamicIsland", "Cache");
            if (System.IO.Directory.Exists(cacheDir))
            {
                System.IO.Directory.Delete(cacheDir, true);
                System.IO.Directory.CreateDirectory(cacheDir);
            }
        }
        catch { }
    }

    void Combo_Click(object sender, RoutedEventArgs e)
    {
        Settings.ComboEnabled = !Settings.ComboEnabled;
        UpdateSwitches(true);
    }

    void ComboStyle_Click(object sender, RoutedEventArgs e)
    {
        int total = Enum.GetValues<ComboCounterStyle>().Length;
        Settings.ComboCounterStyle = (ComboCounterStyle)(((int)Settings.ComboCounterStyle + 1) % total);
        UpdateSwitches(true);
    }

    double SettingsOverflow => Math.Max(SettingsRows.ActualHeight + SettingsRows.Margin.Top + SettingsRows.Margin.Bottom - SettingsView.Height, 0);

    void SettingsRows_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _settingsScroll.Target = Math.Clamp(_settingsScroll.Target, 0, SettingsOverflow);
        StartShapeLoop();
    }

    void ScrollSettings(int notches)
    {
        _settingsScroll.Target = Math.Clamp(_settingsScroll.Target + notches * LookWheelStep, 0, SettingsOverflow);
        StartShapeLoop();
    }

    void ShrinkSettingsEdges(double scrolled)
    {
        double top = Math.Clamp(scrolled / LookEdgeZone, 0, 1), bottom = Math.Clamp((SettingsOverflow - scrolled) / LookEdgeZone, 0, 1);
        foreach (FrameworkElement row in SettingsRows.Children)
        {
            double middle = VisualTreeHelper.GetOffset(row).Y + SettingsRows.Margin.Top + row.ActualHeight / 2 - scrolled;
            double fromTop = Math.Clamp(middle / LookEdgeZone, 0, 1), fromBottom = Math.Clamp((SettingsView.Height - middle) / LookEdgeZone, 0, 1);
            double shrink = Math.Max(top * Smooth(1 - fromTop), bottom * Smooth(1 - fromBottom));

            if (row.RenderTransform is not ScaleTransform size)
            {
                row.RenderTransform = size = new ScaleTransform();
                row.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            size.ScaleX = size.ScaleY = 1 - (1 - LookEdgeScale) * shrink;
        }
    }

    void ApplySettingsScroll()
    {
        double scrolled = _settingsScroll.Value;
        SettingsEdgeTop.Color = EdgeFade(scrolled);
        SettingsEdgeBottom.Color = EdgeFade(SettingsOverflow - scrolled);
        if (SettingsView.IsVisible) ShrinkSettingsEdges(scrolled);
        if (SettingsMove.Y == -scrolled) return;
        SettingsMove.Y = -scrolled;
        if (_view == View.Settings) SettingsRows.FollowPointer();
    }

    void UpdateSwitches(bool animate)
    {
        LyricsState.Text = Settings.Lyrics ? "Вкл" : "Выкл";
        LyricsSwitch.Set(Settings.Lyrics, animate);
        LyricEffectsSwitch.Set(Settings.LyricEffects, animate);
        PointerLyricSegments.Set(Settings.PointerLyric ? FirstPointerLyricShape + (int)Settings.PointerLyricShape : PointerLyricOff, animate);
        LyricPartingSwitch.Set(Settings.LyricParting, animate);
        ComboSwitch.Set(Settings.ComboEnabled, animate);
        ComboStyleText.Text = Settings.ComboCounterStyle switch
        {
            ComboCounterStyle.EnX => "x1",
            ComboCounterStyle.Times => "×1",
            ComboCounterStyle.Brackets => "(1)",
            ComboCounterStyle.Square => "[1]",
            ComboCounterStyle.Hash => "#1",
            ComboCounterStyle.Progress => "1/4",
            _ => "х1",
        };
        ShadowSwitch.Set(Settings.Shadow, animate);
        SystemStatsSwitch.Set(Settings.SystemStats, animate);
        ClickLockSwitch.Set(Settings.ClickLock, animate);
        CapitalizeSwitch.Set(Settings.CapitalizeTitle, animate);
        ObsSwitch.Set(Settings.Obs, animate);
        LanguageText.Text = Settings.Language.ToUpperInvariant();
        AppVolumeSwitch.Set(Settings.AppVolume, animate);
        NetworkSwitch.Set(Settings.Network, animate);
        WeatherSwitch.Set(Settings.Weather, animate);
        FullscreenSwitch.Set(Settings.HideFullscreen, animate);
        AutostartSwitch.Set(Autostart.Enabled, animate);
        InterceptNotifsSwitch.Set(Settings.InterceptWindowsNotifications, animate);
        NotifyDndSwitch.Set(Settings.NotifyDnd, animate);
        NotifyHeadphonesSwitch.Set(Settings.NotifyHeadphones, animate);
        NotifyBatterySwitch.Set(Settings.NotifyBattery, animate);
        NotifyVpnSwitch.Set(Settings.NotifyVpn, animate);
        NotifyDriveSwitch.Set(Settings.NotifyDrive, animate);
        NotifyTimerSwitch.Set(Settings.NotifyTimer, animate);
        NotifyObsSwitch.Set(Settings.NotifyObs, animate);
        NotifyShelfSwitch.Set(Settings.NotifyShelf, animate);

        for (int i = 0; i < SourceChips.Children.Count; i++)
            ((ToggleButton)SourceChips.Children[i]).IsChecked = Settings.LyricSources.HasFlag((LyricSource)(1 << i));
    }

    async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_updater.State != Updater.Stage.Available) await _updater.CheckAsync(true);
        else if (await _updater.InstallAsync()) Exit();
    }

    void RefreshUpdatePage()
    {
        Updater.Stage stage = _updater.State;
        bool loading = stage == Updater.Stage.Loading, found = loading || stage == Updater.Stage.Available;
        Version version = found ? _updater.LatestVersion! : Updater.CurrentVersion;

        UpdateText.Foreground = found ? _orange : _dim;
        UpdateText.Text = loading ? _updater.Percent + "%" : "v" + version;

        UpdateVersion.Text = version.ToString();
        UpdateFrom.Text = stage switch
        {
            Updater.Stage.Checking => "проверяю…",
            Updater.Stage.Latest => "последняя версия",
            Updater.Stage.Failed => "не получилось",
            _ => found ? "вместо " + Updater.CurrentVersion : "",
        };
        UpdateNotes.SetVisible(found && _updater.Notes.Length > 0);
        string notes = string.Join('\n', _updater.Notes.Select(note => "·  " + note));
        if (UpdateNotesText.Text != notes) _updateNotesScroll.Snap(0);
        UpdateNotesText.Text = notes;

        UpdateButton.SetVisible(!loading);
        UpdateLoad.SetVisible(loading);
        UpdateButton.Content = stage switch
        {
            Updater.Stage.Available => "Обновить и перезапустить",
            Updater.Stage.Checking => "Проверяю…",
            Updater.Stage.Failed => "Попробовать ещё раз",
            _ => "Проверить ещё раз",
        };
        if (found) UpdateButton.Background = _orange;
        else UpdateButton.ClearValue(BackgroundProperty);
        UpdateButton.Foreground = found ? Brushes.Black : Brushes.White;

        if (loading) ShowUpdateProgress();
        FitUpdatePage();
        UpdateView();
    }

    void ShowUpdateProgress()
    {
        UpdateBytes.Text = $"{_updater.DownloadedBytes >> MegabyteShift} из {_updater.TotalBytes >> MegabyteShift} МБ";
        LoadingText.Text = _updater.Percent + "%";
        var fill = new DoubleAnimation(_updater.Percent / 100.0, Ms(200));
        UpdateRing.BeginAnimation(Ring.ProgressProperty, fill);
        LoadingRing.BeginAnimation(Ring.ProgressProperty, fill);
    }

    void FitUpdatePage()
    {
        UpdateBody.Measure(new Size(UpdatePage.Width, double.PositiveInfinity));
        double height = Math.Ceiling(UpdateBody.DesiredSize.Height) + UpdatePagePadding;
        if (height == UpdatePage.Height) return;

        UpdatePage.Height = height;
        if (_view == View.Update) UpdateTargets();
    }

    double UpdateNotesOverflow => Math.Max(UpdateNotesText.ActualHeight - UpdateNotesBox.ActualHeight, 0);

    void ScrollUpdateNotes(int notches)
    {
        _updateNotesScroll.Target = Math.Clamp(_updateNotesScroll.Target + notches * UpdateNotesWheelStep, 0, UpdateNotesOverflow);
        StartShapeLoop();
    }

    void ApplyUpdateNotesScroll()
    {
        double scrolled = Math.Min(_updateNotesScroll.Value, UpdateNotesOverflow);
        UpdateNotesMove.Y = -scrolled;
        UpdateNotesEdgeTop.Color = EdgeFade(scrolled);
        UpdateNotesEdgeBottom.Color = EdgeFade(UpdateNotesOverflow - scrolled);
    }

    void SizeSlider_Changed(object? sender, EventArgs e) => SetScale((int)SizeSlider.Value);

    void GapSlider_Changed(object? sender, EventArgs e) => SetGap((int)GapSlider.Value);

    void SetScale(int percent)
    {
        int was = Settings.Scale;
        Settings.Scale = percent;
        if (Settings.Scale != was) ApplyLook();
    }

    void SetGap(int px)
    {
        int was = Settings.Gap;
        Settings.Gap = px;
        if (Settings.Gap != was) ApplyLook();
    }

    void MaxIslandWidthSlider_Changed(object? sender, EventArgs e)
    {
        Settings.MaxIslandWidth = (int)MaxIslandWidthSlider.Value;
        MaxIslandWidthText.Text = $"{Settings.MaxIslandWidth} px";
        StartShapeLoop();
    }

    void InvertGesture_Click(object sender, RoutedEventArgs e)
    {
        Settings.InvertMediaGesture = !Settings.InvertMediaGesture;
        RefreshLookPage();
    }

    void ShowArtist_Click(object sender, RoutedEventArgs e)
    {
        Settings.ShowTrackArtist = !Settings.ShowTrackArtist;
        RefreshLookPage();
        OnMediaChanged();
    }

    void Dots_Click(object sender, RoutedEventArgs e)
    {
        Eq.Dots = Settings.Dots = DotsSegments.PickUnderPointer() == 1;
        RefreshLookPage();
    }

    void Glass_Click(object sender, RoutedEventArgs e)
    {
        Settings.Glass = !Settings.Glass;
        RefreshLookPage();
        SyncGlass(true);
    }

    void SeekStyle_Click(object sender, RoutedEventArgs e)
    {
        Settings.LineBar = SeekStyleSegments.PickUnderPointer() == 1;
        RefreshLookPage();
        SyncSeekStyle(true);
    }

    void SeekHover_Click(object sender, RoutedEventArgs e)
    {
        Settings.SeekHover = (SeekHover)SeekHoverSegments.PickUnderPointer();
        RefreshLookPage();
    }

    void SeekGlow_Click(object sender, RoutedEventArgs e)
    {
        SeekPlain.Glow = SeekLines.Glow = Settings.SeekGlow = !Settings.SeekGlow;
        RefreshLookPage();
    }

    void CompactProgress_Click(object sender, RoutedEventArgs e)
    {
        Settings.CompactProgress = (CompactProgress)CompactProgressSegments.PickUnderPointer();
        RefreshLookPage();
        UpdateMiniProgress();
    }

    void BackdropRow_Click(object sender, RoutedEventArgs e) => ToggleLookTiles(BackdropTiles);

    void LyricChangeRow_Click(object sender, RoutedEventArgs e) => ToggleLookTiles(LyricChangeTiles);

    void HoverRow_Click(object sender, RoutedEventArgs e) => ToggleLookTiles(HoverTiles);

    void BackdropTile_Click(object sender, RoutedEventArgs e)
    {
        Settings.Backdrop = (Backdrop)BackdropStrip.Children.IndexOf((UIElement)sender);
        RefreshLookPage();
        SyncBackdrop();
    }

    void LyricChangeTile_Click(object sender, RoutedEventArgs e)
    {
        Settings.LyricChange = (LyricChange)LyricChangeStrip.Children.IndexOf((UIElement)sender);
        RefreshLookPage();
    }

    void HoverTile_Click(object sender, RoutedEventArgs e)
    {
        Settings.Hover = (Hover)HoverStrip.Children.IndexOf((UIElement)sender);
        RefreshLookPage();
    }

    void ToggleLookTiles(Border tiles)
    {
        _openLookTiles = tiles == _openLookTiles ? null : tiles;
        SlideLookTiles(BackdropTiles, BackdropChevron);
        SlideLookTiles(LyricChangeTiles, LyricChangeChevron);
        SlideLookTiles(HoverTiles, HoverChevron);
    }

    void FoldLookTiles()
    {
        if (_openLookTiles == null) return;
        _openLookTiles = null;
        SlideLookTiles(BackdropTiles, BackdropChevron);
        SlideLookTiles(LyricChangeTiles, LyricChangeChevron);
        SlideLookTiles(HoverTiles, HoverChevron);
    }

    void SlideLookTiles(Border tiles, Icon chevron)
    {
        bool open = tiles == _openLookTiles;
        if (!open && tiles.Visibility != Visibility.Visible) return;

        tiles.Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        double targetHeight = tiles == LyricChangeTiles ? 230 : LookTilesHeight;
        var slide = new DoubleAnimation(open ? targetHeight : 0, Ms(open ? 320 : 240)) { EasingFunction = ease };
        slide.Completed += (_, _) =>
        {
            if (tiles != _openLookTiles) tiles.Visibility = Visibility.Collapsed;
        };
        tiles.BeginAnimation(HeightProperty, slide);
        tiles.Child.BeginAnimation(OpacityProperty, new DoubleAnimation(open ? 1 : 0, Ms(open ? 260 : 160)));
        chevron.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(open ? 90 : 0, Ms(240)) { EasingFunction = ease });
    }

    double LookOverflow => Math.Max(LookRows.ActualHeight + LookRows.Margin.Top + LookRows.Margin.Bottom - LookView.Height, 0);

    void LookRows_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double reveal = _openLookTiles == null ? 0
            : _openLookTiles.TranslatePoint(new Point(0, _openLookTiles.ActualHeight), LookRows).Y + LookRows.Margin.Top + LookRows.Margin.Bottom - LookView.Height;
        _lookScroll.Target = Math.Clamp(Math.Max(_lookScroll.Target, reveal), 0, LookOverflow);
        StartShapeLoop();
    }

    void ScrollLook(int notches)
    {
        _lookScroll.Target = Math.Clamp(_lookScroll.Target + notches * LookWheelStep, 0, LookOverflow);
        StartShapeLoop();
    }

    void ShrinkLookEdges(double scrolled)
    {
        double top = Math.Clamp(scrolled / LookEdgeZone, 0, 1), bottom = Math.Clamp((LookOverflow - scrolled) / LookEdgeZone, 0, 1);
        foreach (FrameworkElement row in LookRows.Children)
        {
            double middle = VisualTreeHelper.GetOffset(row).Y + LookRows.Margin.Top + row.ActualHeight / 2 - scrolled;
            double fromTop = Math.Clamp(middle / LookEdgeZone, 0, 1), fromBottom = Math.Clamp((LookView.Height - middle) / LookEdgeZone, 0, 1);
            double shrink = Math.Max(top * Smooth(1 - fromTop), bottom * Smooth(1 - fromBottom));

            if (row.RenderTransform is not ScaleTransform size)
            {
                row.RenderTransform = size = new ScaleTransform();
                row.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            size.ScaleX = size.ScaleY = 1 - (1 - LookEdgeScale) * shrink;
        }
    }

    static double Smooth(double t) => t * t * (3 - 2 * t);

    void ApplyLookScroll()
    {
        double scrolled = _lookScroll.Value;
        LookEdgeTop.Color = EdgeFade(scrolled);
        LookEdgeBottom.Color = EdgeFade(LookOverflow - scrolled);
        if (LookView.IsVisible) ShrinkLookEdges(scrolled);
        if (LookMove.Y == -scrolled) return;

        LookMove.Y = -scrolled;
        if (_view == View.Look) LookRows.FollowPointer();
    }

    void Accent_Click(object sender, RoutedEventArgs e)
    {
        Settings.Accent = ((RadioButton)sender).Background is SolidColorBrush picked ? picked.Color : null;
        RefreshLookPage();
        SyncAccent();
        SyncRim();
    }

    void RadiusSlider_Changed(object? sender, EventArgs e)
    {
        Settings.Radius = (int)RadiusSlider.Value;
        RadiusText.Text = Settings.Radius + " px";
        ApplyShape();
        StartShapeLoop();
        UpdateTargets();
    }

    void Notch_Click(object sender, RoutedEventArgs e)
    {
        bool wasNotch = Settings.Notch;
        Settings.Notch = NotchSegments.PickUnderPointer() == 1;
        if (Settings.Notch != wasNotch)
        {
            SyncNotchRows(animate: true);
        }
        ApplyLook();
    }

    void SyncNotchRows(bool animate)
    {
        bool notch = Settings.Notch;
        if (!animate)
        {
            GapRow.Visibility = notch ? Visibility.Collapsed : Visibility.Visible;
            GapRow.Height = notch ? 0 : 40;
            GapRow.Opacity = notch ? 0 : 1;
            GapRowMove.X = notch ? -60 : 0;

            PosYRow.Visibility = notch ? Visibility.Collapsed : Visibility.Visible;
            PosYRow.Height = notch ? 0 : 40;
            PosYRow.Opacity = notch ? 0 : 1;
            PosYRowMove.X = notch ? -60 : 0;
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Duration duration = Ms(260);

        if (notch)
        {
            var slideX = new DoubleAnimation(0, -60, duration) { EasingFunction = ease };
            var fadeOut = new DoubleAnimation(1, 0, Ms(200)) { EasingFunction = ease };
            var shrinkH = new DoubleAnimation(40, 0, duration) { EasingFunction = ease };
            shrinkH.Completed += (_, _) =>
            {
                if (Settings.Notch)
                {
                    GapRow.Visibility = Visibility.Collapsed;
                    PosYRow.Visibility = Visibility.Collapsed;
                }
            };

            GapRowMove.BeginAnimation(TranslateTransform.XProperty, slideX);
            GapRow.BeginAnimation(OpacityProperty, fadeOut);
            GapRow.BeginAnimation(HeightProperty, shrinkH);

            PosYRowMove.BeginAnimation(TranslateTransform.XProperty, slideX);
            PosYRow.BeginAnimation(OpacityProperty, fadeOut);
            PosYRow.BeginAnimation(HeightProperty, shrinkH);
        }
        else
        {
            GapRow.Visibility = Visibility.Visible;
            PosYRow.Visibility = Visibility.Visible;

            var slideX = new DoubleAnimation(-60, 0, duration) { EasingFunction = ease };
            var fadeIn = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            var expandH = new DoubleAnimation(0, 40, duration) { EasingFunction = ease };

            GapRowMove.BeginAnimation(TranslateTransform.XProperty, slideX);
            GapRow.BeginAnimation(OpacityProperty, fadeIn);
            GapRow.BeginAnimation(HeightProperty, expandH);

            PosYRowMove.BeginAnimation(TranslateTransform.XProperty, slideX);
            PosYRow.BeginAnimation(OpacityProperty, fadeIn);
            PosYRow.BeginAnimation(HeightProperty, expandH);
        }
    }

    int _baselinePosX = Settings.PosX;
    int _baselinePosY = Settings.PosY;
    DispatcherTimer? _coordConfirmTimer;
    int _coordConfirmSeconds = 8;

    void CheckCoordinateSafety()
    {
        if (Math.Abs(Settings.PosX - _baselinePosX) > 200 || Math.Abs(Settings.PosY - _baselinePosY) > 200)
        {
            ShowCoordConfirm();
        }
    }

    void ShowCoordConfirm()
    {
        CoordConfirmOverlay.Visibility = Visibility.Visible;
        CoordConfirmOverlay.IsHitTestVisible = true;
        _coordConfirmSeconds = 8;
        CoordConfirmTimerText.Text = $"Смещение было сильно изменено.\nОткат через {_coordConfirmSeconds} с";

        CoordConfirmDimmer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        var spring = new BackEase { Amplitude = 0.28, EasingMode = EasingMode.EaseOut };
        CoordConfirmScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.82, 1.0, Ms(320)) { EasingFunction = spring });
        CoordConfirmScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.82, 1.0, Ms(320)) { EasingFunction = spring });
        CoordConfirmCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1.0, Ms(200)));

        if (_coordConfirmTimer == null)
        {
            _coordConfirmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _coordConfirmTimer.Tick += (_, _) =>
            {
                _coordConfirmSeconds--;
                if (_coordConfirmSeconds <= 0)
                {
                    CoordConfirm_Cancel(null!, null!);
                }
                else
                {
                    CoordConfirmTimerText.Text = $"Смещение было сильно изменено.\nОткат через {_coordConfirmSeconds} с";
                }
            };
        }
        _coordConfirmTimer.Stop();
        _coordConfirmTimer.Start();
    }

    void HideCoordConfirm()
    {
        _coordConfirmTimer?.Stop();
        CoordConfirmOverlay.IsHitTestVisible = false;

        var fade = new DoubleAnimation(0, Ms(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => CoordConfirmOverlay.Visibility = Visibility.Collapsed;
        CoordConfirmDimmer.BeginAnimation(OpacityProperty, fade);
        CoordConfirmCard.BeginAnimation(OpacityProperty, fade);
        CoordConfirmScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 0.90, Ms(180)));
        CoordConfirmScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 0.90, Ms(180)));
    }

    void CoordConfirm_BackgroundClick(object sender, MouseButtonEventArgs e)
    {
        CoordConfirm_Cancel(sender, e);
    }

    void CoordConfirm_Accept(object sender, RoutedEventArgs e)
    {
        _baselinePosX = Settings.PosX;
        _baselinePosY = Settings.PosY;
        HideCoordConfirm();
    }

    void CoordConfirm_Cancel(object sender, RoutedEventArgs e)
    {
        HideCoordConfirm();
        Settings.PosX = _baselinePosX;
        Settings.PosY = _baselinePosY;
        ApplyLook();
    }

    void Align_Click(object sender, RoutedEventArgs e)
    {
        Settings.IslandAlign = (IslandAlign)AlignSegments.PickUnderPointer();
        ApplyLook();
    }

    void ResetPos_Click(object sender, RoutedEventArgs e)
    {
        Settings.PosX = 0;
        Settings.PosY = 0;
        ApplyLook();
    }

    void PosXSlider_Changed(object? sender, EventArgs e)
    {
        double oldLeft = Left;
        Settings.PosX = (int)PosXSlider.Value;
        PosXText.Text = Settings.PosX + (Native.IsShiftDown ? " px [1px]" : " px");
        ApplyLook();
        double delta = Left - oldLeft;
        if (Math.Abs(delta) > 0.05)
        {
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            Native.MoveCursorBy((int)Math.Round(delta * dpi), 0);
        }
        CheckCoordinateSafety();
    }

    void PosYSlider_Changed(object? sender, EventArgs e)
    {
        double oldTop = Top;
        Settings.PosY = (int)PosYSlider.Value;
        PosYText.Text = Settings.PosY + (Native.IsShiftDown ? " px [1px]" : " px");
        ApplyLook();
        double delta = Top - oldTop;
        if (Math.Abs(delta) > 0.05)
        {
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleY;
            Native.MoveCursorBy(0, (int)Math.Round(delta * dpi));
        }
        CheckCoordinateSafety();
    }

    void LyricSpeedSlider_Changed(object? sender, EventArgs e)
    {
        Settings.LyricAnimSpeed = (int)LyricSpeedSlider.Value;
        LyricSpeedText.Text = Settings.LyricAnimSpeed + "%";
    }

    void LyricHeightSlider_Changed(object? sender, EventArgs e)
    {
        Settings.LyricAnimHeight = (int)LyricHeightSlider.Value;
        LyricHeightText.Text = Settings.LyricAnimHeight + " px";
    }

    void LyricStaggerSlider_Changed(object? sender, EventArgs e)
    {
        Settings.LyricAnimStagger = (int)LyricStaggerSlider.Value;
        LyricStaggerText.Text = Settings.LyricAnimStagger + " ms";
    }

    void LyricLeadSlider_Changed(object? sender, EventArgs e)
    {
        Settings.LyricLeadSec = (int)LyricLeadSlider.Value;
        LyricLeadText.Text = (Settings.LyricLeadSec / 10.0).ToString("0.0") + " с";
    }

    void CenterPos_Click(object sender, RoutedEventArgs e)
    {
        Settings.PosX = 0;
        ApplyLook();
    }

    void RefreshLookPage()
    {
        bool shift = Native.IsShiftDown;
        SizeText.Text = Settings.Scale + "%";
        GapText.Text = Settings.Gap + " px";
        RadiusText.Text = Settings.Radius + " px";
        PosXText.Text = Settings.PosX + (shift ? " px [1px]" : " px");
        PosYText.Text = Settings.PosY + (shift ? " px [1px]" : " px");
        SizeSlider.Set(Settings.Scale, LookView.IsVisible);
        GapSlider.Set(Settings.Gap, LookView.IsVisible);
        RadiusSlider.Set(Settings.Radius, LookView.IsVisible);
        PosXSlider.Set(Settings.PosX, LookView.IsVisible);
        PosYSlider.Set(Settings.PosY, LookView.IsVisible);
        NotchSegments.Set(Settings.Notch ? 1 : 0, LookView.IsVisible);
        AlignSegments.Set((int)Settings.IslandAlign, LookView.IsVisible);
        LyricSpeedSlider.Set(Settings.LyricAnimSpeed, LookView.IsVisible);
        LyricHeightSlider.Set(Settings.LyricAnimHeight, LookView.IsVisible);
        LyricStaggerSlider.Set(Settings.LyricAnimStagger, LookView.IsVisible);
        LyricLeadSlider.Set(Settings.LyricLeadSec, LookView.IsVisible);
        LyricSpeedText.Text = Settings.LyricAnimSpeed + "%";
        LyricHeightText.Text = Settings.LyricAnimHeight + " px";
        LyricStaggerText.Text = Settings.LyricAnimStagger + " ms";
        LyricLeadText.Text = (Settings.LyricLeadSec / 10.0).ToString("0.0") + " с";
        DotsSegments.Set(Settings.Dots ? 1 : 0, LookView.IsVisible);
        SeekStyleSegments.Set(Settings.LineBar ? 1 : 0, LookView.IsVisible);
        SeekHoverSegments.Set((int)Settings.SeekHover, LookView.IsVisible);
        SeekGlowSwitch.Set(Settings.SeekGlow, LookView.IsVisible);
        CompactProgressSegments.Set((int)Settings.CompactProgress, LookView.IsVisible);
        GlassSwitch.Set(Settings.Glass, LookView.IsVisible);
        LookRimSwitch.Set(Settings.Rim, LookView.IsVisible);
        MaxIslandWidthSlider.Set(Settings.MaxIslandWidth, LookView.IsVisible);
        MaxIslandWidthText.Text = $"{Settings.MaxIslandWidth} px";
        InvertGestureSwitch.Set(Settings.InvertMediaGesture, LookView.IsVisible);
        ShowArtistSwitch.Set(Settings.ShowTrackArtist, LookView.IsVisible);
        ((RadioButton)BackdropStrip.Children[(int)Settings.Backdrop]).IsChecked = true;
        ((RadioButton)LyricChangeStrip.Children[(int)Settings.LyricChange]).IsChecked = true;
        ((RadioButton)HoverStrip.Children[(int)Settings.Hover]).IsChecked = true;
        BackdropText.Text = Settings.Backdrop switch
        {
            Backdrop.Matrix => "Матрица",
            Backdrop.Stars => "Звёзды",
            Backdrop.MatrixAndStars => "Матрица и звёзды",
            Backdrop.Cover => "Обложка",
            _ => "Свечение",
        };
        LyricChangeText.Text = Settings.LyricChange switch
        {
            LyricChange.Wave => "Волна по буквам",
            LyricChange.Drum => "Барабан по словам",
            _ => "Плавно",
        };
        HoverText.Text = Settings.Hover switch
        {
            Hover.Magnet => "Магнит",
            Hover.Flow => "Перетекание",
            _ => "Диск",
        };
        foreach (RadioButton dot in AccentStrip.Children)
        {
            Color? color = dot.Background is SolidColorBrush own ? own.Color : null;
            if (color != Settings.Accent) continue;
            dot.IsChecked = true;
            AccentText.Text = (string)dot.Tag;
        }
        SyncNotchRows(animate: false);
    }

    void ApplyLook()
    {
        ClampPositionsToScreen();
        RefreshLookPage();
        _userScale.Target = Settings.Scale / 100.0;
        _topGap.Target = Settings.Notch ? 0 : Settings.Gap;
        RepositionWindow();
        UpdateTargets();
        _clipRect = Rect.Empty;
        Body.InvalidateNotch();
        StartShapeLoop();
    }

    void OpenLanguageModal()
    {
        PopulateLanguageList();
        LanguageModal.Visibility = Visibility.Visible;
        LanguageModal.Opacity = 0;
        LanguageModalScale.ScaleX = LanguageModalScale.ScaleY = 0.5;

        var ease = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };
        var scaleAnim = new DoubleAnimation(1.0, Ms(320)) { EasingFunction = ease };
        var fadeAnim = new DoubleAnimation(1.0, Ms(200));

        LanguageModal.BeginAnimation(OpacityProperty, fadeAnim);
        LanguageModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        LanguageModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
    }

    void LanguageModal_Close(object sender, RoutedEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var scaleAnim = new DoubleAnimation(0.6, Ms(180)) { EasingFunction = ease };
        var fadeAnim = new DoubleAnimation(0.0, Ms(160));
        scaleAnim.Completed += (_, _) => LanguageModal.Visibility = Visibility.Collapsed;

        LanguageModal.BeginAnimation(OpacityProperty, fadeAnim);
        LanguageModalScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        LanguageModalScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
    }

    void PopulateLanguageList()
    {
        LanguageList.Children.Clear();
        foreach (var (code, name) in Localization.Languages)
        {
            bool isCurrent = code == Settings.Language;
            var btn = new Button
            {
                Height = 36,
                Margin = new Thickness(0, 2, 0, 2),
                Background = isCurrent ? new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Tag = code
            };
            var grid = new Grid { Margin = new Thickness(12, 0, 12, 0) };
            var label = new TextBlock
            {
                Text = name,
                FontSize = 13.5,
                FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isCurrent ? Brushes.White : (Brush)FindResource("Dim"),
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(label);

            if (isCurrent)
            {
                var check = new Icon
                {
                    Kind = Glyph.Check,
                    Width = 14,
                    Height = 14,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                grid.Children.Add(check);
            }

            btn.Content = grid;
            btn.Click += (s, e) =>
            {
                string newLang = (string)((Button)s).Tag;
                Settings.Language = newLang;
                LanguageText.Text = newLang.ToUpperInvariant();
                ApplyLocalization();
                LanguageModal_Close(null!, null!);
            };

            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            factory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { Source = btn });
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            factory.AppendChild(contentFactory);
            template.VisualTree = factory;
            btn.Template = template;

            LanguageList.Children.Add(btn);
        }
    }

    void ApplyLocalization()
    {
        UpdateSwitches(false);
        RefreshLookPage();
    }
}
