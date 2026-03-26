using Raylib_cs;
using GrandStrategyGame.Data;

namespace GrandStrategyGame;

/// <summary>
/// Verwaltet die Hintergrundmusik als Shuffle-Playlist.
/// Spielt zufällige Tracks aus dem Data/Music-Ordner ab.
/// </summary>
public class MusicManager
{
    private const string MainThemeFile = "MD_maintheme.ogg";

    private readonly List<string> _trackPaths = new();
    private readonly List<int> _shuffleOrder = new();
    private int _currentIndex = -1;
    private Music? _currentMusic;
    private float _volume = 0f;
    private bool _initialized;
    private bool _playingMainTheme;
    private bool _switchingTrack;
    private int _failCount;
    private const int MaxConsecutiveFails = 3;

    // Naechster Track wird im Hintergrund vorbereitet
    private Music? _nextMusic;
    private string? _nextTrackName;
    private Task? _preloadTask;

    // Cache fuer Track-Namen (vermeidet per-Frame Allokation)
    private readonly List<string> _cachedTrackNames = new();
    private bool _trackNamesCacheDirty = true;

    private static readonly string[] SupportedExtensions = { ".mp3", ".ogg", ".wav", ".flac" };

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_currentMusic.HasValue)
            {
                Raylib.SetMusicVolume(_currentMusic.Value, _volume);
            }
        }
    }

    public bool IsPlaying => _currentMusic.HasValue && Raylib.IsMusicStreamPlaying(_currentMusic.Value);
    public bool IsPaused { get; private set; }

    public string? CurrentTrackName
    {
        get
        {
            if (_playingMainTheme) return Path.GetFileNameWithoutExtension(MainThemeFile);
            if (_currentIndex >= 0 && _currentIndex < _shuffleOrder.Count)
                return Path.GetFileNameWithoutExtension(_trackPaths[_shuffleOrder[_currentIndex]]);
            return null;
        }
    }

    public int CurrentTrackIndex => _currentIndex;
    public int TrackCount => _trackPaths.Count;

    /// <summary>
    /// Initialisiert den MusicManager und scannt den Music-Ordner
    /// </summary>
    public void Initialize()
    {
        string basePath = CountryDataLoader.FindBasePath();
        string musicPath = Path.Combine(basePath, "Data", "Music");

        if (!Directory.Exists(musicPath))
        {
            Console.WriteLine("[MusicManager] Music-Ordner nicht gefunden, erstelle ihn...");
            Directory.CreateDirectory(musicPath);
            return;
        }

        // Alle Musikdateien sammeln
        foreach (var ext in SupportedExtensions)
        {
            _trackPaths.AddRange(Directory.GetFiles(musicPath, $"*{ext}"));
        }

        if (_trackPaths.Count == 0)
        {
            Console.WriteLine("[MusicManager] Keine Musikdateien in Data/Music gefunden.");
            return;
        }

        Console.WriteLine($"[MusicManager] {_trackPaths.Count} Tracks gefunden.");

        Shuffle();
        _initialized = true;

        // Direkt im Shuffle-Modus starten (zufaelliger erster Track)
        PlayNext();
    }

    /// <summary>
    /// Erstellt eine neue zufällige Reihenfolge (Fisher-Yates Shuffle)
    /// </summary>
    private void Shuffle()
    {
        _shuffleOrder.Clear();
        for (int i = 0; i < _trackPaths.Count; i++)
            _shuffleOrder.Add(i);

        var rng = Random.Shared;
        for (int i = _shuffleOrder.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_shuffleOrder[i], _shuffleOrder[j]) = (_shuffleOrder[j], _shuffleOrder[i]);
        }

        _currentIndex = -1;
        _trackNamesCacheDirty = true;
    }

    /// <summary>
    /// Laedt und spielt einen Track (synchron, fuer Init/Main Theme)
    /// </summary>
    private bool LoadAndPlay(string path)
    {
        StopCurrent();

        string trackName = Path.GetFileNameWithoutExtension(path);
        try
        {
            var music = Raylib.LoadMusicStream(path);
            music.Looping = false;
            Raylib.SetMusicVolume(music, _volume);
            Raylib.PlayMusicStream(music);
            _currentMusic = music;
            _failCount = 0;
            Console.WriteLine($"[MusicManager] Spiele: {trackName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MusicManager] Fehler beim Laden von {trackName}: {ex.Message}");
            _failCount++;
            return false;
        }
    }

    /// <summary>
    /// Stoppt und gibt den aktuellen Track frei
    /// </summary>
    private void StopCurrent()
    {
        if (_currentMusic.HasValue)
        {
            Raylib.StopMusicStream(_currentMusic.Value);
            Raylib.UnloadMusicStream(_currentMusic.Value);
            _currentMusic = null;
        }
    }

    /// <summary>
    /// Spielt den nächsten Track in der Shuffle-Reihenfolge
    /// </summary>
    private void PlayNext()
    {
        if (_switchingTrack) return;
        if (_trackPaths.Count == 0) return;

        _switchingTrack = true;

        // Wenn ein vorgeladener Track bereit ist, diesen nutzen
        if (_nextMusic.HasValue)
        {
            StopCurrent();
            _currentMusic = _nextMusic;
            Raylib.SetMusicVolume(_currentMusic.Value, _volume);
            Raylib.PlayMusicStream(_currentMusic.Value);
            _failCount = 0;

            // Index aktualisieren (Preload war fuer currentIndex + 1)
            _currentIndex++;
            if (_currentIndex >= _shuffleOrder.Count)
            {
                _currentIndex = 0;
            }

            Console.WriteLine($"[MusicManager] Spiele (preloaded): {_nextTrackName}");
            _nextMusic = null;
            _nextTrackName = null;
            _switchingTrack = false;
            return;
        }

        // Sonst synchron laden
        _currentIndex++;
        if (_currentIndex >= _shuffleOrder.Count)
        {
            Shuffle();
            _currentIndex = 0;
        }

        string path = _trackPaths[_shuffleOrder[_currentIndex]];
        if (!LoadAndPlay(path) && _failCount < MaxConsecutiveFails)
        {
            // Track konnte nicht geladen werden, ueberspringe
            _switchingTrack = false;
            PlayNext();
            return;
        }

        _switchingTrack = false;
    }

    /// <summary>
    /// Startet das Vorladen des naechsten Tracks im Hintergrund.
    /// Liest die Datei in den Speicher, damit LoadMusicStream spaeter schneller ist.
    /// </summary>
    private void PreloadNextTrack()
    {
        if (_nextMusic.HasValue) return;
        if (_preloadTask != null && !_preloadTask.IsCompleted) return;
        if (_trackPaths.Count == 0) return;

        int nextIdx = _currentIndex + 1;
        if (nextIdx >= _shuffleOrder.Count) nextIdx = 0;

        string path = _trackPaths[_shuffleOrder[nextIdx]];

        // Datei im Hintergrund in den OS-Cache laden
        _preloadTask = Task.Run(() =>
        {
            try
            {
                // Lese die Datei in den OS-Dateisystem-Cache
                using var fs = File.OpenRead(path);
                byte[] buffer = new byte[64 * 1024];
                while (fs.Read(buffer, 0, buffer.Length) > 0) { }
            }
            catch { /* Ignorieren - Preload ist optional */ }
        });
    }

    /// <summary>
    /// Muss jeden Frame aufgerufen werden um den Music-Stream zu aktualisieren
    /// und zum nächsten Track zu wechseln wenn der aktuelle fertig ist.
    /// </summary>
    public void Update()
    {
        if (!_initialized) return;

        if (_currentMusic.HasValue)
        {
            Raylib.UpdateMusicStream(_currentMusic.Value);

            // Track-Ende erkennen: IsMusicStreamPlaying wird false wenn nicht-loopender Track endet
            // WICHTIG: Nicht wenn pausiert, sonst springt es zum naechsten Track!
            float played = Raylib.GetMusicTimePlayed(_currentMusic.Value);
            float length = Raylib.GetMusicTimeLength(_currentMusic.Value);

            // Preload starten wenn Track zu ~80% gespielt ist
            if (length > 0 && played >= length * 0.8f && !_playingMainTheme && !IsPaused)
            {
                PreloadNextTrack();
            }

            // Nur zum naechsten Track wechseln wenn NICHT pausiert
            if (!Raylib.IsMusicStreamPlaying(_currentMusic.Value) && !IsPaused && played > 0.5f)
            {
                _playingMainTheme = false;
                PlayNext();
            }
        }
        // Kein else-Branch: Wenn _currentMusic null ist und Fehler auftraten,
        // wird nicht jeden Frame erneut versucht zu laden.
    }

    /// <summary>
    /// Springt zum nächsten Track
    /// </summary>
    public void Skip()
    {
        if (!_initialized) return;
        _playingMainTheme = false;
        _failCount = 0;
        IsPaused = false;
        PlayNext();
    }

    /// <summary>
    /// Springt zum vorherigen Track
    /// </summary>
    public void Previous()
    {
        if (!_initialized) return;
        if (_trackPaths.Count == 0) return;

        _playingMainTheme = false;
        _failCount = 0;
        IsPaused = false;

        // Gehe 2 zurueck (weil PlayNext dann +1 macht)
        _currentIndex -= 2;
        if (_currentIndex < -1) _currentIndex = _shuffleOrder.Count - 2;

        // Verwerfe vorgeladenen Track
        if (_nextMusic.HasValue)
        {
            Raylib.UnloadMusicStream(_nextMusic.Value);
            _nextMusic = null;
            _nextTrackName = null;
        }

        PlayNext();
    }

    /// <summary>
    /// Pausiert die Musik
    /// </summary>
    public void Pause()
    {
        if (!_initialized) return;
        if (_currentMusic.HasValue && !IsPaused)
        {
            Raylib.PauseMusicStream(_currentMusic.Value);
            IsPaused = true;
        }
    }

    /// <summary>
    /// Setzt die Musik fort
    /// </summary>
    public void Resume()
    {
        if (!_initialized) return;
        if (_currentMusic.HasValue && IsPaused)
        {
            Raylib.ResumeMusicStream(_currentMusic.Value);
            IsPaused = false;
        }
    }

    /// <summary>
    /// Wechselt zwischen Pause und Abspielen
    /// </summary>
    public void TogglePause()
    {
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    /// <summary>
    /// Gibt alle verfuegbaren Track-Namen zurueck (gecached)
    /// </summary>
    public List<string> GetAllTrackNames()
    {
        if (_trackNamesCacheDirty)
        {
            _cachedTrackNames.Clear();
            for (int i = 0; i < _shuffleOrder.Count; i++)
            {
                int trackIdx = _shuffleOrder[i];
                // Bounds-Check gegen ungültige Shuffle-Indizes
                if (trackIdx >= 0 && trackIdx < _trackPaths.Count)
                {
                    _cachedTrackNames.Add(Path.GetFileNameWithoutExtension(_trackPaths[trackIdx]));
                }
            }
            _trackNamesCacheDirty = false;
        }
        return _cachedTrackNames;
    }

    /// <summary>
    /// Spielt einen bestimmten Track nach Shuffle-Index
    /// </summary>
    public void PlayTrackByIndex(int index)
    {
        if (!_initialized) return;
        if (index < 0 || index >= _shuffleOrder.Count) return;

        // Zusaetzliche Validierung: Shuffle-Index muss gueltig sein
        int trackIndex = _shuffleOrder[index];
        if (trackIndex < 0 || trackIndex >= _trackPaths.Count) return;

        _playingMainTheme = false;
        _failCount = 0;
        IsPaused = false;

        // Verwerfe vorgeladenen Track
        if (_nextMusic.HasValue)
        {
            Raylib.UnloadMusicStream(_nextMusic.Value);
            _nextMusic = null;
            _nextTrackName = null;
        }

        _currentIndex = index;
        string path = _trackPaths[trackIndex];
        LoadAndPlay(path);
    }

    /// <summary>
    /// Gibt alle Ressourcen frei
    /// </summary>
    public void Unload()
    {
        StopCurrent();

        if (_nextMusic.HasValue)
        {
            Raylib.UnloadMusicStream(_nextMusic.Value);
            _nextMusic = null;
        }

        _preloadTask?.Wait(TimeSpan.FromSeconds(1));
    }
}
