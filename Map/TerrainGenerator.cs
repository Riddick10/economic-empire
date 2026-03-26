using Raylib_cs;

namespace GrandStrategyGame;

/// <summary>
/// Generiert prozedurale Terrain-Texturen basierend auf echten Biom-Daten
/// Verwendet Rechteck-basiertes Rendering fuer bessere Performance
/// </summary>
public static class TerrainGenerator
{
    // Biom-Farben (erdige, natuerliche Toene - 35% dunkler)
    private static readonly Color BiomeTundra = new(130, 137, 140, 255);
    private static readonly Color BiomeTaiga = new(55, 68, 52, 255);
    private static readonly Color BiomeTemperateForest = new(49, 78, 42, 255);
    private static readonly Color BiomeGrassland = new(94, 104, 59, 255);
    private static readonly Color BiomeDesert = new(137, 124, 91, 255);
    private static readonly Color BiomeSavanna = new(117, 107, 65, 255);
    private static readonly Color BiomeTropical = new(36, 65, 36, 255);
    private static readonly Color BiomeMediterranean = new(104, 98, 65, 255);
    private static readonly Color BiomeMountain = new(85, 78, 72, 255);
    private static readonly Color BiomeHighMountain = new(117, 114, 111, 255);
    private static readonly Color BiomeIce = new(153, 156, 160, 255);

    // Ozean-Farben fuer Gradient (Aequator bis Pole)
    private static readonly Color OceanEquator = new(12, 35, 60, 255);   // Hellerer Ozean am Aequator
    private static readonly Color OceanPolar = new(3, 10, 20, 255);      // Sehr dunkler Ozean an Polen

    /// <summary>
    /// Generiert eine Terrain-Textur mit Biom-Zonen
    /// </summary>
    public static Texture2D Generate(int width, int height)
    {
        Console.WriteLine("[TerrainGenerator] Starte Terrain-Generierung...");

        // Basis-Image erstellen
        var image = Raylib.GenImageColor(width, height, OceanPolar);

        // Ozean-Gradient zeichnen (dunkler zu den Polen)
        DrawOceanGradient(ref image, width, height);

        // Biom-Zonen als Rechtecke zeichnen (von Nord nach Sued)
        // Jede Zone ist ein horizontaler Streifen oder ein Rechteck

        // === Arktis (70-85 Grad Nord) ===
        DrawLatitudeZone(ref image, width, height, 70, 85, BiomeIce);

        // === Tundra (60-70 Grad Nord) ===
        DrawLatitudeZone(ref image, width, height, 60, 70, BiomeTundra);

        // === Taiga/Borealer Wald (45-60 Grad Nord) ===
        DrawLatitudeZone(ref image, width, height, 45, 60, BiomeTaiga);

        // === Gemaessigte Zone (30-45 Grad Nord) - Basis ===
        DrawLatitudeZone(ref image, width, height, 30, 45, BiomeTemperateForest);

        // === Subtropische/Wuesten Zone (15-30 Grad Nord) ===
        DrawLatitudeZone(ref image, width, height, 15, 30, BiomeSavanna);

        // === Tropische Zone (0-15 Grad Nord) ===
        DrawLatitudeZone(ref image, width, height, 0, 15, BiomeTropical);

        // === Tropische Zone (0-15 Grad Sued) ===
        DrawLatitudeZone(ref image, width, height, -15, 0, BiomeTropical);

        // === Subtropische Zone (15-30 Grad Sued) ===
        DrawLatitudeZone(ref image, width, height, -30, -15, BiomeSavanna);

        // === Gemaessigte Zone Sued (30-45 Grad Sued) ===
        DrawLatitudeZone(ref image, width, height, -45, -30, BiomeGrassland);

        // === Antarktis (50-60 Grad Sued) ===
        DrawLatitudeZone(ref image, width, height, -60, -50, BiomeIce);

        // === Spezifische Regionen ueberschreiben ===

        // Sahara
        DrawRegion(ref image, width, height, -17, 40, 15, 35, BiomeDesert);

        // Arabische Wueste
        DrawRegion(ref image, width, height, 35, 60, 15, 32, BiomeDesert);

        // Australische Wueste
        DrawRegion(ref image, width, height, 115, 145, -30, -18, BiomeDesert);

        // Kalahari
        DrawRegion(ref image, width, height, 15, 30, -28, -18, BiomeDesert);

        // US Suedwesten
        DrawRegion(ref image, width, height, -120, -100, 25, 40, BiomeDesert);

        // Gobi
        DrawRegion(ref image, width, height, 90, 115, 38, 48, BiomeDesert);

        // Mittelmeerraum
        DrawRegion(ref image, width, height, -10, 40, 35, 45, BiomeMediterranean);

        // Zentralasiatische Steppe
        DrawRegion(ref image, width, height, 45, 90, 40, 55, BiomeGrassland);

        // US Great Plains
        DrawRegion(ref image, width, height, -110, -90, 30, 50, BiomeGrassland);

        // === Gebirge ===

        // Alpen
        DrawRegion(ref image, width, height, 5, 17, 44, 48, BiomeMountain);

        // Himalaya & Tibet
        DrawRegion(ref image, width, height, 70, 100, 27, 40, BiomeHighMountain);

        // Rocky Mountains
        DrawRegion(ref image, width, height, -125, -105, 35, 55, BiomeMountain);

        // Anden
        DrawRegion(ref image, width, height, -75, -65, -55, 10, BiomeMountain);

        // Ural
        DrawRegion(ref image, width, height, 55, 65, 50, 68, BiomeMountain);

        // Skandinavische Berge
        DrawRegion(ref image, width, height, 5, 20, 60, 70, BiomeMountain);

        // Kaukasus
        DrawRegion(ref image, width, height, 38, 50, 40, 44, BiomeMountain);

        // Atlas
        DrawRegion(ref image, width, height, -10, 10, 30, 37, BiomeMountain);

        // Aethiopisches Hochland
        DrawRegion(ref image, width, height, 35, 48, 5, 15, BiomeMountain);

        // === Regenwaelder ===

        // Amazonas
        DrawRegion(ref image, width, height, -80, -45, -15, 5, BiomeTropical);

        // Kongobecken
        DrawRegion(ref image, width, height, 10, 35, -10, 5, BiomeTropical);

        // Suedostasien
        DrawRegion(ref image, width, height, 95, 140, -10, 15, BiomeTropical);

        // Texture aus Image erstellen
        var texture = Raylib.LoadTextureFromImage(image);
        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);

        // Image freigeben
        Raylib.UnloadImage(image);

        Console.WriteLine($"[TerrainGenerator] Terrain-Textur generiert ({width}x{height})");

        return texture;
    }

    /// <summary>
    /// Zeichnet den Ozean mit Farbverlauf - dunkler zu den Polen hin
    /// </summary>
    private static void DrawOceanGradient(ref Image image, int width, int height)
    {
        // Karte geht von 85°N bis -60°S
        const double maxLat = 85.0;
        const double minLat = -60.0;
        const double latRange = maxLat - minLat;  // 145 Grad

        // Zeichne horizontale Streifen mit interpolierter Farbe
        for (int y = 0; y < height; y++)
        {
            // Berechne Breitengrad fuer diese Y-Position
            double lat = maxLat - (y / (double)height) * latRange;

            // Berechne Abstand vom Aequator (0 = Aequator, 1 = Pole)
            // Nord: 85° -> distFromEquator = 85/85 = 1.0
            // Aequator: 0° -> distFromEquator = 0
            // Sued: -60° -> distFromEquator = 60/60 = 1.0
            double distFromEquator;
            if (lat >= 0)
            {
                distFromEquator = lat / 85.0;  // Nordpol bei 85°
            }
            else
            {
                distFromEquator = -lat / 60.0; // Suedpol bei -60°
            }
            distFromEquator = Math.Clamp(distFromEquator, 0.0, 1.0);

            // Quadratische Interpolation fuer sanfteren Uebergang
            double t = distFromEquator * distFromEquator;

            // Interpoliere zwischen Aequator-Farbe und Polar-Farbe
            byte r = (byte)(OceanEquator.R + (OceanPolar.R - OceanEquator.R) * t);
            byte g = (byte)(OceanEquator.G + (OceanPolar.G - OceanEquator.G) * t);
            byte b = (byte)(OceanEquator.B + (OceanPolar.B - OceanEquator.B) * t);

            Color lineColor = new(r, g, b, (byte)255);

            // Zeichne horizontale Linie
            Raylib.ImageDrawLine(ref image, 0, y, width, y, lineColor);
        }
    }

    /// <summary>
    /// Zeichnet eine Breitengrad-Zone als horizontalen Streifen
    /// </summary>
    private static void DrawLatitudeZone(ref Image image, int width, int height, double minLat, double maxLat, Color color)
    {
        // Konvertiere Breitengrad zu Y-Koordinaten
        // Karte geht von 85°N bis 60°S (145 Grad Spanne)
        int y1 = LatToY(maxLat, height);
        int y2 = LatToY(minLat, height);

        if (y1 > y2)
        {
            (y1, y2) = (y2, y1);
        }

        int rectHeight = y2 - y1;
        if (rectHeight <= 0) return;

        Raylib.ImageDrawRectangle(ref image, 0, y1, width, rectHeight, color);
    }

    /// <summary>
    /// Zeichnet eine spezifische Region
    /// </summary>
    private static void DrawRegion(ref Image image, int width, int height,
        double minLon, double maxLon, double minLat, double maxLat, Color color)
    {
        int x1 = LonToX(minLon, width);
        int x2 = LonToX(maxLon, width);
        int y1 = LatToY(maxLat, height);
        int y2 = LatToY(minLat, height);

        if (x1 > x2) (x1, x2) = (x2, x1);
        if (y1 > y2) (y1, y2) = (y2, y1);

        int rectWidth = x2 - x1;
        int rectHeight = y2 - y1;

        if (rectWidth <= 0 || rectHeight <= 0) return;

        Raylib.ImageDrawRectangle(ref image, x1, y1, rectWidth, rectHeight, color);
    }

    /// <summary>
    /// Konvertiert Laengengrad zu X-Koordinate
    /// </summary>
    private static int LonToX(double lon, int width)
    {
        // -180 bis +180 -> 0 bis width
        double normalized = (lon + 180.0) / 360.0;
        return (int)(normalized * width);
    }

    /// <summary>
    /// Konvertiert Breitengrad zu Y-Koordinate
    /// </summary>
    private static int LatToY(double lat, int height)
    {
        // 85 bis -60 -> 0 bis height
        double normalized = (85.0 - lat) / 145.0;
        return (int)(normalized * height);
    }
}
