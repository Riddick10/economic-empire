using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.UI;
using GrandStrategyGame.UI.Panels;
using GrandStrategyGame.UI.Screens;

namespace GrandStrategyGame;

/// <summary>
/// Hauptprogramm - Economic Empire
/// Aufgeteilt in Partial Classes:
/// - Program.cs (Kern - Main, Fields, Enums)
/// - Program.Loading.cs (Asynchrones Laden)
/// - Program.Update.cs (Update-Logik)
/// - Program.Draw.cs (Haupt-Zeichenmethoden)
/// - Program.DrawPlaying.cs (Spielbildschirm)
/// - Program.Panels.cs (UI-Panels)
/// - Program.Resources.cs (Textur-Loading)
/// </summary>
partial class Program
{
    // Spiel (Kern-Singletons)
    internal static Game game = new Game();
    internal static WorldMap worldMap = new WorldMap();
    internal static GameScreen currentScreen = GameScreen.Loading;
    internal static MusicManager musicManager = new MusicManager();
    internal static bool shouldQuit = false;

    // Quickstart-Modus: Ueberspringt Hauptmenue und startet direkt mit gewaehltem Land
    // Starten mit: dotnet run -- --quickstart RUS
    internal static string? QuickstartCountryId = null;

    // UI-State (gruppiert alle UI-bezogenen Zustaende)
    internal static readonly UIState ui = new();

    // Panel-Instanzen (eigenstaendige UI-Klassen)
    internal static readonly TechTreePanel _techTreePanel = new();
internal static readonly PoliticsInfoPanel _politicsInfoPanel = new();
    internal static readonly ProvinceInfoPanel _provinceInfoPanel = new();

    // Karten-Interaktion -> siehe ui (UIState)

    // Lade-Status (thread-sicher, gekapselt in LoadingState)
    internal static readonly LoadingState _loading = new();

    // Custom Cursor
    static Texture2D? _cursorTexture = null;

    // Textur-Caches
    static Texture2D? _mapViewIcon = null;
    static Texture2D? _resourceViewIcon = null;
    static Texture2D? _tradeViewIcon = null;
    static Texture2D? _allianceViewIcon = null;
    static Texture2D? _newsIcon = null;
    static Texture2D? _tutorialIcon = null;
    static Dictionary<string, Texture2D> _tutorialIcons = new();
    static Dictionary<string, Texture2D> _flagTextures = new();
    static Dictionary<string, Texture2D> _leaderTextures = new();
    static Dictionary<ResourceType, Texture2D> _resourceIcons = new();
    static Dictionary<TopMenuPanel, Texture2D> _topMenuIcons = new();
    static Dictionary<string, Texture2D> _allianceIcons = new();
    static Dictionary<MineType, Texture2D> _mineIcons = new();
    static Dictionary<string, Texture2D> _newsImageCache = new();
    static Texture2D? _blastIcon = null;
    internal static Texture2D? _loadingScreenTexture = null;

    // Performance-Caches -> siehe PerformanceCache.cs
    internal static readonly PerformanceCache _cache = new();
    internal static Vector2 _cachedMousePos;

    // Gecachte System-Referenzen -> siehe ManagerRefs.cs
    internal static readonly ManagerRefs _mgr = new();

    // Statische Resource-Arrays -> siehe ResourceConfig.cs

    // Logistik-Panel dynamische Inhaltshoehe
    internal static int _logisticsPanelContentHeight = 500;

    // Screen-System (IGameScreen-basiert)
    internal static readonly Dictionary<GameScreen, IGameScreen> _screens = new()
    {
        { GameScreen.Loading, new LoadingScreen() },
        { GameScreen.MainMenu, new MainMenuScreen() },
        { GameScreen.CountrySelect, new CountrySelectScreen() },
        { GameScreen.Playing, new PlayingScreen() },
        { GameScreen.LoadGame, new LoadGameScreen() },
        { GameScreen.SaveGame, new SaveGameScreen() },
    };

    // Aktuelle Fenstergroesse (dynamisch)
    internal static int ScreenWidth => Raylib.GetScreenWidth();
    internal static int ScreenHeight => Raylib.GetScreenHeight();

    static void Main(string[] args)
    {
        // Quickstart-Argument pruefen: --quickstart RUS
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--quickstart")
            {
                QuickstartCountryId = args[i + 1].ToUpperInvariant();
                Console.WriteLine($"[Quickstart] Starte direkt mit Land: {QuickstartCountryId}");
                break;
            }
        }

        // Fenster mit Standardgroesse erstellen
        Raylib.InitWindow(GameConfig.WINDOW_WIDTH, GameConfig.WINDOW_HEIGHT, "Economic Empire");

        // ESC soll das Fenster NICHT schliessen (Raylib Standard deaktivieren)
        Raylib.SetExitKey(KeyboardKey.Null);

        // Fenster-Icon setzen (32x32 RGBA fuer Taskleiste)
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Icons", "task_icon_32.png");
        if (File.Exists(iconPath))
        {
            Image icon = Raylib.LoadImage(iconPath);
            Raylib.SetWindowIcon(icon);
            Raylib.UnloadImage(icon);
        }

        // Custom Cursor laden
        string cursorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Icons", "cursor.png");
        if (File.Exists(cursorPath))
        {
            _cursorTexture = Raylib.LoadTexture(cursorPath);
            Raylib.HideCursor();
        }

        // Fenster maximierbar und in der Groesse aenderbar machen
        Raylib.SetWindowState(ConfigFlags.ResizableWindow);
        Raylib.SetWindowMinSize(GameConfig.WINDOW_MIN_WIDTH, GameConfig.WINDOW_MIN_HEIGHT);
        // FPS unbegrenzt (Spiel nutzt Delta-Zeit)

        // Audio initialisieren
        Raylib.InitAudioDevice();

        // Ladebildschirm-Hintergrund laden (vor allem anderen, damit sofort sichtbar)
        string loadingScreenPath = Path.Combine("Data", "Images", "Loadscreen_1.png");
        if (File.Exists(loadingScreenPath))
        {
            _loadingScreenTexture = Raylib.LoadTexture(loadingScreenPath);
        }

        // Custom Font laden (mit Umlaut-Unterstützung)
        LoadGameFont();

        while (!Raylib.WindowShouldClose() && !shouldQuit)
        {
            // Mausposition einmal pro Frame cachen (Performance)
            _cachedMousePos = Raylib.GetMousePosition();

            // System-Referenzen einmal pro Frame cachen
            if (currentScreen == GameScreen.Playing)
            {
                _mgr.Update(game);
            }

            musicManager.Update();
            Update();
            Draw();
        }

        // Warte auf Lade-Task falls noch aktiv
        if (_loading.LoadingTask != null && !_loading.LoadingTask.IsCompleted)
        {
            _loading.LoadingTask.Wait(TimeSpan.FromSeconds(2));
        }

        // Ressourcen freigeben
        worldMap.Unload();

        // Alle Texturen ueber TextureManager freigeben
        TextureManager.UnloadAll();

        // Legacy-Caches freigeben (werden nach und nach zu TextureManager migriert)
        UnloadTextureCache(_flagTextures);
        UnloadTextureCache(_leaderTextures);
        UnloadTextureCache(_resourceIcons);
        UnloadTextureCache(_topMenuIcons);
        UnloadTextureCache(_allianceIcons);
        UnloadTextureCache(_mineIcons);
        UnloadTextureCache(_newsImageCache);

        // Einzelne Texturen freigeben
        if (_mapViewIcon != null) Raylib.UnloadTexture(_mapViewIcon.Value);
        if (_resourceViewIcon != null) Raylib.UnloadTexture(_resourceViewIcon.Value);
        if (_tradeViewIcon != null) Raylib.UnloadTexture(_tradeViewIcon.Value);
        if (_allianceViewIcon != null) Raylib.UnloadTexture(_allianceViewIcon.Value);
        if (_newsIcon != null) Raylib.UnloadTexture(_newsIcon.Value);
        if (_tutorialIcon != null) Raylib.UnloadTexture(_tutorialIcon.Value);
        if (_loadingScreenTexture != null) Raylib.UnloadTexture(_loadingScreenTexture.Value);
        if (_blastIcon != null) Raylib.UnloadTexture(_blastIcon.Value);
        if (_cursorTexture != null) Raylib.UnloadTexture(_cursorTexture.Value);
        UnloadTextureCache(_tutorialIcons);

        // Sounds und Musik freigeben
        SoundManager.Unload();
        musicManager.Unload();
        Raylib.CloseAudioDevice();

        // Font freigeben
        UnloadGameFont();

        Raylib.CloseWindow();
    }

    /// <summary>
    /// Hilfsmethode zum Freigeben eines Textur-Caches
    /// </summary>
    static void UnloadTextureCache<TKey>(Dictionary<TKey, Texture2D> cache) where TKey : notnull
    {
        foreach (var texture in cache.Values)
        {
            Raylib.UnloadTexture(texture);
        }
        cache.Clear();
    }
}

/// <summary>
/// Gruppiert alle UI-bezogenen Zustaende (Panels, Scroll, Auswahl, Modi)
/// </summary>
class UIState
{
    // Karten-Interaktion
    public bool IsDragging;
    public Vector2 LastMousePos;
    public Vector2 LastHoverCheckPos = new(-99999, -99999);
    public bool ViewportChanged;

    // Karten-Auswahl
    public string? HoveredCountryId;
    public string? SelectedCountryId;
    public string? HoveredProvinceId;
    public string? SelectedProvinceId;

    // Panel-Sichtbarkeit
    public bool ShowPoliticsPanel;
    public bool ShowProvincePanel;
    public bool ShowBuildPanel;
    public bool ShowTutorialPanel;
    public bool ShowPauseMenu;
    public bool ShowOptionsMenu;
    public bool ShowSavePanel;
    public bool ShowTechTree;
    public bool ShowMusicTrackList;
    public bool ShowTradeCreatePanel;
    public bool ShowEconomyRanking;
    public int EconomyRankingTab; // 0 = BIP-Rangliste, 1 = Globale Wirtschaft & Ressourcen
    public HashSet<ResourceType> ResourceChartFilter = new(); // Aktive Ressourcen im Chart (anfangs leer)
    public bool ShowFPS = true;
    public bool ShowMainMenuOptions;

    // Scroll-Offsets
    public int NotificationScrollOffset;
    public int TutorialScrollOffset;
    public int TradeScrollOffset;
    public int TechTreeScrollX;
    public int TechTreeScrollY;
    public int NewsScrollOffset;
    public int ProductionScrollOffset;
    public int LogisticsScrollOffset;
    public int MusicTrackListScroll;
    public int EconomyRankingScrollOffset;

    // Handel-UI
    public bool TradeIsExport = true;
    public ResourceType? TradeSelectedResource;
    public string? TradeSelectedCountry;
    public int TradeAmount = 10;

    // Handel-Partner Cache
    public List<(string CountryId, double Value)> TradePartnersCache = new();
    public List<(string CountryId, double Value)> TradePartnersEU = new();
    public List<(string CountryId, double Value)> TradePartnersOther = new();
    public ResourceType? TradePartnersCacheResource;
    public bool TradePartnersCacheIsExport = true;
    public double TradePartnersCacheTime;

    // Ressourcen-Heatmap Filter (null = Alle anzeigen)
    public ResourceType? HeatmapResource = null;

    // Handelsrouten-Filter (null = Alle Ressourcen anzeigen)
    public ResourceType? TradeRouteFilter = null;

    // Auswahl & Modi
    public TopMenuPanel ActiveTopMenuPanel = TopMenuPanel.None;
    public MapViewMode CurrentMapView = MapViewMode.Political;
    public AllianceViewType CurrentAllianceView = AllianceViewType.Military;
    public FactoryType? FactoryBuildMode;
    public MineType? MineBuildMode;
    public UnitType? RecruitmentMode;
    public MilitaryUnit? SelectedUnit;
    public string? PoliticsPanelCountryId;
    public string? HoveredTechId;
    public string? SelectedTechId;

    // Entscheidungen / Werbung
    public string? SelectedAdvertisingParty;
    public int DecisionScrollOffset;

    // Optionen
    public float OptionsMusicVolume = 0f;
    public float OptionsSoundVolume = 0.5f;
    public bool IsDraggingMusicSlider;
    public bool IsDraggingSoundSlider;
    public bool MainMenuDayNightCycleEnabled = true;

    // Sonstiges
    public int LastNotificationId = -1;

    // Hauptmenue-Buttons
    public Rectangle NewGameButtonRect = new(0, 0, 250, 50);
    public Rectangle LoadGameButtonRect = new(0, 0, 250, 50);
    public Rectangle OptionsButtonRect = new(0, 0, 250, 50);
    public Rectangle QuitButtonRect = new(0, 0, 250, 50);
    public bool NewGameButtonHovered;
    public bool LoadGameButtonHovered;
    public bool OptionsButtonHovered;
    public bool QuitButtonHovered;

    // Save/Load Screen
    public int SelectedSaveSlot = -1;
    public bool[] SaveSlotHovered = new bool[3];
    public SaveSlotInfo?[] SaveSlots = new SaveSlotInfo?[3];
    public Rectangle BackButtonRect = new(0, 0, 150, 40);
    public bool BackButtonHovered;
    public Rectangle[] SaveSlotRects = new Rectangle[3];
    public Rectangle[] DeleteSlotRects = new Rectangle[3];
    public bool[] DeleteSlotHovered = new bool[3];
    public Rectangle ConfirmSaveButtonRect = new(0, 0, 200, 45);
    public bool ConfirmSaveButtonHovered;

    // Laenderauswahl
    public Rectangle SelectCountryButtonRect;
    public bool SelectCountryButtonHovered;

    /// <summary>
    /// Setzt alle Playing-bezogenen UI-Flags zurueck (bei Rueckkehr zum Hauptmenue)
    /// </summary>
    public void ResetPlayingState()
    {
        // Panels schliessen
        ShowPoliticsPanel = false;
        ShowProvincePanel = false;
        ShowBuildPanel = false;
        ShowTutorialPanel = false;
        ShowPauseMenu = false;
        ShowOptionsMenu = false;
        ShowSavePanel = false;
        ShowTechTree = false;
        ShowMusicTrackList = false;
        ShowTradeCreatePanel = false;
        ShowEconomyRanking = false;
        EconomyRankingTab = 0;

        // Auswahl zuruecksetzen
        SelectedCountryId = null;
        SelectedProvinceId = null;
        HoveredCountryId = null;
        HoveredProvinceId = null;
        PoliticsPanelCountryId = null;
        SelectedUnit = null;
        HoveredTechId = null;
        SelectedTechId = null;
        SelectedAdvertisingParty = null;

        // Modi zuruecksetzen
        FactoryBuildMode = null;
        MineBuildMode = null;
        RecruitmentMode = null;
        ActiveTopMenuPanel = TopMenuPanel.None;

        // Scroll-Offsets zuruecksetzen
        NotificationScrollOffset = 0;
        TutorialScrollOffset = 0;
        TradeScrollOffset = 0;
        TechTreeScrollX = 0;
        TechTreeScrollY = 0;
        NewsScrollOffset = 0;
        ProductionScrollOffset = 0;
        LogisticsScrollOffset = 0;
        EconomyRankingScrollOffset = 0;
        DecisionScrollOffset = 0;

        // Slider-Dragging beenden
        IsDraggingMusicSlider = false;
        IsDraggingSoundSlider = false;
    }
}

enum GameScreen
{
    Loading,
    MainMenu,
    LoadGame,       // Spielstand laden (aus Hauptmenü)
    CountrySelect,
    Playing,
    SaveGame        // Spielstand speichern (im Spiel)
}

public enum MapViewMode
{
    Political,  // Normale Kartenansicht
    Resources,  // Ressourcen-Ansicht
    Trade,      // Handelsrouten-Ansicht
    Alliance    // Buendnis-Ansicht (NATO, BRICS, etc.)
}

/// <summary>
/// Buendnis-Ansicht-Untertyp (Militaer vs Wirtschaft)
/// </summary>
public enum AllianceViewType
{
    Military,   // Militaerbuendnisse (NATO, CSTO, SCO)
    Economic    // Wirtschaftsbuendnisse (EU, ASEAN, BRICS, AU, OPEC)
}

/// <summary>
/// Top-Menu Panel-Typen (HOI4-Stil Buttons oben links)
/// </summary>
public enum TopMenuPanel
{
    None,       // Kein Panel offen
    Politics,   // Politik - Regierung, Gesetze, Entscheidungen
    Trade,      // Handel - Import/Export, Handelsabkommen
    Production, // Produktion - Fabriken, Produktionsketten
    Research,   // Forschung - Technologie-Baum
    Diplomacy,  // Diplomatie - Beziehungen, Buendnisse
    Budget,     // Budget - Staatshaushalt, Steuern
    Military,   // Militaer - Streitkraefte, Kriege
    News,       // Nachrichten - Benachrichtigungen, Ereignisse
    Logistics   // Logistik - Ressourcenuebersicht, Lieferketten
}
