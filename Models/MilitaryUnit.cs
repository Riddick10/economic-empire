using System.Numerics;
using GrandStrategyGame.Data;

namespace GrandStrategyGame.Models;

/// <summary>
/// Typen von Militaereinheiten
/// </summary>
public enum UnitType
{
    Infantry,       // Infanterie - Grundeinheit
    Tank,           // Panzer - Stark gegen Infanterie
    Artillery,      // Artillerie - Fernkampf
    Mechanized,     // Mechanisierte Infanterie
    Airborne        // Luftlandetruppen
}

/// <summary>
/// Status einer Militaereinheit
/// </summary>
public enum UnitStatus
{
    Recruiting,     // Wird rekrutiert
    Ready,          // Einsatzbereit
    Moving,         // In Bewegung
    Engaging,       // Kaempft aus Distanz gegen Feind in Zielprovinz (Fernkampf vor dem Einmarsch)
    InCombat,       // Im Kampf (gleiche Provinz)
    Recovering      // Erholt sich
}

/// <summary>
/// Repraesentiert eine Militaereinheit im Spiel
/// </summary>
public class MilitaryUnit
{
    private static int _nextId = 1;

    /// <summary>
    /// Setzt den ID-Zaehler zurueck (wichtig beim Laden eines Spielstands)
    /// </summary>
    public static void ResetIdCounter()
    {
        _nextId = 1;
    }

    public int Id { get; }
    public string Name { get; set; }
    public UnitType Type { get; set; }
    public UnitStatus Status { get; set; }
    public string CountryId { get; set; }
    public string ProvinceId { get; set; }

    // Staerke und Zustand
    public int Manpower { get; set; }           // Anzahl Soldaten
    public int MaxManpower { get; set; }        // Maximale Staerke
    public float Organization { get; set; }     // Organisation (0-1)
    public float Morale { get; set; }           // Moral (0-1)
    public float Experience { get; set; }       // Erfahrung (0-1)

    // Kampfwerte
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SoftAttack { get; set; }         // Gegen Infanterie
    public int HardAttack { get; set; }         // Gegen Panzer

    // Rekrutierung
    public int RecruitmentDaysLeft { get; set; }
    public int TotalRecruitmentDays { get; set; }

    // Bewegung (in Stunden)
    public string? StartProvinceId { get; set; }      // Startprovinz fuer Animation
    public string? TargetProvinceId { get; set; }
    public int MovementHoursLeft { get; set; }
    public int TotalMovementHours { get; set; }
    public float VisualProgress { get; set; }         // Fuer fluessige Animation (0-1)
    public float LastTargetProgress { get; set; }     // Letzter bekannter Zielfortschritt
    public float MovementVelocity { get; set; }       // Berechnete Geschwindigkeit (pro Sekunde)
    public double LastProgressUpdateTime { get; set; } // Echtzeit des letzten Progress-Updates

    // Fernkampf (Engaging): Einheit bleibt stehen, kaempft gegen Feinde in Zielprovinz
    public string? EngageTargetProvinceId { get; set; }

    // Position fuer Map-Rendering (Pixel-Koordinaten)
    public Vector2 MapPosition { get; set; }

    public MilitaryUnit(UnitType type, string countryId, string provinceId)
    {
        Id = _nextId++;
        Name = "Einheit"; // Default, wird in SetDefaultStats ueberschrieben
        Type = type;
        CountryId = countryId;
        ProvinceId = provinceId;
        Status = UnitStatus.Recruiting;
        Organization = 1.0f;
        Morale = BalanceConfig.Military.StartMorale;
        Experience = 0.0f;

        // Setze Standardwerte basierend auf Typ
        SetDefaultStats();
    }

    private void SetDefaultStats()
    {
        string typeName = Type.ToString();
        if (BalanceConfig.Units.TryGetValue(typeName, out var stats))
        {
            Name = stats.Name;
            MaxManpower = stats.MaxManpower;
            Manpower = MaxManpower;
            Attack = stats.Attack;
            Defense = stats.Defense;
            SoftAttack = stats.SoftAttack;
            HardAttack = stats.HardAttack;
            TotalRecruitmentDays = stats.RecruitmentDays;
        }

        RecruitmentDaysLeft = TotalRecruitmentDays;
    }

    /// <summary>
    /// Berechnet die effektive Kampfkraft
    /// </summary>
    public float GetCombatEffectiveness()
    {
        if (MaxManpower <= 0) return 0f;
        float manpowerRatio = (float)Manpower / MaxManpower;
        return manpowerRatio * Organization * Morale * (1 + Experience * BalanceConfig.Military.CombatEffectivenessExpBonus);
    }

    /// <summary>
    /// Gibt den Rekrutierungsfortschritt zurueck (0-1)
    /// </summary>
    public float GetRecruitmentProgress()
    {
        if (TotalRecruitmentDays <= 0) return 1.0f;
        return 1.0f - ((float)RecruitmentDaysLeft / TotalRecruitmentDays);
    }

    /// <summary>
    /// Aktualisiert die Rekrutierung um einen Tag
    /// </summary>
    public bool UpdateRecruitment()
    {
        if (Status != UnitStatus.Recruiting) return false;

        RecruitmentDaysLeft--;
        if (RecruitmentDaysLeft <= 0)
        {
            Status = UnitStatus.Ready;
            RecruitmentDaysLeft = 0;
            return true; // Rekrutierung abgeschlossen
        }
        return false;
    }

    /// <summary>
    /// Startet die Bewegung zu einer Zielprovinz
    /// </summary>
    public void StartMovement(string targetProvinceId, int hoursNeeded)
    {
        if (Status != UnitStatus.Ready) return;

        StartProvinceId = ProvinceId;  // Merke Startposition fuer Animation
        TargetProvinceId = targetProvinceId;
        TotalMovementHours = hoursNeeded;
        MovementHoursLeft = hoursNeeded;
        VisualProgress = 0f;           // Starte bei 0 fuer fluessige Animation
        LastTargetProgress = 0f;       // Reset fuer Geschwindigkeitsberechnung
        MovementVelocity = 0f;         // Wird beim ersten Frame berechnet
        LastProgressUpdateTime = 0;    // Wird beim ersten Render gesetzt
        Status = UnitStatus.Moving;
    }

    /// <summary>
    /// Aktualisiert die Bewegung um eine Stunde
    /// </summary>
    public bool UpdateMovement()
    {
        if (Status != UnitStatus.Moving) return false;

        MovementHoursLeft--;
        if (MovementHoursLeft <= 0)
        {
            MovementHoursLeft = 0;
            // Warte bis visuelle Animation fast fertig ist
            if (VisualProgress >= BalanceConfig.Military.MovementCompleteThreshold)
            {
                // Bewegung abgeschlossen - Einheit in Zielprovinz
                if (TargetProvinceId != null)
                {
                    ProvinceId = TargetProvinceId;
                }
                StartProvinceId = null;
                TargetProvinceId = null;
                VisualProgress = 0f;
                Status = UnitStatus.Ready;
                return true; // Bewegung abgeschlossen
            }
        }
        return false;
    }

    /// <summary>
    /// Gibt den Bewegungsfortschritt zurueck (0-1)
    /// </summary>
    public float GetMovementProgress()
    {
        if (TotalMovementHours <= 0) return 1.0f;
        return 1.0f - ((float)MovementHoursLeft / TotalMovementHours);
    }

    /// <summary>
    /// Gibt Kosten fuer die Rekrutierung zurueck
    /// </summary>
    public static (int manpower, double money) GetRecruitmentCost(UnitType type)
    {
        string typeName = type.ToString();
        if (BalanceConfig.Units.TryGetValue(typeName, out var stats))
            return (stats.RecruitCostManpower, stats.RecruitCostMoney);
        return (5000, 100_000);
    }

    /// <summary>
    /// Gibt den Waffenbedarf fuer die Rekrutierung zurueck
    /// </summary>
    public static double GetWeaponsCost(UnitType type)
    {
        return type switch
        {
            UnitType.Infantry => 50,      // Gewehre, leichte Waffen
            UnitType.Tank => 200,         // Schwere Bewaffnung
            UnitType.Artillery => 150,    // Geschuetze
            UnitType.Mechanized => 100,   // Gemischte Bewaffnung
            UnitType.Airborne => 80,      // Leichte Spezialwaffen
            _ => 50
        };
    }

    /// <summary>
    /// Gibt den Munitionsverbrauch pro Kampftag zurueck
    /// </summary>
    public static double GetAmmoCostPerBattle(UnitType type)
    {
        return type switch
        {
            UnitType.Infantry => 20,
            UnitType.Tank => 50,
            UnitType.Artillery => 80,     // Artillerie verbraucht am meisten
            UnitType.Mechanized => 30,
            UnitType.Airborne => 25,
            _ => 20
        };
    }

    /// <summary>
    /// Gibt den Icon-Namen fuer diesen Einheitentyp zurueck
    /// </summary>
    public static string GetIconName(UnitType type)
    {
        return type switch
        {
            UnitType.Infantry => "infantry.png",
            UnitType.Tank => "tank.png",
            UnitType.Artillery => "artillery.png",
            UnitType.Mechanized => "mechanized.png",
            UnitType.Airborne => "airborne.png",
            _ => "infantry.png"
        };
    }
}

/// <summary>
/// Repraesentiert einen laufenden Rekrutierungsauftrag
/// </summary>
public class RecruitmentOrder
{
    public MilitaryUnit Unit { get; }
    public string ProvinceId { get; }
    public int StartDay { get; }

    public RecruitmentOrder(MilitaryUnit unit, string provinceId, int startDay)
    {
        Unit = unit;
        ProvinceId = provinceId;
        StartDay = startDay;
    }
}
