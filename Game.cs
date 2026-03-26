using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame;

/// <summary>
/// Hauptspielklasse - verwaltet den Spielzustand
/// </summary>
public class Game
{
    public Dictionary<string, Country> Countries { get; private set; }
    public Dictionary<ResourceType, Resource> Resources { get; private set; }
    public Country? PlayerCountry { get; private set; }

    // Alias für Kompatibilität mit GameContext
    public Dictionary<ResourceType, Resource> GlobalMarket => Resources;

    // Zeit
    public int Year { get; private set; }
    public int Month { get; private set; }
    public int Day { get; private set; }
    public int Hour { get; private set; }
    public int Minute { get; private set; }
    public int TotalDays { get; private set; }
    public int TotalHours { get; private set; }

    // Alias für GameContext
    public int CurrentDay => Day;
    public int CurrentMonth => Month;
    public int CurrentYear => Year;
    public int CurrentHour => Hour;
    public int CurrentMinute => Minute;

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; set; }

    // Tick-System fuer Performance
    // Geschwindigkeit 1-5 (0 = Pause wird separat behandelt)
    public int GameSpeed { get; set; } = 1;
    private double _tickAccumulator = 0;

    // Stunden pro Sekunde je Geschwindigkeit (aus balance-config.json)
    private static double[] HoursPerSecond => BalanceConfig.GameSpeeds;

    // Manager-System
    private readonly SystemManager _systemManager;
    private GameContext? _gameContext;

    public Game()
    {
        Countries = new Dictionary<string, Country>();
        Resources = new Dictionary<ResourceType, Resource>();
        _systemManager = new SystemManager();
        Year = BalanceConfig.StartYear;
        Month = 1;
        Day = 1;
        Hour = 8;  // Start um 8:00 Uhr morgens
        Minute = 0;
        TotalDays = 0;
        TotalHours = 0;
        IsPaused = true;
    }

    /// <summary>
    /// Initialisiert das Spiel mit Standarddaten
    /// </summary>
    public void Initialize()
    {
        InitializeResources();
        InitializeCountries();
        InitializeSystems();
        IsRunning = true;
    }

    /// <summary>
    /// Initialisiert alle Spielsysteme (Manager)
    /// </summary>
    private void InitializeSystems()
    {
        // GameContext erstellen
        _gameContext = new GameContext(this);

        // Alle Manager registrieren
        _systemManager.RegisterSystem(new EconomyManager());
        _systemManager.RegisterSystem(new PopulationManager());
        _systemManager.RegisterSystem(new ProductionManager());
        _systemManager.RegisterSystem(new TradeManager());
        _systemManager.RegisterSystem(new DiplomacyManager());
        _systemManager.RegisterSystem(new PoliticsManager());
_systemManager.RegisterSystem(new MilitaryManager());
        _systemManager.RegisterSystem(new NotificationManager());
        _systemManager.RegisterSystem(new TechTreeManager());
        _systemManager.RegisterSystem(new ConflictManager());
        _systemManager.RegisterSystem(new AIManager());

        // Systeme initialisieren
        _systemManager.Initialize(_gameContext);
    }

    private void InitializeResources()
    {
        // Ressourcen aus balance-config.json laden
        foreach (var (key, info) in BalanceConfig.Resources)
        {
            if (Enum.TryParse<ResourceType>(key, out var type))
                AddResource(type, info.Name, info.BasePrice);
        }
    }

    private void AddResource(ResourceType type, string name, double basePrice)
    {
        Resources[type] = new Resource(type, name, basePrice);
    }

    private void InitializeCountries()
    {
        // Lade Daten aus JSON-Dateien
        string basePath = CountryDataLoader.FindBasePath();
        Countries = CountryDataLoader.LoadCountries(basePath);
        CountryDataLoader.LoadProduction(Countries, basePath);
        CountryDataLoader.InitializeStockpiles(Countries);
    }

    /// <summary>
    /// Wählt das Spielerland aus
    /// </summary>
    public bool SelectPlayerCountry(string countryId)
    {
        if (Countries.TryGetValue(countryId, out var country))
        {
            PlayerCountry = country;
            IsPaused = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Update mit Delta-Zeit - simuliert Stunden basierend auf Geschwindigkeit
    /// </summary>
    public void Update(double deltaTime)
    {
        if (IsPaused || GameSpeed <= 0 || GameSpeed >= HoursPerSecond.Length) return;

        // Stunden akkumulieren basierend auf Geschwindigkeit
        _tickAccumulator += deltaTime * HoursPerSecond[GameSpeed];

        // Minuten aus dem verbleibenden Bruchteil einer Stunde berechnen
        double fractionalHour = _tickAccumulator % 1.0;
        Minute = (int)(fractionalHour * 60);

        // Eine Stunde pro Tick simulieren
        while (_tickAccumulator >= 1.0)
        {
            _tickAccumulator -= 1.0;
            SimulateHourInternal();
        }
    }

    /// <summary>
    /// Interne Stunden-Simulation
    /// </summary>
    private void SimulateHourInternal()
    {
        TotalHours++;

        // Stündliche Updates (Bewegungen etc.)
        _systemManager.ProcessHour();

        // Stunde voranschreiten
        Hour++;
        if (Hour >= 24)
        {
            Hour = 0;
            SimulateDayInternal();
        }
    }

    /// <summary>
    /// Interne Tages-Simulation (wird aufgerufen wenn 24 Stunden vergangen sind)
    /// </summary>
    private void SimulateDayInternal()
    {
        TotalDays++;

        // Tägliche Produktions-/Verbrauchswerte zuruecksetzen BEVOR Systeme laufen,
        // damit alle Manager (PopulationManager, ProductionManager, TradeManager etc.)
        // sauber in leere Dicts schreiben koennen
        foreach (var country in Countries.Values)
        {
            country.DailyProduction.Clear();
            country.DailyConsumption.Clear();
        }

        // Manager-Systeme aktualisieren (tägliche Updates inkl. Budget, Produktion, etc.)
        _systemManager.ProcessDay();

        // Datum voranschreiten
        AdvanceDate();
    }

    private void AdvanceDate()
    {
        Day++;

        int daysInMonth = GetDaysInMonth(Month, Year);

        if (Day > daysInMonth)
        {
            Day = 1;
            Month++;

            if (Month > 12)
            {
                Month = 1;
                Year++;
            }
        }
    }

    private int GetDaysInMonth(int month, int year)
    {
        return month switch
        {
            2 => IsLeapYear(year) ? 29 : 28,
            4 or 6 or 9 or 11 => 30,
            _ => 31
        };
    }

    private bool IsLeapYear(int year)
    {
        return (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
    }

    /// <summary>
    /// Gibt das aktuelle Datum als String zurück
    /// </summary>
    public string GetDateString()
    {
        string monthName = Month switch
        {
            1 => "Januar", 2 => "Februar", 3 => "März",
            4 => "April", 5 => "Mai", 6 => "Juni",
            7 => "Juli", 8 => "August", 9 => "September",
            10 => "Oktober", 11 => "November", 12 => "Dezember",
            _ => "?"
        };
        return $"{Day}. {monthName} {Year}";
    }

    /// <summary>
    /// Gibt die aktuelle Uhrzeit als String zurück (HH:MM)
    /// </summary>
    public string GetTimeString()
    {
        return $"{Hour:D2}:{Minute:D2}";
    }

    /// <summary>
    /// Stellt den Spielzustand aus einem Spielstand wieder her
    /// </summary>
    public void LoadState(int year, int month, int day, int hour, int minute, int totalDays, int totalHours, int gameSpeed)
    {
        Year = year;
        Month = month;
        Day = day;
        Hour = hour;
        Minute = minute;
        TotalDays = totalDays;
        TotalHours = totalHours;
        GameSpeed = gameSpeed;
        _tickAccumulator = 0;

        // SystemManager-Tageszaehler synchronisieren
        _systemManager.SetDayCounter(totalDays);
    }

    /// <summary>
    /// Gibt ein Spielsystem nach Typ zurück
    /// </summary>
    public T? GetSystem<T>() where T : class, IGameSystem
    {
        return _systemManager.GetSystem<T>();
    }

    /// <summary>
    /// Gibt den GameContext zurück (für Zugriff auf Event-Bus etc.)
    /// </summary>
    public GameContext? GetGameContext() => _gameContext;

    /// <summary>
    /// GameContext als Property (Alias für GetGameContext)
    /// </summary>
    public GameContext? GameContext => _gameContext;
}
