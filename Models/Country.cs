using GrandStrategyGame.UI;

namespace GrandStrategyGame.Models;

/// <summary>
/// Repräsentiert ein Land im Spiel
/// </summary>
public class Country
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string FullName { get; set; }

    // Bevölkerung
    public long Population { get; set; }
    public double PopulationGrowthRate { get; set; }  // z.B. 0.01 = 1% pro Jahr
    public double UnemploymentRate { get; set; }
    public double EducationLevel { get; set; }        // 0-1

    // Wirtschaft
    public double GDP { get; set; }                   // in Millionen USD
    public double GDPGrowthRate { get; set; }
    public double Budget { get; set; }                // Staatshaushalt
    public double NationalDebt { get; set; }
    public double TaxRate { get; set; }               // 0-1
    public double Inflation { get; set; }

    // Budget-Ausgaben (Prozentsätze vom Gesamtbudget, sollten ~1.0 ergeben)
    public double SocialSpendingPercent { get; set; }        // Soziales (Renten, Sozialhilfe)
    public double MilitarySpendingPercent { get; set; }      // Militär
    public double InfrastructureSpendingPercent { get; set; } // Infrastruktur
    public double EducationSpendingPercent { get; set; }     // Bildung
    public double HealthSpendingPercent { get; set; }        // Gesundheit
    public double AdministrationSpendingPercent { get; set; } // Verwaltung

    /// <summary>
    /// Defizit-Multiplikator: Ausgaben = Einnahmen * DeficitMultiplier.
    /// 1.0 = ausgeglichener Haushalt, 1.08 = 8% Defizit (typisch),
    /// 1.15 = 15% Defizit (expansiv). Defizit wird als Neuverschuldung verbucht.
    /// </summary>
    public double DeficitMultiplier { get; set; } = 1.06;

    // Handel (wird vom TradeManager aktualisiert)
    public double DailyExports { get; set; }
    public double DailyImports { get; set; }
    public double TradeBalance => DailyExports - DailyImports;

    // Ressourcen-Lager
    public Dictionary<ResourceType, double> Stockpile { get; set; }

    // Tägliche Produktion/Verbrauch
    public Dictionary<ResourceType, double> DailyProduction { get; set; }
    public Dictionary<ResourceType, double> DailyConsumption { get; set; }

    // Militaerisches Equipment (produziert von Militaerfabriken, verbraucht bei Rekrutierung)
    public Dictionary<string, int> MilitaryEquipment { get; set; }

    public Country(string id, string name)
    {
        Id = id;
        Name = name;
        FullName = name;
        Stockpile = new Dictionary<ResourceType, double>();
        DailyProduction = new Dictionary<ResourceType, double>();
        DailyConsumption = new Dictionary<ResourceType, double>();
        MilitaryEquipment = new Dictionary<string, int>();

        // Standardwerte
        TaxRate = 0.25;
        Inflation = 0.02;
        EducationLevel = 0.5;

        // Standard-Budgetaufteilung (typisch für entwickelte Länder)
        SocialSpendingPercent = 0.35;
        MilitarySpendingPercent = 0.15;
        InfrastructureSpendingPercent = 0.12;
        EducationSpendingPercent = 0.10;
        HealthSpendingPercent = 0.18;
        AdministrationSpendingPercent = 0.10;
    }

    /// <summary>
    /// BIP pro Kopf berechnen
    /// </summary>
    public double GetGDPPerCapita()
    {
        if (Population <= 0) return 0;
        return (GDP * 1_000_000) / Population;
    }

    /// <summary>
    /// Berechnet den Zinssatz basierend auf der Schuldenquote (Schulden/BIP).
    /// Höhere Verschuldung = höheres Risiko = höhere Zinsen (wie reale Anleihemärkte).
    /// </summary>
    public double GetInterestRate()
    {
        if (GDP <= 0) return 0.035;
        double debtRatio = NationalDebt / GDP;

        return debtRatio switch
        {
            < 0.30 => 0.0075, // 0.75% — sehr sicher (Norwegen, Schweiz)
            < 0.60 => 0.0125, // 1.25% — sicher (Australien, Skandinavien)
            < 0.90 => 0.0175, // 1.75% — moderat (Deutschland, Frankreich)
            < 1.20 => 0.025,  // 2.5%  — erhöht (USA, Spanien)
            < 2.00 => 0.035,  // 3.5%  — hoch (Italien, Griechenland)
            _      => 0.045   // 4.5%  — kritisch (Japan-Niveau)
        };
    }

    /// <summary>
    /// Fügt Ressourcen zum Lager hinzu
    /// </summary>
    public void AddResource(ResourceType type, double amount)
    {
        // Optimiert: CollectionsMarshal fuer direkten Zugriff ohne doppelten Lookup
        ref double current = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(Stockpile, type, out _);
        current += amount;
    }

    /// <summary>
    /// Entnimmt Ressourcen aus dem Lager
    /// </summary>
    public bool UseResource(ResourceType type, double amount)
    {
        // Optimiert: Ein Lookup statt zwei
        if (!Stockpile.TryGetValue(type, out double current) || current < amount)
            return false;
        Stockpile[type] = current - amount;
        return true;
    }

    /// <summary>
    /// Gibt die verfügbare Menge einer Ressource zurück
    /// </summary>
    public double GetResource(ResourceType type)
    {
        return Stockpile.TryGetValue(type, out double amount) ? amount : 0;
    }

    /// <summary>
    /// Berechnet taegliche Staatseinnahmen (Steuern)
    /// </summary>
    public double CalculateDailyRevenue()
    {
        return (GDP * TaxRate) / 365.0;
    }

    /// <summary>
    /// Berechnet taegliche Staatsausgaben basierend auf konfigurierbaren Prozentsätzen.
    /// Die Ausgaben werden mit DeficitMultiplier skaliert (>1.0 = Defizithaushalt).
    /// </summary>
    public double CalculateDailyExpenses()
    {
        // Basis: Einnahmen (Steuereinnahmen)
        double yearlyRevenue = GDP * TaxRate;

        double yearly = 0;

        // Ausgaben basierend auf Prozentsätzen der Einnahmen, skaliert mit Defizit-Multiplikator
        yearly += yearlyRevenue * SocialSpendingPercent;
        yearly += yearlyRevenue * MilitarySpendingPercent;
        yearly += yearlyRevenue * InfrastructureSpendingPercent;
        yearly += yearlyRevenue * EducationSpendingPercent;
        yearly += yearlyRevenue * HealthSpendingPercent;
        yearly += yearlyRevenue * AdministrationSpendingPercent;

        // Defizit-Multiplikator anwenden (z.B. 1.08 = 8% mehr Ausgaben als Einnahmen)
        yearly *= DeficitMultiplier;

        // Schuldenzinsen: variabler Satz basierend auf Schuldenquote
        yearly += NationalDebt * GetInterestRate();

        return yearly / 365.0;
    }

    /// <summary>
    /// Gibt die detaillierten monatlichen Ausgaben zurück
    /// </summary>
    public Dictionary<string, double> GetMonthlyExpenseBreakdown()
    {
        double monthlyRevenue = (GDP * TaxRate) / 12.0;
        double dm = DeficitMultiplier;
        return new Dictionary<string, double>
        {
            { "Soziales", monthlyRevenue * SocialSpendingPercent * dm },
            { "Militär", monthlyRevenue * MilitarySpendingPercent * dm },
            { "Infrastruktur", monthlyRevenue * InfrastructureSpendingPercent * dm },
            { "Bildung", monthlyRevenue * EducationSpendingPercent * dm },
            { "Gesundheit", monthlyRevenue * HealthSpendingPercent * dm },
            { "Verwaltung", monthlyRevenue * AdministrationSpendingPercent * dm }
        };
    }

    /// <summary>
    /// Netto-Budgetaenderung pro Tag (Einnahmen - Ausgaben)
    /// </summary>
    public double CalculateDailyBudgetChange()
    {
        return CalculateDailyRevenue() - CalculateDailyExpenses();
    }

    public override string ToString()
    {
        return $"{Name} | Bevoelkerung: {Formatting.Population(Population)} | BIP: ${Formatting.Number(GDP)}M";
    }
}
