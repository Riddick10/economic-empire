using System.Numerics;
using Raylib_cs;

namespace GrandStrategyGame.Map;

/// <summary>
/// HOI4-Style Bitmap-Lookup System fuer schnelle Positionsabfragen
///
/// Funktionsweise:
/// 1. Beim Laden wird ein ID-Bitmap generiert (gleiche Groesse wie Karte)
/// 2. Jedes Land/Provinz wird mit einer eindeutigen Farbe (=ID) gezeichnet
/// 3. Bei Mausabfrage: Pixel lesen → ID dekodieren → Entity zurueckgeben
///
/// Performance: O(1) statt O(n) fuer Hover/Klick-Erkennung
/// </summary>
public class MapLookup : IDisposable
{
    // ID-Bitmap (CPU-seitig fuer schnellen Pixel-Zugriff)
    private Image _idBitmap;
    private bool _isInitialized = false;

    // Lookup-Tabellen: ID → Entity
    private readonly Dictionary<int, string> _idToCountry = new();
    private readonly Dictionary<int, string> _idToProvince = new();

    // Reverse-Lookup: Entity → ID (fuer Rendering)
    private readonly Dictionary<string, int> _countryToId = new();
    private readonly Dictionary<string, int> _provinceToId = new();

    // ID-Bereiche
    private const int OCEAN_ID = 0;
    private const int COUNTRY_ID_START = 1;
    private const int COUNTRY_ID_END = 999;
    private const int PROVINCE_ID_START = 1000;

    // Karten-Dimensionen (muessen mit WorldMap uebereinstimmen)
    private readonly int _width;
    private readonly int _height;

    public bool IsInitialized => _isInitialized;

    public MapLookup(int width, int height)
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Generiert das ID-Bitmap basierend auf den geladenen Regionen und Provinzen
    /// </summary>
    public void Generate(Dictionary<string, MapRegion> regions, Dictionary<string, Province> provinces)
    {
        Console.WriteLine("MapLookup: Generiere ID-Bitmap...");

        // Altes Bitmap freigeben falls vorhanden
        if (_isInitialized)
        {
            Raylib.UnloadImage(_idBitmap);
        }

        // Neues Bitmap erstellen (Ozean = ID 0 = Schwarz)
        _idBitmap = Raylib.GenImageColor(_width, _height, Color.Black);

        // IDs zuweisen und Lookup-Tabellen fuellen
        AssignIds(regions, provinces);

        // Laender zeichnen (zuerst, da Provinzen drueberliegen)
        DrawCountries(regions);

        // Provinzen zeichnen (ueberschreiben Laender-Pixel)
        DrawProvinces(provinces);

        _isInitialized = true;
        Console.WriteLine($"MapLookup: {_idToCountry.Count} Laender, {_idToProvince.Count} Provinzen registriert");
    }

    /// <summary>
    /// Weist allen Laendern und Provinzen eindeutige IDs zu
    /// </summary>
    private void AssignIds(Dictionary<string, MapRegion> regions, Dictionary<string, Province> provinces)
    {
        _idToCountry.Clear();
        _idToProvince.Clear();
        _countryToId.Clear();
        _provinceToId.Clear();

        // Laender-IDs (1-999)
        int countryId = COUNTRY_ID_START;
        foreach (var countryCode in regions.Keys.OrderBy(k => k))
        {
            if (countryId > COUNTRY_ID_END)
            {
                Console.WriteLine($"MapLookup: Warnung - Zu viele Laender, {countryCode} wird uebersprungen");
                continue;
            }

            _idToCountry[countryId] = countryCode;
            _countryToId[countryCode] = countryId;
            countryId++;
        }

        // Provinz-IDs (1000+)
        int provinceId = PROVINCE_ID_START;
        foreach (var provinceCode in provinces.Keys.OrderBy(k => k))
        {
            _idToProvince[provinceId] = provinceCode;
            _provinceToId[provinceCode] = provinceId;
            provinceId++;
        }
    }

    /// <summary>
    /// Zeichnet alle Laender ins ID-Bitmap
    /// </summary>
    private void DrawCountries(Dictionary<string, MapRegion> regions)
    {
        foreach (var (countryCode, region) in regions)
        {
            if (!_countryToId.TryGetValue(countryCode, out int id))
                continue;

            Color idColor = IdToColor(id);

            // Zeichne alle Polygon-Ringe
            foreach (var ring in region.PolygonRings)
            {
                if (ring.Length < 3) continue;
                DrawFilledPolygon(ring, idColor);
            }
        }
    }

    /// <summary>
    /// Zeichnet alle Provinzen ins ID-Bitmap
    /// </summary>
    private void DrawProvinces(Dictionary<string, Province> provinces)
    {
        foreach (var (provinceCode, province) in provinces)
        {
            if (!_provinceToId.TryGetValue(provinceCode, out int id))
                continue;

            Color idColor = IdToColor(id);

            // Zeichne alle Polygon-Ringe
            foreach (var ring in province.PolygonRings)
            {
                if (ring.Length < 3) continue;
                DrawFilledPolygon(ring, idColor);
            }
        }
    }

    /// <summary>
    /// Zeichnet ein gefuelltes Polygon ins ID-Bitmap (Scanline-Algorithmus)
    /// </summary>
    private void DrawFilledPolygon(Vector2[] polygon, Color color)
    {
        if (polygon.Length < 3) return;

        // Bounding Box berechnen
        float minY = float.MaxValue, maxY = float.MinValue;
        float minX = float.MaxValue, maxX = float.MinValue;

        foreach (var p in polygon)
        {
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
        }

        // Clamp auf Bitmap-Grenzen
        int startY = Math.Max(0, (int)minY);
        int endY = Math.Min(_height - 1, (int)maxY);
        int startX = Math.Max(0, (int)minX);
        int endX = Math.Min(_width - 1, (int)maxX);

        // Scanline-Fuellen
        for (int y = startY; y <= endY; y++)
        {
            // Finde alle Schnittpunkte mit dieser Scanline
            var intersections = new List<float>();

            for (int i = 0; i < polygon.Length; i++)
            {
                int j = (i + 1) % polygon.Length;
                var p1 = polygon[i];
                var p2 = polygon[j];

                // Pruefe ob die Kante diese Y-Koordinate schneidet
                if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                {
                    // Berechne X-Koordinate des Schnittpunkts
                    float t = (y - p1.Y) / (p2.Y - p1.Y);
                    float x = p1.X + t * (p2.X - p1.X);
                    intersections.Add(x);
                }
            }

            // Sortiere Schnittpunkte
            intersections.Sort();

            // Fuelle zwischen Paaren von Schnittpunkten
            for (int i = 0; i + 1 < intersections.Count; i += 2)
            {
                int xStart = Math.Max(startX, (int)intersections[i]);
                int xEnd = Math.Min(endX, (int)intersections[i + 1]);

                for (int x = xStart; x <= xEnd; x++)
                {
                    Raylib.ImageDrawPixel(ref _idBitmap, x, y, color);
                }
            }
        }
    }

    /// <summary>
    /// Findet die Provinz an einer Kartenposition (O(1) Lookup!)
    /// </summary>
    public string? GetProvinceAt(Vector2 mapPos)
    {
        if (!_isInitialized) return null;

        int id = GetIdAt(mapPos);
        if (id == OCEAN_ID) return null;

        // Pruefe ob es eine Provinz ist
        if (_idToProvince.TryGetValue(id, out string? province))
            return province;

        return null;
    }

    /// <summary>
    /// Findet die Entity (Land oder Provinz) an einer Kartenposition
    /// Gibt zurueck: (countryId, provinceId) - provinceId kann null sein
    /// </summary>
    public (string? countryId, string? provinceId) GetEntityAt(Vector2 mapPos)
    {
        if (!_isInitialized) return (null, null);

        int id = GetIdAt(mapPos);
        if (id == OCEAN_ID) return (null, null);

        // Pruefe ob es eine Provinz ist
        if (_idToProvince.TryGetValue(id, out string? province))
        {
            // Provinz gefunden - gib sowohl Land als auch Provinz zurueck
            // Das Land wird spaeter ueber die Province-Daten aufgeloest
            return (null, province);
        }

        // Pruefe ob es ein Land ist
        if (_idToCountry.TryGetValue(id, out string? country))
        {
            return (country, null);
        }

        return (null, null);
    }

    /// <summary>
    /// Liest die ID an einer Kartenposition
    /// </summary>
    private int GetIdAt(Vector2 mapPos)
    {
        int x = (int)mapPos.X;
        int y = (int)mapPos.Y;

        // Grenzen pruefen
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return OCEAN_ID;

        Color pixel = Raylib.GetImageColor(_idBitmap, x, y);
        return ColorToId(pixel);
    }

    /// <summary>
    /// Kodiert eine ID als Farbe (16-Bit in R+G Kanaelen)
    /// </summary>
    private static Color IdToColor(int id)
    {
        byte r = (byte)(id & 0xFF);
        byte g = (byte)((id >> 8) & 0xFF);
        return new Color(r, g, (byte)0, (byte)255);
    }

    /// <summary>
    /// Dekodiert eine Farbe als ID
    /// </summary>
    private static int ColorToId(Color color)
    {
        return color.R + (color.G << 8);
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            Raylib.UnloadImage(_idBitmap);
            _isInitialized = false;
        }
    }
}
