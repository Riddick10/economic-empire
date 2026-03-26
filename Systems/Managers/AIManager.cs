using System.Text.Json;

using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// KI-System: Trifft monatlich Entscheidungen fuer alle Nicht-Spieler-Laender.
/// Gesteuert durch laenderspezifische Persoenlichkeitsprofile (ai-profiles.json).
/// </summary>
public class AIManager : GameSystemBase
{
    public override string Name => "AI";
    public override int Priority => 180; // Nach allen anderen Managern
    public override TickType[] SubscribedTicks => new[] { TickType.Daily, TickType.Monthly };

    private readonly Dictionary<string, AIProfile> _profiles = new();
    private AIProfile _defaultProfile = new();

    // Manager-Referenzen (gecacht bei Initialize)
    private ProductionManager? _production;
    private TradeManager? _trade;
    private MilitaryManager? _military;
    private DiplomacyManager? _diplomacy;

    // Zufallsgenerator
    private readonly Random _rng = new();

    // Letzter Handels-Kontext (fuer Vertragslaufzeit-Pruefung)
    private GameContext? _lastTradeContext;

    // Zaehlt Monate seit letztem Handels-Neuabschluss pro Land
    private readonly Dictionary<string, int> _monthsSinceLastTradeDeal = new();

    // Konstanten
    private const double MinGDPForAI = 5000;         // Kleinstaaten ueberspringen
    private const double FactoryCost = 1000;          // 1 Mrd pro Fabrikgruppe (10 Stueck)
    private const int MaxTradeAgreements = 5;         // Max Handelsabkommen pro Land
    private const double TradeAmountBase = 1.0;        // Basis-Handelsmenge
    private const double TradeAmountPerGDP = 0.0002;  // Zusatz pro BIP-Einheit
    private const double MaxDebtToGDP = 2.5;          // Max 250% Schulden/BIP (Japan-Niveau)

    private bool _initialRunDone;

    public override void Initialize(GameContext context)
    {
        _production = context.Game.GetSystem<ProductionManager>();
        _trade = context.Game.GetSystem<TradeManager>();
        _military = context.Game.GetSystem<MilitaryManager>();
        _diplomacy = context.Game.GetSystem<DiplomacyManager>();

        LoadProfiles();
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        // Erster Durchlauf: Einmalig beim ersten Daily-Tick (Tag 1)
        // damit Handelsrouten sofort sichtbar sind
        if (!_initialRunDone && tickType == TickType.Daily)
        {
            _initialRunDone = true;
            RunForAllAICountries(context);
            return;
        }

        // Regulaer: Nur bei Monthly-Tick
        if (tickType != TickType.Monthly)
            return;

        RunForAllAICountries(context);
    }

    private void RunForAllAICountries(GameContext context)
    {
        foreach (var (countryId, country) in context.Countries)
        {
            if (countryId == context.PlayerCountry?.Id)
                continue;

            if (country.GDP < MinGDPForAI)
                continue;

            ProcessCountry(countryId, country, context);
        }
    }

    private void ProcessCountry(string countryId, Country country, GameContext context)
    {
        var profile = GetProfile(countryId);

        // Kredit-Management zuerst (vor allen Ausgaben)
        ManageLoans(country, profile);

        // Budget-Gesundheit berechnen (Budget relativ zum BIP)
        double budgetRatio = country.GDP > 0 ? country.Budget / country.GDP : 0;

        // Mindest-Reserve: 2% des BIP nie unterschreiten
        double minReserve = country.GDP * 0.02;
        double availableBudget = Math.Max(0, country.Budget - minReserve);

        // Ausgaben-Anteil je nach Budget-Gesundheit skalieren
        // Bei budgetRatio >= 5%: volle Ausgaben
        // Bei budgetRatio < 5%: linear reduzieren bis 0
        double healthMultiplier = Math.Clamp(budgetRatio / 0.05, 0, 1);
        double monthBudget = availableBudget * profile.MaxBudgetSpendRatio * healthMultiplier;

        if (monthBudget <= 0)
        {
            // Auch bei leerem Budget: Handel aufräumen und Diplomatie
            DecideTrade(countryId, country, profile, context);
            DecideDiplomacy(countryId, profile, context);
            return;
        }

        double spent = 0;

        spent += DecideEconomy(countryId, country, profile, monthBudget - spent, context);
        spent += DecideTrade(countryId, country, profile, context);
        DecideMilitary(countryId, country, profile, monthBudget - spent, context);
        DecideDiplomacy(countryId, profile, context);
        DecideExpansion(countryId, country, profile, context);
    }

    // ========================================================================
    // Kredit-Management: Aufnehmen und Tilgen
    // ========================================================================

    private void ManageLoans(Country country, AIProfile profile)
    {
        if (country.GDP <= 0) return;

        double budgetRatio = country.Budget / country.GDP;
        double debtRatio = country.NationalDebt / country.GDP;
        double maxDebt = Math.Min(profile.MaxDebtRatio, MaxDebtToGDP);

        // --- 1) Routine-Defizitfinanzierung ---
        // Die meisten Laender finanzieren sich durch laufende Verschuldung.
        // Kredit aufnehmen wenn Budget unter 3% BIP und unter Schulden-Ziel.
        if (budgetRatio < 0.03 && debtRatio < maxDebt)
        {
            // Kredithoehe: 1% des BIP (sinnvolle Tranche)
            double loanAmount = country.GDP * 0.01;
            loanAmount = Math.Max(100, Math.Min(loanAmount, 50000));

            country.Budget += loanAmount;
            country.NationalDebt += loanAmount;
        }

        // --- 2) Notkredit bei drohendem Budget-Kollaps ---
        // Wenn Budget fast leer und Tagesaenderung negativ: groesserer Kredit
        if (budgetRatio < 0.01 && debtRatio < MaxDebtToGDP)
        {
            double dailyChange = country.CalculateDailyBudgetChange();
            if (dailyChange < 0)
            {
                // Notkredit: 2% BIP, um ~60 Tage Defizit zu decken
                double emergencyLoan = country.GDP * 0.02;
                emergencyLoan = Math.Max(200, Math.Min(emergencyLoan, 100000));

                country.Budget += emergencyLoan;
                country.NationalDebt += emergencyLoan;
            }
        }

        // --- 3) Schulden tilgen (konservativ, nur bei fiskal-gesunden Laendern) ---
        // Nur tilgen wenn Budget sehr gesund UND Schuldenquote ueber Ziel
        if (budgetRatio > 0.10 && country.NationalDebt > 0 && debtRatio > maxDebt * 0.8)
        {
            // Tilge 0.5% des BIP oder Rest der Schulden (vorsichtig)
            double repayAmount = Math.Min(country.GDP * 0.005, country.NationalDebt);
            repayAmount = Math.Min(repayAmount, country.Budget * 0.1); // Max 10% des Budgets

            if (repayAmount > 0)
            {
                country.Budget -= repayAmount;
                country.NationalDebt -= repayAmount;
            }
        }
    }

    // ========================================================================
    // Wirtschaft: Fabriken und Minen bauen
    // ========================================================================

    private double DecideEconomy(string countryId, Country country, AIProfile profile,
        double budgetLeft, GameContext context)
    {
        if (_rng.NextDouble() > profile.EconomyFocus)
            return 0;

        double spent = 0;

        // 1) Zivile Fabrik bauen
        if (budgetLeft >= FactoryCost && _production != null)
        {
            var province = GetRandomOwnedProvince(countryId, context);
            if (province != null)
            {
                country.Budget -= FactoryCost;
                _production.BuildFactory(countryId, FactoryType.Civilian);
                province.CivilianFactories += 10;
                spent += FactoryCost;
            }
        }

        // 2) Mine in Provinz ohne Mine bauen
        if (budgetLeft - spent > 0)
        {
            spent += TryBuildMine(countryId, country, budgetLeft - spent, context);
        }

        return spent;
    }

    private double TryBuildMine(string countryId, Country country, double budgetLeft, GameContext context)
    {
        var provinces = GetProvincesWithoutMines(countryId, context);
        if (provinces.Count == 0)
            return 0;

        // Zufaellige Provinz waehlen
        var province = provinces[_rng.Next(provinces.Count)];
        var mineType = ChooseBestMineType(country);

        double cost = Mine.GetBuildCost(mineType);
        if (budgetLeft < cost)
            return 0;

        province.Mines.Add(new Mine(mineType));
        country.Budget -= cost;
        return cost;
    }

    // ========================================================================
    // Handel: Defizite durch Importe decken
    // ========================================================================

    private double DecideTrade(string countryId, Country country, AIProfile profile,
        GameContext context)
    {
        _lastTradeContext = context;

        if (_trade == null || _diplomacy == null)
            return 0;

        // Bestehende Abkommen sammeln
        var existingAgreements = _trade.GetTradeAgreementsForCountry(countryId).ToList();

        // Nutzlose Abkommen kuendigen (Importe nicht mehr noetig, Exporte nicht mehr tragbar)
        CleanupObsoleteAgreements(countryId, country, existingAgreements);
        CleanupObsoleteExports(countryId, country, existingAgreements);

        // Bei schlechtem Budget: teure Abkommen kuendigen
        double dailyImportCost = _trade.GetExpectedDailyImports(countryId, context);
        double dailyExportIncome = _trade.GetExpectedDailyExports(countryId, context);
        double budgetRatio = country.GDP > 0 ? country.Budget / country.GDP : 0;

        if (budgetRatio < 0.01 && dailyImportCost > dailyExportIncome)
        {
            // Budget kritisch: teuerste Import-Abkommen kuendigen
            CancelMostExpensiveImport(countryId, existingAgreements, context);
            // Trotzdem versuchen zu exportieren um Geld zu verdienen
            DecideExport(countryId, country, profile, context);
            return 0;
        }

        // Neue Deals nur alle 3 Monate versuchen (Cleanup laeuft weiterhin jeden Monat)
        int monthCount = _monthsSinceLastTradeDeal.GetValueOrDefault(countryId, 3);
        _monthsSinceLastTradeDeal[countryId] = monthCount + 1;
        if (monthCount < 3)
            return 0; // Noch nicht Zeit fuer neue Deals, aber Cleanup ist bereits gelaufen
        _monthsSinceLastTradeDeal[countryId] = 0;

        // Export versuchen
        DecideExport(countryId, country, profile, context);

        if (_rng.NextDouble() > profile.TradeFocus)
            return 0;

        // Aktualisiere nach moeglicher Kuendigung
        existingAgreements = _trade.GetTradeAgreementsForCountry(countryId).ToList();
        if (existingAgreements.Count >= MaxTradeAgreements * 2) // Import + Export zusammen
            return 0;

        // Bereits importierte Ressourcen sammeln (keine Duplikate)
        var alreadyImporting = new HashSet<ResourceType>();
        foreach (var agr in existingAgreements)
        {
            if (agr.ImporterId == countryId)
                alreadyImporting.Add(agr.ResourceType);
        }

        // Groesstes Defizit finden, aber bereits importierte Ressourcen ausschliessen
        var deficit = FindBiggestDeficit(country, alreadyImporting);
        if (deficit == null)
            return 0;

        // Pruefen ob sich neues Abkommen langfristig leisten laesst
        // Importkosten sollten max 30% des taeglichen Budgeteinkommens sein
        double dailyBudgetIncome = country.GDP * 0.001; // ~0.1% GDP/Tag als Annaeherung
        if (dailyImportCost > dailyBudgetIncome * 0.3)
            return 0;

        // Budget muss fuer mindestens 3 Monate Importe reichen
        double budgetRatioForTrade = country.GDP > 0 ? country.Budget / country.GDP : 0;
        if (budgetRatioForTrade < 0.02)
            return 0; // Zu wenig Budget fuer langfristigen Import

        // Potenzielle Exporteure suchen
        var exporters = _trade.GetPotentialExporters(deficit.Value, countryId, context);
        if (exporters.Count == 0)
            return 0;

        // Handelsmenge nach BIP skalieren (groessere Wirtschaften handeln mehr)
        double tradeAmount = Math.Clamp(TradeAmountBase + country.GDP * TradeAmountPerGDP, TradeAmountBase, 20.0);

        // Besten Partner waehlen: muss langfristig liefern koennen
        string? partnerId = PickReliableTradePartner(countryId, exporters, deficit.Value, tradeAmount, context);
        if (partnerId == null)
            return 0;

        _trade.CreateTradeAgreement(partnerId, countryId, deficit.Value, tradeAmount);
        return 0;
    }

    /// <summary>
    /// Versucht Ressourcen-Ueberschuesse zu exportieren um Geld zu verdienen.
    /// Exportiert nur wenn: genuegend Vorrat, Produktion > Verbrauch, Abnehmer vorhanden.
    /// </summary>
    private void DecideExport(string countryId, Country country, AIProfile profile, GameContext context)
    {
        if (_trade == null || _diplomacy == null) return;

        var existingAgreements = _trade.GetTradeAgreementsForCountry(countryId).ToList();

        // Bereits exportierte Ressourcen zaehlen
        int exportCount = 0;
        var alreadyExporting = new HashSet<ResourceType>();
        foreach (var agr in existingAgreements)
        {
            if (agr.ExporterId == countryId)
            {
                alreadyExporting.Add(agr.ResourceType);
                exportCount++;
            }
        }

        // Max Export-Deals begrenzen
        if (exportCount >= MaxTradeAgreements) return;

        // Finde beste Export-Ressource: hoher Ueberschuss + grosses Lager
        ResourceType? bestResource = null;
        double bestSurplus = 0;

        foreach (var resource in _tradeableResources)
        {
            if (alreadyExporting.Contains(resource)) continue;

            double production = country.DailyProduction.GetValueOrDefault(resource, 0);
            double consumption = country.DailyConsumption.GetValueOrDefault(resource, 0);
            double stock = country.GetResource(resource);
            double surplus = production - consumption;

            // Nur exportieren wenn: Ueberschuss UND mehr als 30 Tage Vorrat
            if (surplus <= 0.1) continue;
            double daysOfStock = consumption > 0 ? stock / consumption : stock / Math.Max(surplus, 0.1);
            if (daysOfStock < 30) continue;

            // Nahrung nur exportieren wenn sehr viel Vorrat (60+ Tage)
            if (resource == ResourceType.Food && daysOfStock < 60) continue;

            // Score: Ueberschuss * Lagerbestand (groesserer Ueberschuss = besser)
            double score = surplus * Math.Min(daysOfStock, 120);
            if (score > bestSurplus)
            {
                bestSurplus = score;
                bestResource = resource;
            }
        }

        if (bestResource == null) return;

        // Potenzielle Importeure suchen
        var importers = _trade.GetPotentialImporters(bestResource.Value, countryId, context);
        if (importers.Count == 0) return;

        // Exportmenge: max 50% des taeglichen Ueberschusses
        double production2 = country.DailyProduction.GetValueOrDefault(bestResource.Value, 0);
        double consumption2 = country.DailyConsumption.GetValueOrDefault(bestResource.Value, 0);
        double exportAmount = Math.Max(1.0, (production2 - consumption2) * 0.5);
        exportAmount = Math.Min(exportAmount, 20.0); // Max 20 pro Deal

        // Besten Partner waehlen: muss langfristig zahlen koennen
        string? partnerId = PickReliableImportPartner(countryId, importers, bestResource.Value, exportAmount, context);
        if (partnerId == null) return;

        _trade.CreateTradeAgreement(countryId, partnerId, bestResource.Value, exportAmount);
    }

    /// <summary>
    /// Kuendigt Export-Abkommen wenn Vorrat zu niedrig wird
    /// </summary>
    private void CleanupObsoleteExports(string countryId, Country country, List<TradeAgreement> agreements)
    {
        foreach (var agr in agreements)
        {
            if (agr.ExporterId != countryId) continue;

            // Mindest-Vertragslaufzeit respektieren
            bool canCancel = _trade != null && _lastTradeContext != null &&
                             _trade.CanCancelAgreement(agr, _lastTradeContext);

            double production = country.DailyProduction.GetValueOrDefault(agr.ResourceType, 0);
            double consumption = country.DailyConsumption.GetValueOrDefault(agr.ResourceType, 0);
            double stock = country.GetResource(agr.ResourceType);
            double daysOfStock = consumption > 0 ? stock / consumption : stock / Math.Max(production, 1);

            // Notfall-Kuendigung: Vorrat unter 7 Tage (egal ob Vertrag laeuft)
            if (daysOfStock < 7)
            {
                _trade?.CancelTradeAgreement(agr.Id);
                continue;
            }

            // Normale Kuendigung: nur wenn Vertragslaufzeit abgelaufen
            if (!canCancel) continue;
            double surplus = production - consumption;
            if (surplus < 0 || daysOfStock < 15)
            {
                _trade?.CancelTradeAgreement(agr.Id);
            }
        }
    }

    /// <summary>
    /// Kuendigt das teuerste Import-Abkommen wenn Budget kritisch ist
    /// </summary>
    private void CancelMostExpensiveImport(string countryId, List<TradeAgreement> agreements, GameContext context)
    {
        string? worstId = null;
        double worstCost = 0;

        foreach (var agr in agreements)
        {
            if (agr.ImporterId != countryId) continue;

            // Nur kuendigbare Vertraege beruecksichtigen (Mindestlaufzeit abgelaufen)
            if (_trade != null && !_trade.CanCancelAgreement(agr, context))
                continue;

            if (context.GlobalMarket.TryGetValue(agr.ResourceType, out var res))
            {
                double cost = res.CurrentPrice * agr.Amount;
                if (cost > worstCost)
                {
                    worstCost = cost;
                    worstId = agr.Id;
                }
            }
        }

        if (worstId != null)
            _trade?.CancelTradeAgreement(worstId);
    }

    /// <summary>
    /// Kuendigt Import-Abkommen die nicht mehr noetig sind.
    /// Respektiert Mindest-Vertragslaufzeit (6 Monate), ausser bei Budget-Notfall.
    /// </summary>
    private void CleanupObsoleteAgreements(string countryId, Country country, List<TradeAgreement> agreements)
    {
        foreach (var agr in agreements)
        {
            if (agr.ImporterId != countryId) continue;

            double production = country.DailyProduction.GetValueOrDefault(agr.ResourceType, 0);
            double consumption = country.DailyConsumption.GetValueOrDefault(agr.ResourceType, 0);
            double stock = country.GetResource(agr.ResourceType);

            // Nur kuendigen wenn Vertragslaufzeit abgelaufen
            bool canCancel = _trade != null && _lastTradeContext != null &&
                             _trade.CanCancelAgreement(agr, _lastTradeContext);
            if (!canCancel) continue;

            // Kuendigen wenn: kein Defizit mehr UND grosser Vorrat (200+ statt 100)
            if (production >= consumption * 1.2 && stock > 200)
            {
                _trade?.CancelTradeAgreement(agr.Id);
            }
        }
    }

    // ========================================================================
    // Militaer: Einheiten rekrutieren
    // ========================================================================

    private void DecideMilitary(string countryId, Country country, AIProfile profile,
        double budgetLeft, GameContext context)
    {
        if (_military == null)
            return;

        bool atWar = _military.GetWars(countryId).Any();

        // Im Krieg: Truppen zur Grenze bewegen und angreifen
        if (atWar)
        {
            ManageWarOperations(countryId, context);
        }

        if (_rng.NextDouble() > profile.MilitaryFocus)
            return;

        if (budgetLeft <= 0)
            return;

        // Kein Militaer aufbauen wenn Budget unter 3% des BIP (ausser im Krieg)
        double budgetRatio = country.GDP > 0 ? country.Budget / country.GDP : 0;
        if (!atWar && budgetRatio < 0.03)
            return;
        int maxRecruits = atWar ? 2 : 1;

        for (int i = 0; i < maxRecruits; i++)
        {
            var province = GetRandomOwnedProvince(countryId, context);
            if (province == null)
                break;

            var unitType = ChooseUnitType(profile);

            // Pruefe ob genug Waffen vorhanden, sonst billigere Einheit waehlen
            double weaponsNeeded = MilitaryUnit.GetWeaponsCost(unitType);
            double weaponsAvailable = country.GetResource(ResourceType.Weapons);
            if (weaponsAvailable < weaponsNeeded)
            {
                // Versuche Infanterie (niedrigster Waffenbedarf: 50)
                unitType = UnitType.Infantry;
                weaponsNeeded = MilitaryUnit.GetWeaponsCost(unitType);
                if (weaponsAvailable < weaponsNeeded)
                    break; // Nicht genug Waffen fuer jede Einheit
            }

            _military.StartRecruitment(unitType, countryId, province.Id, context);
        }
    }

    /// <summary>
    /// AI-Kriegsfuehrung: Truppen zur Grenze bewegen und in feindliches Gebiet einmarschieren
    /// </summary>
    private void ManageWarOperations(string countryId, GameContext context)
    {
        if (_military == null || context.WorldMap == null) return;

        var wars = _military.GetWars(countryId).ToList();
        if (wars.Count == 0) return;

        var units = _military.GetUnits(countryId);
        if (units.Count == 0) return;

        // Fuer jeden aktiven Krieg
        foreach (var war in wars)
        {
            // Feinde bestimmen
            var enemies = war.Attackers.Contains(countryId) ? war.Defenders : war.Attackers;

            foreach (var enemyId in enemies)
            {
                // Phase 1: Bereite Einheiten an die Grenze verlegen
                var readyUnits = new List<MilitaryUnit>();
                foreach (var unit in units)
                {
                    if (unit.Status == UnitStatus.Ready)
                        readyUnits.Add(unit);
                }

                if (readyUnits.Count == 0) continue;

                // Grenzprovinzen zum Feind finden
                var borderProvinces = FindBorderProvinces(countryId, enemyId, context, 5);
                if (borderProvinces.Count == 0) continue;

                // Feindliche Provinzen in der Naehe finden (Angriffsziele)
                var enemyProvinces = new List<Province>();
                foreach (var province in context.WorldMap.Provinces.Values)
                {
                    if (province.CountryId == enemyId)
                        enemyProvinces.Add(province);
                }

                foreach (var unit in readyUnits)
                {
                    // Pruefen ob Einheit bereits an der Grenze steht
                    bool atBorder = false;
                    foreach (var bp in borderProvinces)
                    {
                        if (unit.ProvinceId == bp.Id) { atBorder = true; break; }
                    }

                    if (atBorder && enemyProvinces.Count > 0)
                    {
                        // An der Grenze: In feindliches Gebiet einmarschieren!
                        // Waehle die naechste feindliche Provinz
                        Province? nearestEnemy = null;
                        double nearestDist = double.MaxValue;

                        if (context.WorldMap.Provinces.TryGetValue(unit.ProvinceId, out var unitProv))
                        {
                            foreach (var ep in enemyProvinces)
                            {
                                double dx = unitProv.LabelPosition.X - ep.LabelPosition.X;
                                double dy = unitProv.LabelPosition.Y - ep.LabelPosition.Y;
                                double dist = dx * dx + dy * dy;
                                if (dist < nearestDist) { nearestDist = dist; nearestEnemy = ep; }
                            }
                        }

                        if (nearestEnemy != null)
                        {
                            int hours = _military.CalculateMovementHours(unit.ProvinceId, nearestEnemy.Id, context);
                            _military.MoveUnit(unit, nearestEnemy.Id, hours);
                        }
                    }
                    else if (!atBorder)
                    {
                        // Nicht an der Grenze: Zur Grenze bewegen
                        var target = borderProvinces[_rng.Next(borderProvinces.Count)];
                        int hours = _military.CalculateMovementHours(unit.ProvinceId, target.Id, context);
                        _military.MoveUnit(unit, target.Id, hours);
                    }
                }
            }
        }
    }

    // ========================================================================
    // Diplomatie: Beziehungen pflegen
    // ========================================================================

    private void DecideDiplomacy(string countryId, AIProfile profile, GameContext context)
    {
        if (_diplomacy == null)
            return;

        if (_rng.NextDouble() > profile.DiplomacyFocus)
            return;

        // Allianz-Mitglieder: Beziehung verbessern
        var alliances = _diplomacy.GetCountryAlliances(countryId);
        foreach (var alliance in alliances)
        {
            var members = _diplomacy.GetCountriesInAlliance(alliance);
            foreach (var memberId in members)
            {
                if (memberId != countryId)
                    _diplomacy.ModifyRelation(countryId, memberId, 2);
            }
        }

        // Kriegsgegner: Beziehung verschlechtern
        if (_military != null)
        {
            var wars = _military.GetWars(countryId);
            foreach (var war in wars)
            {
                var enemies = war.Attackers.Contains(countryId) ? war.Defenders : war.Attackers;
                foreach (var enemyId in enemies)
                    _diplomacy.ModifyRelation(countryId, enemyId, -5);
            }
        }
    }

    // ========================================================================
    // Expansion: Kriegserklaerungen (selten, mit vielen Sicherungen)
    // ========================================================================

    private void DecideExpansion(string countryId, Country country, AIProfile profile,
        GameContext context)
    {
        if (_military == null || _diplomacy == null)
            return;

        // Nur bei hohem Expansionsfokus
        if (profile.ExpansionFocus < 0.3)
            return;

        // Zufalls-Check: ExpansionFocus * 10% Chance pro Monat
        if (_rng.NextDouble() > profile.ExpansionFocus * 0.1)
            return;

        // Nicht wenn bereits im Krieg
        if (_military.GetWars(countryId).Any())
            return;

        // Budget-Reserve pruefen: mindestens 5% des BIP
        if (country.Budget < country.GDP * 0.05)
            return;

        // Ziel suchen: schlechteste Beziehung, aber nicht Spieler
        string? targetId = null;
        int worstRelation = 0;

        foreach (var (otherId, other) in context.Countries)
        {
            if (otherId == countryId || otherId == context.PlayerCountry?.Id)
                continue;
            if (other.GDP < MinGDPForAI)
                continue;

            int relation = _diplomacy.GetRelation(countryId, otherId);
            if (relation < -60 && relation < worstRelation)
            {
                worstRelation = relation;
                targetId = otherId;
            }
        }

        if (targetId == null)
            return;

        // Staerke-Verhaeltnis pruefen: eigene Staerke muss 1.5x Ziel sein
        var ownStrength = _military.GetMilitaryStrength(countryId);
        var targetStrength = _military.GetMilitaryStrength(targetId);

        if (ownStrength == null || targetStrength == null)
            return;

        double ownPower = ownStrength.ActivePersonnel + ownStrength.TankCount * 50;
        double targetPower = targetStrength.ActivePersonnel + targetStrength.TankCount * 50;

        if (targetPower > 0 && ownPower < targetPower * 1.5)
            return;

        _military.DeclareWar(countryId, targetId, "Eroberung", context);
    }

    // ========================================================================
    // Hilfsmethoden
    // ========================================================================

    private AIProfile GetProfile(string countryId)
    {
        return _profiles.TryGetValue(countryId, out var profile) ? profile : _defaultProfile;
    }

    private Province? GetRandomOwnedProvince(string countryId, GameContext context)
    {
        var worldMap = context.WorldMap;
        if (worldMap == null)
            return null;

        // Alle Provinzen des Landes sammeln
        var owned = new List<Province>();
        foreach (var province in worldMap.Provinces.Values)
        {
            if (province.CountryId == countryId)
                owned.Add(province);
        }

        if (owned.Count == 0)
            return null;

        return owned[_rng.Next(owned.Count)];
    }

    private List<Province> GetProvincesWithoutMines(string countryId, GameContext context)
    {
        var result = new List<Province>();
        var worldMap = context.WorldMap;
        if (worldMap == null)
            return result;

        foreach (var province in worldMap.Provinces.Values)
        {
            if (province.CountryId == countryId && province.Mines.Count == 0)
                result.Add(province);
        }

        return result;
    }

    private MineType ChooseBestMineType(Country country)
    {
        // Mine fuer groesstes Ressourcen-Defizit waehlen
        MineType bestType = MineType.IronMine; // Default
        double worstBalance = double.MaxValue;

        foreach (MineType type in Enum.GetValues<MineType>())
        {
            var resource = Mine.GetResourceType(type);
            double production = country.DailyProduction.GetValueOrDefault(resource, 0);
            double consumption = country.DailyConsumption.GetValueOrDefault(resource, 0);
            double balance = production - consumption;

            if (balance < worstBalance)
            {
                worstBalance = balance;
                bestType = type;
            }
        }

        return bestType;
    }

    // Strategische Rohstoffe die jedes Land haben sollte
    private static readonly ResourceType[] _tradeableResources = new[]
    {
        ResourceType.Oil, ResourceType.NaturalGas, ResourceType.Coal,
        ResourceType.Iron, ResourceType.Copper,
        ResourceType.Food, ResourceType.Steel, ResourceType.Electronics,
        ResourceType.Machinery, ResourceType.ConsumerGoods
    };

    private ResourceType? FindBiggestDeficit(Country country, HashSet<ResourceType>? exclude = null)
    {
        ResourceType? worstResource = null;
        double worstScore = 0;

        foreach (var resource in _tradeableResources)
        {
            // Bereits importierte Ressourcen ueberspringen
            if (exclude != null && exclude.Contains(resource))
                continue;

            double production = country.DailyProduction.GetValueOrDefault(resource, 0);
            double consumption = country.DailyConsumption.GetValueOrDefault(resource, 0);
            double stock = country.GetResource(resource);

            // Score: Verbrauchsdefizit + niedriger Vorrat
            double deficit = consumption - production;  // positiv = Defizit
            double stockScore = stock < 50 ? (50 - stock) * 0.1 : 0; // Bonus bei niedrigem Vorrat

            double score = deficit + stockScore;

            if (score > worstScore)
            {
                worstScore = score;
                worstResource = resource;
            }
        }

        return worstResource;
    }

    private string? PickBestTradePartner(string countryId, List<(string CountryId, double Available)> exporters, string? playerId)
    {
        if (_diplomacy == null || exporters.Count == 0)
            return null;

        string? bestId = null;
        int bestRelation = int.MinValue;

        foreach (var (exporterId, _) in exporters)
        {
            // Spieler-Land nie als automatischen Handelspartner waehlen
            if (exporterId == playerId)
                continue;

            int relation = _diplomacy.GetRelation(countryId, exporterId);
            if (relation > bestRelation)
            {
                bestRelation = relation;
                bestId = exporterId;
            }
        }

        return bestId;
    }

    /// <summary>
    /// Waehlt einen zuverlaessigen Export-Partner fuer Importe:
    /// Muss genuegend Ueberschuss haben um langfristig liefern zu koennen.
    /// </summary>
    private string? PickReliableTradePartner(string countryId, List<(string CountryId, double Available)> exporters,
        ResourceType resource, double tradeAmount, GameContext context)
    {
        if (_diplomacy == null || exporters.Count == 0)
            return null;

        string? bestId = null;
        double bestScore = double.MinValue;

        foreach (var (exporterId, available) in exporters)
        {
            // Spieler-Land nie als automatischen Handelspartner waehlen
            if (exporterId == context.PlayerCountry?.Id)
                continue;

            if (!context.Countries.TryGetValue(exporterId, out var exporter))
                continue;

            // Pruefen ob Exporteur langfristig liefern kann
            double prod = exporter.DailyProduction.GetValueOrDefault(resource, 0);
            double cons = exporter.DailyConsumption.GetValueOrDefault(resource, 0);
            double surplus = prod - cons;
            double stock = exporter.GetResource(resource);

            // Mindestens 60 Tage Vorrat NACH dem Export noetig
            double daysOfStockAfterExport = (cons + tradeAmount) > 0 ? stock / (cons + tradeAmount) : 999;
            if (daysOfStockAfterExport < 60) continue;

            // Ueberschuss muss Exportmenge abdecken
            if (surplus < tradeAmount * 0.8) continue;

            // Score: Beziehung + Verfuegbarkeit
            int relation = _diplomacy.GetRelation(countryId, exporterId);
            double score = relation + available * 0.5;

            if (score > bestScore)
            {
                bestScore = score;
                bestId = exporterId;
            }
        }

        return bestId;
    }

    /// <summary>
    /// Waehlt einen zuverlaessigen Import-Partner fuer Exporte:
    /// Muss genuegend Budget haben um langfristig zahlen zu koennen.
    /// </summary>
    private string? PickReliableImportPartner(string countryId, List<(string CountryId, double Demand)> importers,
        ResourceType resource, double tradeAmount, GameContext context)
    {
        if (_diplomacy == null || importers.Count == 0)
            return null;

        string? bestId = null;
        double bestScore = double.MinValue;

        foreach (var (importerId, demand) in importers)
        {
            if (importerId == context.PlayerCountry?.Id)
                continue;

            if (!context.Countries.TryGetValue(importerId, out var importer))
                continue;

            // Pruefen ob Importeur langfristig zahlen kann (Budget > 2% BIP)
            double budgetRatio = importer.GDP > 0 ? importer.Budget / importer.GDP : 0;
            if (budgetRatio < 0.02) continue;

            // Score: Beziehung + Bedarf + Budget-Gesundheit
            int relation = _diplomacy.GetRelation(countryId, importerId);
            double score = relation + demand * 0.3 + budgetRatio * 100;

            if (score > bestScore)
            {
                bestScore = score;
                bestId = importerId;
            }
        }

        return bestId;
    }

    private UnitType ChooseUnitType(AIProfile profile)
    {
        // Hoher Militaerfokus: Panzer/Mechanisiert bevorzugen
        if (profile.MilitaryFocus >= 0.7)
        {
            double roll = _rng.NextDouble();
            if (roll < 0.3) return UnitType.Tank;
            if (roll < 0.5) return UnitType.Mechanized;
            if (roll < 0.7) return UnitType.Artillery;
        }

        return UnitType.Infantry;
    }

    /// <summary>
    /// Findet eigene Provinzen die am naechsten an einem Feindland liegen (fuer AI-Truppenbewegung).
    /// </summary>
    private List<Province> FindBorderProvinces(string ownCountryId, string enemyCountryId, GameContext context, int maxCount = 5)
    {
        if (context.WorldMap == null) return new List<Province>();

        var ownProvinces = new List<Province>();
        var enemyProvinces = new List<Province>();

        foreach (var province in context.WorldMap.Provinces.Values)
        {
            if (province.CountryId == ownCountryId)
                ownProvinces.Add(province);
            else if (province.CountryId == enemyCountryId)
                enemyProvinces.Add(province);
        }

        if (ownProvinces.Count == 0 || enemyProvinces.Count == 0)
            return new List<Province>();

        var scored = new List<(Province Province, double MinDist)>();
        foreach (var own in ownProvinces)
        {
            double minDist = double.MaxValue;
            foreach (var enemy in enemyProvinces)
            {
                double dx = own.LabelPosition.X - enemy.LabelPosition.X;
                double dy = own.LabelPosition.Y - enemy.LabelPosition.Y;
                double dist = dx * dx + dy * dy;
                if (dist < minDist) minDist = dist;
            }
            scored.Add((own, minDist));
        }

        scored.Sort((a, b) => a.MinDist.CompareTo(b.MinDist));
        var result = new List<Province>();
        for (int i = 0; i < Math.Min(maxCount, scored.Count); i++)
            result.Add(scored[i].Province);

        return result;
    }

    // ========================================================================
    // Profil-Laden aus JSON
    // ========================================================================

    private void LoadProfiles()
    {
        string[] searchPaths =
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")
        };

        string? filePath = null;
        foreach (var basePath in searchPaths)
        {
            string testPath = Path.Combine(basePath, "Data", "ai-profiles.json");
            if (File.Exists(testPath))
            {
                filePath = testPath;
                break;
            }
        }

        if (filePath == null)
        {
            Console.WriteLine("[AIManager] ai-profiles.json nicht gefunden!");
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Default-Profil laden
            if (root.TryGetProperty("defaultProfile", out var defaultEl))
                _defaultProfile = ParseProfile("default", defaultEl);

            // Laender-Profile laden
            if (root.TryGetProperty("profiles", out var profilesEl))
            {
                foreach (var prop in profilesEl.EnumerateObject())
                {
                    var profile = ParseProfile(prop.Name, prop.Value);
                    _profiles[prop.Name] = profile;
                }
            }

            Console.WriteLine($"[AIManager] {_profiles.Count} Laender-Profile geladen.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AIManager] Fehler beim Laden: {ex.Message}");
        }
    }

    private static AIProfile ParseProfile(string countryId, JsonElement el)
    {
        return new AIProfile
        {
            CountryId = countryId,
            EconomyFocus = el.TryGetProperty("economyFocus", out var e) ? e.GetDouble() : 0.5,
            MilitaryFocus = el.TryGetProperty("militaryFocus", out var m) ? m.GetDouble() : 0.3,
            DiplomacyFocus = el.TryGetProperty("diplomacyFocus", out var d) ? d.GetDouble() : 0.5,
            ExpansionFocus = el.TryGetProperty("expansionFocus", out var x) ? x.GetDouble() : 0.1,
            TradeFocus = el.TryGetProperty("tradeFocus", out var t) ? t.GetDouble() : 0.5,
            MaxBudgetSpendRatio = el.TryGetProperty("maxBudgetSpendRatio", out var b) ? b.GetDouble() : 0.10,
            MaxDebtRatio = el.TryGetProperty("maxDebtRatio", out var dr) ? dr.GetDouble() : 0.6
        };
    }
}
