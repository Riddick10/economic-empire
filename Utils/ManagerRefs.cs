using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame;

/// <summary>
/// Per-Frame gecachte Referenzen auf Manager-Systeme.
/// Wird einmal pro Frame in der Hauptschleife aktualisiert.
/// </summary>
class ManagerRefs
{
    public MilitaryManager? Military;
    public TradeManager? Trade;
    public NotificationManager? Notif;
    public ConflictManager? Conflict;
    public ProductionManager? Production;
    public EconomyManager? Economy;

    public void Update(Game game)
    {
        Military = game.GetSystem<MilitaryManager>();
        Trade = game.GetSystem<TradeManager>();
        Notif = game.GetSystem<NotificationManager>();
        Conflict = game.GetSystem<ConflictManager>();
        Production = game.GetSystem<ProductionManager>();
        Economy = game.GetSystem<EconomyManager>();
    }
}
