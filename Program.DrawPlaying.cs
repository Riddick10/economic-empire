using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.UI;
using GrandStrategyGame.UI.Panels;

namespace GrandStrategyGame;

/// <summary>
/// Program - Spielbildschirm-Rendering (DrawPlaying, MapViewButtons, Tooltips)
/// </summary>
partial class Program
{
    // Statische Strings und Farben (vermeiden per-Frame Allokation)
    private const string ControlsHintText = "[SPACE] Pause  [+/-] Speed  [1-8] Panels  [WASD/MMB] Verschieben  [Scroll] Zoom  [ESC] Menue";
    private static readonly Color BuildModeOrangeColor = new((byte)255, (byte)165, (byte)0, (byte)255);
    internal static void DrawPlaying()
    {
        if (game.PlayerCountry == null) return;
        var player = game.PlayerCountry;

        // Wolken-Spielzeit setzen (fuer Animation synchron mit Spielzeit)
        worldMap.CloudGameTime = (float)(game.TotalHours + game.Minute / 60.0);

        // Weltkarte als Hintergrund
        worldMap.Draw(ui.HoveredCountryId, ui.SelectedCountryId, player.Id, ui.SelectedProvinceId, ui.HoveredProvinceId, ui.CurrentMapView, ui.HeatmapResource);

        // Handelsrouten zeichnen (nur in Trade-Ansicht)
        if (ui.CurrentMapView == MapViewMode.Trade)
        {
            DrawTradeRoutes(player.Id);
            DrawTradeRouteFilter();
        }

        // Buendnis-Legende zeichnen (nur in Buendnis-Ansicht)
        if (ui.CurrentMapView == MapViewMode.Alliance)
        {
            DrawAllianceLegend();
        }

        // Ressourcen-Heatmap Filter (nur in Ressourcen-Ansicht)
        if (ui.CurrentMapView == MapViewMode.Resources)
        {
            DrawResourceHeatmapFilter();
        }

        // Minen auf der Karte zeichnen (nur bei Provinz-Zoom)
        if (worldMap.Zoom >= 8.0f)
        {
            DrawMines();
        }

        // Militaereinheiten auf der Karte zeichnen (nur bei starkem Zoom und politischer Ansicht)
        if (worldMap.Zoom >= 6.0f && ui.CurrentMapView == MapViewMode.Political)
        {
            var militaryManager = _mgr.Military;
            worldMap.DrawMilitaryUnits(militaryManager, player.Id, ui.SelectedUnit);
        }

        // Konflikt-Icons auf Laendern mit Krieg zeichnen
        DrawConflictIcons();

        // Tag/Nacht-Overlay und Stadtlichter zeichnen (nur politische Ansicht, Geschwindigkeit 1-3)
        if (ui.CurrentMapView == MapViewMode.Political && game.GameSpeed >= 1 && game.GameSpeed <= 3)
        {
            int dayOfYear = GetDayOfYear(game.Month, game.Day);
            float timeOfDay = game.Hour + game.Minute / 60f;
            worldMap.DrawDayNightOverlay(timeOfDay, dayOfYear);

            // Stadtlichter bei Nacht (Deutschland)
            worldMap.DrawCityLights(timeOfDay, dayOfYear);
        }

        // Top Bar (Hauptleiste) - breiter fuer alle Infos
        int topBarHeight = 70;
        Raylib.DrawRectangleGradientV(0, 0, ScreenWidth, topBarHeight,
            new Color((byte)50, (byte)50, (byte)65, (byte)255), ColorPalette.Panel);
        Raylib.DrawLine(0, topBarHeight - 1, ScreenWidth, topBarHeight - 1, new Color((byte)70, (byte)70, (byte)90, (byte)255));

        // Flagge links (nur Anzeige, nicht klickbar)
        int flagHeight = 50;
        int flagX = 10;
        int flagY = 10;
        DrawFlag(player.Id, flagX, flagY, flagHeight);

        // Berechne Flaggenbreite fuer Info-Offset
        int flagWidth = 75;
        var flagTex = GetFlagTexture(player.Id);
        if (flagTex != null)
        {
            float scale = (float)flagHeight / flagTex.Value.Height;
            flagWidth = (int)(flagTex.Value.Width * scale);
        }

        // === INFO-BEREICH rechts neben der Flagge ===
        int infoStartX = flagX + flagWidth + 20;
        int row1Y = 12;  // Erste Reihe: Land-Werte
        int row2Y = 44;  // Zweite Reihe: Ressourcen

        // Rechte Seite: Datum, Geschwindigkeit (eingerahmt)
        Vector2 mousePos = _cachedMousePos;
        int btnSize = 28;
        int btnY = row1Y - 3;

        // Rahmengroesse vorab berechnen: 5 Speed-Buttons + Pause + Luecke + Uhrzeit + Datum
        string timeStr = game.GetTimeString() + " Uhr";
        string dateStr = game.GetDateString();
        int timeWidth = MeasureTextCached(timeStr, 18);
        int dateWidth = MeasureTextCached(dateStr, 18);
        int speedAreaW = 6 * (btnSize + 2) - 2; // 6 Buttons (5 Speed + 1 Pause)
        int dateAreaW = timeWidth + 12 + dateWidth;
        int frameW = 8 + dateAreaW + 15 + speedAreaW + 8 + 4;
        int frameX = ScreenWidth - 15 - frameW + 4;

        // Gesamten Rahmen-Hintergrund ZUERST zeichnen (gleicher Stil wie Info-Boxen)
        Rectangle frameRect = new(frameX, row1Y - 6, frameW, 33);
        Raylib.DrawRectangleRounded(frameRect, 0.15f, 6, ColorPalette.Background);
        Raylib.DrawRectangleRoundedLinesEx(frameRect, 0.15f, 6, 1, ColorPalette.PanelLight);

        // Speed-Buttons (5 Stufen)
        int rightX = ScreenWidth - 15;
        for (int i = 0; i < 5; i++)
        {
            rightX -= btnSize;
            Rectangle btnRect = new Rectangle(rightX, btnY, btnSize, btnSize);
            bool hover = Raylib.CheckCollisionPointRec(mousePos, btnRect);
            bool active = !game.IsPaused && game.GameSpeed == ResourceConfig.SpeedValues[i];
            Raylib.DrawRectangleRec(btnRect, active ? ColorPalette.Accent : (hover ? ColorPalette.PanelLight : ColorPalette.Background));
            Raylib.DrawRectangleLinesEx(btnRect, 1, active ? ColorPalette.Accent : ColorPalette.PanelLight);
            DrawGameText(ResourceConfig.SpeedLabels[i], rightX + 10, btnY + 6, 14, active ? ColorPalette.TextWhite : ColorPalette.TextGray);
            if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left)) { game.GameSpeed = ResourceConfig.SpeedValues[i]; game.IsPaused = false; SoundManager.Play(SoundEffect.SpeedChange); }
            rightX -= 2;
        }

        // Pause Button (||)
        rightX -= btnSize;
        Rectangle pauseRect = new Rectangle(rightX, btnY, btnSize, btnSize);
        bool hoverPause = Raylib.CheckCollisionPointRec(mousePos, pauseRect);
        Raylib.DrawRectangleRec(pauseRect, game.IsPaused ? ColorPalette.Yellow : (hoverPause ? ColorPalette.PanelLight : ColorPalette.Background));
        Raylib.DrawRectangleLinesEx(pauseRect, 1, game.IsPaused ? ColorPalette.Yellow : ColorPalette.PanelLight);
        DrawGameText("||", rightX + 8, btnY + 6, 11, game.IsPaused ? ColorPalette.Background : ColorPalette.TextGray);
        if (hoverPause && Raylib.IsMouseButtonPressed(MouseButton.Left)) { game.IsPaused = !game.IsPaused; SoundManager.Play(game.IsPaused ? SoundEffect.Pause : SoundEffect.Unpause); }
        rightX -= 8;

        // Trennlinie zwischen Buttons und Datum
        Raylib.DrawLine(rightX, row1Y - 2, rightX, row1Y + 22, ColorPalette.PanelLight);
        rightX -= 8;

        // Datum (rechts)
        rightX -= dateWidth;
        DrawGameText(dateStr, rightX, row1Y, 18, ColorPalette.TextWhite);
        rightX -= 12;

        // Uhrzeit (links vom Datum)
        rightX -= timeWidth;
        DrawGameText(timeStr, rightX, row1Y, 18, ColorPalette.Accent);

        // === Zeile 1: Alle 6 Daten-Boxen (Auto-Breite) ===
        int x = infoStartX;

        DrawTopBarInfoBox(ref x, row1Y, "Bev:", Formatting.Population(player.Population));
        DrawTopBarInfoBox(ref x, row1Y, "BIP:", $"${Formatting.Number(player.GDP)}M");
        DrawTopBarInfoBox(ref x, row1Y, "BIP/K:", $"${player.GetGDPPerCapita().ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}");
        DrawTopBarInfoBox(ref x, row1Y, "Wachstum:", Formatting.Percentage(player.GDPGrowthRate, showSign: true),
            player.GDPGrowthRate >= 0 ? ColorPalette.Green : ColorPalette.Red);
        DrawTopBarInfoBox(ref x, row1Y, "Schulden:", Formatting.Money(player.NationalDebt),
            player.NationalDebt > 0 ? ColorPalette.Red : null);
        {
            // Budget-Box mit Netto-Aenderung
            double dailyBudgetChange = player.CalculateDailyBudgetChange();
            double dailyTradeBalance = player.TradeBalance;
            double dailyNet = dailyBudgetChange + dailyTradeBalance;
            string budgetVal = Formatting.Money(player.Budget);
            string netSign = dailyNet >= 0 ? "+" : "-";
            string netStr = netSign + Formatting.BudgetChange(dailyNet);
            Color budgetColor = player.Budget >= 0 ? ColorPalette.TextWhite : ColorPalette.Red;
            int budgetStartX = x;
            DrawTopBarInfoBox(ref x, row1Y, "Budget:", budgetVal, budgetColor, extraWidth: 35);
            // Netto direkt neben Budget-Wert
            int netX = budgetStartX + 8 + MeasureTextCached("Budget:", 16) + 4 + MeasureTextCached(budgetVal, 16);
            Color netColor = dailyNet >= 0 ? ColorPalette.Green : ColorPalette.Red;
            DrawGameText(netStr, netX, row1Y + 4, 11, netColor);
        }

        // === Zeile 2: Alle Ressourcen ===
        x = infoStartX;
        foreach (var resType in ResourceConfig.TopBarRow1)
            DrawTopBarResource(ref x, row2Y, player, resType);
        foreach (var resType in ResourceConfig.TopBarRow2)
            DrawTopBarResource(ref x, row2Y, player, resType);

        // === MINI-MUSIKPLAYER (oben rechts unter der Top-Bar) ===
        DrawMiniMusicPlayer();

        // Ausgewaehltes Land Info (rechts, unter dem Musik-Player)
        if (ui.SelectedCountryId != null && ui.SelectedCountryId != player.Id &&
            game.Countries.TryGetValue(ui.SelectedCountryId, out var selected))
        {
            int px = ScreenWidth - 340;
            int py = 135; // Unter dem Musik-Player
            Rectangle selectedInfoRect = new(px, py, 330, 220);
            Rectangle selectedInfoShadow = new(px + 2, py + 2, 330, 220);
            Raylib.DrawRectangleRounded(selectedInfoShadow, 0.03f, 6, new Color((byte)0, (byte)0, (byte)0, (byte)40));
            Raylib.DrawRectangleRounded(selectedInfoRect, 0.03f, 6, ColorPalette.PanelTransparent());
            Raylib.DrawRectangleRoundedLinesEx(selectedInfoRect, 0.03f, 6, 1, ColorPalette.PanelLight);

            int sy = py + 12;
            DrawGameText(selected.Name, px + 15, sy, 18, ColorPalette.Accent); sy += 32;
            DrawInfoRowAtLarge(px + 15, "Bevoelkerung", Formatting.Population(selected.Population), sy); sy += 26;
            DrawInfoRowAtLarge(px + 15, "BIP", $"${Formatting.Number(selected.GDP)}M", sy); sy += 26;
            DrawInfoRowAtLarge(px + 15, "BIP/Kopf", $"${selected.GetGDPPerCapita().ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}", sy); sy += 26;
            DrawInfoRowAtLarge(px + 15, "Wachstum", Formatting.Percentage(selected.GDPGrowthRate, showSign: true), sy,
                selected.GDPGrowthRate >= 0 ? ColorPalette.Green : ColorPalette.Red);
        }

        // Hover-Info (Provinz hat Prioritaet ueber Land)
        if (ui.HoveredProvinceId != null && worldMap.Provinces.TryGetValue(ui.HoveredProvinceId, out var hoveredProv))
        {
            Vector2 mp = _cachedMousePos;
            int hx = (int)mp.X + 15;
            int hy = (int)mp.Y + 15;

            // Am Bildschirmrand anpassen
            int tooltipW = 200;
            int tooltipH = 50;
            if (hx > ScreenWidth - tooltipW - 10) hx = (int)mp.X - tooltipW - 10;
            if (hy > ScreenHeight - tooltipH - 10) hy = (int)mp.Y - tooltipH - 10;

            Rectangle provTooltipRect = new(hx, hy, tooltipW, tooltipH);
            Rectangle provShadowRect = new(hx + 2, hy + 2, tooltipW, tooltipH);
            Raylib.DrawRectangleRounded(provShadowRect, 0.12f, 6, new Color((byte)0, (byte)0, (byte)0, (byte)80));
            Raylib.DrawRectangleRounded(provTooltipRect, 0.12f, 6, ColorPalette.Panel);
            Raylib.DrawRectangleRoundedLinesEx(provTooltipRect, 0.12f, 6, 1, ColorPalette.Accent);
            DrawGameText(hoveredProv.Name, hx + 10, hy + 6, 18, ColorPalette.TextWhite);

            // Land anzeigen
            string countryName = hoveredProv.CountryId;
            if (game.Countries.TryGetValue(hoveredProv.CountryId, out var provCountry))
            {
                countryName = provCountry.Name;
            }
            DrawGameText(countryName, hx + 10, hy + 28, 11, ColorPalette.TextGray);
        }
        else if (ui.HoveredCountryId != null && game.Countries.TryGetValue(ui.HoveredCountryId, out var hovered))
        {
            Vector2 mp = _cachedMousePos;
            int hx = (int)mp.X + 15;
            int hy = (int)mp.Y + 15;

            // Am Bildschirmrand anpassen
            if (hx > ScreenWidth - 150) hx = (int)mp.X - 150;
            if (hy > ScreenHeight - 40) hy = (int)mp.Y - 40;

            Rectangle countryTooltipRect = new(hx, hy, 140, 30);
            Rectangle countryShadowRect = new(hx + 2, hy + 2, 140, 30);
            Raylib.DrawRectangleRounded(countryShadowRect, 0.15f, 6, new Color((byte)0, (byte)0, (byte)0, (byte)80));
            Raylib.DrawRectangleRounded(countryTooltipRect, 0.15f, 6, ColorPalette.Panel);
            Raylib.DrawRectangleRoundedLinesEx(countryTooltipRect, 0.15f, 6, 1, ColorPalette.PanelLight);
            DrawGameText(hovered.Name, hx + 10, hy + 6, 18, ColorPalette.TextWhite);
        }

        // === TOP-MENU BUTTON-LEISTE (HOI4-Stil) ===
        DrawTopMenuBar();

        // === TOP-MENU PANEL (links) ===
        DrawActiveTopMenuPanel();

        // === POLITIK-PANEL (altes System - nur wenn Top-Menu nicht aktiv) ===
        if (ui.ShowPoliticsPanel && ui.ActiveTopMenuPanel == TopMenuPanel.None)
        {
            // Welches Land anzeigen?
            Country? displayCountry = null;
            bool isOwnCountry = false;

            if (ui.PoliticsPanelCountryId == null)
            {
                displayCountry = player;
                isOwnCountry = true;
            }
            else if (game.Countries.TryGetValue(ui.PoliticsPanelCountryId, out var foreignCountry))
            {
                displayCountry = foreignCountry;
                isOwnCountry = false;
            }

            if (displayCountry != null)
            {
                _politicsInfoPanel.Draw(displayCountry, isOwnCountry);
            }
        }

        // === PROVINZ-PANEL (rechts) ===
        if (ui.ShowProvincePanel && ui.SelectedProvinceId != null)
        {
            if (worldMap.Provinces.TryGetValue(ui.SelectedProvinceId, out var province))
            {
                _provinceInfoPanel.Draw(province);
            }
        }

        // === KARTENANSICHT-BUTTONS (unten rechts) ===
        DrawMapViewButtons();

        // === BAU-MODUS BILDSCHIRMRAND ===
        if (ui.FactoryBuildMode != null || ui.MineBuildMode != null || ui.RecruitmentMode != null)
        {
            int borderThickness = 6;
            // Orange fuer Fabriken/Minen, Gruen fuer Rekrutierung
            Color borderColor = ui.RecruitmentMode != null
                ? ColorPalette.Green
                : BuildModeOrangeColor;

            int topY = 70; // Unter der Top-Bar
            int bottomY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT; // Ueber der Bottom-Bar
            int areaHeight = bottomY - topY;

            // Links
            Raylib.DrawRectangle(0, topY, borderThickness, areaHeight, borderColor);
            // Rechts
            Raylib.DrawRectangle(ScreenWidth - borderThickness, topY, borderThickness, areaHeight, borderColor);
            // Oben (unter Top-Bar)
            Raylib.DrawRectangle(0, topY, ScreenWidth, borderThickness, borderColor);
            // Unten (ueber Bottom-Bar)
            Raylib.DrawRectangle(0, bottomY - borderThickness, ScreenWidth, borderThickness, borderColor);
        }

        // === FABRIK-BAU-MODUS HINWEIS ===
        if (ui.FactoryBuildMode != null)
        {
            string factoryTypeName = ui.FactoryBuildMode switch
            {
                FactoryType.Civilian => "ZIVIL-FABRIK",
                FactoryType.Military => "MILITAER-FABRIK",
                FactoryType.Dockyard => "WERFT",
                _ => "FABRIK"
            };

            string title = $"{factoryTypeName} PLATZIEREN";
            string hint = "Klicke auf Provinzen um Fabriken zu bauen";
            string hint2 = "[ESC] Bau-Modus beenden";
            int titleW = MeasureTextCached(title, 24);
            int hintTextW = MeasureTextCached(hint, 16);
            int hint2W = MeasureTextCached(hint2, 12);
            int hintW = Math.Max(titleW, hintTextW) + 60;
            int hintH = 70;
            int hintX = (ScreenWidth - hintW) / 2;
            int hintY = 85;

            // Hintergrund mit Highlight-Farbe
            Rectangle factoryHintRect = new(hintX, hintY, hintW, hintH);
            Raylib.DrawRectangleRounded(factoryHintRect, 0.08f, 6, ColorPalette.Panel);
            Raylib.DrawRectangleRoundedLinesEx(factoryHintRect, 0.08f, 6, 3, ColorPalette.Accent);

            // Titel zentriert
            DrawGameText(title, hintX + (hintW - titleW) / 2, hintY + 8, 11, ColorPalette.Accent);

            // Hinweis zentriert
            DrawGameText(hint, hintX + (hintW - hintTextW) / 2, hintY + 34, 11, ColorPalette.TextWhite);

            // ESC Hinweis zentriert
            DrawGameText(hint2, hintX + (hintW - hint2W) / 2, hintY + 54, 11, ColorPalette.TextGray);
        }

        // === MINEN-BAU-MODUS HINWEIS ===
        if (ui.MineBuildMode != null)
        {
            string mineTypeName = Mine.GetGermanName(ui.MineBuildMode.Value);
            Color mineColor = Mine.GetMapColor(ui.MineBuildMode.Value);
            ResourceType requiredRes = Mine.GetResourceType(ui.MineBuildMode.Value);
            string resName = Resource.GetGermanName(requiredRes);

            string title = $"{mineTypeName.ToUpper()} PLATZIEREN";
            string hint = $"Klicke auf Provinz mit {resName}";
            string hint2 = "[ESC] Bau-Modus beenden";
            int titleW = MeasureTextCached(title, 24);
            int hintTextW = MeasureTextCached(hint, 16);
            int hint2W = MeasureTextCached(hint2, 12);
            int hintW = Math.Max(titleW, hintTextW) + 60;
            int hintH = 70;
            int hintX = (ScreenWidth - hintW) / 2;
            int hintY = 85;

            // Hintergrund mit Mine-Farbe als Rand
            Rectangle mineHintRect = new(hintX, hintY, hintW, hintH);
            Raylib.DrawRectangleRounded(mineHintRect, 0.08f, 6, ColorPalette.Panel);
            Raylib.DrawRectangleRoundedLinesEx(mineHintRect, 0.08f, 6, 3, mineColor);

            // Titel zentriert
            DrawGameText(title, hintX + (hintW - titleW) / 2, hintY + 8, 11, mineColor);

            // Hinweis zentriert
            DrawGameText(hint, hintX + (hintW - hintTextW) / 2, hintY + 34, 11, ColorPalette.TextWhite);

            // ESC Hinweis zentriert
            DrawGameText(hint2, hintX + (hintW - hint2W) / 2, hintY + 54, 11, ColorPalette.TextGray);
        }

        // === REKRUTIERUNGS-MODUS HINWEIS ===
        if (ui.RecruitmentMode != null)
        {
            string unitTypeName = ui.RecruitmentMode switch
            {
                UnitType.Infantry => "INFANTERIE",
                UnitType.Tank => "PANZER-DIVISION",
                UnitType.Artillery => "ARTILLERIE",
                UnitType.Mechanized => "MECHANISIERTE INF.",
                UnitType.Airborne => "FALLSCHIRMJAEGER",
                _ => "EINHEIT"
            };

            string title = $"{unitTypeName} REKRUTIEREN";
            string hint = "Klicke auf eigene Provinz zum Rekrutieren";
            string hint2 = "[ESC] Abbrechen";
            int titleW = MeasureTextCached(title, 24);
            int hintTextW = MeasureTextCached(hint, 16);
            int hint2W = MeasureTextCached(hint2, 12);
            int hintW = Math.Max(titleW, hintTextW) + 60;
            int hintH = 70;
            int hintX = (ScreenWidth - hintW) / 2;
            int hintY = 85;

            // Hintergrund mit Gruen als Rand (Militaer-Farbe)
            Rectangle recruitHintRect = new(hintX, hintY, hintW, hintH);
            Raylib.DrawRectangleRounded(recruitHintRect, 0.08f, 6, ColorPalette.Panel);
            Raylib.DrawRectangleRoundedLinesEx(recruitHintRect, 0.08f, 6, 3, ColorPalette.Green);

            // Titel zentriert
            DrawGameText(title, hintX + (hintW - titleW) / 2, hintY + 8, 11, ColorPalette.Green);

            // Hinweis zentriert
            DrawGameText(hint, hintX + (hintW - hintTextW) / 2, hintY + 34, 11, ColorPalette.TextWhite);

            // ESC Hinweis zentriert
            DrawGameText(hint2, hintX + (hintW - hint2W) / 2, hintY + 54, 11, ColorPalette.TextGray);
        }

        // === POPUP-NACHRICHTEN (unten rechts) ===
        DrawNotificationPopups();

        // Bottom Bar (Berater-Leiste + Steuerung)
        int bbY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT;
        Raylib.DrawRectangleGradientV(0, bbY, ScreenWidth, GameConfig.BOTTOM_BAR_HEIGHT,
            ColorPalette.Panel, new Color((byte)35, (byte)35, (byte)48, (byte)255));
        Raylib.DrawLine(0, bbY, ScreenWidth, bbY, new Color((byte)70, (byte)70, (byte)90, (byte)255));

        // Zoom-Anzeige (rechts unten, in der grauen Leiste)
        float zoomPercent = (worldMap.Zoom - GameConfig.MIN_ZOOM) / (GameConfig.MAX_ZOOM - GameConfig.MIN_ZOOM) * 100f;
        string zoomStr = $"Zoom: {zoomPercent:F0}%";
        int zoomWidth = MeasureTextCached(zoomStr, 14);
        int bottomBarY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT;
        DrawGameText(zoomStr, ScreenWidth - zoomWidth - 15, bottomBarY + 10, 14, ColorPalette.TextWhite);

        // Steuerungshinweise (links unten, in der grauen Leiste)
        DrawGameText(ControlsHintText, 15, bottomBarY + 10, 11, ColorPalette.TextGray);

        // === FORSCHUNGSBAUM (Vollbild-Overlay) ===
        _techTreePanel.Draw();

        // === TUTORIAL-PANEL ===
        if (ui.ShowTutorialPanel)
        {
            DrawTutorialPanel();
        }

        // === WIRTSCHAFTS-RANGLISTE ===
        if (ui.ShowEconomyRanking)
        {
            DrawEconomyRankingPanel();
        }

        // Pause-Menü (ueber allem)
        if (ui.ShowPauseMenu)
        {
            DrawPauseMenu();
        }

        // Speichern-Panel (hoechste Prioritaet)
        if (ui.ShowSavePanel)
        {
            DrawSavePanelOverlay();
        }

    }

    /// <summary>
    /// Hilfsfunktion: Text umbrechen
    /// </summary>
    internal static List<string> WrapText(string text, int maxWidth, int fontSize)
    {
        var lines = new List<string>();
        string currentLine = "";

        foreach (var word in text.Split(' '))
        {
            string testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
            if (MeasureTextCached(testLine, fontSize) > maxWidth)
            {
                if (currentLine.Length > 0)
                    lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }
        if (currentLine.Length > 0)
            lines.Add(currentLine);

        return lines;
    }
}
