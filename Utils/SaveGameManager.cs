using System.Text.Json;
using System.Text.Json.Serialization;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;

namespace GrandStrategyGame;

/// <summary>
/// Verwaltet Spielstaende - Speichern und Laden
/// Unterstuetzt 3 Speicherplaetze mit benutzerdefinierten Namen
/// sowie einen automatischen Autosave-Slot (Slot 0)
/// </summary>
public static class SaveGameManager
{
    /// <summary>Slot-Nummer des Autosave-Slots</summary>
    public const int AutosaveSlot = 0;

    private static readonly string SaveDirectory;
    private static readonly JsonSerializerOptions JsonOptions;

    static SaveGameManager()
    {
        // Speicherverzeichnis im Data-Ordner
        string basePath = Data.CountryDataLoader.FindBasePath();
        SaveDirectory = Path.Combine(basePath, "Data", "Saves");

        // JSON-Optionen fuer huebsche Formatierung
        JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Stelle sicher dass Speicherverzeichnis existiert
        if (!Directory.Exists(SaveDirectory))
        {
            Directory.CreateDirectory(SaveDirectory);
        }
    }

    /// <summary>
    /// Gibt den Pfad zur Speicherdatei fuer einen Slot zurueck
    /// </summary>
    private static string GetSaveFilePath(int slot)
    {
        return slot == AutosaveSlot
            ? Path.Combine(SaveDirectory, "autosave.json")
            : Path.Combine(SaveDirectory, $"save_{slot}.json");
    }

    /// <summary>
    /// Gibt den Pfad zur Metadaten-Datei fuer einen Slot zurueck
    /// </summary>
    private static string GetMetadataFilePath(int slot)
    {
        return slot == AutosaveSlot
            ? Path.Combine(SaveDirectory, "autosave_meta.json")
            : Path.Combine(SaveDirectory, $"save_{slot}_meta.json");
    }

    /// <summary>
    /// Prueft ob eine Slot-Nummer gueltig ist (0 = Autosave, 1-3 = manuell)
    /// </summary>
    private static bool IsValidSlot(int slot) => slot >= AutosaveSlot && slot <= 3;

    /// <summary>
    /// Laedt die Metadaten eines Speicherslots (fuer Anzeige im Menue)
    /// </summary>
    public static SaveSlotInfo? GetSlotInfo(int slot)
    {
        string metaPath = GetMetadataFilePath(slot);
        if (!File.Exists(metaPath))
            return null;

        try
        {
            string json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<SaveSlotInfo>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gibt alle Speicherslot-Infos zurueck.
    /// Index 0-2 = manuelle Slots 1-3, Index 3 = Autosave.
    /// </summary>
    public static SaveSlotInfo?[] GetAllSlots()
    {
        return new[]
        {
            GetSlotInfo(1),
            GetSlotInfo(2),
            GetSlotInfo(3),
            GetSlotInfo(AutosaveSlot)
        };
    }

    /// <summary>
    /// Speichert das Spiel automatisch in den Autosave-Slot
    /// </summary>
    public static bool Autosave(Game game, WorldMap worldMap)
    {
        return SaveGame(game, worldMap, AutosaveSlot,
            $"Autosave - {game.PlayerCountry?.Name} - {game.GetDateString()}");
    }

    /// <summary>
    /// Speichert das aktuelle Spiel in einen Slot
    /// </summary>
    public static bool SaveGame(Game game, WorldMap worldMap, int slot, string? customName = null)
    {
        if (!IsValidSlot(slot)) return false;
        if (game.PlayerCountry == null) return false;

        try
        {
            // Erstelle Speicherdaten
            var saveData = CreateSaveData(game, worldMap);

            // Speichere Hauptdaten
            string savePath = GetSaveFilePath(slot);
            string json = JsonSerializer.Serialize(saveData, JsonOptions);
            File.WriteAllText(savePath, json);

            // Speichere Metadaten (fuer schnelle Anzeige)
            var metaInfo = new SaveSlotInfo
            {
                Slot = slot,
                Name = customName ?? $"{game.PlayerCountry.Name} - {game.GetDateString()}",
                CountryId = game.PlayerCountry.Id,
                CountryName = game.PlayerCountry.Name,
                GameDate = game.GetDateString(),
                Year = game.Year,
                Month = game.Month,
                Day = game.Day,
                SavedAt = DateTime.Now,
                Budget = game.PlayerCountry.Budget,
                Population = game.PlayerCountry.Population
            };

            string metaPath = GetMetadataFilePath(slot);
            string metaJson = JsonSerializer.Serialize(metaInfo, JsonOptions);
            File.WriteAllText(metaPath, metaJson);

            Console.WriteLine($"[SaveGameManager] Spiel gespeichert in Slot {slot}: {metaInfo.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveGameManager] Fehler beim Speichern: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Laedt ein Spiel aus einem Slot
    /// </summary>
    public static SaveGameData? LoadGame(int slot)
    {
        if (!IsValidSlot(slot)) return null;

        string savePath = GetSaveFilePath(slot);
        if (!File.Exists(savePath))
        {
            Console.WriteLine($"[SaveGameManager] Keine Speicherdatei in Slot {slot}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            var saveData = JsonSerializer.Deserialize<SaveGameData>(json, JsonOptions);
            Console.WriteLine($"[SaveGameManager] Spiel aus Slot {slot} geladen");
            return saveData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveGameManager] Fehler beim Laden: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loescht einen Speicherslot
    /// </summary>
    public static bool DeleteSlot(int slot)
    {
        if (!IsValidSlot(slot)) return false;

        try
        {
            string savePath = GetSaveFilePath(slot);
            string metaPath = GetMetadataFilePath(slot);

            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(metaPath)) File.Delete(metaPath);

            Console.WriteLine($"[SaveGameManager] Slot {slot} geloescht");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveGameManager] Fehler beim Loeschen: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    /// Erstellt SaveGameData aus aktuellem Spielzustand
    /// </summary>
    private static SaveGameData CreateSaveData(Game game, WorldMap worldMap)
    {
        var data = new SaveGameData
        {
            Version = 3,
            SavedAt = DateTime.Now,

            // Zeitdaten
            Year = game.Year,
            Month = game.Month,
            Day = game.Day,
            Hour = game.Hour,
            Minute = game.Minute,
            TotalDays = game.TotalDays,
            TotalHours = game.TotalHours,
            GameSpeed = game.GameSpeed,

            // Spieler
            PlayerCountryId = game.PlayerCountry?.Id ?? "",

            // Laender
            Countries = game.Countries.Values.Select(c => new CountrySaveData
            {
                Id = c.Id,
                Population = c.Population,
                PopulationGrowthRate = c.PopulationGrowthRate,
                UnemploymentRate = c.UnemploymentRate,
                EducationLevel = c.EducationLevel,
                GDP = c.GDP,
                GDPGrowthRate = c.GDPGrowthRate,
                Budget = c.Budget,
                NationalDebt = c.NationalDebt,
                TaxRate = c.TaxRate,
                Inflation = c.Inflation,
                SocialSpendingPercent = c.SocialSpendingPercent,
                MilitarySpendingPercent = c.MilitarySpendingPercent,
                InfrastructureSpendingPercent = c.InfrastructureSpendingPercent,
                EducationSpendingPercent = c.EducationSpendingPercent,
                HealthSpendingPercent = c.HealthSpendingPercent,
                AdministrationSpendingPercent = c.AdministrationSpendingPercent,
                Stockpile = c.Stockpile.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                DeficitMultiplier = c.DeficitMultiplier,
                MilitaryEquipment = c.MilitaryEquipment.Count > 0
                    ? new Dictionary<string, int>(c.MilitaryEquipment)
                    : null
            }).ToList(),

            // Ressourcen-Markt
            ResourcePrices = game.GlobalMarket.ToDictionary(
                kv => kv.Key.ToString(),
                kv => new ResourceSaveData
                {
                    CurrentPrice = kv.Value.CurrentPrice,
                    GlobalSupply = kv.Value.GlobalSupply,
                    GlobalDemand = kv.Value.GlobalDemand
                }),

            // Provinzdaten (Minen, Fabriken, Werften)
            Provinces = worldMap.Provinces.Values
                .Where(p => p.Mines.Count > 0 || p.CivilianFactories > 0 || p.MilitaryFactories > 0 || p.Dockyards > 0)
                .Select(p => new ProvinceSaveData
                {
                    Id = p.Id,
                    CivilianFactories = p.CivilianFactories,
                    MilitaryFactories = p.MilitaryFactories,
                    Dockyards = p.Dockyards,
                    Mines = p.Mines.Select(m => new MineSaveData
                    {
                        Type = m.Type.ToString(),
                        Level = m.Level,
                        ProductionPerDay = m.ProductionPerDay
                    }).ToList()
                }).ToList()
        };

        // Handelsdaten
        var tradeManager = game.GetSystem<TradeManager>();
        if (tradeManager != null)
        {
            data.TradeAgreements = tradeManager.GetTradeAgreements()
                .Where(a => a.IsActive)
                .Select(a => new TradeAgreementSaveData
                {
                    ExporterId = a.ExporterId,
                    ImporterId = a.ImporterId,
                    ResourceType = a.ResourceType.ToString(),
                    Amount = a.Amount,
                    CreatedGameDay = a.CreatedGameDay
                }).ToList();
        }

        // Diplomatie-Beziehungen
        var diplomacyManager = game.GetSystem<DiplomacyManager>();
        if (diplomacyManager != null)
        {
            data.Relations = new List<RelationSaveData>();
            foreach (var (key, value) in diplomacyManager.GetAllRelations())
            {
                data.Relations.Add(new RelationSaveData
                {
                    Country1 = key.Item1,
                    Country2 = key.Item2,
                    Value = value
                });
            }
        }

        // Militaereinheiten
        var militaryManager = game.GetSystem<MilitaryManager>();
        if (militaryManager != null)
        {
            data.MilitaryUnits = militaryManager.GetAllUnits()
                .Select(u => new MilitaryUnitSaveData
                {
                    Name = u.Name,
                    Type = u.Type.ToString(),
                    Status = u.Status.ToString(),
                    CountryId = u.CountryId,
                    ProvinceId = u.ProvinceId,
                    Manpower = u.Manpower,
                    MaxManpower = u.MaxManpower,
                    Organization = u.Organization,
                    Morale = u.Morale,
                    Experience = u.Experience,
                    Attack = u.Attack,
                    Defense = u.Defense,
                    SoftAttack = u.SoftAttack,
                    HardAttack = u.HardAttack,
                    RecruitmentDaysLeft = u.RecruitmentDaysLeft,
                    TotalRecruitmentDays = u.TotalRecruitmentDays,
                    TargetProvinceId = u.TargetProvinceId,
                    StartProvinceId = u.StartProvinceId,
                    MovementHoursLeft = u.MovementHoursLeft,
                    TotalMovementHours = u.TotalMovementHours,
                    VisualProgress = u.VisualProgress,
                    EngageTargetProvinceId = u.EngageTargetProvinceId
                }).ToList();
        }

        // Fabrik-Zuweisungen speichern
        var productionManager = game.GetSystem<ProductionManager>();
        if (productionManager != null && game.PlayerCountry != null)
        {
            var civAssign = productionManager.GetFactoryAssignments(game.PlayerCountry.Id);
            if (civAssign.Count > 0)
            {
                data.FactoryAssignments = new Dictionary<string, Dictionary<string, int>>();
                data.FactoryAssignments[game.PlayerCountry.Id] = new Dictionary<string, int>();
                foreach (var kv in civAssign)
                    data.FactoryAssignments[game.PlayerCountry.Id][kv.Key.ToString()] = kv.Value;
            }

            var milAssign = productionManager.GetMilitaryAssignments(game.PlayerCountry.Id);
            if (milAssign.Count > 0)
            {
                data.MilitaryAssignments = new Dictionary<string, Dictionary<string, int>>();
                data.MilitaryAssignments[game.PlayerCountry.Id] = new Dictionary<string, int>();
                foreach (var kv in milAssign)
                    data.MilitaryAssignments[game.PlayerCountry.Id][kv.Key.ToString()] = kv.Value;
            }
        }

        // Forschungsfortschritt
        var techManager = game.GetSystem<TechTreeManager>();
        if (techManager != null)
        {
            data.CurrentResearch = techManager.CurrentResearch;
            data.TechProgress = techManager.PlayerProgress
                .Where(kv => kv.Value.Status != TechStatus.Locked || kv.Value.ProgressDays > 0)
                .Select(kv => new TechProgressSaveData
                {
                    TechId = kv.Key,
                    Status = kv.Value.Status.ToString(),
                    ProgressDays = kv.Value.ProgressDays
                }).ToList();
        }

        // === Version 3+: Vollstaendige Zustandssicherung ===

        // Eroberte Provinzen: nur Provinzen speichern, deren Besitzer sich
        // gegenueber dem Kartenstart geaendert hat
        data.ProvinceOwners = new List<ProvinceOwnerSaveData>();
        foreach (var province in worldMap.Provinces.Values)
        {
            string? originalOwner = worldMap.GetOriginalOwner(province.Id);
            if (originalOwner != null && originalOwner != province.CountryId)
            {
                data.ProvinceOwners.Add(new ProvinceOwnerSaveData
                {
                    Id = province.Id,
                    OwnerId = province.CountryId
                });
            }
        }

        // Aktive Kriege + Kriegsmuedigkeit
        if (militaryManager != null)
        {
            data.Wars = militaryManager.GetAllWars()
                .Where(w => w.IsActive)
                .Select(w => new WarSaveData
                {
                    Id = w.Id,
                    Attackers = new List<string>(w.Attackers),
                    Defenders = new List<string>(w.Defenders),
                    WarGoal = w.WarGoal,
                    StartYear = w.StartYear
                }).ToList();

            data.WarExhaustion = new Dictionary<string, double>();
            foreach (var countryId in game.Countries.Keys)
            {
                var strength = militaryManager.GetMilitaryStrength(countryId);
                if (strength != null && strength.WarExhaustion > 0)
                    data.WarExhaustion[countryId] = strength.WarExhaustion;
            }
        }

        // Politischer Zustand (Stabilitaet, Zustimmung, Kriegsbereitschaft)
        var politicsManager = game.GetSystem<PoliticsManager>();
        if (politicsManager != null)
        {
            data.Politics = new List<PoliticsSaveData>();
            foreach (var countryId in game.Countries.Keys)
            {
                var politics = politicsManager.GetPolitics(countryId);
                if (politics == null) continue;
                data.Politics.Add(new PoliticsSaveData
                {
                    CountryId = countryId,
                    Stability = politics.Stability,
                    PublicSupport = politics.PublicSupport,
                    WarSupport = politics.WarSupport
                });
            }
        }

        // Demografie (Geburten-/Sterberate, Urbanisierung, Alphabetisierung)
        var populationManager = game.GetSystem<PopulationManager>();
        if (populationManager != null)
        {
            data.Demographics = populationManager.GetAllDemographics()
                .Select(kv => new DemographicsSaveData
                {
                    CountryId = kv.Key,
                    BirthRate = kv.Value.BirthRate,
                    DeathRate = kv.Value.DeathRate,
                    WorkingAgeRatio = kv.Value.WorkingAgeRatio,
                    UrbanizationRate = kv.Value.UrbanizationRate,
                    LiteracyRate = kv.Value.LiteracyRate
                }).ToList();
        }

        // Ressourcen-Diagramm-Historie
        var economyManager = game.GetSystem<EconomyManager>();
        if (economyManager != null)
        {
            data.MoneyHistory = economyManager.MoneyHistory
                .Select(s => new MoneySnapshotSaveData
                {
                    TotalDays = s.TotalDays,
                    Year = s.Year,
                    Month = s.Month,
                    Day = s.Day,
                    ResourceTotals = s.ResourceTotals.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
                }).ToList();
        }

        // Abgeschlossene Missionen
        var missionManager = game.GetSystem<MissionManager>();
        if (missionManager != null)
        {
            data.CompletedMissions = missionManager.CompletedMissionIds.ToList();
        }

        return data;
    }

    /// <summary>
    /// Wendet geladene Daten auf ein Spiel an
    /// </summary>
    public static bool ApplySaveData(SaveGameData saveData, Game game, WorldMap worldMap)
    {
        if (saveData == null) return false;

        try
        {
            // ID-Zaehler zuruecksetzen um Kollisionen zu vermeiden
            MilitaryUnit.ResetIdCounter();

            // Spielzeit wiederherstellen
            game.LoadState(
                saveData.Year, saveData.Month, saveData.Day,
                saveData.Hour, saveData.Minute,
                saveData.TotalDays, saveData.TotalHours,
                saveData.GameSpeed);

            // Wende Laenderdaten an
            foreach (var countrySave in saveData.Countries)
            {
                if (game.Countries.TryGetValue(countrySave.Id, out var country))
                {
                    country.Population = countrySave.Population;
                    country.PopulationGrowthRate = countrySave.PopulationGrowthRate;
                    country.UnemploymentRate = countrySave.UnemploymentRate;
                    country.EducationLevel = countrySave.EducationLevel;
                    country.GDP = countrySave.GDP;
                    country.GDPGrowthRate = countrySave.GDPGrowthRate;
                    country.Budget = countrySave.Budget;
                    country.NationalDebt = countrySave.NationalDebt;
                    country.TaxRate = countrySave.TaxRate;
                    country.Inflation = countrySave.Inflation;
                    country.SocialSpendingPercent = countrySave.SocialSpendingPercent;
                    country.MilitarySpendingPercent = countrySave.MilitarySpendingPercent;
                    country.InfrastructureSpendingPercent = countrySave.InfrastructureSpendingPercent;
                    country.EducationSpendingPercent = countrySave.EducationSpendingPercent;
                    country.HealthSpendingPercent = countrySave.HealthSpendingPercent;
                    country.AdministrationSpendingPercent = countrySave.AdministrationSpendingPercent;
                    country.DeficitMultiplier = countrySave.DeficitMultiplier;

                    // Stockpile wiederherstellen
                    country.Stockpile.Clear();
                    foreach (var (typeStr, amount) in countrySave.Stockpile)
                    {
                        if (Enum.TryParse<ResourceType>(typeStr, out var type))
                        {
                            country.Stockpile[type] = amount;
                        }
                    }

                    // Militaer-Equipment wiederherstellen (Version 3+)
                    if (countrySave.MilitaryEquipment != null)
                    {
                        country.MilitaryEquipment.Clear();
                        foreach (var (equipment, amount) in countrySave.MilitaryEquipment)
                            country.MilitaryEquipment[equipment] = amount;
                    }
                }
            }

            // Eroberte Provinzen wiederherstellen (Version 3+).
            // WICHTIG: Original-Besitzer-Cache VORHER initialisieren, damit er den
            // GeoJSON-Kartenstart widerspiegelt und Eroberungen korrekt erkannt werden.
            worldMap.EnsureOriginalOwnersInitialized();

            // Provinzen auf Kartenstart zuruecksetzen, damit kein Zustand aus der
            // laufenden Session (Eroberungen, gebaute Fabriken/Minen) in den
            // geladenen Spielstand leckt. Der Spielstand enthaelt alle Provinzen
            // mit Fabriken/Minen und alle Besitzerwechsel - die Wiederherstellung
            // unten stellt daher den exakten Save-Zustand her.
            foreach (var province in worldMap.Provinces.Values)
            {
                string? originalOwner = worldMap.GetOriginalOwner(province.Id);
                if (originalOwner != null)
                    province.CountryId = originalOwner;

                province.CivilianFactories = 0;
                province.MilitaryFactories = 0;
                province.Dockyards = 0;
                province.Mines.Clear();
            }

            if (saveData.ProvinceOwners != null)
            {
                foreach (var ownerSave in saveData.ProvinceOwners)
                {
                    if (worldMap.Provinces.TryGetValue(ownerSave.Id, out var province))
                    {
                        province.CountryId = ownerSave.OwnerId;
                    }
                }
            }

            // Wende Ressourcenpreise an
            foreach (var (typeStr, resourceSave) in saveData.ResourcePrices)
            {
                if (Enum.TryParse<ResourceType>(typeStr, out var type) &&
                    game.GlobalMarket.TryGetValue(type, out var resource))
                {
                    resource.CurrentPrice = resourceSave.CurrentPrice;
                    resource.GlobalSupply = resourceSave.GlobalSupply;
                    resource.GlobalDemand = resourceSave.GlobalDemand;
                }
            }

            // Wende Provinzdaten an
            foreach (var provinceSave in saveData.Provinces)
            {
                if (worldMap.Provinces.TryGetValue(provinceSave.Id, out var province))
                {
                    province.CivilianFactories = provinceSave.CivilianFactories;
                    province.MilitaryFactories = provinceSave.MilitaryFactories;
                    province.Dockyards = provinceSave.Dockyards;

                    // Minen wiederherstellen
                    province.Mines.Clear();
                    foreach (var mineSave in provinceSave.Mines)
                    {
                        if (Enum.TryParse<MineType>(mineSave.Type, out var mineType))
                        {
                            // ProductionPerDay bewusst NICHT aus dem Save uebernehmen:
                            // Der Konstruktor setzt den aktuellen Balance-Basiswert,
                            // damit Balance-Aenderungen auch alte Spielstaende erreichen.
                            var mine = new Mine(mineType)
                            {
                                Level = mineSave.Level
                            };
                            province.Mines.Add(mine);
                        }
                    }
                }
            }

            // Fabriken aus Provinzen in ProductionManager synchronisieren
            var productionManager = game.GetSystem<ProductionManager>();
            var gameContext = game.GetGameContext();
            if (gameContext != null)
                productionManager?.ResyncFromProvinces(gameContext);

            // Handelsdaten wiederherstellen
            var tradeManager = game.GetSystem<TradeManager>();
            if (tradeManager != null && saveData.TradeAgreements != null)
            {
                foreach (var agreement in saveData.TradeAgreements)
                {
                    if (Enum.TryParse<ResourceType>(agreement.ResourceType, out var type))
                    {
                        var restored = tradeManager.CreateTradeAgreement(
                            agreement.ExporterId,
                            agreement.ImporterId,
                            type,
                            agreement.Amount);

                        // Erstellungstag uebernehmen, sonst gilt die
                        // Mindest-Vertragslaufzeit nach dem Laden neu
                        if (restored != null)
                            restored.CreatedGameDay = agreement.CreatedGameDay;
                    }
                }
            }

            // Diplomatie-Beziehungen wiederherstellen
            var diplomacyManager = game.GetSystem<DiplomacyManager>();
            if (diplomacyManager != null && saveData.Relations != null)
            {
                foreach (var relation in saveData.Relations)
                {
                    diplomacyManager.SetRelation(relation.Country1, relation.Country2, relation.Value);
                }
            }

            // Militaereinheiten wiederherstellen
            var militaryManager = game.GetSystem<MilitaryManager>();
            if (militaryManager != null && saveData.MilitaryUnits != null)
            {
                militaryManager.ClearAllUnits();

                foreach (var unitSave in saveData.MilitaryUnits)
                {
                    if (Enum.TryParse<UnitType>(unitSave.Type, out var unitType) &&
                        Enum.TryParse<UnitStatus>(unitSave.Status, out var unitStatus))
                    {
                        var unit = new MilitaryUnit(unitType, unitSave.CountryId, unitSave.ProvinceId)
                        {
                            Name = unitSave.Name,
                            Status = unitStatus,
                            Manpower = unitSave.Manpower,
                            MaxManpower = unitSave.MaxManpower,
                            Organization = unitSave.Organization,
                            Morale = unitSave.Morale,
                            Experience = unitSave.Experience,
                            Attack = unitSave.Attack,
                            Defense = unitSave.Defense,
                            SoftAttack = unitSave.SoftAttack,
                            HardAttack = unitSave.HardAttack,
                            RecruitmentDaysLeft = unitSave.RecruitmentDaysLeft,
                            TotalRecruitmentDays = unitSave.TotalRecruitmentDays
                        };

                        // Bewegungsdaten wiederherstellen
                        if (unitStatus == UnitStatus.Moving && unitSave.TargetProvinceId != null)
                        {
                            unit.TargetProvinceId = unitSave.TargetProvinceId;
                            unit.StartProvinceId = unitSave.StartProvinceId;
                            unit.MovementHoursLeft = unitSave.MovementHoursLeft;
                            unit.TotalMovementHours = unitSave.TotalMovementHours;
                            unit.VisualProgress = unitSave.VisualProgress;
                        }

                        // Engaging-Daten wiederherstellen
                        if (unitStatus == UnitStatus.Engaging && unitSave.EngageTargetProvinceId != null)
                        {
                            unit.EngageTargetProvinceId = unitSave.EngageTargetProvinceId;
                        }

                        militaryManager.AddLoadedUnit(unit);
                    }
                }
            }

            // Forschungsfortschritt wiederherstellen
            var techManager = game.GetSystem<TechTreeManager>();
            if (techManager != null && saveData.TechProgress != null)
            {
                var progressDict = new Dictionary<string, TechProgress>();
                foreach (var tp in saveData.TechProgress)
                {
                    if (Enum.TryParse<TechStatus>(tp.Status, out var status))
                    {
                        progressDict[tp.TechId] = new TechProgress
                        {
                            TechId = tp.TechId,
                            Status = status,
                            ProgressDays = tp.ProgressDays
                        };
                    }
                }
                techManager.RestoreProgress(progressDict, saveData.CurrentResearch);
            }

            // Fabrik-Zuweisungen wiederherstellen
            if (productionManager != null)
            {
                if (saveData.FactoryAssignments != null)
                {
                    foreach (var (countryId, assignments) in saveData.FactoryAssignments)
                        foreach (var (resTypeStr, count) in assignments)
                            if (Enum.TryParse<ResourceType>(resTypeStr, out var resType))
                                productionManager.SetFactoryAssignment(countryId, resType, count);
                }
                if (saveData.MilitaryAssignments != null)
                {
                    foreach (var (countryId, assignments) in saveData.MilitaryAssignments)
                        foreach (var (resTypeStr, count) in assignments)
                            if (Enum.TryParse<ResourceType>(resTypeStr, out var resType))
                                productionManager.SetFactoryAssignment(countryId, resType, count);
                }
            }

            // === Version 3+: Vollstaendige Zustandswiederherstellung ===

            // Aktive Kriege wiederherstellen
            if (militaryManager != null && saveData.Wars != null)
            {
                militaryManager.ClearAllWars();
                foreach (var warSave in saveData.Wars)
                {
                    militaryManager.RestoreWar(new War
                    {
                        Id = warSave.Id,
                        Attackers = new List<string>(warSave.Attackers),
                        Defenders = new List<string>(warSave.Defenders),
                        WarGoal = warSave.WarGoal,
                        StartYear = warSave.StartYear,
                        IsActive = true
                    });
                }

                // Kriegsmuedigkeit wiederherstellen
                if (saveData.WarExhaustion != null)
                {
                    foreach (var (countryId, exhaustion) in saveData.WarExhaustion)
                        militaryManager.RestoreWarExhaustion(countryId, exhaustion);
                }
            }

            // Politischen Zustand wiederherstellen
            var politicsManager = game.GetSystem<PoliticsManager>();
            if (politicsManager != null && saveData.Politics != null)
            {
                foreach (var politicsSave in saveData.Politics)
                {
                    var politics = politicsManager.GetPolitics(politicsSave.CountryId);
                    if (politics == null) continue;
                    politics.Stability = politicsSave.Stability;
                    politics.PublicSupport = politicsSave.PublicSupport;
                    politics.WarSupport = politicsSave.WarSupport;
                }
            }

            // Demografie wiederherstellen
            var populationManager = game.GetSystem<PopulationManager>();
            if (populationManager != null && saveData.Demographics != null)
            {
                foreach (var demoSave in saveData.Demographics)
                {
                    // Bevoelkerungszahl kommt aus den Laenderdaten (bereits geladen)
                    long population = game.Countries.TryGetValue(demoSave.CountryId, out var c)
                        ? c.Population
                        : 0;

                    populationManager.RestoreDemographics(demoSave.CountryId, new Demographics
                    {
                        TotalPopulation = population,
                        BirthRate = demoSave.BirthRate,
                        DeathRate = demoSave.DeathRate,
                        WorkingAgeRatio = demoSave.WorkingAgeRatio,
                        UrbanizationRate = demoSave.UrbanizationRate,
                        LiteracyRate = demoSave.LiteracyRate
                    });
                }
            }

            // Ressourcen-Diagramm-Historie wiederherstellen
            var economyManager = game.GetSystem<EconomyManager>();
            if (economyManager != null && saveData.MoneyHistory != null)
            {
                var history = new List<MoneySnapshot>();
                foreach (var snapSave in saveData.MoneyHistory)
                {
                    var snapshot = new MoneySnapshot
                    {
                        TotalDays = snapSave.TotalDays,
                        Year = snapSave.Year,
                        Month = snapSave.Month,
                        Day = snapSave.Day,
                        ResourceTotals = new Dictionary<ResourceType, double>()
                    };
                    foreach (var (typeStr, amount) in snapSave.ResourceTotals)
                    {
                        if (Enum.TryParse<ResourceType>(typeStr, out var type))
                            snapshot.ResourceTotals[type] = amount;
                    }
                    history.Add(snapshot);
                }
                economyManager.RestoreMoneyHistory(history);
            }

            // Abgeschlossene Missionen wiederherstellen
            var missionManager = game.GetSystem<MissionManager>();
            if (missionManager != null && saveData.CompletedMissions != null)
            {
                missionManager.RestoreCompleted(saveData.CompletedMissions);
            }

            // Tageswerte neu berechnen: DailyProduction/DailyConsumption stehen
            // nicht im Save und waeren sonst bis zum ersten Tageswechsel leer
            // (Logistik-Panel wuerde 0 Produktion anzeigen)
            if (productionManager != null && gameContext != null)
                productionManager.PrimeDailyEstimates(gameContext);

            Console.WriteLine($"[SaveGameManager] Spielstand angewendet: {saveData.PlayerCountryId}, {saveData.Year}-{saveData.Month}-{saveData.Day} {saveData.Hour:D2}:{saveData.Minute:D2} (Version {saveData.Version})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveGameManager] Fehler beim Anwenden: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Metadaten fuer einen Speicherslot (fuer schnelle Anzeige im Menue)
/// </summary>
public class SaveSlotInfo
{
    public int Slot { get; set; }
    public string Name { get; set; } = "";
    public string CountryId { get; set; } = "";
    public string CountryName { get; set; } = "";
    public string GameDate { get; set; } = "";
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public DateTime SavedAt { get; set; }
    public double Budget { get; set; }
    public long Population { get; set; }
}

/// <summary>
/// Komplette Speicherdaten
/// </summary>
public class SaveGameData
{
    public int Version { get; set; } = 2;
    public DateTime SavedAt { get; set; }

    // Zeitdaten
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public int TotalDays { get; set; }
    public int TotalHours { get; set; }
    public int GameSpeed { get; set; }

    // Spieler
    public string PlayerCountryId { get; set; } = "";

    // Laenderdaten
    public List<CountrySaveData> Countries { get; set; } = new();

    // Ressourcen-Markt
    public Dictionary<string, ResourceSaveData> ResourcePrices { get; set; } = new();

    // Provinzdaten
    public List<ProvinceSaveData> Provinces { get; set; } = new();

    // Handel
    public List<TradeAgreementSaveData>? TradeAgreements { get; set; }

    // Diplomatie
    public List<RelationSaveData>? Relations { get; set; }

    // Militaereinheiten
    public List<MilitaryUnitSaveData>? MilitaryUnits { get; set; }

    // Fabrik-Zuweisungen
    public Dictionary<string, Dictionary<string, int>>? FactoryAssignments { get; set; }
    public Dictionary<string, Dictionary<string, int>>? MilitaryAssignments { get; set; }

    // Forschungsfortschritt
    public List<TechProgressSaveData>? TechProgress { get; set; }
    public string? CurrentResearch { get; set; }

    // === Version 3+: Vollstaendige Zustandssicherung ===

    // Eroberte Provinzen (Besitzerwechsel gegenueber dem Kartenstart)
    public List<ProvinceOwnerSaveData>? ProvinceOwners { get; set; }

    // Aktive Kriege
    public List<WarSaveData>? Wars { get; set; }

    // Kriegsmuedigkeit pro Land
    public Dictionary<string, double>? WarExhaustion { get; set; }

    // Politischer Zustand pro Land (Stabilitaet etc.)
    public List<PoliticsSaveData>? Politics { get; set; }

    // Demografie pro Land (Geburtenrate, Urbanisierung etc.)
    public List<DemographicsSaveData>? Demographics { get; set; }

    // Ressourcen-Diagramm-Historie
    public List<MoneySnapshotSaveData>? MoneyHistory { get; set; }

    // Abgeschlossene Missionen
    public List<string>? CompletedMissions { get; set; }
}

public class CountrySaveData
{
    public string Id { get; set; } = "";
    public long Population { get; set; }
    public double PopulationGrowthRate { get; set; }
    public double UnemploymentRate { get; set; }
    public double EducationLevel { get; set; }
    public double GDP { get; set; }
    public double GDPGrowthRate { get; set; }
    public double Budget { get; set; }
    public double NationalDebt { get; set; }
    public double TaxRate { get; set; }
    public double Inflation { get; set; }
    public double SocialSpendingPercent { get; set; }
    public double MilitarySpendingPercent { get; set; }
    public double InfrastructureSpendingPercent { get; set; }
    public double EducationSpendingPercent { get; set; }
    public double HealthSpendingPercent { get; set; }
    public double AdministrationSpendingPercent { get; set; }
    public Dictionary<string, double> Stockpile { get; set; } = new();

    // Version 3+: Haushaltspolitik (Default fuer alte Spielstaende: 1.06)
    public double DeficitMultiplier { get; set; } = 1.06;

    // Version 3+: Militaer-Equipment (Waffen, Munition etc.)
    public Dictionary<string, int>? MilitaryEquipment { get; set; }
}

public class ResourceSaveData
{
    public double CurrentPrice { get; set; }
    public double GlobalSupply { get; set; }
    public double GlobalDemand { get; set; }
}

public class ProvinceSaveData
{
    public string Id { get; set; } = "";
    public int CivilianFactories { get; set; }
    public int MilitaryFactories { get; set; }
    public int Dockyards { get; set; }
    public List<MineSaveData> Mines { get; set; } = new();
}

public class MineSaveData
{
    public string Type { get; set; } = "";
    public int Level { get; set; }
    public double ProductionPerDay { get; set; }
}

public class TradeAgreementSaveData
{
    public string ExporterId { get; set; } = "";
    public string ImporterId { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public double Amount { get; set; }
    public int CreatedGameDay { get; set; }
}

public class RelationSaveData
{
    public string Country1 { get; set; } = "";
    public string Country2 { get; set; } = "";
    public int Value { get; set; }
}

public class MilitaryUnitSaveData
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public string CountryId { get; set; } = "";
    public string ProvinceId { get; set; } = "";
    public int Manpower { get; set; }
    public int MaxManpower { get; set; }
    public float Organization { get; set; }
    public float Morale { get; set; }
    public float Experience { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SoftAttack { get; set; }
    public int HardAttack { get; set; }
    public int RecruitmentDaysLeft { get; set; }
    public int TotalRecruitmentDays { get; set; }
    public string? TargetProvinceId { get; set; }
    public string? StartProvinceId { get; set; }
    public int MovementHoursLeft { get; set; }
    public int TotalMovementHours { get; set; }
    public string? EngageTargetProvinceId { get; set; }
    public float VisualProgress { get; set; }
}

public class TechProgressSaveData
{
    public string TechId { get; set; } = "";
    public string Status { get; set; } = "";
    public int ProgressDays { get; set; }
}

/// <summary>
/// Provinz-Besitzerwechsel (Eroberung) - nur gespeichert wenn abweichend vom Kartenstart
/// </summary>
public class ProvinceOwnerSaveData
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
}

/// <summary>
/// Ein aktiver Krieg
/// </summary>
public class WarSaveData
{
    public string Id { get; set; } = "";
    public List<string> Attackers { get; set; } = new();
    public List<string> Defenders { get; set; } = new();
    public string WarGoal { get; set; } = "";
    public int StartYear { get; set; }
}

/// <summary>
/// Politischer Zustand eines Landes (nur die sich aendernden Werte)
/// </summary>
public class PoliticsSaveData
{
    public string CountryId { get; set; } = "";
    public double Stability { get; set; }
    public double PublicSupport { get; set; }
    public double WarSupport { get; set; }
}

/// <summary>
/// Demografische Daten eines Landes
/// </summary>
public class DemographicsSaveData
{
    public string CountryId { get; set; } = "";
    public double BirthRate { get; set; }
    public double DeathRate { get; set; }
    public double WorkingAgeRatio { get; set; }
    public double UrbanizationRate { get; set; }
    public double LiteracyRate { get; set; }
}

/// <summary>
/// Ein Datenpunkt der Ressourcen-Diagramm-Historie
/// </summary>
public class MoneySnapshotSaveData
{
    public int TotalDays { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public Dictionary<string, double> ResourceTotals { get; set; } = new();
}
