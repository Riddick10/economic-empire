using System.Numerics;
using Raylib_cs;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Mehrstufiges Wolken-System mit Zoom-abhaengiger Darstellung
///
/// Weit rausgezoomt: Grosse Wolken-Cluster (Macro-Ebene)
/// Mittlerer Zoom:   Detaillierte Wolken-Textur (Detail-Ebene)
/// Weit reingezoomt:  Wolken verschwinden langsam
///
/// Uebergang zwischen Macro und Detail ist ein sanfter Cross-Fade
/// </summary>
public partial class WorldMap
{
    // Macro-Wolken (grosse Cluster, sichtbar weit rausgezoomt)
    private Texture2D[] _cloudMacroTextures = Array.Empty<Texture2D>();
    // Detail-Wolken (aktuelle Wolkentextur, sichtbar bei mittlerem Zoom)
    private Texture2D[] _cloudDetailTextures = Array.Empty<Texture2D>();
    private bool _cloudsLoaded = false;
    private bool _cloudsInitAttempted = false;

    // Wolken-Einstellungen
    public bool ShowClouds { get; set; } = true;
    public float CloudGameTime { get; set; }

    // Zoom-Schwellen fuer die Uebergaenge
    private const float MACRO_FULL = 1.5f;    // Macro voll sichtbar
    private const float MACRO_FADE_END = 4.0f; // Macro komplett ausgeblendet
    private const float DETAIL_FADE_IN = 2.5f; // Detail beginnt einzublenden
    private const float DETAIL_FULL = 4.5f;    // Detail voll sichtbar
    private const float DETAIL_FADE_OUT = 12.0f; // Detail beginnt auszublenden
    private const float DETAIL_GONE = 18.0f;   // Detail komplett weg

    // Macro-Wolken: Alpha und Scroll-Speed (nur 1 Textur: NASA Satellitenbild)
    private const float MACRO_BASE_ALPHA = 0.55f;
    private const float MACRO_SCROLL_SPEED_X = 0.0003f;
    private const float MACRO_SCROLL_SPEED_Y = 0.0001f;

    // Detail-Wolken Konfiguration (aktuelle Wolken bei mittlerem Zoom)
    private static readonly CloudLayerConfig[] DetailLayers = {
        // Kleine Cumulus-Wolken
        new(NoiseScale: 2.5f, Octaves: 5, SpeedX: 0.0012f, SpeedY: -0.0002f, BaseAlpha: 0.25f, Threshold: 0.35f),
        // Mittlere Cumulus: Hauptschicht
        new(NoiseScale: 5.0f, Octaves: 3, SpeedX: 0.0010f, SpeedY: -0.0003f, BaseAlpha: 0.35f, Threshold: 0.30f),
        // Grosse Wolkenfelder
        new(NoiseScale: 10.0f, Octaves: 2, SpeedX: 0.0008f, SpeedY: 0.0001f, BaseAlpha: 0.22f, Threshold: 0.32f),
    };

    private record struct CloudLayerConfig(
        float NoiseScale, int Octaves,
        float SpeedX, float SpeedY,
        float BaseAlpha, float Threshold);

    // =========================================================================
    //  TEXTUR-GENERIERUNG
    // =========================================================================

    /// <summary>
    /// Laedt die NASA Blue Marble Wolkentextur und konvertiert sie:
    /// Weiss auf Schwarz → Weiss auf Transparent (Alpha = Helligkeit)
    /// </summary>
    private static Texture2D LoadNasaCloudTexture()
    {
        string[] searchPaths = {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Terrain", "clouds_macro.jpg"),
            Path.Combine("Data", "Terrain", "clouds_macro.jpg"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data", "Terrain", "clouds_macro.jpg"),
        };

        string? foundPath = null;
        foreach (var p in searchPaths)
        {
            if (File.Exists(p)) { foundPath = p; break; }
        }

        if (foundPath == null)
        {
            Console.WriteLine("[Clouds] clouds_macro.jpg nicht gefunden");
            return default;
        }

        // Bild laden
        Image img = Raylib.LoadImage(foundPath);
        if (img.Width == 0 || img.Height == 0) return default;

        // Konvertieren: Helligkeit → Alpha (weiss bleibt weiss, schwarz wird transparent)
        unsafe
        {
            // Sicherstellen dass das Bild RGBA ist
            Raylib.ImageFormat(ref img, PixelFormat.UncompressedR8G8B8A8);

            Color* pixels = (Color*)img.Data;
            int total = img.Width * img.Height;
            for (int i = 0; i < total; i++)
            {
                // Helligkeit als Alpha verwenden
                byte brightness = (byte)((pixels[i].R + pixels[i].G + pixels[i].B) / 3);
                pixels[i] = new Color((byte)255, (byte)255, (byte)255, brightness);
            }
        }

        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);

        if (tex.Id != 0)
        {
            Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
            Raylib.SetTextureWrap(tex, TextureWrap.Repeat);
        }

        Console.WriteLine($"[Clouds] NASA Textur geladen: {tex.Width}x{tex.Height}");
        return tex;
    }

    private static Texture2D GenerateCloudTexture(CloudLayerConfig cfg, int texW, int texH,
        float seedX, float seedY, float seedZ, bool useClustering)
    {
        Image img = Raylib.GenImageColor(texW, texH, Color.Blank);

        unsafe
        {
            Color* pixels = (Color*)img.Data;

            for (int py = 0; py < texH; py++)
            {
                float v = (float)py / texH;

                for (int px = 0; px < texW; px++)
                {
                    float u = (float)px / texW;

                    // Torus-Mapping fuer nahtloses horizontales Tiling
                    float angle = u * MathF.PI * 2f;
                    float r = cfg.NoiseScale;
                    float nx = MathF.Cos(angle) * r;
                    float nz = MathF.Sin(angle) * r;
                    float ny = v * r * 2f;

                    // Sanftes Domain-Warping
                    float warpX = CloudNoise.FractalNoise(
                        nx + seedX + 5.2f, ny + seedY + 1.3f, nz + seedZ,
                        2, 0.5f, 2.0f) * 0.3f;
                    float warpY = CloudNoise.FractalNoise(
                        nx + seedX + 9.7f, ny + seedY + 4.8f, nz + seedZ + 3.1f,
                        2, 0.5f, 2.0f) * 0.3f;

                    // Billowy Noise: runde puffige Formen
                    float detail = CloudNoise.BillowNoise(
                        nx + seedX + warpX, ny + seedY + warpY, nz + seedZ,
                        cfg.Octaves, 0.5f, 2.0f);

                    float density;

                    if (useClustering)
                    {
                        // Detail-Wolken: mit Clustering-Maske
                        float groups = CloudNoise.BillowNoise(
                            nx * 0.4f + seedX + 47.3f, ny * 0.4f + seedY + 23.1f, nz * 0.4f + seedZ + 11.7f,
                            2, 0.5f, 2.0f);
                        density = detail * groups;
                    }
                    else
                    {
                        // Macro-Wolken: direkt, grosse Formen
                        density = detail;
                    }

                    // Schwellenwert
                    density = Math.Clamp((density - cfg.Threshold) / (1f - cfg.Threshold), 0f, 1f);

                    // Doppelter Smoothstep: scharfe saubere Raender
                    density = density * density * (3f - 2f * density);
                    density = density * density * (3f - 2f * density);

                    // Polregionen abdunkeln
                    float polarFade = 1f - MathF.Pow(MathF.Abs(v - 0.5f) * 2f, 3f);
                    density *= polarFade;

                    byte alpha = (byte)(density * 255);
                    pixels[py * texW + px] = new Color((byte)255, (byte)255, (byte)255, alpha);
                }
            }
        }

        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);

        if (tex.Id != 0)
        {
            Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
            Raylib.SetTextureWrap(tex, TextureWrap.Repeat);
        }

        return tex;
    }

    private void GenerateCloudTextures()
    {
        try
        {
            Console.WriteLine("[Clouds] Generiere Wolken-Texturen...");

            // Macro-Wolken: NASA Blue Marble Satelliten-Wolkentextur laden
            _cloudMacroTextures = new Texture2D[1];
            _cloudMacroTextures[0] = LoadNasaCloudTexture();
            if (_cloudMacroTextures[0].Id != 0)
                Console.WriteLine("[Clouds] NASA Macro-Wolkentextur geladen");
            else
                Console.WriteLine("[Clouds] NASA Textur nicht gefunden, Macro-Wolken deaktiviert");

            // Detail-Wolken (hoehere Aufloesung fuer Nahansicht)
            _cloudDetailTextures = new Texture2D[DetailLayers.Length];
            for (int i = 0; i < DetailLayers.Length; i++)
            {
                float seedX = i * 347.3f + 31.1f + MathF.Sin(i * 7.13f) * 200f;
                float seedY = i * 521.7f + 67.9f + MathF.Cos(i * 11.37f) * 200f;
                float seedZ = i * 193.9f + 149.3f + MathF.Sin(i * 3.71f) * 200f;
                _cloudDetailTextures[i] = GenerateCloudTexture(DetailLayers[i], 2048, 1024, seedX, seedY, seedZ, true);
                Console.WriteLine($"[Clouds] Detail-Schicht {i} generiert");
            }

            _cloudsLoaded = true;
            Console.WriteLine("[Clouds] Wolken-System aktiviert (Macro + Detail)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Clouds] Fehler bei Generierung: {ex.Message}");
            _cloudsLoaded = false;
        }
    }

    // =========================================================================
    //  RENDERING: Zoom-abhaengiger Cross-Fade zwischen Macro und Detail
    // =========================================================================

    private void DrawCloudOverlay()
    {
        if (!_cloudsLoaded || !ShowClouds) return;

        // Macro-Sichtbarkeit: voll bei niedrigem Zoom, ausblenden ab MACRO_FADE_END
        float macroAlpha = 1.0f;
        if (Zoom > MACRO_FULL)
            macroAlpha = Math.Clamp(1.0f - (Zoom - MACRO_FULL) / (MACRO_FADE_END - MACRO_FULL), 0f, 1f);

        // Detail-Sichtbarkeit: einblenden ab DETAIL_FADE_IN, ausblenden ab DETAIL_FADE_OUT
        float detailAlpha = 0.0f;
        if (Zoom >= DETAIL_FADE_IN && Zoom < DETAIL_GONE)
        {
            if (Zoom < DETAIL_FULL)
                detailAlpha = (Zoom - DETAIL_FADE_IN) / (DETAIL_FULL - DETAIL_FADE_IN);
            else if (Zoom < DETAIL_FADE_OUT)
                detailAlpha = 1.0f;
            else
                detailAlpha = 1.0f - (Zoom - DETAIL_FADE_OUT) / (DETAIL_GONE - DETAIL_FADE_OUT);
            detailAlpha = Math.Clamp(detailAlpha, 0f, 1f);
        }

        if (macroAlpha <= 0.01f && detailAlpha <= 0.01f) return;

        // Karte auf dem Bildschirm
        float destX = Offset.X;
        float destY = Offset.Y;
        float destW = MAP_WIDTH * Zoom;
        float destH = MAP_HEIGHT * Zoom;
        Rectangle dest = new(destX, destY, destW, destH);

        // === MACRO-WOLKEN zeichnen (NASA Satellitentextur) ===
        if (macroAlpha > 0.01f && _cloudMacroTextures.Length > 0 && _cloudMacroTextures[0].Id != 0)
        {
            int texW = _cloudMacroTextures[0].Width;
            int texH = _cloudMacroTextures[0].Height;

            float offsetX = CloudGameTime * MACRO_SCROLL_SPEED_X * texW;
            float offsetY = CloudGameTime * MACRO_SCROLL_SPEED_Y * texH;
            Rectangle source = new(offsetX % texW, offsetY % texH, texW, texH);

            byte alpha = (byte)(MACRO_BASE_ALPHA * 255 * macroAlpha);
            if (alpha >= 2)
            {
                Raylib.DrawTexturePro(_cloudMacroTextures[0], source, dest, Vector2.Zero, 0,
                    new Color((byte)255, (byte)255, (byte)255, alpha));
            }
        }

        // === DETAIL-WOLKEN zeichnen ===
        if (detailAlpha > 0.01f)
        {
            for (int i = 0; i < _cloudDetailTextures.Length && i < DetailLayers.Length; i++)
            {
                if (_cloudDetailTextures[i].Id == 0) continue;
                var cfg = DetailLayers[i];
                int texW = _cloudDetailTextures[i].Width;
                int texH = _cloudDetailTextures[i].Height;

                float offsetX = CloudGameTime * cfg.SpeedX * texW;
                float offsetY = CloudGameTime * cfg.SpeedY * texH;
                Rectangle source = new(offsetX % texW, offsetY % texH, texW, texH);

                byte alpha = (byte)(cfg.BaseAlpha * 255 * detailAlpha);
                if (alpha < 2) continue;

                Raylib.DrawTexturePro(_cloudDetailTextures[i], source, dest, Vector2.Zero, 0,
                    new Color((byte)255, (byte)255, (byte)255, alpha));
            }
        }
    }

    // =========================================================================
    //  CLEANUP
    // =========================================================================

    public void UnloadCloudTextures()
    {
        try
        {
            for (int i = 0; i < _cloudMacroTextures.Length; i++)
                if (_cloudMacroTextures[i].Id != 0) Raylib.UnloadTexture(_cloudMacroTextures[i]);
            for (int i = 0; i < _cloudDetailTextures.Length; i++)
                if (_cloudDetailTextures[i].Id != 0) Raylib.UnloadTexture(_cloudDetailTextures[i]);

            _cloudMacroTextures = Array.Empty<Texture2D>();
            _cloudDetailTextures = Array.Empty<Texture2D>();
            _cloudsLoaded = false;
            Console.WriteLine("[Clouds] Wolken-Texturen freigegeben");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Clouds] Fehler beim Entladen: {ex.Message}");
        }
    }

    // =========================================================================
    //  3D PERLIN NOISE + BILLOWY NOISE
    // =========================================================================

    private static class CloudNoise
    {
        private static readonly int[] P = {
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,
            140,36,103,30,69,142,8,99,37,240,21,10,23,190,6,148,
            247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,
            57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,
            74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,
            60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,
            65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,
            200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,
            52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,
            207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,
            119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,
            129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,
            218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,
            81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,
            184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,
            222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };

        private static readonly int[] Perm;

        static CloudNoise()
        {
            Perm = new int[512];
            for (int i = 0; i < 512; i++)
                Perm[i] = P[i & 255];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private static float Lerp(float t, float a, float b) => a + t * (b - a);

        private static float Grad3(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public static float Noise3D(float x, float y, float z)
        {
            int xi = (int)MathF.Floor(x) & 255;
            int yi = (int)MathF.Floor(y) & 255;
            int zi = (int)MathF.Floor(z) & 255;

            float xf = x - MathF.Floor(x);
            float yf = y - MathF.Floor(y);
            float zf = z - MathF.Floor(z);

            float u = Fade(xf);
            float v = Fade(yf);
            float w = Fade(zf);

            int a  = Perm[xi] + yi;
            int aa = Perm[a] + zi;
            int ab = Perm[a + 1] + zi;
            int b  = Perm[xi + 1] + yi;
            int ba = Perm[b] + zi;
            int bb = Perm[b + 1] + zi;

            return Lerp(w,
                Lerp(v,
                    Lerp(u, Grad3(Perm[aa], xf, yf, zf), Grad3(Perm[ba], xf - 1, yf, zf)),
                    Lerp(u, Grad3(Perm[ab], xf, yf - 1, zf), Grad3(Perm[bb], xf - 1, yf - 1, zf))),
                Lerp(v,
                    Lerp(u, Grad3(Perm[aa + 1], xf, yf, zf - 1), Grad3(Perm[ba + 1], xf - 1, yf, zf - 1)),
                    Lerp(u, Grad3(Perm[ab + 1], xf, yf - 1, zf - 1), Grad3(Perm[bb + 1], xf - 1, yf - 1, zf - 1))));
        }

        public static float FractalNoise(float x, float y, float z, int octaves, float persistence, float lacunarity)
        {
            float total = 0f, amplitude = 1f, frequency = 1f, maxValue = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += Noise3D(x * frequency, y * frequency, z * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            return total / maxValue;
        }

        public static float BillowNoise(float x, float y, float z, int octaves, float persistence, float lacunarity)
        {
            float total = 0f, amplitude = 1f, frequency = 1f, maxValue = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = Noise3D(x * frequency, y * frequency, z * frequency);
                n = 1f - MathF.Abs(n);
                n *= n;
                total += n * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            return total / maxValue;
        }
    }
}
