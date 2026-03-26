using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;

// Region definitions: region name, region id, and list of provinces
var regions = new (string Name, string Id, string[] Provinces)[]
{
    ("Marmararegion", "TUR-00", new[]
    {
        "Istanbul", "Tekirdag", "Edirne", "Kirklareli", "Balikesir",
        "Canakkale", "Kocaeli", "Sakarya", "Bilecik", "Bursa",
        "Yalova", "Bolu", "Duzce"
    }),
    ("Aegean", "TUR-01", new[]
    {
        "Izmir", "Aydin", "Denizli", "Mugla", "Manisa",
        "Afyonkarahisar", "Kutahya", "Usak"
    }),
    ("Mediterranean", "TUR-02", new[]
    {
        "Antalya", "Isparta", "Burdur", "Adana", "Mersin",
        "Hatay", "Kahramanmaras", "Osmaniye"
    }),
    ("CentralAnatolia", "TUR-03", new[]
    {
        "Ankara", "Konya", "Aksaray", "Karaman", "Kirikkale",
        "Kirsehir", "Nevsehir", "Nigde", "Kayseri", "Sivas",
        "Yozgat", "Eskisehir", "Cankiri"
    }),
    ("BlackSea", "TUR-04", new[]
    {
        "Zonguldak", "Bartin", "Karabuk", "Kastamonu", "Sinop",
        "Samsun", "Amasya", "Tokat", "Corum", "Ordu",
        "Giresun", "Trabzon", "Artvin", "Rize", "Gumushane",
        "Bayburt"
    }),
    ("EasternAnatolia", "TUR-05", new[]
    {
        "Erzurum", "Erzincan", "Agri", "Kars", "Igdir",
        "Ardahan", "Malatya", "Elazig", "Bingol", "Tunceli",
        "Van", "Mus", "Bitlis", "Hakkari"
    }),
    ("SoutheasternAnatolia", "TUR-06", new[]
    {
        "Gaziantep", "Adiyaman", "Kilis", "Sanliurfa",
        "Diyarbakir", "Mardin", "Batman", "Sirnak", "Siirt"
    }),
};

// ---- Helper functions ----

static string Normalize(string s)
{
    var normalized = s.Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder();
    foreach (var c in normalized)
    {
        var cat = CharUnicodeInfo.GetUnicodeCategory(c);
        if (cat != UnicodeCategory.NonSpacingMark)
            sb.Append(c);
    }
    var result = sb.ToString().ToLowerInvariant();
    result = result.Replace("\u0131", "i");
    result = result.Replace("\u0130", "i");
    result = result.Replace("\u011F", "g");
    result = result.Replace("\u015F", "s");
    result = result.Replace("\u00E7", "c");
    return result;
}

// Round coordinate to 6 decimal places for matching (~0.1m precision)
static string PointKey(double lon, double lat)
{
    return $"{Math.Round(lon, 6)},{Math.Round(lat, 6)}";
}

// Parse a GeoJSON coordinate [lon, lat] from a JsonNode
static (double lon, double lat) ParseCoord(JsonNode node)
{
    var arr = node.AsArray();
    return (arr[0]!.GetValue<double>(), arr[1]!.GetValue<double>());
}

/// <summary>
/// Dissolves internal boundaries between polygons in a region.
/// Shared edges (appearing in both directions) cancel out, leaving only outer boundary.
/// </summary>
static List<List<(double lon, double lat)>> DissolveRegion(List<JsonArray> allPolygonRingSets)
{
    // Step 1: Collect all directed edges from outer rings only
    // Each polygon ring set = [[outerRing], [hole1], ...] - we only process outerRing [0]
    var edgeMap = new Dictionary<string, (double lon1, double lat1, double lon2, double lat2)>();

    foreach (var polygonRingSet in allPolygonRingSets)
    {
        var outerRing = polygonRingSet[0]!.AsArray();

        for (int i = 0; i < outerRing.Count - 1; i++) // Last point = first point in GeoJSON
        {
            var (lon1, lat1) = ParseCoord(outerRing[i]!);
            var (lon2, lat2) = ParseCoord(outerRing[i + 1]!);

            string pk1 = PointKey(lon1, lat1);
            string pk2 = PointKey(lon2, lat2);

            // Skip degenerate edges
            if (pk1 == pk2) continue;

            string forwardKey = pk1 + ">" + pk2;
            string reverseKey = pk2 + ">" + pk1;

            if (edgeMap.ContainsKey(reverseKey))
            {
                // Shared edge: cancel both (internal boundary)
                edgeMap.Remove(reverseKey);
            }
            else
            {
                // New edge: add it
                edgeMap[forwardKey] = (lon1, lat1, lon2, lat2);
            }
        }
    }

    Console.WriteLine($"    Edges after dissolve: {edgeMap.Count} (from outer rings)");

    // Step 2: Build adjacency map (fromPointKey → list of (toPointKey, toLon, toLat))
    var adjacency = new Dictionary<string, List<(string toKey, double lon, double lat, string edgeKey)>>();

    foreach (var (edgeKey, (lon1, lat1, lon2, lat2)) in edgeMap)
    {
        string fromKey = PointKey(lon1, lat1);
        string toKey = PointKey(lon2, lat2);

        if (!adjacency.ContainsKey(fromKey))
            adjacency[fromKey] = new List<(string, double, double, string)>();
        adjacency[fromKey].Add((toKey, lon2, lat2, edgeKey));
    }

    // Step 3: Chain edges into rings
    var usedEdges = new HashSet<string>();
    var rings = new List<List<(double lon, double lat)>>();

    foreach (var (edgeKey, (lon1, lat1, lon2, lat2)) in edgeMap)
    {
        if (usedEdges.Contains(edgeKey)) continue;

        var ring = new List<(double lon, double lat)>();
        string startKey = PointKey(lon1, lat1);
        ring.Add((lon1, lat1));
        usedEdges.Add(edgeKey);

        string currentKey = PointKey(lon2, lat2);
        ring.Add((lon2, lat2));

        int maxIter = edgeMap.Count + 1;
        for (int iter = 0; iter < maxIter; iter++)
        {
            if (currentKey == startKey) break; // Ring closed

            if (!adjacency.TryGetValue(currentKey, out var nexts)) break;

            // Find next unused outgoing edge
            bool found = false;
            foreach (var candidate in nexts)
            {
                if (!usedEdges.Contains(candidate.edgeKey))
                {
                    usedEdges.Add(candidate.edgeKey);
                    ring.Add((candidate.lon, candidate.lat));
                    currentKey = candidate.toKey;
                    found = true;
                    break;
                }
            }

            if (!found) break;
        }

        if (ring.Count >= 3)
        {
            // Close ring (GeoJSON requires first == last)
            ring.Add(ring[0]);
            rings.Add(ring);
        }
    }

    return rings;
}

// ---- Main logic ----

// Find data directory
string[] possiblePaths = new[]
{
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Data"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Data"),
    Path.Combine(Directory.GetCurrentDirectory(), "Data"),
    @"d:\Maik\Code\economic-empire-main\Data",
};

string dataDir = "";
foreach (var p in possiblePaths)
{
    var full = Path.GetFullPath(p);
    if (File.Exists(Path.Combine(full, "turkey_adm1_raw.geojson")))
    {
        dataDir = full;
        break;
    }
}

if (string.IsNullOrEmpty(dataDir))
{
    Console.Error.WriteLine("Could not find turkey_adm1_raw.geojson in any expected location.");
    return 1;
}

var inputPath = Path.Combine(dataDir, "turkey_adm1_raw.geojson");
var outputPath = Path.Combine(dataDir, "turkey_regions.geojson");

Console.WriteLine($"Reading: {inputPath}");

var jsonText = File.ReadAllText(inputPath, Encoding.UTF8);
var doc = JsonNode.Parse(jsonText)!;
var features = doc["features"]!.AsArray();

Console.WriteLine($"Found {features.Count} province features.");

// Parse all province polygons
var provincePolygons = new Dictionary<string, List<JsonArray>>();
var provinceOriginalNames = new Dictionary<string, string>();

foreach (var feature in features)
{
    var props = feature!["properties"]!;
    var shapeName = props["shapeName"]!.GetValue<string>();
    var normalizedName = Normalize(shapeName);

    var geometry = feature["geometry"]!;
    var geoType = geometry["type"]!.GetValue<string>();
    var coordinates = geometry["coordinates"]!.AsArray();

    if (!provincePolygons.ContainsKey(normalizedName))
    {
        provincePolygons[normalizedName] = new List<JsonArray>();
        provinceOriginalNames[normalizedName] = shapeName;
    }

    if (geoType == "Polygon")
    {
        var cloned = JsonNode.Parse(coordinates.ToJsonString())!.AsArray();
        provincePolygons[normalizedName].Add(cloned);
    }
    else if (geoType == "MultiPolygon")
    {
        foreach (var polygon in coordinates)
        {
            var cloned = JsonNode.Parse(polygon!.ToJsonString())!.AsArray();
            provincePolygons[normalizedName].Add(cloned);
        }
    }
}

Console.WriteLine($"Parsed {provincePolygons.Count} unique provinces.");

// Match and dissolve regions
var usedProvinces = new HashSet<string>();
var regionFeatures = new JsonArray();

foreach (var (regionName, regionId, provinces) in regions)
{
    var regionPolygonRingSets = new List<JsonArray>();
    var matchedCount = 0;

    foreach (var province in provinces)
    {
        var normalizedProvince = Normalize(province);

        string? matchedKey = null;
        if (provincePolygons.ContainsKey(normalizedProvince))
        {
            matchedKey = normalizedProvince;
        }
        else
        {
            foreach (var key in provincePolygons.Keys)
            {
                if (key.StartsWith(normalizedProvince) || normalizedProvince.StartsWith(key))
                {
                    matchedKey = key;
                    break;
                }
            }
        }

        if (matchedKey != null)
        {
            matchedCount++;
            usedProvinces.Add(matchedKey);
            regionPolygonRingSets.AddRange(provincePolygons[matchedKey]);
        }
        else
        {
            Console.Error.WriteLine($"  WARNING: Province '{province}' not found for region '{regionName}'!");
        }
    }

    Console.WriteLine($"Region '{regionName}': matched {matchedCount}/{provinces.Length} provinces, {regionPolygonRingSets.Count} polygon(s)");

    // Dissolve internal boundaries
    Console.WriteLine($"  Dissolving internal boundaries...");
    var dissolvedRings = DissolveRegion(regionPolygonRingSets);
    Console.WriteLine($"  Result: {dissolvedRings.Count} ring(s)");

    // Sort rings by area (largest first) for consistent output
    dissolvedRings.Sort((a, b) =>
    {
        double areaA = Math.Abs(CalculateRingArea(a));
        double areaB = Math.Abs(CalculateRingArea(b));
        return areaB.CompareTo(areaA);
    });

    // Build MultiPolygon coordinates: each ring becomes a separate polygon (outer ring only)
    var multiPolygonCoords = new JsonArray();
    foreach (var ring in dissolvedRings)
    {
        var ringArray = new JsonArray();
        foreach (var (lon, lat) in ring)
        {
            var coord = new JsonArray { JsonValue.Create(lon), JsonValue.Create(lat) };
            ringArray.Add(coord);
        }
        // Each polygon has one outer ring (no holes after dissolve)
        var polygonArray = new JsonArray { ringArray };
        multiPolygonCoords.Add(polygonArray);
    }

    var regionFeature = new JsonObject
    {
        ["type"] = "Feature",
        ["properties"] = new JsonObject
        {
            ["name"] = regionName,
            ["id"] = regionId,
        },
        ["geometry"] = new JsonObject
        {
            ["type"] = "MultiPolygon",
            ["coordinates"] = multiPolygonCoords,
        },
    };

    regionFeatures.Add(regionFeature);
}

// Check for unmatched provinces
var unmatchedProvinces = provincePolygons.Keys.Except(usedProvinces).ToList();
if (unmatchedProvinces.Any())
{
    Console.Error.WriteLine($"\nWARNING: {unmatchedProvinces.Count} province(s) not assigned to any region:");
    foreach (var p in unmatchedProvinces)
        Console.Error.WriteLine($"  - {provinceOriginalNames[p]} (normalized: {p})");
}

// Write output
var output = new JsonObject
{
    ["type"] = "FeatureCollection",
    ["features"] = regionFeatures,
};

var options = new JsonSerializerOptions
{
    WriteIndented = false,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

var outputJson = output.ToJsonString(options);
File.WriteAllText(outputPath, outputJson, new UTF8Encoding(false));

Console.WriteLine($"\nWritten {regionFeatures.Count} region features to: {outputPath}");
Console.WriteLine($"Output file size: {new FileInfo(outputPath).Length:N0} bytes");

return 0;

// Helper: Calculate signed area of a ring (for sorting)
static double CalculateRingArea(List<(double lon, double lat)> ring)
{
    double area = 0;
    for (int i = 0; i < ring.Count - 1; i++)
    {
        area += ring[i].lon * ring[i + 1].lat;
        area -= ring[i + 1].lon * ring[i].lat;
    }
    return area / 2.0;
}
