using System.Linq;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;

using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Provinz-Rendering und Lookup-Methoden
/// </summary>
public partial class WorldMap
{
    // Cache fuer bereits gezeichnete Provinzgrenzen-Kanten (verhindert Ueberlappung)
    private HashSet<long>? _drawnProvinceEdges;

    /// <summary>
    /// Findet das Land an einer Bildschirmposition
    /// Nutzt HOI4-Style Bitmap-Lookup fuer O(1) Performance
    /// </summary>
    public string? GetCountryAtPosition(Vector2 screenPos)
    {
        Vector2 mapPos = ScreenToMap(screenPos);

        // Primaer: Schnelles Bitmap-Lookup (O(1))
        if (_mapLookup != null && _mapLookup.IsInitialized)
        {
            var (countryId, provinceId) = _mapLookup.GetEntityAt(mapPos);

            // Wenn Provinz gefunden, gib das zugehoerige Land zurueck
            if (provinceId != null && Provinces.TryGetValue(provinceId, out var province))
            {
                return province.CountryId;
            }

            // Direkt Land gefunden
            if (countryId != null)
            {
                return countryId;
            }

            return null;
        }

        // Fallback: Alte Methode (O(n)) - nur wenn Lookup nicht verfuegbar
        return GetCountryAtPositionLegacy(mapPos);
    }

    /// <summary>
    /// Findet die Provinz an einer Bildschirmposition
    /// Nutzt HOI4-Style Bitmap-Lookup fuer O(1) Performance
    /// </summary>
    public string? GetProvinceAtPosition(Vector2 screenPos)
    {
        if (_mapLookup == null || !_mapLookup.IsInitialized)
            return null;

        Vector2 mapPos = ScreenToMap(screenPos);
        return _mapLookup.GetProvinceAt(mapPos);
    }

    /// <summary>
    /// Gibt eine Provinz per ID zurueck
    /// </summary>
    public Province? GetProvinceById(string provinceId)
    {
        if (Provinces.TryGetValue(provinceId, out var province))
            return province;
        return null;
    }

    /// <summary>
    /// Legacy-Methode: Findet Land per Polygon-Test (O(n))
    /// Nur als Fallback wenn Bitmap-Lookup nicht verfuegbar
    /// </summary>
    private string? GetCountryAtPositionLegacy(Vector2 mapPos)
    {
        string? smallestCountry = null;
        float smallestArea = float.MaxValue;

        foreach (var (countryId, region) in Regions)
        {
            if (region.ContainsPoint(mapPos))
            {
                if (region.CachedArea < smallestArea)
                {
                    smallestArea = region.CachedArea;
                    smallestCountry = countryId;
                }
            }
        }

        return smallestCountry;
    }

    /// <summary>
    /// Zeichnet alle Provinzgrenzen ohne Ueberlappung
    /// Zeichnet alle Ringe jeder Provinz (GeoJsonLoader extrahiert nur aeussere Ringe)
    /// </summary>
    private void DrawAllProvinceBorders(IEnumerable<Province> provinces)
    {
        // HashSet fuer bereits gezeichnete Kanten zuruecksetzen
        _drawnProvinceEdges ??= new HashSet<long>();
        _drawnProvinceEdges.Clear();

        // Provinzgrenzen sind duenner und heller als Landesgrenzen
        float borderWidth = GetAdaptiveBorderWidth(0.25f);  // 25% der Landesgrenz-Dicke
        Color borderColor = new Color((byte)60, (byte)60, (byte)80, (byte)180);  // Grau, leicht transparent

        // Maximale Linienlaenge um Artefakte zu vermeiden
        float maxLineLength = MAP_WIDTH * Zoom * 0.45f;
        float maxLineLengthSq = maxLineLength * maxLineLength;

        foreach (var province in provinces)
        {
            if (province.TransformedRings == null || province.TransformedRings.Count == 0) continue;

            // Alle Ringe zeichnen (GeoJsonLoader extrahiert nur aeussere Ringe,
            // daher sind alle Ringe legitime Grenzen - wichtig fuer MultiPolygon-Provinzen
            // wie z.B. zusammengefuehrte tuerkische Regionen oder Inseln)
            foreach (var transformedPoints in province.TransformedRings)
            {
                if (transformedPoints.Length < 3) continue;

                for (int i = 0; i < transformedPoints.Length; i++)
                {
                    int next = (i + 1) % transformedPoints.Length;

                    float dx = transformedPoints[next].X - transformedPoints[i].X;
                    float dy = transformedPoints[next].Y - transformedPoints[i].Y;
                    if (dx * dx + dy * dy > maxLineLengthSq)
                        continue;

                    // Kanten-Hash berechnen (gerundet auf 1 Pixel Genauigkeit)
                    long edgeHash = GetEdgeHash(transformedPoints[i], transformedPoints[next]);

                    // Nur zeichnen wenn Kante noch nicht gezeichnet wurde
                    if (_drawnProvinceEdges.Add(edgeHash))
                    {
                        Raylib.DrawLineEx(transformedPoints[i], transformedPoints[next], borderWidth, borderColor);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Berechnet einen eindeutigen Hash fuer eine Kante (unabhaengig von Richtung)
    /// </summary>
    private static long GetEdgeHash(Vector2 p1, Vector2 p2)
    {
        // Runde auf 1 Pixel Genauigkeit
        int x1 = (int)Math.Round(p1.X);
        int y1 = (int)Math.Round(p1.Y);
        int x2 = (int)Math.Round(p2.X);
        int y2 = (int)Math.Round(p2.Y);

        // Normalisiere Reihenfolge (kleinerer Punkt zuerst) fuer konsistenten Hash
        if (x1 > x2 || (x1 == x2 && y1 > y2))
        {
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }

        // Kombiniere zu 64-bit Hash (16 bit pro Koordinate)
        return ((long)(x1 & 0xFFFF) << 48) | ((long)(y1 & 0xFFFF) << 32) |
               ((long)(x2 & 0xFFFF) << 16) | (long)(y2 & 0xFFFF);
    }

    /// <summary>
    /// Zeichnet eine Provinz-Hervorhebung (Fill + Rahmen)
    /// </summary>
    private void DrawProvinceHighlight(Province province, bool isSelected)
    {
        if (province.TransformedRings == null || province.TransformedRings.Count == 0) return;

        // Maximale Linienlaenge um Artefakte zu vermeiden
        float maxLineLength = MAP_WIDTH * Zoom * 0.45f;
        float maxLineLengthSq = maxLineLength * maxLineLength;

        // Farbe fuer ausgewaehlte Provinz: Blau
        Color fillColor = new Color((byte)80, (byte)150, (byte)255, (byte)30);   // Blau sehr transparent
        Color borderColor = new Color((byte)80, (byte)150, (byte)255, (byte)255); // Blau

        float borderWidth = isSelected
            ? GetAdaptiveBorderWidth(0.8f)
            : GetAdaptiveBorderWidth(0.5f);

        // Zeichne Fill fuer alle Ringe mit Dreiecken
        for (int ringIndex = 0; ringIndex < province.TransformedRings.Count; ringIndex++)
        {
            var transformedPoints = province.TransformedRings[ringIndex];
            if (transformedPoints.Length < 3) continue;

            // Nutze vorberechnete Triangulation
            if (ringIndex < province.TrianglesPerRing.Count)
            {
                var triangles = province.TrianglesPerRing[ringIndex];
                foreach (var (a, b, c) in triangles)
                {
                    // Pruefen auf Wrap-Around-Artefakte
                    float dx1 = transformedPoints[b].X - transformedPoints[a].X;
                    float dy1 = transformedPoints[b].Y - transformedPoints[a].Y;
                    float dx2 = transformedPoints[c].X - transformedPoints[a].X;
                    float dy2 = transformedPoints[c].Y - transformedPoints[a].Y;

                    if (dx1 * dx1 + dy1 * dy1 > maxLineLengthSq) continue;
                    if (dx2 * dx2 + dy2 * dy2 > maxLineLengthSq) continue;

                    Raylib.DrawTriangle(
                        transformedPoints[a],
                        transformedPoints[b],
                        transformedPoints[c],
                        fillColor
                    );
                }
            }
        }

        // Zeichne Rahmen fuer alle Ringe
        foreach (var transformedPoints in province.TransformedRings)
        {
            if (transformedPoints.Length < 3) continue;

            for (int i = 0; i < transformedPoints.Length; i++)
            {
                int next = (i + 1) % transformedPoints.Length;

                float dx = transformedPoints[next].X - transformedPoints[i].X;
                float dy = transformedPoints[next].Y - transformedPoints[i].Y;
                if (dx * dx + dy * dy > maxLineLengthSq) continue;

                Raylib.DrawLineEx(transformedPoints[i], transformedPoints[next], borderWidth, borderColor);
            }
        }
    }

    /// <summary>
    /// Zeichnet das Label einer Provinz (nur bei hohem Zoom)
    /// </summary>
    private void DrawProvinceLabel(Province province)
    {
        // Nutze gecachte transformierte Punkte
        if (province.TransformedRings == null || province.TransformedRings.Count == 0) return;

        var transformedPoints = province.TransformedRings[0];
        if (transformedPoints.Length < 3) return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in transformedPoints)
        {
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
        }
        float provinceWidth = maxX - minX;
        float provinceHeight = maxY - minY;

        // Zu kleine Provinzen ueberspringen
        if (provinceWidth < 40 || provinceHeight < 25) return;

        // Schriftgroesse basierend auf Provinzgroesse (kleiner als Laendernamen)
        float provinceSize = Math.Min(provinceWidth, provinceHeight);
        int fontSize = (int)(provinceSize * 0.14f);
        fontSize = Math.Clamp(fontSize, 8, 16);

        int textWidth = Program.MeasureGameText(province.Name, fontSize);

        // Text zu breit? Kuerzen
        if (textWidth > provinceWidth * 0.8f)
        {
            fontSize = (int)(fontSize * provinceWidth * 0.8f / textWidth);
            fontSize = Math.Max(fontSize, 8);
            textWidth = Program.MeasureGameText(province.Name, fontSize);
        }

        // Nutze gecachte transformierte Label-Position
        Vector2 screenPos = province.TransformedLabelPos;
        int textX = (int)(screenPos.X - textWidth / 2);
        int textY = (int)(screenPos.Y - fontSize / 2);

        // Dezenter Schatten
        Color shadowColor = new Color(0, 0, 0, 100);
        Program.DrawGameText(province.Name, textX + 1, textY + 1, fontSize, shadowColor);

        // Text in gedaempftem Weiss
        Color textColor = new Color(220, 220, 230, 220);
        Program.DrawGameText(province.Name, textX, textY, fontSize, textColor);
    }

    /// <summary>
    /// Zeichnet Hauptstaedte als rote Punkte mit Namen
    /// </summary>
    private void DrawCapitals()
    {
        // Minimaler Zoom fuer Hauptstadt-Anzeige (erst bei staerkerem Zoom sichtbar)
        if (Zoom < 4.0f) return;

        Color capitalColor = new Color(220, 60, 60, 255);  // Rot
        Color textColor = new Color(240, 240, 240, 255);   // Weiss
        Color shadowColor = new Color(0, 0, 0, 180);       // Schwarz fuer Schatten

        int dotRadius = Zoom >= 4.0f ? 4 : 3;
        int fontSize = Zoom >= 4.0f ? 12 : 10;

        foreach (var (countryId, capital) in Capitals)
        {
            // Nur zeichnen wenn das Land existiert
            if (!Regions.ContainsKey(countryId)) continue;

            // Geo-Koordinaten zu Bildschirmkoordinaten
            Vector2 mapPos = GeoPointToMap(capital.Lon, capital.Lat);
            Vector2 screenPos = MapToScreen(mapPos);

            // Frustum Culling - nur zeichnen wenn sichtbar
            int screenW = Raylib.GetScreenWidth();
            int screenH = Raylib.GetScreenHeight();
            if (screenPos.X < -50 || screenPos.X > screenW + 50 ||
                screenPos.Y < -50 || screenPos.Y > screenH + 50)
                continue;

            // Roter Punkt
            Raylib.DrawCircleV(screenPos, dotRadius, capitalColor);

            // Name der Hauptstadt (nur bei hohem Zoom)
            if (Zoom >= 5.0f)
            {
                int textX = (int)screenPos.X + dotRadius + 3;
                int textY = (int)screenPos.Y - fontSize / 2;

                // Schatten
                Program.DrawGameText(capital.Name, textX + 1, textY + 1, fontSize, shadowColor);
                // Text
                Program.DrawGameText(capital.Name, textX, textY, fontSize, textColor);
            }
        }
    }

    /// <summary>
    /// Gibt die Bodenschätze einer Provinz mit Heatmap-Wert zurück
    /// </summary>
    public List<(ResourceType Type, string Name, float Value)> GetProvinceResourcesWithValues(string provinceName, string countryId)
    {
        var countryRes = ResourceAbundance.GetCountryResources(countryId);

        const float fallback = 0.05f;
        var resources = new List<(ResourceType, string, float)>
        {
            (ResourceType.Oil, "Erdoel", OilProvinceNames.Contains(provinceName) ? countryRes.Oil : countryRes.Oil * fallback),
            (ResourceType.NaturalGas, "Erdgas", NaturalGasProvinceNames.Contains(provinceName) ? countryRes.NaturalGas : countryRes.NaturalGas * fallback),
            (ResourceType.Coal, "Kohle", CoalProvinceNames.Contains(provinceName) ? countryRes.Coal : countryRes.Coal * fallback),
            (ResourceType.Iron, "Eisen", IronProvinceNames.Contains(provinceName) ? countryRes.Iron : countryRes.Iron * fallback),
            (ResourceType.Copper, "Kupfer", CopperProvinceNames.Contains(provinceName) ? countryRes.Copper : countryRes.Copper * fallback),
            (ResourceType.Uranium, "Uran", UraniumProvinceNames.Contains(provinceName) ? countryRes.Uranium : countryRes.Uranium * fallback),
        };

        return resources;
    }

    /// <summary>
    /// Gibt die Bodenschätze einer Provinz zurück (basierend auf Provinz-Namen)
    /// </summary>
    public List<(ResourceType Type, string Name)> GetProvinceResources(string provinceName)
    {
        var resources = new List<(ResourceType, string)>();

        if (UraniumProvinceNames.Contains(provinceName))
            resources.Add((ResourceType.Uranium, "Uran"));

        if (IronProvinceNames.Contains(provinceName))
            resources.Add((ResourceType.Iron, "Eisen"));

        if (CoalProvinceNames.Contains(provinceName))
            resources.Add((ResourceType.Coal, "Kohle"));

        if (OilProvinceNames.Contains(provinceName))
            resources.Add((ResourceType.Oil, "Erdoel"));

        if (NaturalGasProvinceNames.Contains(provinceName))
            resources.Add((ResourceType.NaturalGas, "Erdgas"));

        // Kupfer-Vorkommen (aus gängigen Bergbau-Regionen)
        if (CopperProvinceNames.Contains(provinceName))
            resources.Add((ResourceType.Copper, "Kupfer"));

        return resources;
    }

    // CopperProvinceNames: Jetzt in WorldMap.Data.cs (geladen aus resource-deposits.json)

    /// <summary>
    /// Zeichnet Staedte als graue Quadrate mit Namen (wenn Provinznamen verschwinden)
    /// </summary>
    private void DrawCities()
    {
        // Erst anzeigen wenn Provinznamen verschwinden (ab Zoom 25)
        if (Zoom < 25.0f) return;

        // Farben
        Color citySquareColor = new Color((byte)90, (byte)90, (byte)100, (byte)255);  // Grau
        Color cityBorderColor = new Color((byte)60, (byte)60, (byte)70, (byte)255);   // Dunkelgrau
        Color textColor = new Color((byte)240, (byte)240, (byte)240, (byte)255);      // Weiss
        Color shadowColor = new Color((byte)0, (byte)0, (byte)0, (byte)180);          // Schwarz

        // Quadrat-Groesse skaliert mit Zoom
        int baseSquareSize = 6;
        int squareSize = (int)(baseSquareSize * Math.Sqrt(Zoom / 25.0f));
        squareSize = Math.Clamp(squareSize, 4, 12);

        int fontSize = Zoom >= 30.0f ? 11 : 9;

        // Nur groessere Staedte bei niedrigerem Zoom zeigen
        int minPopulation = Zoom >= 30.0f ? 100000 : 300000;

        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        foreach (var (countryId, cityList) in Cities)
        {
            // Nur zeichnen wenn das Land existiert
            if (!Regions.ContainsKey(countryId)) continue;

            foreach (var city in cityList)
            {
                // Hauptstaedte ueberspringen (werden bereits als rote Punkte gezeichnet)
                if (city.IsCapital) continue;

                // Kleine Staedte bei niedrigem Zoom ueberspringen
                if (city.Population < minPopulation) continue;

                // Geo-Koordinaten zu Bildschirmkoordinaten
                Vector2 mapPos = GeoPointToMap(city.Lon, city.Lat);
                Vector2 screenPos = MapToScreen(mapPos);

                // Frustum Culling
                if (screenPos.X < -50 || screenPos.X > screenW + 50 ||
                    screenPos.Y < -50 || screenPos.Y > screenH + 50)
                    continue;

                // Graues Quadrat zeichnen
                int squareX = (int)screenPos.X - squareSize / 2;
                int squareY = (int)screenPos.Y - squareSize / 2;

                Raylib.DrawRectangle(squareX, squareY, squareSize, squareSize, citySquareColor);
                Raylib.DrawRectangleLines(squareX, squareY, squareSize, squareSize, cityBorderColor);

                // Stadtname anzeigen
                int textX = (int)screenPos.X + squareSize / 2 + 3;
                int textY = (int)screenPos.Y - fontSize / 2;

                // Schatten
                Program.DrawGameText(city.Name, textX + 1, textY + 1, fontSize, shadowColor);
                // Text
                Program.DrawGameText(city.Name, textX, textY, fontSize, textColor);
            }
        }
    }

    /// <summary>
    /// Aktiviert/Deaktiviert Stadtlichter bei Nacht
    /// </summary>
    public bool CityLightsEnabled { get; set; } = true;

    // Cache fuer Stadt-Pixel (wird einmal pro Stadt generiert)
    private Dictionary<string, List<(Vector2 offset, float intensity)>> _cityPixels = new();
    private Random _pixelRandom = new Random(42);

    // Laender mit Stadtlichtern (static HashSet fuer O(1) Lookup statt Array pro Frame)
    private static readonly HashSet<string> _countriesWithLights = new()
    {
        CountryIds.Germany, CountryIds.France, CountryIds.Russia,
        CountryIds.USA, CountryIds.Poland, CountryIds.Italy, CountryIds.China,
        CountryIds.UK, CountryIds.Spain, CountryIds.India, CountryIds.Canada,
        CountryIds.Norway, CountryIds.Sweden, CountryIds.Belarus, CountryIds.Australia,
        CountryIds.Japan, CountryIds.Austria, CountryIds.CzechRepublic, CountryIds.Switzerland,
        CountryIds.Mexico, CountryIds.Kazakhstan, CountryIds.Brazil,
        CountryIds.Turkey, CountryIds.Greenland,
        CountryIds.Argentina, CountryIds.Peru, CountryIds.Chile, CountryIds.Iran,
        CountryIds.SaudiArabia, CountryIds.Egypt,
        CountryIds.Hungary, CountryIds.Romania, CountryIds.Bulgaria,
        CountryIds.Greece,
        CountryIds.Portugal, CountryIds.Netherlands, CountryIds.Belgium,
        CountryIds.Luxembourg, CountryIds.Ireland, CountryIds.Denmark, CountryIds.Iceland,
        CountryIds.Serbia, CountryIds.Croatia, CountryIds.Slovakia, CountryIds.Slovenia,
        CountryIds.Bosnia, CountryIds.Montenegro,
        CountryIds.Lithuania, CountryIds.Latvia, CountryIds.Estonia,
        CountryIds.Moldova, CountryIds.Albania, CountryIds.NorthMacedonia,
        CountryIds.Colombia, CountryIds.Venezuela, CountryIds.Ecuador,
        CountryIds.Bolivia, CountryIds.Paraguay, CountryIds.Uruguay, CountryIds.Guyana,
        CountryIds.Libya, CountryIds.Tunisia, CountryIds.Algeria, CountryIds.Morocco,
        CountryIds.Sudan, CountryIds.SouthSudan, CountryIds.Nigeria, CountryIds.Ghana,
        CountryIds.IvoryCoast, CountryIds.Senegal, CountryIds.Mali, CountryIds.BurkinaFaso,
        CountryIds.Niger, CountryIds.Guinea, CountryIds.Benin, CountryIds.Togo,
        CountryIds.SierraLeone, CountryIds.Liberia, CountryIds.Mauritania, CountryIds.Gambia,
        CountryIds.DRC, CountryIds.Congo, CountryIds.Cameroon, CountryIds.Chad,
        CountryIds.CAR, CountryIds.Gabon, CountryIds.EquatorialGuinea,
        CountryIds.Ethiopia, CountryIds.Kenya, CountryIds.Tanzania, CountryIds.Uganda,
        CountryIds.Rwanda, CountryIds.Burundi, CountryIds.Somalia, CountryIds.Eritrea,
        CountryIds.Djibouti, CountryIds.Malawi, CountryIds.Zambia, CountryIds.Zimbabwe,
        CountryIds.Mozambique, CountryIds.Madagascar, CountryIds.SouthAfrica,
        CountryIds.Namibia, CountryIds.Botswana, CountryIds.Angola,
        CountryIds.Eswatini, CountryIds.Lesotho,
        CountryIds.Iraq, CountryIds.Syria, CountryIds.Jordan, CountryIds.Lebanon,
        CountryIds.Yemen, CountryIds.Oman, CountryIds.UAE, CountryIds.Kuwait,
        CountryIds.Qatar, CountryIds.Bahrain, CountryIds.Afghanistan, CountryIds.Pakistan,
        CountryIds.Israel, CountryIds.SouthKorea,
        CountryIds.Uzbekistan, CountryIds.Turkmenistan, CountryIds.Tajikistan,
        CountryIds.Kyrgyzstan, CountryIds.Mongolia,
        CountryIds.Bangladesh, CountryIds.SriLanka, CountryIds.Nepal, CountryIds.Myanmar,
        CountryIds.Thailand, CountryIds.Vietnam, CountryIds.Cambodia, CountryIds.Laos,
        CountryIds.Malaysia, CountryIds.Indonesia, CountryIds.Philippines,
        CountryIds.Singapore, CountryIds.Brunei, CountryIds.EastTimor,
        CountryIds.NorthKorea, CountryIds.Taiwan,
        CountryIds.Panama, CountryIds.CostaRica, CountryIds.Nicaragua,
        CountryIds.Honduras, CountryIds.Guatemala, CountryIds.Haiti,
        CountryIds.DominicanRepublic, CountryIds.Cuba
    };

    // Cache fuer Autobahn-Routen (aus JSON geladen)
    private Dictionary<string, List<double[]>>? _highwayRoutes;

    // Cache fuer Autobahn-Pixel (LOD-Stufen: Full, Medium, Low)
    private List<(Vector2 pos, float intensity)>? _highwayPixels;
    private List<(Vector2 pos, float intensity)>? _highwayPixelsMed;  // ~1/4 der Pixel
    private List<(Vector2 pos, float intensity)>? _highwayPixelsLow;  // ~1/12 der Pixel

    /// <summary>
    /// Generiert Pixel-Cluster fuer eine Stadt mit unregelmaessiger Form
    /// Nicht rund - organische, unvorhersehbare Formen
    /// </summary>
    private List<(Vector2 offset, float intensity)> GenerateCityPixels(City city)
    {
        var pixels = new List<(Vector2, float)>();

        // Mehr Pixel fuer groessere Staedte
        int totalPixels = (int)(MathF.Sqrt(city.Population) * 0.6f);
        totalPixels = Math.Clamp(totalPixels, 60, 1500);

        // Groessere Staedte = groesserer Radius (proportional zur Bevoelkerung)
        // Berlin (3.6M): ~6, Hamburg (1.8M): ~5, kleine Stadt (100K): ~2
        float popFactor = MathF.Pow(city.Population / 100000f, 0.4f); // Wurzel-Skalierung
        float baseRadius = 1.5f + popFactor * 1.2f;
        baseRadius = Math.Clamp(baseRadius, 1.5f, 8f);

        // Generiere 4-8 zufaellige "Arme" fuer unregelmaessige Form
        int numArms = 4 + _pixelRandom.Next(5);
        float[] armAngles = new float[numArms];
        float[] armLengths = new float[numArms];
        float[] armWidths = new float[numArms];

        for (int a = 0; a < numArms; a++)
        {
            armAngles[a] = (float)_pixelRandom.NextDouble() * MathF.PI * 2f;
            armLengths[a] = 0.3f + (float)_pixelRandom.NextDouble() * 0.7f; // Kuerzere Arme
            armWidths[a] = 0.2f + (float)_pixelRandom.NextDouble() * 0.4f;
        }

        // Unregelmaessige Grenzfunktion (8-16 Kontrollpunkte)
        int boundaryPoints = 8 + _pixelRandom.Next(9);
        float[] boundaryRadius = new float[boundaryPoints];
        for (int b = 0; b < boundaryPoints; b++)
        {
            boundaryRadius[b] = 0.6f + (float)_pixelRandom.NextDouble() * 0.8f;
        }

        for (int i = 0; i < totalPixels; i++)
        {
            float u = (float)_pixelRandom.NextDouble();
            float normalizedDist = MathF.Pow(u, 0.6f);
            float angle = (float)_pixelRandom.NextDouble() * MathF.PI * 2f;

            // Unregelmaessiger Radius fuer diesen Winkel
            float boundaryIndex = (angle / (MathF.PI * 2f)) * boundaryPoints;
            int idx0 = (int)boundaryIndex % boundaryPoints;
            int idx1 = (idx0 + 1) % boundaryPoints;
            float t = boundaryIndex - (int)boundaryIndex;
            float irregularFactor = boundaryRadius[idx0] * (1 - t) + boundaryRadius[idx1] * t;

            // Pruefe ob Punkt in einem "Arm" liegt
            float armBonus = 0f;
            for (int a = 0; a < numArms; a++)
            {
                float angleDiff = MathF.Abs(angle - armAngles[a]);
                if (angleDiff > MathF.PI) angleDiff = MathF.PI * 2f - angleDiff;
                if (angleDiff < armWidths[a])
                {
                    float armInfluence = 1f - (angleDiff / armWidths[a]);
                    armBonus = MathF.Max(armBonus, armInfluence * (armLengths[a] - 1f));
                }
            }

            float effectiveRadius = baseRadius * irregularFactor * (1f + armBonus);
            float distance = normalizedDist * effectiveRadius;

            // Zufaellige Verschiebung (klein)
            float jitterX = ((float)_pixelRandom.NextDouble() - 0.5f) * 0.4f;
            float jitterY = ((float)_pixelRandom.NextDouble() - 0.5f) * 0.4f;

            float offsetX = MathF.Cos(angle) * distance + jitterX;
            float offsetY = MathF.Sin(angle) * distance + jitterY;

            float intensity = MathF.Exp(-normalizedDist * 2.8f);
            intensity *= irregularFactor;
            intensity = Math.Clamp(intensity, 0.12f, 1.0f);
            intensity *= 0.7f + (float)_pixelRandom.NextDouble() * 0.3f;

            pixels.Add((new Vector2(offsetX, offsetY), intensity));
        }

        // Kleine Vorort-Cluster an zufaelligen Positionen
        int subClusters = 1 + _pixelRandom.Next(3);
        for (int c = 0; c < subClusters; c++)
        {
            float clusterAngle = (float)_pixelRandom.NextDouble() * MathF.PI * 2f;
            float clusterDist = baseRadius * (0.7f + (float)_pixelRandom.NextDouble() * 0.4f);
            float clusterX = MathF.Cos(clusterAngle) * clusterDist;
            float clusterY = MathF.Sin(clusterAngle) * clusterDist;
            float clusterSize = 0.5f + (float)_pixelRandom.NextDouble() * 1f;

            int clusterPixels = 8 + _pixelRandom.Next(12);
            for (int p = 0; p < clusterPixels; p++)
            {
                float pAngle = (float)_pixelRandom.NextDouble() * MathF.PI * 2f;
                float pDist = (float)_pixelRandom.NextDouble() * clusterSize;
                float px = clusterX + MathF.Cos(pAngle) * pDist;
                float py = clusterY + MathF.Sin(pAngle) * pDist;
                float pIntensity = 0.3f + (float)_pixelRandom.NextDouble() * 0.4f;
                pIntensity *= 1f - (pDist / clusterSize) * 0.5f;
                pixels.Add((new Vector2(px, py), pIntensity));
            }
        }

        return pixels;
    }

    /// <summary>
    /// Zeichnet Stadtlichter als dichte Pixel-Cluster
    /// Zentrum extrem dicht, Rand weniger dicht
    /// </summary>
    public void DrawCityLights(float timeOfDay, int dayOfYear = 1)
    {
        if (!CityLightsEnabled || !DayNightCycleEnabled) return;

        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        // Sonnen-Deklination
        float declination = 23.45f * MathF.Sin(2f * MathF.PI * (dayOfYear - 81) / 365f);
        float declinationRad = declination * MathF.PI / 180f;

        // Konvertiere Spielzeit zu UTC
        float utcTime = timeOfDay - TimezoneOffset;
        if (utcTime < 0) utcTime += 24;
        if (utcTime >= 24) utcTime -= 24;

        // Sonnen-Laengengrad
        float sunLongitude = (12f - utcTime) * 15f;
        if (sunLongitude > 180) sunLongitude -= 360;
        if (sunLongitude < -180) sunLongitude += 360;

        foreach (var (countryId, cityList) in Cities)
        {
            if (!_countriesWithLights.Contains(countryId)) continue;

            foreach (var city in cityList)
            {
                Vector2 mapPos = GeoPointToMap(city.Lon, city.Lat);
                Vector2 centerScreen = MapToScreen(mapPos);

                // Frustum Culling
                float margin = 100f * Zoom;
                if (centerScreen.X < -margin || centerScreen.X > screenW + margin ||
                    centerScreen.Y < -margin || centerScreen.Y > screenH + margin)
                    continue;

                // Berechne Dunkelheit
                float latRad = (float)(city.Lat * Math.PI / 180.0);
                float lonDiff = (float)(city.Lon - sunLongitude);
                if (lonDiff > 180) lonDiff -= 360;
                if (lonDiff < -180) lonDiff += 360;
                float hourAngle = lonDiff * MathF.PI / 180f;

                float sinAltitude = MathF.Sin(latRad) * MathF.Sin(declinationRad) +
                                    MathF.Cos(latRad) * MathF.Cos(declinationRad) * MathF.Cos(hourAngle);

                float darkness = 0f;
                if (sinAltitude < -0.18f)
                    darkness = 1.0f;
                else if (sinAltitude < -0.02f)
                {
                    darkness = 1.0f - (sinAltitude + 0.18f) / 0.16f;
                    darkness = Math.Clamp(darkness, 0f, 1f);
                }

                if (darkness < 0.5f) continue;

                // LOD: Bei niedrigem Zoom nur einen Punkt pro Stadt zeichnen
                if (Zoom < 5.0f)
                {
                    // Einfacher Punkt fuer Performance bei weitem Rauszoomen
                    byte alpha = (byte)(darkness * 200f);
                    // Groesse basierend auf Bevoelkerung und Zoom
                    int pointSize;
                    if (Zoom < 2.0f)
                        pointSize = city.Population > 1000000 ? 2 : 1;
                    else if (Zoom < 3.5f)
                        pointSize = city.Population > 500000 ? 3 : (city.Population > 200000 ? 2 : 1);
                    else
                        pointSize = city.Population > 300000 ? 4 : (city.Population > 100000 ? 3 : 2);

                    Color pointColor = new Color((byte)255, (byte)220, (byte)140, alpha);

                    int px = (int)centerScreen.X;
                    int py = (int)centerScreen.Y;

                    if (pointSize == 1)
                        Raylib.DrawPixel(px, py, pointColor);
                    else
                        Raylib.DrawRectangle(px - pointSize/2, py - pointSize/2, pointSize, pointSize, pointColor);

                    continue;
                }

                // Hole oder generiere Pixel fuer diese Stadt
                string cityKey = $"{city.Name}_{city.Population}";
                if (!_cityPixels.TryGetValue(cityKey, out var pixels))
                {
                    pixels = GenerateCityPixels(city);
                    _cityPixels[cityKey] = pixels;
                }

                // Skalierung basierend auf Zoom - sehr kompakte Staedte
                float scale = 0.4f + Zoom * 0.1f;
                scale = Math.Clamp(scale, 0.4f, 6f);

                // Pixel-Groesse: Klein halten damit Luecken sichtbar bleiben
                int pixelSize = 1;
                if (Zoom > 20f) pixelSize = 2;
                if (Zoom > 35f) pixelSize = 3;
                if (Zoom > 50f) pixelSize = 4;

                // LOD: Bei mittlerem Zoom weniger Pixel zeichnen
                int skipFactor = 1;
                if (Zoom < 7.0f) skipFactor = 6;
                else if (Zoom < 10.0f) skipFactor = 4;
                else if (Zoom < 15.0f) skipFactor = 2;

                int pixelIndex = 0;
                // Zeichne jeden Pixel
                foreach (var (offset, intensity) in pixels)
                {
                    // LOD: Pixel ueberspringen bei niedrigem Zoom
                    pixelIndex++;
                    if (skipFactor > 1 && pixelIndex % skipFactor != 0) continue;

                    // Skalierte Position
                    int px = (int)(centerScreen.X + offset.X * scale);
                    int py = (int)(centerScreen.Y + offset.Y * scale);

                    // Frustum Culling mit Rand
                    if (px < -pixelSize || px >= screenW + pixelSize ||
                        py < -pixelSize || py >= screenH + pixelSize)
                        continue;

                    // Alpha basierend auf Dunkelheit und Intensitaet
                    byte alpha = (byte)(darkness * intensity * 255f);
                    if (alpha < 15) continue;

                    // Warme Lichtfarbe (Orange/Gelb)
                    Color pixelColor = new Color((byte)255, (byte)(200 + intensity * 55), (byte)(100 + intensity * 80), alpha);

                    // Zeichne Pixel basierend auf Zoom-Level
                    if (pixelSize == 1)
                    {
                        Raylib.DrawPixel(px, py, pixelColor);
                    }
                    else
                    {
                        // Bei hohem Zoom: groessere Lichtpunkte
                        Raylib.DrawRectangle(px, py, pixelSize, pixelSize, pixelColor);
                    }

                    // Extra Glow nur bei sehr hohem Zoom
                    if (Zoom > 25f && intensity > 0.8f)
                    {
                        byte glowAlpha = (byte)(alpha * 0.2f);
                        Color glowColor = new Color((byte)255, (byte)220, (byte)150, glowAlpha);
                        Raylib.DrawPixel(px - 1, py, glowColor);
                        Raylib.DrawPixel(px + 1, py, glowColor);
                        Raylib.DrawPixel(px, py - 1, glowColor);
                        Raylib.DrawPixel(px, py + 1, glowColor);
                    }
                }
            }
        }

        // Zeichne Autobahn-Lichter (Dunkelheit wird pro Pixel berechnet)
        DrawHighwayLights(sunLongitude, declinationRad);
    }

    /// <summary>
    /// Laedt Autobahn-Routen aus JSON-Dateien (Deutschland, Japan, etc.)
    /// </summary>
    private void LoadHighwayRoutes()
    {
        if (_highwayRoutes != null) return;

        _highwayRoutes = new Dictionary<string, List<double[]>>();

        // Lade Autobahnen aus mehreren Dateien
        var highwayFiles = new[] {
            "german_highways.json",
            "japan_highways.json",
            "austria_highways.json",
            "czech_highways.json",
            "swiss_highways.json",
            "mexico_highways.json",
            "kazakhstan_highways.json",
            "brazil_highways.json",
            "turkey_highways.json",
            "chile_highways.json",
            "peru_highways.json",
            "argentina_highways.json",
            "iran_highways.json",
            "saudi_highways.json",
            "egypt_highways.json",
            "hungary_highways.json",
            "romania_highways.json",
            "bulgaria_highways.json",
            "greece_highways.json",
            "portugal_highways.json",
            "netherlands_highways.json",
            "belgium_highways.json",
            "luxembourg_highways.json",
            "ireland_highways.json",
            "denmark_highways.json",
            "iceland_highways.json",
            "serbia_highways.json",
            "croatia_highways.json",
            "slovakia_highways.json",
            "slovenia_highways.json",
            "bosnia_highways.json",
            "montenegro_highways.json",
            "lithuania_highways.json",
            "latvia_highways.json",
            "estonia_highways.json",
            "moldova_highways.json",
            "albania_highways.json",
            "macedonia_highways.json",
            "colombia_highways.json",
            "venezuela_highways.json",
            "ecuador_highways.json",
            "bolivia_highways.json",
            "paraguay_highways.json",
            "uruguay_highways.json",
            "guyana_highways.json",
            // Afrika
            "LBY_highways.json",
            "TUN_highways.json",
            "DZA_highways.json",
            "MAR_highways.json",
            "SDN_highways.json",
            "SSD_highways.json",
            "NGA_highways.json",
            "GHA_highways.json",
            "CIV_highways.json",
            "SEN_highways.json",
            "MLI_highways.json",
            "BFA_highways.json",
            "NER_highways.json",
            "GIN_highways.json",
            "BEN_highways.json",
            "TGO_highways.json",
            "SLE_highways.json",
            "LBR_highways.json",
            "MRT_highways.json",
            "GMB_highways.json",
            "COD_highways.json",
            "COG_highways.json",
            "CMR_highways.json",
            "TCD_highways.json",
            "CAF_highways.json",
            "GAB_highways.json",
            "GNQ_highways.json",
            "ETH_highways.json",
            "KEN_highways.json",
            "TZA_highways.json",
            "UGA_highways.json",
            "RWA_highways.json",
            "BDI_highways.json",
            "SOM_highways.json",
            "ERI_highways.json",
            "DJI_highways.json",
            "MWI_highways.json",
            "ZMB_highways.json",
            "ZWE_highways.json",
            "MOZ_highways.json",
            "MDG_highways.json",
            "ZAF_highways.json",
            "NAM_highways.json",
            "BWA_highways.json",
            "AGO_highways.json",
            "SWZ_highways.json",
            "LSO_highways.json",
            // Asien
            "IRQ_highways.json",
            "SYR_highways.json",
            "JOR_highways.json",
            "LBN_highways.json",
            "YEM_highways.json",
            "OMN_highways.json",
            "ARE_highways.json",
            "KWT_highways.json",
            "QAT_highways.json",
            "BHR_highways.json",
            "AFG_highways.json",
            "PAK_highways.json",
            "ISR_highways.json",
            "KOR_highways.json",
            "UZB_highways.json",
            "TKM_highways.json",
            "TJK_highways.json",
            "KGZ_highways.json",
            "MNG_highways.json",
            "BGD_highways.json",
            "LKA_highways.json",
            "NPL_highways.json",
            "MMR_highways.json",
            "THA_highways.json",
            "VNM_highways.json",
            "KHM_highways.json",
            "LAO_highways.json",
            "MYS_highways.json",
            "IDN_highways.json",
            "PHL_highways.json",
            "SGP_highways.json",
            "BRN_highways.json",
            "TLS_highways.json",
            "PRK_highways.json",
            "TWN_highways.json",
            // Mittelamerika / Karibik
            "PAN_highways.json",
            "CRI_highways.json",
            "NIC_highways.json",
            "HND_highways.json",
            "GTM_highways.json",
            "HTI_highways.json",
            "DOM_highways.json",
            "CUB_highways.json"
        };

        foreach (var filename in highwayFiles)
        {
            string path = Path.Combine("Data", filename);
            if (!File.Exists(path))
            {
                Console.WriteLine($"[WorldMap] Autobahn-Datei nicht gefunden: {path}");
                continue;
            }

            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var highways = doc.RootElement.GetProperty("highways");

                foreach (var highway in highways.EnumerateObject())
                {
                    var coords = new List<double[]>();
                    var coordArray = highway.Value.GetProperty("coordinates");

                    int ci = 0;
                    foreach (var coord in coordArray.EnumerateArray())
                    {
                        // Jeden 2. Punkt ueberspringen fuer bessere Performance
                        if (ci % 2 != 0 && ci != coordArray.GetArrayLength() - 1)
                        {
                            ci++;
                            continue;
                        }
                        var arr = new double[2];
                        arr[0] = coord[0].GetDouble(); // lon
                        arr[1] = coord[1].GetDouble(); // lat
                        coords.Add(arr);
                        ci++;
                    }

                    _highwayRoutes[highway.Name] = coords;
                }

                Console.WriteLine($"[WorldMap] {filename}: Autobahnen geladen");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorldMap] Fehler beim Laden von {filename}: {ex.Message}");
            }
        }

        Console.WriteLine($"[WorldMap] Gesamt: {_highwayRoutes.Count} Autobahnen geladen");
    }

    /// <summary>
    /// Generiert Pixel entlang der echten Autobahn-Routen
    /// </summary>
    private void GenerateHighwayPixels()
    {
        if (_highwayPixels != null) return;

        LoadHighwayRoutes();
        if (_highwayRoutes == null || _highwayRoutes.Count == 0) return;

        _highwayPixels = new List<(Vector2, float)>();
        var rand = new Random(123);

        foreach (var (name, coords) in _highwayRoutes)
        {
            // Fuer jedes Segment zwischen Wegpunkten
            for (int i = 0; i < coords.Count - 1; i++)
            {
                float lon1 = (float)coords[i][0];
                float lat1 = (float)coords[i][1];
                float lon2 = (float)coords[i + 1][0];
                float lat2 = (float)coords[i + 1][1];

                float dx = lon2 - lon1;
                float dy = lat2 - lat1;
                float segmentDist = MathF.Sqrt(dx * dx + dy * dy);

                // Pixel pro Segment basierend auf Laenge (reduziert fuer Performance)
                int numPixels = (int)(segmentDist * 150);
                numPixels = Math.Clamp(numPixels, 5, 80);

                for (int p = 0; p < numPixels; p++)
                {
                    float t = (float)p / numPixels;

                    float lon = lon1 + dx * t;
                    float lat = lat1 + dy * t;

                    // Minimales Rauschen fuer natuerlicheren Look
                    float noise = ((float)rand.NextDouble() - 0.5f) * 0.003f;
                    lon += noise;
                    lat += noise * 0.5f;

                    float intensity = 0.35f + (float)rand.NextDouble() * 0.25f;

                    // Luecken (30% der Pixel werden gezeichnet)
                    if (rand.NextDouble() > 0.7)
                    {
                        _highwayPixels.Add((new Vector2(lon, lat), intensity));
                    }
                }
            }
        }

        // LOD-Listen vorberechnen (vermeidet per-Frame skipFactor-Iteration)
        _highwayPixelsMed = new List<(Vector2, float)>(_highwayPixels.Count / 4);
        _highwayPixelsLow = new List<(Vector2, float)>(_highwayPixels.Count / 12);
        for (int i = 0; i < _highwayPixels.Count; i++)
        {
            if (i % 4 == 0) _highwayPixelsMed.Add(_highwayPixels[i]);
            if (i % 12 == 0) _highwayPixelsLow.Add(_highwayPixels[i]);
        }

        Console.WriteLine($"[WorldMap] {_highwayPixels.Count} Autobahn-Pixel generiert (Med: {_highwayPixelsMed.Count}, Low: {_highwayPixelsLow.Count})");
    }

    /// <summary>
    /// Zeichnet Autobahn-Lichter bei Nacht (berechnet Dunkelheit pro Pixel basierend auf Position)
    /// </summary>
    private void DrawHighwayLights(float sunLongitude, float declinationRad)
    {
        // Generiere Pixel falls noch nicht geschehen
        GenerateHighwayPixels();
        if (_highwayPixels == null || _highwayPixels.Count == 0) return;

        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        // LOD: Waehle vorberechnete Pixel-Liste basierend auf Zoom
        // Bei niedrigem Zoom wird eine kleinere Liste iteriert statt alle Pixel zu durchlaufen
        List<(Vector2 pos, float intensity)> pixels;
        int skipFactor;
        if (Zoom >= 5.0f)
        {
            pixels = _highwayPixels;           // Volle Aufloesung
            skipFactor = Zoom >= 6.5f ? 1 : 2;
        }
        else if (Zoom >= 2.5f)
        {
            pixels = _highwayPixelsMed!;       // ~1/4 der Pixel
            skipFactor = 1;
        }
        else
        {
            pixels = _highwayPixelsLow!;       // ~1/12 der Pixel
            skipFactor = Zoom < 0.8f ? 3 : 1;  // Bei extremem Zoom noch weiter reduzieren
        }

        int pixelIndex = 0;
        foreach (var (geoPos, intensity) in pixels)
        {
            // Optionaler Skip fuer Fein-Abstufung innerhalb einer LOD-Stufe
            if (skipFactor > 1)
            {
                pixelIndex++;
                if (pixelIndex % skipFactor != 0) continue;
            }

            Vector2 mapPos = GeoPointToMap(geoPos.X, geoPos.Y);
            Vector2 screenPos = MapToScreen(mapPos);

            // Frustum Culling
            if (screenPos.X < 0 || screenPos.X >= screenW ||
                screenPos.Y < 0 || screenPos.Y >= screenH)
                continue;

            // Berechne Dunkelheit fuer diesen Pixel basierend auf seiner Position
            float lat = geoPos.Y;  // Latitude
            float lon = geoPos.X;  // Longitude
            float latRad = lat * MathF.PI / 180f;
            float lonDiff = lon - sunLongitude;
            if (lonDiff > 180) lonDiff -= 360;
            if (lonDiff < -180) lonDiff += 360;
            float hourAngle = lonDiff * MathF.PI / 180f;
            float sinAlt = MathF.Sin(latRad) * MathF.Sin(declinationRad) +
                           MathF.Cos(latRad) * MathF.Cos(declinationRad) * MathF.Cos(hourAngle);

            float darkness = 0f;
            if (sinAlt < -0.18f)
                darkness = 1.0f;
            else if (sinAlt < -0.02f)
            {
                darkness = 1.0f - (sinAlt + 0.18f) / 0.16f;
                darkness = Math.Clamp(darkness, 0f, 1f);
            }

            if (darkness < 0.5f) continue;

            byte alpha = (byte)(darkness * intensity * 180f);
            if (alpha < 10) continue;

            // Orange/Gelbe Autobahn-Lichter
            Color lightColor = new Color((byte)255, (byte)200, (byte)100, alpha);

            Raylib.DrawPixel((int)screenPos.X, (int)screenPos.Y, lightColor);
        }
    }

    // Cache fuer urspruengliche Provinz-Besitzer (einmalig berechnet)
    private Dictionary<string, string>? _originalProvinceOwners;

    /// <summary>
    /// Initialisiert den Cache fuer urspruengliche Provinz-Besitzer (einmalig aufrufen)
    /// </summary>
    private void InitializeOriginalProvinceOwners()
    {
        if (_originalProvinceOwners != null) return;

        _originalProvinceOwners = new Dictionary<string, string>();
        foreach (var province in Provinces.Values)
        {
            // Speichere den aktuellen Besitzer als "Original"
            _originalProvinceOwners[province.Id] = province.CountryId;
        }
    }

    /// <summary>
    /// Zeichnet eroberte Provinzen mit der Farbe des neuen Besitzers.
    /// Optimiert: Nutzt Cache statt teure ContainsPoint-Pruefungen.
    /// </summary>
    public void DrawConqueredProvinces(string playerCountry)
    {
        // Initialisiere Cache beim ersten Aufruf
        InitializeOriginalProvinceOwners();
        if (_originalProvinceOwners == null) return;

        // Maximale Linienlaenge um Artefakte zu vermeiden
        float maxLineLength = MAP_WIDTH * Zoom * 0.45f;
        float maxLineLengthSq = maxLineLength * maxLineLength;

        foreach (var province in Provinces.Values)
        {
            // Schneller Check: Hat sich der Besitzer geaendert?
            if (!_originalProvinceOwners.TryGetValue(province.Id, out var originalCountryId))
                continue;

            // Wenn Provinz jetzt einem anderen Land gehoert, zeichne sie mit neuer Farbe
            if (originalCountryId != province.CountryId)
            {
                province.UpdateTransformedPoints(Zoom, Offset, MapToScreen);
                if (province.TransformedRings == null || province.TransformedRings.Count == 0) continue;

                // Hole Farbe des neuen Besitzers
                Color ownerColor;
                if (Regions.TryGetValue(province.CountryId, out var ownerRegion))
                {
                    ownerColor = ownerRegion.BaseColor;
                }
                else
                {
                    // Fallback: Spielerfarbe wenn kein Region gefunden
                    ownerColor = province.CountryId == playerCountry
                        ? new Color((byte)80, (byte)120, (byte)200, (byte)255)  // Blau fuer Spieler
                        : new Color((byte)150, (byte)80, (byte)80, (byte)255);  // Rot fuer Feind
                }

                // Exakt gleiche Farbe wie das Land des neuen Besitzers
                Color fillColor = new Color(ownerColor.R, ownerColor.G, ownerColor.B, (byte)255);

                // Zeichne Fill fuer alle Ringe mit Dreiecken
                for (int ringIndex = 0; ringIndex < province.TransformedRings.Count; ringIndex++)
                {
                    var transformedPoints = province.TransformedRings[ringIndex];
                    if (transformedPoints.Length < 3) continue;

                    // Nutze vorberechnete Triangulation
                    if (ringIndex < province.TrianglesPerRing.Count)
                    {
                        var triangles = province.TrianglesPerRing[ringIndex];
                        foreach (var (a, b, c) in triangles)
                        {
                            // Pruefen auf Wrap-Around-Artefakte
                            float dx1 = transformedPoints[b].X - transformedPoints[a].X;
                            float dy1 = transformedPoints[b].Y - transformedPoints[a].Y;
                            float dx2 = transformedPoints[c].X - transformedPoints[a].X;
                            float dy2 = transformedPoints[c].Y - transformedPoints[a].Y;

                            if (dx1 * dx1 + dy1 * dy1 > maxLineLengthSq) continue;
                            if (dx2 * dx2 + dy2 * dy2 > maxLineLengthSq) continue;

                            Raylib.DrawTriangle(
                                transformedPoints[a],
                                transformedPoints[b],
                                transformedPoints[c],
                                fillColor
                            );
                        }
                    }
                }

            }
        }
    }
}
