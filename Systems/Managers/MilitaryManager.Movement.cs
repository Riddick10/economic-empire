using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// MilitaryManager - Bewegung und Provinz-Eroberung
/// </summary>
public partial class MilitaryManager
{
    /// <summary>
    /// Verarbeitet alle laufenden Bewegungen (stuendlich fuer schnellere Bewegung)
    /// </summary>
    private void ProcessMovement(GameContext context)
    {
        for (int i = 0; i < _allUnits.Count; i++)
        {
            var unit = _allUnits[i];
            if (unit.Status != UnitStatus.Moving) continue;

            string oldProvinceId = unit.ProvinceId;

            if (unit.UpdateMovement())
            {
                // Bewegung abgeschlossen - Update Provinz-Tracking
                UpdateUnitProvinceTracking(unit, oldProvinceId, unit.ProvinceId);

                // Pruefe ob Provinz erobert werden kann
                TryClaimProvince(unit, context);
            }
        }
    }

    /// <summary>
    /// Versucht eine Provinz zu erobern wenn Einheit in fremdem Gebiet ankommt
    /// Nur moeglich wenn Krieg erklaert wurde!
    /// </summary>
    private void TryClaimProvince(MilitaryUnit unit, GameContext context)
    {
        if (context.WorldMap == null) return;

        // Finde die Provinz in der die Einheit jetzt ist
        if (!context.WorldMap.Provinces.TryGetValue(unit.ProvinceId, out var province))
            return;

        // Pruefe ob die Provinz einem anderen Land gehoert
        string oldOwnerId = province.CountryId;
        string newOwnerId = unit.CountryId;

        if (oldOwnerId == newOwnerId)
            return; // Eigene Provinz - nichts zu tun

        // Pruefe ob Krieg erklaert wurde
        if (!IsAtWarWith(newOwnerId, oldOwnerId))
            return; // Kein Krieg - keine Eroberung moeglich

        // Besitzer aendern
        province.CountryId = newOwnerId;
    }

    /// <summary>
    /// Prueft ob Einheiten ihre visuelle Bewegung abgeschlossen haben (jeden Frame aufrufen)
    /// </summary>
    public void CheckVisualMovementCompletion()
    {
        for (int i = 0; i < _allUnits.Count; i++)
        {
            var unit = _allUnits[i];
            if (unit.Status != UnitStatus.Moving || unit.MovementHoursLeft > 0) continue;
            // Nur verarbeiten wenn TargetProvinceId noch gesetzt ist (verhindert Doppel-Verarbeitung)
            if (unit.TargetProvinceId == null) continue;
            if (unit.VisualProgress >= BalanceConfig.Military.MovementCompleteThreshold)
            {
                string oldProvinceId = unit.ProvinceId;

                // Bewegung visuell abgeschlossen
                unit.ProvinceId = unit.TargetProvinceId;
                unit.StartProvinceId = null;
                unit.TargetProvinceId = null;
                unit.VisualProgress = 0f;
                unit.Status = UnitStatus.Ready;

                // Update Provinz-Tracking
                UpdateUnitProvinceTracking(unit, oldProvinceId, unit.ProvinceId);

                // Provinz erobern falls fremdes Gebiet
                if (_context != null)
                {
                    TryClaimProvince(unit, _context);
                }
            }
        }
    }

    /// <summary>
    /// Aktualisiert das Provinz-Tracking wenn eine Einheit sich bewegt
    /// </summary>
    private void UpdateUnitProvinceTracking(MilitaryUnit unit, string oldProvinceId, string newProvinceId)
    {
        // Entferne aus alter Provinz
        if (_unitsByProvince.TryGetValue(oldProvinceId, out var oldList))
        {
            oldList.Remove(unit);
        }

        // Fuege zu neuer Provinz hinzu
        if (!_unitsByProvince.ContainsKey(newProvinceId))
            _unitsByProvince[newProvinceId] = new List<MilitaryUnit>();
        _unitsByProvince[newProvinceId].Add(unit);
    }

    /// <summary>
    /// Startet die Bewegung einer Einheit zu einer Zielprovinz.
    /// Wenn feindliche Einheiten in der Zielprovinz stehen und Krieg herrscht,
    /// wird die Einheit stattdessen in den Engaging-Modus versetzt (Fernkampf vor Einmarsch).
    /// </summary>
    public bool MoveUnit(MilitaryUnit unit, string targetProvinceId, int movementHours = 12)
    {
        if (unit.Status != UnitStatus.Ready)
            return false;

        if (unit.ProvinceId == targetProvinceId)
            return false;

        // Pruefe ob Zielprovinz existiert
        if (_context?.WorldMap != null && !_context.WorldMap.Provinces.ContainsKey(targetProvinceId))
            return false;

        // Pruefe ob feindliche Einheiten in der Zielprovinz stehen
        if (HasEnemyUnitsInProvince(unit.CountryId, targetProvinceId))
        {
            // Fernkampf: Einheit bleibt stehen und kaempft aus Distanz
            unit.Status = UnitStatus.Engaging;
            unit.EngageTargetProvinceId = targetProvinceId;
            return true;
        }

        unit.StartMovement(targetProvinceId, movementHours);
        return true;
    }

    /// <summary>
    /// Prueft ob feindliche Einheiten in einer Provinz stehen (im Krieg mit dem angegebenen Land)
    /// </summary>
    private bool HasEnemyUnitsInProvince(string countryId, string provinceId)
    {
        if (!_unitsByProvince.TryGetValue(provinceId, out var unitsInProvince))
            return false;

        foreach (var unit in unitsInProvince)
        {
            if (unit.CountryId != countryId && unit.Manpower > 0 &&
                (unit.Status == UnitStatus.Ready || unit.Status == UnitStatus.InCombat || unit.Status == UnitStatus.Engaging) &&
                IsAtWarWith(countryId, unit.CountryId))
                return true;
        }
        return false;
    }


    /// <summary>
    /// Berechnet Bewegungsstunden basierend auf Provinz-Distanz
    /// </summary>
    public int CalculateMovementHours(string fromProvinceId, string toProvinceId, GameContext context)
    {
        if (context.WorldMap == null) return 12;

        if (!context.WorldMap.Provinces.TryGetValue(fromProvinceId, out var from) ||
            !context.WorldMap.Provinces.TryGetValue(toProvinceId, out var to))
            return 12;

        double dx = from.LabelPosition.X - to.LabelPosition.X;
        double dy = from.LabelPosition.Y - to.LabelPosition.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        return Math.Clamp((int)(distance / 50 + 6), 6, 72);
    }
}
