using System.Text.Json;
using GrandStrategyGame.Data;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet aktive Konflikte und Kriege weltweit
/// </summary>
public class ConflictManager : GameSystemBase
{
    public override string Name => "Conflicts";
    public override int Priority => 160;
    public override TickType[] SubscribedTicks => new[] { TickType.Monthly };

    private readonly List<Conflict> _conflicts = new();
    private readonly HashSet<string> _countriesAtWar = new();
    private MilitaryManager? _militaryManager;

    /// <summary>
    /// Alle aktiven Konflikte
    /// </summary>
    public IReadOnlyList<Conflict> Conflicts => _conflicts;

    /// <summary>
    /// Alle Laender die aktuell in einem Konflikt sind
    /// </summary>
    public IReadOnlySet<string> CountriesAtWar => _countriesAtWar;

    public override void Initialize(GameContext context)
    {
        _militaryManager = context.Game.GetSystem<MilitaryManager>();
        LoadConflicts();
    }

    private void LoadConflicts()
    {
        string basePath = CountryDataLoader.FindBasePath();
        string conflictsPath = Path.Combine(basePath, "Data", "conflicts.json");

        if (!File.Exists(conflictsPath))
        {
            Console.WriteLine("[ConflictManager] conflicts.json nicht gefunden.");
            return;
        }

        try
        {
            string json = File.ReadAllText(conflictsPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("conflicts", out var conflictsArray))
            {
                foreach (var conflictElement in conflictsArray.EnumerateArray())
                {
                    // Sichere JSON-Parsing mit TryGetProperty
                    var conflict = new Conflict
                    {
                        Id = conflictElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "",
                        Name = conflictElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "",
                        Type = conflictElement.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "conflict" : "conflict",
                        Intensity = conflictElement.TryGetProperty("intensity", out var intensityProp) ? intensityProp.GetString() ?? "medium" : "medium",
                        StartYear = conflictElement.TryGetProperty("startYear", out var yearProp) ? yearProp.GetInt32() : 2024,
                        Description = conflictElement.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : ""
                    };

                    if (conflictElement.TryGetProperty("countries", out var countriesArray))
                    {
                        foreach (var country in countriesArray.EnumerateArray())
                        {
                            string countryId = country.GetString() ?? "";
                            if (!string.IsNullOrEmpty(countryId))
                            {
                                conflict.Countries.Add(countryId);
                                _countriesAtWar.Add(countryId);
                            }
                        }
                    }

                    // Konflikt-Zonen (Provinz-Namen) laden
                    if (conflictElement.TryGetProperty("conflictZones", out var zonesArray))
                    {
                        foreach (var zone in zonesArray.EnumerateArray())
                        {
                            string zoneName = zone.GetString() ?? "";
                            if (!string.IsNullOrEmpty(zoneName))
                            {
                                conflict.ConflictZones.Add(zoneName);
                            }
                        }
                    }

                    _conflicts.Add(conflict);
                }
            }

            Console.WriteLine($"[ConflictManager] {_conflicts.Count} Konflikte geladen, {_countriesAtWar.Count} Laender betroffen.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConflictManager] Fehler beim Laden: {ex.Message}");
        }
    }

    /// <summary>
    /// Prueft ob ein Land in einem Konflikt ist
    /// </summary>
    public bool IsCountryAtWar(string countryId)
    {
        return _countriesAtWar.Contains(countryId);
    }

    /// <summary>
    /// Gibt den Konflikt fuer ein Land zurueck (falls vorhanden)
    /// </summary>
    public Conflict? GetConflictForCountry(string countryId)
    {
        return _conflicts.FirstOrDefault(c => c.Countries.Contains(countryId));
    }

    /// <summary>
    /// Gibt die Intensitaet des Konflikts fuer ein Land zurueck
    /// </summary>
    public string GetConflictIntensity(string countryId)
    {
        var conflict = GetConflictForCountry(countryId);
        return conflict?.Intensity ?? "none";
    }

    /// <summary>
    /// Gibt die Konflikt-Zonen (Provinz-Namen) fuer ein Land zurueck
    /// </summary>
    public IReadOnlyList<string> GetConflictZones(string countryId)
    {
        var conflict = GetConflictForCountry(countryId);
        return conflict?.ConflictZones ?? new List<string>();
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        if (tickType == TickType.Monthly)
        {
            UpdateConflictEffects(context);
        }
    }

    /// <summary>
    /// Aktualisiert die Auswirkungen aktiver Konflikte auf betroffene Laender
    /// </summary>
    private void UpdateConflictEffects(GameContext context)
    {
        var politicsManager = context.Game.GetSystem<PoliticsManager>();

        foreach (var conflict in _conflicts)
        {
            // Intensitaets-Multiplikator
            double intensityFactor = conflict.Intensity switch
            {
                "high" => 1.0,
                "medium" => 0.5,
                "low" => 0.2,
                _ => 0.3
            };

            // Typ-Multiplikator (Buergerkriege sind schlimmer als externe Konflikte)
            double typeFactor = conflict.Type switch
            {
                "civil_war" => 1.5,
                "war" => 1.0,
                "insurgency" => 0.7,
                "drug_war" => 0.5,
                _ => 0.6
            };

            double impact = intensityFactor * typeFactor;

            foreach (var countryId in conflict.Countries)
            {
                if (!context.Countries.TryGetValue(countryId, out var country))
                    continue;

                // Stabilitaet sinkt durch Konflikte
                var politics = politicsManager?.GetPolitics(countryId);
                if (politics != null)
                {
                    politics.Stability = Math.Clamp(politics.Stability - impact * 0.01, 0, 1);
                    politics.WarSupport = Math.Clamp(politics.WarSupport + impact * 0.005, 0, 1);
                }

                // Wirtschaftliche Auswirkungen: BIP-Wachstum sinkt
                country.GDPGrowthRate -= impact * 0.002;

                // Infrastrukturschaden durch Konfliktzonen
                if (conflict.ConflictZones.Count > 0)
                {
                    country.Budget -= country.GDP * impact * 0.0005; // Kosten fuer Wiederaufbau
                }

                // Militaerische Auswirkungen: Kriegsmuedigkeit steigt
                if (_militaryManager != null)
                {
                    var strength = _militaryManager.GetMilitaryStrength(countryId);
                    if (strength != null)
                    {
                        // Kriegsmuedigkeit basierend auf Intensitaet und Konfliktdauer
                        int yearsAtWar = Math.Max(1, context.CurrentYear - conflict.StartYear);
                        double exhaustionIncrease = impact * 0.005 * Math.Min(yearsAtWar, 5);
                        strength.WarExhaustion = Math.Clamp(strength.WarExhaustion + exhaustionIncrease, 0, 1);

                        // Moral sinkt bei laengeren Konflikten
                        strength.Morale = Math.Clamp(strength.Morale - impact * 0.002, 0.3, 1.0);
                    }
                }
            }
        }
    }

    public override void Shutdown()
    {
        _conflicts.Clear();
        _countriesAtWar.Clear();
    }
}

/// <summary>
/// Repraesentiert einen aktiven Konflikt
/// </summary>
public class Conflict
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Countries { get; set; } = new();
    public List<string> ConflictZones { get; set; } = new(); // Provinz-Namen wo gekaempft wird
    public string Type { get; set; } = "conflict"; // war, civil_war, insurgency, drug_war
    public string Intensity { get; set; } = "medium"; // low, medium, high
    public int StartYear { get; set; }
    public string Description { get; set; } = "";
}
