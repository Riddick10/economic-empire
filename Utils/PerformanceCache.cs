using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;
using Raylib_cs;

namespace GrandStrategyGame;

/// <summary>
/// Caches fuer Performance-Optimierung (vermeidet per-Frame Allokation).
/// </summary>
class PerformanceCache
{
    // Trade-Caches
    public readonly Dictionary<ResourceType, double> ResourceTradeNet = new();
    public readonly Dictionary<(string From, string To), List<TradeAgreement>> TradeRoutes = new();
    public readonly List<TradeAgreement> EmptyTradeList = new();
    public readonly List<ResourceType> TradeRouteResTypes = new();

    // Text-Messung Cache
    public readonly Dictionary<(string text, int fontSize), int> TextWidth = new();

    // Wiederverwendbare Listen fuer Panel-Rendering
    public readonly List<MilitaryUnit> RecruitingUnits = new(16);
    public readonly List<TradeAgreement> TradeAgreements = new(16);
    public readonly List<War> Wars = new(8);

    // Partei-Daten Cache
    public readonly Dictionary<string, List<(string Name, float Percentage, Color Color)>> PartyData = new();
}
