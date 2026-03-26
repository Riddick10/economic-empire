using System.Text.Json;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet diplomatische Beziehungen.
/// - Beziehungswerte zwischen Ländern
/// - Bündnisse
/// - Garantien
/// - Diplomatische Aktionen
/// </summary>
public class DiplomacyManager : GameSystemBase
{
    public override string Name => "Diplomacy";
    public override int Priority => 30;
    public override TickType[] SubscribedTicks => new[] { TickType.Daily, TickType.Monthly };

    // Beziehungen zwischen Ländern (-100 bis +100)
    private readonly Dictionary<(string, string), int> _relations = new();

    // Aktive Bündnisse
    private readonly List<Alliance> _alliances = new();

    // Unabhängigkeitsgarantien
    private readonly HashSet<(string Guarantor, string Protected)> _guarantees = new();

    // Bündnis-Mitgliedschaften pro Land
    private readonly Dictionary<string, List<string>> _countryAlliances = new();

    public override void Initialize(GameContext context)
    {
        // Initialisiere Basisbeziehungen (neutral = 0)
        foreach (var country1 in context.Countries.Keys)
        {
            foreach (var country2 in context.Countries.Keys)
            {
                if (country1 != country2)
                {
                    SetRelation(country1, country2, 0);
                }
            }
        }

        // Lade Beziehungen und Bündnisse aus JSON
        LoadDiplomacyFromJson();
    }

    /// <summary>
    /// Lädt diplomatische Daten aus diplomacy.json
    /// </summary>
    private void LoadDiplomacyFromJson()
    {
        string[] searchPaths =
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")
        };

        string? filePath = null;
        foreach (var basePath in searchPaths)
        {
            string testPath = Path.Combine(basePath, "Data", "diplomacy.json");
            if (File.Exists(testPath))
            {
                filePath = testPath;
                break;
            }
        }

        if (filePath == null)
        {
            Console.WriteLine("[DiplomacyManager] diplomacy.json nicht gefunden - verwende Standardwerte");
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Beziehungen laden
            if (root.TryGetProperty("relations", out var relationsArray))
            {
                foreach (var entry in relationsArray.EnumerateArray())
                {
                    string country1 = entry.GetProperty("country1").GetString() ?? "";
                    string country2 = entry.GetProperty("country2").GetString() ?? "";
                    int value = entry.GetProperty("value").GetInt32();
                    if (!string.IsNullOrEmpty(country1) && !string.IsNullOrEmpty(country2))
                    {
                        SetRelation(country1, country2, value);
                    }
                }
            }

            // Bündnis-Mitgliedschaften laden
            if (root.TryGetProperty("alliances", out var alliancesArray))
            {
                foreach (var entry in alliancesArray.EnumerateArray())
                {
                    string countryId = entry.GetProperty("countryId").GetString() ?? "";
                    if (!string.IsNullOrEmpty(countryId) && entry.TryGetProperty("memberships", out var memberships))
                    {
                        var allianceList = new List<string>();
                        foreach (var membership in memberships.EnumerateArray())
                        {
                            string? allianceName = membership.GetString();
                            if (!string.IsNullOrEmpty(allianceName))
                                allianceList.Add(allianceName);
                        }
                        _countryAlliances[countryId] = allianceList;
                    }
                }
            }

            Console.WriteLine($"[DiplomacyManager] Diplomatische Daten aus JSON geladen");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DiplomacyManager] Fehler beim Laden: {ex.Message}");
        }
    }

    /// <summary>
    /// Gibt die Bündnis-Mitgliedschaften eines Landes zurück
    /// </summary>
    public List<string> GetCountryAlliances(string countryId)
    {
        return _countryAlliances.TryGetValue(countryId, out var alliances)
            ? new List<string>(alliances)
            : new List<string>();
    }

    /// <summary>
    /// Prüft ob zwei Länder beide EU-Mitglieder sind
    /// </summary>
    public bool AreBothEUMembers(string country1, string country2)
    {
        var alliances1 = GetCountryAlliances(country1);
        var alliances2 = GetCountryAlliances(country2);
        return alliances1.Contains("EU") && alliances2.Contains("EU");
    }

    /// <summary>
    /// Gibt alle Länder in einem bestimmten Bündnis zurück
    /// </summary>
    public List<string> GetCountriesInAlliance(string allianceName)
    {
        return _countryAlliances
            .Where(kvp => kvp.Value.Contains(allianceName))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        switch (tickType)
        {
            case TickType.Daily:
                // Tägliche diplomatische Events (selten)
                break;

            case TickType.Monthly:
                DecayRelations(context);
                break;
        }
    }

    /// <summary>
    /// Beziehungen verfallen langsam in Richtung neutral
    /// </summary>
    // Wiederverwendbare Liste fuer Dictionary-Iteration mit Modifikation
    private readonly List<(string, string)> _decayKeysBuffer = new();

    private void DecayRelations(GameContext context)
    {
        _decayKeysBuffer.Clear();
        foreach (var key in _relations.Keys)
        {
            int current = _relations[key];
            if (current != 0)
                _decayKeysBuffer.Add(key);
        }
        foreach (var key in _decayKeysBuffer)
        {
            int current = _relations[key];
            if (current > 0)
                _relations[key] = current - 1;
            else
                _relations[key] = current + 1;
        }
    }

    /// <summary>
    /// Gibt die Beziehung zwischen zwei Ländern zurück
    /// </summary>
    public int GetRelation(string country1, string country2)
    {
        var key = GetRelationKey(country1, country2);
        return _relations.TryGetValue(key, out var value) ? value : 0;
    }

    /// <summary>
    /// Setzt die Beziehung zwischen zwei Ländern
    /// </summary>
    public void SetRelation(string country1, string country2, int value)
    {
        var key = GetRelationKey(country1, country2);
        _relations[key] = Math.Clamp(value, -100, 100);
    }

    /// <summary>
    /// Ändert die Beziehung um einen bestimmten Wert
    /// </summary>
    public void ModifyRelation(string country1, string country2, int delta)
    {
        int current = GetRelation(country1, country2);
        SetRelation(country1, country2, current + delta);
    }

    private (string, string) GetRelationKey(string c1, string c2)
    {
        // Konsistente Reihenfolge für symmetrische Beziehungen
        return string.Compare(c1, c2) < 0 ? (c1, c2) : (c2, c1);
    }

    /// <summary>
    /// Gibt alle Beziehungen zurueck (fuer Speichern)
    /// </summary>
    public IReadOnlyDictionary<(string, string), int> GetAllRelations() => _relations;
}

/// <summary>
/// Repräsentiert ein Bündnis zwischen Ländern
/// </summary>
public class Alliance
{
    public string Id { get; set; } = "";
    public string LeaderId { get; set; } = "";
    public List<string> MemberIds { get; set; } = new();
    public AllianceType Type { get; set; }
    public int FoundedYear { get; set; }
    public string? Name { get; set; }
}

public enum AllianceType
{
    DefensivePact,      // Verteidigungsbündnis
    MilitaryAlliance,   // Militärbündnis
    EconomicUnion,      // Wirtschaftsunion
    Federation          // Staatenbund
}
