namespace GrandStrategyGame.Models;

/// <summary>
/// Typ einer Benachrichtigung (bestimmt Farbe und Icon)
/// </summary>
public enum NotificationType
{
    Info,       // Allgemeine Info (blau)
    Warning,    // Warnung (gelb)
    Danger,     // Gefahr/Krieg (rot)
    Success     // Positive Nachricht (gruen)
}

/// <summary>
/// Eine Spielnachricht/Benachrichtigung
/// </summary>
public class GameNotification
{
    private static int _nextId = 1;

    public int Id { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public NotificationType Type { get; set; }
    public int Day { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public bool IsRead { get; set; }
    public string? RelatedCountryId { get; set; }
    public int PopupRemainingDays { get; set; }
    public string? ImageName { get; set; }  // Optional: Bildname fuer die Nachricht

    public GameNotification(string title, string message, NotificationType type,
        int day, int month, int year, string? relatedCountryId = null, string? imageName = null)
    {
        Id = _nextId++;
        Title = title;
        Message = message;
        Type = type;
        Day = day;
        Month = month;
        Year = year;
        IsRead = false;
        RelatedCountryId = relatedCountryId;
        PopupRemainingDays = 10; // 10 Spiel-Tage sichtbar
        ImageName = imageName;
    }

    /// <summary>
    /// Formatiertes Datum als String
    /// </summary>
    public string DateString => $"{Day:D2}.{Month:D2}.{Year}";
}
