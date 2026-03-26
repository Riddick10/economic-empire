using System.Text.Json;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Data;

/// <summary>
/// Lädt Länder- und Produktionsdaten aus JSON-Dateien
/// </summary>
public static class CountryDataLoader
{
    /// <summary>
    /// Lädt alle Länder aus der JSON-Datei
    /// </summary>
    public static Dictionary<string, Country> LoadCountries(string basePath)
    {
        var countries = new Dictionary<string, Country>();
        string filePath = Path.Combine(basePath, "Data", "countries.json");

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[CountryDataLoader] Datei nicht gefunden: {filePath}");
            return countries;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("countries", out var countriesArray))
            {
                foreach (var countryJson in countriesArray.EnumerateArray())
                {
                    var country = ParseCountry(countryJson);
                    if (country != null)
                    {
                        countries[country.Id] = country;
                    }
                }
            }

            Console.WriteLine($"[CountryDataLoader] {countries.Count} Länder geladen");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CountryDataLoader] Fehler: {ex.Message}");
        }

        return countries;
    }

    /// <summary>
    /// Lädt Produktionsdaten und wendet sie auf Länder an
    /// </summary>
    public static void LoadProduction(Dictionary<string, Country> countries, string basePath)
    {
        string filePath = Path.Combine(basePath, "Data", "production.json");

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[CountryDataLoader] Produktionsdaten nicht gefunden: {filePath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("production", out var production))
            {
                foreach (var resourceProp in production.EnumerateObject())
                {
                    if (!Enum.TryParse<ResourceType>(resourceProp.Name, out var resourceType))
                        continue;

                    foreach (var countryProp in resourceProp.Value.EnumerateObject())
                    {
                        string countryId = countryProp.Name;
                        if (!countries.TryGetValue(countryId, out var country))
                            continue;

                        var values = countryProp.Value;
                        if (values.GetArrayLength() >= 2)
                        {
                            // Produktion kommt nur aus Minen (ProductionManager)
                            // Hier nur Verbrauch laden
                            double cons = values[1].GetDouble();
                            country.DailyConsumption[resourceType] = cons;
                        }
                    }
                }
            }

            Console.WriteLine("[CountryDataLoader] Produktionsdaten geladen");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CountryDataLoader] Fehler bei Produktion: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialisiert Lagerbestände basierend auf BIP
    /// </summary>
    public static void InitializeStockpiles(Dictionary<string, Country> countries)
    {
        foreach (var country in countries.Values)
        {
            double factor = Math.Max(country.GDP / 5_000_000, 0.01);

            // Nahrung: 1 Jahr Vorrat basierend auf Bevoelkerung
            double dailyFood = country.Population * GameConfig.FOOD_PER_PERSON_PER_DAY;
            country.AddResource(ResourceType.Food, dailyFood * GameConfig.INITIAL_FOOD_DAYS);

            // Basis-Vorraete nach BIP
            double oil = 100 * factor;
            double gas = 60 * factor;
            double coal = 30 * factor;
            double iron = 20 * factor;
            double copper = 10 * factor;
            double uranium = 2 * factor;
            double steel = 50 * factor;
            double electronics = 40 * factor;
            double machinery = 35 * factor;
            double consumerGoods = 75 * factor;

            // Laenderspezifische Multiplikatoren (reale Ressourcen-Staerken)
            switch (country.Id)
            {
                // Oel-Produzenten
                case "SAU": oil *= 12; gas *= 4; break;              // Saudi-Arabien: groesster Exporteur
                case "RUS": oil *= 8; gas *= 10; coal *= 3; iron *= 2; uranium *= 3; break;
                case "USA": oil *= 4; gas *= 5; coal *= 3; break;
                case "CAN": oil *= 5; gas *= 3; uranium *= 8; break; // Oelsand + Uran
                case "IRQ": oil *= 8; gas *= 2; break;
                case "IRN": oil *= 6; gas *= 6; break;
                case "ARE": oil *= 8; gas *= 3; break;               // VAE
                case "KWT": oil *= 10; gas *= 2; break;              // Kuwait
                case "VEN": oil *= 6; break;
                case "NOR": oil *= 5; gas *= 4; break;
                case "NGA": oil *= 4; gas *= 2; break;               // Nigeria
                case "LBY": oil *= 5; break;

                // Gas-Produzenten
                case "QAT": gas *= 12; oil *= 4; break;
                case "TKM": gas *= 8; break;                          // Turkmenistan

                // Kohle/Eisen/Kupfer
                case "AUS": coal *= 6; iron *= 8; copper *= 3; uranium *= 6; break;
                case "BRA": iron *= 8; copper *= 2; break;
                case "IND": coal *= 5; iron *= 3; break;
                case "IDN": coal *= 6; copper *= 2; break;            // Indonesien
                case "ZAF": coal *= 4; iron *= 2; copper *= 2; break; // Suedafrika
                case "CHL": copper *= 12; break;                      // Chile: groesster Kupferproduzent
                case "PER": copper *= 8; break;                       // Peru
                case "COD": copper *= 6; uranium *= 3; break;         // Kongo

                // Uran-Produzenten
                case "KAZ": uranium *= 15; coal *= 2; break;          // Kasachstan: Nr.1 Uran
                case "NER": uranium *= 5; break;                       // Niger
                case "UZB": uranium *= 4; gas *= 3; break;

                // Industrie-Nationen (Elektronik, Maschinen, Stahl)
                case "CHN": steel *= 5; electronics *= 4; machinery *= 4; coal *= 4; iron *= 3; break;
                case "DEU": machinery *= 5; steel *= 2; electronics *= 2; break;
                case "JPN": electronics *= 4; machinery *= 3; steel *= 3; break;
                case "KOR": electronics *= 5; steel *= 3; machinery *= 2; break;
                case "TWN": electronics *= 8; break;                   // Taiwan: Halbleiter
                case "GBR": machinery *= 2; electronics *= 2; break;
                case "FRA": machinery *= 2; electronics *= 2; uranium *= 3; break;
                case "ITA": machinery *= 2; steel *= 2; break;
            }

            // Militaergueter: Waffen und Munition basierend auf BIP + Militaerausgaben
            double militaryFactor = factor * (country.MilitarySpendingPercent / 0.15); // 15% ist Durchschnitt
            double weapons = 200 * militaryFactor;
            double ammunition = 500 * militaryFactor;

            // Grosse Militaermaechte bekommen mehr
            switch (country.Id)
            {
                case "USA": weapons *= 8; ammunition *= 8; break;
                case "RUS": weapons *= 6; ammunition *= 6; break;
                case "CHN": weapons *= 5; ammunition *= 5; break;
                case "IND": weapons *= 3; ammunition *= 3; break;
                case "GBR": weapons *= 3; ammunition *= 3; break;
                case "FRA": weapons *= 3; ammunition *= 3; break;
                case "DEU": weapons *= 2; ammunition *= 2; break;
                case "JPN": weapons *= 2; ammunition *= 2; break;
                case "KOR": weapons *= 2; ammunition *= 2; break;
                case "ISR": weapons *= 4; ammunition *= 4; break;
                case "TUR": weapons *= 2; ammunition *= 2; break;
                case "SAU": weapons *= 3; ammunition *= 3; break;
                case "PAK": weapons *= 2; ammunition *= 2; break;
                case "BRA": weapons *= 2; ammunition *= 2; break;
            }

            country.AddResource(ResourceType.Oil, oil);
            country.AddResource(ResourceType.NaturalGas, gas);
            country.AddResource(ResourceType.Coal, coal);
            country.AddResource(ResourceType.Iron, iron);
            country.AddResource(ResourceType.Copper, copper);
            country.AddResource(ResourceType.Uranium, uranium);
            country.AddResource(ResourceType.Steel, steel);
            country.AddResource(ResourceType.Electronics, electronics);
            country.AddResource(ResourceType.Machinery, machinery);
            country.AddResource(ResourceType.ConsumerGoods, consumerGoods);
            country.AddResource(ResourceType.Weapons, weapons);
            country.AddResource(ResourceType.Ammunition, ammunition);
        }
    }

    private static Country? ParseCountry(JsonElement json)
    {
        try
        {
            string id = json.GetProperty("id").GetString() ?? "";
            string name = json.GetProperty("name").GetString() ?? "";

            if (string.IsNullOrEmpty(id)) return null;

            var country = new Country(id, name)
            {
                FullName = json.GetProperty("fullName").GetString() ?? name,
                Population = json.GetProperty("population").GetInt64(),
                GDP = json.GetProperty("gdp").GetDouble(),
                PopulationGrowthRate = json.GetProperty("popGrowth").GetDouble(),
                GDPGrowthRate = json.GetProperty("gdpGrowth").GetDouble(),
                UnemploymentRate = json.GetProperty("unemployment").GetDouble(),
                TaxRate = json.GetProperty("taxRate").GetDouble()
            };

            // Realistische Startbudgets und Staatsschulden (Stand 2024, in Millionen USD)
            // Budget = verfuegbare Staatskasse, basierend auf Wirtschaftskraft und Reserven
            // Formel fuer nicht explizit gesetzte Laender: BIP * 0.5% (konservativ)
            country.Budget = country.GDP * 0.005;

            switch (country.Id)
            {
                // === Nordamerika ===
                case CountryIds.USA:
                    country.Budget = 500_000;           // $500 Mrd. Startkapital
                    country.NationalDebt = 34_000_000;  // ~$34 Bio. (hoechste der Welt)
                    country.MilitarySpendingPercent = 0.25;
                    country.SocialSpendingPercent = 0.30;
                    country.DeficitMultiplier = 1.15;   // ~15% Defizit (chronisch hohes US-Defizit)
                    break;
                case "CAN": // Kanada
                    country.Budget = 80_000;
                    country.NationalDebt = 1_200_000;
                    break;
                case "MEX": // Mexiko
                    country.Budget = 40_000;
                    country.NationalDebt = 700_000;
                    break;

                // === Suedamerika ===
                case "BRA": // Brasilien
                    country.Budget = 60_000;
                    country.NationalDebt = 1_600_000;
                    country.DeficitMultiplier = 1.08;   // Moderates Defizit
                    break;
                case "ARG": // Argentinien
                    country.Budget = 15_000;
                    country.NationalDebt = 400_000;
                    break;
                case "COL": // Kolumbien
                    country.Budget = 20_000;
                    country.NationalDebt = 180_000;
                    break;
                case "CHL": // Chile
                    country.Budget = 25_000;
                    country.NationalDebt = 120_000;
                    break;
                case "VEN": // Venezuela
                    country.Budget = 5_000;
                    country.NationalDebt = 150_000;
                    break;
                case "PER": // Peru
                    country.Budget = 15_000;
                    country.NationalDebt = 80_000;
                    break;

                // === Westeuropa ===
                case CountryIds.Germany:
                    country.Budget = 200_000;           // $200 Mrd.
                    country.NationalDebt = 2_690_000;   // ~2,69 Bio. USD
                    country.DeficitMultiplier = 1.03;   // Schuldenbremse, niedrig
                    break;
                case "GBR": // Grossbritannien
                    country.Budget = 150_000;
                    country.NationalDebt = 3_200_000;
                    country.DeficitMultiplier = 1.08;   // Moderates Defizit
                    break;
                case "FRA": // Frankreich
                    country.Budget = 120_000;
                    country.NationalDebt = 3_100_000;
                    country.DeficitMultiplier = 1.10;   // Frankreich hat traditionell hohes Defizit
                    break;
                case "ITA": // Italien
                    country.Budget = 80_000;
                    country.NationalDebt = 2_900_000;
                    country.DeficitMultiplier = 1.08;   // Moderates Defizit
                    break;
                case "ESP": // Spanien
                    country.Budget = 60_000;
                    country.NationalDebt = 1_600_000;
                    country.DeficitMultiplier = 1.07;   // Moderates Defizit
                    break;
                case "NLD": // Niederlande
                    country.Budget = 50_000;
                    country.NationalDebt = 500_000;
                    break;
                case "BEL": // Belgien
                    country.Budget = 30_000;
                    country.NationalDebt = 600_000;
                    break;
                case "CHE": // Schweiz
                    country.Budget = 80_000;
                    country.NationalDebt = 200_000;     // Sehr niedrige Schulden
                    country.DeficitMultiplier = 1.01;   // Fast ausgeglichen (Schuldenbremse)
                    break;
                case "AUT": // Oesterreich
                    country.Budget = 35_000;
                    country.NationalDebt = 350_000;
                    break;
                case "PRT": // Portugal
                    country.Budget = 20_000;
                    country.NationalDebt = 300_000;
                    break;
                case "IRL": // Irland
                    country.Budget = 25_000;
                    country.NationalDebt = 250_000;
                    break;
                case "LUX": // Luxemburg
                    country.Budget = 15_000;
                    country.NationalDebt = 20_000;
                    break;

                // === Nordeuropa ===
                case "NOR": // Norwegen
                    country.Budget = 100_000;           // Oelfonds!
                    country.NationalDebt = 180_000;
                    country.DeficitMultiplier = 0.97;   // Ueberschuss dank Oelfonds
                    break;
                case "SWE": // Schweden
                    country.Budget = 40_000;
                    country.NationalDebt = 200_000;
                    break;
                case "FIN": // Finnland
                    country.Budget = 25_000;
                    country.NationalDebt = 180_000;
                    break;
                case "DNK": // Daenemark
                    country.Budget = 30_000;
                    country.NationalDebt = 120_000;
                    break;
                case "ISL": // Island
                    country.Budget = 5_000;
                    country.NationalDebt = 15_000;
                    break;

                // === Osteuropa ===
                case "POL": // Polen
                    country.Budget = 40_000;
                    country.NationalDebt = 350_000;
                    break;
                case "CZE": // Tschechien
                    country.Budget = 25_000;
                    country.NationalDebt = 120_000;
                    break;
                case "UKR": // Ukraine
                    country.Budget = 10_000;
                    country.NationalDebt = 130_000;
                    country.DeficitMultiplier = 1.25;   // Kriegswirtschaft, massives Defizit
                    break;
                case "ROU": // Rumaenien
                    country.Budget = 20_000;
                    country.NationalDebt = 150_000;
                    break;
                case "HUN": // Ungarn
                    country.Budget = 15_000;
                    country.NationalDebt = 120_000;
                    break;
                case "BLR": // Belarus
                    country.Budget = 8_000;
                    country.NationalDebt = 20_000;
                    break;
                case "BGR": // Bulgarien
                    country.Budget = 10_000;
                    country.NationalDebt = 40_000;
                    break;
                case "SRB": // Serbien
                    country.Budget = 8_000;
                    country.NationalDebt = 35_000;
                    break;
                case "HRV": // Kroatien
                    country.Budget = 8_000;
                    country.NationalDebt = 45_000;
                    break;
                case "SVK": // Slowakei
                    country.Budget = 10_000;
                    country.NationalDebt = 65_000;
                    break;
                case "SVN": // Slowenien
                    country.Budget = 8_000;
                    country.NationalDebt = 45_000;
                    break;
                case "GRC": // Griechenland
                    country.Budget = 15_000;
                    country.NationalDebt = 400_000;
                    country.DeficitMultiplier = 1.04;   // Nach Schuldenkrise konsolidiert
                    break;

                // === Russland und Zentralasien ===
                case CountryIds.Russia:
                    country.Budget = 150_000;           // Grosse Reserven
                    country.NationalDebt = 300_000;     // Relativ niedrig
                    country.MilitarySpendingPercent = 0.30;
                    country.SocialSpendingPercent = 0.25;
                    country.DeficitMultiplier = 1.04;   // Eher konservativ
                    break;
                case "KAZ": // Kasachstan
                    country.Budget = 30_000;
                    country.NationalDebt = 50_000;
                    break;
                case "UZB": // Usbekistan
                    country.Budget = 10_000;
                    country.NationalDebt = 25_000;
                    break;

                // === Asien ===
                case "CHN": // China
                    country.Budget = 600_000;           // Groesste Devisenreserven
                    country.NationalDebt = 14_000_000;
                    country.DeficitMultiplier = 1.12;   // Expansive Fiskalpolitik
                    break;
                case "JPN": // Japan
                    country.Budget = 150_000;
                    country.NationalDebt = 12_500_000;  // Hoechste Schuldenquote
                    country.DeficitMultiplier = 1.10;   // Chronisches Defizit
                    break;
                case "KOR": // Suedkorea
                    country.Budget = 80_000;
                    country.NationalDebt = 700_000;
                    break;
                case "PRK": // Nordkorea
                    country.Budget = 5_000;
                    country.NationalDebt = 20_000;
                    country.MilitarySpendingPercent = 0.40;
                    country.DeficitMultiplier = 1.20;   // Massive Militaerausgaben, hohes Defizit
                    break;
                case "IND": // Indien
                    country.Budget = 100_000;
                    country.NationalDebt = 2_500_000;
                    country.DeficitMultiplier = 1.09;   // Entwicklungsland, moderates Defizit
                    break;
                case "IDN": // Indonesien
                    country.Budget = 40_000;
                    country.NationalDebt = 400_000;
                    break;
                case "THA": // Thailand
                    country.Budget = 35_000;
                    country.NationalDebt = 300_000;
                    break;
                case "VNM": // Vietnam
                    country.Budget = 25_000;
                    country.NationalDebt = 150_000;
                    break;
                case "MYS": // Malaysia
                    country.Budget = 30_000;
                    country.NationalDebt = 250_000;
                    break;
                case "SGP": // Singapur
                    country.Budget = 60_000;
                    country.NationalDebt = 500_000;
                    break;
                case "PHL": // Philippinen
                    country.Budget = 20_000;
                    country.NationalDebt = 200_000;
                    break;
                case "PAK": // Pakistan
                    country.Budget = 15_000;
                    country.NationalDebt = 250_000;
                    break;
                case "BGD": // Bangladesch
                    country.Budget = 15_000;
                    country.NationalDebt = 100_000;
                    break;
                case "TWN": // Taiwan
                    country.Budget = 60_000;
                    country.NationalDebt = 200_000;
                    break;

                // === Naher Osten ===
                case "SAU": // Saudi-Arabien
                    country.Budget = 200_000;           // Oel-Reichtum
                    country.NationalDebt = 250_000;
                    country.DeficitMultiplier = 1.02;   // Oelueberschuesse halten Defizit niedrig
                    break;
                case "ARE": // VAE
                    country.Budget = 100_000;
                    country.NationalDebt = 150_000;
                    country.DeficitMultiplier = 1.01;   // Meist Ueberschuss
                    break;
                case "ISR": // Israel
                    country.Budget = 50_000;
                    country.NationalDebt = 300_000;
                    break;
                case "IRN": // Iran
                    country.Budget = 30_000;
                    country.NationalDebt = 50_000;
                    break;
                case "IRQ": // Irak
                    country.Budget = 25_000;
                    country.NationalDebt = 130_000;
                    break;
                case "TUR": // Tuerkei
                    country.Budget = 40_000;
                    country.NationalDebt = 500_000;
                    country.DeficitMultiplier = 1.10;   // Expansive Politik unter Erdogan
                    break;
                case "EGY": // Aegypten
                    country.Budget = 20_000;
                    country.NationalDebt = 350_000;
                    country.DeficitMultiplier = 1.12;   // Hohes Defizit
                    break;
                case "QAT": // Katar
                    country.Budget = 80_000;
                    country.NationalDebt = 100_000;
                    break;
                case "KWT": // Kuwait
                    country.Budget = 60_000;
                    country.NationalDebt = 50_000;
                    break;

                // === Afrika ===
                case "ZAF": // Suedafrika
                    country.Budget = 30_000;
                    country.NationalDebt = 250_000;
                    break;
                case "NGA": // Nigeria
                    country.Budget = 15_000;
                    country.NationalDebt = 100_000;
                    break;
                case "DZA": // Algerien
                    country.Budget = 20_000;
                    country.NationalDebt = 60_000;
                    break;
                case "MAR": // Marokko
                    country.Budget = 15_000;
                    country.NationalDebt = 100_000;
                    break;
                case "ETH": // Aethiopien
                    country.Budget = 8_000;
                    country.NationalDebt = 30_000;
                    break;
                case "KEN": // Kenia
                    country.Budget = 10_000;
                    country.NationalDebt = 70_000;
                    break;

                // === Ozeanien ===
                case "AUS": // Australien
                    country.Budget = 80_000;
                    country.NationalDebt = 600_000;
                    break;
                case "NZL": // Neuseeland
                    country.Budget = 20_000;
                    country.NationalDebt = 80_000;
                    break;
            }

            return country;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Findet den Basis-Pfad für Datendateien
    /// </summary>
    public static string FindBasePath()
    {
        string[] searchPaths =
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")
        };

        foreach (var basePath in searchPaths)
        {
            string dataPath = Path.Combine(basePath, "Data", "countries.json");
            if (File.Exists(dataPath))
            {
                return basePath;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
