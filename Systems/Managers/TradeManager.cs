using GrandStrategyGame.Data;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet Handel und Märkte.
/// - Import/Export zwischen Ländern
/// - Weltmarktpreise
/// - Handelsabkommen
/// - Embargos
/// - EU-Freihandelszone (keine Embargos, Bonus-Effizienz)
/// </summary>
public class TradeManager : GameSystemBase
{
    public override string Name => "Trade";
    public override int Priority => 30; // Nach Produktion, damit produzierte Ressourcen verfuegbar sind
    public override TickType[] SubscribedTicks => new[] { TickType.Daily, TickType.Weekly };

    // Aktive Handelsabkommen
    private readonly List<TradeAgreement> _tradeAgreements = new();

    // Embargos
    private readonly HashSet<(string Country1, string Country2)> _embargos = new();

    // Handelspartner-Praeferenzen (basierend auf Distanz/Diplomatie)
    private readonly Dictionary<(string, string), double> _tradeEfficiency = new();

    // Referenz zum DiplomacyManager für EU-Prüfungen
    private DiplomacyManager? _diplomacyManager;

    // Referenz zum NotificationManager für Handels-Benachrichtigungen
    private NotificationManager? _notificationManager;

    // Speichert fehlgeschlagene Handelsversuche um Spam zu vermeiden (nur 1x pro Tag benachrichtigen)
    private readonly HashSet<string> _failedTradesNotifiedToday = new();

    // Letzter Kontext (fuer Spielzeit-Zugriff)
    private GameContext? _lastContext;

    // Zaehlt aufeinanderfolgende Fehltage pro Abkommen (fuer automatische Kuendigung)
    private readonly Dictionary<string, int> _consecutiveFailDays = new();

    // Tatsaechlich gehandelte Ressourcenmengen pro Land und Tag (Import positiv, Export negativ)
    private readonly Dictionary<(string CountryId, ResourceType), double> _dailyTradeVolume = new();

    public override void Initialize(GameContext context)
    {
        // Hole Referenz zum DiplomacyManager
        _diplomacyManager = context.Game.GetSystem<DiplomacyManager>();

        // Hole Referenz zum NotificationManager
        _notificationManager = context.Game.GetSystem<NotificationManager>();

        // Initialisiere Handelseffizienz basierend auf geografischer Naehe
        InitializeTradeEfficiency(context);
    }

    private void InitializeTradeEfficiency(GameContext context)
    {
        // EU-Mitglieder aus DiplomacyManager holen (Freihandelszone - 50% Bonus)
        if (_diplomacyManager != null)
        {
            var euMembers = _diplomacyManager.GetCountriesInAlliance("EU");
            for (int i = 0; i < euMembers.Count; i++)
            {
                for (int j = i + 1; j < euMembers.Count; j++)
                {
                    _tradeEfficiency[(euMembers[i], euMembers[j])] = BalanceConfig.Trade.EuTradeBonus;
                    _tradeEfficiency[(euMembers[j], euMembers[i])] = BalanceConfig.Trade.EuTradeBonus;
                }
            }

            // ASEAN-Mitglieder (20% Bonus)
            var aseanMembers = _diplomacyManager.GetCountriesInAlliance("ASEAN");
            for (int i = 0; i < aseanMembers.Count; i++)
            {
                for (int j = i + 1; j < aseanMembers.Count; j++)
                {
                    if (!_tradeEfficiency.ContainsKey((aseanMembers[i], aseanMembers[j])))
                    {
                        _tradeEfficiency[(aseanMembers[i], aseanMembers[j])] = BalanceConfig.Trade.AseanTradeBonus;
                        _tradeEfficiency[(aseanMembers[j], aseanMembers[i])] = BalanceConfig.Trade.AseanTradeBonus;
                    }
                }
            }
        }

        // Weitere regionale Handelsgruppen (aus balance-config.json)
        var regionalGroups = BalanceConfig.Trade.RegionalGroups;

        foreach (var (_, group) in regionalGroups)
        {
            for (int i = 0; i < group.Countries.Length; i++)
            {
                for (int j = i + 1; j < group.Countries.Length; j++)
                {
                    var key1 = (group.Countries[i], group.Countries[j]);
                    var key2 = (group.Countries[j], group.Countries[i]);
                    // Nur setzen wenn noch nicht definiert (EU hat Vorrang)
                    if (!_tradeEfficiency.ContainsKey(key1))
                    {
                        _tradeEfficiency[key1] = group.Bonus;
                        _tradeEfficiency[key2] = group.Bonus;
                    }
                }
            }
        }

        Console.WriteLine($"[TradeManager] Handelseffizienz initialisiert für {_tradeEfficiency.Count / 2} Länderpaare");
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        _lastContext = context;

        switch (tickType)
        {
            case TickType.Daily:
                ProcessDailyTrade(context);
                break;

            case TickType.Weekly:
                UpdateMarketPrices(context);
                break;
        }
    }

    private void ProcessDailyTrade(GameContext context)
    {
        // Zuerst alle täglichen Handelswerte zurücksetzen
        foreach (var country in context.Countries.Values)
        {
            country.DailyExports = 0;
            country.DailyImports = 0;
        }

        // Reset der fehlgeschlagenen Handels-Benachrichtigungen für neuen Tag
        _failedTradesNotifiedToday.Clear();

        // Tatsaechliche Handelsmengen zuruecksetzen
        _dailyTradeVolume.Clear();

        // Inaktive Abkommen entfernen (z.B. durch Embargo deaktiviert)
        _tradeAgreements.RemoveAll(a => !a.IsActive);

        // Verarbeite alle aktiven Handelsabkommen
        foreach (var agreement in _tradeAgreements)
        {
            // Prüfe ob Embargo besteht
            if (HasEmbargo(agreement.ExporterId, agreement.ImporterId))
            {
                agreement.IsActive = false;
                continue;
            }

            // Führe Handel durch
            ExecuteTrade(agreement, context);
        }
    }

    private void ExecuteTrade(TradeAgreement agreement, GameContext context)
    {
        if (!context.Countries.TryGetValue(agreement.ExporterId, out var exporter))
            return;
        if (!context.Countries.TryGetValue(agreement.ImporterId, out var importer))
            return;

        string resourceName = Resource.GetGermanName(agreement.ResourceType);
        string exporterName = exporter.Name;
        string importerName = importer.Name;
        bool isPlayerImporter = importer.Id == context.Game.PlayerCountry?.Id;
        bool isPlayerExporter = exporter.Id == context.Game.PlayerCountry?.Id;

        // Prüfe ob Exporteur genug Ressourcen hat
        double available = exporter.GetResource(agreement.ResourceType);
        double tradeAmount = Math.Min(agreement.Amount, available);

        if (tradeAmount <= 0)
        {
            // Fehltage zaehlen - Handel pausiert, aber Abkommen bleibt bestehen
            _consecutiveFailDays.TryGetValue(agreement.Id, out int failDays);
            _consecutiveFailDays[agreement.Id] = failDays + 1;

            // Benachrichtigung bei Spielerbeteiligung (nur alle 30 Tage)
            if (failDays == 0 && !_failedTradesNotifiedToday.Contains(agreement.Id))
            {
                _failedTradesNotifiedToday.Add(agreement.Id);
                if (isPlayerImporter)
                {
                    _notificationManager?.AddNotification(
                        "Import pausiert",
                        $"{exporterName} kann aktuell keine {resourceName} liefern. Abkommen bleibt bestehen.",
                        NotificationType.Warning,
                        exporter.Id);
                }
            }

            // Nach 30 Fehltagen: Abkommen automatisch kuendigen
            if (failDays + 1 >= 30)
            {
                agreement.IsActive = false;
                _consecutiveFailDays.Remove(agreement.Id);

                if (isPlayerExporter)
                {
                    _notificationManager?.AddNotification(
                        "Export beendet",
                        $"Handelsabkommen mit {importerName} beendet: Seit 30 Tagen keine {resourceName} lieferbar.",
                        NotificationType.Warning,
                        importer.Id);
                }
                else if (isPlayerImporter)
                {
                    _notificationManager?.AddNotification(
                        "Import beendet",
                        $"Handelsabkommen mit {exporterName} beendet: Seit 30 Tagen keine {resourceName} geliefert.",
                        NotificationType.Warning,
                        exporter.Id);
                }
            }
            return;
        }

        // Handelseffizienz (EU-Bonus, regionale Boni)
        double efficiency = GetTradeEfficiency(agreement.ExporterId, agreement.ImporterId);

        // Tech-Bonus fuer Handelseffizienz (Spieler-Land)
        string? playerId = context.Game.PlayerCountry?.Id;
        if (playerId != null && (agreement.ExporterId == playerId || agreement.ImporterId == playerId))
        {
            var techManager = context.Game.GetSystem<TechTreeManager>();
            if (techManager != null)
            {
                double tradeBonus = techManager.GetEffect("trade_efficiency");
                efficiency *= (1.0 + tradeBonus);
            }
        }

        // Marktpreis sicher abrufen
        if (!context.GlobalMarket.TryGetValue(agreement.ResourceType, out var marketResource))
            return;

        double currentPrice = marketResource.CurrentPrice;
        if (currentPrice <= 0) return;

        // Berechne Preis - bei höherer Effizienz niedrigerer Preis (EU-Vorteil)
        double basePrice = currentPrice * tradeAmount;
        double price = basePrice / efficiency;

        // Prüfe ob Importeur genug Budget hat
        if (importer.Budget < price)
        {
            // Reduziere Handelsmenge auf das was sich Importeur leisten kann
            double affordableAmount = (importer.Budget * efficiency) / currentPrice;

            if (affordableAmount <= 0)
            {
                // Fehltage zaehlen - Handel pausiert bei leerem Budget
                _consecutiveFailDays.TryGetValue(agreement.Id, out int budgetFailDays);
                _consecutiveFailDays[agreement.Id] = budgetFailDays + 1;

                // Benachrichtigung bei Spielerbeteiligung (nur beim ersten Fehltag)
                if (budgetFailDays == 0)
                {
                    if (isPlayerImporter)
                    {
                        _notificationManager?.AddNotification(
                            "Import pausiert",
                            $"Nicht genug Budget fuer {resourceName} aus {exporterName}. Abkommen bleibt bestehen.",
                            NotificationType.Warning);
                    }
                }

                // Nach 30 Fehltagen: Abkommen kuendigen
                if (budgetFailDays + 1 >= 30)
                {
                    agreement.IsActive = false;
                    _consecutiveFailDays.Remove(agreement.Id);

                    if (isPlayerImporter)
                    {
                        _notificationManager?.AddNotification(
                            "Import beendet",
                            $"Import von {resourceName} aus {exporterName} beendet: Seit 30 Tagen nicht bezahlbar.",
                            NotificationType.Danger);
                    }
                    else if (isPlayerExporter)
                    {
                        _notificationManager?.AddNotification(
                            "Export beendet",
                            $"Export von {resourceName} an {importerName} beendet: {importerName} konnte 30 Tage nicht zahlen.",
                            NotificationType.Warning,
                            importer.Id);
                    }
                }
                return;
            }

            // Reduzierte Menge handeln
            tradeAmount = Math.Min(tradeAmount, affordableAmount);
            basePrice = currentPrice * tradeAmount;
            price = basePrice / efficiency;
        }

        // Führe Transaktion durch
        if (exporter.UseResource(agreement.ResourceType, tradeAmount))
        {
            importer.AddResource(agreement.ResourceType, tradeAmount);
            exporter.Budget += price;
            importer.Budget -= price;

            // Tracke Exports/Imports (täglich)
            exporter.DailyExports += price;
            importer.DailyImports += price;

            // Tatsaechliche Handelsmengen tracken (Export negativ, Import positiv)
            var expKey = (agreement.ExporterId, agreement.ResourceType);
            _dailyTradeVolume[expKey] = _dailyTradeVolume.GetValueOrDefault(expKey, 0) - tradeAmount;
            var impKey = (agreement.ImporterId, agreement.ResourceType);
            _dailyTradeVolume[impKey] = _dailyTradeVolume.GetValueOrDefault(impKey, 0) + tradeAmount;

            // Erfolgreicher Handel: Fehlzaehler zuruecksetzen
            _consecutiveFailDays.Remove(agreement.Id);
        }
    }

    /// <summary>
    /// Gibt die Handelseffizienz zwischen zwei Ländern zurück (1.0 = normal, >1.0 = Bonus)
    /// </summary>
    public double GetTradeEfficiency(string country1, string country2)
    {
        if (_tradeEfficiency.TryGetValue((country1, country2), out var efficiency))
            return efficiency;
        return 1.0; // Standard ohne Bonus
    }

    /// <summary>
    /// Gibt die Handelsabkommen für ein Land zurück (als Exporteur oder Importeur)
    /// </summary>
    public IEnumerable<TradeAgreement> GetTradeAgreementsForCountry(string countryId)
    {
        return _tradeAgreements.Where(a => a.IsActive &&
            (a.ExporterId == countryId || a.ImporterId == countryId));
    }

    private void UpdateMarketPrices(GameContext context)
    {
        // Berechne globales Angebot und Nachfrage
        foreach (var (resourceType, resource) in context.GlobalMarket)
        {
            double totalSupply = 0;
            double totalDemand = 0;

            foreach (var country in context.Countries.Values)
            {
                totalSupply += country.DailyProduction.TryGetValue(resourceType, out var prod) ? prod : 0;
                totalDemand += country.DailyConsumption.TryGetValue(resourceType, out var cons) ? cons : 0;
            }

            resource.GlobalSupply = totalSupply;
            resource.GlobalDemand = totalDemand;
            resource.UpdatePrice();
        }
    }

    /// <summary>
    /// Erstellt ein neues Handelsabkommen
    /// </summary>
    /// <summary>
    /// Berechnet den aktuellen Spieltag als fortlaufende Zahl (fuer Vertragslaufzeiten)
    /// </summary>
    public static int GetGameDay(GameContext context)
    {
        return context.Game.Year * 365 + (context.Game.Month - 1) * 30 + context.Game.Day;
    }

    /// <summary>
    /// Minimale Vertragslaufzeit in Spieltagen (6 Monate = 180 Tage)
    /// </summary>
    private const int MinContractDurationDays = 180;

    public TradeAgreement? CreateTradeAgreement(string exporterId, string importerId, ResourceType resource, double amount)
    {
        if (exporterId == importerId) return null;

        var agreement = new TradeAgreement
        {
            Id = Guid.NewGuid().ToString(),
            ExporterId = exporterId,
            ImporterId = importerId,
            ResourceType = resource,
            Amount = amount,
            IsActive = true,
            CreatedGameDay = _lastContext != null ? GetGameDay(_lastContext) : 0
        };
        _tradeAgreements.Add(agreement);
        return agreement;
    }

    /// <summary>
    /// Prueft ob ein Handelsabkommen die Mindestlaufzeit erreicht hat und gekuendigt werden darf
    /// </summary>
    public bool CanCancelAgreement(TradeAgreement agreement, GameContext context)
    {
        int currentDay = GetGameDay(context);
        return (currentDay - agreement.CreatedGameDay) >= MinContractDurationDays;
    }

    public bool HasEmbargo(string country1, string country2)
    {
        return _embargos.Contains((country1, country2)) || _embargos.Contains((country2, country1));
    }

    public IReadOnlyList<TradeAgreement> GetTradeAgreements() => _tradeAgreements.AsReadOnly();

    /// <summary>
    /// Gibt die tatsaechlich gehandelte Menge einer Ressource fuer ein Land zurueck.
    /// Positiv = Netto-Import, Negativ = Netto-Export.
    /// </summary>
    public double GetDailyTradeVolume(string countryId, ResourceType resourceType)
    {
        return _dailyTradeVolume.GetValueOrDefault((countryId, resourceType), 0);
    }

    /// <summary>
    /// Kuendigt ein Handelsabkommen
    /// </summary>
    public bool CancelTradeAgreement(string agreementId)
    {
        var agreement = _tradeAgreements.FirstOrDefault(a => a.Id == agreementId);
        if (agreement != null)
        {
            agreement.IsActive = false;
            _tradeAgreements.Remove(agreement);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Berechnet die erwarteten taeglichen Importkosten basierend auf aktiven Handelsabkommen.
    /// Beruecksichtigt verfuegbare Ressourcen beim Exporteur.
    /// </summary>
    public double GetExpectedDailyImports(string countryId, GameContext context)
    {
        double totalImports = 0;

        foreach (var agreement in _tradeAgreements)
        {
            if (!agreement.IsActive) continue;
            if (agreement.ImporterId != countryId) continue;

            // Berechne den Preis basierend auf Marktpreis und Handelseffizienz
            if (context.GlobalMarket.TryGetValue(agreement.ResourceType, out var resource))
            {
                // Pruefe wieviel der Exporteur tatsaechlich hat
                double actualAmount = agreement.Amount;
                if (context.Countries.TryGetValue(agreement.ExporterId, out var exporter))
                {
                    double available = exporter.GetResource(agreement.ResourceType);
                    actualAmount = Math.Min(agreement.Amount, available);
                }

                if (actualAmount <= 0) continue;

                double efficiency = GetTradeEfficiency(agreement.ExporterId, agreement.ImporterId);
                double price = (resource.CurrentPrice * actualAmount) / efficiency;
                totalImports += price;
            }
        }

        return totalImports;
    }

    /// <summary>
    /// Berechnet die erwarteten taeglichen Exporteinnahmen basierend auf aktiven Handelsabkommen.
    /// Beruecksichtigt verfuegbare Ressourcen beim Exporteur (Spieler).
    /// </summary>
    public double GetExpectedDailyExports(string countryId, GameContext context)
    {
        double totalExports = 0;

        foreach (var agreement in _tradeAgreements)
        {
            if (!agreement.IsActive) continue;
            if (agreement.ExporterId != countryId) continue;

            // Berechne den Preis basierend auf Marktpreis und Handelseffizienz
            if (context.GlobalMarket.TryGetValue(agreement.ResourceType, out var resource))
            {
                // Pruefe wieviel wir tatsaechlich exportieren koennen
                double actualAmount = agreement.Amount;
                if (context.Countries.TryGetValue(countryId, out var exporter))
                {
                    double available = exporter.GetResource(agreement.ResourceType);
                    actualAmount = Math.Min(agreement.Amount, available);
                }

                if (actualAmount <= 0) continue;

                double efficiency = GetTradeEfficiency(agreement.ExporterId, agreement.ImporterId);
                double price = (resource.CurrentPrice * actualAmount) / efficiency;
                totalExports += price;
            }
        }

        return totalExports;
    }

    /// <summary>
    /// Gibt Laender zurueck, die eine Ressource exportieren koennen (haben Ueberschuss)
    /// EU-Mitglieder sind immer als Partner verfuegbar (EU-Binnenmarkt)
    /// </summary>
    public List<(string CountryId, double Available)> GetPotentialExporters(ResourceType resource, string excludeCountryId, GameContext context)
    {
        var result = new List<(string, double)>();
        bool playerIsEU = _diplomacyManager?.GetCountryAlliances(excludeCountryId).Contains("EU") ?? false;

        foreach (var (id, country) in context.Countries)
        {
            if (id == excludeCountryId) continue;

            // Pruefe auf Embargo
            if (HasEmbargo(excludeCountryId, id)) continue;

            double stock = country.GetResource(resource);
            double production = country.DailyProduction.GetValueOrDefault(resource, 0);
            double consumption = country.DailyConsumption.GetValueOrDefault(resource, 0);
            double surplus = production - consumption;

            // EU-Binnenmarkt: EU-Mitglieder sind immer füreinander verfügbar
            bool isEUPartner = playerIsEU && (_diplomacyManager?.GetCountryAlliances(id).Contains("EU") ?? false);

            // Land muss Ressource haben (mindestens 1) oder Ueberschuss produzieren
            // ODER ist EU-Partner (immer verfügbar im Binnenmarkt)
            if (stock > 1 || surplus > 0 || isEUPartner)
            {
                double available = Math.Max(stock * 0.5, surplus * 30); // Max 50% Lager oder 30-Tage-Ueberschuss
                if (available <= 0 && isEUPartner)
                {
                    available = 1; // EU-Partner haben immer mind. minimale Verfügbarkeit
                }
                if (available > 0)
                {
                    result.Add((id, available));
                }
            }
        }
        return result.OrderByDescending(x => x.Item2).Take(30).ToList(); // Mehr Partner für EU
    }

    /// <summary>
    /// Gibt Laender zurueck, die eine Ressource importieren wollen (haben Bedarf).
    /// Bedarf basiert auf BIP (groessere Wirtschaft = mehr Importbedarf),
    /// tatsaechlichem Produktionsdefizit und Lagerbestand.
    /// </summary>
    public List<(string CountryId, double Demand)> GetPotentialImporters(ResourceType resource, string excludeCountryId, GameContext context)
    {
        var result = new List<(string, double)>();
        bool playerIsEU = _diplomacyManager?.GetCountryAlliances(excludeCountryId).Contains("EU") ?? false;

        // BIP-basierter Grundbedarf pro Ressourcentyp (pro 1 Mio GDP pro Tag)
        double gdpDemandFactor = resource switch
        {
            ResourceType.Oil => 0.0003,
            ResourceType.NaturalGas => 0.0002,
            ResourceType.Coal => 0.00015,
            ResourceType.Iron => 0.00025,
            ResourceType.Copper => 0.0001,
            ResourceType.Uranium => 0.00002,
            ResourceType.Food => 0.0004,
            ResourceType.Steel => 0.0002,
            ResourceType.Electronics => 0.00015,
            ResourceType.Machinery => 0.0001,
            ResourceType.ConsumerGoods => 0.0003,
            _ => 0.0001
        };

        foreach (var (id, country) in context.Countries)
        {
            if (id == excludeCountryId) continue;
            if (HasEmbargo(excludeCountryId, id)) continue;

            double consumption = country.DailyConsumption.GetValueOrDefault(resource, 0);
            double production = country.DailyProduction.GetValueOrDefault(resource, 0);
            double stock = country.GetResource(resource);
            double deficit = consumption - production;

            bool isEUPartner = playerIsEU && (_diplomacyManager?.GetCountryAlliances(id).Contains("EU") ?? false);

            // BIP-basierter Grundbedarf (groessere Wirtschaft importiert mehr)
            double gdpDemand = country.GDP * gdpDemandFactor;

            // Bedarf berechnen: Produktionsdefizit + BIP-Grundbedarf
            double demand = 0;
            if (deficit > 0)
            {
                // Akutes Defizit: tatsaechlicher Fehlbedarf + BIP-Basis
                demand = deficit + gdpDemand;
            }
            else if (consumption > 0 && stock < consumption * 30)
            {
                // Niedriger Lagerbestand: BIP-Basis + halber Verbrauch
                demand = gdpDemand + consumption * 0.5;
            }
            else if (consumption > 0 || isEUPartner)
            {
                // Allgemeiner Bedarf basierend auf BIP
                demand = gdpDemand;
            }

            // Lager-Abzug: Laender mit grossen Lagern haben weniger dringenden Bedarf
            if (stock > 0 && demand > 0)
            {
                double stockCover = stock / Math.Max(demand, 1);
                if (stockCover > 60) demand *= 0.3;       // 60+ Tage Lager: wenig Bedarf
                else if (stockCover > 30) demand *= 0.6;   // 30-60 Tage: moderater Bedarf
            }

            if (demand > 0)
            {
                result.Add((id, Math.Round(demand, 1)));
            }
        }
        return result.OrderByDescending(x => x.Item2).Take(30).ToList();
    }
}

/// <summary>
/// Repräsentiert ein Handelsabkommen zwischen zwei Ländern
/// </summary>
public class TradeAgreement
{
    public string Id { get; set; } = "";
    public string ExporterId { get; set; } = "";
    public string ImporterId { get; set; } = "";
    public ResourceType ResourceType { get; set; }
    public double Amount { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Erstellungstag in Spielzeit (Tage seit Spielstart, berechnet aus Year/Month/Day)
    /// Wird fuer Mindest-Vertragslaufzeit verwendet.
    /// </summary>
    public int CreatedGameDay { get; set; }
}
