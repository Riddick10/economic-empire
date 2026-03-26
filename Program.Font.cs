using Raylib_cs;
using System.Numerics;
using System.Runtime.InteropServices;

namespace GrandStrategyGame;

/// <summary>
/// Program - Font-System mit Umlaut-Unterstützung
/// </summary>
partial class Program
{
    // Custom Font mit Umlaut-Unterstützung
    static Font _gameFont;
    static bool _fontLoaded = false;

    /// <summary>
    /// Lädt den Custom Font mit allen benötigten Zeichen (inkl. Umlaute)
    /// </summary>
    static void LoadGameFont()
    {
        string fontPath = Path.Combine("Data", "Fonts", "VCR_OSD_MONO.ttf");

        // Fallback-Pfade versuchen
        string[] searchPaths = {
            fontPath,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fontPath),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", fontPath)
        };

        string? foundPath = null;
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                foundPath = path;
                break;
            }
        }

        if (foundPath == null)
        {
            Console.WriteLine("[Font] WARNUNG: VCR_OSD_MONO.ttf nicht gefunden!");
            Console.WriteLine("[Font] Verwende Standard-Font (ohne Umlaute)");
            _fontLoaded = false;
            return;
        }

        // Unicode-Codepoints für alle benötigten Zeichen
        // ASCII (32-126) + Deutsche Umlaute + weitere Sonderzeichen
        var codepoints = new List<int>();

        // Standard ASCII (Space bis Tilde)
        for (int i = 32; i <= 126; i++)
        {
            codepoints.Add(i);
        }

        // Deutsche Sonderzeichen
        codepoints.Add(196);  // Ä
        codepoints.Add(214);  // Ö
        codepoints.Add(220);  // Ü
        codepoints.Add(228);  // ä
        codepoints.Add(246);  // ö
        codepoints.Add(252);  // ü
        codepoints.Add(223);  // ß

        // Weitere nützliche Zeichen
        codepoints.Add(8364); // € (Euro)
        codepoints.Add(176);  // ° (Grad)
        codepoints.Add(177);  // ± (Plus-Minus)
        codepoints.Add(8211); // – (Gedankenstrich)
        codepoints.Add(8212); // — (Langer Strich)
        codepoints.Add(171);  // «
        codepoints.Add(187);  // »
        codepoints.Add(8230); // … (Ellipse)

        // Font mit 48px Basisgröße laden (wird dann skaliert)
        int[] codepointArray = codepoints.ToArray();

        // String zu sbyte* marshalen für Raylib-cs API
        IntPtr fileNamePtr = Marshal.StringToHGlobalAnsi(foundPath);
        try
        {
            unsafe
            {
                fixed (int* codepointsPtr = codepointArray)
                {
                    _gameFont = Raylib.LoadFontEx((sbyte*)fileNamePtr, 48, codepointsPtr, codepointArray.Length);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(fileNamePtr);
        }

        // Point-Filter für scharfe Pixel-Schrift (VCR OSD Mono Retro-Look)
        Raylib.SetTextureFilter(_gameFont.Texture, TextureFilter.Point);

        _fontLoaded = true;
        Console.WriteLine($"[Font] Geladen: {foundPath} ({codepointArray.Length} Zeichen)");
    }

    /// <summary>
    /// Gibt Font-Ressourcen frei
    /// </summary>
    static void UnloadGameFont()
    {
        if (_fontLoaded)
        {
            Raylib.UnloadFont(_gameFont);
            _fontLoaded = false;
        }
    }

    /// <summary>
    /// Zeichnet Text mit dem Custom Font (oder Fallback auf Standard)
    /// </summary>
    public static void DrawGameText(string text, int x, int y, int fontSize, Color color)
    {
        if (_fontLoaded)
        {
            // Custom Font verwenden
            float scale = fontSize / 48f; // Basis-Größe ist 48
            Raylib.DrawTextEx(_gameFont, text, new Vector2(x, y), fontSize, scale, color);
        }
        else
        {
            // Fallback auf Standard-Font (ohne Umlaute)
            Raylib.DrawText(text, x, y, fontSize, color);
        }
    }

    /// <summary>
    /// Misst die Breite eines Textes mit dem Custom Font
    /// </summary>
    public static int MeasureGameText(string text, int fontSize)
    {
        if (_fontLoaded)
        {
            float scale = fontSize / 48f;
            Vector2 size = Raylib.MeasureTextEx(_gameFont, text, fontSize, scale);
            return (int)size.X;
        }
        else
        {
            return Raylib.MeasureText(text, fontSize);
        }
    }

    /// <summary>
    /// Zeichnet Text mit dem Custom Font und Position als Vector2
    /// </summary>
    public static void DrawGameText(string text, Vector2 position, int fontSize, Color color)
    {
        DrawGameText(text, (int)position.X, (int)position.Y, fontSize, color);
    }
}
