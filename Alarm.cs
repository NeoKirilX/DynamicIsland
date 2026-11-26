using System.IO;
using System.Media;

namespace DynamicIsland;

sealed class Alarm
{
    SoundPlayer? _player;

    public void Ring()
    {
        Stop();
        if (Settings.TimerSound == 5) return; // Mute
        try
        {
            string winMedia = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
            string soundFile = Settings.TimerSound switch
            {
                0 => Path.Combine(winMedia, "Alarm01.wav"),
                1 => Path.Combine(winMedia, "chimes.wav"),
                2 => Path.Combine(winMedia, "tada.wav"),
                3 => Path.Combine(winMedia, "notify.wav"),
                _ => Path.Combine(winMedia, "Alarm01.wav")
            };

            if (!File.Exists(soundFile)) soundFile = Path.Combine(winMedia, "Alarm01.wav");

            if (!File.Exists(soundFile))
            {
                SystemSounds.Exclamation.Play();
                return;
            }
            _player = new SoundPlayer(soundFile);
            _player.PlayLooping();
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }

    public void Stop()
    {
        try
        {
            _player?.Stop();
            _player?.Dispose();
        }
        catch { }
        _player = null;
    }
}
