using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Ladebildschirm - Zeigt Fortschritt und startet asynchrones Laden
/// </summary>
internal class LoadingScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.Loading;

    public void Enter() { }
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
        // Hole aktuelle Werte thread-sicher
        float progress = Program._loading.Progress;
        string status = Program._loading.Status;

        // Hintergrund-Bild zeichnen (skaliert auf Bildschirmgroesse)
        if (Program._loadingScreenTexture != null && Program._loadingScreenTexture.Value.Id != 0)
        {
            var tex = Program._loadingScreenTexture.Value;
            float scaleX = (float)Program.ScreenWidth / tex.Width;
            float scaleY = (float)Program.ScreenHeight / tex.Height;
            float scale = Math.Max(scaleX, scaleY);
            int drawW = (int)(tex.Width * scale);
            int drawH = (int)(tex.Height * scale);
            int drawX = (Program.ScreenWidth - drawW) / 2;
            int drawY = (Program.ScreenHeight - drawH) / 2;

            Raylib.DrawTexturePro(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(drawX, drawY, drawW, drawH),
                new Vector2(0, 0), 0f, Color.White);

            Raylib.DrawRectangle(0, 0, Program.ScreenWidth, Program.ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)150));
        }
        else
        {
            Raylib.DrawRectangleGradientV(0, 0, Program.ScreenWidth, Program.ScreenHeight,
                new Color((byte)20, (byte)20, (byte)30, (byte)255),
                new Color((byte)40, (byte)40, (byte)60, (byte)255));
        }

        // Titel
        Program.DrawGameTitle(Program.ScreenWidth / 2, Program.ScreenHeight / 2 - 120);

        // Untertitel
        string subtitle = "Build Your Economic Dominance";
        int subWidth = Program.MeasureTextCached(subtitle, GameConfig.FONT_SIZE_LARGE);
        Program.DrawGameText(subtitle, (Program.ScreenWidth - subWidth) / 2, Program.ScreenHeight / 2 - 50, GameConfig.FONT_SIZE_LARGE, ColorPalette.TextGray);

        // Fortschrittsbalken
        int barWidth = GameConfig.LOADING_BAR_WIDTH;
        int barHeight = GameConfig.LOADING_BAR_HEIGHT;
        int barX = (Program.ScreenWidth - barWidth) / 2;
        int barY = Program.ScreenHeight / 2 + 30;

        // Dezenter Glow hinter dem Balken
        float glowAlpha = 0.3f + 0.1f * MathF.Sin((float)Raylib.GetTime() * 2f);
        byte ga = (byte)(glowAlpha * 255 * progress);
        Raylib.DrawRectangle(barX - 8, barY - 6, barWidth + 16, barHeight + 12,
            new Color(ColorPalette.Accent.R, ColorPalette.Accent.G, ColorPalette.Accent.B, (byte)(ga / 4)));

        UIHelper.DrawProgressBar(barX, barY, barWidth, barHeight, progress, ColorPalette.Accent);

        // Prozentzahl
        string percent = $"{(int)(progress * 100)}%";
        int percentWidth = Program.MeasureTextCached(percent, GameConfig.FONT_SIZE_NORMAL);
        Program.DrawGameText(percent, (Program.ScreenWidth - percentWidth) / 2, barY + barHeight + 12, GameConfig.FONT_SIZE_NORMAL, ColorPalette.TextWhite);

        // Aktueller Ladestatus
        int statusWidth = Program.MeasureTextCached(status, GameConfig.FONT_SIZE_NORMAL);
        Program.DrawGameText(status, (Program.ScreenWidth - statusWidth) / 2, barY + barHeight + 38, GameConfig.FONT_SIZE_NORMAL, ColorPalette.TextGray);

        // Lade-Animation (rotierende Punkte)
        float time = (float)Raylib.GetTime();
        int dotCount = 3;
        for (int i = 0; i < dotCount; i++)
        {
            float angle = time * 3 + i * (MathF.PI * 2 / dotCount);
            float dotX = Program.ScreenWidth / 2 + MathF.Cos(angle) * 15;
            float dotY = barY + barHeight + 80 + MathF.Sin(angle) * 15;
            byte alpha = (byte)(150 + 105 * MathF.Sin(time * 5 + i));
            Raylib.DrawCircle((int)dotX, (int)dotY, 4, new Color(ColorPalette.Accent.R, ColorPalette.Accent.G, ColorPalette.Accent.B, alpha));
        }

        // Version unten
        Program.DrawGameText("v0.1", 11, Program.ScreenHeight - 25, 11, ColorPalette.TextGray);
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
