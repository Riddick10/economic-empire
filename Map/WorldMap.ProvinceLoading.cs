using System.Numerics;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Provinz- und Fluss-Laden
/// </summary>
public partial class WorldMap
{
    // === Konfiguration: Alle Laender ohne Fabrikverteilung ===
    private static readonly (string File, string Prefix, double Tolerance)[] ProvinceConfigs =
    {
        // Europa
        ("poland_voivodeships.geojson", "POL", 0.02),
        ("ukraine_oblasts.geojson", "UKR", 0.02),
        ("italy_regions.geojson", "ITA", 0.015),
        ("spain_communities.geojson", "ESP", 0.02),
        ("norway_counties.geojson", "NOR", 0.02),
        ("sweden_counties.geojson", "SWE", 0.02),
        ("canada_provinces.geojson", "CAN", 0.02),
        ("mexico_states.geojson", "MEX", 0.02),
        ("belarus_oblasts.geojson", "BLR", 0.02),
        ("kazakhstan_oblasts.geojson", "KAZ", 0.03),
        ("swiss_grossregionen.geojson", "CHE", 0.01),
        ("austria_bundeslaender.geojson", "AUT", 0.01),
        ("czech_regions.geojson", "CZE", 0.01),
        ("finland_regions.geojson", "FIN", 0.02),
        ("greenland_adm1_raw.geojson", "GRL", 0.05),
        ("chile_regions.geojson", "CHL", 0.02),
        ("peru_departments.geojson", "PER", 0.01),
        ("argentina_provinces.geojson", "ARG", 0.04),
        ("iran_provinces.geojson", "IRN", 0.03),
        ("saudi_provinces.geojson", "SAU", 0.04),
        ("egypt_governorates.geojson", "EGY", 0.02),
        ("hungary_regions.geojson", "HUN", 0.02),
        ("romania_regions.geojson", "ROU", 0.02),
        ("bulgaria_regions.geojson", "BGR", 0.02),
        ("greece_regions.geojson", "GRC", 0.02),
        ("portugal_regions.geojson", "PRT", 0.02),
        ("netherlands_regions.geojson", "NLD", 0.02),
        ("belgium_provinces.geojson", "BEL", 0.005),
        ("luxembourg_single.geojson", "LUX", 0.02),
        ("ireland_provinces.geojson", "IRL", 0.02),
        ("denmark_regions.geojson", "DNK", 0.02),
        ("iceland_regions.geojson", "ISL", 0.02),
        ("serbia_regions.geojson", "SRB", 0.02),
        ("croatia_regions.geojson", "HRV", 0.02),
        ("slovakia_regions.geojson", "SVK", 0.02),
        ("slovenia_regions.geojson", "SVN", 0.02),
        ("bosnia_entities.geojson", "BIH", 0.02),
        ("montenegro_single.geojson", "MNE", 0.02),
        ("lithuania_regions.geojson", "LTU", 0.02),
        ("latvia_stat_regions.geojson", "LVA", 0.02),
        ("estonia_regions.geojson", "EST", 0.02),
        ("moldova_regions.geojson", "MDA", 0.02),
        ("albania_single.geojson", "ALB", 0.02),
        ("macedonia_single.geojson", "MKD", 0.02),
        ("georgia_single.geojson", "GEO", 0.02),
        ("azerbaijan_single.geojson", "AZE", 0.02),
        ("armenia_single.geojson", "ARM", 0.02),
        // Suedamerika
        ("colombia_departments.geojson", "COL", 0.02),
        ("venezuela_regions.geojson", "VEN", 0.02),
        ("ecuador_provinces.geojson", "ECU", 0.02),
        ("bolivia_departments.geojson", "BOL", 0.02),
        ("paraguay_departments.geojson", "PRY", 0.02),
        ("uruguay_departments.geojson", "URY", 0.02),
        ("guyana_single.geojson", "GUY", 0.02),
        // Afrika
        ("geoBoundaries-LBY-ADM1.geojson", "LBY", 0.01),
        ("geoBoundaries-TUN-ADM1.geojson", "TUN", 0.01),
        ("geoBoundaries-DZA-ADM1.geojson", "DZA", 0.02),
        ("geoBoundaries-MAR-ADM1.geojson", "MAR", 0.01),
        ("westsahara_provinces.geojson", "ESH", 0.01),
        ("geoBoundaries-SDN-ADM1.geojson", "SDN", 0.02),
        ("geoBoundaries-SSD-ADM1.geojson", "SSD", 0.01),
        ("geoBoundaries-NGA-ADM1.geojson", "NGA", 0.02),
        ("geoBoundaries-GHA-ADM1.geojson", "GHA", 0.01),
        ("geoBoundaries-CIV-ADM1.geojson", "CIV", 0.01),
        ("geoBoundaries-SEN-ADM1.geojson", "SEN", 0.01),
        ("geoBoundaries-MLI-ADM1.geojson", "MLI", 0.02),
        ("geoBoundaries-BFA-ADM1.geojson", "BFA", 0.01),
        ("geoBoundaries-NER-ADM1.geojson", "NER", 0.02),
        ("geoBoundaries-GIN-ADM1.geojson", "GIN", 0.01),
        ("geoBoundaries-BEN-ADM1.geojson", "BEN", 0.005),
        ("geoBoundaries-TGO-ADM1.geojson", "TGO", 0.005),
        ("geoBoundaries-SLE-ADM1.geojson", "SLE", 0.005),
        ("geoBoundaries-LBR-ADM1.geojson", "LBR", 0.005),
        ("geoBoundaries-MRT-ADM1.geojson", "MRT", 0.02),
        ("gambia_single.geojson", "GMB", 0.002),
        ("geoBoundaries-COD-ADM1.geojson", "COD", 0.03),
        ("geoBoundaries-COG-ADM1.geojson", "COG", 0.01),
        ("geoBoundaries-CMR-ADM1.geojson", "CMR", 0.01),
        ("geoBoundaries-TCD-ADM1.geojson", "TCD", 0.02),
        ("geoBoundaries-CAF-ADM1.geojson", "CAF", 0.01),
        ("geoBoundaries-GAB-ADM1.geojson", "GAB", 0.005),
        ("eqguinea_single.geojson", "GNQ", 0.002),
        ("geoBoundaries-ETH-ADM1.geojson", "ETH", 0.002),
        ("geoBoundaries-KEN-ADM1.geojson", "KEN", 0.01),
        ("geoBoundaries-TZA-ADM1.geojson", "TZA", 0.02),
        ("geoBoundaries-UGA-ADM1.geojson", "UGA", 0.01),
        ("rwanda_single.geojson", "RWA", 0.002),
        ("burundi_single.geojson", "BDI", 0.002),
        ("geoBoundaries-SOM-ADM1.geojson", "SOM", 0.02),
        ("geoBoundaries-ERI-ADM1.geojson", "ERI", 0.005),
        ("djibouti_single.geojson", "DJI", 0.002),
        ("geoBoundaries-MWI-ADM1.geojson", "MWI", 0.005),
        ("geoBoundaries-ZMB-ADM1.geojson", "ZMB", 0.02),
        ("geoBoundaries-ZWE-ADM1.geojson", "ZWE", 0.02),
        ("geoBoundaries-MOZ-ADM1.geojson", "MOZ", 0.02),
        ("geoBoundaries-MDG-ADM1.geojson", "MDG", 0.02),
        ("geoBoundaries-ZAF-ADM1.geojson", "ZAF", 0.03),
        ("geoBoundaries-NAM-ADM1.geojson", "NAM", 0.02),
        ("geoBoundaries-BWA-ADM1.geojson", "BWA", 0.02),
        ("geoBoundaries-AGO-ADM1.geojson", "AGO", 0.02),
        ("eswatini_single.geojson", "SWZ", 0.002),
        ("lesotho_single.geojson", "LSO", 0.005),
        // Asien
        ("geoBoundaries-IRQ-ADM1.geojson", "IRQ", 0.01),
        ("geoBoundaries-SYR-ADM1.geojson", "SYR", 0.01),
        ("geoBoundaries-JOR-ADM1.geojson", "JOR", 0.01),
        ("geoBoundaries-LBN-ADM1.geojson", "LBN", 0.005),
        ("geoBoundaries-YEM-ADM1.geojson", "YEM", 0.01),
        ("geoBoundaries-OMN-ADM1.geojson", "OMN", 0.01),
        ("geoBoundaries-ARE-ADM1.geojson", "ARE", 0.01),
        ("geoBoundaries-KWT-ADM1.geojson", "KWT", 0.01),
        ("qatar_single.geojson", "QAT", 0.005),
        ("bahrain_single.geojson", "BHR", 0.001),
        ("geoBoundaries-AFG-ADM1.geojson", "AFG", 0.01),
        ("geoBoundaries-PAK-ADM1.geojson", "PAK", 0.01),
        ("geoBoundaries-ISR-ADM1.geojson", "ISR", 0.01),
        ("geoBoundaries-KOR-ADM1.geojson", "KOR", 0.01),
        ("geoBoundaries-UZB-ADM1.geojson", "UZB", 0.01),
        ("geoBoundaries-TKM-ADM1.geojson", "TKM", 0.01),
        ("geoBoundaries-TJK-ADM1.geojson", "TJK", 0.005),
        ("geoBoundaries-KGZ-ADM1.geojson", "KGZ", 0.005),
        ("geoBoundaries-MNG-ADM1.geojson", "MNG", 0.03),
        ("geoBoundaries-BGD-ADM1.geojson", "BGD", 0.01),
        ("geoBoundaries-LKA-ADM1.geojson", "LKA", 0.01),
        ("geoBoundaries-NPL-ADM1.geojson", "NPL", 0.01),
        ("geoBoundaries-MMR-ADM1.geojson", "MMR", 0.005),
        ("geoBoundaries-THA-ADM1.geojson", "THA", 0.01),
        ("geoBoundaries-VNM-ADM1.geojson", "VNM", 0.01),
        ("geoBoundaries-KHM-ADM1.geojson", "KHM", 0.01),
        ("geoBoundaries-LAO-ADM1.geojson", "LAO", 0.005),
        ("geoBoundaries-MYS-ADM1.geojson", "MYS", 0.01),
        ("geoBoundaries-IDN-ADM1.geojson", "IDN", 0.01),
        ("geoBoundaries-PHL-ADM1.geojson", "PHL", 0.01),
        ("singapore_single.geojson", "SGP", 0.001),
        ("brunei_single.geojson", "BRN", 0.005),
        ("easttimor_single.geojson", "TLS", 0.005),
        ("geoBoundaries-PRK-ADM1.geojson", "PRK", 0.01),
        ("geoBoundaries-TWN-ADM1.geojson", "TWN", 0.005),
        // Mittelamerika / Karibik
        ("panama_single.geojson", "PAN", 0.01),
        ("costarica_single.geojson", "CRI", 0.01),
        ("nicaragua_single.geojson", "NIC", 0.01),
        ("honduras_single.geojson", "HND", 0.01),
        ("guatemala_single.geojson", "GTM", 0.01),
        ("haiti_single.geojson", "HTI", 0.01),
        ("dominican_single.geojson", "DOM", 0.01),
        ("geoBoundaries-CUB-ADM1.geojson", "CUB", 0.01),
    };

    // === Fabrikverteilungen fuer Industrielaender ===

    private static readonly Dictionary<string, (int Civil, int Military)> GermanFactories = new()
    {
        { "DE-NW", (100, 30) },  // Nordrhein-Westfalen
        { "DE-BY", (80, 30) },   // Bayern
        { "DE-BW", (80, 20) },   // Baden-Wuerttemberg
        { "DE-NI", (60, 20) },   // Niedersachsen
        { "DE-HE", (50, 10) },   // Hessen
        { "DE-RP", (30, 10) },   // Rheinland-Pfalz
        { "DE-SN", (30, 10) },   // Sachsen
        { "DE-BE", (30, 0) },    // Berlin
        { "DE-SH", (20, 10) },   // Schleswig-Holstein
        { "DE-BB", (20, 10) },   // Brandenburg
        { "DE-ST", (20, 0) },    // Sachsen-Anhalt
        { "DE-TH", (20, 0) },    // Thueringen
        { "DE-MV", (10, 10) },   // Mecklenburg-Vorpommern
        { "DE-HH", (10, 0) },    // Hamburg
        { "DE-HB", (0, 0) },     // Bremen
        { "DE-SL", (0, 0) },     // Saarland
    };

    // Startminen fuer Deutschland (basierend auf realen Vorkommen)
    // Regel: Max 1 Mine pro Typ pro Provinz, nur wo Rohstoff auf Ressourcenmap existiert
    private static readonly Dictionary<string, MineType[]> GermanMines = new()
    {
        { "DE-NW", new[] { MineType.CoalMine } },                          // NRW: Ruhr/Rheinisches Revier
        { "DE-NI", new[] { MineType.GasDrill, MineType.IronMine } },       // Niedersachsen: Norddeutsches Becken, Salzgitter
        { "DE-BB", new[] { MineType.CoalMine } },                          // Brandenburg: Lausitz
        { "DE-SN", new[] { MineType.CoalMine, MineType.UraniumMine } },    // Sachsen: Lausitz, Erzgebirge (Wismut)
        { "DE-ST", new[] { MineType.CopperMine, MineType.CoalMine } },     // Sachsen-Anhalt: Mansfeld, Profen
        { "DE-TH", new[] { MineType.UraniumMine } },                       // Thueringen: Ronneburg (Wismut)
    };

    // Startminen fuer USA (basierend auf realen Vorkommen)
    // Regel: Max 1 Mine pro Typ pro Provinz, nur wo Rohstoff auf Ressourcenmap existiert
    private static readonly Dictionary<string, MineType[]> USAMines = new()
    {
        { "USA-43", new[] { MineType.OilWell, MineType.GasDrill, MineType.UraniumMine } }, // Texas: Permian Basin, Haynesville, Alta Mesa
        { "USA-01", new[] { MineType.OilWell } },                                          // Alaska: North Slope / Prudhoe Bay
        { "USA-34", new[] { MineType.OilWell } },                                          // North Dakota: Bakken Formation
        { "USA-31", new[] { MineType.OilWell, MineType.UraniumMine } },                    // New Mexico: Permian Basin, Grants Mineral Belt
        { "USA-36", new[] { MineType.OilWell, MineType.GasDrill } },                       // Oklahoma: SCOOP/STACK, Anadarko Basin
        { "USA-38", new[] { MineType.GasDrill, MineType.CoalMine } },                      // Pennsylvania: Marcellus Shale, Anthrazit
        { "USA-48", new[] { MineType.GasDrill, MineType.CoalMine } },                      // West Virginia: Marcellus, Appalachian Coalfields
        { "USA-18", new[] { MineType.OilWell, MineType.GasDrill } },                       // Louisiana: Gulf Coast, Haynesville Shale
        { "USA-50", new[] { MineType.OilWell, MineType.CoalMine, MineType.UraniumMine } }, // Wyoming: Powder River Basin, Smith Ranch
        { "USA-13", new[] { MineType.CoalMine } },                                         // Illinois: Illinois Basin
        { "USA-26", new[] { MineType.CoalMine } },                                         // Montana: Rosebud Mine
        { "USA-23", new[] { MineType.IronMine } },                                         // Minnesota: Mesabi Range (70% US-Eisen)
        { "USA-22", new[] { MineType.IronMine } },                                         // Michigan: Upper Peninsula (25% US-Eisen)
        { "USA-02", new[] { MineType.CopperMine } },                                       // Arizona: Morenci Mine (70% US-Kupfer)
        { "USA-44", new[] { MineType.CopperMine } },                                       // Utah: Bingham Canyon Mine
    };

    // Startminen fuer Russland (basierend auf realen Vorkommen)
    // Regel: Max 1 Mine pro Typ pro Provinz, nur wo Rohstoff auf Ressourcenmap existiert
    private static readonly Dictionary<string, MineType[]> RussianMines = new()
    {
        { "RUS-05", new[] { MineType.OilWell } },                                          // Khanty-Mansiysk: Samotlor, 40-45% russ. Oel
        { "RUS-68", new[] { MineType.GasDrill } },                                         // Yamalo-Nenets: Urengoy/Yamburg, 80% russ. Gas
        { "RUS-22", new[] { MineType.OilWell } },                                          // Tatarstan: Romashkino-Feld, Wolga-Ural
        { "RUS-82", new[] { MineType.OilWell } },                                          // Bashkortostan: Wolga-Ural-Basin
        { "RUS-80", new[] { MineType.OilWell, MineType.GasDrill, MineType.CopperMine } },  // Orenburg: Orenburg-Gasfeld, Gaisky GOK
        { "RUS-52", new[] { MineType.OilWell } },                                          // Sakhalin: Sakhalin-1/2 Offshore
        { "RUS-27", new[] { MineType.GasDrill } },                                         // Astrachan: Gas-Kondensat-Feld
        { "RUS-75", new[] { MineType.CoalMine } },                                         // Kemerovo: Kuzbass, 55-60% russ. Kohle
        { "RUS-09", new[] { MineType.CoalMine } },                                         // Krasnoyarsk: Kansk-Atschinsk-Becken
        { "RUS-53", new[] { MineType.CoalMine } },                                         // Sakha/Jakutien: Suedjakutisches Becken
        { "RUS-28", new[] { MineType.IronMine } },                                         // Belgorod: KMA, weltgroesste Eisenerz-Reserve
        { "RUS-40", new[] { MineType.IronMine } },                                         // Kursk: KMA, Michailowski GOK
        { "RUS-11", new[] { MineType.IronMine, MineType.CopperMine } },                    // Sverdlovsk: Kachkanar + UMMC
        { "RUS-32", new[] { MineType.IronMine, MineType.CopperMine } },                    // Chelyabinsk: Suedural-Bergbau
        { "RUS-03", new[] { MineType.UraniumMine } },                                      // Kurgan: Dalur ISL-Mine
    };

    private static readonly Dictionary<string, (int Civil, int Military)> RussianFactories = new()
    {
        { "RUS-43", (100, 30) },  // Moscow
        { "RUS-44", (80, 40) },   // Moscow Oblast
        { "RUS-51", (80, 30) },   // Saint Petersburg
        { "RUS-41", (40, 20) },   // Leningrad Oblast
        { "RUS-11", (70, 40) },   // Sverdlovsk Oblast
        { "RUS-32", (60, 40) },   // Chelyabinsk Oblast
        { "RUS-82", (50, 20) },   // Bashkortostan
        { "RUS-14", (40, 20) },   // Perm Krai
        { "RUS-80", (30, 10) },   // Orenburg Oblast
        { "RUS-22", (60, 30) },   // Tatarstan
        { "RUS-19", (50, 20) },   // Samara Oblast
        { "RUS-24", (50, 20) },   // Nizhny Novgorod Oblast
        { "RUS-54", (20, 10) },   // Saratov Oblast
        { "RUS-12", (20, 10) },   // Volgograd Oblast
        { "RUS-63", (20, 0) },    // Ulyanovsk Oblast
        { "RUS-36", (10, 0) },    // Penza Oblast
        { "RUS-33", (10, 0) },    // Chuvashia
        { "RUS-42", (10, 0) },    // Mari El
        { "RUS-01", (10, 0) },    // Mordovia
        { "RUS-59", (20, 10) },   // Udmurtia
        { "RUS-05", (40, 10) },   // Khanty-Mansiysk
        { "RUS-68", (30, 10) },   // Yamalo-Nenets
        { "RUS-34", (30, 10) },   // Tyumen Oblast
        { "RUS-48", (40, 20) },   // Novosibirsk Oblast
        { "RUS-49", (20, 10) },   // Omsk Oblast
        { "RUS-23", (20, 10) },   // Tomsk Oblast
        { "RUS-75", (40, 20) },   // Kemerovo Oblast
        { "RUS-09", (30, 20) },   // Krasnoyarsk Krai
        { "RUS-13", (20, 10) },   // Irkutsk Oblast
        { "RUS-00", (20, 10) },   // Altai Krai
        { "RUS-39", (40, 20) },   // Krasnodar Krai
        { "RUS-16", (40, 20) },   // Rostov Oblast
        { "RUS-56", (20, 10) },   // Stavropol Krai
        { "RUS-27", (10, 0) },    // Astrakhan Oblast
        { "RUS-02", (30, 20) },   // Tula Oblast
        { "RUS-60", (20, 10) },   // Kaluga Oblast
        { "RUS-64", (20, 0) },    // Vladimir Oblast
        { "RUS-66", (20, 10) },   // Yaroslavl Oblast
        { "RUS-58", (10, 0) },    // Tver Oblast
        { "RUS-55", (10, 0) },    // Smolensk Oblast
        { "RUS-29", (10, 10) },   // Bryansk Oblast
        { "RUS-17", (10, 0) },    // Ryazan Oblast
        { "RUS-08", (10, 0) },    // Kostroma Oblast
        { "RUS-70", (10, 0) },    // Ivanovo Oblast
        { "RUS-50", (10, 0) },    // Oryol Oblast
        { "RUS-61", (10, 0) },    // Lipetsk Oblast
        { "RUS-67", (20, 10) },   // Voronezh Oblast
        { "RUS-28", (10, 10) },   // Belgorod Oblast
        { "RUS-40", (10, 10) },   // Kursk Oblast
        { "RUS-21", (10, 0) },    // Tambov Oblast
        { "RUS-65", (10, 0) },    // Vologda Oblast
        { "RUS-26", (10, 10) },   // Arkhangelsk Oblast
        { "RUS-45", (10, 10) },   // Murmansk Oblast
        { "RUS-79", (10, 10) },   // Kaliningrad
        { "RUS-47", (10, 0) },    // Novgorod Oblast
        { "RUS-15", (10, 0) },    // Pskov Oblast
        { "RUS-06", (10, 0) },    // Kirov Oblast
        { "RUS-76", (20, 20) },   // Khabarovsk Krai
        { "RUS-81", (20, 20) },   // Primorsky Krai
        { "RUS-37", (10, 10) },   // Amur Oblast
        { "RUS-52", (10, 10) },   // Sakhalin Oblast
        { "RUS-53", (10, 0) },    // Sakha Republic
        { "RUS-78", (10, 10) },   // Dagestan
        { "RUS-03", (10, 0) },    // Kurgan Oblast
        { "RUS-07", (10, 0) },    // Komi Republic
        { "RUS-25", (10, 0) },    // Republic of Karelia
    };

    private static readonly Dictionary<string, (int Civil, int Military)> USAFactories = new()
    {
        { "USA-04", (200, 60) },  // California
        { "USA-43", (180, 80) },  // Texas
        { "USA-32", (150, 20) },  // New York
        { "USA-13", (80, 20) },   // Illinois
        { "USA-35", (70, 30) },   // Ohio
        { "USA-38", (70, 30) },   // Pennsylvania
        { "USA-22", (70, 20) },   // Michigan
        { "USA-09", (60, 40) },   // Florida
        { "USA-47", (60, 30) },   // Washington
        { "USA-46", (50, 60) },   // Virginia
        { "USA-10", (50, 30) },   // Georgia
        { "USA-21", (50, 20) },   // Massachusetts
        { "USA-30", (50, 20) },   // New Jersey
        { "USA-06", (40, 30) },   // Connecticut
        { "USA-14", (40, 20) },   // Indiana
        { "USA-33", (40, 20) },   // North Carolina
        { "USA-20", (30, 30) },   // Maryland
        { "USA-00", (30, 30) },   // Alabama
        { "USA-18", (30, 20) },   // Louisiana
        { "USA-25", (30, 20) },   // Missouri
        { "USA-49", (30, 10) },   // Wisconsin
        { "USA-23", (30, 10) },   // Minnesota
        { "USA-42", (30, 10) },   // Tennessee
        { "USA-05", (20, 30) },   // Colorado
        { "USA-40", (20, 20) },   // South Carolina
        { "USA-02", (20, 20) },   // Arizona
        { "USA-17", (20, 10) },   // Kentucky
        { "USA-37", (20, 10) },   // Oregon
        { "USA-15", (20, 0) },    // Iowa
        { "USA-31", (10, 30) },   // New Mexico
        { "USA-28", (10, 30) },   // Nevada
        { "USA-44", (10, 20) },   // Utah
        { "USA-16", (10, 20) },   // Kansas
        { "USA-36", (10, 20) },   // Oklahoma
        { "USA-11", (10, 20) },   // Hawaii
        { "USA-01", (10, 20) },   // Alaska
        { "USA-24", (10, 10) },   // Mississippi
        { "USA-03", (10, 10) },   // Arkansas
        { "USA-27", (10, 10) },   // Nebraska
        { "USA-08", (10, 10) },   // District of Columbia
        { "USA-07", (10, 10) },   // Delaware
        { "USA-19", (10, 10) },   // Maine
        { "USA-39", (10, 10) },   // Rhode Island
        { "USA-12", (10, 10) },   // Idaho
        { "USA-48", (10, 0) },    // West Virginia
        { "USA-29", (10, 0) },    // New Hampshire
        { "USA-34", (0, 10) },    // North Dakota
        { "USA-41", (0, 10) },    // South Dakota
        { "USA-26", (0, 10) },    // Montana
        { "USA-50", (0, 10) },    // Wyoming
        { "USA-51", (10, 0) },    // Puerto Rico
    };

    private static readonly Dictionary<string, (int Civil, int Military)> IndianFactories = new()
    {
        { "IND-20", (200, 40) },  // Maharashtra
        { "IND-30", (150, 30) },  // Tamil Nadu
        { "IND-11", (130, 20) },  // Gujarat
        { "IND-16", (120, 30) },  // Karnataka
        { "IND-32", (100, 20) },  // Uttar Pradesh
        { "IND-09", (80, 20) },   // Delhi
        { "IND-12", (40, 10) },   // Haryana
        { "IND-34", (70, 20) },   // West Bengal
        { "IND-15", (40, 10) },   // Jharkhand
        { "IND-25", (30, 10) },   // Orissa
        { "IND-01", (70, 20) },   // Andhra Pradesh
        { "IND-17", (40, 10) },   // Kerala
        { "IND-27", (40, 10) },   // Punjab
        { "IND-28", (50, 20) },   // Rajasthan
        { "IND-19", (50, 10) },   // Madhya Pradesh
        { "IND-06", (30, 10) },   // Chhattisgarh
        { "IND-33", (20, 10) },   // Uttaranchal
        { "IND-03", (20, 10) },   // Assam
        { "IND-14", (10, 20) },   // Jammu and Kashmir
        { "IND-04", (20, 0) },    // Bihar
        { "IND-10", (10, 10) },   // Goa
        { "IND-13", (10, 0) },    // Himachal Pradesh
    };

    private static readonly Dictionary<string, (int Civil, int Military)> ChineseFactories = new()
    {
        { "CHN-05", (300, 40) },  // Guangdong
        { "CHN-15", (250, 40) },  // Jiangsu
        { "CHN-36", (200, 30) },  // Zhejiang
        { "CHN-24", (200, 40) },  // Shandong
        { "CHN-25", (150, 20) },  // Shanghai
        { "CHN-11", (120, 20) },  // Henan
        { "CHN-13", (100, 30) },  // Hubei
        { "CHN-27", (100, 40) },  // Sichuan
        { "CHN-09", (100, 20) },  // Hebei
        { "CHN-14", (80, 20) },   // Hunan
        { "CHN-03", (80, 20) },   // Fujian
        { "CHN-00", (70, 10) },   // Anhui
        { "CHN-18", (80, 40) },   // Liaoning
        { "CHN-10", (40, 30) },   // Heilongjiang
        { "CHN-17", (40, 20) },   // Jilin
        { "CHN-01", (80, 40) },   // Beijing
        { "CHN-28", (60, 20) },   // Tianjin
        { "CHN-02", (60, 20) },   // Chongqing
        { "CHN-16", (50, 10) },   // Jiangxi
        { "CHN-23", (50, 40) },   // Shaanxi
        { "CHN-06", (40, 10) },   // Guangxi
        { "CHN-26", (40, 10) },   // Shanxi
        { "CHN-07", (30, 20) },   // Guizhou
        { "CHN-35", (30, 10) },   // Yunnan
        { "CHN-04", (20, 20) },   // Gansu
        { "CHN-20", (30, 10) },   // NeiMongol
        { "CHN-29", (10, 10) },   // XinjiangUygur 1
        { "CHN-30", (10, 10) },   // XinjiangUygur 2
        { "CHN-08", (10, 20) },   // Hainan
        { "CHN-12", (30, 0) },    // HongKong
        { "CHN-22", (10, 10) },   // Qinghai
        { "CHN-21", (10, 0) },    // NingxiaHui
        { "CHN-32", (0, 10) },    // Xizang
    };

    private static readonly Dictionary<string, (int Civil, int Military)> FrenchFactories = new()
    {
        { "FRA-07", (150, 50) },  // Ile-de-France
        { "FRA-00", (100, 30) },  // Auvergne-Rhone-Alpes
        { "FRA-06", (80, 20) },   // Hauts-de-France
        { "FRA-05", (60, 20) },   // Grand Est
        { "FRA-12", (60, 30) },   // Provence-Alpes-Cote d'Azur
        { "FRA-09", (60, 20) },   // Nouvelle-Aquitaine
        { "FRA-10", (60, 30) },   // Occitanie
        { "FRA-11", (50, 20) },   // Pays de la Loire
        { "FRA-08", (40, 20) },   // Normandie
        { "FRA-01", (30, 10) },   // Bourgogne-Franche-Comte
        { "FRA-02", (30, 20) },   // Bretagne
        { "FRA-03", (30, 10) },   // Centre-Val de Loire
        { "FRA-04", (0, 10) },    // Corse
    };

    private static readonly Dictionary<string, (int Civil, int Military)> UKFactories = new()
    {
        { "GBR-03", (200, 60) },  // England
        { "GBR-01", (40, 30) },   // Scotland
        { "GBR-02", (20, 10) },   // Wales
        { "GBR-00", (10, 10) },   // Northern Ireland
    };

    private static readonly Dictionary<string, (int Civil, int Military)> JapaneseFactories = new()
    {
        { "JPN-30", (200, 30) },  // Tokyo
        { "JPN-46", (120, 30) },  // Kanagawa
        { "JPN-31", (80, 10) },   // Saitama
        { "JPN-28", (70, 10) },   // Chiba
        { "JPN-11", (50, 10) },   // Ibaraki
        { "JPN-05", (30, 0) },    // Tochigi
        { "JPN-08", (20, 0) },    // Gunma
        { "JPN-04", (150, 20) },  // Aichi
        { "JPN-10", (60, 10) },   // Shizuoka
        { "JPN-22", (30, 0) },    // Nagano
        { "JPN-19", (20, 10) },   // Gifu
        { "JPN-24", (20, 0) },    // Mie
        { "JPN-29", (10, 0) },    // Toyama
        { "JPN-34", (10, 0) },    // Ishikawa
        { "JPN-35", (10, 0) },    // Fukui
        { "JPN-21", (120, 20) },  // Osaka
        { "JPN-27", (80, 20) },   // Hyogo
        { "JPN-00", (40, 10) },   // Kyoto
        { "JPN-07", (10, 0) },    // Shiga
        { "JPN-37", (10, 0) },    // Nara
        { "JPN-14", (10, 0) },    // Wakayama
        { "JPN-25", (50, 20) },   // Hiroshima
        { "JPN-17", (20, 0) },    // Okayama
        { "JPN-32", (20, 10) },   // Yamaguchi
        { "JPN-18", (60, 10) },   // Fukuoka
        { "JPN-02", (30, 10) },   // Kumamoto
        { "JPN-15", (20, 20) },   // Nagasaki
        { "JPN-23", (10, 10) },   // Oita
        { "JPN-42", (10, 10) },   // Kagoshima
        { "JPN-01", (10, 0) },    // Saga
        { "JPN-26", (30, 20) },   // Hokkaido
        { "JPN-09", (30, 20) },   // Miyagi
        { "JPN-33", (20, 0) },    // Fukushima
        { "JPN-43", (20, 0) },    // Niigata
        { "JPN-20", (10, 10) },   // Aomori
        { "JPN-39", (10, 0) },    // Iwate
        { "JPN-13", (10, 0) },    // Yamagata
        { "JPN-16", (10, 0) },    // Akita
        { "JPN-36", (10, 0) },    // Ehime
        { "JPN-03", (10, 0) },    // Kagawa
        { "JPN-12", (0, 20) },    // Okinawa
        { "JPN-06", (10, 0) },    // Yamanashi
    };

    private static readonly Dictionary<string, (int Civil, int Military)> AustralianFactories = new()
    {
        { "AUS-00", (10, 10) },   // ACT
        { "AUS-01", (120, 30) },  // New South Wales
        { "AUS-02", (10, 20) },   // Northern Territory
        { "AUS-03", (80, 20) },   // Queensland
        { "AUS-04", (40, 20) },   // South Australia
        { "AUS-05", (10, 0) },    // Tasmania
        { "AUS-06", (100, 20) },  // Victoria
        { "AUS-07", (60, 20) },   // Western Australia
    };

    private static readonly Dictionary<string, (int Civil, int Military)> BrazilianFactories = new()
    {
        { "BRA-00", (10, 0) },    // Acre
        { "BRA-01", (20, 0) },    // Alagoas
        { "BRA-02", (10, 0) },    // Amapa
        { "BRA-03", (30, 10) },   // Amazonas
        { "BRA-04", (60, 10) },   // Bahia
        { "BRA-05", (40, 10) },   // Ceara
        { "BRA-06", (20, 30) },   // Distrito Federal
        { "BRA-07", (30, 10) },   // Espirito Santo
        { "BRA-08", (40, 10) },   // Goias
        { "BRA-09", (20, 0) },    // Maranhao
        { "BRA-10", (30, 10) },   // Mato Grosso
        { "BRA-11", (20, 10) },   // Mato Grosso do Sul
        { "BRA-12", (120, 30) },  // Minas Gerais
        { "BRA-13", (30, 10) },   // Para
        { "BRA-14", (20, 0) },    // Paraiba
        { "BRA-15", (80, 20) },   // Parana
        { "BRA-16", (40, 10) },   // Pernambuco
        { "BRA-17", (10, 0) },    // Piaui
        { "BRA-18", (100, 30) },  // Rio de Janeiro
        { "BRA-19", (20, 10) },   // Rio Grande do Norte
        { "BRA-20", (60, 10) },   // Rio Grande do Sul
        { "BRA-21", (10, 0) },    // Rondonia
        { "BRA-22", (10, 10) },   // Roraima
        { "BRA-23", (50, 10) },   // Santa Catarina
        { "BRA-24", (250, 40) },  // Sao Paulo
        { "BRA-25", (10, 0) },    // Sergipe
        { "BRA-26", (10, 0) },    // Tocantins
    };

    private static readonly Dictionary<string, (int Civil, int Military)> TurkishFactories = new()
    {
        { "TUR-00", (60, 20) },   // Marmararegion
        { "TUR-01", (30, 10) },   // Aegaeisregion
        { "TUR-02", (20, 10) },   // Mittelmeerregion
        { "TUR-03", (40, 20) },   // Zentralanatolien
        { "TUR-04", (20, 10) },   // Schwarzmeerregion
        { "TUR-05", (10, 10) },   // Ostanatolien
        { "TUR-06", (20, 0) },    // Suedostanatolien
    };

    /// <summary>
    /// Initialisiert Provinzen (Unterteilungen von Laendern)
    /// </summary>
    private void InitializeProvinces(Action<string, float>? onProgress = null)
    {
        void ReportProgress(string status, float progress) => onProgress?.Invoke(status, progress);

        // Alle einfachen Laender laden (ohne Fabrikverteilung)
        ReportProgress("Lade Provinzen...", 0.50f);
        foreach (var (file, prefix, tolerance) in ProvinceConfigs)
        {
            LoadCountryProvinces(file, prefix, tolerance);
        }

        // Laender mit Fabrikverteilung
        ReportProgress("Lade Industrielaender...", 0.85f);
        LoadCountryProvinces("german_states.geojson", "DEU", 0.02, GermanFactories, useGermanLoader: true, mines: GermanMines);
        LoadCountryProvinces("russia_regions.geojson", "RUS", 0.025, RussianFactories, mines: RussianMines);
        LoadCountryProvinces("us_states.geojson", "USA", 0.04, USAFactories, mines: USAMines);
        LoadCountryProvinces("india_states.geojson", "IND", 0.04, IndianFactories);
        LoadCountryProvinces("china_provinces.geojson", "CHN", 0.05, ChineseFactories);
        LoadCountryProvinces("france_regions.geojson", "FRA", 0.02, FrenchFactories);
        LoadCountryProvinces("uk_regions.geojson", "GBR", 0.02, UKFactories);
        LoadCountryProvinces("japan_prefectures.geojson", "JPN", 0.01, JapaneseFactories);
        LoadCountryProvinces("australia_states.geojson", "AUS", 0.05, AustralianFactories);
        LoadCountryProvinces("brazil_states.geojson", "BRA", 0.05, BrazilianFactories);
        LoadCountryProvinces("turkey_regions.geojson", "TUR", 0.02, TurkishFactories);

        // Automatisch Startminen fuer alle Laender zuweisen (ausser DEU, USA, RUS - die haben manuelle Minen)
        AssignStartingMinesFromDeposits();

        Console.WriteLine($"Gesamt: {Provinces.Count} Provinzen geladen");
    }

    /// <summary>
    /// Weist Startminen basierend auf den Ressourcenvorkommen aus resource-deposits.json zu.
    /// Laender mit manuell definierten Minen (DEU, USA, RUS) werden uebersprungen.
    /// </summary>
    private void AssignStartingMinesFromDeposits()
    {
        var manualMineCountries = new HashSet<string> { "DEU", "USA", "RUS" };
        int totalAssigned = 0;

        foreach (var province in Provinces.Values)
        {
            // Ueberspringe Laender mit manuell definierten Minen
            if (manualMineCountries.Contains(province.CountryId)) continue;

            // Ueberspringe Provinzen die bereits Minen haben
            if (province.Mines.Count > 0) continue;

            var deposits = GetProvinceResources(province.Name);
            if (deposits.Count == 0) continue;

            foreach (var (resourceType, _) in deposits)
            {
                var mineType = ResourceTypeToMineType(resourceType);
                if (mineType.HasValue)
                {
                    province.Mines.Add(new Mine(mineType.Value));
                    totalAssigned++;
                }
            }
        }

        Console.WriteLine($"[Minen] {totalAssigned} Startminen automatisch zugewiesen");
    }

    /// <summary>
    /// Konvertiert einen ResourceType in den entsprechenden MineType
    /// </summary>
    private static MineType? ResourceTypeToMineType(ResourceType type)
    {
        return type switch
        {
            ResourceType.Oil => MineType.OilWell,
            ResourceType.NaturalGas => MineType.GasDrill,
            ResourceType.Coal => MineType.CoalMine,
            ResourceType.Iron => MineType.IronMine,
            ResourceType.Copper => MineType.CopperMine,
            ResourceType.Uranium => MineType.UraniumMine,
            _ => null
        };
    }

    /// <summary>
    /// Generische Methode zum Laden von Provinzen aus einer GeoJSON-Datei
    /// </summary>
    private void LoadCountryProvinces(string filename, string prefix, double tolerance,
        Dictionary<string, (int Civil, int Military)>? factories = null,
        bool useGermanLoader = false,
        Dictionary<string, MineType[]>? mines = null)
    {
        string? geoJsonPath = FindDataFile(filename);
        if (geoJsonPath == null || !File.Exists(geoJsonPath))
        {
            Console.WriteLine($"Warnung: {filename} nicht gefunden");
            return;
        }

        var regions = useGermanLoader
            ? GeoJsonLoader.LoadGermanStates(geoJsonPath)
            : GeoJsonLoader.LoadProvinces(geoJsonPath, prefix);

        foreach (var (id, data) in regions)
        {
            var polys = data.Polygons
                .Select(p => SimplifyAndConvertGeoToScreen(p, tolerance))
                .Where(p => p.Length >= 3)
                .ToList();

            if (polys.Count > 0)
            {
                var province = new Province(id, data.Name, prefix, polys);

                if (factories != null && factories.TryGetValue(id, out var fac))
                {
                    province.CivilianFactories = fac.Civil;
                    province.MilitaryFactories = fac.Military;
                }

                if (mines != null && mines.TryGetValue(id, out var mineTypes))
                {
                    foreach (var mineType in mineTypes)
                        province.Mines.Add(new Mine(mineType));
                }

                Provinces[id] = province;
                CountriesWithProvinces.Add(prefix);
            }
        }
    }

    /// <summary>
    /// Laedt Fluesse aus GeoJSON
    /// </summary>
    private void LoadRivers()
    {
        string? geoJsonPath = FindDataFile("rivers.geojson");
        if (geoJsonPath == null || !File.Exists(geoJsonPath))
        {
            Console.WriteLine("Warnung: rivers.geojson nicht gefunden");
            return;
        }

        try
        {
            string json = File.ReadAllText(geoJsonPath);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var features = doc.RootElement.GetProperty("features");

            foreach (var feature in features.EnumerateArray())
            {
                var props = feature.GetProperty("properties");
                string? name = props.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                int scaleRank = props.TryGetProperty("scalerank", out var rankProp) ? rankProp.GetInt32() : 6;

                var geometry = feature.GetProperty("geometry");
                string? geomType = geometry.GetProperty("type").GetString();
                var coordinates = geometry.GetProperty("coordinates");

                var lineSegments = new List<Vector2[]>();

                if (geomType == "LineString")
                {
                    var points = ParseLineString(coordinates);
                    if (points.Length >= 2)
                        lineSegments.Add(points);
                }
                else if (geomType == "MultiLineString")
                {
                    foreach (var line in coordinates.EnumerateArray())
                    {
                        var points = ParseLineString(line);
                        if (points.Length >= 2)
                            lineSegments.Add(points);
                    }
                }

                if (lineSegments.Count > 0)
                {
                    _rivers.Add(new River(name, scaleRank, lineSegments));
                }
            }

            Console.WriteLine($"[WorldMap] {_rivers.Count} Fluesse geladen");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Laden der Fluesse: {ex.Message}");
        }
    }

    /// <summary>
    /// Parst einen LineString aus GeoJSON-Koordinaten
    /// </summary>
    private Vector2[] ParseLineString(System.Text.Json.JsonElement coordinates)
    {
        var points = new List<Vector2>();
        foreach (var coord in coordinates.EnumerateArray())
        {
            double lon = coord[0].GetDouble();
            double lat = coord[1].GetDouble();
            points.Add(GeoPointToMap(lon, lat));
        }
        return points.ToArray();
    }
}
