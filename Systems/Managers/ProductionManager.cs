using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet Produktion und Industrie.
/// - Fabriken und Produktionsstätten
/// - Produktionsketten
/// - Ressourcenverarbeitung
/// - Industriekapazität
/// </summary>
public class ProductionManager : GameSystemBase
{
    public override string Name => "Production";
    public override int Priority => 25; // Nach Wirtschaft, vor Handel
    public override TickType[] SubscribedTicks => new[] { TickType.Daily, TickType.Weekly };

    // Industriedaten pro Land
    private readonly Dictionary<string, IndustryData> _industryData = new();
    private GameContext? _lastContext;
    private PoliticsManager? _politicsManager;

    // Produktionsketten und Verbrauch (aus balance-config.json geladen)
    private static Dictionary<ResourceType, ProductionRecipe> CivilianRecipes => _civilianRecipes;
    private static Dictionary<ResourceType, ProductionRecipe> MilitaryRecipes => _militaryRecipes;
    private static Dictionary<ResourceType, double> PopulationConsumption => BalanceConfig.Production.PopulationConsumption;

    private static Dictionary<ResourceType, ProductionRecipe> _civilianRecipes = new();
    private static Dictionary<ResourceType, ProductionRecipe> _militaryRecipes = new();
    private static bool _recipesLoaded = false;

    private static void EnsureRecipesLoaded()
    {
        if (_recipesLoaded) return;
        _recipesLoaded = true;

        foreach (var (type, cfg) in BalanceConfig.Production.CivilianRecipes)
            _civilianRecipes[type] = new ProductionRecipe(type, cfg.Inputs, cfg.Output);

        foreach (var (type, cfg) in BalanceConfig.Production.MilitaryRecipes)
            _militaryRecipes[type] = new ProductionRecipe(type, cfg.Inputs, cfg.Output);
    }

    // Wie viele Fabriken pro Rezept zugewiesen sind (pro Land)
    // Zivile Fabriken -> CivilianRecipes, Militaerfabriken -> MilitaryRecipes
    private readonly Dictionary<string, Dictionary<ResourceType, int>> _factoryAssignments = new();
    private readonly Dictionary<string, Dictionary<ResourceType, int>> _militaryAssignments = new();

    // Flag ob Provinz-Fabriken bereits geladen wurden
    private bool _provincesInitialized = false;

    public override void Initialize(GameContext context)
    {
        _lastContext = context;
        _politicsManager = context.Game.GetSystem<PoliticsManager>();
        EnsureRecipesLoaded();

        // Erste Initialisierung - Fabriken starten bei 0
        // Echte Werte kommen aus Provinzen (InitializeFromProvinces)
        foreach (var (countryId, country) in context.Countries)
        {
            _industryData[countryId] = new IndustryData
            {
                CivilianFactories = 0,
                MilitaryFactories = 0,
                Dockyards = 0,
                IndustrialEfficiency = 0.8,
                ProductionQueue = new List<ProductionOrder>()
            };
        }
    }

    /// <summary>
    /// Berechnet Fabriken aus Provinzen (wenn WorldMap verfuegbar)
    /// </summary>
    private void InitializeFromProvinces(GameContext context)
    {
        if (_provincesInitialized || context.WorldMap == null)
            return;

        _provincesInitialized = true;

        foreach (var (countryId, _) in context.Countries)
        {
            int civilianFromProvinces = 0;
            int militaryFromProvinces = 0;
            int dockyardsFromProvinces = 0;

            foreach (var province in context.WorldMap.Provinces.Values)
            {
                if (province.CountryId == countryId)
                {
                    civilianFromProvinces += province.CivilianFactories;
                    militaryFromProvinces += province.MilitaryFactories;
                    dockyardsFromProvinces += province.Dockyards;
                }
            }

            // Aktualisieren wenn irgendwelche Fabriken in Provinzen existieren
            int totalFromProvinces = civilianFromProvinces + militaryFromProvinces + dockyardsFromProvinces;
            if (totalFromProvinces > 0 && _industryData.TryGetValue(countryId, out var industry))
            {
                industry.CivilianFactories = civilianFromProvinces;
                industry.MilitaryFactories = militaryFromProvinces;
                industry.Dockyards = dockyardsFromProvinces;
            }
        }
    }

    /// <summary>
    /// Erzwingt Neusynchronisation der Fabriken aus Provinzen (z.B. nach Save/Load)
    /// </summary>
    public void ResyncFromProvinces(GameContext context)
    {
        if (context.WorldMap == null) return;

        foreach (var (countryId, _) in context.Countries)
        {
            int civilian = 0, military = 0, dockyards = 0;
            foreach (var province in context.WorldMap.Provinces.Values)
            {
                if (province.CountryId == countryId)
                {
                    civilian += province.CivilianFactories;
                    military += province.MilitaryFactories;
                    dockyards += province.Dockyards;
                }
            }

            if (_industryData.TryGetValue(countryId, out var industry))
            {
                industry.CivilianFactories = civilian;
                industry.MilitaryFactories = military;
                industry.Dockyards = dockyards;
            }
        }

        _provincesInitialized = true;
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        _lastContext = context;

        // Einmalig Provinz-Fabriken laden wenn WorldMap verfuegbar
        if (!_provincesInitialized)
        {
            InitializeFromProvinces(context);
        }

        switch (tickType)
        {
            case TickType.Daily:
                ProcessProduction(context);
                break;

            case TickType.Weekly:
                UpdateIndustrialCapacity(context);
                break;
        }
    }

    /// <summary>
    /// Befuellt DailyProduction/DailyConsumption aller Laender mit einer Vorschau
    /// des naechsten Tages-Ticks, ohne Lagerbestaende oder Politik zu veraendern.
    /// Noetig nach Spielstart und Save-Load: Bis zum ersten Tageswechsel waeren
    /// die Werte sonst leer und die UI (z.B. Logistik-Panel) zeigt faelschlich 0.
    /// </summary>
    public void PrimeDailyEstimates(GameContext context)
    {
        _lastContext = context;
        EnsureRecipesLoaded();

        if (!_provincesInitialized)
            InitializeFromProvinces(context);

        foreach (var country in context.Countries.Values)
        {
            country.DailyProduction.Clear();
            country.DailyConsumption.Clear();
        }

        ProcessMineProduction(context, applyEffects: false);

        foreach (var (countryId, industry) in _industryData)
        {
            if (!context.Countries.TryGetValue(countryId, out var country))
                continue;

            ProcessResourceConversion(country, industry, context, applyEffects: false);
            ProcessPopulationConsumption(country, applyEffects: false);
        }
    }

    private void ProcessProduction(GameContext context)
    {
        // DailyProduction/DailyConsumption werden in Game.SimulateDayInternal()
        // zurueckgesetzt bevor irgendein System laeuft - kein Clearing hier noetig

        // Minen-Produktion berechnen (für alle Länder)
        ProcessMineProduction(context);

        foreach (var (countryId, industry) in _industryData)
        {
            if (!context.Countries.TryGetValue(countryId, out var country))
                continue;

            // Verarbeite Rohstoffe zu verarbeiteten Gütern
            ProcessResourceConversion(country, industry, context);

            // Bevoelkerungsverbrauch (Konsumgueter, Energie, Elektronik)
            ProcessPopulationConsumption(country);

            // Verarbeite Produktionsaufträge
            ProcessProductionQueue(country, industry);
        }
    }

    /// <summary>
    /// Berechnet den Ressourcenverbrauch durch die Bevoelkerung
    /// und wendet Zufriedenheits-Effekte bei Engpaessen an
    /// </summary>
    private void ProcessPopulationConsumption(Country country, bool applyEffects = true)
    {
        double populationMillions = country.Population / 1_000_000.0;
        double totalSatisfactionPenalty = 0;

        foreach (var (resourceType, consumptionPerMillion) in PopulationConsumption)
        {
            double consumption = populationMillions * consumptionPerMillion;

            // Versuche Ressource zu verbrauchen
            double available = country.GetResource(resourceType);
            double actualConsumption = Math.Min(consumption, available);

            if (applyEffects && actualConsumption > 0)
                country.UseResource(resourceType, actualConsumption);

            // Vollen BEDARF tracken (nicht nur Ist-Verbrauch): Im Fluss-System
            // haengen Defizit-Erkennung (KI, Handel), Versorgungsanzeige und
            // der Tages-Cap an diesem Wert - ungedeckter Bedarf muss sichtbar sein
            if (consumption > 0)
            {
                if (!country.DailyConsumption.ContainsKey(resourceType))
                    country.DailyConsumption[resourceType] = 0;
                country.DailyConsumption[resourceType] += consumption;
            }

            // Satisfaction-System: Wenn weniger als 80% des Bedarfs gedeckt
            if (consumption > 0 && actualConsumption < consumption * 0.8)
            {
                double deficitRatio = 1.0 - (actualConsumption / consumption);

                // Gewichtung nach Ressourcentyp (Konsumgueter am kritischsten)
                double weight = resourceType switch
                {
                    ResourceType.ConsumerGoods => 1.5,   // Konsumguetermangel spuerbar
                    ResourceType.Electronics => 0.7,     // Elektronikmangel aergerlich
                    ResourceType.Oil => 0.8,             // Energiemangel moderat
                    ResourceType.NaturalGas => 0.6,
                    ResourceType.Machinery => 0.5,       // Maschinenmangel (Haushaltsgeraete)
                    ResourceType.Coal => 0.4,
                    _ => 0.5
                };

                totalSatisfactionPenalty += deficitRatio * weight;
            }
        }

        // Zufriedenheits-Effekte auf Stabilitaet und oeffentliche Unterstuetzung anwenden
        if (applyEffects && totalSatisfactionPenalty > 0 && _politicsManager != null)
        {
            var politics = _politicsManager.GetPolitics(country.Id);
            if (politics != null)
            {
                // Taeglich kleine Anpassung (akkumuliert sich ueber Wochen)
                double stabilityHit = Math.Min(totalSatisfactionPenalty * 0.001, 0.005);
                double supportHit = Math.Min(totalSatisfactionPenalty * 0.002, 0.01);

                politics.Stability = Math.Clamp(politics.Stability - stabilityHit, 0, 1);
                politics.PublicSupport = Math.Clamp(politics.PublicSupport - supportHit, 0, 1);
            }
        }
    }

    /// <summary>
    /// Berechnet die Produktion aller Minen und fügt sie den Ländern hinzu
    /// </summary>
    private void ProcessMineProduction(GameContext context, bool applyEffects = true)
    {
        if (context.WorldMap == null) return;

        // Sammle Minen-Produktion pro Land
        var mineProduction = new Dictionary<string, Dictionary<ResourceType, double>>();

        foreach (var (_, province) in context.WorldMap.Provinces)
        {
            if (province.Mines.Count == 0) continue;
            if (string.IsNullOrEmpty(province.CountryId)) continue;

            if (!mineProduction.ContainsKey(province.CountryId))
                mineProduction[province.CountryId] = new Dictionary<ResourceType, double>();

            // Provinz-Ressourcenvorkommen fuer Produktionsbonus pruefen
            var provinceDeposits = context.WorldMap.GetProvinceResources(province.Name);

            // Provinz-Ressourcenwerte mit Abundance-Daten (0.0-1.0) holen
            var provinceResValues = context.WorldMap.GetProvinceResourcesWithValues(province.Name, province.CountryId);

            foreach (var mine in province.Mines)
            {
                var resourceType = Mine.GetResourceType(mine.Type);
                double production = mine.ProductionPerDay * mine.Level;

                // Produktion skaliert mit Ressourcenvorkommen der Provinz (0.0-1.0)
                // Hohe Vorkommen (1.0) = volle Produktion, niedrige = weniger.
                // WICHTIG: Mindestens 25% Basisleistung, sonst produzieren Minen in
                // Laendern mit Abundanz 0.0 (z.B. Eisen in DEU) gar nichts -
                // eine gebaute Mine muss sich immer zumindest etwas lohnen.
                float abundanceValue = 0.05f;
                foreach (var (type, _, value) in provinceResValues)
                {
                    if (type == resourceType) { abundanceValue = value; break; }
                }
                production *= Math.Max(abundanceValue, 0.25f);

                if (!mineProduction[province.CountryId].ContainsKey(resourceType))
                    mineProduction[province.CountryId][resourceType] = 0;

                mineProduction[province.CountryId][resourceType] += production;
            }
        }

        // Tech-Bonus fuer Spieler-Minen berechnen
        var techManager = context.Game.GetSystem<TechTreeManager>();
        string? playerId = context.PlayerCountry?.Id;

        // Füge Minen-Produktion zu den Ländern hinzu
        foreach (var (countryId, resources) in mineProduction)
        {
            if (!context.Countries.TryGetValue(countryId, out var country))
                continue;

            foreach (var (resourceType, amount) in resources)
            {
                double finalAmount = amount;

                // Tech-Bonus nur fuer Spieler-Land
                if (countryId == playerId && techManager != null)
                {
                    double techBonus = GetMineTechBonus(techManager, resourceType);
                    finalAmount *= (1.0 + techBonus);
                }

                // Ressource zum Lager hinzufügen
                if (applyEffects)
                    country.AddResource(resourceType, finalAmount);

                // Tägliche Produktion tracken
                if (!country.DailyProduction.ContainsKey(resourceType))
                    country.DailyProduction[resourceType] = 0;
                country.DailyProduction[resourceType] += finalAmount;
            }
        }
    }

    /// <summary>
    /// Berechnet den Tech-Bonus fuer Minenproduktion basierend auf Ressourcentyp
    /// </summary>
    private static double GetMineTechBonus(TechTreeManager techManager, ResourceType resourceType)
    {
        double bonus = techManager.GetEffect("mining_output");
        switch (resourceType)
        {
            case ResourceType.Oil:
                bonus += techManager.GetEffect("oil_output");
                break;
            case ResourceType.NaturalGas:
            case ResourceType.Uranium:
                bonus += techManager.GetEffect("energy_output");
                break;
        }
        return bonus;
    }

    private void ProcessResourceConversion(Country country, IndustryData industry, GameContext context, bool applyEffects = true)
    {
        double efficiency = industry.IndustrialEfficiency;

        // Vorschau-Modus: Lagerbewegungen nur virtuell verbuchen
        var virtualDelta = applyEffects ? null : new Dictionary<ResourceType, double>();

        // Fabrik-Oelverbrauch: 0.01 Oel pro Fabrik pro Tag
        int totalFactories = industry.CivilianFactories + industry.MilitaryFactories + industry.Dockyards;
        double oilNeeded = totalFactories * 0.01;
        if (oilNeeded > 0)
        {
            double oilAvailable = country.GetResource(ResourceType.Oil);
            double oilConsumed = Math.Min(oilNeeded, oilAvailable);
            if (applyEffects && oilConsumed > 0)
                country.UseResource(ResourceType.Oil, oilConsumed);

            // Vollen Bedarf tracken (siehe ProcessPopulationConsumption)
            if (!country.DailyConsumption.ContainsKey(ResourceType.Oil))
                country.DailyConsumption[ResourceType.Oil] = 0;
            country.DailyConsumption[ResourceType.Oil] += oilNeeded;

            // Effizienz sinkt wenn nicht genug Oel vorhanden
            if (oilAvailable < oilNeeded)
            {
                double oilRatio = oilNeeded > 0 ? oilAvailable / oilNeeded : 1.0;
                // Mindestens 30% Effizienz, auch ohne Oel
                efficiency *= 0.3 + 0.7 * oilRatio;
            }
        }

        // Tech-Bonus fuer Fabrikproduktion (nur Spieler-Land)
        double factoryTechBonus = 0;
        double electronicsTechBonus = 0;
        if (country.Id == context.PlayerCountry?.Id)
        {
            var techManager = context.Game.GetSystem<TechTreeManager>();
            if (techManager != null)
            {
                factoryTechBonus = techManager.GetEffect("factory_output");
                electronicsTechBonus = techManager.GetEffect("electronics_output");
            }
        }

        // === Zivile Produktion (Zivilfabriken) ===
        if (!_factoryAssignments.TryGetValue(country.Id, out var civAssignments))
        {
            civAssignments = new Dictionary<ResourceType, int>();
            int recipeCount = CivilianRecipes.Count > 0 ? CivilianRecipes.Count : 1;
            int perRecipe = industry.CivilianFactories / recipeCount;
            int factoriesPerRecipe = (perRecipe / 10) * 10;
            if (factoriesPerRecipe == 0 && industry.CivilianFactories >= recipeCount * 10)
                factoriesPerRecipe = 10;
            foreach (var outputType in CivilianRecipes.Keys)
                civAssignments[outputType] = factoriesPerRecipe;
            _factoryAssignments[country.Id] = civAssignments;
        }

        RunRecipes(country, CivilianRecipes, civAssignments, efficiency, factoryTechBonus, electronicsTechBonus, virtualDelta);

        // === Militaerische Produktion (Militaerfabriken) ===
        if (!_militaryAssignments.TryGetValue(country.Id, out var milAssignments))
        {
            milAssignments = new Dictionary<ResourceType, int>();
            int recipeCount = MilitaryRecipes.Count > 0 ? MilitaryRecipes.Count : 1;
            int perRecipe = industry.MilitaryFactories / recipeCount;
            int factoriesPerRecipe = (perRecipe / 10) * 10;
            if (factoriesPerRecipe == 0 && industry.MilitaryFactories >= recipeCount * 10)
                factoriesPerRecipe = 10;
            foreach (var outputType in MilitaryRecipes.Keys)
                milAssignments[outputType] = factoriesPerRecipe;
            _militaryAssignments[country.Id] = milAssignments;
        }

        RunRecipes(country, MilitaryRecipes, milAssignments, efficiency, factoryTechBonus, electronicsTechBonus, virtualDelta);
    }

    /// <summary>
    /// Fuehrt Produktionsrezepte mit zugewiesenen Fabriken aus.
    /// techFactoryBonus und techElectronicsBonus kommen aus abgeschlossenen Technologien.
    /// virtualDelta != null = Vorschau-Modus: Lagerbewegungen werden nur im
    /// Delta-Konto verbucht statt am echten Lager, das Tracking laeuft normal.
    /// </summary>
    private static void RunRecipes(Country country, Dictionary<ResourceType, ProductionRecipe> recipes,
        Dictionary<ResourceType, int> assignments, double efficiency,
        double techFactoryBonus = 0, double techElectronicsBonus = 0,
        Dictionary<ResourceType, double>? virtualDelta = null)
    {
        foreach (var (outputType, recipe) in recipes)
        {
            int assignedFactories = assignments.TryGetValue(outputType, out var count) ? count : 0;
            if (assignedFactories < 10) continue;

            // Pro 10 Fabriken laeuft ein Produktionszyklus
            int productionBlocks = assignedFactories / 10;

            // Anteilige Produktion (HOI4-Stil): Fehlen Inputs teilweise, laeuft
            // die Produktion gedrosselt weiter statt komplett zu stoppen.
            // Das verhindert Kaskaden-Ausfaelle entlang der Produktionskette.
            double ratio = 1.0;
            foreach (var (inputType, amount) in recipe.Inputs)
            {
                double needed = amount * productionBlocks;
                if (needed <= 0) continue;
                double available = country.GetResource(inputType)
                    + (virtualDelta?.GetValueOrDefault(inputType) ?? 0);
                ratio = Math.Min(ratio, Math.Clamp(available / needed, 0, 1));
            }
            if (recipe.Inputs.Length > 0 && ratio <= 0.001) continue;

            foreach (var (inputType, amount) in recipe.Inputs)
            {
                double needed = amount * productionBlocks;
                double used = needed * ratio;

                if (used > 0)
                {
                    if (virtualDelta == null)
                        country.UseResource(inputType, Math.Min(used, country.GetResource(inputType)));
                    else
                        virtualDelta[inputType] = virtualDelta.GetValueOrDefault(inputType) - used;
                }

                // Vollen Bedarf tracken (nicht nur ratio-gedrosselten Ist-Verbrauch):
                // so bleibt ein Input-Defizit fuer KI/Handel/Anzeige sichtbar
                if (!country.DailyConsumption.ContainsKey(inputType))
                    country.DailyConsumption[inputType] = 0;
                country.DailyConsumption[inputType] += needed;
            }

            // Basis-Produktion mit Effizienz und Drosselung
            double outputAmount = recipe.OutputAmount * productionBlocks * ratio * efficiency;

            // Tech-Bonus: factory_output gilt fuer alle, electronics_output nur fuer Elektronik
            double techBonus = techFactoryBonus;
            if (outputType == ResourceType.Electronics)
                techBonus += techElectronicsBonus;
            outputAmount *= (1.0 + techBonus);

            if (outputAmount <= 0) continue;

            if (virtualDelta == null)
                country.AddResource(outputType, outputAmount);
            else
                virtualDelta[outputType] = virtualDelta.GetValueOrDefault(outputType) + outputAmount;

            if (!country.DailyProduction.ContainsKey(outputType))
                country.DailyProduction[outputType] = 0;
            country.DailyProduction[outputType] += outputAmount;
        }
    }

    private void ProcessProductionQueue(Country country, IndustryData industry)
    {
        // Verarbeite Produktionsauftraege (Militaergueter etc.)
        var queue = industry.ProductionQueue;
        for (int i = 0; i < queue.Count; i++)
        {
            var order = queue[i];
            if (order.IsCompleted) continue;

            order.Progress += industry.MilitaryFactories * industry.IndustrialEfficiency * 0.01;

            if (order.Progress >= order.RequiredProgress)
            {
                order.IsCompleted = true;

                // Produziertes Equipment dem Land hinzufuegen
                ApplyCompletedProduction(country, order);
            }
        }

        // Entferne abgeschlossene Auftraege
        industry.ProductionQueue.RemoveAll(o => o.IsCompleted);
    }

    /// <summary>
    /// Wendet abgeschlossene Produktionsauftraege an - erzeugt Militaer-Equipment
    /// </summary>
    private void ApplyCompletedProduction(Country country, ProductionOrder order)
    {
        // Militaer-Equipment zum Lager hinzufuegen
        if (!country.MilitaryEquipment.ContainsKey(order.ItemType))
            country.MilitaryEquipment[order.ItemType] = 0;
        country.MilitaryEquipment[order.ItemType]++;
    }

    private void UpdateIndustrialCapacity(GameContext context)
    {
        foreach (var (countryId, industry) in _industryData)
        {
            // Effizienz verbessert sich langsam
            industry.IndustrialEfficiency = Math.Min(1.0, industry.IndustrialEfficiency + 0.001);

            // AI-Laender: Fabrikzuweisung dynamisch optimieren
            if (countryId != context.PlayerCountry?.Id &&
                context.Countries.TryGetValue(countryId, out var country))
            {
                OptimizeFactoryAssignments(country, industry);
            }
        }
    }

    /// <summary>
    /// Optimiert die Fabrikzuweisung fuer AI-Laender basierend auf Bedarf und Rohstoffverfuegbarkeit.
    /// Fabriken werden dorthin verschoben wo sie am meisten gebraucht werden.
    /// </summary>
    private void OptimizeFactoryAssignments(Country country, IndustryData industry)
    {
        if (industry.CivilianFactories < 10) return;

        // === Zivile Fabriken optimieren ===
        if (!_factoryAssignments.TryGetValue(country.Id, out var civAssignments))
            return;

        int totalCivFactories = industry.CivilianFactories;
        var scores = new Dictionary<ResourceType, double>();
        double totalScore = 0;

        foreach (var (outputType, recipe) in CivilianRecipes)
        {
            double score = 1.0; // Basis-Score

            // Fluss-System: Bedarf = taegliches Defizit (Verbrauch vs. Produktion)
            double consumption = country.DailyConsumption.GetValueOrDefault(outputType, 0);
            double production = country.DailyProduction.GetValueOrDefault(outputType, 0);

            if (consumption > 0 && production < consumption)
                score += 3.0; // Tagesdefizit: dringend
            else if (consumption > 0 && production < consumption * 1.2)
                score += 1.5; // Knapp gedeckt: erhoehter Bedarf

            // Konsumgueter wichtig fuer Zufriedenheit
            if (outputType == ResourceType.ConsumerGoods && production < consumption)
                score += 2.0;

            // Rohstoff-Verfuegbarkeit pruefen: Fließt genug Input fuer das Rezept?
            bool hasInputs = true;
            foreach (var (inputType, inputAmount) in recipe.Inputs)
            {
                double inputFlow = country.DailyProduction.GetValueOrDefault(inputType, 0)
                    + country.GetResource(inputType);
                if (inputFlow < inputAmount)
                {
                    hasInputs = false;
                    break;
                }
            }
            if (!hasInputs)
                score *= 0.1; // Stark reduzieren wenn Rohstoffe fehlen

            // Deutliche Ueberproduktion reduzieren (Ueberschuss verfaellt ohnehin)
            if (consumption > 0 && production > consumption * 2)
                score *= 0.3;

            scores[outputType] = Math.Max(0.1, score);
            totalScore += scores[outputType];
        }

        if (totalScore <= 0) return;

        // Fabriken proportional nach Score verteilen (in 10er-Bloecken)
        int assigned = 0;
        var newAssignments = new Dictionary<ResourceType, int>();
        var sortedRecipes = scores.OrderByDescending(s => s.Value).ToList();

        foreach (var (outputType, score) in sortedRecipes)
        {
            double ratio = score / totalScore;
            int factories = ((int)(totalCivFactories * ratio) / 10) * 10; // Auf 10er abrunden
            newAssignments[outputType] = factories;
            assigned += factories;
        }

        // Uebrige Fabriken an hoechsten Score vergeben
        int remaining = totalCivFactories - assigned;
        if (remaining >= 10 && sortedRecipes.Count > 0)
        {
            var topRecipe = sortedRecipes[0].Key;
            newAssignments[topRecipe] += (remaining / 10) * 10;
        }

        _factoryAssignments[country.Id] = newAssignments;

        // === Militaerische Fabriken gleichmaessig verteilen ===
        if (industry.MilitaryFactories >= 10)
        {
            var milAssignments = new Dictionary<ResourceType, int>();
            int recipeCount = MilitaryRecipes.Count > 0 ? MilitaryRecipes.Count : 1;
            int perRecipe = (industry.MilitaryFactories / recipeCount / 10) * 10;
            foreach (var outputType in MilitaryRecipes.Keys)
                milAssignments[outputType] = Math.Max(perRecipe, 10);
            _militaryAssignments[country.Id] = milAssignments;
        }
    }

    /// <summary>
    /// Gibt Industriedaten für ein Land zurück.
    /// Synchronisiert Fabriken aus Provinzen falls noch nicht geschehen.
    /// </summary>
    public IndustryData? GetIndustryData(string countryId)
    {
        if (!_provincesInitialized && _lastContext != null)
            InitializeFromProvinces(_lastContext);

        return _industryData.TryGetValue(countryId, out var data) ? data : null;
    }

    /// <summary>
    /// Baut neue Fabriken
    /// </summary>
    public void BuildFactory(string countryId, FactoryType type)
    {
        if (!_industryData.TryGetValue(countryId, out var industry))
            return;

        switch (type)
        {
            case FactoryType.Civilian:
                industry.CivilianFactories += 10;
                break;
            case FactoryType.Military:
                industry.MilitaryFactories += 10;
                break;
            case FactoryType.Dockyard:
                industry.Dockyards += 10;
                break;
        }
    }

    /// <summary>
    /// Gibt die zivile Fabrik-Zuweisung für ein Land zurück
    /// </summary>
    public Dictionary<ResourceType, int> GetFactoryAssignments(string countryId)
    {
        if (_factoryAssignments.TryGetValue(countryId, out var assignments))
            return new Dictionary<ResourceType, int>(assignments);
        return new Dictionary<ResourceType, int>();
    }

    /// <summary>
    /// Gibt die militaerische Fabrik-Zuweisung für ein Land zurück
    /// </summary>
    public Dictionary<ResourceType, int> GetMilitaryAssignments(string countryId)
    {
        if (_militaryAssignments.TryGetValue(countryId, out var assignments))
            return new Dictionary<ResourceType, int>(assignments);
        return new Dictionary<ResourceType, int>();
    }

    /// <summary>
    /// Setzt die Anzahl der Fabriken für ein bestimmtes Rezept (zivil oder militaerisch)
    /// </summary>
    public void SetFactoryAssignment(string countryId, ResourceType outputType, int factoryCount)
    {
        // Bestimme ob zivil oder militaerisch
        bool isMilitary = MilitaryRecipes.ContainsKey(outputType);
        var targetDict = isMilitary ? _militaryAssignments : _factoryAssignments;

        if (!targetDict.ContainsKey(countryId))
            targetDict[countryId] = new Dictionary<ResourceType, int>();

        int rounded = (Math.Max(0, factoryCount) / 10) * 10;
        targetDict[countryId][outputType] = rounded;
    }

    /// <summary>
    /// Gibt alle zivilen Produktionsrezepte zurück
    /// </summary>
    public static IReadOnlyDictionary<ResourceType, ProductionRecipe> GetCivilianRecipes() => CivilianRecipes;

    /// <summary>
    /// Gibt alle militaerischen Produktionsrezepte zurück
    /// </summary>
    public static IReadOnlyDictionary<ResourceType, ProductionRecipe> GetMilitaryRecipes() => MilitaryRecipes;
}

/// <summary>
/// Industriedaten eines Landes
/// </summary>
public class IndustryData
{
    public int CivilianFactories { get; set; }
    public int MilitaryFactories { get; set; }
    public int Dockyards { get; set; }
    public double IndustrialEfficiency { get; set; }
    public List<ProductionOrder> ProductionQueue { get; set; } = new();
}

/// <summary>
/// Produktionsrezept (Input -> Output)
/// </summary>
public class ProductionRecipe
{
    public ResourceType OutputType { get; }
    public (ResourceType Type, double Amount)[] Inputs { get; }
    public double OutputAmount { get; }

    public ProductionRecipe(ResourceType outputType, (ResourceType, double)[] inputs, double outputAmount)
    {
        OutputType = outputType;
        Inputs = inputs;
        OutputAmount = outputAmount;
    }
}

/// <summary>
/// Produktionsauftrag
/// </summary>
public class ProductionOrder
{
    public string ItemType { get; set; } = "";  // z.B. "Infantry Equipment", "Tank"
    public double Progress { get; set; }
    public double RequiredProgress { get; set; }
    public bool IsCompleted { get; set; }
}

public enum FactoryType
{
    Civilian,
    Military,
    Dockyard
}
