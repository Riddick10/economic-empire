using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.UI;

namespace GrandStrategyGame;

/// <summary>
/// Program - Ressourcen-Laden und -Zeichnen (Flaggen, Leader, Icons)
/// </summary>
partial class Program
{
    /// <summary>
    /// Generiert moegliche Dateinamen fuer einen Leader-Namen.
    /// Probiert verschiedene Formate: nachname_vorname, vorname_nachname, alle_teile
    /// </summary>
    internal static string[] GetLeaderFileNameVariants(string leaderName)
    {
        if (string.IsNullOrEmpty(leaderName))
            return Array.Empty<string>();

        // Sonderzeichen entfernen und in Kleinbuchstaben
        var normalized = leaderName.ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
            .Replace("ß", "ss").Replace("-", "_").Replace("'", "");

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return Array.Empty<string>();
        if (parts.Length == 1) return new[] { parts[0] };

        var variants = new List<string>();

        // Format 1: nachname_vorname(n) - fuer westliche Namen
        string lastName = parts[^1];
        string firstNames = string.Join("_", parts[..^1]);
        variants.Add($"{lastName}_{firstNames}");

        // Format 2: vorname_nachname - fuer asiatische Namen (Xi Jinping -> xi_jinping)
        variants.Add(string.Join("_", parts));

        // Format 3: erster_rest - fuer Namen wie "Kim Jong Un" -> "kim_jong_un"
        // (schon durch Format 2 abgedeckt)

        return variants.Distinct().ToArray();
    }

    /// <summary>
    /// Gibt den deutschen Namen fuer eine Regierungsform zurueck
    /// </summary>
    internal static string GetGovernmentTypeName(GovernmentType type)
    {
        return type switch
        {
            GovernmentType.Democracy => "Demokratie",
            GovernmentType.AuthoritarianRegime => "Autoritaeres Regime",
            GovernmentType.Monarchy => "Monarchie",
            GovernmentType.CommunistState => "Kommunistischer Staat",
            GovernmentType.Theocracy => "Theokratie",
            GovernmentType.MilitaryJunta => "Militaerjunta",
            _ => "Unbekannt"
        };
    }

    /// <summary>
    /// Gibt den deutschen Namen fuer eine Ideologie zurueck
    /// </summary>
    internal static string GetIdeologyName(Ideology ideology)
    {
        return ideology switch
        {
            Ideology.Democratic => "Demokratisch",
            Ideology.Conservative => "Konservativ",
            Ideology.Socialist => "Sozialistisch",
            Ideology.Communist => "Kommunistisch",
            Ideology.Fascist => "Faschistisch",
            Ideology.Nationalist => "Nationalistisch",
            Ideology.Liberal => "Liberal",
            Ideology.Green => "Gruen",
            _ => "Unbekannt"
        };
    }

    /// <summary>
    /// Laedt eine Flagge oder gibt sie aus dem Cache zurueck
    /// </summary>
    internal static Texture2D? GetFlagTexture(string countryId)
    {
        // Bereits im Cache?
        if (_flagTextures.TryGetValue(countryId, out var cached))
        {
            return cached;
        }

        // Versuche Flagge zu laden
        string flagPath = Path.Combine("Data", "Flags", $"{countryId}.png");
        if (File.Exists(flagPath))
        {
            var texture = Raylib.LoadTexture(flagPath);
            _flagTextures[countryId] = texture;
            return texture;
        }

        return null;
    }

    /// <summary>
    /// Laedt ein News-Bild oder gibt es aus dem Cache zurueck
    /// </summary>
    static Texture2D? LoadNewsImage(string imageName)
    {
        if (string.IsNullOrEmpty(imageName))
            return null;

        // Bereits im Cache?
        if (_newsImageCache.TryGetValue(imageName, out var cached))
        {
            return cached;
        }

        // Suchpfade (verschiedene Arbeitsverzeichnisse und Ordner)
        string[] searchPaths =
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")
        };

        // Unterstuetzte Erweiterungen
        string[] extensions = { "", ".jpg", ".png", ".jpeg" };

        // Entferne evtl. vorhandene Extension vom imageName
        string baseName = Path.GetFileNameWithoutExtension(imageName);
        string originalExt = Path.GetExtension(imageName);

        foreach (var basePath in searchPaths)
        {
            // Versuche verschiedene Ordner
            string[] folders = { "", "Data", "Data/Icons", "Data/Icons/News", "Data/News" };

            foreach (var folder in folders)
            {
                foreach (var ext in extensions)
                {
                    // Bei leerem ext, nutze originalen Namen (falls Extension schon enthalten)
                    string fileName = ext == "" ? imageName : baseName + ext;
                    string fullPath = string.IsNullOrEmpty(folder)
                        ? Path.Combine(basePath, fileName)
                        : Path.Combine(basePath, folder, fileName);

                    if (File.Exists(fullPath))
                    {
                        var texture = Raylib.LoadTexture(fullPath);
                        if (texture.Id != 0)
                        {
                            _newsImageCache[imageName] = texture;
                            return texture;
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Laedt ein Leader-Portrait oder gibt es aus dem Cache zurueck
    /// </summary>
    internal static Texture2D? GetLeaderTexture(string leaderName)
    {
        // Bereits im Cache?
        if (_leaderTextures.TryGetValue(leaderName, out var cached))
        {
            return cached;
        }

        // Suchpfade (verschiedene Arbeitsverzeichnisse)
        string[] searchPaths =
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")
        };

        // Versuche Portrait zu laden (verschiedene Formate)
        string[] extensions = { ".jpg", ".png", ".jpeg", ".bmp" };

        foreach (var basePath in searchPaths)
        {
            foreach (var ext in extensions)
            {
                // Im Data/Leaders Ordner suchen
                string leaderFolderPath = Path.Combine(basePath, "Data", "Leaders", $"{leaderName}{ext}");
                if (File.Exists(leaderFolderPath))
                {
                    var texture = Raylib.LoadTexture(leaderFolderPath);
                    // Pruefen ob Textur gueltig geladen wurde (Width > 0)
                    if (texture.Width > 0 && texture.Height > 0)
                    {
                        _leaderTextures[leaderName] = texture;
                        Console.WriteLine($"[Leader] Geladen: {leaderFolderPath}");
                        return texture;
                    }
                    else
                    {
                        Console.WriteLine($"[Leader] Fehler beim Laden: {leaderFolderPath}");
                    }
                }
            }
        }

        Console.WriteLine($"[Leader] Nicht gefunden: {leaderName}");
        return null;
    }

    /// <summary>
    /// Laedt ein Ressourcen-Icon oder gibt es aus dem Cache zurueck
    /// </summary>
    internal static Texture2D? GetResourceIcon(ResourceType type)
    {
        // Bereits im Cache?
        if (_resourceIcons.TryGetValue(type, out var cached))
        {
            return cached;
        }

        // Icon-Dateiname basierend auf Ressourcentyp
        string? iconName = type switch
        {
            ResourceType.Oil => "oil_deposit",
            ResourceType.NaturalGas => "natural_gas",
            ResourceType.Coal => "coal",
            ResourceType.Iron => "iron",
            ResourceType.Copper => "copper",
            ResourceType.Uranium => "uran",
            ResourceType.Food => "food",
            ResourceType.Steel => "steel",
            ResourceType.Electronics => "electronics",
            ResourceType.Machinery => "machinery",
            ResourceType.ConsumerGoods => "consumer_goods",
            ResourceType.Weapons => "weapons",
            ResourceType.Ammunition => "ammunition",
            _ => null
        };

        if (iconName == null) return null;

        string iconPath = Path.Combine("Data", "Icons", $"{iconName}.png");
        if (File.Exists(iconPath))
        {
            var texture = Raylib.LoadTexture(iconPath);
            _resourceIcons[type] = texture;
            return texture;
        }

        return null;
    }

    /// <summary>
    /// Zeichnet ein Ressourcen-Icon
    /// </summary>
    internal static void DrawResourceIcon(ResourceType type, int x, int y, int size)
    {
        var texture = GetResourceIcon(type);
        if (texture == null) return;

        var tex = texture.Value;
        Rectangle source = new(0, 0, tex.Width, tex.Height);
        Rectangle dest = new(x, y, size, size);
        Raylib.DrawTexturePro(tex, source, dest, new Vector2(0, 0), 0, Color.White);
    }

    internal static void DrawResourceIconTinted(ResourceType type, int x, int y, int size, Color tint)
    {
        var texture = GetResourceIcon(type);
        if (texture == null) return;

        var tex = texture.Value;
        Rectangle source = new(0, 0, tex.Width, tex.Height);
        Rectangle dest = new(x, y, size, size);
        Raylib.DrawTexturePro(tex, source, dest, new Vector2(0, 0), 0, tint);
    }

    /// <summary>
    /// Zeichnet eine Flagge mit angegebener Hoehe (Breite wird proportional berechnet)
    /// </summary>
    internal static void DrawFlag(string countryId, int x, int y, int height)
    {
        var texture = GetFlagTexture(countryId);
        if (texture == null) return;

        var tex = texture.Value;
        float scale = (float)height / tex.Height;
        int width = (int)(tex.Width * scale);

        Raylib.DrawTextureEx(tex, new Vector2(x, y), 0, scale, Color.White);
    }

    /// <summary>
    /// Prueft ob ein Leader-Portrait existiert
    /// </summary>
    internal static bool HasLeaderPortrait(string leaderName)
    {
        foreach (var fileName in GetLeaderFileNameVariants(leaderName))
        {
            var texture = GetLeaderTexture(fileName);
            if (texture != null) return true;
        }
        return false;
    }

    /// <summary>
    /// Zeichnet ein Leader-Portrait mit angegebener Hoehe
    /// </summary>
    internal static void DrawLeaderPortrait(string leaderName, int x, int y, int height)
    {
        // Probiere verschiedene Namensformate
        Texture2D? texture = null;
        foreach (var fileName in GetLeaderFileNameVariants(leaderName))
        {
            texture = GetLeaderTexture(fileName);
            if (texture != null) break;
        }

        if (texture == null)
        {
            // Fallback: Platzhalter-Rechteck zeichnen
            Raylib.DrawRectangle(x, y, (int)(height * 0.75f), height, new Color((byte)60, (byte)60, (byte)70, (byte)255));
            Raylib.DrawRectangleLines(x, y, (int)(height * 0.75f), height, ColorPalette.TextGray);
            return;
        }

        var tex = texture.Value;
        float scale = (float)height / tex.Height;
        Raylib.DrawTextureEx(tex, new Vector2(x, y), 0, scale, Color.White);
        // Rahmen um das Portrait
        int width = (int)(tex.Width * scale);
        Raylib.DrawRectangleLines(x, y, width, height, ColorPalette.Accent);
    }

    /// <summary>
    /// Laedt ein Top-Menu Icon oder gibt es aus dem Cache zurueck
    /// </summary>
    static Texture2D? GetTopMenuIcon(TopMenuPanel panel)
    {
        // None hat kein Icon
        if (panel == TopMenuPanel.None) return null;

        // Bereits im Cache?
        if (_topMenuIcons.TryGetValue(panel, out var cached))
        {
            return cached;
        }

        // Icon-Dateiname basierend auf Panel-Typ
        string? iconName = panel switch
        {
            TopMenuPanel.Politics => "politics",
            TopMenuPanel.Trade => "trade",
            TopMenuPanel.Production => "production",
            TopMenuPanel.Research => "research",
            TopMenuPanel.Diplomacy => "diplomacy",
            TopMenuPanel.Budget => "budget",
            TopMenuPanel.Military => "military",
            TopMenuPanel.News => "news",
            TopMenuPanel.Logistics => "logistics",
            _ => null
        };

        if (iconName == null) return null;

        string iconPath = Path.Combine("Data", "Icons", $"{iconName}.png");
        if (File.Exists(iconPath))
        {
            var texture = Raylib.LoadTexture(iconPath);
            _topMenuIcons[panel] = texture;
            return texture;
        }

        return null;
    }

    /// <summary>
    /// Zeichnet ein Top-Menu Icon
    /// </summary>
    static void DrawTopMenuIcon(TopMenuPanel panel, int x, int y, int size, Color tint)
    {
        var texture = GetTopMenuIcon(panel);
        if (texture == null) return;

        var tex = texture.Value;
        Rectangle source = new(0, 0, tex.Width, tex.Height);
        Rectangle dest = new(x, y, size, size);
        Raylib.DrawTexturePro(tex, source, dest, new Vector2(0, 0), 0, tint);
    }

    /// <summary>
    /// Laedt ein Buendnis-Icon oder gibt es aus dem Cache zurueck
    /// </summary>
    static Texture2D? GetAllianceIcon(string alliance)
    {
        // Bereits im Cache?
        if (_allianceIcons.TryGetValue(alliance, out var cached))
        {
            return cached;
        }

        // Icon-Dateiname basierend auf Buendnis
        string? iconName = alliance switch
        {
            "NATO" => "nato",
            "EU" => "eu",
            "BRICS" => "brics",
            "Commonwealth" => "commonwealth",
            "G20" => "g20",
            "USMCA" => "usmca",
            "QUAD" => "quad",
            "US-Allianz" => "us_alliance",
            "SCO" => "sco",
            "OVKS" => "csto",
            _ => null
        };

        if (iconName == null) return null;

        string iconPath = Path.Combine("Data", "Icons", $"{iconName}.png");
        if (File.Exists(iconPath))
        {
            var texture = Raylib.LoadTexture(iconPath);
            _allianceIcons[alliance] = texture;
            return texture;
        }

        return null;
    }

    /// <summary>
    /// Zeichnet ein Buendnis-Icon
    /// </summary>
    internal static void DrawAllianceIcon(string alliance, int x, int y, int size)
    {
        var texture = GetAllianceIcon(alliance);
        if (texture == null) return;

        var tex = texture.Value;
        Rectangle source = new(0, 0, tex.Width, tex.Height);
        Rectangle dest = new(x, y, size, size);
        Raylib.DrawTexturePro(tex, source, dest, new Vector2(0, 0), 0, Color.White);
    }

    /// <summary>
    /// Laedt das Money-Icon fuer die Budget-Anzeige (cached)
    /// </summary>
    static Texture2D? _budgetIconCache = null;
    static bool _budgetIconLoaded = false;
    static Texture2D? GetResourceIcon_Money()
    {
        if (_budgetIconLoaded) return _budgetIconCache;
        _budgetIconLoaded = true;

        string iconPath = Path.Combine("Data", "Icons", "money.png");
        if (File.Exists(iconPath))
        {
            _budgetIconCache = Raylib.LoadTexture(iconPath);
        }
        return _budgetIconCache;
    }

    /// <summary>
    /// Laedt ein Icon aus Data/Icons mit Caching (fuer View-Buttons etc.)
    /// </summary>
    static Texture2D? LoadCachedIcon(ref Texture2D? cache, string iconFile)
    {
        if (cache != null) return cache;
        string iconPath = Path.Combine("Data", "Icons", iconFile);
        if (!File.Exists(iconPath)) return null;
        var tex = Raylib.LoadTexture(iconPath);
        if (tex.Id == 0) return null;
        Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
        cache = tex;
        return cache;
    }

    /// <summary>
    /// Zeichnet ein View-Button-Icon zentriert im Button
    /// </summary>
    static void DrawViewIcon(Texture2D? icon, int btnX, int btnY, int btnSize, bool active, bool hover)
    {
        if (icon == null) return;
        var tex = icon.Value;
        int iconSize = btnSize - 8;
        float scale = (float)iconSize / Math.Max(tex.Width, tex.Height);
        int drawW = (int)(tex.Width * scale);
        int drawH = (int)(tex.Height * scale);
        int iconX = btnX + (btnSize - drawW) / 2;
        int iconY = btnY + (btnSize - drawH) / 2;
        Rectangle source = new(0, 0, tex.Width, tex.Height);
        Rectangle dest = new(iconX, iconY, drawW, drawH);
        Color tint = active || hover ? Color.White : new Color((byte)200, (byte)200, (byte)200, (byte)255);
        Raylib.DrawTexturePro(tex, source, dest, Vector2.Zero, 0, tint);
    }
}
