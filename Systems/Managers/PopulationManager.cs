using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet Bevölkerung und Migration.
/// - Bevölkerungswachstum
/// - Migration zwischen Ländern
/// - Demographie (Alter, Arbeitskräfte)
/// - Flüchtlinge
/// </summary>
public class PopulationManager : GameSystemBase
{
    public override string Name => "Population";
    public override int Priority => 5; // VOR EconomyManager (10), da PopGrowthRate dort gelesen wird
    public override TickType[] SubscribedTicks => new[] { TickType.Daily, TickType.Monthly, TickType.Yearly };

    // Demografische Daten pro Land
    private readonly Dictionary<string, Demographics> _demographics = new();

    // Migrationsdruck zwischen Ländern
    private readonly Dictionary<(string From, string To), double> _migrationPressure = new();

    // Nahrungsbedarf pro Person pro Tag - nutze GameConfig Konstante

    // Hungersnot-Tracking pro Land
    private readonly Dictionary<string, int> _starvationDays = new();

    // Referenzen fuer andere Manager
    private NotificationManager? _notificationManager;
    private PoliticsManager? _politicsManager;
    private ConflictManager? _conflictManager;
    private MilitaryManager? _militaryManager;

    public override void Initialize(GameContext context)
    {
        _notificationManager = context.Game.GetSystem<NotificationManager>();
        _politicsManager = context.Game.GetSystem<PoliticsManager>();
        _conflictManager = context.Game.GetSystem<ConflictManager>();
        _militaryManager = context.Game.GetSystem<MilitaryManager>();

        foreach (var (countryId, country) in context.Countries)
        {
            _demographics[countryId] = new Demographics
            {
                TotalPopulation = country.Population,
                BirthRate = 0.012,        // 1.2% pro Jahr
                DeathRate = 0.008,        // 0.8% pro Jahr
                WorkingAgeRatio = 0.65,   // 65% im arbeitsfähigen Alter
                UrbanizationRate = 0.5,   // 50% städtisch
                LiteracyRate = 0.9        // 90% Alphabetisierung
            };
        }
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        switch (tickType)
        {
            case TickType.Daily:
                ProcessFoodConsumption(context);
                break;

            case TickType.Monthly:
                ProcessMigration(context);
                break;

            case TickType.Yearly:
                UpdatePopulationGrowth(context);
                UpdateDemographics(context);
                break;
        }
    }

    /// <summary>
    /// Verarbeitet taeglichen Nahrungsverbrauch und Verhungern
    /// </summary>
    private void ProcessFoodConsumption(GameContext context)
    {
        foreach (var (countryId, country) in context.Countries)
        {
            if (!_demographics.TryGetValue(countryId, out var demo))
                continue;

            // Berechne Nahrungsbedarf
            double foodNeeded = demo.TotalPopulation * GameConfig.FOOD_PER_PERSON_PER_DAY;
            double foodAvailable = country.GetResource(ResourceType.Food);

            if (foodAvailable >= foodNeeded)
            {
                // Genug Nahrung - verbrauchen und Hungertage zuruecksetzen
                country.UseResource(ResourceType.Food, foodNeeded);
                _starvationDays[countryId] = 0;

                // Verbrauch fuer Logistik-Panel tracken
                if (!country.DailyConsumption.ContainsKey(ResourceType.Food))
                    country.DailyConsumption[ResourceType.Food] = 0;
                country.DailyConsumption[ResourceType.Food] += foodNeeded;
            }
            else
            {
                // Nicht genug Nahrung!
                // Verbrauche was da ist
                if (foodAvailable > 0)
                    country.UseResource(ResourceType.Food, foodAvailable);

                // Bedarf tracken (vollen Bedarf, nicht nur Teilverzehr)
                if (!country.DailyConsumption.ContainsKey(ResourceType.Food))
                    country.DailyConsumption[ResourceType.Food] = 0;
                country.DailyConsumption[ResourceType.Food] += foodNeeded;

                // Erhoehe Hungertage
                if (!_starvationDays.ContainsKey(countryId))
                    _starvationDays[countryId] = 0;
                _starvationDays[countryId]++;

                // Nach 30 Tagen ohne genug Nahrung beginnt das Sterben
                int starveDays = _starvationDays[countryId];
                if (starveDays >= 30)
                {
                    // Berechne wie viel Prozent der Bevoelkerung verhungert
                    // Langsame Eskalation ueber 90 Tage bis Maximum
                    double starvationSeverity = Math.Min(1.0, (starveDays - 30) / 90.0); // Max nach 120 Tagen
                    double foodDeficit = 1.0 - (foodAvailable / Math.Max(foodNeeded, 0.001));

                    // Todesfaktor: 0.01% bis 0.2% pro Tag bei voller Hungersnot
                    double deathRate = 0.0001 + (starvationSeverity * foodDeficit * 0.0019);
                    long deaths = (long)(demo.TotalPopulation * deathRate);

                    if (deaths > 0)
                    {
                        // Bevoelkerung darf nie unter 0 fallen
                        deaths = Math.Min(deaths, demo.TotalPopulation - 1);
                        deaths = Math.Min(deaths, country.Population - 1);
                        if (deaths <= 0) continue;

                        demo.TotalPopulation -= deaths;
                        country.Population -= deaths;

                        // Benachrichtigung fuer Spieler
                        if (country.Id == context.PlayerCountry?.Id)
                        {
                            context.Events.Publish(new StarvationEvent(countryId, deaths));

                            // Warnung nur alle 7 Tage senden um Spam zu vermeiden
                            if (starveDays % 7 == 0)
                            {
                                _notificationManager?.AddNotification(
                                    "Hungersnot!",
                                    $"{deaths:N0} Menschen sind heute verhungert. Importiere Nahrung!",
                                    NotificationType.Danger,
                                    countryId);
                            }
                        }
                    }
                }
            }
        }
    }

    private void ProcessMigration(GameContext context)
    {
        // Berechne Migrationsdruck basierend auf:
        // - Wirtschaftlicher Unterschied
        // - Stabilität
        // - Kriege

        foreach (var fromCountry in context.Countries.Values)
        {
            foreach (var toCountry in context.Countries.Values)
            {
                if (fromCountry.Id == toCountry.Id) continue;

                double pressure = CalculateMigrationPressure(fromCountry, toCountry, context);
                _migrationPressure[(fromCountry.Id, toCountry.Id)] = pressure;

                // Wenn Druck hoch genug, migrieren Menschen
                if (pressure > 0.5)
                {
                    long migrants = (long)(fromCountry.Population * pressure * 0.0001); // Max 0.01% pro Monat
                    if (migrants > 0)
                    {
                        ApplyMigration(fromCountry.Id, toCountry.Id, migrants, context);
                    }
                }
            }
        }
    }

    private double CalculateMigrationPressure(Country from, Country to, GameContext context)
    {
        double pressure = 0;

        // Wirtschaftlicher Faktor (BIP pro Kopf)
        double gdpPerCapitaFrom = from.GDP / Math.Max(1, from.Population);
        double gdpPerCapitaTo = to.GDP / Math.Max(1, to.Population);

        if (gdpPerCapitaFrom > 0 && gdpPerCapitaTo > gdpPerCapitaFrom)
        {
            pressure += (gdpPerCapitaTo / gdpPerCapitaFrom - 1) * 0.3;
        }

        // Stabilitaetsfaktor: Niedrige Stabilitaet im Herkunftsland treibt Migration
        if (_politicsManager != null)
        {
            var fromPolitics = _politicsManager.GetPolitics(from.Id);
            var toPolitics = _politicsManager.GetPolitics(to.Id);

            if (fromPolitics != null && fromPolitics.Stability < 0.4)
                pressure += (0.4 - fromPolitics.Stability) * 0.5; // Max +0.2 bei Stabilitaet 0

            if (toPolitics != null && toPolitics.Stability > 0.6)
                pressure += (toPolitics.Stability - 0.6) * 0.2; // Max +0.08 bei Stabilitaet 1
        }

        // Kriegsfaktor: Aktive Kriege im Herkunftsland treiben Migration stark
        if (_militaryManager != null)
        {
            var wars = _militaryManager.GetWars(from.Id);
            if (wars.Any())
                pressure += 0.15 * wars.Count(); // +0.15 pro Krieg
        }

        // Konfliktfaktor: Vordefinierte Konflikte (Buergerkriege etc.)
        if (_conflictManager != null && _conflictManager.IsCountryAtWar(from.Id))
        {
            string intensity = _conflictManager.GetConflictIntensity(from.Id);
            pressure += intensity switch
            {
                "high" => 0.3,
                "medium" => 0.15,
                "low" => 0.05,
                _ => 0.1
            };
        }

        return Math.Clamp(pressure, 0, 1);
    }

    private void ApplyMigration(string fromId, string toId, long amount, GameContext context)
    {
        if (!_demographics.TryGetValue(fromId, out var fromDemo)) return;
        if (!_demographics.TryGetValue(toId, out var toDemo)) return;

        // Bevoelkerung darf nie unter 1 fallen
        amount = Math.Min(amount, fromDemo.TotalPopulation - 1);
        if (amount <= 0) return;

        fromDemo.TotalPopulation -= amount;
        toDemo.TotalPopulation += amount;

        // Publiziere Event
        context.Events.Publish(new PopulationMigratedEvent(fromId, toId, amount));
    }

    private void UpdatePopulationGrowth(GameContext context)
    {
        foreach (var (countryId, demo) in _demographics)
        {
            if (!context.Countries.TryGetValue(countryId, out var country))
                continue;

            // Natuerliches Wachstum
            double growthRate = demo.BirthRate - demo.DeathRate;

            // Tech-Bonus: population_growth (nur Spieler-Land)
            if (countryId == context.PlayerCountry?.Id)
            {
                var techManager = context.Game.GetSystem<TechTreeManager>();
                if (techManager != null)
                    growthRate += techManager.GetEffect("population_growth");
            }

            long growth = (long)(demo.TotalPopulation * growthRate);

            demo.TotalPopulation += growth;

            // Synchronisiere mit Country-Objekt
            country.Population = demo.TotalPopulation;
            country.PopulationGrowthRate = growthRate;
        }
    }

    private void UpdateDemographics(GameContext context)
    {
        foreach (var (countryId, demo) in _demographics)
        {
            // Urbanisierung steigt langsam
            demo.UrbanizationRate = Math.Min(0.95, demo.UrbanizationRate + 0.002);

            // Alphabetisierung steigt
            demo.LiteracyRate = Math.Min(1.0, demo.LiteracyRate + 0.005);

            // Geburtenrate sinkt mit Urbanisierung
            demo.BirthRate = Math.Max(0.008, demo.BirthRate - (demo.UrbanizationRate * 0.001));
        }
    }

}

/// <summary>
/// Demografische Daten eines Landes
/// </summary>
public class Demographics
{
    public long TotalPopulation { get; set; }
    public double BirthRate { get; set; }        // Pro Jahr
    public double DeathRate { get; set; }        // Pro Jahr
    public double WorkingAgeRatio { get; set; }  // Anteil 15-65 Jahre
    public double UrbanizationRate { get; set; } // Anteil städtischer Bevölkerung
    public double LiteracyRate { get; set; }     // Alphabetisierungsrate
}
