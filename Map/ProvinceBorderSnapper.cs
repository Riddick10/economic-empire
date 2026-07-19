using System.Numerics;

namespace GrandStrategyGame.Map;

/// <summary>
/// Richtet Provinzgrenzen an der Landeskontur aus ("Snapping").
///
/// Hintergrund: Laenderumrisse (countries.geojson) und Provinzpolygone
/// (separate GeoJSON-Dateien) stammen aus unterschiedlichen Quellen mit
/// unterschiedlicher Aufloesung, und Provinzen werden beim Laden zusaetzlich
/// Douglas-Peucker-vereinfacht. Ohne Korrektur weichen Provinzgrenzen an
/// Kuesten und Landesgrenzen sichtbar von der Landeskontur ab (haessliche
/// Doppellinien).
///
/// Loesung: Beim Laden werden alle Provinz-Eckpunkte, die nahe an der
/// Landeskontur liegen, exakt auf diese projiziert. Kanten, deren beide
/// Endpunkte auf der Kontur liegen, werden als "Aussenkanten" markiert und
/// beim Zeichnen der Provinzgrenzen uebersprungen - dort zeichnet allein
/// die (dickere) Landesgrenze die Kontur.
/// </summary>
internal sealed class ProvinceBorderSnapper
{
    // Segmente laenger als das sind Datums-/Wrap-Artefakte, keine echten Grenzen
    private const float MaxSegmentLength = 200f;

    private readonly SegmentGrid _grid;
    private readonly float _radiusSq;

    /// <summary>
    /// Baut den raeumlichen Index ueber die Landeskontur einmal auf -
    /// danach koennen alle Provinzen des Landes damit gesnappt werden.
    /// </summary>
    public ProvinceBorderSnapper(List<Vector2[]> countryRings, float snapRadius)
    {
        _grid = new SegmentGrid(countryRings, snapRadius);
        _radiusSq = snapRadius * snapRadius;
    }

    /// <summary>
    /// Snappt Provinzringe auf die Landeskontur (alles in Map-Koordinaten).
    /// outerEdgeFlags[ring][i] == true bedeutet: die Kante von Punkt i zum
    /// Folgepunkt liegt auf der Landeskontur (Kueste oder Landesgrenze).
    /// </summary>
    public List<Vector2[]> Snap(List<Vector2[]> provinceRings, out List<bool[]> outerEdgeFlags)
    {
        outerEdgeFlags = new List<bool[]>(provinceRings.Count);
        var result = new List<Vector2[]>(provinceRings.Count);

        var grid = _grid;
        float radiusSq = _radiusSq;

        foreach (var ring in provinceRings)
        {
            var points = new List<Vector2>(ring.Length);
            var snapped = new List<bool>(ring.Length);

            foreach (var p in ring)
            {
                bool wasSnapped = grid.TryProject(p, radiusSq, out var projected);
                var newPoint = wasSnapped ? projected : p;

                // Durch Snapping entstandene Duplikate direkt ueberspringen
                if (points.Count > 0 && Vector2.DistanceSquared(points[^1], newPoint) < 0.0001f)
                {
                    if (wasSnapped) snapped[^1] = true;
                    continue;
                }

                points.Add(newPoint);
                snapped.Add(wasSnapped);
            }

            // Schliessende Duplikate am Ringende entfernen
            while (points.Count > 1 && Vector2.DistanceSquared(points[0], points[^1]) < 0.0001f)
            {
                if (snapped[^1]) snapped[0] = true;
                points.RemoveAt(points.Count - 1);
                snapped.RemoveAt(snapped.Count - 1);
            }

            // Aussenkanten markieren: beide Endpunkte liegen auf der Kontur
            var flags = new bool[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                flags[i] = snapped[i] && snapped[next];
            }

            result.Add(points.ToArray());
            outerEdgeFlags.Add(flags);
        }

        return result;
    }

    /// <summary>
    /// Raeumlicher Index ueber die Liniensegmente der Landeskontur.
    /// Zellen-Grid, damit das Snapping pro Punkt nur Nachbarsegmente prueft
    /// statt der kompletten Kontur (O(1) statt O(n) pro Punkt).
    /// </summary>
    private sealed class SegmentGrid
    {
        private readonly Dictionary<long, List<int>> _cells = new();
        private readonly List<(Vector2 A, Vector2 B)> _segments = new();
        private readonly float _cellSize;

        public SegmentGrid(List<Vector2[]> rings, float snapRadius)
        {
            // Zellgroesse >= Snap-Radius, damit die 3x3-Nachbarschaftssuche
            // garantiert alle Segmente im Radius findet
            _cellSize = Math.Max(snapRadius, 0.25f);

            foreach (var ring in rings)
            {
                if (ring.Length < 2) continue;

                for (int i = 0; i < ring.Length; i++)
                {
                    var a = ring[i];
                    var b = ring[(i + 1) % ring.Length];

                    if (Vector2.DistanceSquared(a, b) > MaxSegmentLength * MaxSegmentLength)
                        continue;

                    int index = _segments.Count;
                    _segments.Add((a, b));

                    // Segment in alle Zellen seiner Bounding Box eintragen
                    int minX = CellOf(Math.Min(a.X, b.X));
                    int maxX = CellOf(Math.Max(a.X, b.X));
                    int minY = CellOf(Math.Min(a.Y, b.Y));
                    int maxY = CellOf(Math.Max(a.Y, b.Y));

                    for (int cx = minX; cx <= maxX; cx++)
                    {
                        for (int cy = minY; cy <= maxY; cy++)
                        {
                            long key = Key(cx, cy);
                            if (!_cells.TryGetValue(key, out var list))
                                _cells[key] = list = new List<int>();
                            list.Add(index);
                        }
                    }
                }
            }
        }

        private int CellOf(float v) => (int)MathF.Floor(v / _cellSize);
        private static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        /// <summary>
        /// Projiziert den Punkt auf das naechstgelegene Kontursegment,
        /// sofern eines innerhalb des Snap-Radius liegt.
        /// </summary>
        public bool TryProject(Vector2 p, float radiusSq, out Vector2 projected)
        {
            projected = p;
            float bestDistSq = radiusSq;
            bool found = false;

            int cx = CellOf(p.X);
            int cy = CellOf(p.Y);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!_cells.TryGetValue(Key(cx + dx, cy + dy), out var list))
                        continue;

                    foreach (int idx in list)
                    {
                        var (a, b) = _segments[idx];
                        var candidate = ClosestPointOnSegment(p, a, b);
                        float distSq = Vector2.DistanceSquared(p, candidate);
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            projected = candidate;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1e-12f) return a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }
    }
}
