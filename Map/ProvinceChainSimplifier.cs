namespace GrandStrategyGame.Map;

/// <summary>
/// Topologisch konsistente Vereinfachung der Provinzpolygone eines Landes.
///
/// Problem: Wird jede Provinz unabhaengig Douglas-Peucker-vereinfacht, wird die
/// gemeinsame Grenze zweier Nachbarprovinzen auf beiden Seiten UNTERSCHIEDLICH
/// vereinfacht - die Linien laufen auseinander und beim starken Zoom entstehen
/// sichtbare Doppellinien/Ueberlappungen (z.B. Saarland vs. Rheinland-Pfalz).
///
/// Loesung: Die Ringe werden anhand der Kanten-Eigentuemer in Ketten zerlegt
/// (zusammenhaengende Abschnitte mit identischer Nachbarschaft). Jede Kette wird
/// nur EINMAL vereinfacht (kanonische Richtung + Cache) und von beiden
/// Nachbarprovinzen als exakt gleiche Punktfolge uebernommen. Geteilte Grenzen
/// liegen dadurch nach der Vereinfachung punktgenau aufeinander.
/// </summary>
internal static class ProvinceChainSimplifier
{
    /// <summary>
    /// Vereinfacht alle Provinzpolygone eines Landes mit konsistenten
    /// gemeinsamen Grenzen. Eingabe/Ausgabe in Geo-Koordinaten (Lon/Lat).
    /// </summary>
    public static Dictionary<string, List<double[][]>> Simplify(
        Dictionary<string, List<double[][]>> provincePolygons, double tolerance)
    {
        // === 1) Kanten-Eigentuemer ueber alle Provinzen sammeln ===
        var edgeOwners = new Dictionary<(long, long, long, long), HashSet<string>>();

        foreach (var (id, rings) in provincePolygons)
        {
            foreach (var ring in rings)
            {
                int n = RingLength(ring);
                if (n < 2) continue;

                for (int i = 0; i < n; i++)
                {
                    var a = Key(ring[i]);
                    var b = Key(ring[(i + 1) % n]);
                    if (a == b) continue;

                    var ek = EdgeKey(a, b);
                    if (!edgeOwners.TryGetValue(ek, out var owners))
                        edgeOwners[ek] = owners = new HashSet<string>();
                    owners.Add(id);
                }
            }
        }

        // === 2) Ringe kettenweise vereinfachen (mit gemeinsamem Ketten-Cache) ===
        var chainCache = new Dictionary<string, double[][]>();
        var result = new Dictionary<string, List<double[][]>>();

        foreach (var (id, rings) in provincePolygons)
        {
            var newRings = new List<double[][]>(rings.Count);
            foreach (var ring in rings)
            {
                var simplified = SimplifyRing(ring, edgeOwners, chainCache, tolerance);
                if (simplified.Length >= 3)
                    newRings.Add(simplified);
            }
            result[id] = newRings;
        }

        return result;
    }

    /// <summary>
    /// Effektive Ringlaenge ohne schliessenden Duplikat-Punkt (GeoJSON-Ringe
    /// wiederholen den Startpunkt am Ende)
    /// </summary>
    private static int RingLength(double[][] ring)
    {
        int n = ring.Length;
        while (n > 1 && Key(ring[0]) == Key(ring[n - 1])) n--;
        return n;
    }

    private static (long X, long Y) Key(double[] p) =>
        ((long)Math.Round(p[0] * 1e6), (long)Math.Round(p[1] * 1e6));

    private static (long, long, long, long) EdgeKey((long X, long Y) a, (long X, long Y) b) =>
        (a.X < b.X || (a.X == b.X && a.Y <= b.Y))
            ? (a.X, a.Y, b.X, b.Y)
            : (b.X, b.Y, a.X, a.Y);

    /// <summary>
    /// Signatur einer Kante: sortierte Liste der Eigentuemer-Provinzen
    /// </summary>
    private static string EdgeSignature((long, long, long, long) ek,
        Dictionary<(long, long, long, long), HashSet<string>> edgeOwners)
    {
        if (!edgeOwners.TryGetValue(ek, out var owners)) return "";
        var sorted = new List<string>(owners);
        sorted.Sort(StringComparer.Ordinal);
        return string.Join(",", sorted);
    }

    private static double[][] SimplifyRing(double[][] ring,
        Dictionary<(long, long, long, long), HashSet<string>> edgeOwners,
        Dictionary<string, double[][]> chainCache, double tolerance)
    {
        int n = RingLength(ring);
        if (n < 4)
        {
            var copy = new double[Math.Min(n, ring.Length)][];
            Array.Copy(ring, copy, copy.Length);
            return copy;
        }

        // Kanten-Signaturen (Eigentuemer-Mengen) pro Ringkante
        var sig = new string[n];
        for (int i = 0; i < n; i++)
        {
            var a = Key(ring[i]);
            var b = Key(ring[(i + 1) % n]);
            sig[i] = a == b ? "" : EdgeSignature(EdgeKey(a, b), edgeOwners);
        }

        // Bruchstellen: Vertex i, an dem sich die Kanten-Signatur aendert
        var nodes = new List<int>();
        for (int i = 0; i < n; i++)
        {
            if (sig[(i - 1 + n) % n] != sig[i])
                nodes.Add(i);
        }

        // Kein Wechsel im ganzen Ring (Enklave/reine Kueste):
        // kanonisch rotieren, damit beide Seiten identisch vereinfachen
        if (nodes.Count == 0)
        {
            int start = 0;
            var minKey = Key(ring[0]);
            for (int i = 1; i < n; i++)
            {
                var k = Key(ring[i]);
                if (k.X < minKey.X || (k.X == minKey.X && k.Y < minKey.Y)) { minKey = k; start = i; }
            }

            var rotated = new List<double[]>(n + 1);
            for (int i = 0; i <= n; i++)
                rotated.Add(ring[(start + i) % n]);

            var closed = GetSimplifiedChain(rotated, chainCache, tolerance);
            // schliessenden Duplikat-Punkt wieder entfernen
            var outRing = new double[closed.Length - 1][];
            Array.Copy(closed, outRing, outRing.Length);
            return outRing;
        }

        // Ketten von Bruchstelle zu Bruchstelle vereinfachen und zusammensetzen
        var outPoints = new List<double[]>(n);
        for (int k = 0; k < nodes.Count; k++)
        {
            int start = nodes[k];
            int end = nodes[(k + 1) % nodes.Count];

            var chain = new List<double[]>();
            int idx = start;
            chain.Add(ring[idx]);
            while (idx != end)
            {
                idx = (idx + 1) % n;
                chain.Add(ring[idx]);
            }

            var simplified = GetSimplifiedChain(chain, chainCache, tolerance);

            // Letzten Punkt weglassen - er ist der Startpunkt der naechsten Kette
            for (int i = 0; i < simplified.Length - 1; i++)
                outPoints.Add(simplified[i]);
        }

        return outPoints.ToArray();
    }

    /// <summary>
    /// Vereinfacht eine Kette in kanonischer Richtung und cached das Ergebnis,
    /// damit beide Nachbarprovinzen exakt dieselbe Punktfolge erhalten
    /// </summary>
    private static double[][] GetSimplifiedChain(List<double[]> chain,
        Dictionary<string, double[][]> chainCache, double tolerance)
    {
        string fwdKey = SequenceKey(chain, reverse: false);
        string revKey = SequenceKey(chain, reverse: true);
        bool useReverse = string.CompareOrdinal(revKey, fwdKey) < 0;
        string cacheKey = useReverse ? revKey : fwdKey;

        if (!chainCache.TryGetValue(cacheKey, out var simplified))
        {
            var canonical = useReverse ? ReversedCopy(chain) : chain;
            simplified = DouglasPeucker(canonical, tolerance);
            chainCache[cacheKey] = simplified;
        }

        if (!useReverse) return simplified;

        var back = new double[simplified.Length][];
        for (int i = 0; i < simplified.Length; i++)
            back[i] = simplified[simplified.Length - 1 - i];
        return back;
    }

    private static List<double[]> ReversedCopy(List<double[]> chain)
    {
        var copy = new List<double[]>(chain);
        copy.Reverse();
        return copy;
    }

    private static string SequenceKey(List<double[]> chain, bool reverse)
    {
        var sb = new System.Text.StringBuilder(chain.Count * 16);
        for (int i = 0; i < chain.Count; i++)
        {
            var p = chain[reverse ? chain.Count - 1 - i : i];
            var k = Key(p);
            sb.Append(k.X).Append(':').Append(k.Y).Append(';');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Douglas-Peucker fuer eine offene Kette (Endpunkte bleiben erhalten)
    /// </summary>
    private static double[][] DouglasPeucker(List<double[]> pts, double tolerance)
    {
        int n = pts.Count;
        if (n <= 2 || tolerance <= 0)
            return pts.ToArray();

        var keep = new bool[n];
        keep[0] = keep[n - 1] = true;

        var stack = new Stack<(int Start, int End)>();
        stack.Push((0, n - 1));

        while (stack.Count > 0)
        {
            var (s, e) = stack.Pop();
            if (e - s < 2) continue;

            double maxDist = 0;
            int maxIdx = -1;
            for (int i = s + 1; i < e; i++)
            {
                double d = PerpendicularDistance(pts[i], pts[s], pts[e]);
                if (d > maxDist) { maxDist = d; maxIdx = i; }
            }

            if (maxIdx >= 0 && maxDist > tolerance)
            {
                keep[maxIdx] = true;
                stack.Push((s, maxIdx));
                stack.Push((maxIdx, e));
            }
        }

        var result = new List<double[]>(n);
        for (int i = 0; i < n; i++)
            if (keep[i]) result.Add(pts[i]);
        return result.ToArray();
    }

    private static double PerpendicularDistance(double[] p, double[] a, double[] b)
    {
        double dx = b[0] - a[0];
        double dy = b[1] - a[1];
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-18)
        {
            double ex = p[0] - a[0];
            double ey = p[1] - a[1];
            return Math.Sqrt(ex * ex + ey * ey);
        }

        double t = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / lenSq;
        t = Math.Clamp(t, 0, 1);
        double px = a[0] + t * dx;
        double py = a[1] + t * dy;
        double ddx = p[0] - px;
        double ddy = p[1] - py;
        return Math.Sqrt(ddx * ddx + ddy * ddy);
    }
}
