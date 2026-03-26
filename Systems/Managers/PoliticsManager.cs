using System.Text.Json;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet das politische System.
/// - Regierungsformen
/// - Parteien und Ideologien
/// - Wahlen
/// - Politische Stabilität
/// - Innenpolitik
/// </summary>
public class PoliticsManager : GameSystemBase
{
    public override string Name => "Politics";
    public override int Priority => 40;
    public override TickType[] SubscribedTicks => new[] { TickType.Daily, TickType.Monthly, TickType.Yearly };

    // Politische Daten pro Land
    private readonly Dictionary<string, CountryPolitics> _countryPolitics = new();

    // Aktive Werbekampagnen pro Land
    private readonly Dictionary<string, AdvertisingCampaign> _activeAdvertising = new();

    // Tracking: letztes Wahljahr pro Land (verhindert doppelte Wahlen)
    private readonly Dictionary<string, int> _lastElectionYear = new();

    /// <summary>
    /// Kosten einer Werbekampagne (in Millionen USD)
    /// </summary>
    public const double AdvertisingCost = 500;

    public override void Initialize(GameContext context)
    {
        // Lade politische Daten aus JSON
        LoadPoliticsFromJson();

        // Für Länder ohne spezifische Daten: Standardwerte setzen
        foreach (var (countryId, country) in context.Countries)
        {
            if (!_countryPolitics.ContainsKey(countryId))
            {
                _countryPolitics[countryId] = new CountryPolitics
                {
                    GovernmentType = GovernmentType.Democracy,
                    Stability = 0.7,
                    WarSupport = 0.3,
                    RulingParty = null,
                    Parties = new List<PoliticalParty>()
                };
            }
        }

        Console.WriteLine($"[PoliticsManager] {_countryPolitics.Count} Länder mit politischen Daten");
    }

    /// <summary>
    /// Lädt politische Daten aus der politics.json Datei
    /// </summary>
    private void LoadPoliticsFromJson()
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
            string testPath = Path.Combine(basePath, "Data", "politics.json");
            if (File.Exists(testPath))
            {
                filePath = testPath;
                break;
            }
        }

        if (filePath == null)
        {
            Console.WriteLine("[PoliticsManager] politics.json nicht gefunden");
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("politics", out var politicsArray))
            {
                foreach (var entry in politicsArray.EnumerateArray())
                {
                    var politics = ParsePoliticsEntry(entry);
                    if (politics != null)
                    {
                        _countryPolitics[politics.CountryId] = politics;
                    }
                }
            }

            Console.WriteLine($"[PoliticsManager] Politische Daten aus JSON geladen");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PoliticsManager] Fehler beim Laden: {ex.Message}");
        }
    }

    private CountryPolitics? ParsePoliticsEntry(JsonElement json)
    {
        try
        {
            string countryId = json.GetProperty("id").GetString() ?? "";
            if (string.IsNullOrEmpty(countryId)) return null;

            // Regierungsform parsen
            string govTypeStr = json.GetProperty("governmentType").GetString() ?? "Democracy";
            GovernmentType govType = govTypeStr switch
            {
                "AuthoritarianRegime" => GovernmentType.AuthoritarianRegime,
                "Monarchy" => GovernmentType.Monarchy,
                "CommunistState" => GovernmentType.CommunistState,
                "Theocracy" => GovernmentType.Theocracy,
                "MilitaryJunta" => GovernmentType.MilitaryJunta,
                _ => GovernmentType.Democracy
            };

            var politics = new CountryPolitics
            {
                CountryId = countryId,
                GovernmentType = govType,
                HeadOfState = json.TryGetProperty("headOfState", out var hos) ? hos.GetString() : null,
                HeadOfGovernment = json.TryGetProperty("headOfGovernment", out var hog) ? hog.GetString() : null,
                HeadOfStateImageFile = json.TryGetProperty("headOfStateImage", out var hosImg) ? hosImg.GetString() : null,
                Stability = json.TryGetProperty("stability", out var stab) ? stab.GetDouble() : 0.7,
                PublicSupport = json.TryGetProperty("publicSupport", out var pubSup) ? pubSup.GetDouble() : 0.6,
                WarSupport = json.TryGetProperty("warSupport", out var warSup) ? warSup.GetDouble() : 0.3,
                EconomyAlignment = json.TryGetProperty("economyAlignment", out var ecoAlign) ? ecoAlign.GetDouble() : 0.5,
                SocietyAlignment = json.TryGetProperty("societyAlignment", out var socAlign) ? socAlign.GetDouble() : 0.5,
                FreedomAlignment = json.TryGetProperty("freedomAlignment", out var freAlign) ? freAlign.GetDouble() : 0.5,
                Parties = new List<PoliticalParty>()
            };

            // Parteien laden
            if (json.TryGetProperty("parties", out var partiesArray))
            {
                foreach (var partyJson in partiesArray.EnumerateArray())
                {
                    var party = ParseParty(partyJson);
                    if (party != null)
                    {
                        politics.Parties.Add(party);
                    }
                }
            }

            // Regierende Partei setzen
            string? rulingPartyName = json.TryGetProperty("rulingParty", out var rp) ? rp.GetString() : null;
            if (rulingPartyName != null)
            {
                politics.RulingParty = politics.Parties.FirstOrDefault(p => p.Name == rulingPartyName);
            }

            return politics;
        }
        catch
        {
            return null;
        }
    }

    private PoliticalParty? ParseParty(JsonElement json)
    {
        try
        {
            string name = json.GetProperty("name").GetString() ?? "";
            if (string.IsNullOrEmpty(name)) return null;

            // Ideologie parsen
            string ideologyStr = json.TryGetProperty("ideology", out var ideo) ? ideo.GetString() ?? "Democratic" : "Democratic";
            Ideology ideology = ideologyStr switch
            {
                "Conservative" => Ideology.Conservative,
                "Socialist" => Ideology.Socialist,
                "Communist" => Ideology.Communist,
                "Fascist" => Ideology.Fascist,
                "Nationalist" => Ideology.Nationalist,
                "Liberal" => Ideology.Liberal,
                "Green" => Ideology.Green,
                _ => Ideology.Democratic
            };

            return new PoliticalParty
            {
                Name = name,
                Ideology = ideology,
                Popularity = json.TryGetProperty("popularity", out var pop) ? pop.GetDouble() : 0.1,
                Seats = json.TryGetProperty("seats", out var seats) ? seats.GetInt32() : 0,
                TotalSeats = json.TryGetProperty("totalSeats", out var ts) ? ts.GetInt32() : 0
            };
        }
        catch
        {
            return null;
        }
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        switch (tickType)
        {
            case TickType.Daily:
                UpdateAdvertising(context);
                break;

            case TickType.Monthly:
                UpdateStability(context);
                break;

            case TickType.Yearly:
                CheckElections(context);
                break;
        }
    }

    private void UpdateStability(GameContext context)
    {
        var militaryManager = context.Game.GetSystem<MilitaryManager>();

        foreach (var (countryId, politics) in _countryPolitics)
        {
            if (!context.Countries.TryGetValue(countryId, out var country))
                continue;

            double stabilityChange = 0;

            // Wirtschaftsfaktor: Positives BIP-Wachstum stabilisiert
            if (country.GDPGrowthRate > 0.01)
                stabilityChange += 0.005; // Gutes Wachstum = mehr Stabilitaet
            else if (country.GDPGrowthRate < -0.01)
                stabilityChange -= 0.01;  // Rezession = Instabilitaet

            // Arbeitslosigkeit destabilisiert
            if (country.UnemploymentRate > 0.10)
                stabilityChange -= (country.UnemploymentRate - 0.10) * 0.05;

            // Hohe Inflation destabilisiert
            if (country.Inflation > 0.05)
                stabilityChange -= (country.Inflation - 0.05) * 0.1;

            // Budget-Defizit destabilisiert
            double dailyBudgetChange = country.CalculateDailyBudgetChange();
            if (dailyBudgetChange < 0)
                stabilityChange -= 0.002;

            // Kriege destabilisieren
            if (militaryManager != null)
            {
                var wars = militaryManager.GetWars(countryId);
                if (wars.Any())
                    stabilityChange -= 0.005 * wars.Count();
            }

            // Natuerliche Erholung in Richtung Mittelwert
            if (politics.Stability < 0.5)
                stabilityChange += 0.005;
            else if (politics.Stability > 0.9)
                stabilityChange -= 0.002; // Leichter Rueckgang bei sehr hoher Stabilitaet

            // Oeffentliche Unterstuetzung passt sich der Stabilitaet an
            double supportDelta = (politics.Stability - politics.PublicSupport) * 0.05;
            politics.PublicSupport = Math.Clamp(politics.PublicSupport + supportDelta, 0, 1);

            // Tech bonus: population_happiness verbessert Stabilitaet
            if (countryId == context.PlayerCountry?.Id)
            {
                var techManager = context.Game.GetSystem<TechTreeManager>();
                if (techManager != null)
                {
                    double happinessBonus = techManager.GetEffect("population_happiness");
                    stabilityChange += happinessBonus * 0.01;
                }
            }

            politics.Stability = Math.Clamp(politics.Stability + stabilityChange, 0, 1);
        }
    }

    private void CheckElections(GameContext context)
    {
        foreach (var (countryId, politics) in _countryPolitics)
        {
            if (politics.GovernmentType != GovernmentType.Democracy)
                continue;

            // Prüfe ob Wahljahr (nur einmal pro Wahljahr)
            if (context.CurrentYear % 4 == 0 &&
                (!_lastElectionYear.TryGetValue(countryId, out var lastYear) || lastYear != context.CurrentYear))
            {
                _lastElectionYear[countryId] = context.CurrentYear;
                HoldElection(countryId, politics, context);
            }
        }
    }

    private void HoldElection(string countryId, CountryPolitics politics, GameContext context)
    {
        if (politics.Parties.Count == 0)
            return;

        // Simuliere Wahlergebnis basierend auf Popularität
        var winner = politics.Parties
            .OrderByDescending(p => p.Popularity + (Random.Shared.NextDouble() - 0.5) * 0.2)
            .FirstOrDefault();

        if (winner != null)
        {
            politics.RulingParty = winner;

            // Publiziere Event
            context.Events.Publish(new ElectionResultEvent(countryId, winner.Name));
        }
    }

    /// <summary>
    /// Gibt politische Daten für ein Land zurück
    /// </summary>
    public CountryPolitics? GetPolitics(string countryId)
    {
        return _countryPolitics.TryGetValue(countryId, out var politics) ? politics : null;
    }

    /// <summary>
    /// Startet eine Werbekampagne fuer eine Partei (180 Tage, ~+2% Popularitaet/Monat)
    /// </summary>
    public bool StartAdvertising(string countryId, string partyName)
    {
        if (!_countryPolitics.TryGetValue(countryId, out var politics))
            return false;

        // Partei muss existieren
        var party = politics.Parties.FirstOrDefault(p => p.Name == partyName);
        if (party == null)
            return false;

        // Bereits aktive Kampagne? Ueberschreiben
        _activeAdvertising[countryId] = new AdvertisingCampaign
        {
            PartyName = partyName,
            RemainingDays = 180,
            TotalDays = 180,
            DailyEffect = 0.02 / 30.0  // ~+2% pro Monat, taeglich verteilt
        };

        return true;
    }

    /// <summary>
    /// Gibt die aktive Werbekampagne fuer ein Land zurueck (oder null)
    /// </summary>
    public AdvertisingCampaign? GetActiveAdvertising(string countryId)
    {
        return _activeAdvertising.TryGetValue(countryId, out var campaign) ? campaign : null;
    }

    /// <summary>
    /// Bricht eine aktive Werbekampagne ab
    /// </summary>
    public void CancelAdvertising(string countryId)
    {
        _activeAdvertising.Remove(countryId);
    }

    /// <summary>
    /// Aktualisiert aktive Werbekampagnen (taeglich)
    /// </summary>
    private void UpdateAdvertising(GameContext context)
    {
        var toRemove = new List<string>();

        foreach (var (countryId, campaign) in _activeAdvertising)
        {
            if (!_countryPolitics.TryGetValue(countryId, out var politics))
            {
                toRemove.Add(countryId);
                continue;
            }

            var targetParty = politics.Parties.FirstOrDefault(p => p.Name == campaign.PartyName);
            if (targetParty == null)
            {
                toRemove.Add(countryId);
                continue;
            }

            // Popularitaet der Zielpartei taeglich erhoehen
            double boost = campaign.DailyEffect;
            double oldPopularity = targetParty.Popularity;
            targetParty.Popularity = Math.Min(0.95, targetParty.Popularity + boost);
            double actualBoost = targetParty.Popularity - oldPopularity;

            // Andere Parteien proportional reduzieren (damit Summe ~1.0 bleibt)
            double othersTotal = politics.Parties.Where(p => p.Name != campaign.PartyName).Sum(p => p.Popularity);
            if (othersTotal > 0 && actualBoost > 0)
            {
                foreach (var otherParty in politics.Parties)
                {
                    if (otherParty.Name == campaign.PartyName) continue;
                    double reduction = actualBoost * (otherParty.Popularity / othersTotal);
                    otherParty.Popularity = Math.Max(0.01, otherParty.Popularity - reduction);
                }
            }

            campaign.RemainingDays--;

            // Periodische Nachrichtenartikel zur Kampagne (alle ~30 Tage)
            int elapsed = campaign.TotalDays - campaign.RemainingDays;
            if (elapsed > 0 && elapsed % 30 == 0 && campaign.RemainingDays > 0)
            {
                var notifManager = context.Game.GetSystem<NotificationManager>();
                if (notifManager != null)
                {
                    double currentPct = targetParty.Popularity * 100;
                    int monthNum = elapsed / 30;
                    string headline = monthNum switch
                    {
                        1 => $"{campaign.PartyName}: Plakate ueberall",
                        2 => $"{campaign.PartyName} legt in Umfragen zu",
                        3 => $"{campaign.PartyName} bei {currentPct:F1}%",
                        4 => $"Sonntagsfrage: {currentPct:F1}%",
                        5 => $"{campaign.PartyName}: Endspurt",
                        _ => $"{campaign.PartyName} im Aufwind"
                    };
                    string message = monthNum switch
                    {
                        1 => $"In vielen Staedten sind neue Wahlplakate der {campaign.PartyName} aufgetaucht. " +
                             $"\"Man kommt kaum daran vorbei\", sagt ein Passant. Aktuelle Umfrage: {currentPct:F1}%.",
                        2 => $"Die Werbekampagne der {campaign.PartyName} scheint Fruechte zu tragen. " +
                             $"Im aktuellen Politbarometer liegt die Partei bei {currentPct:F1}%. " +
                             $"\"Die Leute reden darueber\", berichtet ein Lokalpolitiker.",
                        3 => $"Ob im Fernsehen, auf Social Media oder an der Bushaltestelle - die {campaign.PartyName} " +
                             $"ist derzeit omnipraesent. Laut neuester Umfrage stehen sie bei {currentPct:F1}%. " +
                             $"Kritiker sprechen von \"aggressivem Wahlkampf\".",
                        4 => $"Die juengste Sonntagsfrage sieht die {campaign.PartyName} bei {currentPct:F1}%. " +
                             $"\"Das ist ein klares Signal\", kommentiert ein Politikwissenschaftler.",
                        5 => $"Auf der Zielgeraden gibt die {campaign.PartyName} nochmal alles. " +
                             $"Mit {currentPct:F1}% in den Umfragen sehen sich die Strategen bestaetigt.",
                        _ => $"Die {campaign.PartyName} liegt laut aktueller Erhebung bei {currentPct:F1}%."
                    };
                    notifManager.AddNotification(headline, message, NotificationType.Info, countryId);
                }
            }

            if (campaign.RemainingDays <= 0)
            {
                toRemove.Add(countryId);

                // Abschluss-Benachrichtigung
                double finalPct = targetParty.Popularity * 100;
                var notifManager = context.Game.GetSystem<NotificationManager>();
                notifManager?.AddNotification(
                    $"{campaign.PartyName}: Kampagne vorbei",
                    $"Nach monatelanger Werbung ist die Kampagne der {campaign.PartyName} ausgelaufen. " +
                    $"Die Partei steht nun bei {finalPct:F1}% - Beobachter sprechen von einem " +
                    (finalPct >= 30 ? "beeindruckenden Ergebnis." : "soliden Ergebnis.") +
                    " Ob die Wirkung anhaelt, bleibt abzuwarten.",
                    NotificationType.Info,
                    countryId
                );
            }
        }

        foreach (var id in toRemove)
        {
            _activeAdvertising.Remove(id);
        }
    }

}

/// <summary>
/// Politische Daten eines Landes
/// </summary>
public class CountryPolitics
{
    public string CountryId { get; set; } = "";
    public GovernmentType GovernmentType { get; set; }
    public string? HeadOfState { get; set; }
    public string? HeadOfGovernment { get; set; }
    public string? HeadOfStateImageFile { get; set; }  // z.B. "merz_friedrich"
    public double Stability { get; set; }        // 0-1
    public double PublicSupport { get; set; }    // 0-1 (Zustimmung der Bevölkerung)
    public double WarSupport { get; set; }       // 0-1
    public PoliticalParty? RulingParty { get; set; }
    public List<PoliticalParty> Parties { get; set; } = new();

    // Politische Ausrichtung (jeweils 0-1, wobei 0.5 = Mitte)
    public double EconomyAlignment { get; set; } = 0.5;     // 0=Links, 1=Rechts
    public double SocietyAlignment { get; set; } = 0.5;     // 0=Progressiv, 1=Konservativ
    public double FreedomAlignment { get; set; } = 0.5;     // 0=Autoritär, 1=Liberal
}

/// <summary>
/// Politische Partei
/// </summary>
public class PoliticalParty
{
    public string Name { get; set; } = "";
    public Ideology Ideology { get; set; }
    public double Popularity { get; set; }  // 0-1
    public int Seats { get; set; }          // Aktuelle Sitze im Parlament
    public int TotalSeats { get; set; }     // Gesamtsitze im Parlament
}

public enum GovernmentType
{
    Democracy,
    AuthoritarianRegime,
    Monarchy,
    CommunistState,
    Theocracy,
    MilitaryJunta
}

public enum Ideology
{
    Democratic,
    Conservative,
    Socialist,
    Communist,
    Fascist,
    Nationalist,
    Liberal,
    Green
}

/// <summary>
/// Aktive Werbekampagne fuer eine Partei
/// </summary>
public class AdvertisingCampaign
{
    public string PartyName { get; set; } = "";
    public int RemainingDays { get; set; }       // Verbleibende Laufzeit in Tagen
    public double DailyEffect { get; set; }      // Popularitaets-Boost pro Tag
    public int TotalDays { get; set; } = 180;    // Gesamtlaufzeit (fuer Fortschrittsanzeige)
}

// Event für Wahlergebnisse
public record ElectionResultEvent(string CountryId, string WinningParty) : IGameEvent;
