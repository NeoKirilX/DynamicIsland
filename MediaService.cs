using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Manager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using Status = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace DynamicIsland;

sealed class MediaService
{
    const string Untitled = "Без названия";
    const string BarSeparator = " – ";
    const int MinPercentToMeasure = 10;
    const int PollPlayingMs = 250, PollPausedMs = 1000;
    const int UnsetYear = 2000;
    const int BarMissesToForget = 3;

    readonly Dispatcher _ui;
    readonly PlayerBar _bar = new();
    Manager? _manager;
    Session? _session;
    Session? _pinned;
    int _refreshVersion;

    string _title = "", _artist = "";
    TimeSpan _position, _duration;
    DateTime _positionAt = DateTime.UtcNow;
    DateTimeOffset _timelineStamp;
    double _rate = 1;
    Status _status;

    bool _noTimeline;
    bool _barSearched;
    bool _barFound;
    string _barText = "", _barTitle = "", _barArtist = "";
    TimeSpan _barTime = TimeSpan.MinValue;
    int _barPercent = -1;
    int _barMisses;
    TimeSpan _measuredLength;
    TimeSpan _assumedLength;

    public MediaService(Dispatcher ui) => _ui = ui;

    public event Action? Changed;

    public string RawTitle => Capitalize(CleanTrackTitle(_title.Length > 0 ? _title : _barTitle));
    public string Title
    {
        get
        {
            string t = RawTitle;
            if (Settings.ShowTrackArtist && Artist.Length > 0)
                return $"{t} • {Artist}";
            return t;
        }
    }
    public string Artist => _title.Length > 0 ? _artist : _barArtist;
    public string Name => Title.Length > 0 ? Title : SourceApp.Name(Source) is { Length: > 0 } app ? app : Untitled;
    public ImageSource? Art { get; private set; }
    public Color[] Palette { get; private set; } = CoverPalette.Plain;
    public Color Accent => Palette[0];
    public bool IsPlaying { get; private set; }
    public bool HasTrack => _session != null && !IsAd(Source, RawTitle, _artist, _duration) && (RawTitle.Length > 0 || (_noTimeline && _barSearched && IsLive));
    public TimeSpan Duration => _duration;
    public bool HasBarPosition => _noTimeline && _barFound;
    public bool Seekable => _noTimeline ? _barFound : _duration.TotalSeconds >= 1;

    public void RaisePlayer() => SourceApp.BringToFront(Source, Title);

    static bool IsAd(string source, string title, string artist, TimeSpan duration)
    {
        bool isSpotify = source.Contains("Spotify", StringComparison.OrdinalIgnoreCase)
            || string.Equals(artist, "Spotify", StringComparison.OrdinalIgnoreCase)
            || string.Equals(title, "Spotify", StringComparison.OrdinalIgnoreCase);

        double sec = duration.TotalSeconds;

        if (isSpotify)
        {
            if ((sec >= 28.5 && sec <= 31.5) || (sec >= 13.5 && sec <= 16.5)) return true;
            if (string.Equals(title, "Advertisement", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(title, "Spotify", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(artist, "Spotify", StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(artist) && sec >= 28.0 && sec <= 32.0))
            {
                return true;
            }
        }

        if (string.Equals(title, "Advertisement", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Advertisement", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(artist, "Advertisement", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Math.Abs(sec - 30.0) <= 0.6 && (isSpotify || string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title)))
        {
            return true;
        }

        return false;
    }

    static readonly Regex TrackTagsPattern = new(
        @"\s*[\(\[][^\)\]]*\b(?:official\s*(?:music|video|audio)?|video|audio|lyrics?|visuali[sz]er|remaster(?:ed)?|hd|hq|4k|8k|mv|feat(?:uring|\.)?|клип|премьера|slowed(?:\s*\+\s*reverb)?|sped\s*up|speed\s*up|reverb|nightcore|hardstyle|phonk|remix|edit|prod(?:\s*by)?|замедленно|ускорено|ремикс|басс|хрип|басы)[^\)\]]*[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string CleanTrackTitle(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string cleaned = TrackTagsPattern.Replace(text, "");
        cleaned = Regex.Replace(cleaned, @"[\(\[]\s*[\)\]]", "");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '-', '—', '–', '|');
        return cleaned.Length > 0 ? cleaned : text;
    }

    static string Capitalize(string text)
    {
        if (!Settings.CapitalizeTitle || string.IsNullOrEmpty(text)) return text;
        int i = 0;
        while (i < text.Length && !char.IsLetter(text[i])) i++;
        if (i >= text.Length || char.IsUpper(text[i])) return text;
        return text[..i] + char.ToUpper(text[i]) + text[(i + 1)..];
    }

    bool IsShortSound(Session session)
    {
        try
        {
            var timeline = session.GetTimelineProperties();
            double seconds = (timeline.EndTime - timeline.StartTime).TotalSeconds;
            if (seconds > 0 && seconds < Settings.MinMusicDuration) return true;
            bool isSpotify = session.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase);
            if (isSpotify)
            {
                if ((seconds >= 28.5 && seconds <= 31.5) || (seconds >= 13.5 && seconds <= 16.5)) return true;
            }
            else if (Math.Abs(seconds - 30.0) <= 0.6)
            {
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    public double Progress => _duration.TotalSeconds >= 1 ? Math.Clamp(Position / _duration, 0, 1)
        : HasBarPosition && _barPercent > 0 ? _barPercent / 100.0 : 0;

    public string Source
    {
        get
        {
            try { return _session?.SourceAppUserModelId ?? ""; }
            catch { return ""; }
        }
    }

    public TimeSpan Position
    {
        get
        {
            TimeSpan position = RunningPosition;
            if (position < TimeSpan.Zero || (_noTimeline && !_barFound)) return TimeSpan.Zero;
            bool endless = _noTimeline && _duration <= TimeSpan.Zero;
            return position > _duration && !endless ? _duration : position;
        }
    }

    TimeSpan RunningPosition => IsPlaying ? _position + (DateTime.UtcNow - _positionAt) * _rate : _position;

    bool IsLive => _status is Status.Playing or Status.Paused;

    public void AssumeDuration(TimeSpan length)
    {
        _assumedLength = length;
        if (_noTimeline) UpdateBarDuration();
    }

    public async Task StartAsync()
    {
        _manager = await Manager.RequestAsync();
        _manager.CurrentSessionChanged += (_, _) => _ui.InvokeAsync(() =>
        {
            UnpinIfAnotherPlays();
            AttachSession();
        });
        _manager.SessionsChanged += (_, _) => _ui.InvokeAsync(AttachSession);
        AttachSession();
        WatchBar();
        WatchPlayback();
    }

    public bool SwitchSession(int direction)
    {
        try
        {
            var sessions = _manager?.GetSessions();
            if (sessions == null || sessions.Count < 2) return false;

            int count = sessions.Count;
            int at = IndexOf(sessions, session => ReferenceEquals(session, _session));
            if (at < 0) at = IndexOf(sessions, session => SameApp(session, _session));

            _pinned = sessions[at < 0 ? (direction > 0 ? 0 : count - 1) : ((at + direction) % count + count) % count];
            AttachSession();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void TogglePlay() => SendCommand(session => session.TryTogglePlayPauseAsync());

    public void Next() => SendCommand(session => session.TrySkipNextAsync());

    public void Previous() => SendCommand(session => session.TrySkipPreviousAsync());

    public async void Seek(double fraction)
    {
        Session? session = _session;
        if (session == null) return;
        if (_noTimeline)
        {
            if (!_barFound || !await Task.Run(() => _bar.Seek(fraction)) || !ReferenceEquals(session, _session)) return;
            _position = _duration * Math.Clamp(fraction, 0, 1);
            _positionAt = DateTime.UtcNow;
            _barTime = TimeSpan.MinValue;
            _barPercent = -1;
            Changed?.Invoke();
            return;
        }
        if (_duration <= TimeSpan.Zero) return;
        try
        {
            var target = TimeSpan.FromTicks((long)(_duration.Ticks * Math.Clamp(fraction, 0, 1)));
            TimeSpan start = session.GetTimelineProperties().StartTime;
            if (await session.TryChangePlaybackPositionAsync((start + target).Ticks) && ReferenceEquals(session, _session))
            {
                _position = target;
                _positionAt = DateTime.UtcNow;
                Changed?.Invoke();
            }
        }
        catch { }
    }

    async void SendCommand(Func<Session, IAsyncOperation<bool>> command)
    {
        try { if (_session != null) await command(_session); }
        catch { }
    }

    static int IndexOf(IReadOnlyList<Session> sessions, Func<Session, bool> match)
    {
        for (int i = 0; i < sessions.Count; i++)
            if (match(sessions[i])) return i;
        return -1;
    }

    void UnpinIfAnotherPlays()
    {
        try
        {
            Session? current = _manager?.GetCurrentSession();
            if (current != null && !SameApp(current, _pinned) && IsPlayingSession(current)) _pinned = null;
        }
        catch { }
    }

    static bool SameApp(Session? a, Session? b)
    {
        if (a == null || b == null) return false;
        if (ReferenceEquals(a, b)) return true;
        try { return a.SourceAppUserModelId == b.SourceAppUserModelId; }
        catch { return false; }
    }

    bool IsPlayingSession(Session session)
    {
        try { return session.GetPlaybackInfo().PlaybackStatus == Status.Playing && !IsShortSound(session); }
        catch { return false; }
    }

    void AttachSession()
    {
        if (_session != null)
        {
            try
            {
                _session.MediaPropertiesChanged -= OnProperties;
                _session.PlaybackInfoChanged -= OnPlayback;
                _session.TimelinePropertiesChanged -= OnTimeline;
            }
            catch { }
        }

        Session? previous = _session;
        _session = PickSession();
        _timelineStamp = default;
        if (!SameApp(previous, _session))
        {
            _noTimeline = _barSearched = false;
            ForgetBar();
        }

        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnProperties;
            _session.PlaybackInfoChanged += OnPlayback;
            _session.TimelinePropertiesChanged += OnTimeline;
        }
        _ = RefreshAsync();
    }

    Session? PickSession()
    {
        try
        {
            if (_pinned != null)
            {
                if (_manager!.GetSessions().FirstOrDefault(session => SameApp(session, _pinned)) is { } pinned) return pinned;
                _pinned = null;
            }

            Session? current = _manager?.GetCurrentSession();
            if (current != null && IsPlayingSession(current)) return current;
            return _manager!.GetSessions().FirstOrDefault(IsPlayingSession) ?? current;
        }
        catch
        {
            return null;
        }
    }

    void OnProperties(Session session, MediaPropertiesChangedEventArgs e) => _ui.InvokeAsync(() => _ = RefreshAsync());

    void OnPlayback(Session session, PlaybackInfoChangedEventArgs e) => _ui.InvokeAsync(() =>
    {
        ReadPlayback();
        ReadTimeline();
        Changed?.Invoke();
    });

    void OnTimeline(Session session, TimelinePropertiesChangedEventArgs e) => _ui.InvokeAsync(() =>
    {
        ReadPlayback();
        ReadTimeline();
        Changed?.Invoke();
    });

    async Task RefreshAsync()
    {
        Session? session = _session;
        int version = ++_refreshVersion;

        if (session == null)
        {
            _title = _artist = "";
            Art = null;
            Palette = CoverPalette.Plain;
            IsPlaying = false;
            Changed?.Invoke();
            return;
        }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            if (version != _refreshVersion) return;

            string title = properties.Title ?? "";
            bool sameTrack = title == _title;
            (ImageSource? art, Color[] palette) = (null, CoverPalette.Plain);
            if (properties.Thumbnail != null)
            {
                (art, palette) = await LoadCoverAsync(properties.Thumbnail);
                if (version != _refreshVersion) return;
            }

            if (art == null && title.Length > 0)
            {
                (art, palette) = await FetchFallbackCoverAsync(title, properties.Artist ?? "");
                if (version != _refreshVersion) return;
            }

            _title = title;
            _artist = properties.Artist ?? "";
            if (art != null || !sameTrack)
            {
                Art = art;
                Palette = palette;
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }

        ReadPlayback();
        ReadTimeline();
        Changed?.Invoke();
    }

    static async Task<(ImageSource? Art, Color[] Palette)> LoadCoverAsync(IRandomAccessStreamReference thumbnail)
    {
        try
        {
            using var source = await thumbnail.OpenReadAsync();
            using var stream = source.AsStreamForRead();
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            return await Task.Run(() => CoverPalette.Decode(buffer));
        }
        catch
        {
            return (null, CoverPalette.Plain);
        }
    }

    static readonly HttpClient WebCoverClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    static readonly string CoverCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIsland", "Cache", "Covers");

    static async Task<(ImageSource? Art, Color[] Palette)> FetchFallbackCoverAsync(string title, string artist)
    {
        try
        {
            Directory.CreateDirectory(CoverCacheDir);
            string safeKey = new string($"{artist}_{title}".ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            if (safeKey.Length > 60) safeKey = safeKey[..60];
            string cachePath = Path.Combine(CoverCacheDir, safeKey + ".jpg");

            if (File.Exists(cachePath))
            {
                byte[] bytes = await File.ReadAllBytesAsync(cachePath);
                using var ms = new MemoryStream(bytes);
                return CoverPalette.Decode(ms);
            }

            string clean = CleanTrackTitle(title);
            string query = Uri.EscapeDataString($"{artist} {clean}".Trim());

            string? coverUrl = null;
            try
            {
                string itunesUrl = $"https://itunes.apple.com/search?term={query}&entity=song&limit=1";
                using var itunesResp = await WebCoverClient.GetAsync(itunesUrl);
                if (itunesResp.IsSuccessStatusCode)
                {
                    using var json = await JsonDocument.ParseAsync(await itunesResp.Content.ReadAsStreamAsync());
                    if (json.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                    {
                        var item = results[0];
                        if (item.TryGetProperty("artworkUrl100", out var artProp))
                        {
                            coverUrl = artProp.GetString()?.Replace("100x100bb", "600x600bb");
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(coverUrl))
            {
                try
                {
                    string deezerUrl = $"https://api.deezer.com/search?limit=1&q={query}";
                    using var deezerResp = await WebCoverClient.GetAsync(deezerUrl);
                    if (deezerResp.IsSuccessStatusCode)
                    {
                        using var json = await JsonDocument.ParseAsync(await deezerResp.Content.ReadAsStreamAsync());
                        if (json.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                        {
                            var item = data[0];
                            if (item.TryGetProperty("album", out var album))
                            {
                                if (album.TryGetProperty("cover_xl", out var cXl)) coverUrl = cXl.GetString();
                                else if (album.TryGetProperty("cover_big", out var cBig)) coverUrl = cBig.GetString();
                            }
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(coverUrl))
            {
                byte[] imgData = await WebCoverClient.GetByteArrayAsync(coverUrl);
                if (imgData.Length > 0)
                {
                    try { await File.WriteAllBytesAsync(cachePath, imgData); } catch { }
                    using var ms = new MemoryStream(imgData);
                    return CoverPalette.Decode(ms);
                }
            }
        }
        catch { }
        return (null, CoverPalette.Plain);
    }

    bool ReadPlayback()
    {
        if (_session == null) return false;
        try
        {
            var info = _session.GetPlaybackInfo();
            Status status = info.PlaybackStatus;
            bool playing = status == Status.Playing;
            double rate = info.PlaybackRate is double told && told > 0 ? told : 1;
            bool changed = playing != IsPlaying || rate != _rate || status != _status;
            if (playing != IsPlaying || rate != _rate)
            {
                _position = Position;
                _positionAt = DateTime.UtcNow;
                IsPlaying = playing;
                _rate = rate;
            }
            if (status != _status)
            {
                _status = status;
                if (IsLive) _bar.ResetRetry();
                else ForgetBar();
            }
            return changed;
        }
        catch
        {
            return false;
        }
    }

    bool ReadTimeline()
    {
        if (_session == null) return false;
        try
        {
            var timeline = _session.GetTimelineProperties();
            _noTimeline = timeline.LastUpdatedTime.Year < UnsetYear && timeline.EndTime <= timeline.StartTime;
            if (_noTimeline)
            {
                UpdateBarDuration();
                return false;
            }

            TimeSpan duration = timeline.EndTime > timeline.StartTime ? timeline.EndTime - timeline.StartTime : TimeSpan.Zero;
            bool resized = duration != _duration;
            _duration = duration;
            if (timeline.LastUpdatedTime == _timelineStamp) return resized;

            _timelineStamp = timeline.LastUpdatedTime;
            _position = timeline.Position - timeline.StartTime;
            DateTime updated = timeline.LastUpdatedTime.UtcDateTime;
            _positionAt = updated.Year < UnsetYear ? DateTime.UtcNow : updated;
            return true;
        }
        catch
        {
            return false;
        }
    }

    async void WatchPlayback()
    {
        while (true)
        {
            await Task.Delay(IsPlaying ? PollPlayingMs : PollPausedMs);
            try
            {
                if (ReadPlayback() | ReadTimeline()) Changed?.Invoke();
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        }
    }

    async void WatchBar()
    {
        while (true)
        {
            await Task.Delay(IsPlaying ? PollPlayingMs : PollPausedMs);
            Session? session = _session;
            if (session == null || !_noTimeline || !IsLive) continue;
            try
            {
                string app = Source;
                if (!PlayerBar.Supports(app)) continue;
                PlayerBar.Reading? reading = await Task.Run(() => _bar.Read(app));
                if (ReferenceEquals(session, _session) && _noTimeline && IsLive) ApplyBarReading(reading);
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        }
    }

    void ApplyBarReading(PlayerBar.Reading? reading)
    {
        bool hadTrack = HasTrack;
        (string title, TimeSpan duration) = (Title, _duration);
        _barSearched = true;

        if (reading is { } bar)
        {
            _barFound = true;
            if (bar.Text != _barText)
            {
                ForgetBar();
                _barFound = true;
                _barText = bar.Text;
                int dash = bar.Text.IndexOf(BarSeparator, StringComparison.Ordinal);
                (_barArtist, _barTitle) = dash > 0 ? (bar.Text[..dash], bar.Text[(dash + BarSeparator.Length)..]) : ("", bar.Text);
            }
            _barMisses = 0;
            if (bar.Time is { } time && time != _barTime)
            {
                _barTime = _position = time;
                _positionAt = DateTime.UtcNow;
            }
            MeasureLength(bar.Percent);
        }
        else if (_barFound && ++_barMisses >= BarMissesToForget)
        {
            ForgetBar();
        }

        UpdateBarDuration();
        if (HasTrack != hadTrack || Title != title || _duration != duration) Changed?.Invoke();
    }

    void MeasureLength(int percent)
    {
        if (percent == _barPercent) return;

        bool stepped = IsPlaying && percent == _barPercent + 1;
        _barPercent = percent;
        if (!stepped || percent < MinPercentToMeasure) return;

        double length = RunningPosition.TotalSeconds * 100 / (percent - 0.5);
        if (_measuredLength == TimeSpan.Zero || Math.Abs(length - _measuredLength.TotalSeconds) > 50.0 / percent)
            _measuredLength = TimeSpan.FromSeconds(Math.Round(length));
    }

    void UpdateBarDuration()
    {
        if (_assumedLength > TimeSpan.Zero && _barPercent >= 0
            && Math.Abs(RunningPosition / _assumedLength * 100 - _barPercent) > 1.5 + 150 / _assumedLength.TotalSeconds)
            _assumedLength = TimeSpan.Zero;
        _duration = !_barFound ? TimeSpan.Zero : _measuredLength > TimeSpan.Zero ? _measuredLength : _assumedLength;
    }

    void ForgetBar()
    {
        _barFound = false;
        _barText = _barTitle = _barArtist = "";
        _barTime = TimeSpan.MinValue;
        _barPercent = -1;
        _measuredLength = _assumedLength = TimeSpan.Zero;
    }
}
