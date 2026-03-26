namespace GrandStrategyGame.Systems;

/// <summary>
/// Definiert die verschiedenen Update-Frequenzen für Spielsysteme.
/// Systeme können sich für bestimmte Tick-Typen registrieren.
/// </summary>
public enum TickType
{
    /// <summary>
    /// Jede Spielstunde - für sehr häufige Updates (Bewegungen, Animationen)
    /// </summary>
    Hourly,

    /// <summary>
    /// Jeden Spieltag (24 Stunden) - für kritische Berechnungen (Wirtschaft, Bewegungen)
    /// </summary>
    Daily,

    /// <summary>
    /// Alle 7 Spieltage - für häufige Updates (Handel, Produktion)
    /// </summary>
    Weekly,

    /// <summary>
    /// Alle 30 Spieltage - für monatliche Berechnungen (Steuern, Diplomatie-Events)
    /// </summary>
    Monthly,

    /// <summary>
    /// Alle 365 Spieltage - für jährliche Events (Wahlen, große Ereignisse)
    /// </summary>
    Yearly
}
