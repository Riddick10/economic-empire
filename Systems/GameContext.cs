using GrandStrategyGame.Map;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems;

/// <summary>
/// Zentraler Kontext, der allen Spielsystemen Zugriff auf gemeinsame Daten gibt.
/// Vermeidet direkte Abhängigkeiten zwischen Systemen.
/// </summary>
public class GameContext
{
    /// <summary>
    /// Referenz zum Hauptspiel
    /// </summary>
    public Game Game { get; }

    /// <summary>
    /// Aktuelles Spieldatum und Uhrzeit
    /// </summary>
    public int CurrentHour => Game.CurrentHour;
    public int CurrentMinute => Game.CurrentMinute;
    public int CurrentDay => Game.CurrentDay;
    public int CurrentMonth => Game.CurrentMonth;
    public int CurrentYear => Game.CurrentYear;
    public int TotalDays => Game.TotalDays;
    public int TotalHours => Game.TotalHours;

    /// <summary>
    /// Alle Länder im Spiel
    /// </summary>
    public Dictionary<string, Country> Countries => Game.Countries;

    /// <summary>
    /// Globaler Ressourcenmarkt
    /// </summary>
    public Dictionary<ResourceType, Resource> GlobalMarket => Game.GlobalMarket;

    /// <summary>
    /// Das aktuell vom Spieler kontrollierte Land (kann null sein)
    /// </summary>
    public Country? PlayerCountry => Game.PlayerCountry;

    /// <summary>
    /// Event-Bus für System-übergreifende Kommunikation
    /// </summary>
    public GameEventBus Events { get; }

    /// <summary>
    /// Referenz zur Weltkarte (für Provinz-Zugriff)
    /// </summary>
    public WorldMap? WorldMap { get; set; }

    public GameContext(Game game)
    {
        Game = game;
        Events = new GameEventBus();
    }
}

/// <summary>
/// Einfacher Event-Bus für lose Kopplung zwischen Systemen.
/// Systeme können Events publizieren und abonnieren.
/// </summary>
public class GameEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    /// <summary>
    /// Registriert einen Handler für einen Event-Typ
    /// </summary>
    public void Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Delegate>();
        _handlers[type].Add(handler);
    }

    /// <summary>
    /// Entfernt einen Handler
    /// </summary>
    public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
    {
        var type = typeof(T);
        if (_handlers.ContainsKey(type))
            _handlers[type].Remove(handler);
    }

    /// <summary>
    /// Publiziert ein Event an alle registrierten Handler
    /// </summary>
    public void Publish<T>(T gameEvent) where T : IGameEvent
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var handlers))
        {
            foreach (var handler in handlers)
            {
                ((Action<T>)handler)(gameEvent);
            }
        }
    }
}

/// <summary>
/// Marker-Interface für Spiel-Events
/// </summary>
public interface IGameEvent { }

public record WarDeclaredEvent(string AggressorId, string DefenderId) : IGameEvent;
public record PopulationMigratedEvent(string FromCountryId, string ToCountryId, long Amount) : IGameEvent;
public record UnitRecruitedEvent(MilitaryUnit Unit) : IGameEvent;
public record StarvationEvent(string CountryId, long Deaths) : IGameEvent;
