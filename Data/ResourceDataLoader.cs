using System.Text.Json;
using GrandStrategyGame.Models;

namespace GrandStrategyGame;

/// <summary>
/// Laedt Ressourcen-Daten aus JSON-Dateien (resource-deposits.json, resource-abundance.json)
/// </summary>
public static class ResourceDataLoader
{
    private static readonly Dictionary<string, ResourceType> ResourceTypeMap = new()
    {
        ["oil"] = ResourceType.Oil,
        ["naturalGas"] = ResourceType.NaturalGas,
        ["coal"] = ResourceType.Coal,
        ["iron"] = ResourceType.Iron,
        ["copper"] = ResourceType.Copper,
        ["uranium"] = ResourceType.Uranium,
    };

    /// <summary>
    /// Laedt Provinznamen und Koordinaten-Deposits aus resource-deposits.json
    /// </summary>
    public static (
        Dictionary<ResourceType, HashSet<string>> ProvinceNames,
        Dictionary<ResourceType, List<(string CountryId, double Lon, double Lat)>> Deposits
    ) LoadDeposits(string basePath)
    {
        var provinceNames = new Dictionary<ResourceType, HashSet<string>>();
        var deposits = new Dictionary<ResourceType, List<(string, double, double)>>();

        // Initialisiere leere Collections fuer alle Typen
        foreach (var type in ResourceTypeMap.Values)
        {
            provinceNames[type] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            deposits[type] = new List<(string, double, double)>();
        }

        string path = Path.Combine(basePath, "Data", "resource-deposits.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[ResourceDataLoader] WARNUNG: {path} nicht gefunden!");
            return (provinceNames, deposits);
        }

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        foreach (var kvp in ResourceTypeMap)
        {
            string jsonKey = kvp.Key;
            ResourceType type = kvp.Value;

            if (!root.TryGetProperty(jsonKey, out var resourceElement))
                continue;

            // Provinznamen laden
            if (resourceElement.TryGetProperty("provinces", out var provincesArray))
            {
                foreach (var province in provincesArray.EnumerateArray())
                {
                    string? name = province.GetString();
                    if (!string.IsNullOrEmpty(name))
                        provinceNames[type].Add(name);
                }
            }

            // Koordinaten-Deposits laden
            if (resourceElement.TryGetProperty("deposits", out var depositsArray))
            {
                foreach (var deposit in depositsArray.EnumerateArray())
                {
                    string? countryId = deposit.GetProperty("countryId").GetString();
                    double lon = deposit.GetProperty("lon").GetDouble();
                    double lat = deposit.GetProperty("lat").GetDouble();
                    if (!string.IsNullOrEmpty(countryId))
                        deposits[type].Add((countryId, lon, lat));
                }
            }
        }

        return (provinceNames, deposits);
    }

    /// <summary>
    /// Laedt Laender-Ressourcenwerte aus resource-abundance.json
    /// </summary>
    public static Dictionary<string, ResourceAbundance.CountryResources> LoadAbundance(string basePath)
    {
        var data = new Dictionary<string, ResourceAbundance.CountryResources>();

        string path = Path.Combine(basePath, "Data", "resource-abundance.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[ResourceDataLoader] WARNUNG: {path} nicht gefunden!");
            return data;
        }

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        foreach (var country in root.EnumerateObject())
        {
            string countryId = country.Name;
            var res = country.Value;

            float oil = GetFloat(res, "oil");
            float gas = GetFloat(res, "naturalGas");
            float coal = GetFloat(res, "coal");
            float iron = GetFloat(res, "iron");
            float copper = GetFloat(res, "copper");
            float uranium = GetFloat(res, "uranium");

            data[countryId] = new ResourceAbundance.CountryResources(oil, gas, coal, iron, copper, uranium);
        }

        return data;
    }

    private static float GetFloat(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
            return (float)prop.GetDouble();
        return 0f;
    }
}
