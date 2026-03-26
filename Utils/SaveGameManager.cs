using System.Text.Json;
using System.Text.Json.Serialization;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;

namespace GrandStrategyGame;

/// <summary>
/// Verwaltet Spielstaende - Speichern und Laden
/// Unterstuetzt 3 Speicherplaetze mit benutzerdefinierten Namen
/// </summary>
public static class SaveGameManager
{
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
        return Path.Combine(SaveDirectory, $"save_{slot}.json");
    }

    /// <summary>
    /// Gibt den Pfad zur Metadaten-Datei fuer einen Slot zurueck
    /// </summary>
    private static string GetMetadataFilePath(int slot)
    {
        return Path.Combine(SaveDirectory, $"save_{slot}_meta.json");
    }

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
    /// Gibt alle 3 Speicherslot-Infos zurueck
    /// </summary>
    public static SaveSlotInfo?[] GetAllSlots()
    {
        return new[]
        {
            GetSlotInfo(1),
            GetSlotInfo(2),
            GetSlotInfo(3)
        };
    }

    /// <summary>
    /// Speichert das aktuelle Spiel in einen Slot
    /// </summary>
    public static bool SaveGame(Game game, WorldMap worldMap, int slot, string? customName = null)
    {
        if (slot < 1 || slot > 3) return false;
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
        if (slot < 1 || slot > 3) return null;

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
        if (slot < 1 || slot > 3) return false;

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
            Version = 2,
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
                Stockpile = c.Stockpile.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
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

            // Provinzdaten (Minen, Fabriken)
            Provinces = worldMap.Provinces.Values
                .Where(p => p.Mines.Count > 0 || p.CivilianFactories > 0 || p.MilitaryFactories > 0)
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

                    // Stockpile wiederherstellen
                    country.Stockpile.Clear();
                    foreach (var (typeStr, amount) in countrySave.Stockpile)
                    {
                        if (Enum.TryParse<ResourceType>(typeStr, out var type))
                        {
                            country.Stockpile[type] = amount;
                        }
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
                            var mine = new Mine(mineType)
                            {
                                Level = mineSave.Level,
                                ProductionPerDay = mineSave.ProductionPerDay
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
                        tradeManager.CreateTradeAgreement(
                            agreement.ExporterId,
                            agreement.ImporterId,
                            type,
                            agreement.Amount);
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

            Console.WriteLine($"[SaveGameManager] Spielstand angewendet: {saveData.PlayerCountryId}, {saveData.Year}-{saveData.Month}-{saveData.Day} {saveData.Hour:D2}:{saveData.Minute:D2}");
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
