namespace GrandStrategyGame.Systems;

/// <summary>
/// Verwaltet alle Spielsysteme und koordiniert deren Updates.
/// Zentrale Stelle für das Hinzufügen und Entfernen von Systemen.
/// </summary>
public class SystemManager
{
    private readonly List<IGameSystem> _systems = new();
    private readonly Dictionary<TickType, List<IGameSystem>> _tickSubscriptions = new();
    private readonly Dictionary<Type, IGameSystem> _systemsByType = new();
    private GameContext? _context;

    // Zähler für Tick-Typen
    private int _dayCounter = 0;
    private const int DaysPerWeek = 7;
    private const int DaysPerMonth = 30;
    private const int DaysPerYear = 365;

    public SystemManager()
    {
        // Initialisiere Subscription-Listen für alle Tick-Typen
        foreach (TickType tick in Enum.GetValues<TickType>())
        {
            _tickSubscriptions[tick] = new List<IGameSystem>();
        }
    }

    /// <summary>
    /// Registriert ein neues Spielsystem
    /// </summary>
    public void RegisterSystem(IGameSystem system)
    {
        _systems.Add(system);
        _systemsByType[system.GetType()] = system;

        // Sortiere nach Priorität
        _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // Registriere für die entsprechenden Tick-Typen
        foreach (var tick in system.SubscribedTicks)
        {
            _tickSubscriptions[tick].Add(system);
            _tickSubscriptions[tick].Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        Console.WriteLine($"[SystemManager] System registriert: {system.Name} (Priorität: {system.Priority})");
    }

    /// <summary>
    /// Initialisiert alle registrierten Systeme
    /// </summary>
    public void Initialize(GameContext context)
    {
        _context = context;
        foreach (var system in _systems)
        {
            system.Initialize(context);
            Console.WriteLine($"[SystemManager] System initialisiert: {system.Name}");
        }
    }

    /// <summary>
    /// Wird jede Spielstunde aufgerufen - für hochfrequente Updates
    /// </summary>
    public void ProcessHour()
    {
        if (_context == null) return;

        // Stündliche Updates (Bewegungen etc.)
        ProcessTick(TickType.Hourly);
    }

    /// <summary>
    /// Wird jeden Spieltag aufgerufen - verteilt Ticks an die Systeme
    /// </summary>
    public void ProcessDay()
    {
        if (_context == null) return;

        _dayCounter++;

        // Tägliche Updates
        ProcessTick(TickType.Daily);

        // Wöchentliche Updates (alle 7 Tage)
        if (_dayCounter % DaysPerWeek == 0)
        {
            ProcessTick(TickType.Weekly);
        }

        // Monatliche Updates (alle 30 Tage)
        if (_dayCounter % DaysPerMonth == 0)
        {
            ProcessTick(TickType.Monthly);
        }

        // Jährliche Updates (alle 365 Tage)
        if (_dayCounter % DaysPerYear == 0)
        {
            ProcessTick(TickType.Yearly);
        }
    }

    /// <summary>
    /// Führt einen bestimmten Tick-Typ für alle subscribed Systeme aus
    /// </summary>
    private void ProcessTick(TickType tickType)
    {
        if (_context == null) return;

        foreach (var system in _tickSubscriptions[tickType])
        {
            try
            {
                system.OnTick(tickType, _context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SystemManager] Fehler in {system.Name}.OnTick({tickType}): {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Beendet alle Systeme sauber
    /// </summary>
    public void Shutdown()
    {
        foreach (var system in _systems)
        {
            system.Shutdown();
        }
        _systems.Clear();
        foreach (var list in _tickSubscriptions.Values)
        {
            list.Clear();
        }
    }

    /// <summary>
    /// Gibt ein System nach Typ zurück (für Debugging/UI)
    /// </summary>
    public T? GetSystem<T>() where T : class, IGameSystem
    {
        return _systemsByType.TryGetValue(typeof(T), out var system) ? system as T : null;
    }

    /// <summary>
    /// Setzt den Tageszaehler (fuer Save/Load Synchronisation)
    /// </summary>
    public void SetDayCounter(int totalDays)
    {
        _dayCounter = totalDays;
    }

}
