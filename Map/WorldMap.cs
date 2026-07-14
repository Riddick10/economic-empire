using System.Numerics;
using Raylib_cs;

using GrandStrategyGame.Models;

namespace GrandStrategyGame.Map;

/// <summary>
/// Verwaltet die Weltkarte mit echten geografischen Koordinaten
/// Aufgeteilt in Partial Classes:
/// - WorldMap.cs (Kern - Fields, Initialize, Draw)
/// - WorldMap.Data.cs (Statische Daten)
/// - WorldMap.Coordinates.cs (Koordinaten-Transformation)
/// - WorldMap.Camera.cs (Kamera-Steuerung)
/// - WorldMap.ProvinceLoading.cs (Provinz-Laden)
/// - WorldMap.Rendering.cs (Rendering-Methoden)
/// - WorldMap.Provinces.cs (Provinz-Rendering)
/// </summary>
public partial class WorldMap
{
    // Regionen und Provinzen
    public Dictionary<string, MapRegion> Regions { get; private set; }
    public Dictionary<string, Province> Provinces { get; private set; }

    // Staedte (nach Land gruppiert)
    public Dictionary<string, List<City>> Cities { get; private set; } = new();

    // Zeitzonen-Daten fuer realistische Ortszeiten
    public TimezoneData Timezones { get; private set; } = new();

    // Kamera
    public Vector2 Offset { get; set; }
    public float Zoom { get; set; }

    // Karten-Dimensionen
    public const int MAP_WIDTH = 2000;
    public const int MAP_HEIGHT = 1000;

    // Geografische Grenzen (Lon/Lat)
    private const double MIN_LON = -180;
    private const double MAX_LON = 180;
    private const double MIN_LAT = -60;   // Antarktis ausblenden
    private const double MAX_LAT = 85;

    // HOI4-Style Bitmap-Lookup fuer O(1) Positionsabfragen
    private MapLookup? _mapLookup;

    // Visuelle Einstellungen
    public bool ShowGrid { get; set; } = false;
    public bool ShowRivers { get; set; } = true;
    public float BorderWidth { get; set; } = 2.0f;
    public bool DayNightCycleEnabled { get; set; } = true;

    // Zeitzone fuer Tag/Nacht-Zyklus (Offset zu UTC in Stunden)
    // Standard: CET (Mitteleuropaeische Zeit, UTC+1)
    public float TimezoneOffset { get; set; } = 1.0f;

    // Tag/Nacht-Overlay Cache fuer Performance
    private RenderTexture2D _dayNightCache;
    private float _cachedTimeOfDay = -1;
    private int _cachedDayOfYear = -1;
    private int _cachedScreenW = 0;
    private int _cachedScreenH = 0;
    private Vector2 _cachedOffset;
    private float _cachedZoom = 0;

    // Fluesse
    private List<River> _rivers = new();

    // Ressourcen-Vorkommen Icons (Oil=0, NaturalGas=1, Coal=2, Iron=3, Uranium=4, Copper=5)
    private readonly Texture2D?[] _depositIcons = new Texture2D?[6];

    // Buendnis-Daten fuer Buendnis-Ansicht - aus JSON geladen
    public static Dictionary<string, string[]> Alliances { get; private set; } = new();

    // Kategorisierung der Buendnisse - aus JSON geladen
    public static string[] MilitaryAlliances { get; private set; } = Array.Empty<string>();
    public static string[] EconomicAlliances { get; private set; } = Array.Empty<string>();

    // Aktueller Buendnis-Ansichtstyp (wird von Program.cs gesetzt)
    public static AllianceViewType CurrentAllianceViewType { get; set; } = AllianceViewType.Military;

    // Statische Allianz-Prioritaets-Arrays (werden beim Laden gesetzt)
    private static string[] MilitaryAlliancePriority = { "NATO", "CSTO", "SCO" };
    private static string[] EconomicAlliancePriority = { "EU", "BRICS", "ASEAN", "AU", "OPEC" };

    // Wiederverwendbare Listen fuer Draw() (vermeiden per-Frame Allokation)
    private readonly List<(string Id, MapRegion Region)> _visibleRegionsCache = new(256);
    private readonly List<Province> _visibleProvincesCache = new(512);

    // Dynamisches HashSet fuer Laender mit Provinzen (wird beim Laden befuellt)
    private readonly HashSet<string> CountriesWithProvinces = new();

    // Buendnis-Farben - aus JSON geladen
    public static Dictionary<string, Color> AllianceColors { get; private set; } = new()
    {
        { "None", new Color((byte)70, (byte)70, (byte)70, (byte)255) }
    };

    /// <summary>
    /// Laedt Buendnis-Daten aus JSON.
    /// Muss beim Spielstart aufgerufen werden.
    /// </summary>
    public static void LoadAllianceData(string basePath)
    {
        var (alliances, colors, military, economic) = GameDataLoader.LoadAlliances(basePath);
        Alliances = alliances;
        AllianceColors = colors;
        MilitaryAlliances = military;
        EconomicAlliances = economic;
        MilitaryAlliancePriority = military;
        EconomicAlliancePriority = economic;
    }

    /// <summary>
    /// Gibt das primaere Buendnis eines Landes zurueck (oder "None")
    /// Filtert nach aktuellem Buendnis-Ansichtstyp (Militaer/Wirtschaft)
    /// </summary>
    public static string GetPrimaryAlliance(string countryId)
    {
        // Prioritaet basierend auf aktuellem Ansichtstyp (statische Arrays)
        string[] priorityOrder = CurrentAllianceViewType == AllianceViewType.Military
            ? MilitaryAlliancePriority
            : EconomicAlliancePriority;

        foreach (var alliance in priorityOrder)
        {
            if (Alliances.TryGetValue(alliance, out var members) && members.Contains(countryId))
            {
                return alliance;
            }
        }
        return "None";
    }

    /// <summary>
    /// Gibt die Farbe fuer ein Land basierend auf seinem Buendnis zurueck
    /// </summary>
    public static Color GetAllianceColor(string countryId)
    {
        var alliance = GetPrimaryAlliance(countryId);
        return AllianceColors.TryGetValue(alliance, out var color) ? color : AllianceColors["None"];
    }

    public WorldMap()
    {
        Regions = new Dictionary<string, MapRegion>();
        Provinces = new Dictionary<string, Province>();
        Offset = Vector2.Zero;
        Zoom = 1.0f;
    }

    /// <summary>
    /// Initialisiert die Karte mit echten geografischen Daten
    /// </summary>
    /// <param name="onProgress">Callback fuer Fortschrittsmeldungen (Status, Fortschritt 0-1)</param>
    public void Initialize(Action<string, float>? onProgress = null)
    {
        void ReportProgress(string status, float progress) => onProgress?.Invoke(status, progress);

        // Laender nach Regionen gruppiert laden
        var regions = new (string Name, string[] Countries)[] {
            ("Nordamerika", new[] { "USA", "CAN", "MEX" }),
            ("Mittelamerika", new[] { "GTM", "CUB", "HTI", "DOM", "HND", "NIC", "CRI", "PAN" }),
            ("Suedamerika", new[] { "BRA", "ARG", "COL", "VEN", "PER", "CHL", "ECU", "BOL", "PRY", "URY", "GUY" }),
            ("Westeuropa", new[] { "GBR", "FRA", "DEU", "ITA", "ESP", "PRT", "NLD", "BEL", "LUX", "CHE", "AUT", "IRL" }),
            ("Nordeuropa", new[] { "NOR", "SWE", "FIN", "DNK", "ISL", "GRL" }),
            ("Osteuropa", new[] { "POL", "UKR", "CZE", "ROU", "HUN", "BLR", "BGR", "SRB", "HRV", "SVK", "LTU", "LVA", "EST", "MDA", "ALB", "MKD", "SVN", "BIH", "MNE" }),
            ("Suedeuropa", new[] { "GRC" }),
            ("Russland", new[] { "RUS" }),
            ("Zentralasien", new[] { "KAZ", "UZB", "TKM", "KGZ", "TJK", "MNG", "AFG" }),
            ("Ostasien", new[] { "CHN", "JPN", "KOR", "PRK", "TWN" }),
            ("Suedostasien", new[] { "IDN", "THA", "VNM", "MYS", "PHL", "MMR", "KHM", "LAO" }),
            ("Suedasien", new[] { "IND", "PAK", "BGD", "NPL", "LKA" }),
            ("Naher Osten", new[] { "SAU", "IRN", "TUR", "IRQ", "SYR", "YEM", "OMN", "ARE", "KWT", "JOR", "ISR", "LBN", "QAT", "BHR", "GEO", "ARM", "AZE" }),
            ("Nordafrika", new[] { "EGY", "LBY", "TUN", "DZA", "MAR", "SDN", "SSD" }),
            ("Westafrika", new[] { "NGA", "GHA", "CIV", "SEN", "MLI", "BFA", "NER", "GIN", "BEN", "TGO", "SLE", "LBR", "MRT", "GMB" }),
            ("Zentralafrika", new[] { "COD", "COG", "CMR", "TCD", "CAF", "GAB", "GNQ" }),
            ("Ostafrika", new[] { "ETH", "KEN", "TZA", "UGA", "RWA", "BDI", "SOM", "ERI", "DJI", "MWI", "ZMB", "ZWE", "MOZ", "MDG" }),
            ("Suedafrika", new[] { "ZAF", "NAM", "BWA", "AGO", "SWZ", "LSO" }),
            ("Ozeanien", new[] { "AUS", "NZL", "PNG" })
        };

        int totalCountries = regions.Sum(r => r.Countries.Length);
        int loadedCountries = 0;

        foreach (var (regionName, countries) in regions)
        {
            foreach (var countryCode in countries)
            {
                float progress = (float)loadedCountries / totalCountries * 0.5f;
                ReportProgress($"Lade {regionName}: {countryCode}...", progress);

                try
                {
                    var multiPolygons = GeoData.GetCountryMultiPolygons(countryCode);

                    if (multiPolygons != null && multiPolygons.Count > 0)
                    {
                        var allPolygonRings = multiPolygons
                            .Select(polygonRing => ConvertGeoToScreen(polygonRing))
                            .Where(p => p.Length >= 3)
                            .ToList();

                        if (allPolygonRings.Count > 0)
                        {
                            Color regionColor = GetRegionColor(countryCode);
                            Regions[countryCode] = new MapRegion(countryCode, allPolygonRings, regionColor);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Laden von {countryCode}: {ex.Message}");
                }

                loadedCountries++;
            }
        }

        Console.WriteLine($"[WorldMap] {Regions.Count} Laender geladen");

        // Provinzen laden
        InitializeProvinces(onProgress);

        // Fluesse laden
        ReportProgress("Lade Fluesse...", 0.96f);
        LoadRivers();

        // HOI4-Style Bitmap-Lookup generieren
        ReportProgress("Generiere Karten-Lookup...", 0.98f);
        GenerateMapLookup();

        // Staedte laden
        ReportProgress("Lade Staedte...", 0.99f);
        LoadCities();

        // Zeitzonen laden
        LoadTimezones();

        // HINWEIS: Terrain-Texturen werden NICHT hier geladen!
        // Raylib-Texturen muessen im Hauptthread geladen werden.
        // Das passiert beim ersten Draw-Aufruf (lazy loading).

        ReportProgress("Fertig!", 1.0f);
    }

    /// <summary>
    /// Laedt Staedte aus der cities.json Datei
    /// </summary>
    private void LoadCities()
    {
        string citiesPath = Path.Combine("Data", "cities.json");
        if (!File.Exists(citiesPath))
        {
            Console.WriteLine($"[WorldMap] Staedte-Datei nicht gefunden: {citiesPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(citiesPath);
            var citiesData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<CityJson>>>(json);

            if (citiesData == null) return;

            foreach (var (countryId, cityList) in citiesData)
            {
                var cities = cityList.Select(c => new City(c.name, c.lat, c.lon, c.population, c.isCapital)).ToList();
                Cities[countryId] = cities;
            }

            int totalCities = Cities.Values.Sum(c => c.Count);
            Console.WriteLine($"[WorldMap] {totalCities} Staedte geladen fuer {Cities.Count} Laender");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorldMap] Fehler beim Laden der Staedte: {ex.Message}");
        }
    }

    /// <summary>
    /// Laedt Zeitzonen-Daten aus der timezones.json Datei
    /// </summary>
    private void LoadTimezones()
    {
        string timezonesPath = Path.Combine("Data", "timezones.json");
        if (!File.Exists(timezonesPath))
        {
            Console.WriteLine($"[WorldMap] Zeitzonen-Datei nicht gefunden: {timezonesPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(timezonesPath);
            var data = System.Text.Json.JsonSerializer.Deserialize<TimezoneDataJson>(json);

            if (data == null) return;

            // Laender-Zeitzonen laden
            foreach (var (countryId, offset) in data.countries)
            {
                Timezones.CountryTimezones[countryId] = offset;
            }

            // Provinz-Zeitzonen laden
            foreach (var (countryId, provinces) in data.provinces)
            {
                Timezones.ProvinceTimezones[countryId] = new Dictionary<string, double>(provinces);
            }

            Console.WriteLine($"[WorldMap] Zeitzonen geladen: {Timezones.CountryTimezones.Count} Laender, {Timezones.ProvinceTimezones.Count} mit Provinz-Daten");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorldMap] Fehler beim Laden der Zeitzonen: {ex.Message}");
        }
    }

    // Flag fuer einmaliges Terrain-Laden im Hauptthread
    private bool _terrainInitAttempted = false;

    /// <summary>
    /// Zeichnet die gesamte Weltkarte
    /// </summary>
    public void Draw(string? hoveredCountry, string? selectedCountry, string? playerCountry, string? selectedProvince = null, string? hoveredProvince = null, MapViewMode viewMode = MapViewMode.Political, ResourceType? heatmapResource = null)
    {
        // Lazy Loading: Terrain-Texturen beim ersten Draw laden (Hauptthread)
        if (!_terrainInitAttempted)
        {
            _terrainInitAttempted = true;
            LoadTerrainTextures();
        }

        // Hintergrund (Ozean-Gradient)
        DrawOceanBackground();

        if (ShowGrid) DrawGrid();

        // Berechne sichtbaren Bereich fuer Frustum Culling
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();
        float viewMinX = -Offset.X / Zoom;
        float viewMinY = -Offset.Y / Zoom;
        float viewMaxX = (screenW - Offset.X) / Zoom;
        float viewMaxY = (screenH - Offset.Y) / Zoom;

        // Sammle sichtbare Regionen und aktualisiere deren Caches (wiederverwendbare Liste)
        _visibleRegionsCache.Clear();
        foreach (var (countryId, region) in Regions)
        {
            if (!IsRegionVisible(region, viewMinX, viewMinY, viewMaxX, viewMaxY))
                continue;
            region.UpdateTransformedPoints(Zoom, Offset, MapToScreen);
            _visibleRegionsCache.Add((countryId, region));
        }
        var visibleRegions = _visibleRegionsCache;

        // Sammle sichtbare Provinzen und aktualisiere deren Caches (wiederverwendbare Liste)
        _visibleProvincesCache.Clear();
        if (Zoom >= 8.0f || viewMode == MapViewMode.Resources)
        {
            foreach (var province in Provinces.Values)
            {
                if (!IsProvinceVisible(province, viewMinX, viewMinY, viewMaxX, viewMaxY))
                    continue;
                province.UpdateTransformedPoints(Zoom, Offset, MapToScreen);
                _visibleProvincesCache.Add(province);
            }
        }
        var visibleProvinces = _visibleProvincesCache;

        // Erst alle Laender flaechendeckend fuellen (mit gecachten Koordinaten)
        foreach (var (countryId, region) in visibleRegions)
        {
            if (viewMode == MapViewMode.Resources)
            {
                // Alle Laender mit Heatmap-Farbe fuellen (dient als Basis, Provinzen uebermalen spaeter)
                DrawCountryFillResources(region, countryId, countryId == hoveredCountry, countryId == selectedCountry, heatmapResource);
            }
            else if (viewMode == MapViewMode.Alliance)
            {
                DrawCountryFillAlliance(region, countryId, countryId == hoveredCountry, countryId == selectedCountry);
            }
            else if (viewMode == MapViewMode.Trade)
            {
                DrawCountryFillTrade(region, countryId, countryId == hoveredCountry, countryId == selectedCountry);
            }
            else
            {
                DrawCountryFill(region, countryId == hoveredCountry, countryId == selectedCountry, countryId == playerCountry);
            }
        }

        // Ressourcen-Heatmap: Provinzen einzeln einfaerben (fuer Laender mit Provinz-Daten)
        if (viewMode == MapViewMode.Resources)
        {
            foreach (var province in visibleProvinces)
            {
                float provValue = GetProvinceHeatmapValue(province, heatmapResource);
                DrawProvinceFillHeatmap(province, provValue, province.Id == hoveredProvince, province.Id == selectedProvince);
            }
        }

        // Eroberte Provinzen mit neuer Besitzer-Farbe zeichnen
        DrawConqueredProvinces(playerCountry ?? "");

        // Terrain-Overlay zeichnen (Relief-Shading fuer 3D-Effekt)
        DrawTerrainOverlay();

        // Fluesse zeichnen (ueber Land, aber unter Grenzen)
        if (ShowRivers)
        {
            DrawRivers(viewMinX, viewMinY, viewMaxX, viewMaxY);
        }

        // Provinzgrenzen zeichnen (nur bei starkem Zoom, nicht in Ressourcen-Ansicht)
        if (viewMode != MapViewMode.Resources)
        {
            DrawAllProvinceBorders(visibleProvinces);
        }

        // Ausgewaehlte Provinz hervorheben (mit Fill und Rahmen)
        if (selectedProvince != null && Provinces.TryGetValue(selectedProvince, out var selProv))
        {
            selProv.UpdateTransformedPoints(Zoom, Offset, MapToScreen);
            DrawProvinceHighlight(selProv, isSelected: true);
        }

        // Hover-Hervorhebung deaktiviert - Provinzen werden nur bei Klick hervorgehoben

        // Dann Grenzen aller sichtbaren Laender zeichnen (ohne spezielle Laender)
        // Laender mit Provinzen: Bei hohem Zoom nur aeussere Grenze zeichnen (Provinzgrenzen uebernehmen)
        var countriesWithProvinces = CountriesWithProvinces;

        MapRegion? playerRegion = null;
        MapRegion? selectedRegion = null;
        MapRegion? hoveredRegion = null;
        foreach (var (countryId, region) in visibleRegions)
        {
            if (countryId == playerCountry)
            {
                playerRegion = region;
                continue;  // Spieler-Grenze wird zuletzt gezeichnet
            }
            if (countryId == selectedCountry)
            {
                selectedRegion = region;
                continue;  // Selected-Grenze wird zuletzt gezeichnet
            }
            if (countryId == hoveredCountry && countryId != playerCountry)
            {
                hoveredRegion = region;
                // Normale Grenze trotzdem zeichnen, Hover-Overlay kommt danach
            }

            // Bei Zoom >= 8: Ueberspringe interne Grenzen fuer Laender mit Provinzen
            // (Provinzgrenzen werden separat gezeichnet)
            if (Zoom >= 8.0f && countriesWithProvinces.Contains(countryId))
            {
                DrawCountryBorderOuterOnly(region);
            }
            else
            {
                DrawCountryBorder(region, false, false, false);
            }
        }

        // Spezielle Grenzen zuletzt zeichnen (ueber allen anderen Grenzen)
        // Reihenfolge: hovered -> selected -> player (player ganz oben)
        if (hoveredRegion != null && hoveredCountry != selectedCountry)
        {
            DrawHighlightBorderOverlay(hoveredRegion, isSelected: false);
        }
        if (selectedRegion != null && selectedCountry != playerCountry)
        {
            DrawHighlightBorderOverlay(selectedRegion, isSelected: true);
        }
        if (playerRegion != null)
        {
            DrawPlayerBorderOverlay(playerRegion);
        }

        // Laendernamen zeichnen (verschwinden wenn Provinzgrenzen erscheinen)
        if (Zoom >= 0.6f && Zoom < 8.0f)
        {
            foreach (var (countryId, region) in visibleRegions)
            {
                DrawCountryLabel(countryId, region);
            }
        }

        // Provinznamen zeichnen (nur bei starkem Zoom, aber nicht bei sehr starkem Zoom wo Staedte angezeigt werden)
        if (Zoom >= 12.0f && Zoom < 25.0f)
        {
            foreach (var province in visibleProvinces)
            {
                DrawProvinceLabel(province);
            }
        }

        // Hauptstaedte zeichnen (bei mittlerem bis hohem Zoom)
        DrawCapitals();

        // Staedte zeichnen (bei sehr hohem Zoom)
        DrawCities();

        // Ressourcen-Icons nicht mehr in der Heatmap-Ansicht zeichnen
    }

    /// <summary>
    /// Zeichnet ein Tag/Nacht-Overlay basierend auf der aktuellen Zeit.
    /// Projiziert realistisch mit gekruemmtem Terminator basierend auf Jahreszeit.
    /// Verwendet Caching fuer bessere Performance.
    /// </summary>
    public void DrawDayNightOverlay(float timeOfDay, int dayOfYear = 1)
    {
        if (!DayNightCycleEnabled) return;

        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        // Runde Zeit auf 1-Minuten-Schritte fuer fluessige Animation
        float roundedTime = MathF.Round(timeOfDay * 60f) / 60f; // 1-Minuten-Schritte

        // Pruefe ob Cache aktualisiert werden muss
        bool needsUpdate = _cachedScreenW != screenW ||
                          _cachedScreenH != screenH ||
                          MathF.Abs(_cachedTimeOfDay - roundedTime) > 0.001f ||
                          _cachedDayOfYear != dayOfYear ||
                          MathF.Abs(_cachedOffset.X - Offset.X) > 1f ||
                          MathF.Abs(_cachedOffset.Y - Offset.Y) > 1f ||
                          MathF.Abs(_cachedZoom - Zoom) > 0.01f;

        if (needsUpdate)
        {
            // Cache-Textur erstellen oder neu erstellen bei Groessenaenderung
            if (_cachedScreenW != screenW || _cachedScreenH != screenH)
            {
                if (_dayNightCache.Id != 0)
                    Raylib.UnloadRenderTexture(_dayNightCache);
                _dayNightCache = Raylib.LoadRenderTexture(screenW, screenH);
            }

            // In Render-Textur zeichnen
            Raylib.BeginTextureMode(_dayNightCache);
            Raylib.ClearBackground(new Color(0, 0, 0, 0)); // Transparent

            RenderDayNightToTexture(roundedTime, dayOfYear, screenW, screenH);

            Raylib.EndTextureMode();

            // Cache-Werte aktualisieren
            _cachedTimeOfDay = roundedTime;
            _cachedDayOfYear = dayOfYear;
            _cachedScreenW = screenW;
            _cachedScreenH = screenH;
            _cachedOffset = Offset;
            _cachedZoom = Zoom;
        }

        // Gecachte Textur zeichnen (Y-Flip wegen OpenGL)
        if (_dayNightCache.Id != 0)
        {
            Rectangle srcRect = new Rectangle(0, 0, screenW, -screenH);
            Rectangle destRect = new Rectangle(0, 0, screenW, screenH);
            Raylib.DrawTexturePro(_dayNightCache.Texture, srcRect, destRect, Vector2.Zero, 0, Color.White);
        }
    }

    /// <summary>
    /// Rendert das Tag/Nacht-Overlay in die aktuelle Render-Textur
    /// </summary>
    private void RenderDayNightToTexture(float timeOfDay, int dayOfYear, int screenW, int screenH)
    {
        // Sonnen-Deklination (Neigung der Erdachse, aendert sich im Jahresverlauf)
        float declination = 23.45f * MathF.Sin(2f * MathF.PI * (dayOfYear - 81) / 365f);
        float declinationRad = declination * MathF.PI / 180f;

        // Konvertiere Spielzeit zu UTC (Spielzeit ist in lokaler Zeitzone)
        float utcTime = timeOfDay - TimezoneOffset;
        if (utcTime < 0) utcTime += 24;
        if (utcTime >= 24) utcTime -= 24;

        // Sonnen-Laengengrad (wo es gerade Mittag ist) - basierend auf UTC
        float sunLongitude = (12f - utcTime) * 15f;
        if (sunLongitude > 180) sunLongitude -= 360;
        if (sunLongitude < -180) sunLongitude += 360;

        // Adaptiver Pixel-Step basierend auf Zoom
        int pixelStep = Zoom switch
        {
            < 0.5f => 5,    // 0% - Weltkarte - sehr fein
            < 1.0f => 6,    // 0-1% - fein
            < 2.0f => 7,    // 1-3% - fein
            < 4.0f => 14,   // 3-7% - mittel
            _ => 16         // 7-100% - mittel
        };

        for (int screenX = 0; screenX < screenW; screenX += pixelStep)
        {
            for (int screenY = 0; screenY < screenH; screenY += pixelStep)
            {
                // Screen zu Map-Koordinaten
                float mapX = (screenX - Offset.X) / Zoom;
                float mapY = (screenY - Offset.Y) / Zoom;

                // Map zu Geo-Koordinaten
                float longitude = (mapX / MAP_WIDTH) * 360f - 180f;
                float latitude = 85f - (mapY / MAP_HEIGHT) * 145f;

                // Berechne Sonnenhoehe
                float latRad = latitude * MathF.PI / 180f;
                float hourAngle = (longitude - sunLongitude) * MathF.PI / 180f;
                float sinAltitude = MathF.Sin(latRad) * MathF.Sin(declinationRad) +
                                   MathF.Cos(latRad) * MathF.Cos(declinationRad) * MathF.Cos(hourAngle);

                // Konvertiere zu Dunkelheit
                float darkness;
                if (sinAltitude > 0.1f)
                    darkness = 0f;
                else if (sinAltitude > -0.1f)
                    darkness = (0.1f - sinAltitude) / 0.2f;
                else
                    darkness = 1f;

                if (darkness > 0.01f)
                {
                    byte alpha = (byte)(darkness * 165);  // Dunklere Nacht
                    Color nightColor = new Color((byte)5, (byte)10, (byte)30, alpha);
                    Raylib.DrawRectangle(screenX, screenY, pixelStep, pixelStep, nightColor);
                }
            }
        }
    }
}
