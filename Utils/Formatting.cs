using System.Globalization;

namespace GrandStrategyGame.UI;

/// <summary>
/// Zentrale Formatierungsfunktionen fuer Zahlen und Werte
/// </summary>
public static class Formatting
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formatiert Bevoelkerungszahlen (z.B. "1.41 Mrd." oder "67.8 Mio.")
    /// </summary>
    public static string Population(long number)
    {
        if (number >= 1_000_000_000) return (number / 1_000_000_000.0).ToString("F2", Inv) + " Mrd.";
        if (number >= 1_000_000) return (number / 1_000_000.0).ToString("F1", Inv) + " Mio.";
        return number.ToString("N0", Inv);
    }

    /// <summary>
    /// Formatiert grosse Zahlen (z.B. BIP) mit K/Mio./Mrd. Suffix
    /// </summary>
    public static string Number(double number)
    {
        if (number >= 1_000_000_000) return (number / 1_000_000_000).ToString("F2", Inv) + " Mrd.";
        if (number >= 1_000_000) return (number / 1_000_000).ToString("F1", Inv) + " Mio.";
        if (number >= 1_000) return (number / 1_000).ToString("F1", Inv) + "K";
        return number.ToString("F1", Inv);
    }

    /// <summary>
    /// Formatiert Ressourcenmengen in deutscher Notation (z.B. "100.0Mrd" oder "5.2Mio")
    /// </summary>
    public static string Resource(double amount)
    {
        if (amount >= 1_000_000_000) return (amount / 1_000_000_000).ToString("F1", Inv) + "Mrd";
        if (amount >= 1_000_000) return (amount / 1_000_000).ToString("F1", Inv) + "Mio";
        if (amount >= 1_000) return (amount / 1_000).ToString("F1", Inv) + "Tsd";
        return amount.ToString("F0", Inv);
    }

    /// <summary>
    /// Formatiert Budget-Aenderungen mit K/M Suffix (Spielwerte sind in Millionen)
    /// z.B. 5000 -> "5.0K" (5 Milliarden), 50 -> "50M" (50 Millionen)
    /// </summary>
    public static string BudgetChange(double amount)
    {
        double abs = Math.Abs(amount);
        if (abs >= 1_000_000) return (abs / 1_000_000).ToString("F1", Inv) + "B";  // Billionen
        if (abs >= 1_000) return (abs / 1_000).ToString("F1", Inv) + "K";          // Milliarden (Kilo-Millionen)
        if (abs >= 1) return abs.ToString("F0", Inv) + "M";                         // Millionen
        if (abs >= 0.001) return (abs * 1000).ToString("F0", Inv) + "K";           // Tausende
        return "0";
    }

    /// <summary>
    /// Formatiert Geldbetraege die in Millionen gespeichert sind (z.B. "$2.69 Bio." oder "$5.2 Mrd.")
    /// </summary>
    public static string Money(double millions)
    {
        double abs = Math.Abs(millions);
        string sign = millions < 0 ? "-" : "";
        if (abs >= 1_000_000) return sign + "$" + (abs / 1_000_000).ToString("F2", Inv) + " Bio.";
        if (abs >= 1_000) return sign + "$" + (abs / 1_000).ToString("F1", Inv) + " Mrd.";
        if (abs >= 1) return sign + "$" + abs.ToString("F1", Inv) + " Mio.";
        return sign + "$" + (abs * 1000).ToString("F0", Inv) + "K";
    }

    /// <summary>
    /// Formatiert Prozentwerte (z.B. "2.5%" oder "+1.0%")
    /// </summary>
    public static string Percentage(double value, bool showSign = false)
    {
        double pct = value * 100;
        if (showSign)
            return (pct >= 0 ? "+" : "") + pct.ToString("F1", Inv) + "%";
        return pct.ToString("F1", Inv) + "%";
    }
}
