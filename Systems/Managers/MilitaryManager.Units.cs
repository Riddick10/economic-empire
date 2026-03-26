using GrandStrategyGame.Data;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// MilitaryManager - Rekrutierung, Erholung und Einheiten-Abfragen
/// </summary>
public partial class MilitaryManager
{
    /// <summary>
    /// Verarbeitet alle laufenden Rekrutierungen
    /// </summary>
    private void ProcessRecruitment(GameContext context)
    {
        var completedRecruitments = new List<RecruitmentOrder>();

        foreach (var order in _activeRecruitments)
        {
            if (order.Unit.UpdateRecruitment())
            {
                // Rekrutierung abgeschlossen
                completedRecruitments.Add(order);

                // Event publizieren
                context.Events.Publish(new UnitRecruitedEvent(order.Unit));
            }
        }

        // Abgeschlossene Rekrutierungen entfernen
        foreach (var completed in completedRecruitments)
        {
            _activeRecruitments.Remove(completed);
        }
    }

    /// <summary>
    /// Einheiten im Recovering-Status erholen sich taeglich
    /// </summary>
    private void ProcessRecovery()
    {
        for (int i = 0; i < _allUnits.Count; i++)
        {
            var unit = _allUnits[i];
            if (unit.Status != UnitStatus.Recovering) continue;

            var mil = BalanceConfig.Military;
            unit.Organization = Math.Min(1.0f, unit.Organization + mil.DailyOrgRecovery);
            unit.Morale = Math.Min(1.0f, unit.Morale + mil.DailyMoraleRecovery);

            if (unit.Organization >= mil.CombatReadyOrgThreshold)
            {
                unit.Status = UnitStatus.Ready;
            }
        }
    }

    /// <summary>
    /// Gibt die benoetigten Equipment-Typen fuer einen Einheitentyp zurueck
    /// </summary>
    public static Dictionary<string, int> GetEquipmentRequirements(UnitType type)
    {
        return type switch
        {
            UnitType.Infantry => new() { { "Infantry Equipment", 1 } },
            UnitType.Tank => new() { { "Tank", 1 } },
            UnitType.Artillery => new() { { "Artillery", 1 } },
            UnitType.Mechanized => new() { { "Infantry Equipment", 1 }, { "Tank", 1 } },
            UnitType.Airborne => new() { { "Infantry Equipment", 1 } },
            _ => new()
        };
    }

    public MilitaryUnit? StartRecruitment(UnitType type, string countryId, string provinceId, GameContext context)
    {
        // Pruefe ob Land existiert
        if (!context.Countries.TryGetValue(countryId, out var country))
            return null;

        // Pruefe Kosten
        var (manpowerCost, moneyCost) = MilitaryUnit.GetRecruitmentCost(type);

        // Pruefe ob genug Geld vorhanden
        if (country.Budget < moneyCost)
            return null;

        // Pruefe ob genug Waffen vorhanden
        double weaponsNeeded = MilitaryUnit.GetWeaponsCost(type);
        if (country.GetResource(ResourceType.Weapons) < weaponsNeeded)
            return null;

        // Erstelle neue Einheit
        var unit = new MilitaryUnit(type, countryId, provinceId);

        // Ziehe Kosten ab
        country.Budget -= moneyCost;
        country.UseResource(ResourceType.Weapons, weaponsNeeded);

        // Fuege Einheit zu Listen hinzu
        _allUnits.Add(unit);

        if (!_unitsByCountry.ContainsKey(countryId))
            _unitsByCountry[countryId] = new List<MilitaryUnit>();
        _unitsByCountry[countryId].Add(unit);

        if (!_unitsByProvince.ContainsKey(provinceId))
            _unitsByProvince[provinceId] = new List<MilitaryUnit>();
        _unitsByProvince[provinceId].Add(unit);

        // Erstelle Rekrutierungsauftrag
        var order = new RecruitmentOrder(unit, provinceId, context.TotalDays);
        _activeRecruitments.Add(order);

        return unit;
    }

    /// <summary>
    /// Gibt alle Einheiten eines Landes zurueck
    /// </summary>
    public IReadOnlyList<MilitaryUnit> GetUnits(string countryId)
    {
        if (_unitsByCountry.TryGetValue(countryId, out var units))
            return units.AsReadOnly();
        return Array.Empty<MilitaryUnit>();
    }

    /// <summary>
    /// Gibt alle Einheiten im Spiel zurueck (fuer Map-Rendering)
    /// </summary>
    public IReadOnlyList<MilitaryUnit> GetAllUnits() => _allUnits.AsReadOnly();

    /// <summary>
    /// Fuegt eine geladene Einheit hinzu (fuer Save/Load)
    /// </summary>
    public void AddLoadedUnit(MilitaryUnit unit)
    {
        _allUnits.Add(unit);

        if (!_unitsByCountry.ContainsKey(unit.CountryId))
            _unitsByCountry[unit.CountryId] = new List<MilitaryUnit>();
        _unitsByCountry[unit.CountryId].Add(unit);

        if (!_unitsByProvince.ContainsKey(unit.ProvinceId))
            _unitsByProvince[unit.ProvinceId] = new List<MilitaryUnit>();
        _unitsByProvince[unit.ProvinceId].Add(unit);

        // Falls noch in Rekrutierung, Auftrag erstellen
        if (unit.Status == UnitStatus.Recruiting)
        {
            _activeRecruitments.Add(new RecruitmentOrder(unit, unit.ProvinceId, 0));
        }
    }

    /// <summary>
    /// Entfernt alle Einheiten (fuer Save/Load Reset)
    /// </summary>
    public void ClearAllUnits()
    {
        _allUnits.Clear();
        _unitsByCountry.Clear();
        _unitsByProvince.Clear();
        _activeRecruitments.Clear();
    }
}
