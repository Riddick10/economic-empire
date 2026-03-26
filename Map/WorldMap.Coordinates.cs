using System.Numerics;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Koordinaten-Transformation und Hilfsfunktionen
/// </summary>
public partial class WorldMap
{
    /// <summary>
    /// Sucht eine Datendatei im Data-Ordner
    /// </summary>
    private static string? FindDataFile(string fileName)
    {
        // Versuche verschiedene Pfade
        string[] searchPaths = {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data", fileName)
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Konvertiert geografische Koordinaten (Lon/Lat) zu Bildschirmkoordinaten
    /// </summary>
    private Vector2[] ConvertGeoToScreen(double[][] geoCoords)
    {
        var points = new Vector2[geoCoords.Length];

        for (int i = 0; i < geoCoords.Length; i++)
        {
            double lon = geoCoords[i][0];
            double lat = geoCoords[i][1];

            // Einfache equirektangulaere Projektion
            // Lon: -180 bis 180 -> 0 bis MAP_WIDTH
            // Lat: MAX_LAT bis MIN_LAT -> 0 bis MAP_HEIGHT (invertiert, da Y nach unten waechst)

            float x = (float)((lon - MIN_LON) / (MAX_LON - MIN_LON) * MAP_WIDTH);
            float y = (float)((MAX_LAT - lat) / (MAX_LAT - MIN_LAT) * MAP_HEIGHT);

            points[i] = new Vector2(x, y);
        }

        return points;
    }

    /// <summary>
    /// Vereinfacht und konvertiert geografische Koordinaten zu Bildschirmkoordinaten
    /// Nutzt Douglas-Peucker Algorithmus fuer effiziente Polygon-Vereinfachung
    /// </summary>
    /// <param name="geoCoords">Geografische Koordinaten (Lon/Lat)</param>
    /// <param name="tolerance">Vereinfachungs-Toleranz in Grad (0.05 = ~5km, 0.1 = ~11km)</param>
    private Vector2[] SimplifyAndConvertGeoToScreen(double[][] geoCoords, double tolerance = 0.08)
    {
        // Vereinfache zuerst mit Douglas-Peucker
        var simplified = GeoJsonLoader.SimplifyPolygon(geoCoords, tolerance);
        return ConvertGeoToScreen(simplified);
    }

    /// <summary>
    /// Konvertiert einen einzelnen geografischen Punkt (Lon/Lat) zu Kartenkoordinaten
    /// </summary>
    private Vector2 GeoPointToMap(double lon, double lat)
    {
        float x = (float)((lon - MIN_LON) / (MAX_LON - MIN_LON) * MAP_WIDTH);
        float y = (float)((MAX_LAT - lat) / (MAX_LAT - MIN_LAT) * MAP_HEIGHT);
        return new Vector2(x, y);
    }

    /// <summary>
    /// Konvertiert Bildschirm- zu Kartenkoordinaten
    /// </summary>
    public Vector2 ScreenToMap(Vector2 screenPos)
    {
        return (screenPos - Offset) / Zoom;
    }

    /// <summary>
    /// Konvertiert Karten- zu Bildschirmkoordinaten
    /// </summary>
    public Vector2 MapToScreen(Vector2 mapPos)
    {
        return mapPos * Zoom + Offset;
    }

    private Vector2 CalculateCenter(Vector2[] points)
        => PolygonUtils.CalculateCenter(points);
}
