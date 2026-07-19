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
///
/// Zusaetzlich: Manche Provinzdaten enthalten Seegebiete, die weit ueber die
/// Landeskontur hinausragen (finnische Schaeren, niederlaendisches Wattenmeer).
/// Kanten mit mindestens einem Endpunkt ausserhalb des Landespolygons werden
/// deshalb ebenfalls markiert und nicht gezeichnet.
/// </summary>
internal sealed class ProvinceBorderSnapper
{
    // Segmente laenger als das sind Datums-/Wrap-Artefakte, keine echten Grenzen
    private const float MaxSegmentLength = 200f;

    private readonly SegmentGrid _grid;
    private readonly float _radiusSq;

    // Landesringe + Bounding Boxes fuer den Punkt-im-Land-Test
    private readonly List<Vector2[]> _countryRings;
    private readonly (float MinX, float MaxX, float MinY, float MaxY)[] _ringBounds;

    /// <summary>
    /// Baut den raeumlichen Index ueber die Landeskontur einmal auf -
    /// danach koennen alle Provinzen des Landes damit gesnappt werden.
    /// </summary>
    public ProvinceBorderSnapper(List<Vector2[]> countryRings, float snapRadius)
    {
        _grid = new SegmentGrid(countryRings, snapRadius);
        _radiusSq = snapRadius * snapRadius;

        _countryRings = countryRings;
        _ringBounds = new (float, float, float, float)[countryRings.Count];
        for (int r = 0; r < countryRings.Count; r++)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in countryRings[r])
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            _ringBounds[r] = (minX, maxX, minY, maxY);
        }
    }

    /// <summary>
    /// Liegt der Punkt innerhalb des Landespolygons (irgendein Ring)?
    /// Bounding-Box-Vorfilter haelt den Test auch bei grossen Konturen schnell.
    /// </summary>
    private bool IsInsideCountry(Vector2 p)
    {
        for (int r = 0; r < _countryRings.Count; r++)
        {
            var (minX, maxX, minY, maxY) = _ringBounds[r];
            if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY) continue;
            if (PolygonUtils.ContainsPoint(_countryRings[r], p)) return true;
        }
        return false;
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

            // Punkte ausserhalb des Landespolygons ermitteln (Seegebiete:
            // manche Provinzdaten reichen weit ins Meer, z.B. Bottnischer
            // Meerbusen in Finnland oder Wattenmeer in den Niederlanden).
            // Gesnappte Punkte liegen per Definition auf der Kontur -> "innen".
            var outside = new bool[points.Count];
            bool anyOutside = false;
            bool allOutside = true;
            for (int i = 0; i < points.Count; i++)
            {
                outside[i] = !snapped[i] && !IsInsideCountry(points[i]);
                anyOutside |= outside[i];
                allOutside &= outside[i];
            }

            // Ring liegt komplett im Meer (kein Pendant in der Landeskontur,
            // z.B. reine Seegebiets-Ringe oder winzige Schaeren): verwerfen
            if (allOutside || points.Count < 3)
                continue;

            var chordEdges = new HashSet<int>();
            if (anyOutside)
            {
                // Meer-Abschnitte herausschneiden und durch den echten
                // Kuestenverlauf der Landeskontur ersetzen (Splicing).
                // Danach folgen Fuellung, Auswahl-Umriss und Klick-Flaechen
                // exakt der Kueste statt ins Meer zu ragen.
                SpliceSeaRuns(points, snapped, outside, chordEdges);
                if (points.Count < 3) continue;
            }

            // Nicht zu zeichnende Kanten markieren:
            // - beide Endpunkte auf der Kontur (Kueste/Landesgrenze), ODER
            // - Notfall-Sehne (Splice zwischen verschiedenen Konturringen nicht moeglich)
            var flags = new bool[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                flags[i] = (snapped[i] && snapped[next]) || chordEdges.Contains(i);
            }

            result.Add(points.ToArray());
            outerEdgeFlags.Add(flags);
        }

        return result;
    }

    /// <summary>
    /// Ersetzt zusammenhaengende Laeufe von Meer-Punkten durch den Verlauf
    /// der Landeskontur zwischen den beiden Anschlusspunkten an Land.
    /// points/snapped werden in-place umgebaut; chordEdges sammelt Kanten,
    /// bei denen kein Splice moeglich war (Anschluss auf verschiedenen
    /// Konturringen) - diese Sehnen werden beim Zeichnen uebersprungen.
    /// </summary>
    private void SpliceSeaRuns(List<Vector2> points, List<bool> snapped, bool[] outside,
        HashSet<int> chordEdges)
    {
        int n = points.Count;

        // Ring so rotieren, dass er mit einem Land-Punkt beginnt -
        // dann koennen Meer-Laeufe nicht ueber das Ringende wickeln
        int start = Array.FindIndex(outside, o => !o);

        var newPoints = new List<Vector2>(n);
        var newSnapped = new List<bool>(n);

        void Append(Vector2 p, bool isSnapped)
        {
            if (newPoints.Count > 0 && Vector2.DistanceSquared(newPoints[^1], p) < 0.0001f)
            {
                if (isSnapped) newSnapped[^1] = true;
                return;
            }
            newPoints.Add(p);
            newSnapped.Add(isSnapped);
        }

        int k = 0;
        while (k < n)
        {
            int idx = (start + k) % n;

            if (!outside[idx])
            {
                Append(points[idx], snapped[idx]);
                k++;
                continue;
            }

            // Meer-Lauf: Ende suchen (kann nicht wickeln, da start an Land liegt)
            int runEnd = k;
            while (runEnd < n && outside[(start + runEnd) % n]) runEnd++;

            // Anschlusspunkte: letzter Land-Punkt davor, erster Land-Punkt danach
            Vector2 landBefore = newPoints[^1];
            int afterIdx = (start + runEnd) % n;   // == start wenn Lauf am Ringende
            Vector2 landAfter = points[afterIdx];

            var from = BruteForceProject(landBefore);
            var to = BruteForceProject(landAfter);

            if (from.RingIndex >= 0 && from.RingIndex == to.RingIndex)
            {
                // Kuestenverlauf zwischen den Projektionen einfuegen
                var path = BuildCoastPath(from, to);
                foreach (var p in path)
                    Append(p, isSnapped: true);
            }
            else
            {
                // Kein gemeinsamer Konturring: Sehne markieren, damit die
                // direkte Verbindung nicht als Provinzgrenze gezeichnet wird
                chordEdges.Add(newPoints.Count - 1);
            }

            k = runEnd;
        }

        // Schliessende Duplikate entfernen (Splice am Ringende kann den
        // Startpunkt erneut erzeugen)
        while (newPoints.Count > 1 && Vector2.DistanceSquared(newPoints[0], newPoints[^1]) < 0.0001f)
        {
            if (newSnapped[^1]) newSnapped[0] = true;
            newPoints.RemoveAt(newPoints.Count - 1);
            newSnapped.RemoveAt(newSnapped.Count - 1);
        }

        points.Clear();
        points.AddRange(newPoints);
        snapped.Clear();
        snapped.AddRange(newSnapped);
    }

    /// <summary>
    /// Projiziert einen Punkt ohne Radius-Begrenzung auf die naechstgelegene
    /// Stelle der Landeskontur. Nur fuer Splice-Anschlusspunkte (selten).
    /// </summary>
    private (int RingIndex, int SegIndex, float T, Vector2 Point) BruteForceProject(Vector2 p)
    {
        int bestRing = -1, bestSeg = -1;
        float bestT = 0f, bestDistSq = float.MaxValue;
        Vector2 bestPoint = p;

        for (int r = 0; r < _countryRings.Count; r++)
        {
            var ring = _countryRings[r];
            if (ring.Length < 2) continue;

            for (int s = 0; s < ring.Length; s++)
            {
                var a = ring[s];
                var b = ring[(s + 1) % ring.Length];
                if (Vector2.DistanceSquared(a, b) > MaxSegmentLength * MaxSegmentLength)
                    continue;

                var ab = b - a;
                float lenSq = ab.LengthSquared();
                float t = lenSq < 1e-12f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
                var candidate = a + ab * t;
                float distSq = Vector2.DistanceSquared(p, candidate);

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestRing = r;
                    bestSeg = s;
                    bestT = t;
                    bestPoint = candidate;
                }
            }
        }

        return (bestRing, bestSeg, bestT, bestPoint);
    }

    /// <summary>
    /// Baut den Kuestenpfad zwischen zwei Projektionen auf demselben
    /// Konturring. Von beiden Laufrichtungen wird die kuerzere gewaehlt.
    /// </summary>
    private List<Vector2> BuildCoastPath(
        (int RingIndex, int SegIndex, float T, Vector2 Point) from,
        (int RingIndex, int SegIndex, float T, Vector2 Point) to)
    {
        var ring = _countryRings[from.RingIndex];
        int m = ring.Length;

        // Vorwaerts: from.Point -> V[seg+1] -> V[seg+2] -> ... -> V[to.Seg] -> to.Point
        var forward = new List<Vector2> { from.Point };
        if (!(from.SegIndex == to.SegIndex && to.T >= from.T))
        {
            int idx = (from.SegIndex + 1) % m;
            for (int steps = 0; steps < m; steps++)
            {
                forward.Add(ring[idx]);
                if (idx == to.SegIndex) break;
                idx = (idx + 1) % m;
            }
        }
        forward.Add(to.Point);

        // Rueckwaerts: from.Point -> V[seg] -> V[seg-1] -> ... -> V[to.Seg+1] -> to.Point
        var backward = new List<Vector2> { from.Point };
        if (!(from.SegIndex == to.SegIndex && to.T <= from.T))
        {
            int idx = from.SegIndex;
            for (int steps = 0; steps < m; steps++)
            {
                backward.Add(ring[idx]);
                if (idx == (to.SegIndex + 1) % m) break;
                idx = (idx - 1 + m) % m;
            }
        }
        backward.Add(to.Point);

        return PathLength(forward) <= PathLength(backward) ? forward : backward;
    }

    private static float PathLength(List<Vector2> path)
    {
        float length = 0;
        for (int i = 1; i < path.Count; i++)
            length += Vector2.Distance(path[i - 1], path[i]);
        return length;
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
