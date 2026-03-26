using System.Text.Json;
using Raylib_cs;

namespace GrandStrategyGame;

/// <summary>
/// Laedt Spieldaten aus JSON-Dateien (countries-meta.json, country-colors.json, alliances.json)
/// </summary>
public static class GameDataLoader
{
    /// <summary>
    /// Laedt Laendernamen und Hauptstaedte aus countries-meta.json
    /// </summary>
    public static (
        Dictionary<string, string> CountryNames,
        Dictionary<string, (string Name, double Lon, double Lat)> Capitals
    ) LoadCountryMeta(string basePath)
    {
        var countryNames = new Dictionary<string, string>();
        var capitals = new Dictionary<string, (string, double, double)>();

        string path = Path.Combine(basePath, "Data", "countries-meta.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameDataLoader] WARNUNG: {path} nicht gefunden!");
            return (countryNames, capitals);
        }

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        foreach (var country in root.EnumerateObject())
        {
            string countryId = country.Name;
            var data = country.Value;

            if (data.TryGetProperty("name", out var nameProp))
                countryNames[countryId] = nameProp.GetString() ?? countryId;

            if (data.TryGetProperty("capital", out var capitalProp))
            {
                string capitalName = capitalProp.GetProperty("name").GetString() ?? "";
                double lon = capitalProp.GetProperty("lon").GetDouble();
                double lat = capitalProp.GetProperty("lat").GetDouble();
                capitals[countryId] = (capitalName, lon, lat);
            }
        }

        Console.WriteLine($"[GameDataLoader] {countryNames.Count} Laender, {capitals.Count} Hauptstaedte geladen");
        return (countryNames, capitals);
    }

    /// <summary>
    /// Laedt Farbpalette und Laender-Farb-Zuordnung aus country-colors.json
    /// </summary>
    public static (
        Color[] Palette,
        Dictionary<string, int> ColorIndex
    ) LoadCountryColors(string basePath)
    {
        var colorIndex = new Dictionary<string, int>();
        var palette = Array.Empty<Color>();

        string path = Path.Combine(basePath, "Data", "country-colors.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameDataLoader] WARNUNG: {path} nicht gefunden!");
            return (palette, colorIndex);
        }

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Palette laden
        if (root.TryGetProperty("palette", out var paletteArray))
        {
            var colors = new List<Color>();
            foreach (var colorArr in paletteArray.EnumerateArray())
            {
                var vals = new List<byte>();
                foreach (var v in colorArr.EnumerateArray())
                    vals.Add((byte)v.GetInt32());
                if (vals.Count >= 4)
                    colors.Add(new Color(vals[0], vals[1], vals[2], vals[3]));
            }
            palette = colors.ToArray();
        }

        // Country-Index laden
        if (root.TryGetProperty("countryIndex", out var indexObj))
        {
            foreach (var entry in indexObj.EnumerateObject())
                colorIndex[entry.Name] = entry.Value.GetInt32();
        }

        Console.WriteLine($"[GameDataLoader] {palette.Length} Farben, {colorIndex.Count} Laenderzuordnungen geladen");
        return (palette, colorIndex);
    }

    /// <summary>
    /// Laedt Buendnisse und Buendnisfarben aus alliances.json
    /// </summary>
    public static (
        Dictionary<string, string[]> Alliances,
        Dictionary<string, Color> AllianceColors,
        string[] MilitaryAlliances,
        string[] EconomicAlliances
    ) LoadAlliances(string basePath)
    {
        var alliances = new Dictionary<string, string[]>();
        var allianceColors = new Dictionary<string, Color>();
        var militaryAlliances = Array.Empty<string>();
        var economicAlliances = Array.Empty<string>();

        string path = Path.Combine(basePath, "Data", "alliances.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameDataLoader] WARNUNG: {path} nicht gefunden!");
            return (alliances, allianceColors, militaryAlliances, economicAlliances);
        }

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Allianzen laden
        if (root.TryGetProperty("alliances", out var alliancesObj))
        {
            foreach (var alliance in alliancesObj.EnumerateObject())
            {
                string name = alliance.Name;
                var data = alliance.Value;

                // Mitglieder
                if (data.TryGetProperty("members", out var membersArr))
                {
                    var members = new List<string>();
                    foreach (var m in membersArr.EnumerateArray())
                    {
                        string? member = m.GetString();
                        if (!string.IsNullOrEmpty(member))
                            members.Add(member);
                    }
                    alliances[name] = members.ToArray();
                }

                // Farbe
                if (data.TryGetProperty("color", out var colorArr))
                {
                    var vals = new List<byte>();
                    foreach (var v in colorArr.EnumerateArray())
                        vals.Add((byte)v.GetInt32());
                    if (vals.Count >= 4)
                        allianceColors[name] = new Color(vals[0], vals[1], vals[2], vals[3]);
                }
            }
        }

        // "None"-Farbe
        if (root.TryGetProperty("noneColor", out var noneColorArr))
        {
            var vals = new List<byte>();
            foreach (var v in noneColorArr.EnumerateArray())
                vals.Add((byte)v.GetInt32());
            if (vals.Count >= 4)
                allianceColors["None"] = new Color(vals[0], vals[1], vals[2], vals[3]);
        }

        // Kategorien
        if (root.TryGetProperty("militaryAlliances", out var milArr))
        {
            var list = new List<string>();
            foreach (var item in milArr.EnumerateArray())
            {
                string? s = item.GetString();
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
            militaryAlliances = list.ToArray();
        }

        if (root.TryGetProperty("economicAlliances", out var ecoArr))
        {
            var list = new List<string>();
            foreach (var item in ecoArr.EnumerateArray())
            {
                string? s = item.GetString();
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
            economicAlliances = list.ToArray();
        }

        Console.WriteLine($"[GameDataLoader] {alliances.Count} Buendnisse geladen");
        return (alliances, allianceColors, militaryAlliances, economicAlliances);
    }
}
