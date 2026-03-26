using System.Numerics;
using Raylib_cs;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Kamera-Steuerung
/// </summary>
public partial class WorldMap
{
    /// <summary>
    /// Generiert das HOI4-Style Bitmap-Lookup fuer schnelle Positionsabfragen
    /// </summary>
    private void GenerateMapLookup()
    {
        _mapLookup = new MapLookup(MAP_WIDTH, MAP_HEIGHT);
        _mapLookup.Generate(Regions, Provinces);
    }

    /// <summary>
    /// Entlaedt Ressourcen
    /// </summary>
    public void Unload()
    {
        // Terrain-Texturen freigeben
        UnloadTerrainTextures();

        // Ressourcen-Icons freigeben
        for (int i = 0; i < _depositIcons.Length; i++)
        {
            if (_depositIcons[i] is { } icon)
            {
                Raylib.UnloadTexture(icon);
                _depositIcons[i] = null;
            }
        }

        // MapLookup freigeben
        _mapLookup?.Dispose();
        _mapLookup = null;
    }

    /// <summary>
    /// Bewegt die Kamera - mit Begrenzung am Kartenrand
    /// </summary>
    public void Move(Vector2 delta)
    {
        Offset += delta;
        ClampOffset(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
    }

    /// <summary>
    /// Zoomt die Karte - mit dynamischem Minimum um nicht ueber die Karte hinaus zu zoomen
    /// </summary>
    public void ZoomAt(Vector2 screenPos, float zoomDelta)
    {
        float oldZoom = Zoom;

        // Dynamisches Minimum: Karte muss mindestens so gross wie der Bildschirm sein
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();
        float minZoomX = screenWidth / (float)MAP_WIDTH;
        float minZoomY = screenHeight / (float)MAP_HEIGHT;
        float minZoom = Math.Max(minZoomX, minZoomY);

        // Zoom-Bereich: dynamisches Minimum bis MAX_ZOOM
        Zoom = Math.Clamp(Zoom + zoomDelta * Zoom * 0.5f, minZoom, GameConfig.MAX_ZOOM);

        // Zoom zum Mauszeiger hin
        if (Math.Abs(oldZoom - Zoom) > 0.001f)
        {
            Vector2 mapPos = (screenPos - Offset) / oldZoom;
            Offset = screenPos - mapPos * Zoom;
        }

        // Karte im sichtbaren Bereich halten
        ClampOffset(screenWidth, screenHeight);
    }

    /// <summary>
    /// Haelt die Karte im sichtbaren Bereich - verhindert Scrollen ueber den Rand
    /// </summary>
    private void ClampOffset(int screenWidth, int screenHeight)
    {
        float mapWidth = MAP_WIDTH * Zoom;
        float mapHeight = MAP_HEIGHT * Zoom;

        float newX = Offset.X;
        float newY = Offset.Y;

        // Wenn Karte groesser als Bildschirm: Begrenzen
        if (mapWidth >= screenWidth)
        {
            newX = Math.Clamp(Offset.X, screenWidth - mapWidth, 0);
        }
        else
        {
            // Karte zentrieren wenn kleiner als Bildschirm
            newX = (screenWidth - mapWidth) / 2;
        }

        if (mapHeight >= screenHeight)
        {
            newY = Math.Clamp(Offset.Y, screenHeight - mapHeight, 0);
        }
        else
        {
            newY = (screenHeight - mapHeight) / 2;
        }

        Offset = new Vector2(newX, newY);
    }

    /// <summary>
    /// Zentriert die Karte auf ein Land
    /// </summary>
    public void CenterOnCountry(string countryId, int screenWidth, int screenHeight)
    {
        if (Regions.TryGetValue(countryId, out var region))
        {
            Vector2 center = CalculateCenter(region.Points);
            Offset = new Vector2(screenWidth / 2, screenHeight / 2) - center * Zoom;
        }
    }
}
