using GrandStrategyGame.Data;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// MilitaryManager - Kriegserklaerungen, Friedensverhandlungen, Kriegsmuedigkeit
/// </summary>
public partial class MilitaryManager
{
    private void UpdateWarProgress(GameContext context)
    {
        for (int i = 0; i < _activeWars.Count; i++)
        {
            var war = _activeWars[i];
            if (!war.IsActive) continue;
            // Berechne Kriegsmüdigkeit
            foreach (var participantId in war.Attackers.Concat(war.Defenders))
            {
                if (_militaryStrength.TryGetValue(participantId, out var strength))
                {
                    strength.WarExhaustion += BalanceConfig.Military.DailyWarExhaustion;
                    strength.WarExhaustion = Math.Min(1.0, strength.WarExhaustion);
                }
            }
        }
    }

    private void CheckWarExhaustion(GameContext context)
    {
        // Länder mit hoher Kriegsmüdigkeit suchen automatisch Frieden
        for (int i = 0; i < _activeWars.Count; i++)
        {
            var war = _activeWars[i];
            if (!war.IsActive) continue;
            double peaceThreshold = BalanceConfig.Military.PeaceExhaustionThreshold;
            bool attackersExhausted = war.Attackers.All(a =>
                _militaryStrength.TryGetValue(a, out var s) && s.WarExhaustion > peaceThreshold);

            bool defendersExhausted = war.Defenders.All(d =>
                _militaryStrength.TryGetValue(d, out var s) && s.WarExhaustion > peaceThreshold);

            if (attackersExhausted && defendersExhausted)
            {
                // Weißer Frieden
                EndWar(war, WarResult.WhitePeace);
            }
        }
    }

    /// <summary>
    /// Erklärt einem Land den Krieg
    /// </summary>
    public War? DeclareWar(string attackerId, string defenderId, string warGoal, GameContext context)
    {
        // Kann nicht sich selbst den Krieg erklaeren
        if (attackerId == defenderId) return null;

        // Prüfe ob bereits im Krieg
        for (int i = 0; i < _activeWars.Count; i++)
        {
            var w = _activeWars[i];
            if (w.IsActive &&
                (w.Attackers.Contains(attackerId) && w.Defenders.Contains(defenderId) ||
                 w.Attackers.Contains(defenderId) && w.Defenders.Contains(attackerId)))
                return null;
        }

        var war = new War
        {
            Id = Guid.NewGuid().ToString(),
            Attackers = new List<string> { attackerId },
            Defenders = new List<string> { defenderId },
            WarGoal = warGoal,
            StartYear = context.CurrentYear,
            IsActive = true
        };

        _activeWars.Add(war);

        // Publiziere Event
        context.Events.Publish(new WarDeclaredEvent(attackerId, defenderId));

        // Hole verbündete Länder in den Krieg
        var diplomacyManager = context.Game.GetSystem<DiplomacyManager>();
        if (diplomacyManager != null)
        {
            // Verbündete des Verteidigers treten als Verteidiger bei
            var defenderAlliances = diplomacyManager.GetCountryAlliances(defenderId);
            if (defenderAlliances != null)
            {
                for (int i = 0; i < defenderAlliances.Count; i++)
                {
                    if (!IsMilitaryAlliance(defenderAlliances[i])) continue;

                    var members = diplomacyManager.GetCountriesInAlliance(defenderAlliances[i]);
                    if (members == null) continue;

                    for (int j = 0; j < members.Count; j++)
                    {
                        var member = members[j];
                        if (member == defenderId || member == attackerId) continue;
                        if (war.Defenders.Contains(member)) continue;

                        war.Defenders.Add(member);
                        _notificationManager?.AddNotification(
                            "Bündnisbeitritt",
                            $"{member} tritt dem Krieg als Verteidiger bei (Bündnis: {defenderAlliances[i]})",
                            NotificationType.Warning,
                            member);
                    }
                }
            }

            // Verbündete des Angreifers treten als Angreifer bei
            var attackerAlliances = diplomacyManager.GetCountryAlliances(attackerId);
            if (attackerAlliances != null)
            {
                for (int i = 0; i < attackerAlliances.Count; i++)
                {
                    if (!IsMilitaryAlliance(attackerAlliances[i])) continue;

                    var members = diplomacyManager.GetCountriesInAlliance(attackerAlliances[i]);
                    if (members == null) continue;

                    for (int j = 0; j < members.Count; j++)
                    {
                        var member = members[j];
                        if (member == attackerId || member == defenderId) continue;
                        if (war.Attackers.Contains(member)) continue;
                        if (war.Defenders.Contains(member)) continue;

                        war.Attackers.Add(member);
                        _notificationManager?.AddNotification(
                            "Bündnisbeitritt",
                            $"{member} tritt dem Krieg als Angreifer bei (Bündnis: {attackerAlliances[i]})",
                            NotificationType.Warning,
                            member);
                    }
                }
            }
        }

        return war;
    }

    /// <summary>
    /// Beendet einen Krieg
    /// </summary>
    public void EndWar(War war, WarResult result)
    {
        war.IsActive = false;
        war.Result = result;

        // Setze Kriegsmüdigkeit zurück
        foreach (var participantId in war.Attackers.Concat(war.Defenders))
        {
            if (_militaryStrength.TryGetValue(participantId, out var strength))
            {
                strength.WarExhaustion = 0;
            }
        }
    }

    /// <summary>
    /// Gibt alle aktiven Kriege eines Landes zurück
    /// </summary>
    public IEnumerable<War> GetWars(string countryId)
    {
        for (int i = 0; i < _activeWars.Count; i++)
        {
            var w = _activeWars[i];
            if (w.IsActive && (w.Attackers.Contains(countryId) || w.Defenders.Contains(countryId)))
                yield return w;
        }
    }

    /// <summary>
    /// Prueft ob zwei Laender im Krieg miteinander sind
    /// </summary>
    public bool IsAtWarWith(string countryA, string countryB)
    {
        for (int i = 0; i < _activeWars.Count; i++)
        {
            var w = _activeWars[i];
            if (w.IsActive &&
                ((w.Attackers.Contains(countryA) && w.Defenders.Contains(countryB)) ||
                 (w.Attackers.Contains(countryB) && w.Defenders.Contains(countryA))))
                return true;
        }
        return false;
    }

}
