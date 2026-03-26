using System.Numerics;

using GrandStrategyGame.Models;

namespace GrandStrategyGame.Map;

/// <summary>
/// Repräsentiert eine Provinz (Unterteilung eines Landes)
/// z.B. Bundesländer in Deutschland, Staaten in den USA
/// </summary>
public class Province
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string CountryId { get; set; }
    public List<Vector2[]> PolygonRings { get; set; }
    public Vector2 LabelPosition { get; set; }
    public float CachedArea { get; private set; }
    public List<(int, int, int)[]> TrianglesPerRing { get; private set; } = new();

    // Fabriken in dieser Provinz
    public int CivilianFactories { get; set; }
    public int MilitaryFactories { get; set; }
    public int Dockyards { get; set; }

    // Minen in dieser Provinz
    public List<Mine> Mines { get; set; } = new();

    // Vorberechnete Bounding Box (Map-Koordinaten)
    public float BoundsMinX { get; private set; }
    public float BoundsMaxX { get; private set; }
    public float BoundsMinY { get; private set; }
    public float BoundsMaxY { get; private set; }

    // Cache fuer transformierte Punkte (Screen-Koordinaten)
    public List<Vector2[]>? TransformedRings { get; private set; }
    public Vector2 TransformedLabelPos { get; private set; }
    private float _lastZoom = -1;
    private Vector2 _lastOffset = new(-99999, -99999);

    /// <summary>
    /// Aktualisiert die gecachten Screen-Koordinaten wenn sich Viewport geaendert hat
    /// </summary>
    public void UpdateTransformedPoints(float zoom, Vector2 offset, Func<Vector2, Vector2> mapToScreen)
    {
        if (Math.Abs(zoom - _lastZoom) < 0.0001f &&
            Math.Abs(offset.X - _lastOffset.X) < 0.1f &&
            Math.Abs(offset.Y - _lastOffset.Y) < 0.1f &&
            TransformedRings != null)
        {
            return;
        }

        _lastZoom = zoom;
        _lastOffset = offset;

        TransformedRings = new List<Vector2[]>(PolygonRings.Count);
        foreach (var ring in PolygonRings)
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

    public Province(string id, string name, string countryId, List<Vector2[]> polygonRings)
    {
        Id = id;
        Name = name;
        CountryId = countryId;

        // Filtere und sortiere Ringe - berechne Flaeche nur einmal pro Ring
        var ringsWithArea = polygonRings
            .Where(r => r.Length >= 3)
            .Select(r => (Ring: r, Area: PolygonUtils.CalculateRingArea(r)))
            .OrderByDescending(x => x.Area)
            .ToList();

        PolygonRings = ringsWithArea.Select(x => x.Ring).ToList();
        CachedArea = ringsWithArea.Sum(x => x.Area);

        LabelPosition = PolygonRings.Count > 0
            ? PolygonUtils.CalculateCenter(PolygonRings[0])
            : Vector2.Zero;

        // Trianguliere alle Polygon-Ringe
        TrianglesPerRing = new List<(int, int, int)[]>(PolygonRings.Count);
        foreach (var ring in PolygonRings)
        {
            TrianglesPerRing.Add(PolygonUtils.TriangulateRing(ring));
        }

        // Bounding Box vorberechnen
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
