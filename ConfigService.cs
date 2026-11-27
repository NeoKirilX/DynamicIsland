using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace DynamicIsland;

static class ConfigService
{
    const string DniMagic = "DYNAMIC_ISLAND_CONFIG";
    const int DniVersion = 1;
    const string AppVersion = "1.28.0";

    public static string ExportsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DynamicIsland",
        "configs"
    );

    public static (bool Success, string PathOrError) ExportConfig(string? destPath = null, string name = "Мой остров")
    {
        try
        {
            Directory.CreateDirectory(ExportsDir);
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            var settingsSnapshot = new Dictionary<string, object?>
            {
                ["scale"] = Settings.Scale,
                ["gap"] = Settings.Gap,
                ["radius"] = Settings.Radius,
                ["glass"] = Settings.GlassAmount,
                ["height"] = Settings.HeightOffset,
                ["text_scale"] = Settings.TextScale,
                ["material"] = Settings.Material.ToString().ToLowerInvariant(),
                ["align"] = Settings.Align.ToString().ToLowerInvariant(),
                ["rim"] = Settings.Rim,
                ["shadow"] = Settings.Shadow,
                ["accent"] = Settings.Accent is { } c ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : null,
                ["hover"] = Settings.Hover.ToString().ToLowerInvariant(),
                ["seek_hover"] = Settings.SeekHover.ToString().ToLowerInvariant(),
                ["lyrics"] = Settings.Lyrics,
                ["lyric_sources"] = (int)Settings.LyricSources,
                ["lyric_effects"] = Settings.LyricEffects,
                ["lyric_anim_style"] = Settings.LyricAnimStyle.ToString().ToLowerInvariant(),
                ["lyric_anim_speed"] = Settings.LyricAnimSpeed,
                ["lyric_anim_height"] = Settings.LyricAnimHeight,
                ["lyric_anim_stagger"] = Settings.LyricAnimStagger,
                ["lyric_lead_sec"] = Settings.LyricLeadSec,
                ["combo_enabled"] = Settings.ComboEnabled,
                ["combo_min_repeats"] = Settings.ComboMinRepeats,
                ["combo_counter_style"] = Settings.ComboCounterStyle.ToString().ToLowerInvariant(),
                ["combo_split_mode"] = Settings.ComboSplitMode.ToString().ToLowerInvariant(),
                ["combo_ignore_adlibs"] = Settings.ComboIgnoreAdlibs,
                ["combo_strip_brackets"] = Settings.ComboStripBrackets,
                ["combo_min_word_len"] = Settings.ComboMinWordLen,
                ["weather"] = Settings.Weather,
                ["system_stats"] = Settings.SystemStats,
                ["hide_fullscreen"] = Settings.HideFullscreen,
                ["click_lock"] = Settings.ClickLock,
                ["app_volume"] = Settings.AppVolume,
                ["network"] = Settings.Network,
                ["capitalize_title"] = Settings.CapitalizeTitle,
                ["obs"] = Settings.Obs,
                ["line_bar"] = Settings.LineBar,
                ["equalizer_dots"] = Settings.EqualizerDots,
                ["compact_equalizer"] = Settings.CompactEqualizer,
                ["compact_eq_dots"] = Settings.CompactEqDots,
                ["compact_eq_bars"] = Settings.CompactEqBars,
                ["mini_equalizer"] = Settings.MiniEqualizer,
                ["eq_bars"] = Settings.EqBars,
                ["eq_sensitivity"] = Settings.EqSensitivity,
                ["matrix_rows"] = Settings.MatrixRows,
                ["matrix_fade_strength"] = Settings.MatrixFadeStrength,
                ["matrix_density"] = Settings.MatrixDensity.ToString().ToLowerInvariant(),
                ["matrix_opacity"] = Settings.MatrixOpacity,
                ["player_bg"] = Settings.Backdrop.ToString().ToLowerInvariant(),
                ["language"] = Settings.Language,
                ["notch"] = Settings.Notch,
                ["pos_x"] = Settings.PosX,
                ["pos_y"] = Settings.PosY,
                ["island_align"] = Settings.IslandAlign.ToString().ToLowerInvariant(),
                ["invert_gesture"] = Settings.InvertMediaGesture,
            };

            var data = new Dictionary<string, object>
            {
                ["magic"] = DniMagic,
                ["version"] = DniVersion,
                ["app_version"] = AppVersion,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["exported_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["name"] = name,
                ["screen"] = new Dictionary<string, double>
                {
                    ["width"] = screenW,
                    ["height"] = screenH,
                },
                ["settings"] = settingsSnapshot,
            };

            destPath ??= Path.Combine(ExportsDir, $"island_setup_{DateTime.Now:yyyyMMdd_HHmmss}.dni");
            if (!destPath.EndsWith(".dni", StringComparison.OrdinalIgnoreCase))
                destPath += ".dni";

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(destPath, json);
            return (true, destPath);
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return (false, ex.Message);
        }
    }

    public static bool ExportWithDialog()
    {
        try
        {
            Directory.CreateDirectory(ExportsDir);
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Dynamic Island Config (*.dni)|*.dni",
                InitialDirectory = ExportsDir,
                FileName = $"island_setup_{DateTime.Now:yyyyMMdd_HHmmss}.dni"
            };
            if (dialog.ShowDialog() == true)
            {
                var res = ExportConfig(dialog.FileName);
                return res.Success;
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
        return false;
    }

    public static bool ImportWithDialog()
    {
        try
        {
            Directory.CreateDirectory(ExportsDir);
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Dynamic Island Config (*.dni)|*.dni|All Files (*.*)|*.*",
                InitialDirectory = ExportsDir
            };
            if (dialog.ShowDialog() == true)
            {
                var res = ImportConfig(dialog.FileName);
                return res.Success;
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
        return false;
    }

    public static (bool Success, string Message) ImportConfig(string filePath, bool adaptScreen = true)
    {
        try
        {
            if (!File.Exists(filePath))
                return (false, "Файл не найден");

            string json = File.ReadAllText(filePath);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            JsonElement settings;
            if (root.TryGetProperty("magic", out JsonElement magic) && magic.GetString() == DniMagic && root.TryGetProperty("settings", out JsonElement s1))
            {
                settings = s1;
            }
            else if (root.TryGetProperty("settings", out JsonElement s2))
            {
                settings = s2;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                settings = root;
            }
            else
            {
                return (false, "Неверный формат файла конфигурации");
            }

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;
            double origW = screenW, origH = screenH;

            if (root.TryGetProperty("screen", out JsonElement screen))
            {
                if (screen.TryGetProperty("width", out JsonElement w)) origW = w.GetDouble();
                if (screen.TryGetProperty("height", out JsonElement h)) origH = h.GetDouble();
            }

            double scaleRatio = (origW > 100 && origH > 100 && adaptScreen)
                ? ((screenW / origW) + (screenH / origH)) / 2.0
                : 1.0;

            foreach (JsonProperty prop in settings.EnumerateObject())
            {
                string key = prop.Name;
                JsonElement val = prop.Value;

                switch (key)
                {
                    case "scale":
                        int s = val.GetInt32();
                        if (adaptScreen && Math.Abs(scaleRatio - 1.0) > 0.05) s = (int)Math.Round(s * scaleRatio);
                        Settings.Scale = s;
                        break;
                    case "gap":
                        Settings.Gap = val.GetInt32();
                        break;
                    case "radius":
                        Settings.Radius = val.GetInt32();
                        break;
                    case "glass":
                        Settings.GlassAmount = val.GetInt32();
                        break;
                    case "height":
                        Settings.HeightOffset = val.GetInt32();
                        break;
                    case "text_scale":
                        Settings.TextScale = val.GetInt32();
                        break;
                    case "material":
                        if (Enum.TryParse(val.GetString(), true, out MaterialStyle mat)) Settings.Material = mat;
                        break;
                    case "align":
                        if (Enum.TryParse(val.GetString(), true, out LyricAlign align)) Settings.Align = align;
                        break;
                    case "rim":
                        Settings.Rim = val.GetBoolean();
                        break;
                    case "shadow":
                        Settings.Shadow = val.GetBoolean();
                        break;
                    case "accent":
                        if (val.GetString() is { } hex && hex.StartsWith("#") && hex.Length == 7)
                        {
                            byte r = Convert.ToByte(hex.Substring(1, 2), 16);
                            byte g = Convert.ToByte(hex.Substring(3, 2), 16);
                            byte b = Convert.ToByte(hex.Substring(5, 2), 16);
                            Settings.Accent = Color.FromRgb(r, g, b);
                        }
                        else Settings.Accent = null;
                        break;
                    case "hover":
                        if (Enum.TryParse(val.GetString(), true, out Hover hover)) Settings.Hover = hover;
                        break;
                    case "seek_hover":
                        if (Enum.TryParse(val.GetString(), true, out SeekHover sh)) Settings.SeekHover = sh;
                        break;
                    case "lyrics":
                        Settings.Lyrics = val.GetBoolean();
                        break;
                    case "lyric_sources":
                        Settings.LyricSources = (LyricSource)val.GetInt32();
                        break;
                    case "lyric_effects":
                        Settings.LyricEffects = val.GetBoolean();
                        break;
                    case "lyric_anim_style":
                        if (Enum.TryParse(val.GetString(), true, out LyricAnimStyle anim)) Settings.LyricAnimStyle = anim;
                        break;
                    case "lyric_anim_speed":
                        Settings.LyricAnimSpeed = val.GetInt32();
                        break;
                    case "lyric_anim_height":
                        Settings.LyricAnimHeight = val.GetInt32();
                        break;
                    case "lyric_anim_stagger":
                        Settings.LyricAnimStagger = val.GetInt32();
                        break;
                    case "lyric_lead_sec":
                        Settings.LyricLeadSec = val.GetInt32();
                        break;
                    case "combo_enabled":
                        Settings.ComboEnabled = val.GetBoolean();
                        break;
                    case "combo_min_repeats":
                        Settings.ComboMinRepeats = val.GetInt32();
                        break;
                    case "combo_counter_style":
                        if (Enum.TryParse(val.GetString(), true, out ComboCounterStyle ccs)) Settings.ComboCounterStyle = ccs;
                        break;
                    case "combo_split_mode":
                        if (Enum.TryParse(val.GetString(), true, out ComboSplitMode csm)) Settings.ComboSplitMode = csm;
                        break;
                    case "combo_ignore_adlibs":
                        Settings.ComboIgnoreAdlibs = val.GetBoolean();
                        break;
                    case "combo_strip_brackets":
                        Settings.ComboStripBrackets = val.GetBoolean();
                        break;
                    case "combo_min_word_len":
                        Settings.ComboMinWordLen = val.GetInt32();
                        break;
                    case "weather":
                        Settings.Weather = val.GetBoolean();
                        break;
                    case "system_stats":
                        Settings.SystemStats = val.GetBoolean();
                        break;
                    case "hide_fullscreen":
                        Settings.HideFullscreen = val.GetBoolean();
                        break;
                    case "click_lock":
                        Settings.ClickLock = val.GetBoolean();
                        break;
                    case "app_volume":
                        Settings.AppVolume = val.GetBoolean();
                        break;
                    case "network":
                        Settings.Network = val.GetBoolean();
                        break;
                    case "capitalize_title":
                        Settings.CapitalizeTitle = val.GetBoolean();
                        break;
                    case "obs":
                        Settings.Obs = val.GetBoolean();
                        break;
                    case "line_bar":
                        Settings.LineBar = val.GetBoolean();
                        break;
                    case "equalizer_dots":
                        Settings.EqualizerDots = val.GetBoolean();
                        break;
                    case "compact_equalizer":
                        Settings.CompactEqualizer = val.GetBoolean();
                        break;
                    case "compact_eq_dots":
                        Settings.CompactEqDots = val.GetBoolean();
                        break;
                    case "compact_eq_bars":
                        Settings.CompactEqBars = val.GetInt32();
                        break;
                    case "mini_equalizer":
                        Settings.MiniEqualizer = val.GetBoolean();
                        break;
                    case "eq_bars":
                        Settings.EqBars = val.GetInt32();
                        break;
                    case "eq_sensitivity":
                        Settings.EqSensitivity = val.GetInt32();
                        break;
                    case "matrix_rows":
                        Settings.MatrixRows = val.GetInt32();
                        break;
                    case "matrix_fade_strength":
                        Settings.MatrixFadeStrength = val.GetInt32();
                        break;
                    case "matrix_density":
                        if (Enum.TryParse(val.GetString(), true, out MatrixDensity md)) Settings.MatrixDensity = md;
                        break;
                    case "matrix_opacity":
                        Settings.MatrixOpacity = val.GetInt32();
                        break;
                    case "player_bg":
                        if (Enum.TryParse(val.GetString(), true, out Backdrop bd)) Settings.Backdrop = bd;
                        break;
                    case "language":
                        Settings.Language = val.GetString() ?? "ru";
                        break;
                    case "notch":
                        Settings.Notch = val.GetBoolean();
                        break;
                    case "pos_x":
                        Settings.PosX = val.GetInt32();
                        break;
                    case "pos_y":
                        Settings.PosY = val.GetInt32();
                        break;
                    case "island_align":
                        if (Enum.TryParse(val.GetString(), true, out IslandAlign ia)) Settings.IslandAlign = ia;
                        break;
                    case "invert_gesture":
                    case "invert_media_gesture":
                        Settings.InvertMediaGesture = val.GetBoolean();
                        break;
                }
            }

            return (true, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            App.Log(ex);
            return (false, ex.Message);
        }
    }

    public static bool IsConfigFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
        if (filePath.EndsWith(".dni", StringComparison.OrdinalIgnoreCase)) return true;
        if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("magic", out var m) && m.GetString() == DniMagic) return true;
                    if (root.TryGetProperty("settings", out _)) return true;
                    if (root.TryGetProperty("scale", out _) || root.TryGetProperty("gap", out _) || root.TryGetProperty("notch", out _)) return true;
                }
            }
            catch { }
        }
        return false;
    }

}
