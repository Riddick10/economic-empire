using GrandStrategyGame.Models;

namespace GrandStrategyGame;

/// <summary>
/// Reale Ressourcen-Produktionsdaten pro Land (normalisiert 0.0-1.0, 1.0 = groesster Produzent)
/// Daten werden aus Data/resource-abundance.json geladen.
/// </summary>
public static class ResourceAbundance
{
    public struct CountryResources
    {
        public float Oil;
        public float NaturalGas;
        public float Coal;
        public float Iron;
        public float Copper;
        public float Uranium;

        public CountryResources(float oil, float gas, float coal, float iron, float copper, float uranium)
        {
            Oil = oil; NaturalGas = gas; Coal = coal; Iron = iron; Copper = copper; Uranium = uranium;
        }

        public float GetValue(ResourceType type) => type switch
        {
            ResourceType.Oil => Oil,
            ResourceType.NaturalGas => NaturalGas,
            ResourceType.Coal => Coal,
            ResourceType.Iron => Iron,
            ResourceType.Copper => Copper,
            ResourceType.Uranium => Uranium,
            _ => 0f
        };

        public float GetMaxValue() => Math.Max(Oil, Math.Max(NaturalGas, Math.Max(Coal, Math.Max(Iron, Math.Max(Copper, Uranium)))));
    }

    private static Dictionary<string, CountryResources> _data = new();

    /// <summary>
    /// Laedt Ressourcen-Daten aus resource-abundance.json.
    /// Muss beim Spielstart aufgerufen werden.
    /// </summary>
    public static void Load(string basePath)
    {
        _data = ResourceDataLoader.LoadAbundance(basePath);
    }

    /// <summary>
    /// Gibt die Ressourcen-Daten fuer ein Land zurueck
    /// </summary>
    public static CountryResources GetCountryResources(string countryId)
    {
        return _data.TryGetValue(countryId, out var res) ? res : default;
    }

    /// <summary>
    /// Gibt den Heatmap-Wert (0-1) fuer ein Land und eine bestimmte Ressource zurueck.
    /// Bei null wird der hoechste Wert aller Ressourcen genommen.
    /// </summary>
    public static float GetHeatmapValue(string countryId, ResourceType? resourceFilter)
    {
        var res = GetCountryResources(countryId);
        if (resourceFilter.HasValue)
            return res.GetValue(resourceFilter.Value);
        return res.GetMaxValue();
    }
}
