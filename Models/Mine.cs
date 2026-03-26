using System.Numerics;
using GrandStrategyGame.Models;
using Raylib_cs;

namespace GrandStrategyGame.Models;

/// <summary>
/// Typen von Minen/Foerderanlagen
/// </summary>
public enum MineType
{
    OilWell,        // Oelbohrung
    GasDrill,       // Gasfoerderung
    CoalMine,       // Kohlemine
    IronMine,       // Eisenmine
    CopperMine,     // Kupfermine
    UraniumMine     // Uranmine
}

/// <summary>
/// Repraesentiert eine Mine/Foerderanlage in einer Provinz
/// </summary>
public class Mine
{
    public MineType Type { get; set; }
    public double ProductionPerDay { get; set; }
    public int Level { get; set; } = 1;

    public Mine(MineType type)
    {
        Type = type;
        ProductionPerDay = GetBaseProduction(type);
    }

    /// <summary>
    /// Gibt die Basis-Produktion pro Tag zurueck
    /// Werte sind so balanciert, dass eine Mine ca. 3-5 Fabriken versorgen kann
    /// </summary>
    public static double GetBaseProduction(MineType type)
    {
        return type switch
        {
            MineType.OilWell => 10,      // Oel
            MineType.GasDrill => 6,      // Erdgas
            MineType.CoalMine => 7,      // Kohle
            MineType.IronMine => 6,      // Eisen
            MineType.CopperMine => 4,    // Kupfer
            MineType.UraniumMine => 0.2, // Uran (selten)
            _ => 5
        };
    }

    /// <summary>
    /// Gibt den zugehoerigen ResourceType zurueck
    /// </summary>
    public static ResourceType GetResourceType(MineType type)
    {
        return type switch
        {
            MineType.OilWell => ResourceType.Oil,
            MineType.GasDrill => ResourceType.NaturalGas,
            MineType.CoalMine => ResourceType.Coal,
            MineType.IronMine => ResourceType.Iron,
            MineType.CopperMine => ResourceType.Copper,
            MineType.UraniumMine => ResourceType.Uranium,
            _ => ResourceType.Iron
        };
    }

    /// <summary>
    /// Gibt den deutschen Namen der Mine zurueck
    /// </summary>
    public static string GetGermanName(MineType type)
    {
        return type switch
        {
            MineType.OilWell => "Oelbohrung",
            MineType.GasDrill => "Gasfoerderanlage",
            MineType.CoalMine => "Kohlemine",
            MineType.IronMine => "Eisenmine",
            MineType.CopperMine => "Kupfermine",
            MineType.UraniumMine => "Uranmine",
            _ => "Mine"
        };
    }

    /// <summary>
    /// Gibt die Baukosten fuer eine Mine zurueck
    /// </summary>
    public static double GetBuildCost(MineType type)
    {
        return type switch
        {
            MineType.OilWell => 4_000,      // 4 Mrd.
            MineType.GasDrill => 3_600,     // 3.6 Mrd.
            MineType.CoalMine => 3_100,     // 3.1 Mrd.
            MineType.IronMine => 2_800,     // 2.8 Mrd.
            MineType.CopperMine => 3_200,   // 3.2 Mrd.
            MineType.UraniumMine => 4_500,  // 4.5 Mrd.
            _ => 3_000
        };
    }

    /// <summary>
    /// Gibt die Farbe fuer die Kartenanzeige zurueck
    /// </summary>
    public static Color GetMapColor(MineType type)
    {
        return type switch
        {
            MineType.OilWell => new Color((byte)255, (byte)140, (byte)0, (byte)255),    // Orange
            MineType.GasDrill => new Color((byte)100, (byte)200, (byte)255, (byte)255), // Hellblau
            MineType.CoalMine => new Color((byte)60, (byte)60, (byte)60, (byte)255),    // Dunkelgrau
            MineType.IronMine => new Color((byte)150, (byte)150, (byte)150, (byte)255), // Grau
            MineType.CopperMine => new Color((byte)184, (byte)115, (byte)51, (byte)255),// Kupfer/Braun
            MineType.UraniumMine => new Color((byte)180, (byte)255, (byte)0, (byte)255),// Gelbgruen
            _ => new Color((byte)255, (byte)255, (byte)255, (byte)255)
        };
    }
}
