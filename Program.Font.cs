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

    // Zweiter, glatter Font (DejaVu Sans Bold) nur fuer die Laendernamen auf der Karte
    static Font _labelFont;
    static bool _labelFontLoaded = false;

    // Backing-Aufloesung des Laender-Namen-Atlas (hoch, damit auch grosse
    // Laendernamen bis ~240px scharf sind).
    private const int FontAtlasSize = 200;

    // Backing-Aufloesung des UI-Fonts. Der UI-Text wird nur mit 11-60px gezeichnet
    // (Titel = 60px ist das Maximum). Ein 64px-Atlas rendert den Titel quasi 1:1
    // und verkleinert den haeufigen 14px-Text nur ~4.6x statt ~14x -> deutlich
    // schaerfer. WICHTIG: keine Mipmaps (verursachen Unschaerfe + Glyphen-"Bluten"
    // als vertikale Linien), stattdessen Bilinear.
    private const int GameFontAtlasSize = 64;

    /// <summary>
    /// Lädt den Custom Font mit allen benötigten Zeichen (inkl. Umlaute)
    /// </summary>
    static void LoadGameFont()
    {
        string fontPath = Path.Combine("Data", "Fonts", "Inter-SemiBold.ttf");

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
            Console.WriteLine("[Font] WARNUNG: Inter-SemiBold.ttf nicht gefunden!");
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

        // Font-Atlas in hoher Aufloesung backen: kleine Texte (Leisten, 17px) werden
        // daraus scharf verkleinert, grosse (Laendernamen bis ~240px) bleiben scharf,
        // statt aus einem 96px-Atlas klobig hochskaliert zu werden.
        int[] codepointArray = codepoints.ToArray();

        // String zu sbyte* marshalen für Raylib-cs API
        IntPtr fileNamePtr = Marshal.StringToHGlobalAnsi(foundPath);
        try
        {
            unsafe
            {
                fixed (int* codepointsPtr = codepointArray)
                {
                    _gameFont = Raylib.LoadFontEx((sbyte*)fileNamePtr, GameFontAtlasSize, codepointsPtr, codepointArray.Length);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(fileNamePtr);
        }

        // Bilinear ohne Mipmaps: scharfe, glatte Schrift. (Mipmaps verursachten
        // Unschaerfe und vertikale Linien durch Glyphen-Bluten - daher entfernt.)
        Raylib.SetTextureFilter(_gameFont.Texture, TextureFilter.Bilinear);

        _fontLoaded = true;

        // Diagnose: pruefen ob die Umlaut-Glyphen wirklich geladen wurden
        // (GetGlyphIndex liefert den Index des Fallback-Zeichens, wenn ein Codepoint fehlt)
        int fallbackIndex = Raylib.GetGlyphIndex(_gameFont, '?');
        string missing = "";
        foreach (char c in "ÄÖÜäöüß")
        {
            if (Raylib.GetGlyphIndex(_gameFont, c) == fallbackIndex)
                missing += c;
        }
        Console.WriteLine(missing.Length == 0
            ? $"[Font] Geladen: {foundPath} ({codepointArray.Length} Zeichen, Umlaute OK)"
            : $"[Font] Geladen: {foundPath} - WARNUNG: fehlende Glyphen: {missing}");

        // Zweite Schrift fuer die Laendernamen: klarer, glatter Sans-Serif
        // (DejaVu Sans Bold) statt des pixeligen Retro-Fonts der restlichen UI.
        LoadLabelFont(codepointArray);
    }

    /// <summary>
    /// Laedt den glatten Label-Font (DejaVu Sans Bold) fuer die Laendernamen.
    /// </summary>
    static void LoadLabelFont(int[] codepointArray)
    {
        string rel = Path.Combine("Data", "Fonts", "DejaVuSans-Bold.ttf");
        string[] searchPaths = {
            rel,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rel),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", rel)
        };
        string? path = null;
        foreach (var p in searchPaths)
            if (File.Exists(p)) { path = p; break; }

        if (path == null)
        {
            Console.WriteLine("[Font] Label-Font (DejaVuSans-Bold.ttf) nicht gefunden - nutze Retro-Font fuer Laendernamen");
            return;
        }

        IntPtr ptr = Marshal.StringToHGlobalAnsi(path);
        try
        {
            unsafe
            {
                fixed (int* cp = codepointArray)
                {
                    _labelFont = Raylib.LoadFontEx((sbyte*)ptr, FontAtlasSize, cp, codepointArray.Length);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        Raylib.SetTextureFilter(_labelFont.Texture, TextureFilter.Bilinear);
        _labelFontLoaded = _labelFont.Texture.Id != 0;
        Console.WriteLine(_labelFontLoaded
            ? $"[Font] Label-Font geladen: {path}"
            : "[Font] Label-Font konnte nicht geladen werden");
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
        if (_labelFontLoaded)
        {
            Raylib.UnloadFont(_labelFont);
            _labelFontLoaded = false;
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
            float scale = fontSize / 96f; // Basis-Größe ist 48
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
            float scale = fontSize / 96f;
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

    /// <summary>
    /// Zeichnet Text zentriert um einen Punkt, um einen Winkel gedreht und mit
    /// zusaetzlichem Buchstabenabstand (fuer HOI4-artige Laendernamen, die sich
    /// an Form und Ausrichtung des Territoriums anpassen).
    /// </summary>
    /// <param name="letterSpacing">Zusaetzlicher Abstand zwischen Buchstaben (Pixel)</param>
    /// <param name="angleDeg">Drehung in Grad (im Uhrzeigersinn)</param>
    public static void DrawGameTextTransformed(string text, Vector2 center, int fontSize,
        float letterSpacing, float angleDeg, Color color)
    {
        if (!_fontLoaded)
        {
            // Fallback: horizontal, ohne Rotation
            int w = Raylib.MeasureText(text, fontSize);
            Raylib.DrawText(text, (int)(center.X - w / 2f), (int)(center.Y - fontSize / 2f), fontSize, color);
            return;
        }

        // Gesamtmasse inkl. Buchstabenabstand ermitteln, damit wir exakt zentrieren
        Vector2 size = Raylib.MeasureTextEx(_gameFont, text, fontSize, letterSpacing);

        Rlgl.PushMatrix();
        Rlgl.Translatef(center.X, center.Y, 0f);
        Rlgl.Rotatef(angleDeg, 0f, 0f, 1f);
        Raylib.DrawTextEx(_gameFont, text, new Vector2(-size.X / 2f, -size.Y / 2f),
            fontSize, letterSpacing, color);
        Rlgl.PopMatrix();
    }

    /// <summary>
    /// Misst die Textbreite mit dem Custom Font bei gegebenem Buchstabenabstand.
    /// </summary>
    public static float MeasureGameTextSpaced(string text, int fontSize, float letterSpacing)
    {
        if (_fontLoaded)
            return Raylib.MeasureTextEx(_gameFont, text, fontSize, letterSpacing).X;
        return Raylib.MeasureText(text, fontSize);
    }

    /// <summary>
    /// Zeichnet ein einzelnes Zeichen zentriert um einen Punkt, gedreht.
    /// Fuer gekruemmte Laendernamen, bei denen jeder Buchstabe entlang einer
    /// Kurve platziert und tangential gedreht wird (HOI4-Stil).
    /// </summary>
    public static void DrawGameGlyph(string ch, Vector2 center, int fontSize, float angleDeg, Color color)
    {
        if (!_fontLoaded)
        {
            int w = Raylib.MeasureText(ch, fontSize);
            Raylib.DrawText(ch, (int)(center.X - w / 2f), (int)(center.Y - fontSize / 2f), fontSize, color);
            return;
        }

        Vector2 size = Raylib.MeasureTextEx(_gameFont, ch, fontSize, 0f);
        Rlgl.PushMatrix();
        Rlgl.Translatef(center.X, center.Y, 0f);
        Rlgl.Rotatef(angleDeg, 0f, 0f, 1f);
        Raylib.DrawTextEx(_gameFont, ch, new Vector2(-size.X / 2f, -size.Y / 2f), fontSize, 0f, color);
        Rlgl.PopMatrix();
    }

    /// <summary>
    /// Wie DrawGameGlyph, aber mit dem glatten Label-Font (DejaVu Sans Bold)
    /// fuer die Laendernamen. Faellt auf den Retro-Font zurueck falls nicht geladen.
    /// </summary>
    public static void DrawLabelGlyph(string ch, Vector2 center, int fontSize, float angleDeg, Color color)
    {
        if (!_labelFontLoaded) { DrawGameGlyph(ch, center, fontSize, angleDeg, color); return; }

        Vector2 size = Raylib.MeasureTextEx(_labelFont, ch, fontSize, 0f);
        Rlgl.PushMatrix();
        Rlgl.Translatef(center.X, center.Y, 0f);
        Rlgl.Rotatef(angleDeg, 0f, 0f, 1f);
        Raylib.DrawTextEx(_labelFont, ch, new Vector2(-size.X / 2f, -size.Y / 2f), fontSize, 0f, color);
        Rlgl.PopMatrix();
    }

    /// <summary>
    /// Misst die Textbreite mit dem Label-Font (Fallback: Retro-Font).
    /// </summary>
    public static float MeasureLabelText(string text, int fontSize)
    {
        if (_labelFontLoaded)
            return Raylib.MeasureTextEx(_labelFont, text, fontSize, 0f).X;
        return MeasureGameTextSpaced(text, fontSize, 0f);
    }
}
