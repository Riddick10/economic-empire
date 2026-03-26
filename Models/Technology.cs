namespace GrandStrategyGame.Models;

/// <summary>
/// Kategorie einer Technologie
/// </summary>
public enum TechCategory
{
    Industry,       // Industrie & Produktion
    Infrastructure, // Infrastruktur & Logistik
    Electronics,    // Elektronik & Computer
    Energy,         // Energie & Ressourcen
    Military,       // Militaer & Verteidigung
    Society         // Gesellschaft & Bildung
}

/// <summary>
/// Status einer Technologie
/// </summary>
public enum TechStatus
{
    Locked,      // Voraussetzungen nicht erfuellt
    Available,   // Kann erforscht werden
    Researching, // Wird gerade erforscht
    Completed    // Bereits erforscht
}

/// <summary>
/// Repräsentiert eine einzelne Technologie im Forschungsbaum
/// </summary>
public class Technology
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public TechCategory Category { get; set; }
    public int Tier { get; set; } // 1-5, bestimmt Position im Baum
    public int ResearchCost { get; set; } // Forschungspunkte
    public int ResearchTime { get; set; } // Tage
    public List<string> Prerequisites { get; set; } = new(); // IDs der Voraussetzungen
    public string IconName { get; set; } = ""; // Icon-Dateiname

    // Effekte (fuer spaeter)
    public Dictionary<string, double> Effects { get; set; } = new();

    // Position im Baum (wird beim Laden berechnet)
    public int TreeX { get; set; }
    public int TreeY { get; set; }
}

/// <summary>
/// Spieler-spezifischer Forschungsfortschritt
/// </summary>
public class TechProgress
{
    public string TechId { get; set; } = "";
    public TechStatus Status { get; set; } = TechStatus.Locked;
    public int ProgressDays { get; set; } = 0; // Bereits investierte Tage
}
