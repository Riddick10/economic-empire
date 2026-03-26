using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet Militär und Kriege.
/// - Armeen und Einheiten
/// - Rekrutierung
/// - Kriegserklärungen
/// - Schlachten
/// - Besatzung
/// - Friedensverhandlungen
/// </summary>
public partial class MilitaryManager : GameSystemBase
{
    public override string Name => "Military";
    public override int Priority => 50;
    public override TickType[] SubscribedTicks => new[] { TickType.Hourly, TickType.Daily, TickType.Weekly };

    // Aktive Kriege
    private readonly List<War> _activeWars = new();

    // Militärische Stärke pro Land (gecacht)
    private readonly Dictionary<string, MilitaryStrength> _militaryStrength = new();

    // Alle Militaereinheiten im Spiel
    private readonly List<MilitaryUnit> _allUnits = new();

    // Einheiten pro Land (fuer schnellen Zugriff)
    private readonly Dictionary<string, List<MilitaryUnit>> _unitsByCountry = new();

    // Einheiten pro Provinz (fuer Map-Rendering)
    private readonly Dictionary<string, List<MilitaryUnit>> _unitsByProvince = new();

    // Laufende Rekrutierungen
    private readonly List<RecruitmentOrder> _activeRecruitments = new();

    // Referenzen fuer Provinz-Eroberung
    private GameContext? _context;
    private NotificationManager? _notificationManager;

    public override void Initialize(GameContext context)
    {
        _context = context;
        _notificationManager = context.Game.GetSystem<NotificationManager>();

        // Initialisiere Militärstärke für alle Länder
        foreach (var (countryId, country) in context.Countries)
        {
            var mil = BalanceConfig.Military;
            _militaryStrength[countryId] = new MilitaryStrength
            {
                ActivePersonnel = (long)(country.Population * mil.ActivePersonnelPercent),
                ReservePersonnel = (long)(country.Population * mil.ReservePersonnelPercent),
                TankCount = 0,
                AircraftCount = 0,
                NavalVessels = 0,
                MilitaryBudget = country.GDP * mil.BudgetPercentGDP
            };
        }

        // Starteinheiten fuer groessere Laender erstellen (basierend auf BIP)
        CreateStartingUnits(context);
    }

    /// <summary>
    /// Erstellt Starteinheiten fuer alle Laender basierend auf deren BIP.
    /// Groessere Laender haben mehr Einheiten beim Spielstart.
    /// </summary>
    private void CreateStartingUnits(GameContext context)
    {
        if (context.WorldMap == null) return;

        foreach (var (countryId, country) in context.Countries)
        {
            // Anzahl Starteinheiten basierend auf BIP (0-10)
            int unitCount;
            if (country.GDP > 10_000_000) unitCount = 10;       // USA, CHN
            else if (country.GDP > 3_000_000) unitCount = 6;    // DEU, JPN, GBR, FRA, IND
            else if (country.GDP > 1_000_000) unitCount = 4;    // RUS, BRA, KOR, ITA
            else if (country.GDP > 300_000) unitCount = 2;      // Mittelgrosse Laender
            else if (country.GDP > 50_000) unitCount = 1;       // Kleine Laender
            else continue;                                       // Sehr kleine Laender: keine Einheiten

            // Finde Provinzen des Landes
            var provinces = new List<string>();
            foreach (var province in context.WorldMap.Provinces.Values)
            {
                if (province.CountryId == countryId)
                    provinces.Add(province.Id);
            }
            if (provinces.Count == 0) continue;

            // Einheiten erstellen (ohne Waffen-Kosten, da Start-Setup)
            for (int i = 0; i < unitCount; i++)
            {
                string provinceId = provinces[i % provinces.Count];

                // Mix aus verschiedenen Einheitentypen
                UnitType type;
                if (i == 0) type = UnitType.Infantry;
                else if (i == 1 && unitCount >= 3) type = UnitType.Tank;
                else if (i == 2 && unitCount >= 4) type = UnitType.Artillery;
                else type = UnitType.Infantry;

                // Direkt als Ready erstellen (nicht rekrutieren)
                var unit = new MilitaryUnit(type, countryId, provinceId);
                unit.Status = UnitStatus.Ready;
                unit.RecruitmentDaysLeft = 0;

                _allUnits.Add(unit);
                if (!_unitsByCountry.ContainsKey(countryId))
                    _unitsByCountry[countryId] = new List<MilitaryUnit>();
                _unitsByCountry[countryId].Add(unit);

                if (!_unitsByProvince.ContainsKey(provinceId))
                    _unitsByProvince[provinceId] = new List<MilitaryUnit>();
                _unitsByProvince[provinceId].Add(unit);
            }
        }

        Console.WriteLine($"[MilitaryManager] {_allUnits.Count} Starteinheiten fuer {_unitsByCountry.Count} Laender erstellt");
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        switch (tickType)
        {
            case TickType.Hourly:
                ProcessMovement(context);  // Bewegung stuendlich verarbeiten (schneller)
                break;

            case TickType.Daily:
                ProcessRecruitment(context);
                ProcessBattles(context);
                ProcessRecovery();
                break;

            case TickType.Weekly:
                UpdateWarProgress(context);
                CheckWarExhaustion(context);
                break;
        }
    }

    /// <summary>
    /// Gibt Militärstärke eines Landes zurück
    /// </summary>
    public MilitaryStrength? GetMilitaryStrength(string countryId)
    {
        return _militaryStrength.TryGetValue(countryId, out var strength) ? strength : null;
    }

    private static bool IsMilitaryAlliance(string allianceName)
    {
        return allianceName is "NATO" or "OVKS" or "SCO";
    }
}

/// <summary>
/// Militärische Stärke eines Landes
/// </summary>
public class MilitaryStrength
{
    public long ActivePersonnel { get; set; }
    public long ReservePersonnel { get; set; }
    public int TankCount { get; set; }
    public int AircraftCount { get; set; }
    public int NavalVessels { get; set; }
    public double MilitaryBudget { get; set; }
    public double WarExhaustion { get; set; }  // 0-1
    public double Morale { get; set; } = 0.8;
}

/// <summary>
/// Repräsentiert einen Krieg
/// </summary>
public class War
{
    public string Id { get; set; } = "";
    public List<string> Attackers { get; set; } = new();
    public List<string> Defenders { get; set; } = new();
    public string WarGoal { get; set; } = "";
    public int StartYear { get; set; }
    public bool IsActive { get; set; }
    public WarResult? Result { get; set; }
}

public enum WarResult
{
    AttackerVictory,
    DefenderVictory,
    WhitePeace,
    Stalemate
}
