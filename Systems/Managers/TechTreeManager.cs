using System.Text.Json;
using GrandStrategyGame.Data;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet den Forschungsbaum und Forschungsfortschritt
/// </summary>
public class TechTreeManager : GameSystemBase
{
    public override string Name => "TechTree";
    public override int Priority => 150;
    public override TickType[] SubscribedTicks => new[] { TickType.Daily };

    private readonly Dictionary<string, Technology> _technologies = new();
    private readonly Dictionary<string, TechProgress> _playerProgress = new();
    private string? _currentResearch = null;
    private GameContext? _context;

    /// <summary>
    /// Alle verfuegbaren Technologien
    /// </summary>
    public IReadOnlyDictionary<string, Technology> Technologies => _technologies;

    /// <summary>
    /// Forschungsfortschritt des Spielers
    /// </summary>
    public IReadOnlyDictionary<string, TechProgress> PlayerProgress => _playerProgress;

    /// <summary>
    /// Aktuell erforschte Technologie (null wenn keine)
    /// </summary>
    public string? CurrentResearch => _currentResearch;

    public override void Initialize(GameContext context)
    {
        _context = context;
        LoadTechnologies();
        InitializePlayerProgress();
    }

    private void LoadTechnologies()
    {
        string basePath = CountryDataLoader.FindBasePath();
        string techPath = Path.Combine(basePath, "Data", "tech_tree.json");

        if (!File.Exists(techPath))
        {
            Console.WriteLine("[TechTreeManager] tech_tree.json nicht gefunden.");
            return;
        }

        try
        {
            string json = File.ReadAllText(techPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("technologies", out var techArray))
            {
                foreach (var techElement in techArray.EnumerateArray())
                {
                    var tech = new Technology
                    {
                        Id = techElement.GetProperty("id").GetString() ?? "",
                        Name = techElement.GetProperty("name").GetString() ?? "",
                        Description = techElement.GetProperty("description").GetString() ?? "",
                        Category = Enum.Parse<TechCategory>(techElement.GetProperty("category").GetString() ?? "Industry"),
                        Tier = techElement.GetProperty("tier").GetInt32(),
                        ResearchCost = techElement.GetProperty("researchCost").GetInt32(),
                        ResearchTime = techElement.GetProperty("researchTime").GetInt32(),
                        IconName = techElement.TryGetProperty("iconName", out var iconProp) ? iconProp.GetString() ?? "" : ""
                    };

                    // Voraussetzungen laden
                    if (techElement.TryGetProperty("prerequisites", out var prereqs))
                    {
                        foreach (var prereq in prereqs.EnumerateArray())
                        {
                            tech.Prerequisites.Add(prereq.GetString() ?? "");
                        }
                    }

                    // Effekte laden
                    if (techElement.TryGetProperty("effects", out var effects))
                    {
                        foreach (var effect in effects.EnumerateObject())
                        {
                            tech.Effects[effect.Name] = effect.Value.GetDouble();
                        }
                    }

                    _technologies[tech.Id] = tech;
                }
            }

            Console.WriteLine($"[TechTreeManager] {_technologies.Count} Technologien geladen.");
            CalculateTreePositions();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TechTreeManager] Fehler beim Laden: {ex.Message}");
        }
    }

    private void InitializePlayerProgress()
    {
        foreach (var tech in _technologies.Values)
        {
            var progress = new TechProgress
            {
                TechId = tech.Id,
                Status = tech.Prerequisites.Count == 0 ? TechStatus.Available : TechStatus.Locked,
                ProgressDays = 0
            };
            _playerProgress[tech.Id] = progress;
        }
    }

    /// <summary>
    /// Berechnet die Positionen der Technologien im Baum
    /// </summary>
    private void CalculateTreePositions()
    {
        // Gruppiere nach Kategorie
        var byCategory = _technologies.Values.GroupBy(t => t.Category);

        int categoryIndex = 0;
        foreach (var categoryGroup in byCategory.OrderBy(g => g.Key))
        {
            // Sortiere nach Tier
            var byTier = categoryGroup.OrderBy(t => t.Tier).ToList();

            int techIndexInCategory = 0;
            foreach (var tech in byTier)
            {
                tech.TreeX = tech.Tier - 1; // X = Tier (0-basiert)
                tech.TreeY = categoryIndex;  // Y = Kategorie-Index
                techIndexInCategory++;
            }
            categoryIndex++;
        }
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        if (tickType != TickType.Daily) return;

        // Forschungsfortschritt mit Tech-Bonus
        if (_currentResearch != null && _playerProgress.TryGetValue(_currentResearch, out var progress))
        {
            progress.ProgressDays++;

            if (_technologies.TryGetValue(_currentResearch, out var tech))
            {
                int effectiveTime = GetEffectiveResearchTime(tech);
                if (progress.ProgressDays >= effectiveTime)
                {
                    CompleteResearch(_currentResearch);
                }
            }
        }
    }

    /// <summary>
    /// Gibt den Status einer Technologie zurueck
    /// </summary>
    public TechStatus GetTechStatus(string techId)
    {
        if (_playerProgress.TryGetValue(techId, out var progress))
            return progress.Status;
        return TechStatus.Locked;
    }

    /// <summary>
    /// Prueft ob alle Voraussetzungen erfuellt sind
    /// </summary>
    public bool ArePrerequisitesMet(string techId)
    {
        if (!_technologies.TryGetValue(techId, out var tech))
            return false;

        foreach (var prereqId in tech.Prerequisites)
        {
            if (!_playerProgress.TryGetValue(prereqId, out var prereqProgress))
                return false;
            if (prereqProgress.Status != TechStatus.Completed)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Startet die Forschung einer Technologie (fuer spaeter)
    /// </summary>
    public bool StartResearch(string techId)
    {
        if (!CanResearch(techId))
            return false;

        _currentResearch = techId;
        if (_playerProgress.TryGetValue(techId, out var progress))
        {
            progress.Status = TechStatus.Researching;
        }
        return true;
    }

    /// <summary>
    /// Prueft ob eine Technologie erforscht werden kann
    /// </summary>
    public bool CanResearch(string techId)
    {
        if (_currentResearch != null) return false; // Bereits etwas am Forschen
        if (!ArePrerequisitesMet(techId)) return false;

        if (_playerProgress.TryGetValue(techId, out var progress))
        {
            return progress.Status == TechStatus.Available;
        }
        return false;
    }

    /// <summary>
    /// Schliesst eine Forschung ab
    /// </summary>
    private void CompleteResearch(string techId)
    {
        if (_playerProgress.TryGetValue(techId, out var progress))
        {
            progress.Status = TechStatus.Completed;
        }
        _currentResearch = null;

        // Aktualisiere Status abhaengiger Technologien
        UpdateDependentTechs();
    }

    /// <summary>
    /// Aktualisiert den Status von Technologien deren Voraussetzungen sich geaendert haben
    /// </summary>
    private void UpdateDependentTechs()
    {
        foreach (var tech in _technologies.Values)
        {
            if (_playerProgress.TryGetValue(tech.Id, out var progress))
            {
                if (progress.Status == TechStatus.Locked && ArePrerequisitesMet(tech.Id))
                {
                    progress.Status = TechStatus.Available;
                }
            }
        }
    }

    /// <summary>
    /// Gibt Technologien einer bestimmten Kategorie zurueck
    /// </summary>
    public IEnumerable<Technology> GetTechsByCategory(TechCategory category)
    {
        return _technologies.Values.Where(t => t.Category == category).OrderBy(t => t.Tier);
    }

    /// <summary>
    /// Gibt alle Kategorien mit ihren Technologien zurueck
    /// </summary>
    public Dictionary<TechCategory, List<Technology>> GetTechTree()
    {
        var tree = new Dictionary<TechCategory, List<Technology>>();
        foreach (TechCategory cat in Enum.GetValues<TechCategory>())
        {
            tree[cat] = GetTechsByCategory(cat).ToList();
        }
        return tree;
    }

    /// <summary>
    /// Gibt den Gesamtwert eines bestimmten Effekts aus abgeschlossenen Technologien zurueck.
    /// Summiert alle Effekte mit dem angegebenen Namen (z.B. "factory_output" = 0.05 + 0.10 + 0.15 = 0.30).
    /// </summary>
    public double GetEffect(string effectName)
    {
        double total = 0;
        foreach (var (techId, progress) in _playerProgress)
        {
            if (progress.Status != TechStatus.Completed)
                continue;
            if (!_technologies.TryGetValue(techId, out var tech))
                continue;
            if (tech.Effects.TryGetValue(effectName, out var value))
                total += value;
        }
        return total;
    }

    /// <summary>
    /// Gibt die effektive Forschungszeit nach Tech-Boni zurueck.
    /// research_speed reduziert die benoetigte Zeit.
    /// </summary>
    public int GetEffectiveResearchTime(Technology tech)
    {
        double researchBonus = GetEffect("research_speed");
        return Math.Max(1, (int)(tech.ResearchTime / (1.0 + researchBonus)));
    }

    /// <summary>
    /// Stellt Forschungsfortschritt aus einem Spielstand wieder her
    /// </summary>
    public void RestoreProgress(Dictionary<string, TechProgress> progress, string? currentResearch)
    {
        foreach (var (techId, saved) in progress)
        {
            if (_playerProgress.ContainsKey(techId))
            {
                _playerProgress[techId] = saved;
            }
        }
        _currentResearch = currentResearch;

        // Aktualisiere abhaengige Technologien
        UpdateDependentTechs();
    }

    public override void Shutdown()
    {
        _technologies.Clear();
        _playerProgress.Clear();
    }
}
