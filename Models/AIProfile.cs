namespace GrandStrategyGame.Models;

/// <summary>
/// AI-Persoenlichkeitsprofil fuer ein Land.
/// Gewichte bestimmen, wie stark der AI-Fokus auf jede Kategorie liegt (0.0-1.0).
/// </summary>
public class AIProfile
{
    public string CountryId { get; set; } = "";

    /// <summary>Wirtschaftsfokus: Fabrikbau, Minenausbau</summary>
    public double EconomyFocus { get; set; } = 0.5;

    /// <summary>Militaerfokus: Rekrutierung, Aufruestung</summary>
    public double MilitaryFocus { get; set; } = 0.3;

    /// <summary>Diplomatiefokus: Beziehungspflege</summary>
    public double DiplomacyFocus { get; set; } = 0.5;

    /// <summary>Expansionsfokus: Kriegsbereitschaft</summary>
    public double ExpansionFocus { get; set; } = 0.1;

    /// <summary>Handelsfokus: Handelsabkommen</summary>
    public double TradeFocus { get; set; } = 0.5;

    /// <summary>Max Anteil des Budgets pro Monat fuer Investitionen (0.0-1.0)</summary>
    public double MaxBudgetSpendRatio { get; set; } = 0.10;

    /// <summary>Ziel-Schuldenquote (Schulden/BIP). Bestimmt wie hoch sich ein Land verschuldet.
    /// z.B. 0.6 = 60% BIP (konservativ), 1.2 = 120% (USA), 2.5 = 250% (Japan)</summary>
    public double MaxDebtRatio { get; set; } = 0.6;
}
