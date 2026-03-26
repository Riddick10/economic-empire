using System.Text.Json;

namespace GrandStrategyGame;

/// <summary>
/// Lädt und parst GeoJSON-Dateien für präzise Ländergrenzen
/// </summary>
public static class GeoJsonLoader
{
    /// <summary>
    /// Lädt Ländergrenzen aus einer GeoJSON-Datei (alle Polygone pro Land)
    /// </summary>
    public static Dictionary<string, List<double[][]>> LoadCountriesMultiPolygon(string filePath)
    {
        var countries = new Dictionary<string, List<double[][]>>();

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"GeoJSON-Datei nicht gefunden: {filePath}");
            return countries;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    string? countryCode = GetCountryCode(feature);
                    if (countryCode == null) continue;

                    var polygons = ExtractAllPolygons(feature);
                    if (polygons.Count > 0)
                    {
                        countries[countryCode] = polygons;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Laden der GeoJSON-Datei: {ex.Message}");
        }

        return countries;
    }

    /// <summary>
    /// Lädt Ländergrenzen (nur größtes Polygon - für Kompatibilität)
    /// </summary>
    public static Dictionary<string, double[][]> LoadCountries(string filePath)
    {
        var multiPolygon = LoadCountriesMultiPolygon(filePath);
        var countries = new Dictionary<string, double[][]>();

        foreach (var (code, polygons) in multiPolygon)
        {
            // Nimm das größte Polygon
            double[][] largest = Array.Empty<double[]>();
            double maxArea = 0;
            foreach (var poly in polygons)
            {
                double area = CalculatePolygonArea(poly);
                if (area > maxArea)
                {
                    maxArea = area;
                    largest = poly;
                }
            }
            if (largest.Length > 0)
                countries[code] = largest;
        }

        return countries;
    }

    /// <summary>
    /// Extrahiert ALLE Polygone aus einem Feature (für MultiPolygon-Unterstützung)
    /// </summary>
    private static List<double[][]> ExtractAllPolygons(JsonElement feature)
    {
        var result = new List<double[][]>();

        if (!feature.TryGetProperty("geometry", out var geometry))
            return result;

        if (!geometry.TryGetProperty("type", out var typeElement))
            return result;

        string? type = typeElement.GetString();
        if (!geometry.TryGetProperty("coordinates", out var coordinates))
            return result;

        if (type == "Polygon")
        {
            var coords = ExtractPolygonCoordinates(coordinates);
            if (coords != null && coords.Length > 0)
                result.Add(coords);
        }
        else if (type == "MultiPolygon")
        {
            // ALLE Polygone extrahieren, nicht nur das größte
            foreach (var polygon in coordinates.EnumerateArray())
            {
                if (polygon.GetArrayLength() == 0) continue;

                var outerRing = polygon[0];
                var coords = ExtractRingCoordinates(outerRing);

                if (coords != null && coords.Length > 0)
                {
                    // Nur Polygone mit signifikanter Größe hinzufügen (> 0.1 Quadratgrad)
                    double area = CalculatePolygonArea(coords);
                    if (area > 0.1)
                    {
                        result.Add(coords);
                    }
                }
            }
        }

        return result;
    }

    private static string? GetCountryCode(JsonElement feature)
    {
        if (!feature.TryGetProperty("properties", out var properties))
            return null;

        // Versuche verschiedene Property-Namen für den ISO-Code
        string[] codeProperties = { "ISO_A3", "iso_a3", "ADM0_A3", "adm0_a3", "ISO3", "iso3", "SOV_A3" };

        foreach (var prop in codeProperties)
        {
            if (properties.TryGetProperty(prop, out var code))
            {
                string? codeStr = code.GetString();
                if (!string.IsNullOrEmpty(codeStr) && codeStr != "-99")
                    return codeStr;
            }
        }

        return null;
    }

    private static double[][]? ExtractPolygonCoordinates(JsonElement coordinates)
    {
        // Ein Polygon hat [[ring1], [ring2], ...] - wir nehmen den äußeren Ring (erster)
        if (coordinates.GetArrayLength() == 0)
            return null;

        var outerRing = coordinates[0];
        return ExtractRingCoordinates(outerRing);
    }

    private static double[][] ExtractRingCoordinates(JsonElement ring)
    {
        var points = new List<double[]>();

        foreach (var point in ring.EnumerateArray())
        {
            if (point.GetArrayLength() >= 2)
            {
                double lon = point[0].GetDouble();
                double lat = point[1].GetDouble();
                points.Add(new[] { lon, lat });
            }
        }

        return points.ToArray();
    }

    private static double CalculatePolygonArea(double[][] coords)
    {
        // Shoelace-Formel für Fläche (vereinfacht, ohne Projektion)
        double area = 0;
        int n = coords.Length;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += coords[i][0] * coords[j][1];
            area -= coords[j][0] * coords[i][1];
        }

        return Math.Abs(area) / 2;
    }

    /// <summary>
    /// Vereinfacht ein Polygon mit dem Douglas-Peucker Algorithmus
    /// Erhält die Form viel besser als einfache Distanz-Filterung
    /// </summary>
    public static double[][] SimplifyPolygon(double[][] coords, double tolerance = 0.1)
    {
        if (coords.Length <= 4)
            return coords;

        // Douglas-Peucker Algorithmus
        var result = DouglasPeucker(coords, 0, coords.Length - 1, tolerance);

        // Stelle sicher, dass das Polygon geschlossen ist
        if (result.Count > 0 &&
            (result[0][0] != result[^1][0] || result[0][1] != result[^1][1]))
        {
            result.Add(result[0]);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Douglas-Peucker Rekursion - findet die wichtigsten Punkte zur Formerhaltung
    /// </summary>
    private static List<double[]> DouglasPeucker(double[][] points, int startIndex, int endIndex, double tolerance)
    {
        if (endIndex <= startIndex + 1)
        {
            var result = new List<double[]> { points[startIndex] };
            if (endIndex != startIndex)
                result.Add(points[endIndex]);
            return result;
        }

        // Finde den Punkt mit maximaler Distanz zur Linie
        double maxDistance = 0;
        int maxIndex = startIndex;

        double[] start = points[startIndex];
        double[] end = points[endIndex];

        for (int i = startIndex + 1; i < endIndex; i++)
        {
            double dist = PerpendicularDistance(points[i], start, end);
            if (dist > maxDistance)
            {
                maxDistance = dist;
                maxIndex = i;
            }
        }

        // Wenn maximale Distanz größer als Toleranz, rekursiv vereinfachen
        if (maxDistance > tolerance)
        {
            var left = DouglasPeucker(points, startIndex, maxIndex, tolerance);
            var right = DouglasPeucker(points, maxIndex, endIndex, tolerance);

            // Kombiniere Ergebnisse (ohne Duplikat in der Mitte)
            var combined = new List<double[]>(left);
            combined.RemoveAt(combined.Count - 1); // Entferne letzten Punkt der linken Seite
            combined.AddRange(right);
            return combined;
        }
        else
        {
            // Behalte nur Start- und Endpunkt
            return new List<double[]> { points[startIndex], points[endIndex] };
        }
    }

    /// <summary>
    /// Berechnet die senkrechte Distanz eines Punktes zu einer Linie
    /// </summary>
    private static double PerpendicularDistance(double[] point, double[] lineStart, double[] lineEnd)
    {
        double dx = lineEnd[0] - lineStart[0];
        double dy = lineEnd[1] - lineStart[1];

        // Länge der Linie
        double lineLengthSq = dx * dx + dy * dy;

        if (lineLengthSq == 0)
        {
            // Start und End sind identisch
            return Math.Sqrt(
                Math.Pow(point[0] - lineStart[0], 2) +
                Math.Pow(point[1] - lineStart[1], 2)
            );
        }

        // Berechne senkrechte Distanz mit Kreuzprodukt
        double area = Math.Abs(
            (lineEnd[1] - lineStart[1]) * point[0] -
            (lineEnd[0] - lineStart[0]) * point[1] +
            lineEnd[0] * lineStart[1] -
            lineEnd[1] * lineStart[0]
        );

        return area / Math.Sqrt(lineLengthSq);
    }

    /// <summary>
    /// Mapping von Natural Earth Codes zu unseren ISO-Codes
    /// </summary>
    public static readonly Dictionary<string, string> CodeMapping = new()
    {
        // Sonderfälle und abweichende Codes
        { "KOS", "XKX" },  // Kosovo
        { "SDS", "SSD" },  // Südsudan
        { "SOL", "SOM" },  // Somaliland → Somalia
        { "CYN", "CYP" },  // Nordzypern → Zypern
        { "SAH", "ESH" },  // Westsahara
    };

    /// <summary>
    /// Normalisiert einen Ländercode
    /// </summary>
    public static string NormalizeCode(string code)
    {
        if (CodeMapping.TryGetValue(code, out var mapped))
            return mapped;
        return code;
    }

    /// <summary>
    /// Lädt deutsche Bundesländer aus einer GeoJSON-Datei
    /// Gibt Dictionary mit ID (z.B. "DE-BW") und (Name, Polygone) zurück
    /// </summary>
    public static Dictionary<string, (string Name, List<double[][]> Polygons)> LoadGermanStates(string filePath)
    {
        var states = new Dictionary<string, (string Name, List<double[][]> Polygons)>();

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Bundesländer-GeoJSON nicht gefunden: {filePath}");
            return states;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    if (!feature.TryGetProperty("properties", out var properties))
                        continue;

                    // ID und Name aus Properties extrahieren
                    string? stateId = null;
                    string? stateName = null;

                    if (properties.TryGetProperty("id", out var idProp))
                        stateId = idProp.GetString();
                    if (properties.TryGetProperty("name", out var nameProp))
                        stateName = nameProp.GetString();

                    if (string.IsNullOrEmpty(stateId) || string.IsNullOrEmpty(stateName))
                        continue;

                    var polygons = ExtractAllPolygons(feature);
                    if (polygons.Count > 0)
                    {
                        states[stateId] = (stateName, polygons);
                    }
                }
            }

            Console.WriteLine($"Bundesländer-GeoJSON: {states.Count} Bundesländer geladen");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Laden der Bundesländer-GeoJSON: {ex.Message}");
        }

        return states;
    }

    /// <summary>
    /// Lädt Provinzen/Regionen eines Landes aus einer GeoJSON-Datei
    /// Unterstützt verschiedene GeoJSON-Formate (Natural Earth, GADM, etc.)
    /// </summary>
    public static Dictionary<string, (string Name, List<double[][]> Polygons)> LoadProvinces(string filePath, string countryPrefix)
    {
        var provinces = new Dictionary<string, (string Name, List<double[][]> Polygons)>();

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Provinz-GeoJSON nicht gefunden: {filePath}");
            return provinces;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("features", out var features))
            {
                int index = 0;
                foreach (var feature in features.EnumerateArray())
                {
                    if (!feature.TryGetProperty("properties", out var properties))
                        continue;

                    // Versuche verschiedene Property-Namen für den Namen
                    // Bevorzuge deutsche Namen (name:de) da die Schrift keine Kyrillisch unterstützt
                    // "nam" wird von Japan GeoJSON verwendet
                    string? provinceName = null;
                    string[] nameProperties = { "Maakunta", "name:de", "nam", "NAME_1", "name", "Name", "NAME", "VARNAME_1", "NL_NAME_1", "shapeName", "reg_name" };

                    foreach (var prop in nameProperties)
                    {
                        if (properties.TryGetProperty(prop, out var nameProp))
                        {
                            provinceName = nameProp.GetString();
                            if (!string.IsNullOrEmpty(provinceName))
                                break;
                        }
                    }

                    if (string.IsNullOrEmpty(provinceName))
                        continue;

                    // Generiere eine ID
                    string provinceId = $"{countryPrefix}-{index:D2}";
                    index++;

                    var polygons = ExtractAllPolygons(feature);
                    if (polygons.Count > 0)
                    {
                        provinces[provinceId] = (provinceName, polygons);
                    }
                }
            }

            Console.WriteLine($"Provinz-GeoJSON ({countryPrefix}): {provinces.Count} Regionen geladen");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Laden der Provinz-GeoJSON: {ex.Message}");
        }

        return provinces;
    }
}
