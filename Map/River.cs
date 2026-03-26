using System.Numerics;

namespace GrandStrategyGame.Map;

/// <summary>
/// Repräsentiert einen Fluss als Linienzug
/// </summary>
public class River
{
    public string? Name { get; set; }
    public int ScaleRank { get; set; }  // 1 = wichtigster Fluss, 6 = kleinster
    public List<Vector2[]> LineSegments { get; set; }  // Mehrere Segmente (MultiLineString)

    // Cache fuer transformierte Punkte (Screen-Koordinaten)
    public List<Vector2[]>? TransformedSegments { get; private set; }
    private float _lastZoom = -1;
    private Vector2 _lastOffset = new(-99999, -99999);

    public River(string? name, int scaleRank, List<Vector2[]> lineSegments)
    {
        Name = name;
        ScaleRank = scaleRank;
        LineSegments = lineSegments;
    }

    /// <summary>
    /// Aktualisiert die gecachten Screen-Koordinaten wenn sich Viewport geaendert hat
    /// </summary>
    public void UpdateTransformedPoints(float zoom, Vector2 offset, Func<Vector2, Vector2> mapToScreen)
    {
        if (Math.Abs(zoom - _lastZoom) < 0.0001f &&
            Math.Abs(offset.X - _lastOffset.X) < 0.1f &&
            Math.Abs(offset.Y - _lastOffset.Y) < 0.1f &&
            TransformedSegments != null)
        {
            return;
        }

        _lastZoom = zoom;
        _lastOffset = offset;

        TransformedSegments = new List<Vector2[]>(LineSegments.Count);
        foreach (var segment in LineSegments)
        {
            var transformed = new Vector2[segment.Length];
            for (int i = 0; i < segment.Length; i++)
            {
                transformed[i] = mapToScreen(segment[i]);
            }
            TransformedSegments.Add(transformed);
        }
    }

    /// <summary>
    /// Gibt die Linienbreite basierend auf ScaleRank zurück
    /// </summary>
    public float GetLineWidth(float zoom)
    {
        // Wichtigere Flüsse (niedrigerer ScaleRank) sind breiter
        float baseWidth = ScaleRank switch
        {
            1 => 1.5f,
            2 => 1.25f,
            3 => 1.0f,
            4 => 0.75f,
            5 => 0.6f,
            _ => 0.5f
        };

        // Skaliere mit Zoom, aber nicht zu dünn/dick
        return Math.Clamp(baseWidth * zoom * 0.3f, 0.3f, 2.5f);
    }
}
