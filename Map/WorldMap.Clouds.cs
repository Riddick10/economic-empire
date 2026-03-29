using System.Numerics;
using Raylib_cs;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Wolken-System fuer atmosphaerische Kartenvisualisierung
/// Generiert prozedural mehrere Wolkenschichten mit Perlin-Noise
/// und rendert sie als halbtransparentes Overlay mit Parallax-Animation
/// </summary>
public partial class WorldMap
{
    // Wolken-Texturen (3 Schichten fuer Parallax-Effekt)
    private Texture2D[] _cloudTextures = Array.Empty<Texture2D>();
    private bool _cloudsLoaded = false;
    private bool _cloudsInitAttempted = false;

    // Wolken-Einstellungen
    public bool ShowClouds { get; set; } = true;
    public float CloudGameTime { get; set; }

    // Wolkenschicht-Konfiguration
    private static readonly CloudLayerConfig[] CloudLayers = {
        // Hohe Cirrus-Wolken: duenne Schleierwolken, schnell
        new(NoiseScale: 2.2f, Octaves: 3, SpeedX: 0.0022f, SpeedY: 0.0003f, BaseAlpha: 0.20f, Threshold: 0.51f),
        // Mittlere Cumulus-Wolken: Hauptschicht, deutlich sichtbar
        new(NoiseScale: 4.5f, Octaves: 5, SpeedX: 0.0011f, SpeedY: -0.0005f, BaseAlpha: 0.35f, Threshold: 0.48f),
        // Niedrige Stratus-Wolken: grosse langsame Formationen
        new(NoiseScale: 7.0f, Octaves: 5, SpeedX: 0.0006f, SpeedY: 0.0004f, BaseAlpha: 0.25f, Threshold: 0.54f),
    };

    private record struct CloudLayerConfig(
        float NoiseScale, int Octaves,
        float SpeedX, float SpeedY,
        float BaseAlpha, float Threshold);

    /// <summary>
    /// Generiert die Wolken-Texturen prozedural mit Perlin-Noise
    /// </summary>
    private void GenerateCloudTextures()
    {
        try
        {
            Console.WriteLine("[Clouds] Generiere Wolken-Texturen...");

            int texW = 512;
            int texH = 256;
            _cloudTextures = new Texture2D[CloudLayers.Length];

            for (int layer = 0; layer < CloudLayers.Length; layer++)
            {
                var cfg = CloudLayers[layer];

                // Seed pro Schicht stark variieren fuer unterschiedliche Muster
                float seedX = layer * 173.7f + 31.1f;
                float seedY = layer * 251.3f + 67.9f;
                float seedZ = layer * 89.1f + 149.3f;

                // CPU-seitiges Image erstellen
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

                            // Domain-Warping: Noise verzerrt die Koordinaten fuer unregelmaessigere Formen
                            float warpStrength = 1.2f;
                            float warpX = CloudNoise.FractalNoise(
                                nx + seedX + 5.2f, ny + seedY + 1.3f, nz + seedZ,
                                2, 0.5f, 2.0f) * warpStrength;
                            float warpY = CloudNoise.FractalNoise(
                                nx + seedX + 9.7f, ny + seedY + 4.8f, nz + seedZ + 3.1f,
                                2, 0.5f, 2.0f) * warpStrength;

                            // Fraktales Noise mit verzerrten Koordinaten
                            float noise = CloudNoise.FractalNoise(
                                nx + seedX + warpX, ny + seedY + warpY, nz + seedZ,
                                cfg.Octaves, 0.5f, 2.0f);

                            // Normalisieren [-1,1] -> [0,1]
                            float density = (noise + 1f) * 0.5f;

                            // Schwellenwert anwenden (erzeugt klare Luecken zwischen Wolken)
                            density = Math.Clamp((density - cfg.Threshold) / (1f - cfg.Threshold), 0f, 1f);

                            // Steilere Kurve: dichter Kern, duenne Raender
                            density = MathF.Pow(density, 1.5f);

                            // Polregionen abdunkeln (weniger Wolken an den Raendern)
                            float polarFade = 1f - MathF.Pow(MathF.Abs(v - 0.5f) * 2f, 3f);
                            density *= polarFade;

                            byte alpha = (byte)(density * 255);
                            pixels[py * texW + px] = new Color((byte)255, (byte)255, (byte)255, alpha);
                        }
                    }
                }

                // Zur GPU-Textur konvertieren
                _cloudTextures[layer] = Raylib.LoadTextureFromImage(img);
                Raylib.UnloadImage(img);

                if (_cloudTextures[layer].Id != 0)
                {
                    Raylib.SetTextureFilter(_cloudTextures[layer], TextureFilter.Bilinear);
                    Raylib.SetTextureWrap(_cloudTextures[layer], TextureWrap.Repeat);
                }

                Console.WriteLine($"[Clouds] Schicht {layer} generiert ({texW}x{texH})");
            }

            _cloudsLoaded = true;
            Console.WriteLine("[Clouds] Wolken-System aktiviert (3 Schichten)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Clouds] Fehler bei Generierung: {ex.Message}");
            _cloudsLoaded = false;
        }
    }

    /// <summary>
    /// Zeichnet alle Wolkenschichten als animiertes Overlay
    /// Muss NACH Terrain-Overlay aber VOR Grenzen aufgerufen werden
    /// </summary>
    private void DrawCloudOverlay()
    {
        if (!_cloudsLoaded || !ShowClouds) return;

        // Bei hohem Zoom ausblenden (Provinz-Ebene)
        float zoomFade = 1.0f;
        if (Zoom >= 6.0f)
            zoomFade = Math.Clamp(1.0f - (Zoom - 6.0f) / 4.0f, 0.0f, 1.0f);
        if (zoomFade <= 0.01f) return;

        // Bei sehr niedrigem Zoom leicht reduzieren
        if (Zoom < 1.0f)
            zoomFade *= Math.Clamp(Zoom / 1.0f, 0.3f, 1.0f);

        // Ziel-Rechteck (Karte auf Bildschirm)
        float destX = Offset.X;
        float destY = Offset.Y;
        float destW = MAP_WIDTH * Zoom;
        float destH = MAP_HEIGHT * Zoom;
        Rectangle dest = new(destX, destY, destW, destH);

        for (int i = 0; i < _cloudTextures.Length && i < CloudLayers.Length; i++)
        {
            if (_cloudTextures[i].Id == 0) continue;

            var cfg = CloudLayers[i];
            int texW = _cloudTextures[i].Width;
            int texH = _cloudTextures[i].Height;

            // Animation: Offset basierend auf Spielzeit
            float offsetX = CloudGameTime * cfg.SpeedX * texW;
            float offsetY = CloudGameTime * cfg.SpeedY * texH;

            // Source-Rechteck mit Offset (TextureWrap.Repeat sorgt fuer Tiling)
            Rectangle source = new(offsetX % texW, offsetY % texH, texW, texH);

            // Alpha berechnen
            byte alpha = (byte)(cfg.BaseAlpha * 255 * zoomFade);
            if (alpha < 2) continue;

            Color tint = new((byte)255, (byte)255, (byte)255, alpha);

            Raylib.DrawTexturePro(_cloudTextures[i], source, dest, Vector2.Zero, 0, tint);
        }
    }

    /// <summary>
    /// Gibt die Wolken-Texturen frei
    /// </summary>
    public void UnloadCloudTextures()
    {
        try
        {
            for (int i = 0; i < _cloudTextures.Length; i++)
            {
                if (_cloudTextures[i].Id != 0)
                {
                    Raylib.UnloadTexture(_cloudTextures[i]);
                }
            }
            _cloudTextures = Array.Empty<Texture2D>();
            _cloudsLoaded = false;
            Console.WriteLine("[Clouds] Wolken-Texturen freigegeben");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Clouds] Fehler beim Entladen: {ex.Message}");
        }
    }

    /// <summary>
    /// 3D Perlin-Noise Implementierung fuer nahtlose Wolken-Texturen
    /// </summary>
    private static class CloudNoise
    {
        // Permutationstabelle (Ken Perlin Standard)
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

        // Verdoppelte Tabelle (vermeidet Modulo-Operationen)
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

        /// <summary>
        /// 3D Perlin-Noise [-1, 1]
        /// </summary>
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

        /// <summary>
        /// Fraktales Noise (mehrere Oktaven fuer natuerliche Wolkenformen)
        /// </summary>
        public static float FractalNoise(float x, float y, float z, int octaves, float persistence, float lacunarity)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += Noise3D(x * frequency, y * frequency, z * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return total / maxValue;
        }
    }
}
