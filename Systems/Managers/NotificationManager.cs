using GrandStrategyGame.Models;

namespace GrandStrategyGame.Systems.Managers;

/// <summary>
/// Verwaltet Spielnachrichten und Benachrichtigungen.
/// Abonniert Game-Events und erstellt daraus Nachrichten.
/// </summary>
public class NotificationManager : GameSystemBase
{
    public override string Name => "Notification";
    public override int Priority => 200; // Nach allen anderen Managern
    public override TickType[] SubscribedTicks => new[] { TickType.Daily };

    private readonly List<GameNotification> _notifications = new();
    private GameContext? _context;

    // Bereits ausgeloeste datumsbasierte Nachrichten (verhindert Duplikate)
    private readonly HashSet<string> _triggeredDateEvents = new();

    // Gecachte Popup-Liste (wird nur bei Aenderungen aktualisiert)
    private readonly List<GameNotification> _activePopupsCache = new();
    private bool _popupCacheDirty = true;

    /// <summary>
    /// Alle Nachrichten (neueste zuerst)
    /// </summary>
    public IReadOnlyList<GameNotification> Notifications => _notifications;

    /// <summary>
    /// Anzahl ungelesener Nachrichten
    /// </summary>
    public int UnreadCount
    {
        get
        {
            int count = 0;
            foreach (var n in _notifications)
                if (!n.IsRead) count++;
            return count;
        }
    }

    /// <summary>
    /// Aktive Popups (PopupRemainingDays > 0), maximal 5
    /// </summary>
    public List<GameNotification> ActivePopups
    {
        get
        {
            if (_popupCacheDirty)
            {
                _activePopupsCache.Clear();
                foreach (var n in _notifications)
                {
                    if (n.PopupRemainingDays > 0)
                    {
                        _activePopupsCache.Add(n);
                        if (_activePopupsCache.Count >= 5) break;
                    }
                }
                _popupCacheDirty = false;
            }
            return _activePopupsCache;
        }
    }

    public override void Initialize(GameContext context)
    {
        _context = context;

        // Game-Events abonnieren
        context.Events.Subscribe<WarDeclaredEvent>(OnWarDeclared);
        context.Events.Subscribe<ElectionResultEvent>(OnElectionResult);
    }

    public override void OnTick(TickType tickType, GameContext context)
    {
        if (tickType != TickType.Daily) return;

        // KEIN Auto-Dismiss mehr - Popups bleiben bis der Spieler sie schliesst

        // Datumsbasierte Nachrichten pruefen
        CheckDateEvents(context);
    }

    /// <summary>
    /// Fuegt eine neue Nachricht hinzu
    /// </summary>
    public void AddNotification(string title, string message, NotificationType type,
        string? relatedCountryId = null, string? imageName = null)
    {
        if (_context == null) return;

        var notification = new GameNotification(
            title, message, type,
            _context.CurrentDay, _context.CurrentMonth, _context.CurrentYear,
            relatedCountryId, imageName);

        _notifications.Insert(0, notification); // Neueste zuerst
        _popupCacheDirty = true;

        // Twitter-Sound abspielen (wie X/Twitter Benachrichtigung)
        // Fallback auf NotificationInfo wenn Twitter-Sound nicht vorhanden
        SoundManager.PlayWithFallback(SoundEffect.NotificationTwitter, SoundEffect.NotificationInfo);
    }

    /// <summary>
    /// Markiert alle Nachrichten als gelesen
    /// </summary>
    public void MarkAllAsRead()
    {
        foreach (var n in _notifications)
            n.IsRead = true;
    }

    /// <summary>
    /// Schliesst ein Popup (setzt PopupRemainingDays auf 0)
    /// </summary>
    public void DismissPopup(int notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification != null)
        {
            notification.PopupRemainingDays = 0;
            _popupCacheDirty = true;
        }
    }

    /// <summary>
    /// Zeigt eine existierende Nachricht erneut als Popup an
    /// </summary>
    public void ReshowAsPopup(int notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification != null)
        {
            // Popup wieder aktivieren
            notification.PopupRemainingDays = 1;
            _popupCacheDirty = true;
            // Sound abspielen
            SoundManager.PlayWithFallback(SoundEffect.NotificationTwitter, SoundEffect.NotificationInfo);
        }
    }

    // --- Datumsbasierte Nachrichten ---

    private void CheckDateEvents(GameContext context)
    {
        // 24. Februar 2024 — 2 Jahre Russland-Ukraine-Krieg
        if (context.CurrentDay == 24 && context.CurrentMonth == 2 && context.CurrentYear == 2024)
        {
            TriggerDateEvent("ukraine_krieg_2j",
                "2 Jahre Krieg in der Ukraine",
                "EILMELDUNG: Der russische Angriffskrieg auf die Ukraine dauert nun seit zwei Jahren an. " +
                "Am 24. Februar 2022 begann Russland unter Praesident Wladimir Putin die grossangelegte Invasion des Nachbarlandes. " +
                "Seitdem wurden Tausende Zivilisten getoetet, Millionen Menschen sind geflohen und ganze Staedte wurden zerstoert. " +
                "Die internationale Gemeinschaft hat mit beispiellosen Sanktionen reagiert. Die NATO hat ihre Ostflanke verstaerkt. " +
                "Die Ukraine erhaelt militaerische und humanitaere Hilfe aus dem Westen. Ein Ende des Konflikts ist nicht in Sicht.",
                NotificationType.Danger,
                "RUS",
                "news_picture_1.jpg");
        }

        // Weitere politische/wirtschaftliche Events koennen hier hinzugefuegt werden
    }

    private void TriggerDateEvent(string eventId, string title, string message,
        NotificationType type, string? relatedCountryId = null, string? imageName = null)
    {
        if (_triggeredDateEvents.Contains(eventId)) return;
        _triggeredDateEvents.Add(eventId);
        AddNotification(title, message, type, relatedCountryId, imageName);
    }

    // --- Event-Handler ---

    private void OnWarDeclared(WarDeclaredEvent e)
    {
        if (_context == null) return;

        string aggressorName = GetCountryName(e.AggressorId);
        string defenderName = GetCountryName(e.DefenderId);

        AddNotification(
            "Krieg erklaert!",
            $"{aggressorName} hat {defenderName} den Krieg erklaert.",
            NotificationType.Danger,
            e.AggressorId);
    }

    private void OnElectionResult(ElectionResultEvent e)
    {
        if (_context == null) return;

        // Nur Wahlergebnisse von USA und Russland anzeigen
        if (e.CountryId != CountryIds.USA && e.CountryId != CountryIds.Russia)
            return;

        string countryName = GetCountryName(e.CountryId);

        AddNotification(
            "Wahlergebnis",
            $"In {countryName} hat die Partei \"{e.WinningParty}\" die Wahl gewonnen.",
            NotificationType.Info,
            e.CountryId);
    }

    private string GetCountryName(string countryId)
    {
        if (_context?.Countries.TryGetValue(countryId, out var country) == true)
            return country.Name;
        return countryId;
    }

    public override void Shutdown()
    {
        _notifications.Clear();
    }
}
