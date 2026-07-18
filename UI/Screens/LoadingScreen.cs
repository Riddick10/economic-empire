using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Ladebildschirm - Klassischer Aufbau (Hintergrundbild, zentrierter Titel,
/// Fortschrittsbalken) mit dezentem Feinschliff: weiche Vignette,
/// Glas-Fortschrittsbalken mit Glow und rotierende Gameplay-Tipps.
/// </summary>
internal class LoadingScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.Loading;

    private float _shownProgress; // weich nachgezogener Fortschritt

    private static readonly string[] Tips =
    {
        "Tipp: Mit WASD verschiebst du die Karte, das Mausrad zoomt.",
        "Tipp: F9 speichert dein Spiel blitzschnell.",
        "Tipp: Fabriken brauchen Rohstoffe - sichere deine Lieferketten.",
        "Tipp: Der EU-Binnenmarkt gibt Handelsboni zwischen Mitgliedern.",
        "Tipp: Wahlen gewinnst du mit Stabilitaet und Werbekampagnen.",
        "Tipp: Forschung ist teuer - aber Rueckstand ist teurer.",
        "Tipp: Beobachte die Marktpreise, bevor du exportierst.",
        "Tipp: Ein Krieg ist schnell erklaert und langsam gewonnen.",
        "Tipp: Alle 90 Spieltage wird automatisch gespeichert.",
        "Tipp: Rechtsklick auf ein Land zeigt dir alle Details.",
    };

    public void Enter()
    {
        _shownProgress = 0f;
    }

    public void Exit() { }

    public void Update()
    {
        // Starte asynchrones Laden beim ersten Aufruf
        if (!Program._loading.Started)
        {
            StartAsyncLoading();
        }

        // Pruefe ob Laden abgeschlossen
        if (Program._loading.Complete)
        {
            // Sounds starten (Musik laeuft bereits)
            SoundManager.Initialize();

            // Quickstart: Direkt ins Spiel mit gewaehltem Land
            // (via GameQuickstart.cs oder --quickstart Argument)
            string? quickstartId = Program.QuickstartCountryId ??
                (GameQuickstart.Enabled ? GameQuickstart.CountryId : null);
            if (quickstartId != null)
            {
                if (Program.game.SelectPlayerCountry(quickstartId))
                {
                    Program.worldMap.DayNightCycleEnabled = Program.ui.MainMenuDayNightCycleEnabled;
                    Program.worldMap.Zoom = 2.0f;
                    Program.worldMap.CenterOnCountry(quickstartId, Program.ScreenWidth, Program.ScreenHeight);
                    Program.currentScreen = GameScreen.Playing;
                    Console.WriteLine($"[Quickstart] Spiel gestartet mit {quickstartId}");
                }
                else
                {
                    Console.WriteLine($"[Quickstart] Land '{quickstartId}' nicht gefunden! Starte Hauptmenue.");
                    Program.worldMap.Zoom = 2.0f;
                    Program.worldMap.CenterOnCountry("DEU", Program.ScreenWidth, Program.ScreenHeight);
                    Program.currentScreen = GameScreen.MainMenu;
                }
                Program.QuickstartCountryId = null; // Einmalig
                GameQuickstart.Enabled = false;
                return;
            }

            // Initial auf Europa zoomen und zentrieren
            Program.worldMap.Zoom = 2.0f;
            Program.worldMap.CenterOnCountry("DEU", Program.ScreenWidth, Program.ScreenHeight);

            Program.currentScreen = GameScreen.MainMenu;
        }
    }

    public void Draw()
    {
        float dt = Raylib.GetFrameTime();
        float time = (float)Raylib.GetTime();
        int w = Program.ScreenWidth;
        int h = Program.ScreenHeight;

        float progress = Program._loading.Progress;
        string status = Program._loading.Status;

        // Fortschritt weich nachziehen (keine Spruenge)
        _shownProgress += (progress - _shownProgress) * Math.Min(1f, dt * 6f);

        // === Hintergrund-Bild (skaliert auf Bildschirmgroesse) ===
        if (Program._loadingScreenTexture != null && Program._loadingScreenTexture.Value.Id != 0)
        {
            var tex = Program._loadingScreenTexture.Value;
            float scaleX = (float)w / tex.Width;
            float scaleY = (float)h / tex.Height;
            float scale = Math.Max(scaleX, scaleY);
            int drawW = (int)(tex.Width * scale);
            int drawH = (int)(tex.Height * scale);
            int drawX = (w - drawW) / 2;
            int drawY = (h - drawH) / 2;

            Raylib.DrawTexturePro(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(drawX, drawY, drawW, drawH),
                new Vector2(0, 0), 0f, Color.White);

            // Weiche Vignette statt flacher Abdunklung:
            // Grundton + dunklere Raender oben/unten fuer bessere Lesbarkeit
            Raylib.DrawRectangle(0, 0, w, h, new Color((byte)0, (byte)0, (byte)0, (byte)110));
            Raylib.DrawRectangleGradientV(0, 0, w, (int)(h * 0.30f),
                new Color((byte)0, (byte)0, (byte)0, (byte)130), new Color((byte)0, (byte)0, (byte)0, (byte)0));
            Raylib.DrawRectangleGradientV(0, h - (int)(h * 0.35f), w, (int)(h * 0.35f),
                new Color((byte)0, (byte)0, (byte)0, (byte)0), new Color((byte)0, (byte)0, (byte)0, (byte)160));
        }
        else
        {
            Raylib.DrawRectangleGradientV(0, 0, w, h,
                new Color((byte)20, (byte)20, (byte)30, (byte)255),
                new Color((byte)40, (byte)40, (byte)60, (byte)255));
        }

        // === Titel + Untertitel (zentriert wie vorher) ===
        Program.DrawGameTitle(w / 2, h / 2 - 120);

        string subtitle = "Baue dein Wirtschaftsimperium";
        int subWidth = Program.MeasureTextCached(subtitle, GameConfig.FONT_SIZE_LARGE);
        Program.DrawGameText(subtitle, (w - subWidth) / 2, h / 2 - 50, GameConfig.FONT_SIZE_LARGE, ColorPalette.TextGray);

        // === Fortschrittsbalken (Glas-Stil mit Glow) ===
        int barWidth = GameConfig.LOADING_BAR_WIDTH;
        int barHeight = GameConfig.LOADING_BAR_HEIGHT;
        int barX = (w - barWidth) / 2;
        int barY = h / 2 + 30;

        // Dezenter Glow hinter dem Balken (pulsiert leicht, waechst mit Fortschritt)
        float glowAlpha = 0.3f + 0.1f * MathF.Sin(time * 2f);
        byte ga = (byte)(glowAlpha * 255 * _shownProgress);
        Raylib.DrawRectangle(barX - 8, barY - 6, barWidth + 16, barHeight + 12,
            new Color(ColorPalette.Accent.R, ColorPalette.Accent.G, ColorPalette.Accent.B, (byte)(ga / 4)));

        // Track: dunkles Glas mit feiner heller Oberkante
        Raylib.DrawRectangle(barX, barY, barWidth, barHeight, new Color((byte)14, (byte)18, (byte)30, (byte)220));
        Raylib.DrawRectangle(barX, barY, barWidth, 1, new Color((byte)255, (byte)255, (byte)255, (byte)18));
        Raylib.DrawRectangleLinesEx(new Rectangle(barX - 1, barY - 1, barWidth + 2, barHeight + 2), 1,
            new Color((byte)90, (byte)98, (byte)120, (byte)160));

        // Fuellung: Accent-Verlauf mit hellem Kopf
        int fillW = (int)(barWidth * _shownProgress);
        if (fillW > 2)
        {
            Color fillDark = new Color((byte)(ColorPalette.Accent.R * 0.75f), (byte)(ColorPalette.Accent.G * 0.75f),
                (byte)(ColorPalette.Accent.B * 0.75f), (byte)255);
            Raylib.DrawRectangleGradientH(barX, barY, fillW, barHeight, fillDark, ColorPalette.Accent);

            // Pulsierender Glow am Fuellungskopf
            float pulse = 0.6f + 0.4f * MathF.Sin(time * 4f);
            Raylib.DrawCircleGradient(barX + fillW, barY + barHeight / 2, 14f * pulse,
                new Color(ColorPalette.Accent.R, ColorPalette.Accent.G, ColorPalette.Accent.B, (byte)80),
                new Color((byte)0, (byte)0, (byte)0, (byte)0));
            Raylib.DrawRectangle(barX + fillW - 2, barY, 2, barHeight,
                new Color((byte)220, (byte)235, (byte)255, (byte)255));
        }

        // Prozentzahl
        string percent = $"{(int)(_shownProgress * 100)}%";
        int percentWidth = Program.MeasureTextCached(percent, GameConfig.FONT_SIZE_NORMAL);
        Program.DrawGameText(percent, (w - percentWidth) / 2, barY + barHeight + 12, GameConfig.FONT_SIZE_NORMAL, ColorPalette.TextWhite);

        // Aktueller Ladestatus
        int statusWidth = Program.MeasureTextCached(status, GameConfig.FONT_SIZE_NORMAL);
        Program.DrawGameText(status, (w - statusWidth) / 2, barY + barHeight + 38, GameConfig.FONT_SIZE_NORMAL, ColorPalette.TextGray);

        // Lade-Animation (rotierende Punkte, wie vorher)
        int dotCount = 3;
        for (int i = 0; i < dotCount; i++)
        {
            float angle = time * 3 + i * (MathF.PI * 2 / dotCount);
            float dotX = w / 2 + MathF.Cos(angle) * 15;
            float dotY = barY + barHeight + 80 + MathF.Sin(angle) * 15;
            byte alpha = (byte)(150 + 105 * MathF.Sin(time * 5 + i));
            Raylib.DrawCircle((int)dotX, (int)dotY, 4, new Color(ColorPalette.Accent.R, ColorPalette.Accent.G, ColorPalette.Accent.B, alpha));
        }

        // === Rotierender Gameplay-Tipp unten ===
        DrawTip(w, h, time);

        // Version unten links (wie vorher)
        Program.DrawGameText("v0.1", 11, h - 25, 11, ColorPalette.TextGray);
    }

    /// <summary>
    /// Rotierender Gameplay-Tipp mit Ein-/Ausblendung
    /// </summary>
    private static void DrawTip(int w, int h, float time)
    {
        const float cycle = 6f;
        int idx = (int)(time / cycle) % Tips.Length;
        float t = time % cycle;
        float fade = Math.Min(1f, Math.Min(t / 0.7f, (cycle - t) / 0.7f));
        if (fade <= 0f) return;

        string tip = Tips[idx];
        int tw = Program.MeasureTextCached(tip, 16);
        int tx = (w - tw) / 2;
        int ty = h - 58;

        Raylib.DrawCircleV(new Vector2(tx - 14, ty + 8), 2.5f,
            new Color(ColorPalette.Accent.R, ColorPalette.Accent.G, ColorPalette.Accent.B, (byte)(200 * fade)));
        Program.DrawGameText(tip, tx, ty, 16,
            new Color((byte)200, (byte)205, (byte)218, (byte)(210 * fade)));
    }

    /// <summary>
    /// Startet das asynchrone Laden im Hintergrund
    /// </summary>
    private void StartAsyncLoading()
    {
        if (Program._loading.Started) return;
        Program._loading.Started = true;

        // Musik sofort starten (im Hauptthread fuer Raylib-Kompatibilitaet)
        Program.musicManager.Initialize();

        Program._loading.LoadingTask = Task.Run(() =>
        {
            try
            {
                Program._loading.Status = "Lade Spieldaten...";
                Program._loading.Progress = 0.05f;
                string basePath = CountryDataLoader.FindBasePath();
                BalanceConfig.Load(basePath);
                ResourceAbundance.Load(basePath);

                Program._loading.Status = "Initialisiere Spiellogik...";
                Program._loading.Progress = 0.08f;
                Program.game.Initialize();
                WorldMap.LoadResourceData(basePath);
                WorldMap.LoadWorldMapData(basePath);
                WorldMap.LoadAllianceData(basePath);

                Program.worldMap.Initialize((status, progress) =>
                {
                    Program._loading.Status = status;
                    Program._loading.Progress = 0.1f + progress * 0.85f;
                });

                if (Program.game.GameContext != null)
                {
                    Program.game.GameContext.WorldMap = Program.worldMap;
                }

                Program._loading.Status = "Fertig!";
                Program._loading.Progress = 1.0f;
                Thread.Sleep(300);

                Program._loading.Complete = true;
            }
            catch (Exception ex)
            {
                Program._loading.Status = $"Fehler: {ex.Message}";
                Console.WriteLine($"Ladefehler: {ex}");
            }
        });
    }
}
