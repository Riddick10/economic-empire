using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Missions-/Zielsystem: Prueft monatlich, ob der Spieler bestimmte Ziele
/// erreicht hat, und meldet Erfolge als Benachrichtigung.
/// Abgeschlossene Missionen werden im Spielstand gespeichert.
/// </summary>
public class MissionManager : GameSystemBase
{
    public override string Name => "Missions";
    public override int Priority => 170; // Nach Wirtschaft/Militaer, vor der KI
    public override TickType[] SubscribedTicks => new[] { TickType.Monthly };

    private readonly List<Mission> _missions = new();
    private readonly HashSet<string> _completedIds = new();

    private NotificationManager? _notifications;
    private TradeManager? _trade;
    private MilitaryManager? _military;
    private TechTreeManager? _tech;
    private PoliticsManager? _politics;

    /// <summary>Alle definierten Missionen (fuer UI-Anzeige)</summary>
    public IReadOnlyList<Mission> Missions => _missions;

    /// <summary>IDs der abgeschlossenen Missionen (fuer Speichern)</summary>
    public IReadOnlyCollection<string> CompletedMissionIds => _completedIds;

    public bool IsCompleted(string missionId) => _completedIds.Contains(missionId);

    public override void Initialize(GameContext context)
    {
        _notifications = context.Game.GetSystem<NotificationManager>();
        _trade = context.Game.GetSystem<TradeManager>();
        _military = context.Game.GetSystem<MilitaryManager>();
        _tech = context.Game.GetSystem<TechTreeManager>();
        _politics = context.Game.GetSystem<PoliticsManager>();

        DefineMissions();
    }

    private void DefineMissions()
    {
        _missions.Clear();

        _missions.Add(new Mission("top10_gdp", "Aufsteigende Wirtschaftsmacht",
            "Erreiche mit deinem BIP die Top 10 der Welt.",
            (ctx, player) => GetGdpRank(ctx, player) <= 10));

        _missions.Add(new Mission("top5_gdp", "Wirtschaftsgigant",
            "Erreiche mit deinem BIP die Top 5 der Welt.",
            (ctx, player) => GetGdpRank(ctx, player) <= 5));

        _missions.Add(new Mission("full_employment", "Vollbeschäftigung",
            "Senke die Arbeitslosigkeit unter 4%.",
            (ctx, player) => player.UnemploymentRate < 0.04));

        _missions.Add(new Mission("education_nation", "Bildungsnation",
            "Steigere das Bildungsniveau auf über 80%.",
            (ctx, player) => player.EducationLevel > 0.8));

        _missions.Add(new Mission("solid_finances", "Solide Finanzen",
            "Halte die Staatsschulden unter 10% des BIP.",
            (ctx, player) => player.GDP > 0 && player.NationalDebt / player.GDP < 0.10));

        _missions.Add(new Mission("trade_empire", "Handelsimperium",
            "Unterhalte mindestens 5 aktive Handelsabkommen.",
            (ctx, player) => CountPlayerTradeAgreements(player.Id) >= 5));

        _missions.Add(new Mission("military_power", "Militärmacht",
            "Kommandiere mindestens 10 Militäreinheiten.",
            (ctx, player) => CountPlayerUnits(player.Id) >= 10));

        _missions.Add(new Mission("tech_pioneer", "Forschergeist",
            "Erforsche mindestens 5 Technologien.",
            (ctx, player) => CountCompletedTechs() >= 5));

        _missions.Add(new Mission("rock_solid", "Fels in der Brandung",
            "Erreiche eine politische Stabilität von über 85%.",
            (ctx, player) => (_politics?.GetPolitics(player.Id)?.Stability ?? 0) > 0.85));

        _missions.Add(new Mission("full_treasury", "Volle Kassen",
            "Fülle die Staatskasse auf über 50 Milliarden.",
            (ctx, player) => player.Budget > 50_000));
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        if (tickType != TickType.Monthly) return;

        var player = context.PlayerCountry;
        if (player == null) return;

        foreach (var mission in _missions)
        {
            if (_completedIds.Contains(mission.Id)) continue;

            bool fulfilled;
            try
            {
                fulfilled = mission.Check(context, player);
            }
            catch
            {
                continue; // Einzelne fehlerhafte Pruefung darf das Spiel nicht stoppen
            }

            if (fulfilled)
            {
                _completedIds.Add(mission.Id);
                _notifications?.AddNotification(
                    $"Mission erfüllt: {mission.Title}",
                    $"{mission.Description} ({_completedIds.Count}/{_missions.Count} Missionen abgeschlossen)",
                    NotificationType.Success);
            }
        }
    }

    /// <summary>
    /// Stellt abgeschlossene Missionen aus einem Spielstand wieder her
    /// </summary>
    public void RestoreCompleted(IEnumerable<string> missionIds)
    {
        _completedIds.Clear();
        foreach (var id in missionIds)
            _completedIds.Add(id);
    }

    // =========================================================================
    //  Hilfsmethoden fuer Missions-Pruefungen
    // =========================================================================

    private static int GetGdpRank(GameContext context, Country player)
    {
        int rank = 1;
        foreach (var country in context.Countries.Values)
        {
            if (country.Id != player.Id && country.GDP > player.GDP)
                rank++;
        }
        return rank;
    }

    private int CountPlayerTradeAgreements(string playerId)
    {
        if (_trade == null) return 0;
        int count = 0;
        foreach (var agreement in _trade.GetTradeAgreements())
        {
            if (agreement.IsActive && (agreement.ExporterId == playerId || agreement.ImporterId == playerId))
                count++;
        }
        return count;
    }

    private int CountPlayerUnits(string playerId)
    {
        if (_military == null) return 0;
        int count = 0;
        foreach (var unit in _military.GetAllUnits())
        {
            if (unit.CountryId == playerId && unit.Manpower > 0)
                count++;
        }
        return count;
    }

    private int CountCompletedTechs()
    {
        if (_tech == null) return 0;
        int count = 0;
        foreach (var progress in _tech.PlayerProgress.Values)
        {
            if (progress.Status == TechStatus.Completed)
                count++;
        }
        return count;
    }
}

/// <summary>
/// Eine Mission mit Ziel-Pruefung
/// </summary>
public class Mission
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public Func<GameContext, Country, bool> Check { get; }

    public Mission(string id, string title, string description, Func<GameContext, Country, bool> check)
    {
        Id = id;
        Title = title;
        Description = description;
        Check = check;
    }
}
