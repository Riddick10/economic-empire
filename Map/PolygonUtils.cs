using System.Numerics;

namespace GrandStrategyGame;

/// <summary>
/// Gemeinsame Hilfsmethoden fuer Polygon-Berechnungen
/// Eliminiert Code-Duplikation zwischen MapRegion und Province
/// </summary>
public static class PolygonUtils
{
    /// <summary>
    /// Berechnet die Flaeche eines Polygon-Rings (Shoelace-Formel)
    /// </summary>
    public static float CalculateRingArea(Vector2[] ring)
    {
        if (ring.Length < 3) return 0;

        float area = 0;
        for (int i = 0; i < ring.Length; i++)
        {
            int j = (i + 1) % ring.Length;
            area += ring[i].X * ring[j].Y;
            area -= ring[j].X * ring[i].Y;
        }
        return Math.Abs(area) * 0.5f;
    }

    /// <summary>
    /// Berechnet die vorzeichenbehaftete Flaeche (fuer Windungsrichtung)
    /// </summary>
    public static float GetSignedArea(Vector2[] ring)
    {
        float area = 0;
        for (int i = 0; i < ring.Length; i++)
        {
            int j = (i + 1) % ring.Length;
            area += ring[i].X * ring[j].Y;
            area -= ring[j].X * ring[i].Y;
        }
        return area * 0.5f;
    }

    /// <summary>
    /// Berechnet das Zentrum eines Polygons
    /// </summary>
    public static Vector2 CalculateCenter(Vector2[] ring)
    {
        if (ring.Length == 0) return Vector2.Zero;

        float sumX = 0, sumY = 0;
        for (int i = 0; i < ring.Length; i++)
        {
            sumX += ring[i].X;
            sumY += ring[i].Y;
        }
        return new Vector2(sumX / ring.Length, sumY / ring.Length);
    }

    /// <summary>
    /// Trianguliert einen Polygon-Ring mit dem Ear Clipping Algorithmus
    /// </summary>
    public static (int, int, int)[] TriangulateRing(Vector2[] ring)
    {
        if (ring.Length < 3)
            return Array.Empty<(int, int, int)>();

        var triangles = new List<(int, int, int)>(ring.Length - 2);
        var indices = new List<int>(ring.Length);

        for (int i = 0; i < ring.Length; i++)
            indices.Add(i);

        if (GetSignedArea(ring) > 0)
            indices.Reverse();

        int safetyCounter = 0;
        int maxIterations = ring.Length * ring.Length;

        while (indices.Count > 3 && safetyCounter < maxIterations)
        {
            safetyCounter++;
            bool earFound = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int prev = (i - 1 + indices.Count) % indices.Count;
                int next = (i + 1) % indices.Count;

                if (IsEar(ring, indices, prev, i, next))
                {
                    triangles.Add((indices[prev], indices[i], indices[next]));
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if (!earFound && indices.Count >= 3)
            {
                triangles.Add((indices[0], indices[1], indices[2]));
                indices.RemoveAt(1);
            }
        }

        if (indices.Count == 3)
        {
            triangles.Add((indices[0], indices[1], indices[2]));
        }

        return triangles.ToArray();
    }

    /// <summary>
    /// Prueft ob ein Punkt im Polygon liegt (Ray-Casting)
    /// </summary>
    public static bool ContainsPoint(Vector2[] ring, Vector2 point)
    {
        bool inside = false;
        int j = ring.Length - 1;

        for (int i = 0; i < ring.Length; i++)
        {
            if ((ring[i].Y < point.Y && ring[j].Y >= point.Y ||
                 ring[j].Y < point.Y && ring[i].Y >= point.Y) &&
                (ring[i].X <= point.X || ring[j].X <= point.X))
            {
                if (ring[i].X + (point.Y - ring[i].Y) /
                    (ring[j].Y - ring[i].Y) * (ring[j].X - ring[i].X) < point.X)
                {
                    inside = !inside;
                }
            }
            j = i;
        }
        return inside;
    }

    private static bool IsEar(Vector2[] ring, List<int> indices, int prev, int curr, int next)
    {
        Vector2 a = ring[indices[prev]];
        Vector2 b = ring[indices[curr]];
        Vector2 c = ring[indices[next]];

        // Konvexitaetstest
        float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (cross >= 0) return false;

        // Pruefe ob kein anderer Punkt im Dreieck liegt
        for (int i = 0; i < indices.Count; i++)
        {
            if (i == prev || i == curr || i == next)
                continue;

            if (PointInTriangle(ring[indices[i]], a, b, c))
                return false;
        }

        return true;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.X - a.X) * (b.Y - a.Y) - (b.X - a.X) * (p.Y - a.Y);
        float d2 = (p.X - b.X) * (c.Y - b.Y) - (c.X - b.X) * (p.Y - b.Y);
        float d3 = (p.X - c.X) * (a.Y - c.Y) - (a.X - c.X) * (p.Y - c.Y);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    /// <summary>
    /// Vereinfacht ein Polygon mit dem Douglas-Peucker Algorithmus
    /// Reduziert die Anzahl der Punkte unter Beibehaltung der Form
    /// </summary>
    /// <param name="points">Original-Punkte</param>
    /// <param name="tolerance">Toleranz - je höher, desto weniger Punkte</param>
    public static Vector2[] SimplifyPolygon(Vector2[] points, float tolerance)
    {
        if (points.Length < 4)
            return points;

        // Douglas-Peucker Algorithmus
        var keepIndices = new List<int> { 0, points.Length - 1 };
        DouglasPeuckerRecursive(points, 0, points.Length - 1, tolerance, keepIndices);

        keepIndices.Sort();

        var simplified = new Vector2[keepIndices.Count];
        for (int i = 0; i < keepIndices.Count; i++)
        {
            simplified[i] = points[keepIndices[i]];
        }

        // Mindestens 3 Punkte für ein gültiges Polygon
        return simplified.Length >= 3 ? simplified : points;
    }

    private static void DouglasPeuckerRecursive(Vector2[] points, int startIdx, int endIdx, float tolerance, List<int> keepIndices)
    {
        if (endIdx - startIdx < 2)
            return;

        float maxDist = 0;
        int maxIdx = startIdx;

        Vector2 start = points[startIdx];
        Vector2 end = points[endIdx];

        for (int i = startIdx + 1; i < endIdx; i++)
        {
            float dist = PerpendicularDistance(points[i], start, end);
            if (dist > maxDist)
            {
                maxDist = dist;
                maxIdx = i;
            }
        }

        if (maxDist > tolerance)
        {
            keepIndices.Add(maxIdx);
            DouglasPeuckerRecursive(points, startIdx, maxIdx, tolerance, keepIndices);
            DouglasPeuckerRecursive(points, maxIdx, endIdx, tolerance, keepIndices);
        }
    }

    private static float PerpendicularDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        float dx = lineEnd.X - lineStart.X;
        float dy = lineEnd.Y - lineStart.Y;

        float lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 0.0001f)
            return Vector2.Distance(point, lineStart);

        float t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0, 1);

        Vector2 projection = new(lineStart.X + t * dx, lineStart.Y + t * dy);
        return Vector2.Distance(point, projection);
    }
}
