namespace GrandStrategyGame.Systems;

/// <summary>
/// Basisinterface für alle Spielsysteme (Manager).
/// Jedes System kann sich für bestimmte Tick-Typen registrieren.
/// </summary>
public interface IGameSystem
{
    /// <summary>
    /// Name des Systems für Debugging und Logging
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Priorität des Systems (niedrigere Werte = frühere Ausführung)
    /// Wichtig für Abhängigkeiten zwischen Systemen
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Gibt an, welche Tick-Typen dieses System verarbeitet
    /// </summary>
    TickType[] SubscribedTicks { get; }

    /// <summary>
    /// Initialisiert das System beim Spielstart
    /// </summary>
    void Initialize(GameContext context);

    /// <summary>
    /// Wird bei jedem relevanten Tick aufgerufen
    /// </summary>
    void OnTick(TickType tickType, GameContext context);

    /// <summary>
    /// Räumt Ressourcen auf beim Beenden
    /// </summary>
    void Shutdown();
}

/// <summary>
/// Abstrakte Basisklasse für Spielsysteme mit Standardimplementierungen
/// </summary>
public abstract class GameSystemBase : IGameSystem
{
    public abstract string Name { get; }
    public virtual int Priority => 100;
    public abstract TickType[] SubscribedTicks { get; }

    public virtual void Initialize(GameContext context) { }
    public abstract void OnTick(TickType tickType, GameContext context);
    public virtual void Shutdown() { }
}
