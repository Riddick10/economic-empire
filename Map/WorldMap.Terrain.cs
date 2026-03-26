using System.Numerics;
using Raylib_cs;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Terrain-System fuer realistische Landschaftsdarstellung
/// Laedt und rendert Hoehenmap, Relief-Shading und Terrain-Typen
/// </summary>
public partial class WorldMap
{
    // Terrain-Texturen (nicht nullable - verwende default struct)
    private Texture2D _heightmapTexture;
    private Texture2D _reliefTexture;
    private Texture2D _terrainTexture;
    private bool _terrainLoaded = false;
    private bool _heightmapLoaded = false;
    private bool _reliefLoaded = false;
    private bool _terrainTypeLoaded = false;

    // Terrain-Render-Einstellungen
    public bool ShowTerrain { get; set; } = true;
    public float TerrainOpacity { get; set; } = 0.05f;  // Wie stark das Terrain sichtbar ist
    public float ReliefIntensity { get; set; } = 0.40f;  // Dezentes Relief-Shading (dunkler)

    /// <summary>
    /// Laedt die Terrain-Texturen aus dem Data/Terrain Ordner
    /// </summary>
    public void LoadTerrainTextures()
    {
        try
        {
            // Basis-Pfad ermitteln
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string terrainDir = Path.Combine(baseDir, "Data", "Terrain");

            // Fallback: Relativer Pfad
            if (!Directory.Exists(terrainDir))
            {
                terrainDir = Path.Combine("Data", "Terrain");
            }

            // Noch ein Fallback: Vom Projektverzeichnis
            if (!Directory.Exists(terrainDir))
            {
                string projectDir = Path.Combine(baseDir, "..", "..", "..", "Data", "Terrain");
                if (Directory.Exists(projectDir))
                {
                    terrainDir = projectDir;
                }
            }

            Console.WriteLine($"[Terrain] Suche in: {terrainDir}");

            // Pfade fuer die Texturen
            string heightmapPath = Path.Combine(terrainDir, "heightmap.png");
            string reliefPath = Path.Combine(terrainDir, "relief.png");
            string terrainPath = Path.Combine(terrainDir, "terrain.png");

            // Pruefe ob Dateien existieren
            bool heightmapExists = File.Exists(heightmapPath);
            bool reliefExists = File.Exists(reliefPath);
            bool terrainExists = File.Exists(terrainPath);

            Console.WriteLine($"[Terrain] heightmap.png: {(heightmapExists ? "gefunden" : "nicht gefunden")}");
            Console.WriteLine($"[Terrain] relief.png: {(reliefExists ? "gefunden" : "nicht gefunden")}");
            Console.WriteLine($"[Terrain] terrain.png: {(terrainExists ? "gefunden" : "nicht gefunden")}");

            if (!heightmapExists && !reliefExists && !terrainExists)
            {
                Console.WriteLine("[Terrain] Keine Terrain-Texturen gefunden");
                Console.WriteLine("[Terrain] Fuehre 'python Data/download_terrain.py' aus um sie zu generieren");
                _terrainLoaded = false;
                return;
            }

            // Heightmap laden
            if (heightmapExists)
            {
                try
                {
                    _heightmapTexture = Raylib.LoadTexture(heightmapPath);
                    if (_heightmapTexture.Id != 0)
                    {
                        Raylib.SetTextureFilter(_heightmapTexture, TextureFilter.Bilinear);
                        _heightmapLoaded = true;
                        Console.WriteLine($"[Terrain] Heightmap geladen: {_heightmapTexture.Width}x{_heightmapTexture.Height}");
                    }
                    else
                    {
                        Console.WriteLine("[Terrain] Heightmap konnte nicht geladen werden (ID=0)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Terrain] Fehler beim Laden der Heightmap: {ex.Message}");
                }
            }

            // Relief laden
            if (reliefExists)
            {
                try
                {
                    _reliefTexture = Raylib.LoadTexture(reliefPath);
                    if (_reliefTexture.Id != 0)
                    {
                        Raylib.SetTextureFilter(_reliefTexture, TextureFilter.Bilinear);
                        _reliefLoaded = true;
                        Console.WriteLine($"[Terrain] Relief geladen: {_reliefTexture.Width}x{_reliefTexture.Height}");
                    }
                    else
                    {
                        Console.WriteLine("[Terrain] Relief konnte nicht geladen werden (ID=0)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Terrain] Fehler beim Laden des Reliefs: {ex.Message}");
                }
            }

            // Terrain-Typen laden
            if (terrainExists)
            {
                try
                {
                    _terrainTexture = Raylib.LoadTexture(terrainPath);
                    if (_terrainTexture.Id != 0)
                    {
                        Raylib.SetTextureFilter(_terrainTexture, TextureFilter.Bilinear);
                        _terrainTypeLoaded = true;
                        Console.WriteLine($"[Terrain] Terrain-Typen geladen: {_terrainTexture.Width}x{_terrainTexture.Height}");
                    }
                    else
                    {
                        Console.WriteLine("[Terrain] Terrain-Typen konnte nicht geladen werden (ID=0)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Terrain] Fehler beim Laden der Terrain-Typen: {ex.Message}");
                }
            }

            // Terrain ist geladen wenn mindestens Relief vorhanden ist
            _terrainLoaded = _reliefLoaded;

            if (_terrainLoaded)
            {
                Console.WriteLine("[Terrain] Terrain-System aktiviert");
            }
            else
            {
                Console.WriteLine("[Terrain] Terrain-System nicht aktiviert (keine Relief-Textur)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Terrain] Kritischer Fehler beim Laden: {ex.Message}");
            _terrainLoaded = false;
        }
    }

    /// <summary>
    /// Gibt die Terrain-Texturen frei
    /// </summary>
    public void UnloadTerrainTextures()
    {
        try
        {
            if (_heightmapLoaded && _heightmapTexture.Id != 0)
            {
                Raylib.UnloadTexture(_heightmapTexture);
            }
            _heightmapLoaded = false;

            if (_reliefLoaded && _reliefTexture.Id != 0)
            {
                Raylib.UnloadTexture(_reliefTexture);
            }
            _reliefLoaded = false;

            if (_terrainTypeLoaded && _terrainTexture.Id != 0)
            {
                Raylib.UnloadTexture(_terrainTexture);
            }
            _terrainTypeLoaded = false;

            _terrainLoaded = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Terrain] Fehler beim Entladen: {ex.Message}");
        }
    }

    /// <summary>
    /// Zeichnet das Relief-Shading als Overlay ueber die Karte
    /// Muss NACH den Laender-Fuellungen aber VOR den Grenzen aufgerufen werden
    /// </summary>
    private void DrawTerrainOverlay()
    {
        if (!_terrainLoaded || !ShowTerrain) return;
        if (!_reliefLoaded || _reliefTexture.Id == 0) return;

        try
        {
            // Berechne Position und Groesse basierend auf Kamera
            float destX = Offset.X;
            float destY = Offset.Y;
            float destW = MAP_WIDTH * Zoom;
            float destH = MAP_HEIGHT * Zoom;

            // Source-Rectangle (gesamte Textur)
            Rectangle source = new(0, 0, _reliefTexture.Width, _reliefTexture.Height);

            // Destination-Rectangle (auf Bildschirm)
            Rectangle dest = new(destX, destY, destW, destH);

            // Relief-Intensitaet bei starkem Herauszoomen reduzieren
            // Zoom < 1.5: Terrain verblasst, damit die Karte uebersichtlicher bleibt
            float zoomFade = Zoom < 1.5f ? Math.Clamp((Zoom - 0.4f) / 1.1f, 0.1f, 1.0f) : 1.0f;
            byte alpha = (byte)(ReliefIntensity * 200 * zoomFade);  // Moderater Schatten-Effekt
            Color tint = new((byte)255, (byte)255, (byte)255, alpha);

            // Zeichne mit multiplikativer Blendung fuer Schatten-Effekt
            Raylib.BeginBlendMode(BlendMode.Multiplied);
            Raylib.DrawTexturePro(_reliefTexture, source, dest, Vector2.Zero, 0, tint);
            Raylib.EndBlendMode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Terrain] Fehler beim Zeichnen: {ex.Message}");
            _terrainLoaded = false;  // Deaktiviere bei Fehler
        }
    }

}
