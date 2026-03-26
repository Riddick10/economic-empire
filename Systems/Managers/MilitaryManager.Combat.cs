using GrandStrategyGame.Data;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// MilitaryManager - Kampf und Schlachten
/// </summary>
public partial class MilitaryManager
{
    // Wiederverwendbare Collections fuer ProcessBattles (vermeidet GC-Druck)
    private readonly Dictionary<string, List<MilitaryUnit>> _battleUnitsByCountry = new();
    private readonly List<string> _battleCountryIds = new();

    private void ProcessBattles(GameContext context)
    {
        if (_activeWars.Count == 0) return;

        // 1. Klassischer Kampf: Feindliche Einheiten in DERSELBEN Provinz
        foreach (var (provinceId, unitsInProvince) in _unitsByProvince)
        {
            if (unitsInProvince.Count < 2) continue;

            _battleUnitsByCountry.Clear();
            int combatCount = 0;
            foreach (var unit in unitsInProvince)
            {
                if (unit.Status != UnitStatus.Ready && unit.Status != UnitStatus.InCombat)
                    continue;
                if (unit.Manpower <= 0)
                    continue;
                combatCount++;

                if (!_battleUnitsByCountry.TryGetValue(unit.CountryId, out var countryUnits))
                {
                    countryUnits = new List<MilitaryUnit>();
                    _battleUnitsByCountry[unit.CountryId] = countryUnits;
                }
                countryUnits.Add(unit);
            }

            if (combatCount < 2 || _battleUnitsByCountry.Count < 2) continue;

            _battleCountryIds.Clear();
            _battleCountryIds.AddRange(_battleUnitsByCountry.Keys);

            for (int i = 0; i < _battleCountryIds.Count; i++)
            {
                for (int j = i + 1; j < _battleCountryIds.Count; j++)
                {
                    if (IsAtWarWith(_battleCountryIds[i], _battleCountryIds[j]))
                    {
                        ResolveBattle(
                            _battleUnitsByCountry[_battleCountryIds[i]],
                            _battleUnitsByCountry[_battleCountryIds[j]],
                            provinceId,
                            context);
                    }
                }
            }
        }

        // 2. Fernkampf (Engaging): Einheiten kaempfen aus ihrer Provinz gegen Feinde in der Zielprovinz
        ProcessEngagingBattles(context);
    }

    /// <summary>
    /// Verarbeitet Fernkaempfe: Engaging-Einheiten kaempfen gegen Feinde in der Zielprovinz.
    /// Wenn alle Feinde besiegt sind, marschiert die Einheit ein.
    /// </summary>
    private void ProcessEngagingBattles(GameContext context)
    {
        // Sammle alle Engaging-Einheiten (kopiere Liste weil sich Status aendern kann)
        var engagingUnits = new List<MilitaryUnit>();
        foreach (var unit in _allUnits)
        {
            if (unit.Status == UnitStatus.Engaging && unit.EngageTargetProvinceId != null)
                engagingUnits.Add(unit);
        }

        // Gruppiere nach Zielprovinz und Angreifer-Land
        var engagementsByTarget = new Dictionary<string, Dictionary<string, List<MilitaryUnit>>>();
        foreach (var unit in engagingUnits)
        {
            var targetId = unit.EngageTargetProvinceId!;
            if (!engagementsByTarget.TryGetValue(targetId, out var byCountry))
            {
                byCountry = new Dictionary<string, List<MilitaryUnit>>();
                engagementsByTarget[targetId] = byCountry;
            }
            if (!byCountry.TryGetValue(unit.CountryId, out var countryUnits))
            {
                countryUnits = new List<MilitaryUnit>();
                byCountry[unit.CountryId] = countryUnits;
            }
            countryUnits.Add(unit);
        }

        foreach (var (targetProvinceId, attackersByCountry) in engagementsByTarget)
        {
            // Sammle Verteidiger in der Zielprovinz
            var defenders = new List<MilitaryUnit>();
            if (_unitsByProvince.TryGetValue(targetProvinceId, out var unitsInTarget))
            {
                foreach (var unit in unitsInTarget)
                {
                    if (unit.Manpower > 0 && (unit.Status == UnitStatus.Ready || unit.Status == UnitStatus.InCombat))
                        defenders.Add(unit);
                }
            }

            foreach (var (attackerCountryId, attackers) in attackersByCountry)
            {
                // Finde feindliche Verteidiger
                var enemyDefenders = new List<MilitaryUnit>();
                foreach (var def in defenders)
                {
                    if (IsAtWarWith(attackerCountryId, def.CountryId))
                        enemyDefenders.Add(def);
                }

                if (enemyDefenders.Count == 0)
                {
                    // Keine Feinde mehr: Einmarsch starten!
                    foreach (var attacker in attackers)
                    {
                        attacker.Status = UnitStatus.Ready;
                        attacker.EngageTargetProvinceId = null;
                        // Jetzt normal zur Provinz bewegen
                        if (context.WorldMap != null)
                        {
                            int hours = CalculateMovementHours(attacker.ProvinceId, targetProvinceId, context);
                            MoveUnit(attacker, targetProvinceId, hours);
                        }
                    }
                    continue;
                }

                // Setze alle beteiligten Einheiten auf InCombat-Status (fuer HP-Bars)
                foreach (var unit in attackers) unit.Status = UnitStatus.Engaging;
                foreach (var unit in enemyDefenders) unit.Status = UnitStatus.InCombat;

                // Fernkampf-Runde ausfuehren (Angreifer haben leichten Malus weil sie aus Distanz kaempfen)
                double ammoMultiplierAtk = ConsumeAmmunition(attackers, context);
                double ammoMultiplierDef = ConsumeAmmunition(enemyDefenders, context);

                double strengthAtk = CalculateSideStrength(attackers) * ammoMultiplierAtk * BalanceConfig.Military.EngagingAttackerPenalty;
                double strengthDef = CalculateSideStrength(enemyDefenders) * ammoMultiplierDef * BalanceConfig.Military.DefenderBonus;

                if (strengthAtk <= 0 && strengthDef <= 0) continue;

                var mil = BalanceConfig.Military;
                double damageToDefenders = strengthAtk * mil.BaseDamageMultiplier * (mil.DamageVarianceMin + Random.Shared.NextDouble() * mil.DamageVarianceRange);
                double damageToAttackers = strengthDef * mil.BaseDamageMultiplier * (mil.DamageVarianceMin + Random.Shared.NextDouble() * mil.DamageVarianceRange);

                ApplyDamage(attackers, damageToAttackers);
                ApplyDamage(enemyDefenders, damageToDefenders);

                // Pruefe ob eine Seite besiegt ist
                bool attackersDefeated = true;
                foreach (var u in attackers) { if (u.Organization >= mil.DefeatOrgThreshold && u.Manpower > 0) { attackersDefeated = false; break; } }
                bool defendersDefeated = true;
                foreach (var u in enemyDefenders) { if (u.Organization >= mil.DefeatOrgThreshold && u.Manpower > 0) { defendersDefeated = false; break; } }

                if (attackersDefeated || defendersDefeated)
                {
                    var losers = attackersDefeated ? attackers : enemyDefenders;
                    var winners = attackersDefeated ? enemyDefenders : attackers;

                    foreach (var unit in losers)
                    {
                        if (unit.Manpower <= 0)
                            RemoveUnit(unit);
                        else
                        {
                            unit.Status = UnitStatus.Recovering;
                            unit.EngageTargetProvinceId = null;
                            unit.Organization = Math.Max(mil.MinOrganization, unit.Organization);
                        }
                    }

                    foreach (var unit in winners)
                    {
                        unit.Experience = Math.Min(1.0f, unit.Experience + mil.ExperienceGainPerBattle);

                        if (unit.Status == UnitStatus.Engaging)
                        {
                            // Angreifer hat gewonnen: Jetzt einmarschieren!
                            unit.Status = UnitStatus.Ready;
                            string? engageTarget = unit.EngageTargetProvinceId;
                            unit.EngageTargetProvinceId = null;
                            if (engageTarget != null && context.WorldMap != null)
                            {
                                int hours = CalculateMovementHours(unit.ProvinceId, engageTarget, context);
                                MoveUnit(unit, engageTarget, hours);
                            }
                        }
                        else
                        {
                            unit.Status = UnitStatus.Ready;
                        }
                    }

                    Console.WriteLine($"[Engagement] {(attackersDefeated ? "Verteidiger" : "Angreifer")} gewinnt in {targetProvinceId}");
                }
            }
        }
    }

    /// <summary>
    /// Loest eine Schlacht zwischen zwei Gruppen feindlicher Einheiten auf
    /// </summary>
    private void ResolveBattle(List<MilitaryUnit> sideA, List<MilitaryUnit> sideB, string provinceId, GameContext context)
    {
        // Setze alle beteiligten Einheiten auf InCombat
        foreach (var unit in sideA.Concat(sideB))
            unit.Status = UnitStatus.InCombat;

        // Munitionsverbrauch: Jede Einheit verbraucht Munition im Kampf
        double ammoMultiplierA = ConsumeAmmunition(sideA, context);
        double ammoMultiplierB = ConsumeAmmunition(sideB, context);

        // Bestimme welche Seite der Verteidiger ist (Besitzer der Provinz)
        string? provinceOwner = null;
        if (context.WorldMap != null && context.WorldMap.Provinces.TryGetValue(provinceId, out var province))
            provinceOwner = province.CountryId;

        bool sideADefends = sideA.Count > 0 && sideA[0].CountryId == provinceOwner;
        bool sideBDefends = sideB.Count > 0 && sideB[0].CountryId == provinceOwner;

        // Berechne Gesamtstaerke jeder Seite (mit Munitions-Multiplikator)
        double strengthA = CalculateSideStrength(sideA) * ammoMultiplierA;
        double strengthB = CalculateSideStrength(sideB) * ammoMultiplierB;

        // Verteidiger-Bonus
        var mil = BalanceConfig.Military;
        if (sideADefends) strengthA *= mil.DefenderBonus;
        if (sideBDefends) strengthB *= mil.DefenderBonus;

        if (strengthA <= 0 && strengthB <= 0) return;

        // Kampfrunde: Jede Seite verursacht Schaden basierend auf relativer Staerke
        double damageToB = strengthA * mil.BaseDamageMultiplier * (mil.DamageVarianceMin + Random.Shared.NextDouble() * mil.DamageVarianceRange);
        double damageToA = strengthB * mil.BaseDamageMultiplier * (mil.DamageVarianceMin + Random.Shared.NextDouble() * mil.DamageVarianceRange);

        // Verteile Schaden auf Einheiten
        ApplyDamage(sideA, damageToA);
        ApplyDamage(sideB, damageToB);

        // Pruefe ob eine Seite besiegt ist
        bool sideADefeated = sideA.All(u => u.Organization < mil.DefeatOrgThreshold || u.Manpower <= 0);
        bool sideBDefeated = sideB.All(u => u.Organization < mil.DefeatOrgThreshold || u.Manpower <= 0);

        if (sideADefeated || sideBDefeated)
        {
            // Verlierer zieht sich zurueck oder wird zerstoert
            var losers = sideADefeated ? sideA : sideB;
            var winners = sideADefeated ? sideB : sideA;

            foreach (var unit in losers)
            {
                if (unit.Manpower <= 0)
                {
                    // Einheit zerstoert
                    RemoveUnit(unit);
                }
                else
                {
                    // Einheit erholt sich
                    unit.Status = UnitStatus.Recovering;
                    unit.Organization = Math.Max(mil.MinOrganization, unit.Organization);
                }
            }

            // Gewinner bekommen Erfahrung
            foreach (var unit in winners)
            {
                unit.Experience = Math.Min(1.0f, unit.Experience + mil.ExperienceGainPerBattle);
                unit.Status = UnitStatus.Ready;
            }

            // Provinz-Eroberung: Wenn Gewinner die Provinz nicht besitzt, erobere sie
            if (winners.Count == 0) return;
            string winnerCountry = winners[0].CountryId;
            string loserCountry = losers.Count > 0 ? losers[0].CountryId : "";

            if (provinceOwner != null && provinceOwner == loserCountry)
            {
                foreach (var unit in winners)
                {
                    TryClaimProvince(unit, context);
                }
            }

            // Kampfergebnis nur in Konsole loggen (keine Benachrichtigung)
            Console.WriteLine($"[Battle] {winnerCountry} besiegt {loserCountry} in {provinceId}");
        }
    }

    /// <summary>
    /// Verbraucht Munition fuer eine Seite und gibt den Effektivitaets-Multiplikator zurueck.
    /// Ohne Munition: nur 30% Kampfkraft (Bajonett-Angriffe etc.)
    /// </summary>
    private double ConsumeAmmunition(List<MilitaryUnit> units, GameContext context)
    {
        if (units.Count == 0) return 1.0;
        string countryId = units[0].CountryId;
        if (!context.Countries.TryGetValue(countryId, out var country))
            return 1.0;

        // Berechne benoetigte Munition
        double totalAmmoNeeded = 0;
        foreach (var unit in units)
            totalAmmoNeeded += MilitaryUnit.GetAmmoCostPerBattle(unit.Type);

        double ammoAvailable = country.GetResource(ResourceType.Ammunition);

        if (ammoAvailable >= totalAmmoNeeded)
        {
            // Volle Munition: volle Kampfkraft
            country.UseResource(ResourceType.Ammunition, totalAmmoNeeded);
            return 1.0;
        }
        else if (ammoAvailable > 0)
        {
            // Teilweise Munition: proportionale Kampfkraft (min 30%)
            country.UseResource(ResourceType.Ammunition, ammoAvailable);
            double ratio = ammoAvailable / totalAmmoNeeded;
            return 0.3 + 0.7 * ratio;
        }
        else
        {
            // Keine Munition: nur 30% Kampfkraft
            return 0.3;
        }
    }

    /// <summary>
    /// Berechnet die Gesamtkampfstaerke einer Seite
    /// </summary>
    private double CalculateSideStrength(List<MilitaryUnit> units)
    {
        double total = 0;
        foreach (var unit in units)
        {
            double effectiveness = unit.GetCombatEffectiveness();
            double attackPower = unit.Attack + unit.SoftAttack * BalanceConfig.Military.SoftHardAttackWeight + unit.HardAttack * BalanceConfig.Military.SoftHardAttackWeight;
            total += attackPower * effectiveness * (unit.Manpower / (double)unit.MaxManpower);
        }

        // Apply military_strength tech bonus for the player's units
        if (units.Count > 0 && _context != null)
        {
            var countryId = units[0].CountryId;
            if (countryId == _context.PlayerCountry?.Id)
            {
                var techManager = _context.Game.GetSystem<TechTreeManager>();
                if (techManager != null)
                    total *= (1.0 + techManager.GetEffect("military_strength"));
            }
        }

        return total;
    }

    /// <summary>
    /// Verteilt Schaden auf Einheiten einer Seite
    /// </summary>
    private void ApplyDamage(List<MilitaryUnit> units, double totalDamage)
    {
        if (units.Count == 0) return;

        // Schaden gleichmaessig verteilen
        double damagePerUnit = totalDamage / units.Count;

        foreach (var unit in units)
        {
            var mil = BalanceConfig.Military;

            // Organisation sinkt (Hauptfaktor fuer Kampffaehigkeit)
            float orgDamage = (float)(damagePerUnit / (unit.Defense + mil.OrgDamageDefenseBase) * mil.OrgDamageMultiplier);
            unit.Organization = Math.Max(0, unit.Organization - orgDamage);

            // Mannschaftsverluste
            int manpowerLoss = (int)(damagePerUnit * mil.ManpowerLossMultiplier * (1.0 - unit.Defense / mil.ManpowerDefenseDivisor));
            manpowerLoss = Math.Max(0, Math.Min(manpowerLoss, unit.Manpower));
            unit.Manpower -= manpowerLoss;

            // Moral sinkt bei Verlusten
            float moraleDamage = (float)manpowerLoss / unit.MaxManpower * mil.MoraleDamageMultiplier;
            unit.Morale = Math.Max(mil.MinMorale, unit.Morale - moraleDamage);
        }
    }

    /// <summary>
    /// Entfernt eine zerstoerte Einheit aus allen Listen
    /// </summary>
    private void RemoveUnit(MilitaryUnit unit)
    {
        _allUnits.Remove(unit);

        if (_unitsByCountry.TryGetValue(unit.CountryId, out var countryUnits))
            countryUnits.Remove(unit);

        if (_unitsByProvince.TryGetValue(unit.ProvinceId, out var provinceUnits))
            provinceUnits.Remove(unit);
    }
}
