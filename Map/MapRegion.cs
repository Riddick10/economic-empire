using System.Numerics;
using Raylib_cs;

namespace GrandStrategyGame.Map;

/// <summary>
/// Repräsentiert ein Land auf der Weltkarte
/// Unterstützt mehrere Polygon-Ringe (z.B. USA mit Alaska, Indonesien mit Inseln)
/// Mit LOD (Level of Detail) für Performance bei niedrigem Zoom
/// </summary>
public class MapRegion
{
    public string CountryId { get; set; }
    public List<Vector2[]> PolygonRings { get; set; }
    public Color BaseColor { get; set; }
    public Color HoverColor { get; set; }
    public Vector2 LabelPosition { get; set; }
    public float CachedArea { get; private set; }
    public Vector2[] Points => PolygonRings.Count > 0 ? PolygonRings[0] : Array.Empty<Vector2>();
    public List<(int, int, int)[]> TrianglesPerRing { get; private set; } = new();

    // Vorberechnete Bounding Box (Map-Koordinaten)
    public float BoundsMinX { get; private set; }
    public float BoundsMaxX { get; private set; }
    public float BoundsMinY { get; private set; }
    public float BoundsMaxY { get; private set; }

    // LOD (Level of Detail) - vereinfachte Polygone für niedrigen Zoom
    public List<Vector2[]> SimplifiedRingsLow { get; private set; } = new();  // Zoom < 1.0
    public List<Vector2[]> SimplifiedRingsMed { get; private set; } = new();  // Zoom 1.0 - 3.0
    public List<(int, int, int)[]> TrianglesLow { get; private set; } = new();
    public List<(int, int, int)[]> TrianglesMed { get; private set; } = new();

    // Cache fuer transformierte Punkte (Screen-Koordinaten)
    public List<Vector2[]>? TransformedRings { get; private set; }
    public List<(int, int, int)[]>? CurrentTriangles { get; private set; }
    public Vector2 TransformedLabelPos { get; private set; }
    private float _lastZoom = -1;
    private Vector2 _lastOffset = new(-99999, -99999);
    private int _lastLodLevel = -1;

    /// <summary>
    /// Gibt das aktuelle LOD-Level zurück (0=low, 1=med, 2=high)
    /// </summary>
    public static int GetLodLevel(float zoom)
    {
        if (zoom < 0.7f) return 0;      // Low Detail
        if (zoom < 1.5f) return 1;      // Medium Detail
        return 2;                        // Full Detail
    }

    /// <summary>
    /// Aktualisiert die gecachten Screen-Koordinaten wenn sich Viewport geaendert hat
    /// </summary>
    public void UpdateTransformedPoints(float zoom, Vector2 offset, Func<Vector2, Vector2> mapToScreen)
    {
        int lodLevel = GetLodLevel(zoom);

        // Nur neu berechnen wenn sich Viewport oder LOD-Level geaendert hat
        if (Math.Abs(zoom - _lastZoom) < 0.0001f &&
            Math.Abs(offset.X - _lastOffset.X) < 0.1f &&
            Math.Abs(offset.Y - _lastOffset.Y) < 0.1f &&
            lodLevel == _lastLodLevel &&
            TransformedRings != null)
        {
            return;
        }

        _lastZoom = zoom;
        _lastOffset = offset;
        _lastLodLevel = lodLevel;

        // Wähle Quell-Ringe basierend auf LOD-Level
        var sourceRings = lodLevel switch
        {
            0 => SimplifiedRingsLow.Count > 0 ? SimplifiedRingsLow : PolygonRings,
            1 => SimplifiedRingsMed.Count > 0 ? SimplifiedRingsMed : PolygonRings,
            _ => PolygonRings
        };

        // Wähle passende Triangles
        CurrentTriangles = lodLevel switch
        {
            0 => TrianglesLow.Count > 0 ? TrianglesLow : TrianglesPerRing,
            1 => TrianglesMed.Count > 0 ? TrianglesMed : TrianglesPerRing,
            _ => TrianglesPerRing
        };

        // Transformiere alle Ringe
        TransformedRings = new List<Vector2[]>(sourceRings.Count);
        foreach (var ring in sourceRings)
        {
            var transformed = new Vector2[ring.Length];
            for (int i = 0; i < ring.Length; i++)
            {
                transformed[i] = mapToScreen(ring[i]);
            }
            TransformedRings.Add(transformed);
        }

        TransformedLabelPos = mapToScreen(LabelPosition);
    }

    public MapRegion(string countryId, Vector2[] points, Color color)
        : this(countryId, new List<Vector2[]> { points }, color)
    {
    }

    public MapRegion(string countryId, List<Vector2[]> polygonRings, Color color)
    {
        CountryId = countryId;

        // Filtere und sortiere Ringe - berechne Flaeche nur einmal pro Ring
        var ringsWithArea = polygonRings
            .Where(r => r.Length >= 3)
            .Select(r => (Ring: r, Area: PolygonUtils.CalculateRingArea(r)))
            .OrderByDescending(x => x.Area)
            .ToList();

        PolygonRings = ringsWithArea.Select(x => x.Ring).ToList();
        CachedArea = ringsWithArea.Sum(x => x.Area);

        BaseColor = color;
        HoverColor = new Color(
            (byte)Math.Min(color.R + 40, 255),
            (byte)Math.Min(color.G + 40, 255),
            (byte)Math.Min(color.B + 40, 255),
            color.A
        );

        LabelPosition = PolygonRings.Count > 0
            ? PolygonUtils.CalculateCenter(PolygonRings[0])
            : Vector2.Zero;

        // Trianguliere alle Polygon-Ringe (Full Detail)
        TrianglesPerRing = new List<(int, int, int)[]>(PolygonRings.Count);
        foreach (var ring in PolygonRings)
        {
            TrianglesPerRing.Add(PolygonUtils.TriangulateRing(ring));
        }

        // Generiere LOD-Versionen
        GenerateLodData();

        // Bounding Box vorberechnen
        ComputeBounds();
    }

    private void ComputeBounds()
    {
        BoundsMinX = float.MaxValue;
        BoundsMaxX = float.MinValue;
        BoundsMinY = float.MaxValue;
        BoundsMaxY = float.MinValue;
        foreach (var ring in PolygonRings)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                if (ring[i].X < BoundsMinX) BoundsMinX = ring[i].X;
                if (ring[i].X > BoundsMaxX) BoundsMaxX = ring[i].X;
                if (ring[i].Y < BoundsMinY) BoundsMinY = ring[i].Y;
                if (ring[i].Y > BoundsMaxY) BoundsMaxY = ring[i].Y;
            }
        }
    }

    /// <summary>
    /// Generiert vereinfachte Polygon-Versionen für verschiedene Zoom-Stufen
    /// </summary>
    private void GenerateLodData()
    {
        // Medium Detail (Zoom 1.0 - 3.0): Toleranz 2.0
        SimplifiedRingsMed = new List<Vector2[]>(PolygonRings.Count);
        TrianglesMed = new List<(int, int, int)[]>(PolygonRings.Count);
        foreach (var ring in PolygonRings)
        {
            var simplified = PolygonUtils.SimplifyPolygon(ring, 2.0f);
            SimplifiedRingsMed.Add(simplified);
            TrianglesMed.Add(PolygonUtils.TriangulateRing(simplified));
        }

        // Low Detail (Zoom < 1.0): Toleranz 5.0
        SimplifiedRingsLow = new List<Vector2[]>(PolygonRings.Count);
        TrianglesLow = new List<(int, int, int)[]>(PolygonRings.Count);
        foreach (var ring in PolygonRings)
        {
            var simplified = PolygonUtils.SimplifyPolygon(ring, 5.0f);
            SimplifiedRingsLow.Add(simplified);
            TrianglesLow.Add(PolygonUtils.TriangulateRing(simplified));
        }
    }

    public bool ContainsPoint(Vector2 point)
    {
        foreach (var ring in PolygonRings)
        {
            if (PolygonUtils.ContainsPoint(ring, point))
                return true;
        }
        return false;
    }
}
