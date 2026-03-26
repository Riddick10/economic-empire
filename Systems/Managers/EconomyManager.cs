using GrandStrategyGame.Data;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet die Wirtschaftssimulation.
/// - BIP-Berechnung
/// - Inflation
/// - Staatshaushalt
/// - Steuern
/// </summary>
public class EconomyManager : GameSystemBase
{
    public override string Name => "Economy";
    public override int Priority => 10; // Früh ausführen, da andere Systeme davon abhängen
    public override TickType[] SubscribedTicks => new[] { TickType.Daily, TickType.Monthly, TickType.Yearly };

    // Wirtschaftsdaten pro Land (für Performance gecacht)
    private readonly Dictionary<string, CountryEconomyData> _economyData = new();

    // Historische Ressourcen-Daten fuer Liniendiagramm
    private readonly List<MoneySnapshot> _moneyHistory = new();
    private int _lastSnapshotDay = -1;

    /// <summary>
    /// Historische Ressourcen-Datenpunkte (fuer UI-Diagramm)
    /// </summary>
    public IReadOnlyList<MoneySnapshot> MoneyHistory => _moneyHistory;

    public override void Initialize(GameContext context)
    {
        // Initialisiere Wirtschaftsdaten für alle Länder
        foreach (var (countryId, country) in context.Countries)
        {
            _economyData[countryId] = new CountryEconomyData
            {
                Inflation = BalanceConfig.Economy.BaseInflation,
                TaxRate = BalanceConfig.Economy.DefaultTaxRate,
                Budget = 0
            };
        }
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        switch (tickType)
        {
            case TickType.Daily:
                // Tägliches Budget-Update fuer alle Laender
                foreach (var country in context.Countries.Values)
                {
                    double budgetChange = country.CalculateDailyBudgetChange();
                    country.Budget += budgetChange;

                    // Defizit als Neuverschuldung verbuchen (wie in der Realitaet)
                    if (budgetChange < 0)
                    {
                        country.NationalDebt += Math.Abs(budgetChange);
                    }
                }
                // Ressourcen-Snapshot alle 7 Tage erfassen
                RecordMoneySnapshot(context);
                break;

            case TickType.Monthly:
                CalculateMonthlyEconomy(context);
                break;

            case TickType.Yearly:
                CalculateYearlyEconomy(context);
                break;
        }
    }

    private void CalculateMonthlyEconomy(GameContext context)
    {
        foreach (var (countryId, data) in _economyData)
        {
            if (!context.Countries.TryGetValue(countryId, out var country))
                continue;

            // BIP-proportionale Geldschoepfung: 0.25% des BIP pro Monat
            // Simuliert Zentralbank-Liquiditaet / Wirtschaftskreislauf
            double liquidityInjection = country.GDP * 0.0025;
            country.Budget += liquidityInjection;

            // Gecachte Daten synchronisieren
            data.Budget = country.Budget;
        }
    }

    private void CalculateYearlyEconomy(GameContext context)
    {
        foreach (var (countryId, data) in _economyData)
        {
            if (!context.Countries.TryGetValue(countryId, out var country))
                continue;

            // Jaehrliches Wirtschaftswachstum basierend auf verschiedenen Faktoren
            double growthRate = CalculateGrowthRate(country, data);
            double newGDP = country.GDP * (1 + growthRate);

            // GDP im Country-Objekt aktualisieren (Country ist Single Source of Truth)
            country.GDP = newGDP;
            country.GDPGrowthRate = growthRate;

            // Inflationsanpassung
            var eco = BalanceConfig.Economy;
            double inflationChange = (growthRate - eco.BaseGrowthRate) * eco.InflationChangeMultiplier;
            data.Inflation = Math.Clamp(data.Inflation + inflationChange, eco.InflationMin, eco.InflationMax);
            country.Inflation = data.Inflation;

            // Tech-Effekte anwenden (nur Spieler-Land)
            if (countryId == context.PlayerCountry?.Id)
            {
                var techManager = context.Game.GetSystem<TechTreeManager>();
                if (techManager != null)
                {
                    double eduBonus = techManager.GetEffect("education_level");
                    if (eduBonus > 0)
                        country.EducationLevel = Math.Clamp(country.EducationLevel + eduBonus * 0.1, 0, 1);

                    double unempBonus = techManager.GetEffect("unemployment");
                    if (unempBonus > 0)
                        country.UnemploymentRate = Math.Clamp(country.UnemploymentRate - unempBonus, 0, 1);
                }
            }
        }
    }

    private void RecordMoneySnapshot(GameContext context)
    {
        int totalDays = context.Game.TotalDays;

        // Alle 7 Tage einen Snapshot + immer am Tag 0 (Spielstart)
        if (_lastSnapshotDay >= 0 && totalDays - _lastSnapshotDay < 7)
            return;

        _lastSnapshotDay = totalDays;

        // Globale Ressourcenmengen erfassen
        var resourceTotals = new Dictionary<ResourceType, double>();
        foreach (var rt in Enum.GetValues<ResourceType>())
            resourceTotals[rt] = 0;
        foreach (var country in context.Countries.Values)
        {
            foreach (var rt in Enum.GetValues<ResourceType>())
                resourceTotals[rt] += country.GetResource(rt);
        }

        _moneyHistory.Add(new MoneySnapshot
        {
            TotalDays = totalDays,
            Year = context.Game.CurrentYear,
            Month = context.Game.CurrentMonth,
            Day = context.Game.CurrentDay,
            ResourceTotals = resourceTotals
        });

        // Maximal 200 Datenpunkte behalten (~3.8 Jahre Spielzeit)
        if (_moneyHistory.Count > 200)
            _moneyHistory.RemoveAt(0);
    }

    private double CalculateGrowthRate(Country country, CountryEconomyData data)
    {
        var eco = BalanceConfig.Economy;

        double baseGrowth = eco.BaseGrowthRate;
        double educationBonus = country.EducationLevel * eco.EducationBonusMultiplier;
        double populationBonus = Math.Clamp(country.PopulationGrowthRate * eco.PopulationBonusMultiplier, -eco.PopulationBonusClamp, eco.PopulationBonusClamp);
        double infraBonus = country.InfrastructureSpendingPercent * eco.InfraBonusMultiplier;
        double taxPenalty = (country.TaxRate - eco.TaxNeutralRate) * -eco.TaxPenaltyMultiplier;

        double debtPenalty = 0;
        if (country.GDP > 0)
        {
            double debtToGDP = country.NationalDebt / country.GDP;
            data.DebtToGDP = debtToGDP;
            debtPenalty = Math.Max(0, debtToGDP - eco.DebtThreshold) * -eco.DebtPenaltyMultiplier;
        }

        double randomFactor = (Random.Shared.NextDouble() - 0.5) * eco.RandomFactor;

        return Math.Clamp(
            baseGrowth + educationBonus + populationBonus + infraBonus + taxPenalty + debtPenalty + randomFactor,
            eco.GrowthMin, eco.GrowthMax);
    }
}

/// <summary>
/// Gecachte Wirtschaftsdaten pro Land
/// </summary>
public class CountryEconomyData
{
    public double Inflation { get; set; }
    public double TaxRate { get; set; }
    public double Budget { get; set; }
    public double DebtToGDP { get; set; }
}

/// <summary>
/// Ein Datenpunkt fuer das Guetermengen-Liniendiagramm
/// </summary>
public class MoneySnapshot
{
    public int TotalDays { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }

    /// <summary>
    /// Globale Ressourcenmengen zum Zeitpunkt des Snapshots
    /// </summary>
    public Dictionary<ResourceType, double> ResourceTotals { get; set; } = new();

    public string DateLabel => $"{Day}.{Month}.{Year}";
}
