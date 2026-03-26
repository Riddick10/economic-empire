using Raylib_cs;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Statische Daten (Laendernamen, Farben, Oel-Vorkommen, Hauptstaedte)
/// </summary>
public partial class WorldMap
{
    // Fluss-Farbe (gleiche Farbe wie Ozean)
    private static readonly Color RiverColor = new(25, 58, 92, 220);

    // Ressourcen-Daten (aus JSON geladen via LoadResourceData)
    private static HashSet<string> UraniumProvinceNames = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> IronProvinceNames = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> CoalProvinceNames = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> OilProvinceNames = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> NaturalGasProvinceNames = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> CopperProvinceNames = new(StringComparer.OrdinalIgnoreCase);

    private static List<(string CountryId, double Lon, double Lat)> UraniumDeposits = new();
    private static List<(string CountryId, double Lon, double Lat)> IronDeposits = new();
    private static List<(string CountryId, double Lon, double Lat)> CoalDeposits = new();
    private static List<(string CountryId, double Lon, double Lat)> OilDeposits = new();
    private static List<(string CountryId, double Lon, double Lat)> NaturalGasDeposits = new();
    private static List<(string CountryId, double Lon, double Lat)> CopperDeposits = new();

    /// <summary>
    /// Laedt alle Ressourcen-Daten aus JSON (resource-deposits.json)
    /// Muss beim Spielstart aufgerufen werden, bevor Provinzen gerendert werden.
    /// </summary>
    public static void LoadResourceData(string basePath)
    {
        var (provinces, deposits) = ResourceDataLoader.LoadDeposits(basePath);

        UraniumProvinceNames = provinces[ResourceType.Uranium];
        IronProvinceNames = provinces[ResourceType.Iron];
        CoalProvinceNames = provinces[ResourceType.Coal];
        OilProvinceNames = provinces[ResourceType.Oil];
        NaturalGasProvinceNames = provinces[ResourceType.NaturalGas];
        CopperProvinceNames = provinces[ResourceType.Copper];

        UraniumDeposits = deposits[ResourceType.Uranium];
        IronDeposits = deposits[ResourceType.Iron];
        CoalDeposits = deposits[ResourceType.Coal];
        OilDeposits = deposits[ResourceType.Oil];
        NaturalGasDeposits = deposits[ResourceType.NaturalGas];
        CopperDeposits = deposits[ResourceType.Copper];
    }

    // Laendernamen (Code -> voller Name) - aus JSON geladen
    private static Dictionary<string, string> CountryNames = new();

    // Hauptstaedte (Code -> Name, Longitude, Latitude) - aus JSON geladen
    private static Dictionary<string, (string Name, double Lon, double Lat)> Capitals = new();

    // Feste Laender-Index-Zuordnung fuer konsistente einzigartige Farben - aus JSON geladen
    private static Dictionary<string, int> CountryColorIndex = new();

    // Farbpalette im HOI4-Stil - aus JSON geladen
    private static Color[] CountryPalette = Array.Empty<Color>();

    /// <summary>
    /// Laedt Laendernamen, Hauptstaedte und Farben aus JSON-Dateien.
    /// Muss beim Spielstart aufgerufen werden.
    /// </summary>
    public static void LoadWorldMapData(string basePath)
    {
        var (names, capitals) = GameDataLoader.LoadCountryMeta(basePath);
        CountryNames = names;
        Capitals = capitals;

        var (palette, colorIndex) = GameDataLoader.LoadCountryColors(basePath);
        CountryPalette = palette;
        CountryColorIndex = colorIndex;
    }

    private static Color GetRegionColor(string countryId)
    {
        if (CountryPalette.Length == 0) return new Color(100, 100, 100, 255);

        if (!CountryColorIndex.TryGetValue(countryId, out int index))
        {
            // Fallback fuer unbekannte Laender
            index = Math.Abs(countryId.GetHashCode()) % CountryPalette.Length;
        }

        // Nutze Index um Farbe aus Palette zu waehlen
        // Mische benachbarte Laender nicht mit aehnlichen Farben
        int paletteIndex = (index * 7) % CountryPalette.Length;  // 7 ist teilerfremd zur Palettengroesse

        return CountryPalette[paletteIndex];
    }
}

/// <summary>
/// Repraesentiert eine Stadt auf der Karte
/// </summary>
public record City(string Name, double Lat, double Lon, int Population, bool IsCapital = false);

/// <summary>
/// JSON-Deserialisierungsklasse fuer Staedte
/// </summary>
public class CityJson
{
    public string name { get; set; } = "";
    public double lat { get; set; }
    public double lon { get; set; }
    public int population { get; set; }
    public bool isCapital { get; set; }
}

/// <summary>
/// JSON-Deserialisierungsklasse fuer Zeitzonen
/// </summary>
public class TimezoneDataJson
{
    public Dictionary<string, double> countries { get; set; } = new();
    public Dictionary<string, Dictionary<string, double>> provinces { get; set; } = new();
}

/// <summary>
/// Verwaltet Zeitzonen-Daten fuer realistische Ortszeiten
/// </summary>
public class TimezoneData
{
    // Zeitzonen pro Land (UTC-Offset in Stunden)
    public Dictionary<string, double> CountryTimezones { get; } = new();

    // Zeitzonen pro Provinz (fuer Laender mit mehreren Zeitzonen)
    public Dictionary<string, Dictionary<string, double>> ProvinceTimezones { get; } = new();

    /// <summary>
    /// Gibt den Zeitzonen-Offset fuer eine Provinz zurueck
    /// </summary>
    public double GetTimezoneOffset(string countryId, string? provinceName)
    {
        // Zuerst Provinz-spezifische Zeitzone pruefen
        if (provinceName != null && ProvinceTimezones.TryGetValue(countryId, out var provinceZones))
        {
            if (provinceZones.TryGetValue(provinceName, out double offset))
                return offset;

            // Fallback auf "default" fuer das Land
            if (provinceZones.TryGetValue("default", out double defaultOffset))
                return defaultOffset;
        }

        // Dann Land-Zeitzone
        if (CountryTimezones.TryGetValue(countryId, out double countryOffset))
            return countryOffset;

        // Fallback: UTC
        return 0;
    }
}
