using System.Numerics;

namespace GrandStrategyGame.Map;

/// <summary>
/// Enthält echte geografische Koordinaten für Länder
/// Unterstützt GeoJSON für präzise Grenzen, mit Fallback auf vereinfachte Koordinaten
/// </summary>
public static class GeoData
{
    // GeoJSON-Daten werden beim ersten Zugriff geladen
    private static Dictionary<string, double[][]>? _geoJsonData;
    private static Dictionary<string, List<double[][]>>? _geoJsonMultiData;
    private static bool _geoJsonLoaded = false;

    // Manuelle Koordinaten als Fallback (aus JSON geladen)
    private static Dictionary<string, double[][]>? _manualCoordinates;
    private static bool _manualCoordsLoaded = false;

    // Pfad zur GeoJSON-Datei (Natural Earth 110m oder 50m)
    private const string GeoJsonPath = "Data/countries.geojson";
    private const string ManualCoordsPath = "Data/manual-coordinates.json";

    /// <summary>
    /// Lädt GeoJSON-Daten wenn verfügbar
    /// </summary>
    private static void EnsureGeoJsonLoaded()
    {
        if (_geoJsonLoaded) return;
        _geoJsonLoaded = true;

        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GeoJsonPath);

        if (File.Exists(fullPath))
        {
            Console.WriteLine($"Lade GeoJSON-Daten von: {fullPath}");
            _geoJsonData = GeoJsonLoader.LoadCountries(fullPath);
            _geoJsonMultiData = GeoJsonLoader.LoadCountriesMultiPolygon(fullPath);
            Console.WriteLine($"GeoJSON: {_geoJsonData.Count} Länder geladen");
        }
        else
        {
            // Versuche auch im Projektverzeichnis
            string projectPath = Path.Combine(Directory.GetCurrentDirectory(), GeoJsonPath);
            if (File.Exists(projectPath))
            {
                Console.WriteLine($"Lade GeoJSON-Daten von: {projectPath}");
                _geoJsonData = GeoJsonLoader.LoadCountries(projectPath);
                _geoJsonMultiData = GeoJsonLoader.LoadCountriesMultiPolygon(projectPath);
                Console.WriteLine($"GeoJSON: {_geoJsonData.Count} Länder geladen");
            }
            else
            {
                Console.WriteLine("Keine GeoJSON-Datei gefunden - verwende vereinfachte Koordinaten");
                Console.WriteLine($"Für präzise Grenzen, lade Natural Earth Daten nach: {GeoJsonPath}");
            }
        }
    }

    /// <summary>
    /// Gibt die Polygon-Koordinaten für ein Land zurück
    /// Versucht zuerst GeoJSON, dann Fallback auf manuelle Koordinaten
    /// </summary>
    public static double[][] GetCountryCoordinates(string countryId)
    {
        // Versuche GeoJSON zu laden
        EnsureGeoJsonLoaded();

        // Prüfe ob GeoJSON-Daten verfügbar
        if (_geoJsonData != null)
        {
            string normalizedCode = GeoJsonLoader.NormalizeCode(countryId);

            if (_geoJsonData.TryGetValue(normalizedCode, out var coords))
            {
                // Keine Vereinfachung - Original-Koordinaten für maximale Schärfe beim Zoom
                return coords;
            }

            if (_geoJsonData.TryGetValue(countryId, out coords))
            {
                return coords;
            }
        }

        // Fallback auf manuelle Koordinaten
        return GetManualCoordinates(countryId);
    }

    /// <summary>
    /// Gibt ALLE Polygon-Koordinaten für ein Land zurück (für MultiPolygon-Unterstützung)
    /// Wichtig für Länder mit mehreren Gebieten wie USA (mit Alaska), Russland (mit Kaliningrad), Indonesien, etc.
    /// </summary>
    public static List<double[][]> GetCountryMultiPolygons(string countryId)
    {
        EnsureGeoJsonLoaded();

        if (_geoJsonMultiData != null)
        {
            string normalizedCode = GeoJsonLoader.NormalizeCode(countryId);

            if (_geoJsonMultiData.TryGetValue(normalizedCode, out var polygons))
            {
                // Keine Vereinfachung - Original-Koordinaten für maximale Schärfe beim Zoom
                return polygons.ToList();
            }

            if (_geoJsonMultiData.TryGetValue(countryId, out polygons))
            {
                return polygons.ToList();
            }
        }

        // Fallback: Einzelnes Polygon als Liste zurückgeben
        var singlePolygon = GetCountryCoordinates(countryId);
        if (singlePolygon.Length > 0)
        {
            return new List<double[][]> { singlePolygon };
        }

        return new List<double[][]>();
    }

    /// <summary>
    /// Manuelle Koordinaten als Fallback (aus JSON geladen)
    /// </summary>
    private static double[][] GetManualCoordinates(string countryId)
    {
        EnsureManualCoordsLoaded();

        if (_manualCoordinates != null &&
            _manualCoordinates.TryGetValue(countryId, out var coords))
        {
            return coords;
        }

        return Array.Empty<double[]>();
    }

    /// <summary>
    /// Lädt manuelle Koordinaten aus JSON-Datei
    /// </summary>
    private static void EnsureManualCoordsLoaded()
    {
        if (_manualCoordsLoaded) return;
        _manualCoordsLoaded = true;

        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ManualCoordsPath);
        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(Directory.GetCurrentDirectory(), ManualCoordsPath);
        }

        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double[][]>>(json);
                if (raw != null)
                {
                    _manualCoordinates = raw;
                    Console.WriteLine($"Manuelle Koordinaten: {_manualCoordinates.Count} Laender geladen");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden manueller Koordinaten: {ex.Message}");
            }
        }
    }
}
