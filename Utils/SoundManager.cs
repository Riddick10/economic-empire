using Raylib_cs;
using GrandStrategyGame.Data;

namespace GrandStrategyGame;

public enum SoundEffect
{
    Click,
    Build,
    NotificationInfo,
    NotificationWarning,
    NotificationDanger,
    NotificationSuccess,
    NotificationTwitter, // Twitter/X-Stil Benachrichtigungston
    Pause,
    Unpause,
    SpeedChange,
    Coin
}

public static class SoundManager
{
    private static readonly Dictionary<SoundEffect, Sound> _sounds = new();
    private static float _volume = 0.5f;
    private static bool _initialized;

    public static float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            foreach (var sound in _sounds.Values)
                Raylib.SetSoundVolume(sound, _volume);
        }
    }

    public static void Initialize()
    {
        string basePath = CountryDataLoader.FindBasePath();
        string soundsPath = Path.Combine(basePath, "Data", "Sounds");

        if (!Directory.Exists(soundsPath))
        {
            Console.WriteLine("[SoundManager] Sounds-Ordner nicht gefunden.");
            return;
        }

        var soundFiles = new Dictionary<SoundEffect, string>
        {
            { SoundEffect.Click, "click.wav" },
            { SoundEffect.Build, "build.wav" },
            { SoundEffect.NotificationInfo, "notification_info.wav" },
            { SoundEffect.NotificationWarning, "notification_warning.wav" },
            { SoundEffect.NotificationDanger, "notification_danger.wav" },
            { SoundEffect.NotificationSuccess, "notification_success.wav" },
            { SoundEffect.NotificationTwitter, "twitter_notification.wav" }, // Twitter/X-Stil Sound
            { SoundEffect.Pause, "pause.wav" },
            { SoundEffect.Unpause, "unpause.wav" },
            { SoundEffect.SpeedChange, "speed_change.wav" },
            { SoundEffect.Coin, "coin.wav" },
        };

        foreach (var (effect, filename) in soundFiles)
        {
            string path = Path.Combine(soundsPath, filename);
            if (File.Exists(path))
            {
                _sounds[effect] = Raylib.LoadSound(path);
            }
            else
            {
                Console.WriteLine($"[SoundManager] Nicht gefunden: {filename}");
            }
        }

        foreach (var sound in _sounds.Values)
            Raylib.SetSoundVolume(sound, _volume);

        _initialized = true;
        Console.WriteLine($"[SoundManager] {_sounds.Count} Sounds geladen.");
    }

    public static void Play(SoundEffect effect)
    {
        if (!_initialized) return;
        if (_sounds.TryGetValue(effect, out var sound))
            Raylib.PlaySound(sound);
    }

    /// <summary>
    /// Spielt einen Sound ab, mit Fallback auf einen anderen Sound falls der erste nicht verfuegbar ist
    /// </summary>
    public static void PlayWithFallback(SoundEffect effect, SoundEffect fallback)
    {
        if (!_initialized) return;
        if (_sounds.TryGetValue(effect, out var sound))
            Raylib.PlaySound(sound);
        else if (_sounds.TryGetValue(fallback, out var fallbackSound))
            Raylib.PlaySound(fallbackSound);
    }

    public static void Unload()
    {
        foreach (var sound in _sounds.Values)
            Raylib.UnloadSound(sound);
        _sounds.Clear();
        _initialized = false;
    }
}
