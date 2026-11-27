using System.Globalization;
using System.IO;
using System.Windows.Media;
using Microsoft.Win32;

namespace DynamicIsland;

[Flags]
enum Backdrop
{
    Glow = 0,
    Matrix = 1,
    Stars = 2,
    MatrixAndStars = Matrix | Stars,
    Cover = 4,
}

enum LyricChange
{
    Smooth,
    Wave,
    Drum,
}

[Flags]
enum LyricSource
{
    Lrclib = 1,
    Deezer = 2,
    NetEase = 4,
    QqMusic = 8,
    Kugou = 16,
    Paxsenix = 32,
    Usual = Lrclib | Deezer | NetEase | QqMusic | Kugou,
    All = Usual | Paxsenix,
}

enum PointerLyricShape
{
    Column,
    Line,
}

enum Hover
{
    Disc,
    Magnet,
    Flow,
}

enum SeekHover
{
    Magnifier,
    Lift,
    Wave,
}

enum CompactProgress
{
    Off,
    Ring,
    Wash,
}

enum MaterialStyle
{
    Liquid,
    Matte,
    None,
}

enum LyricAlign
{
    Center,
    Left,
    Right,
}

enum IslandAlign
{
    Center,
    Left,
    Right,
}

enum MatrixDensity
{
    Sparse,
    Standard,
    Dense,
}

enum LyricAnimStyle
{
    Letters,
    Wave,
    Bounce,
    Slide,
}

enum ComboCounterStyle
{
    RuX,
    EnX,
    Times,
    Brackets,
    Square,
    Hash,
    Progress,
}

enum ComboSplitMode
{
    All,
    Punct,
    Words,
    Lines,
}

static class Settings
{
    const string Key = @"Software\DynamicIsland";
    const int MinScale = 75, MaxScale = 130, MaxGap = 24;
    const int DefaultScale = 100, DefaultGap = 8;

    static bool _lyrics = ReadSwitch(nameof(Lyrics)), _lyricEffects = ReadSwitch(nameof(LyricEffects));
    static bool _network = ReadSwitch(nameof(Network)), _hideFullscreen = ReadSwitch(nameof(HideFullscreen));
    static bool _rim = ReadSwitch(nameof(Rim)), _appVolume = ReadSwitch(nameof(AppVolume));
    static bool _weather = ReadSwitch(nameof(Weather));
    static bool _pointerLyric = ReadSwitch(nameof(PointerLyric), false);
    static bool _lyricParting = ReadSwitch(nameof(LyricParting));
    static PointerLyricShape _pointerLyricShape = (PointerLyricShape)Math.Clamp(Read(nameof(PointerLyricShape), 0), 0, (int)PointerLyricShape.Line);
    static Place? _place = ReadPlace();
    static bool _dots = ReadSwitch(nameof(Dots), false);
    static bool _lineBar = ReadSwitch(nameof(LineBar));
    static bool _seekGlow = ReadSwitch(nameof(SeekGlow));
    static bool _glass = ReadSwitch(nameof(Glass), false);
    static Backdrop _backdrop = (Backdrop)Math.Clamp(Read(nameof(Backdrop), 0), 0, (int)Backdrop.Cover);
    static LyricChange _lyricChange = (LyricChange)Math.Clamp(Read(nameof(LyricChange), (int)LyricChange.Wave), 0, (int)LyricChange.Drum);
    static LyricSource _lyricSources = Known((LyricSource)Read(nameof(LyricSources), (int)LyricSource.Usual));
    static Hover _hover = (Hover)Math.Clamp(Read(nameof(Hover), 0), 0, (int)Hover.Flow);
    static SeekHover _seekHover = (SeekHover)Math.Clamp(Read(nameof(SeekHover), 0), 0, (int)SeekHover.Wave);
    static CompactProgress _compactProgress = (CompactProgress)Math.Clamp(Read(nameof(CompactProgress), (int)CompactProgress.Ring), 0, (int)CompactProgress.Wash);
    static int _scale = Math.Clamp(Read(nameof(Scale), DefaultScale), MinScale, MaxScale);
    static int _gap = Math.Clamp(Read(nameof(Gap), DefaultGap), 0, MaxGap);
    static int _accent = Read(nameof(Accent), 0);

    // New properties ported from Roc edition
    static bool _shadow = ReadSwitch(nameof(Shadow), true);
    static bool _systemStats = ReadSwitch(nameof(SystemStats), true);
    static bool _clickLock = ReadSwitch(nameof(ClickLock), true);
    static bool _capitalizeTitle = ReadSwitch(nameof(CapitalizeTitle), true);
    static bool _obs = ReadSwitch(nameof(Obs), true);
    static string _language = Read(nameof(Language), "ru");
    static int _radius = Math.Clamp(Read(nameof(Radius), 100), 0, 100);
    static int _heightOffset = Math.Clamp(Read(nameof(HeightOffset), 0), 0, 16);
    static int _textScale = Math.Clamp(Read(nameof(TextScale), 100), 75, 130);
    static int _glassAmount = Math.Clamp(Read(nameof(GlassAmount), 100), 20, 100);
    static MaterialStyle _material = (MaterialStyle)Math.Clamp(Read(nameof(Material), 0), 0, (int)MaterialStyle.None);
    static LyricAlign _align = (LyricAlign)Math.Clamp(Read(nameof(Align), 0), 0, (int)LyricAlign.Right);
    static int _posX = Read(nameof(PosX), 0);
    static int _posY = Read(nameof(PosY), 0);
    static bool _notch = ReadSwitch(nameof(Notch), false);
    static IslandAlign _islandAlign = (IslandAlign)Math.Clamp(Read(nameof(IslandAlign), 0), 0, (int)IslandAlign.Right);
    static string _lyricSourceRatings = Read(nameof(LyricSourceRatings), "10,9,8,7,6,5");

    // Equalizer properties
    static bool _compactEqualizer = ReadSwitch(nameof(CompactEqualizer), true);
    static bool _compactEqDots = ReadSwitch(nameof(CompactEqDots), false);
    static int _compactEqBars = Math.Clamp(Read(nameof(CompactEqBars), 4), 2, 8);
    static bool _miniEqualizer = ReadSwitch(nameof(MiniEqualizer), true);
    static int _eqBars = Math.Clamp(Read(nameof(EqBars), 5), 3, 9);
    static int _eqSensitivity = Math.Clamp(Read(nameof(EqSensitivity), 100), 50, 200);
    static int _matrixRows = Math.Clamp(Read(nameof(MatrixRows), 12), 4, 20);
    static int _matrixFadeStrength = Math.Clamp(Read(nameof(MatrixFadeStrength), 70), 0, 100);
    static MatrixDensity _matrixDensity = (MatrixDensity)Math.Clamp(Read(nameof(MatrixDensity), (int)MatrixDensity.Standard), 0, (int)MatrixDensity.Dense);
    static int _matrixOpacity = Math.Clamp(Read(nameof(MatrixOpacity), 95), 30, 100);

    // Lyric animation and Combo
    static LyricAnimStyle _lyricAnimStyle = (LyricAnimStyle)Math.Clamp(Read(nameof(LyricAnimStyle), (int)LyricAnimStyle.Wave), 0, (int)LyricAnimStyle.Slide);
    static int _lyricAnimSpeed = Math.Clamp(Read(nameof(LyricAnimSpeed), 100), 50, 200);
    static int _lyricAnimHeight = Math.Clamp(Read(nameof(LyricAnimHeight), 26), 15, 80);
    static int _lyricAnimStagger = Math.Clamp(Read(nameof(LyricAnimStagger), 25), 10, 60);
    static int _lyricLeadSec = Math.Clamp(Read(nameof(LyricLeadSec), 5), 0, 9);

    static bool _comboEnabled = ReadSwitch(nameof(ComboEnabled), true);
    static int _comboMinRepeats = Math.Clamp(Read(nameof(ComboMinRepeats), 3), 2, 5);
    static ComboCounterStyle _comboCounterStyle = (ComboCounterStyle)Math.Clamp(Read(nameof(ComboCounterStyle), (int)ComboCounterStyle.RuX), 0, (int)ComboCounterStyle.Progress);
    static ComboSplitMode _comboSplitMode = (ComboSplitMode)Math.Clamp(Read(nameof(ComboSplitMode), (int)ComboSplitMode.All), 0, (int)ComboSplitMode.Lines);
    static bool _comboIgnoreAdlibs = ReadSwitch(nameof(ComboIgnoreAdlibs), true);
    static bool _comboStripBrackets = ReadSwitch(nameof(ComboStripBrackets), true);
    static int _comboMinWordLen = Math.Clamp(Read(nameof(ComboMinWordLen), 2), 1, 5);
    static double _minMusicDuration = ReadDouble(nameof(MinMusicDuration), 3.0);

    static int _maxIslandWidth = Math.Clamp(Read(nameof(MaxIslandWidth), 460), 220, 800);
    static bool _invertMediaGesture = ReadSwitch(nameof(InvertMediaGesture), false);
    static bool _showTrackArtist = ReadSwitch(nameof(ShowTrackArtist), false);
    static bool _interceptWindowsNotifications = ReadSwitch(nameof(InterceptWindowsNotifications), false);
    static bool _notifyDnd = ReadSwitch(nameof(NotifyDnd), true);
    static bool _notifyHeadphones = ReadSwitch(nameof(NotifyHeadphones), true);
    static bool _notifyBattery = ReadSwitch(nameof(NotifyBattery), true);
    static bool _notifyVpn = ReadSwitch(nameof(NotifyVpn), true);
    static bool _notifyTimer = ReadSwitch(nameof(NotifyTimer), true);
    static bool _notifyObs = ReadSwitch(nameof(NotifyObs), true);
    static bool _notifyShelf = ReadSwitch(nameof(NotifyShelf), true);
    static bool _notifyDrive = ReadSwitch(nameof(NotifyDrive), true);
    static int _timerSound = Math.Clamp(Read(nameof(TimerSound), 0), 0, 5);
    static bool _timerIntervalMode = ReadSwitch(nameof(TimerIntervalMode), false);
    static bool _timerOvertime = ReadSwitch(nameof(TimerOvertime), true);

    public static bool Dots
    {
        get => _dots;
        set => Write(nameof(Dots), _dots = value);
    }

    public static bool EqualizerDots
    {
        get => _dots;
        set => Write(nameof(Dots), _dots = value);
    }

    public static bool LineBar
    {
        get => _lineBar;
        set => Write(nameof(LineBar), _lineBar = value);
    }

    public static bool SeekGlow
    {
        get => _seekGlow;
        set => Write(nameof(SeekGlow), _seekGlow = value);
    }

    public static bool Glass
    {
        get => _glass;
        set => Write(nameof(Glass), _glass = value);
    }

    public static Backdrop Backdrop
    {
        get => _backdrop;
        set => Write(nameof(Backdrop), (int)(_backdrop = value));
    }

    public static LyricChange LyricChange
    {
        get => _lyricChange;
        set => Write(nameof(LyricChange), (int)(_lyricChange = value));
    }

    public static LyricSource LyricSources
    {
        get => _lyricSources;
        set => Write(nameof(LyricSources), (int)(_lyricSources = Known(value)));
    }

    public static Hover Hover
    {
        get => _hover;
        set => Write(nameof(Hover), (int)(_hover = value));
    }

    public static SeekHover SeekHover
    {
        get => _seekHover;
        set => Write(nameof(SeekHover), (int)(_seekHover = value));
    }

    public static CompactProgress CompactProgress
    {
        get => _compactProgress;
        set => Write(nameof(CompactProgress), (int)(_compactProgress = value));
    }

    public static bool Lyrics
    {
        get => _lyrics;
        set => Write(nameof(Lyrics), _lyrics = value);
    }

    public static bool LyricEffects
    {
        get => _lyricEffects;
        set => Write(nameof(LyricEffects), _lyricEffects = value);
    }

    public static bool PointerLyric
    {
        get => _pointerLyric;
        set => Write(nameof(PointerLyric), _pointerLyric = value);
    }

    public static PointerLyricShape PointerLyricShape
    {
        get => _pointerLyricShape;
        set => Write(nameof(PointerLyricShape), (int)(_pointerLyricShape = value));
    }

    public static bool LyricParting
    {
        get => _lyricParting;
        set => Write(nameof(LyricParting), _lyricParting = value);
    }

    public static bool Rim
    {
        get => _rim;
        set => Write(nameof(Rim), _rim = value);
    }

    public static bool AppVolume
    {
        get => _appVolume;
        set => Write(nameof(AppVolume), _appVolume = value);
    }

    public static bool Network
    {
        get => _network;
        set => Write(nameof(Network), _network = value);
    }

    public static bool Weather
    {
        get => _weather;
        set => Write(nameof(Weather), _weather = value);
    }

    public static Place? Place
    {
        get => _place;
        set
        {
            _place = value;
            string[] stored = value is { } place ? [place.Name, place.Region, Coordinate(place.Latitude), Coordinate(place.Longitude)] : [];
            Write(nameof(Place), stored, RegistryValueKind.MultiString);
        }
    }

    public static bool HideFullscreen
    {
        get => _hideFullscreen;
        set => Write(nameof(HideFullscreen), _hideFullscreen = value);
    }

    public static int Scale
    {
        get => _scale;
        set => Write(nameof(Scale), _scale = Math.Clamp(value, MinScale, MaxScale));
    }

    public static int Gap
    {
        get => _gap;
        set => Write(nameof(Gap), _gap = Math.Clamp(value, 0, MaxGap));
    }

    public static Color? Accent
    {
        get => _accent == 0 ? null : Color.FromRgb((byte)(_accent >> 16), (byte)(_accent >> 8), (byte)_accent);
        set => Write(nameof(Accent), _accent = value is { } c ? c.R << 16 | c.G << 8 | c.B : 0);
    }

    public static string[] Shelf
    {
        get => Read<string[]>(nameof(Shelf), []);
        set => Write(nameof(Shelf), value, RegistryValueKind.MultiString);
    }

    // New properties
    public static bool Shadow
    {
        get => _shadow;
        set => Write(nameof(Shadow), _shadow = value);
    }

    public static bool SystemStats
    {
        get => _systemStats;
        set => Write(nameof(SystemStats), _systemStats = value);
    }

    public static bool ClickLock
    {
        get => _clickLock;
        set => Write(nameof(ClickLock), _clickLock = value);
    }

    public static bool CapitalizeTitle
    {
        get => _capitalizeTitle;
        set => Write(nameof(CapitalizeTitle), _capitalizeTitle = value);
    }

    public static bool Obs
    {
        get => _obs;
        set => Write(nameof(Obs), _obs = value);
    }

    public static string Language
    {
        get => _language;
        set => Write(nameof(Language), _language = value);
    }

    public static int Radius
    {
        get => _radius;
        set => Write(nameof(Radius), _radius = Math.Clamp(value, 0, 100));
    }

    public static int HeightOffset
    {
        get => _heightOffset;
        set => Write(nameof(HeightOffset), _heightOffset = Math.Clamp(value, 0, 16));
    }

    public static int TextScale
    {
        get => _textScale;
        set => Write(nameof(TextScale), _textScale = Math.Clamp(value, 75, 130));
    }

    public static int GlassAmount
    {
        get => _glassAmount;
        set => Write(nameof(GlassAmount), _glassAmount = Math.Clamp(value, 20, 100));
    }

    public static MaterialStyle Material
    {
        get => _material;
        set => Write(nameof(Material), (int)(_material = value));
    }

    public static LyricAlign Align
    {
        get => _align;
        set => Write(nameof(Align), (int)(_align = value));
    }

    public static int PosX
    {
        get => _posX;
        set => Write(nameof(PosX), _posX = value);
    }

    public static int PosY
    {
        get => _posY;
        set => Write(nameof(PosY), _posY = value);
    }

    public static bool Notch
    {
        get => _notch;
        set => Write(nameof(Notch), _notch = value);
    }

    public static IslandAlign IslandAlign
    {
        get => _islandAlign;
        set => Write(nameof(IslandAlign), (int)(_islandAlign = value));
    }

    public static string LyricSourceRatings
    {
        get => _lyricSourceRatings;
        set => Write(nameof(LyricSourceRatings), _lyricSourceRatings = value);
    }

    public static bool CompactEqualizer
    {
        get => _compactEqualizer;
        set => Write(nameof(CompactEqualizer), _compactEqualizer = value);
    }

    public static bool CompactEqDots
    {
        get => _compactEqDots;
        set => Write(nameof(CompactEqDots), _compactEqDots = value);
    }

    public static int CompactEqBars
    {
        get => _compactEqBars;
        set => Write(nameof(CompactEqBars), _compactEqBars = Math.Clamp(value, 2, 8));
    }

    public static bool MiniEqualizer
    {
        get => _miniEqualizer;
        set => Write(nameof(MiniEqualizer), _miniEqualizer = value);
    }

    public static int EqBars
    {
        get => _eqBars;
        set => Write(nameof(EqBars), _eqBars = Math.Clamp(value, 3, 9));
    }

    public static int EqSensitivity
    {
        get => _eqSensitivity;
        set => Write(nameof(EqSensitivity), _eqSensitivity = Math.Clamp(value, 50, 200));
    }

    public static int MatrixRows
    {
        get => _matrixRows;
        set => Write(nameof(MatrixRows), _matrixRows = Math.Clamp(value, 4, 20));
    }

    public static int MatrixFadeStrength
    {
        get => _matrixFadeStrength;
        set => Write(nameof(MatrixFadeStrength), _matrixFadeStrength = Math.Clamp(value, 0, 100));
    }

    public static MatrixDensity MatrixDensity
    {
        get => _matrixDensity;
        set => Write(nameof(MatrixDensity), (int)(_matrixDensity = value));
    }

    public static int MatrixOpacity
    {
        get => _matrixOpacity;
        set => Write(nameof(MatrixOpacity), _matrixOpacity = Math.Clamp(value, 30, 100));
    }

    public static LyricAnimStyle LyricAnimStyle
    {
        get => _lyricAnimStyle;
        set => Write(nameof(LyricAnimStyle), (int)(_lyricAnimStyle = value));
    }

    public static int LyricAnimSpeed
    {
        get => _lyricAnimSpeed;
        set => Write(nameof(LyricAnimSpeed), _lyricAnimSpeed = Math.Clamp(value, 50, 200));
    }

    public static int LyricAnimHeight
    {
        get => _lyricAnimHeight;
        set => Write(nameof(LyricAnimHeight), _lyricAnimHeight = Math.Clamp(value, 15, 80));
    }

    public static int LyricAnimStagger
    {
        get => _lyricAnimStagger;
        set => Write(nameof(LyricAnimStagger), _lyricAnimStagger = Math.Clamp(value, 10, 60));
    }

    public static int LyricLeadSec
    {
        get => _lyricLeadSec;
        set => Write(nameof(LyricLeadSec), _lyricLeadSec = Math.Clamp(value, 0, 9));
    }

    public static bool ComboEnabled
    {
        get => _comboEnabled;
        set => Write(nameof(ComboEnabled), _comboEnabled = value);
    }

    public static int ComboMinRepeats
    {
        get => _comboMinRepeats;
        set => Write(nameof(ComboMinRepeats), _comboMinRepeats = Math.Clamp(value, 2, 5));
    }

    public static ComboCounterStyle ComboCounterStyle
    {
        get => _comboCounterStyle;
        set => Write(nameof(ComboCounterStyle), (int)(_comboCounterStyle = value));
    }

    public static ComboSplitMode ComboSplitMode
    {
        get => _comboSplitMode;
        set => Write(nameof(ComboSplitMode), (int)(_comboSplitMode = value));
    }

    public static bool ComboIgnoreAdlibs
    {
        get => _comboIgnoreAdlibs;
        set => Write(nameof(ComboIgnoreAdlibs), _comboIgnoreAdlibs = value);
    }

    public static bool ComboStripBrackets
    {
        get => _comboStripBrackets;
        set => Write(nameof(ComboStripBrackets), _comboStripBrackets = value);
    }

    public static int ComboMinWordLen
    {
        get => _comboMinWordLen;
        set => Write(nameof(ComboMinWordLen), _comboMinWordLen = Math.Clamp(value, 1, 5));
    }

    public static double MinMusicDuration
    {
        get => _minMusicDuration;
        set => Write(nameof(MinMusicDuration), _minMusicDuration = value);
    }

    public static int MaxIslandWidth
    {
        get => _maxIslandWidth;
        set => Write(nameof(MaxIslandWidth), _maxIslandWidth = Math.Clamp(value, 220, 800));
    }

    public static bool InvertMediaGesture
    {
        get => _invertMediaGesture;
        set => Write(nameof(InvertMediaGesture), _invertMediaGesture = value);
    }

    public static bool ShowTrackArtist
    {
        get => _showTrackArtist;
        set => Write(nameof(ShowTrackArtist), _showTrackArtist = value);
    }

    public static bool InterceptWindowsNotifications
    {
        get => _interceptWindowsNotifications;
        set => Write(nameof(InterceptWindowsNotifications), _interceptWindowsNotifications = value);
    }

    public static bool NotifyDnd
    {
        get => _notifyDnd;
        set => Write(nameof(NotifyDnd), _notifyDnd = value);
    }

    public static bool NotifyHeadphones
    {
        get => _notifyHeadphones;
        set => Write(nameof(NotifyHeadphones), _notifyHeadphones = value);
    }

    public static bool NotifyBattery
    {
        get => _notifyBattery;
        set => Write(nameof(NotifyBattery), _notifyBattery = value);
    }

    public static bool NotifyVpn
    {
        get => _notifyVpn;
        set => Write(nameof(NotifyVpn), _notifyVpn = value);
    }

    public static bool NotifyTimer
    {
        get => _notifyTimer;
        set => Write(nameof(NotifyTimer), _notifyTimer = value);
    }

    public static bool NotifyObs
    {
        get => _notifyObs;
        set => Write(nameof(NotifyObs), _notifyObs = value);
    }

    public static bool NotifyShelf
    {
        get => _notifyShelf;
        set => Write(nameof(NotifyShelf), _notifyShelf = value);
    }

    public static bool NotifyDrive
    {
        get => _notifyDrive;
        set => Write(nameof(NotifyDrive), _notifyDrive = value);
    }

    public static int TimerSound
    {
        get => _timerSound;
        set => Write(nameof(TimerSound), _timerSound = Math.Clamp(value, 0, 5));
    }

    public static bool TimerIntervalMode
    {
        get => _timerIntervalMode;
        set => Write(nameof(TimerIntervalMode), _timerIntervalMode = value);
    }

    public static bool TimerOvertime
    {
        get => _timerOvertime;
        set => Write(nameof(TimerOvertime), _timerOvertime = value);
    }

    static LyricSource Known(LyricSource sources) => (sources & LyricSource.All) == 0 ? LyricSource.Usual : sources & LyricSource.All;

    static Place? ReadPlace() =>
        Read<string[]>(nameof(Place), []) is [var name, var region, var latitude, var longitude]
        && double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)
        && double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)
            ? new Place(name, region, lat, lon) : null;

    static string Coordinate(double degrees) => degrees.ToString("R", CultureInfo.InvariantCulture);

    static bool ReadSwitch(string name, bool fallback = true) => Read(name, fallback ? 1 : 0) != 0;

    static double ReadDouble(string name, double fallback)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(Key);
            return key?.GetValue(name) is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    static T Read<T>(string name, T fallback)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(Key);
            return key?.GetValue(name) is T value ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    static void Write(string name, bool value) => Write(name, value ? 1 : 0);

    static void Write(string name, int value) => Write(name, value, RegistryValueKind.DWord);

    static void Write(string name, double value) => Write(name, value.ToString("R", CultureInfo.InvariantCulture), RegistryValueKind.String);

    static void Write(string name, string value) => Write(name, value, RegistryValueKind.String);

    static void Write(string name, object value, RegistryValueKind kind)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(Key);
            key.SetValue(name, value, kind);
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }
}
