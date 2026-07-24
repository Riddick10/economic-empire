using System.Numerics;

namespace GrandStrategyGame.Map;

/// <summary>
/// Nachbarschaftsgraph aller Provinzen fuer HOI4-artiges Pathfinding: Einheiten
/// bewegen sich von Provinz zu benachbarter Provinz statt auf Luftlinie.
///
/// Aufbau: Provinzen sind Nachbarn, wenn sie sich einen echten Grenzabschnitt
/// teilen. Dazu werden alle Polygon-Kanten in feinen Abstaenden abgetastet und
/// auf ein Raster (Zellgroesse = delta) gehasht. Provinzen, die sich >= 2 Zellen
/// teilen (also mehr als eine blosse Eckberuehrung), gelten als benachbart. Das
/// Abtasten macht die Erkennung robust gegen unterschiedliche Punktdichten und
/// leicht abweichende Grenzverlaeufe aus verschiedenen GeoJSON-Quellen
/// (laenderuebergreifende Grenzen) - offline an echten Geodaten verifiziert.
/// </summary>
public class ProvinceGraph
{
    private readonly Dictionary<string, List<string>> _neighbors = new();

    public int ProvinceCount => _neighbors.Count;
    public int EdgeCount { get; private set; }

    public IReadOnlyList<string> GetNeighbors(string provinceId) =>
        _neighbors.TryGetValue(provinceId, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>();

    public bool HasAnyNeighbor(string provinceId) =>
        _neighbors.TryGetValue(provinceId, out var list) && list.Count > 0;

    /// <summary>
    /// Baut den Nachbarschaftsgraphen aus den Provinz-Polygonen.
    /// </summary>
    /// <param name="delta">Rasterzellgroesse in Karteneinheiten (Toleranz fuer
    /// gemeinsame Grenzen). ~1.0 auf der 2000x1000-Karte hat sich als robust
    /// erwiesen (deckt auch laenderuebergreifende Grenzen ab, ohne Falsch-Nachbarn).</param>
    public void Build(IReadOnlyCollection<Province> provinces, float delta = 1.0f)
    {
        _neighbors.Clear();
        EdgeCount = 0;

        float step = delta * 0.6f;      // Abtastabstand entlang der Kanten
        float inv = 1f / delta;

        // Raster-Zelle -> Provinzen, die diese Zelle beruehren
        var cellProvinces = new Dictionary<long, HashSet<string>>();

        static long CellKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        foreach (var p in provinces)
        {
            // Jede Provinz-ID im Graph registrieren (auch ohne Nachbarn -> Inseln)
            if (!_neighbors.ContainsKey(p.Id))
                _neighbors[p.Id] = new List<string>();

            foreach (var ring in p.PolygonRings)
            {
                if (ring.Length < 2) continue;
                for (int i = 0; i < ring.Length; i++)
                {
                    Vector2 a = ring[i];
                    Vector2 b = ring[(i + 1) % ring.Length];
                    float len = Vector2.Distance(a, b);
                    int n = Math.Max(1, (int)(len / step));
                    for (int k = 0; k <= n; k++)
                    {
                        float t = (float)k / n;
                        float x = a.X + (b.X - a.X) * t;
                        float y = a.Y + (b.Y - a.Y) * t;
                        int cx = (int)MathF.Round(x * inv);
                        int cy = (int)MathF.Round(y * inv);
                        long key = CellKey(cx, cy);
                        if (!cellProvinces.TryGetValue(key, out var set))
                        {
                            set = new HashSet<string>();
                            cellProvinces[key] = set;
                        }
                        set.Add(p.Id);
                    }
                }
            }
        }

        // Gemeinsame Zellen pro Provinzpaar zaehlen
        var sharedCells = new Dictionary<(string, string), int>();
        foreach (var set in cellProvinces.Values)
        {
            if (set.Count < 2) continue;
            // Alle ungeordneten Paare in dieser Zelle
            var arr = set.ToArray();
            for (int i = 0; i < arr.Length; i++)
                for (int j = i + 1; j < arr.Length; j++)
                {
                    var pair = string.CompareOrdinal(arr[i], arr[j]) < 0
                        ? (arr[i], arr[j]) : (arr[j], arr[i]);
                    sharedCells.TryGetValue(pair, out int c);
                    sharedCells[pair] = c + 1;
                }
        }

        // Kanten mit >= 2 gemeinsamen Zellen als Nachbarschaft eintragen
        const int minSharedCells = 2;
        foreach (var kv in sharedCells)
        {
            if (kv.Value < minSharedCells) continue;
            var (a, b) = kv.Key;
            _neighbors[a].Add(b);
            _neighbors[b].Add(a);
            EdgeCount++;
        }
    }

    /// <summary>
    /// A*-Kuerzester-Pfad von Start- zu Zielprovinz entlang benachbarter Provinzen.
    /// Kantengewicht = Distanz zwischen den Provinz-Zentren, Heuristik = Luftlinie
    /// zum Ziel (zulaessig -> optimaler Pfad).
    ///
    /// <paramref name="passable"/> entscheidet, ob eine Zwischen-Provinz betreten
    /// werden darf (eigen/verbuendet/im-Krieg = ja, neutral = umgehen). Die
    /// Zielprovinz ist immer erlaubt (der Spieler hat sie bewusst angeklickt).
    /// Gibt null zurueck, wenn kein Landweg existiert -> Aufrufer nutzt Luftlinie.
    /// </summary>
    public List<string>? FindPath(string startId, string goalId,
        IReadOnlyDictionary<string, Province> provinces, Func<string, bool> passable)
    {
        if (startId == goalId) return new List<string> { startId };
        if (!provinces.TryGetValue(startId, out _) ||
            !provinces.TryGetValue(goalId, out var goalProv))
            return null;
        if (!_neighbors.ContainsKey(startId)) return null;

        Vector2 goalPos = goalProv.LabelPosition;

        var open = new PriorityQueue<string, float>();
        var cameFrom = new Dictionary<string, string>();
        var gScore = new Dictionary<string, float> { [startId] = 0f };
        var closed = new HashSet<string>();

        open.Enqueue(startId, 0f);

        while (open.Count > 0)
        {
            string current = open.Dequeue();
            if (current == goalId)
                return Reconstruct(cameFrom, current);
            if (!closed.Add(current)) continue;

            Vector2 curPos = provinces[current].LabelPosition;
            float curG = gScore[current];

            foreach (var nb in GetNeighbors(current))
            {
                // Zwischenprovinzen muessen passierbar sein; das Ziel immer erlaubt
                if (nb != goalId && !passable(nb)) continue;
                if (!provinces.TryGetValue(nb, out var nbProv)) continue;

                float tentative = curG + Vector2.Distance(curPos, nbProv.LabelPosition);
                if (gScore.TryGetValue(nb, out float known) && tentative >= known) continue;

                gScore[nb] = tentative;
                cameFrom[nb] = current;
                float f = tentative + Vector2.Distance(nbProv.LabelPosition, goalPos);
                open.Enqueue(nb, f);
            }
        }

        return null; // kein Landweg
    }

    private static List<string> Reconstruct(Dictionary<string, string> cameFrom, string current)
    {
        var path = new List<string> { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}
