using GrandStrategyGame.Data;
using GrandStrategyGame.Models;
using Raylib_cs;

namespace GrandStrategyGame;

/// <summary>
/// Zentraler Manager fuer alle Texturen im Spiel.
/// Verwaltet Laden, Cachen und Freigeben von Texturen.
/// </summary>
public static class TextureManager
{
    // Verschiedene Textur-Caches nach Kategorie
    private static readonly Dictionary<string, Texture2D> _flags = new();
    private static readonly Dictionary<string, Texture2D> _leaders = new();
    private static readonly Dictionary<ResourceType, Texture2D> _resources = new();
    private static readonly Dictionary<UnitType, Texture2D> _militaryUnits = new();

    // Einzelne Texturen
    private static Texture2D? _mapViewIcon;
    private static Texture2D? _resourceViewIcon;
    private static Texture2D? _tradeViewIcon;
    private static Texture2D? _newsIcon;
    private static Texture2D? _blastIcon;
    private static Texture2D? _menuBackground;

    private static string? _basePath;

    /// <summary>
    /// Basis-Pfad fuer alle Daten-Dateien
    /// </summary>
    public static string BasePath => _basePath ??= CountryDataLoader.FindBasePath();

    // === FLAGGEN ===

    /// <summary>
    /// Laedt oder gibt gecachte Flagge zurueck
    /// </summary>
    public static Texture2D? GetFlag(string countryId)
    {
        if (string.IsNullOrEmpty(countryId))
            return null;

        if (_flags.TryGetValue(countryId, out var cached))
            return cached;

        string path = Path.Combine(BasePath, "Data", "Flags", $"{countryId}.png");
        return LoadAndCache(_flags, countryId, path);
    }

    // === LEADER PORTRAITS ===

    /// <summary>
    /// Laedt oder gibt gecachtes Leader-Portrait zurueck
    /// </summary>
    public static Texture2D? GetLeader(string leaderName)
    {
        if (string.IsNullOrEmpty(leaderName))
            return null;

        if (_leaders.TryGetValue(leaderName, out var cached))
            return cached;

        string safeName = SanitizeFilename(leaderName);

        // Versuche relative Pfade (wie GetFlagTexture) dann absolute Pfade
        string relativePath = Path.Combine("Data", "Portraits", "Leaders", $"{safeName}.png");
        string absolutePath = Path.Combine(BasePath, "Data", "Portraits", "Leaders", $"{safeName}.png");

        string path = relativePath;
        if (!File.Exists(path))
        {
            path = absolutePath;
        }
        return LoadAndCache(_leaders, leaderName, path);
    }

    // === RESSOURCEN ICONS ===

    /// <summary>
    /// Laedt oder gibt gecachtes Ressourcen-Icon zurueck
    /// </summary>
    public static Texture2D? GetResource(ResourceType type)
    {
        if (_resources.TryGetValue(type, out var cached))
            return cached;

        string filename = GetResourceIconName(type);
        string path = Path.Combine(BasePath, "Data", "Icons", filename);
        var texture = LoadAndCache(_resources, type, path);
        return texture;
    }

    private static string GetResourceIconName(ResourceType type)
    {
        return type switch
        {
            ResourceType.Oil => "oil.png",
            ResourceType.Coal => "coal.png",
            ResourceType.Iron => "iron.png",
            ResourceType.Steel => "steel.png",
            ResourceType.Uranium => "uran.png",
            ResourceType.Copper => "copper.png",
            ResourceType.NaturalGas => "natural_gas.png",
            ResourceType.Food => "food.png",
            ResourceType.ConsumerGoods => "consumer_goods.png",
            ResourceType.Electronics => "electronics.png",
            ResourceType.Machinery => "machinery.png",
            ResourceType.Weapons => "weapons.png",
            ResourceType.Ammunition => "ammunition.png",
            _ => "default.png"
        };
    }

    // === MILITARY UNIT ICONS ===

    /// <summary>
    /// Laedt oder gibt gecachtes Militaereinheit-Icon zurueck
    /// </summary>
    public static Texture2D? GetMilitaryUnit(UnitType type)
    {
        if (_militaryUnits.TryGetValue(type, out var cached))
            return cached;

        string filename = MilitaryUnit.GetIconName(type);
        string path = Path.Combine(BasePath, "Data", "Icons", "Military", filename);
        return LoadAndCache(_militaryUnits, type, path);
    }

    // === VIEW ICONS ===

    public static Texture2D? MapViewIcon => _mapViewIcon ??= LoadSingle(
        Path.Combine(BasePath, "Data", "Icons", "map_view.png"));

    public static Texture2D? ResourceViewIcon => _resourceViewIcon ??= LoadSingle(
        Path.Combine(BasePath, "Data", "Icons", "resource_view.png"));

    public static Texture2D? TradeViewIcon => _tradeViewIcon ??= LoadSingle(
        Path.Combine(BasePath, "Data", "Icons", "trade_view.png"));

    public static Texture2D? NewsIcon => _newsIcon ??= LoadSingle(
        Path.Combine(BasePath, "Data", "Icons", "news.png"));

    public static Texture2D? BlastIcon => _blastIcon ??= LoadSingle(
        Path.Combine(BasePath, "Data", "Icons", "blast.png"));

    // === MENU BACKGROUND ===

    public static Texture2D? MenuBackground => _menuBackground ??= LoadSingle(
        Path.Combine(BasePath, "Data", "menu_background.png"));

    // === HILFSMETHODEN ===

    private static Texture2D? LoadAndCache<TKey>(Dictionary<TKey, Texture2D> cache, TKey key, string path)
        where TKey : notnull
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var texture = Raylib.LoadTexture(path);
            if (texture.Id != 0)
            {
                cache[key] = texture;
                return texture;
            }
        }
        catch
        {
            // Ignoriere Ladefehler
        }

        return null;
    }

    private static Texture2D? LoadSingle(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var texture = Raylib.LoadTexture(path);
            return texture.Id != 0 ? texture : null;
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFilename(string name)
    {
        // Entferne ungueltige Zeichen fuer Dateinamen
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }

    /// <summary>
    /// Gibt alle geladenen Texturen frei
    /// </summary>
    public static void UnloadAll()
    {
        UnloadCache(_flags);
        UnloadCache(_leaders);
        UnloadCache(_resources);
        UnloadCache(_militaryUnits);

        UnloadSingle(ref _mapViewIcon);
        UnloadSingle(ref _resourceViewIcon);
        UnloadSingle(ref _tradeViewIcon);
        UnloadSingle(ref _newsIcon);
        UnloadSingle(ref _blastIcon);
        UnloadSingle(ref _menuBackground);
    }

    private static void UnloadCache<TKey>(Dictionary<TKey, Texture2D> cache) where TKey : notnull
    {
        foreach (var texture in cache.Values)
        {
            Raylib.UnloadTexture(texture);
        }
        cache.Clear();
    }

    private static void UnloadSingle(ref Texture2D? texture)
    {
        if (texture.HasValue)
        {
            Raylib.UnloadTexture(texture.Value);
            texture = null;
        }
    }
}
