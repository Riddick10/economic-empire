using GrandStrategyGame.Models;

namespace GrandStrategyGame.Models;

/// <summary>
/// Statische Ressourcen-Konfigurationen fuer UI-Anzeigen und Spiellogik.
/// </summary>
public static class ResourceConfig
{
    public static readonly ResourceType[] All = (ResourceType[])Enum.GetValues(typeof(ResourceType));

    public static readonly ResourceType[] Tradeable = {
        ResourceType.Oil, ResourceType.Coal, ResourceType.Iron,
        ResourceType.Steel, ResourceType.Uranium, ResourceType.Copper,
        ResourceType.NaturalGas, ResourceType.Food, ResourceType.ConsumerGoods,
        ResourceType.Electronics, ResourceType.Machinery
    };

    public static readonly ResourceType[] BottomBar = {
        ResourceType.Oil, ResourceType.Coal, ResourceType.Iron,
        ResourceType.Uranium, ResourceType.NaturalGas, ResourceType.Copper
    };

    public static readonly ResourceType[] TopBar = {
        ResourceType.Oil, ResourceType.NaturalGas, ResourceType.Coal,
        ResourceType.Iron, ResourceType.Copper, ResourceType.Uranium,
        ResourceType.Food,
        ResourceType.Steel, ResourceType.Electronics, ResourceType.Machinery, ResourceType.ConsumerGoods
    };

    public static readonly ResourceType[] TopBarRow1 = {
        ResourceType.Oil, ResourceType.NaturalGas, ResourceType.Coal,
        ResourceType.Iron, ResourceType.Copper, ResourceType.Uranium, ResourceType.Food
    };

    public static readonly ResourceType[] TopBarRow2 = {
        ResourceType.Steel, ResourceType.Electronics, ResourceType.Machinery,
        ResourceType.ConsumerGoods, ResourceType.Weapons, ResourceType.Ammunition
    };

    public static readonly ResourceType[] LogisticsRaw = {
        ResourceType.Oil, ResourceType.NaturalGas, ResourceType.Coal,
        ResourceType.Iron, ResourceType.Copper, ResourceType.Uranium
    };

    public static readonly ResourceType[] LogisticsProcessed = {
        ResourceType.Steel, ResourceType.Electronics,
        ResourceType.Machinery, ResourceType.ConsumerGoods
    };

    public static readonly ResourceType[] LogisticsMilitary = {
        ResourceType.Weapons, ResourceType.Ammunition
    };

    public static readonly string[] SpeedLabels = { "5", "4", "3", "2", "1" };
    public static readonly int[] SpeedValues = { 5, 4, 3, 2, 1 };
}
