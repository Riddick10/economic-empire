namespace GrandStrategyGame.Models;

/// <summary>
/// Typen von Ressourcen
/// </summary>
public enum ResourceType
{
    // Rohstoffe
    Oil,            // Erdöl
    NaturalGas,     // Erdgas
    Coal,           // Kohle
    Iron,           // Eisenerz
    Copper,         // Kupfer
    Uranium,        // Uran

    // Agrar
    Food,           // Nahrungsmittel

    // Verarbeitete Güter
    Steel,          // Stahl
    Electronics,    // Elektronik
    Machinery,      // Maschinen
    ConsumerGoods,  // Konsumgüter

    // Militärgüter
    Weapons,        // Waffen
    Ammunition      // Munition
}

/// <summary>
/// Ressource mit Marktdaten
/// </summary>
public class Resource
{
    public ResourceType Type { get; set; }
    public string Name { get; set; }
    public double BasePrice { get; set; }
    public double CurrentPrice { get; set; }
    public double GlobalSupply { get; set; }
    public double GlobalDemand { get; set; }
    public double Volatility { get; set; }   // Preis-Volatilitaet (0.0 - 1.0)

    private static readonly Random _random = new();

    public Resource(ResourceType type, string name, double basePrice)
    {
        Type = type;
        Name = name;
        BasePrice = basePrice;
        CurrentPrice = basePrice;
        Volatility = GetBaseVolatility(type);
    }

    /// <summary>
    /// Gibt Basis-Volatilitaet zurueck (Rohstoffe schwanken mehr)
    /// </summary>
    private static double GetBaseVolatility(ResourceType type)
    {
        return type switch
        {
            ResourceType.Oil => 0.15,        // Oel ist sehr volatil
            ResourceType.NaturalGas => 0.12, // Gas auch volatil
            ResourceType.Uranium => 0.10,    // Uran schwankt
            ResourceType.Copper => 0.08,     // Kupfer moderat
            ResourceType.Iron => 0.06,       // Eisen stabiler
            ResourceType.Coal => 0.05,       // Kohle stabil
            ResourceType.Food => 0.04,       // Nahrung relativ stabil
            ResourceType.Steel => 0.03,      // Verarbeitete Gueter
            ResourceType.Electronics => 0.04,
            ResourceType.Machinery => 0.03,
            ResourceType.ConsumerGoods => 0.02, // Konsumgueter am stabilsten
            ResourceType.Weapons => 0.06,       // Waffen schwanken bei Konflikten
            ResourceType.Ammunition => 0.05,    // Munition aehnlich
            _ => 0.05
        };
    }

    /// <summary>
    /// Aktualisiert den Preis basierend auf Angebot/Nachfrage mit Marktdynamik
    /// </summary>
    public void UpdatePrice()
    {
        // Sicherheitscheck gegen Division durch 0
        if (GlobalSupply <= 0.0001)
        {
            // Kein Angebot = extreme Knappheit
            CurrentPrice = BasePrice * 8;
            return;
        }

        // Grundlegendes Angebot/Nachfrage-Verhaeltnis (mit Minimum-Schutz)
        double ratio = GlobalDemand / Math.Max(0.0001, GlobalSupply);

        // Zielpreis basierend auf Ratio
        double targetPrice = BasePrice * Math.Clamp(ratio, 0.3, 6.0);

        // Preis bewegt sich graduell zum Zielpreis (Markt-Traegheit)
        double priceChange = (targetPrice - CurrentPrice) * 0.3; // 30% Anpassung pro Woche

        // Zufaellige Marktschwankung
        double randomFactor = 1.0 + ((_random.NextDouble() - 0.5) * 2 * Volatility);

        // Neuer Preis mit Volatilitaet
        CurrentPrice = Math.Clamp(
            (CurrentPrice + priceChange) * randomFactor,
            BasePrice * 0.2,  // Mindestens 20% des Basispreises
            BasePrice * 10.0  // Maximal 1000% des Basispreises
        );
    }

    /// <summary>
    /// Gibt einen deutschen Namen zurück
    /// </summary>
    public static string GetGermanName(ResourceType type)
    {
        return type switch
        {
            ResourceType.Oil => "Erdöl",
            ResourceType.NaturalGas => "Erdgas",
            ResourceType.Coal => "Kohle",
            ResourceType.Iron => "Eisenerz",
            ResourceType.Copper => "Kupfer",
            ResourceType.Uranium => "Uran",
            ResourceType.Food => "Nahrung",
            ResourceType.Steel => "Stahl",
            ResourceType.Electronics => "Elektronik",
            ResourceType.Machinery => "Maschinen",
            ResourceType.ConsumerGoods => "Konsumgüter",
            ResourceType.Weapons => "Waffen",
            ResourceType.Ammunition => "Munition",
            _ => type.ToString()
        };
    }
}
