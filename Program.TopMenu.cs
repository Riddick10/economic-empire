using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.UI;
using GrandStrategyGame.UI.Panels;

namespace GrandStrategyGame;

/// <summary>
/// Program - Top-Menu Bar und zugehoerige Panels (HOI4-Stil)
/// Enthaelt: Button-Leiste, Panel-Dispatch, und Helper-Methoden die von UI/Panels/ genutzt werden.
/// </summary>
partial class Program
{
    // Button-Konfiguration
    private const int TOP_MENU_BTN_SIZE = 50;
    private const int TOP_MENU_BTN_SPACING = 6;
    private const int TOP_MENU_START_Y = 75; // Direkt unter der Top-Bar

    // Panel-Instanzen (ITopMenuPanel)
    private static readonly Dictionary<TopMenuPanel, ITopMenuPanel> _topMenuPanels = new()
    {
        { TopMenuPanel.Politics, new PoliticsTopMenuPanel() },
        { TopMenuPanel.Trade, new TradeTopMenuPanel() },
        { TopMenuPanel.Production, new ProductionTopMenuPanel() },
        { TopMenuPanel.Research, new ResearchTopMenuPanel() },
        { TopMenuPanel.Diplomacy, new DiplomacyTopMenuPanel() },
        { TopMenuPanel.Budget, new BudgetTopMenuPanel() },
        { TopMenuPanel.Military, new MilitaryTopMenuPanel() },
        { TopMenuPanel.News, new NewsTopMenuPanel() },
        { TopMenuPanel.Logistics, new LogisticsTopMenuPanel() },
    };

    // Statisches Button-Array (vermeidet per-Frame Allokation)
    private static readonly (TopMenuPanel Panel, string Tooltip)[] TopMenuButtons = {
        (TopMenuPanel.Politics, "Politik"),
        (TopMenuPanel.Trade, "Handel"),
        (TopMenuPanel.Production, "Produktion"),
        (TopMenuPanel.Logistics, "Logistik"),
        (TopMenuPanel.Research, "Forschung"),
        (TopMenuPanel.Diplomacy, "Diplomatie"),
        (TopMenuPanel.Budget, "Finanzen"),
        (TopMenuPanel.Military, "Militaer"),
    };

    // Caches -> siehe _cache (PerformanceCache)

    /// <summary>
    /// Formatiert grosse Zahlen in deutscher Notation (Mrd, Mio, Tsd)
    /// </summary>
    internal static string FormatGermanNumber(double value)
    {
        double absValue = Math.Abs(value);
        string sign = value < 0 ? "-" : "";

        if (absValue >= 1_000_000_000)
        {
            return $"{sign}{absValue / 1_000_000_000:F1}Mrd";
        }
        else if (absValue >= 1_000_000)
        {
            return $"{sign}{absValue / 1_000_000:F1}Mio";
        }
        else if (absValue >= 1_000)
        {
            return $"{sign}{absValue / 1_000:F1}Tsd";
        }
        else
        {
            return $"{sign}{absValue:F0}";
        }
    }

    /// <summary>
    /// Prueft ob die Maus ueber einem Top-Menu Button ist
    /// </summary>
    static bool IsMouseOverTopMenuButtons()
    {
        Vector2 mousePos = _cachedMousePos;
        int x = 10;
        int y = TOP_MENU_START_Y;

        // 7 Buttons pruefen (News-Button ist unten rechts separat)
        for (int i = 0; i < 7; i++)
        {
            Rectangle btnRect = new Rectangle(x, y, TOP_MENU_BTN_SIZE, TOP_MENU_BTN_SIZE);
            if (Raylib.CheckCollisionPointRec(mousePos, btnRect))
            {
                return true;
            }
            x += TOP_MENU_BTN_SIZE + TOP_MENU_BTN_SPACING;
        }
        return false;
    }

    /// <summary>
    /// Prueft ob die Maus ueber dem aktiven Top-Menu Panel ist
    /// </summary>
    static bool IsMouseOverTopMenuPanel()
    {
        if (ui.ActiveTopMenuPanel == TopMenuPanel.None) return false;

        Vector2 mousePos = _cachedMousePos;
        var (panelX, panelY, panelW, panelH) = GetTopMenuPanelRect();
        Rectangle panelRect = new Rectangle(panelX, panelY, panelW, panelH);

        if (Raylib.CheckCollisionPointRec(mousePos, panelRect))
            return true;

        // Bau-Seiten-Panel pruefen (wenn offen)
        if (ui.ShowBuildPanel && ui.ActiveTopMenuPanel == TopMenuPanel.Production)
        {
            int buildPanelX = panelX + panelW + 2;
            int buildPanelW = 280;
            Rectangle buildRect = new Rectangle(buildPanelX, panelY, buildPanelW, panelH);
            if (Raylib.CheckCollisionPointRec(mousePos, buildRect))
                return true;
        }

        // Handel-Seiten-Panel pruefen (wenn offen)
        if (ui.ShowTradeCreatePanel && ui.TradeSelectedResource != null && ui.ActiveTopMenuPanel == TopMenuPanel.Trade)
        {
            int tradePanelX = panelX + panelW + 2;
            int tradePanelW = 260;
            Rectangle tradeRect = new Rectangle(tradePanelX, panelY, tradePanelW, panelH);
            if (Raylib.CheckCollisionPointRec(mousePos, tradeRect))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gecachte Textbreiten-Messung (vermeidet wiederholte MeasureText-Aufrufe)
    /// </summary>
    internal static int MeasureTextCached(string text, int fontSize)
    {
        var key = (text, fontSize);
        if (!_cache.TextWidth.TryGetValue(key, out int width))
        {
            width = MeasureGameText(text, fontSize);
            _cache.TextWidth[key] = width;
        }
        return width;
    }

    /// <summary>
    /// Zeichnet die Top-Menu Button-Leiste (HOI4-Stil)
    /// </summary>
    static void DrawTopMenuBar()
    {
        Vector2 mousePos = _cachedMousePos;
        int x = 10;
        int y = TOP_MENU_START_Y;

        for (int idx = 0; idx < TopMenuButtons.Length; idx++)
        {
            var (panel, tooltip) = TopMenuButtons[idx];
            Rectangle btnRect = new Rectangle(x, y, TOP_MENU_BTN_SIZE, TOP_MENU_BTN_SIZE);
            bool isHovered = Raylib.CheckCollisionPointRec(mousePos, btnRect);
            bool isActive = ui.ActiveTopMenuPanel == panel;

            // Button-Farbe
            Color btnColor = isActive ? ColorPalette.Accent : (isHovered ? ColorPalette.PanelLight : ColorPalette.Panel);
            Color borderColor = isActive ? ColorPalette.Accent : (isHovered ? ColorPalette.Accent : ColorPalette.PanelLight);

            // Button zeichnen (abgerundet)
            Raylib.DrawRectangleRounded(btnRect, 0.12f, 6, btnColor);
            Raylib.DrawRectangleRoundedLinesEx(btnRect, 0.12f, 6, 2, borderColor);

            // Icon zentriert zeichnen (mit Padding)
            int iconPadding = 8;
            int iconSize = TOP_MENU_BTN_SIZE - (iconPadding * 2);
            int iconX = x + iconPadding;
            int iconY = y + iconPadding;
            Color iconTint = isActive ? ColorPalette.TextWhite : (isHovered ? ColorPalette.TextWhite : ColorPalette.TextGray);
            DrawTopMenuIcon(panel, iconX, iconY, iconSize, iconTint);

            // Shortcut-Zahl unten rechts im Button (1-8 fuer alle Buttons)
            if (idx < 8)
            {
                string shortcutNum = (idx + 1).ToString();
                int numFontSize = 11;
                int numW = MeasureTextCached(shortcutNum, numFontSize);
                int numX = x + TOP_MENU_BTN_SIZE - numW - 4;
                int numY = y + TOP_MENU_BTN_SIZE - numFontSize - 3;
                DrawGameText(shortcutNum, numX, numY, numFontSize, isActive ? ColorPalette.TextWhite : ColorPalette.TextGray);
            }

            // Klick-Handler
            if (isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                // Toggle - wenn bereits aktiv, schliessen
                ui.ActiveTopMenuPanel = isActive ? TopMenuPanel.None : panel;
                SoundManager.Play(SoundEffect.Click);

                // Andere Panels schliessen
                ui.ShowBuildPanel = false;
                if (ui.ActiveTopMenuPanel != TopMenuPanel.None)
                {
                    ui.ShowPoliticsPanel = false;
                    ui.ShowProvincePanel = false;
                }
            }

            // Tooltip bei Hover
            if (isHovered)
            {
                string hotkeyHint = idx < 8 ? $" [{idx + 1}]" : "";
                string fullTooltip = tooltip + hotkeyHint;
                int tooltipWidth = MeasureTextCached(fullTooltip, 14) + 16;
                int tooltipX = x + TOP_MENU_BTN_SIZE + 5;
                int tooltipY = y + TOP_MENU_BTN_SIZE / 2 - 11;
                Rectangle ttRect = new(tooltipX, tooltipY, tooltipWidth, 22);
                Raylib.DrawRectangleRounded(ttRect, 0.2f, 6, ColorPalette.Panel);
                Raylib.DrawRectangleRoundedLinesEx(ttRect, 0.2f, 6, 1, ColorPalette.Accent);
                DrawGameText(fullTooltip, tooltipX + 8, tooltipY + 4, 14, ColorPalette.TextWhite);
            }

            x += TOP_MENU_BTN_SIZE + TOP_MENU_BTN_SPACING;
        }
    }

    /// <summary>
    /// Zeichnet das aktive Top-Menu Panel auf der linken Seite
    /// </summary>
    static void DrawActiveTopMenuPanel()
    {
        if (ui.ActiveTopMenuPanel == TopMenuPanel.None) return;
        if (game.PlayerCountry == null) return;

        // Dispatch an ITopMenuPanel-Instanzen
        if (_topMenuPanels.TryGetValue(ui.ActiveTopMenuPanel, out var panel))
        {
            var ctx = new TopMenuContext
            {
                Game = game,
                UI = ui,
                MousePos = _cachedMousePos,
                Bounds = new Rectangle(10, TOP_MENU_START_Y + TOP_MENU_BTN_SIZE + 10, 450, ScreenHeight - (TOP_MENU_START_Y + TOP_MENU_BTN_SIZE + 10) - GameConfig.BOTTOM_BAR_HEIGHT - 10),
                Managers = _mgr,
                WorldMap = worldMap,
                Cache = _cache,
                ScreenWidth = ScreenWidth,
                ScreenHeight = ScreenHeight,
            };
            panel.Draw(ctx);
        }
    }

    /// <summary>
    /// Basis-Panel-Layout (gemeinsam fuer alle Top-Menu Panels)
    /// </summary>
    internal static (int X, int Y, int W, int H) GetTopMenuPanelRect()
    {
        int panelX = 10;
        int panelY = TOP_MENU_START_Y + TOP_MENU_BTN_SIZE + 10;
        int panelW = 450;
        int panelH = ScreenHeight - panelY - GameConfig.BOTTOM_BAR_HEIGHT - 10;
        return (panelX, panelY, panelW, panelH);
    }

    /// <summary>
    /// Zeichnet den Panel-Hintergrund mit Titel
    /// </summary>
    internal static int DrawTopMenuPanelHeader(string title)
    {
        var (panelX, panelY, panelW, panelH) = GetTopMenuPanelRect();

        float roundness = 0.02f;
        int segments = 6;
        Rectangle panelRect = new(panelX, panelY, panelW, panelH);

        // Schatten
        Rectangle shadowRect = new(panelX + 2, panelY + 2, panelW, panelH);
        Raylib.DrawRectangleRounded(shadowRect, roundness, segments, new Color((byte)0, (byte)0, (byte)0, (byte)40));

        // Panel-Hintergrund
        Raylib.DrawRectangleRounded(panelRect, roundness, segments, ColorPalette.Panel);

        // Oberer Glanz
        Rectangle glossRect = new(panelX + 1, panelY + 1, panelW - 2, 25);
        Raylib.DrawRectangleRounded(glossRect, roundness, segments, new Color((byte)255, (byte)255, (byte)255, (byte)8));

        Raylib.DrawRectangleRoundedLinesEx(panelRect, roundness, segments, 2, ColorPalette.Accent);

        // Titel
        int y = panelY + 15;
        int contentX = panelX + 15;
        DrawGameText(title, contentX, y, 30, ColorPalette.Accent);
        y += 38;

        // Trennlinie (Gradient-artig mit Alpha)
        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.Accent);
        y += 15;

        return y;
    }

    // ============================================================
    // Helper-Methoden (von UI/Panels/ Klassen aufgerufen)
    // ============================================================

    /// <summary>
    /// Konvertiert Parteien aus PoliticsManager in Anzeigeformat
    /// </summary>
    internal static List<(string Name, float Percentage, Color Color)> GetPartyDataFromPolitics(CountryPolitics? politics)
    {
        if (politics == null || politics.Parties.Count == 0)
        {
            return new List<(string, float, Color)>
            {
                ("Keine Daten", 100f, ColorPalette.TextGray)
            };
        }

        // Partei-spezifische Farben (ueberschreiben Ideologie-Farben)
        Color? GetPartySpecificColor(string partyName) => partyName switch
        {
            "CDU/CSU" => new Color((byte)30, (byte)30, (byte)30, (byte)255),      // Schwarz
            "AfD" => new Color((byte)0, (byte)158, (byte)224, (byte)255),         // AfD-Blau
            "SPD" => new Color((byte)220, (byte)50, (byte)50, (byte)255),         // Rot
            "Gruene" or "Grüne" => new Color((byte)80, (byte)180, (byte)80, (byte)255), // Gruen
            "FDP" => new Color((byte)255, (byte)220, (byte)0, (byte)255),         // Gelb
            "Die Linke" or "Linke" => new Color((byte)190, (byte)30, (byte)90, (byte)255), // Magenta
            "BSW" => new Color((byte)140, (byte)50, (byte)160, (byte)255),        // Lila
            _ => null
        };

        // Ideologie-Farben (Fallback)
        Color GetIdeologyColor(Ideology ideology) => ideology switch
        {
            Ideology.Democratic => new Color((byte)100, (byte)149, (byte)237, (byte)255), // Blau
            Ideology.Conservative => new Color((byte)0, (byte)0, (byte)139, (byte)255),   // Dunkelblau
            Ideology.Socialist => new Color((byte)220, (byte)20, (byte)60, (byte)255),    // Rot
            Ideology.Communist => new Color((byte)139, (byte)0, (byte)0, (byte)255),      // Dunkelrot
            Ideology.Fascist => new Color((byte)64, (byte)64, (byte)64, (byte)255),       // Grau
            Ideology.Nationalist => new Color((byte)0, (byte)158, (byte)224, (byte)255),  // Blau
            Ideology.Liberal => new Color((byte)255, (byte)215, (byte)0, (byte)255),      // Gold
            Ideology.Green => new Color((byte)34, (byte)139, (byte)34, (byte)255),        // Gruen
            _ => ColorPalette.TextGray
        };

        return politics.Parties
            .OrderByDescending(p => p.Popularity)
            .Select(p => (p.Name, (float)(p.Popularity * 100), GetPartySpecificColor(p.Name) ?? GetIdeologyColor(p.Ideology)))
            .ToList();
    }

    /// <summary>
    /// Zeichnet das Sub-Panel zum Erstellen eines neuen Handels
    /// </summary>
    internal static void DrawTradeCreateSubPanel(ref int y, int contentX, int panelX, int panelW, Country player,
        TradeManager? tradeManager, GameContext? gameContext, Vector2 mousePos)
    {
        // Export/Import Toggle
        DrawGameText("Richtung:", contentX, y, 14, ColorPalette.TextGray);
        y += 18;

        int toggleW = 80;
        int toggleH = 24;

        // Export Button
        Rectangle exportBtn = new Rectangle(contentX, y, toggleW, toggleH);
        bool exportHovered = Raylib.CheckCollisionPointRec(mousePos, exportBtn);
        Color exportBg = ui.TradeIsExport ? ColorPalette.Green : (exportHovered ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(exportBtn, exportBg);
        Raylib.DrawRectangleLinesEx(exportBtn, 1, ui.TradeIsExport ? ColorPalette.Green : ColorPalette.PanelLight);
        DrawGameText("Export", contentX + 18, y + 5, 14, ColorPalette.TextWhite);

        if (exportHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.TradeIsExport = true;
            ui.TradeSelectedCountry = null;
            SoundManager.Play(SoundEffect.Click);
        }

        // Import Button
        Rectangle importBtn = new Rectangle(contentX + toggleW + 8, y, toggleW, toggleH);
        bool importHovered = Raylib.CheckCollisionPointRec(mousePos, importBtn);
        Color importBg = !ui.TradeIsExport ? ColorPalette.Red : (importHovered ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(importBtn, importBg);
        Raylib.DrawRectangleLinesEx(importBtn, 1, !ui.TradeIsExport ? ColorPalette.Red : ColorPalette.PanelLight);
        DrawGameText("Import", contentX + toggleW + 26, y + 5, 14, ColorPalette.TextWhite);

        if (importHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.TradeIsExport = false;
            ui.TradeSelectedCountry = null;
            SoundManager.Play(SoundEffect.Click);
        }

        y += 32;

        // Ressourcen-Auswahl
        DrawGameText("Ressource:", contentX, y, 14, ColorPalette.TextGray);
        y += 18;

        // Ressourcen-Buttons (2 Reihen)
        var resources = new[]
        {
            (ResourceType.Oil, "Oel"),
            (ResourceType.NaturalGas, "Gas"),
            (ResourceType.Coal, "Kohle"),
            (ResourceType.Iron, "Eisen"),
            (ResourceType.Copper, "Kupfer"),
            (ResourceType.Uranium, "Uran"),
            (ResourceType.Food, "Nahrung"),
            (ResourceType.Steel, "Stahl"),
            (ResourceType.Electronics, "Elektr."),
            (ResourceType.Machinery, "Masch."),
            (ResourceType.ConsumerGoods, "Konsum")
        };

        int resBtnW = 58;
        int resBtnH = 22;
        int resCol = 0;
        int resStartY = y;

        foreach (var (resType, resName) in resources)
        {
            int bx = contentX + resCol * (resBtnW + 4);
            int by = y;

            Rectangle resBtn = new Rectangle(bx, by, resBtnW, resBtnH);
            bool resHovered = Raylib.CheckCollisionPointRec(mousePos, resBtn);
            bool resSelected = ui.TradeSelectedResource == resType;

            // Bei Export: Nur Ressourcen anzeigen die wir haben
            // Bei Import: Alle Ressourcen anzeigen
            double available = player.GetResource(resType);
            bool canTrade = ui.TradeIsExport ? available > 0 : true;

            Color resBg = !canTrade ? new Color((byte)40, (byte)40, (byte)50, (byte)255) :
                          resSelected ? ColorPalette.Accent :
                          resHovered ? ColorPalette.PanelLight : ColorPalette.Panel;
            Color resBorder = resSelected ? ColorPalette.Accent : ColorPalette.PanelLight;
            Color resText = !canTrade ? ColorPalette.TextGray : ColorPalette.TextWhite;

            Raylib.DrawRectangleRec(resBtn, resBg);
            Raylib.DrawRectangleLinesEx(resBtn, 1, resBorder);
            DrawGameText(resName, bx + 4, by + 4, 14, resText);

            if (canTrade && resHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                ui.TradeSelectedResource = resType;
                ui.TradeSelectedCountry = null;
                SoundManager.Play(SoundEffect.Click);
            }

            resCol++;
            if (resCol >= 6)
            {
                resCol = 0;
                y += resBtnH + 4;
            }
        }
        if (resCol != 0) y += resBtnH + 4;
        y += 8;

        // Wenn Ressource ausgewaehlt: Menge und Info anzeigen
        if (ui.TradeSelectedResource != null)
        {
            var selectedRes = ui.TradeSelectedResource.Value;
            double stock = player.GetResource(selectedRes);
            double price = game.Resources.TryGetValue(selectedRes, out var res) ? res.CurrentPrice : 0;

            DrawGameText($"Lager: {FormatGermanNumber(stock)}  |  Preis: ${price:F0}M", contentX, y, 14, ColorPalette.TextGray);
            y += 18;

            // Mengen-Slider
            DrawGameText("Menge/Tag:", contentX, y, 14, ColorPalette.TextGray);
            y += 18;

            // Minus Button
            Rectangle minusBtn = new Rectangle(contentX, y, 14, 22);
            bool minusHovered = Raylib.CheckCollisionPointRec(mousePos, minusBtn);
            Raylib.DrawRectangleRec(minusBtn, minusHovered ? ColorPalette.Accent : ColorPalette.PanelLight);
            DrawGameText("-", contentX + 8, y + 3, 14, ColorPalette.TextWhite);
            if (minusHovered && Raylib.IsMouseButtonPressed(MouseButton.Left) && ui.TradeAmount > 1)
            {
                ui.TradeAmount = Math.Max(1, ui.TradeAmount - (ui.TradeAmount >= 100 ? 10 : (ui.TradeAmount >= 10 ? 5 : 1)));
                SoundManager.Play(SoundEffect.Click);
            }

            // Menge Anzeige
            DrawGameText($"{ui.TradeAmount}", contentX + 32, y + 3, 14, ColorPalette.TextWhite);

            // Plus Button
            Rectangle plusBtn = new Rectangle(contentX + 70, y, 14, 22);
            bool plusHovered = Raylib.CheckCollisionPointRec(mousePos, plusBtn);
            Raylib.DrawRectangleRec(plusBtn, plusHovered ? ColorPalette.Accent : ColorPalette.PanelLight);
            DrawGameText("+", contentX + 78, y + 3, 14, ColorPalette.TextWhite);

            int maxAmount = ui.TradeIsExport ? (int)stock : 1000;
            if (plusHovered && Raylib.IsMouseButtonPressed(MouseButton.Left) && ui.TradeAmount < maxAmount)
            {
                ui.TradeAmount = Math.Min(maxAmount, ui.TradeAmount + (ui.TradeAmount >= 100 ? 10 : (ui.TradeAmount >= 10 ? 5 : 1)));
                SoundManager.Play(SoundEffect.Click);
            }

            // Schnell-Buttons
            int[] quickAmounts = { 10, 50, 100, 500 };
            int qx = contentX + 110;
            foreach (int qa in quickAmounts)
            {
                if (ui.TradeIsExport && qa > stock) continue;
                Rectangle qaBtn = new Rectangle(qx, y, 36, 22);
                bool qaHovered = Raylib.CheckCollisionPointRec(mousePos, qaBtn);
                Raylib.DrawRectangleRec(qaBtn, qaHovered ? ColorPalette.Accent : ColorPalette.Panel);
                DrawGameText($"{qa}", qx + 4, y + 4, 14, ColorPalette.TextWhite);
                if (qaHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    ui.TradeAmount = qa;
                    SoundManager.Play(SoundEffect.Click);
                }
                qx += 40;
            }

            y += 28;

            // Tageswert anzeigen
            double dailyValue = price * ui.TradeAmount;
            string valueLabel = ui.TradeIsExport ? "Einnahmen:" : "Kosten:";
            Color valueColor = ui.TradeIsExport ? ColorPalette.Green : ColorPalette.Red;
            DrawGameText($"{valueLabel} ${dailyValue:N0}M/Tag", contentX, y, 14, valueColor);
            y += 20;

            // Partner auswaehlen Hinweis
            if (ui.TradeSelectedCountry == null)
            {
                DrawGameText("-> Partner im Seitenpanel waehlen", contentX, y, 14, ColorPalette.Accent);
                y += 18;
            }
            else
            {
                // Ausgewaehlter Partner
                string partnerName = game.Countries.TryGetValue(ui.TradeSelectedCountry, out var pc) ? pc.Name : ui.TradeSelectedCountry;
                DrawGameText($"Partner: {partnerName}", contentX, y, 14, ColorPalette.TextWhite);
                y += 20;

                // Bestaetigen Button
                int confirmW = panelW - 30;
                int confirmH = 28;
                Rectangle confirmBtn = new Rectangle(contentX, y, confirmW, confirmH);
                bool confirmHovered = Raylib.CheckCollisionPointRec(mousePos, confirmBtn);

                Color confirmBg = confirmHovered ? ColorPalette.Green : new Color((byte)34, (byte)139, (byte)34, (byte)200);
                Raylib.DrawRectangleRec(confirmBtn, confirmBg);
                Raylib.DrawRectangleLinesEx(confirmBtn, 2, ColorPalette.Green);

                string confirmLabel = ui.TradeIsExport ? "Export starten" : "Import starten";
                int confirmTextW = MeasureGameText(confirmLabel, 14);
                DrawGameText(confirmLabel, contentX + (confirmW - confirmTextW) / 2, y + 6, 14, ColorPalette.TextWhite);

                if (confirmHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    // Handel erstellen
                    if (tradeManager != null && ui.TradeSelectedCountry != null)
                    {
                        string exporterId = ui.TradeIsExport ? player.Id : ui.TradeSelectedCountry;
                        string importerId = ui.TradeIsExport ? ui.TradeSelectedCountry : player.Id;
                        tradeManager.CreateTradeAgreement(exporterId, importerId, selectedRes, ui.TradeAmount);

                        // Reset
                        ui.ShowTradeCreatePanel = false;
                        ui.TradeSelectedResource = null;
                        ui.TradeSelectedCountry = null;
                        SoundManager.Play(SoundEffect.Click);
                    }
                }
                y += 36;
            }
        }
        else
        {
            DrawGameText("Waehle eine Ressource", contentX, y, 14, ColorPalette.TextGray);
            y += 18;
        }
    }

    /// <summary>
    /// Zeichnet das Seitenpanel zur Laenderauswahl fuer den Handel
    /// </summary>
    internal static void DrawTradeCountrySelectPanel(int panelX, int panelY, int panelW, int panelH,
        Country player, TradeManager? tradeManager, GameContext? gameContext, Vector2 mousePos)
    {
        if (ui.TradeSelectedResource == null || tradeManager == null || gameContext == null) return;

        // Panel rechts neben dem Hauptpanel
        int sidePanelX = panelX + panelW + 2;
        int sidePanelW = 260;
        int sidePanelH = panelH;

        // Hintergrund (abgerundet)
        Rectangle tradeSideRect = new(sidePanelX, panelY, sidePanelW, sidePanelH);
        Rectangle tradeSideShadow = new(sidePanelX + 2, panelY + 2, sidePanelW, sidePanelH);
        Raylib.DrawRectangleRounded(tradeSideShadow, 0.02f, 6, new Color((byte)0, (byte)0, (byte)0, (byte)40));
        Raylib.DrawRectangleRounded(tradeSideRect, 0.02f, 6, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(tradeSideRect, 0.02f, 6, 2, ColorPalette.Accent);

        int contentX = sidePanelX + 12;
        int y = panelY + 15;

        // Titel
        string title = ui.TradeIsExport ? "ABNEHMER WAEHLEN" : "LIEFERANT WAEHLEN";
        DrawGameText(title, contentX, y, 14, ColorPalette.Accent);
        y += 28;

        // Trennlinie
        Raylib.DrawLine(contentX, y, sidePanelX + sidePanelW - 12, y, ColorPalette.Accent);
        y += 12;

        // Laenderliste holen (mit Cache um Flackern zu vermeiden)
        var selectedRes = ui.TradeSelectedResource.Value;
        double currentTime = Raylib.GetTime();

        // Cache aktualisieren wenn: andere Ressource, anderer Modus, oder alle 2 Sekunden
        bool needsRefresh = ui.TradePartnersCacheResource != selectedRes ||
                           ui.TradePartnersCacheIsExport != ui.TradeIsExport ||
                           (currentTime - ui.TradePartnersCacheTime) > 2.0;

        if (needsRefresh)
        {
            List<(string CountryId, double Value)> countries;
            if (ui.TradeIsExport)
            {
                countries = tradeManager.GetPotentialImporters(selectedRes, player.Id, gameContext);
            }
            else
            {
                countries = tradeManager.GetPotentialExporters(selectedRes, player.Id, gameContext);
            }

            // Kategorisiere Länder in EU und Andere
            var diplomacyManager = game.GetSystem<DiplomacyManager>();
            ui.TradePartnersEU.Clear();
            ui.TradePartnersOther.Clear();

            foreach (var entry in countries)
            {
                var alliances = diplomacyManager?.GetCountryAlliances(entry.CountryId) ?? new List<string>();
                if (alliances.Contains("EU"))
                    ui.TradePartnersEU.Add(entry);
                else
                    ui.TradePartnersOther.Add(entry);
            }

            ui.TradePartnersCache = countries;
            ui.TradePartnersCacheResource = selectedRes;
            ui.TradePartnersCacheIsExport = ui.TradeIsExport;
            ui.TradePartnersCacheTime = currentTime;
        }

        // Verwende gecachte Listen
        var euCountries = ui.TradePartnersEU;
        var otherCountries = ui.TradePartnersOther;
        var allCountries = ui.TradePartnersCache;

        if (allCountries.Count == 0)
        {
            DrawGameText("Keine Handelspartner", contentX, y, 14, ColorPalette.TextGray);
            DrawGameText("verfuegbar", contentX, y + 16, 14, ColorPalette.TextGray);
            return;
        }

        // Prüfe ob Spieler EU-Mitglied ist (nur dann Kategorien anzeigen)
        var diplomacyMgr = game.GetSystem<DiplomacyManager>();
        bool playerIsEU = diplomacyMgr?.GetCountryAlliances(player.Id).Contains("EU") ?? false;

        // Berechne Gesamthöhe (mit oder ohne Kategorie-Headern)
        int visibleHeight = sidePanelH - (y - panelY) - 15;
        int itemHeight = 36;
        int headerHeight = 28;
        int totalHeight = 0;

        if (playerIsEU)
        {
            // EU-Spieler: Mit Kategorien
            if (euCountries.Count > 0) totalHeight += headerHeight + euCountries.Count * itemHeight;
            if (otherCountries.Count > 0) totalHeight += headerHeight + otherCountries.Count * itemHeight;
        }
        else
        {
            // Nicht-EU-Spieler: Einfache Liste ohne Kategorien
            totalHeight = allCountries.Count * itemHeight;
        }
        int maxScroll = Math.Max(0, totalHeight - visibleHeight);

        // Mausrad-Scrolling
        Rectangle scrollArea = new Rectangle(sidePanelX, y, sidePanelW, visibleHeight);
        if (Raylib.CheckCollisionPointRec(mousePos, scrollArea))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                ui.TradeScrollOffset -= (int)(wheel * 30);
                ui.TradeScrollOffset = Math.Clamp(ui.TradeScrollOffset, 0, maxScroll);
            }
        }

        // Clipping
        Raylib.BeginScissorMode(sidePanelX + 1, y, sidePanelW - 2, visibleHeight);

        int drawY = y - ui.TradeScrollOffset;
        double price = game.Resources.TryGetValue(selectedRes, out var res) ? res.CurrentPrice : 0;

        // Hilfsfunktion zum Zeichnen eines Landes
        void DrawCountryItem(string countryId, double value)
        {
            if (drawY + itemHeight < y || drawY > panelY + panelH)
            {
                drawY += itemHeight;
                return;
            }

            if (!game.Countries.TryGetValue(countryId, out var country))
            {
                drawY += itemHeight;
                return;
            }

            Rectangle itemRect = new Rectangle(contentX, drawY, sidePanelW - 26, itemHeight - 4);
            bool itemHovered = Raylib.CheckCollisionPointRec(mousePos, itemRect);
            bool itemSelected = ui.TradeSelectedCountry == countryId;

            Color itemBg = itemSelected ? ColorPalette.Accent :
                           itemHovered ? ColorPalette.PanelLight : ColorPalette.Panel;
            Raylib.DrawRectangleRec(itemRect, itemBg);
            Raylib.DrawRectangleLinesEx(itemRect, 1, itemSelected ? ColorPalette.Accent : ColorPalette.PanelLight);

            // Flagge
            DrawFlag(countryId, contentX + 4, drawY + 4, 18);

            // Landesname
            DrawGameText(country.Name, contentX + 28, drawY + 4, 14, ColorPalette.TextWhite);

            // Verfuegbare Menge / Bedarf
            string valueLabel = ui.TradeIsExport ? $"Bedarf: {value:F0}" : $"Verf.: {value:F0}";
            DrawGameText(valueLabel, contentX + 28, drawY + 20, 14, ColorPalette.TextGray);

            // Preis pro Einheit
            DrawGameText($"${price:F0}M", contentX + 195, drawY + 10, 14, ColorPalette.TextGray);

            if (itemHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                ui.TradeSelectedCountry = countryId;
                SoundManager.Play(SoundEffect.Click);
            }

            drawY += itemHeight;
        }

        // Hilfsfunktion zum Zeichnen eines Kategorie-Headers
        void DrawCategoryHeader(string title, Color accentColor)
        {
            if (drawY + headerHeight >= y && drawY <= panelY + panelH)
            {
                Raylib.DrawRectangle(contentX, drawY, sidePanelW - 26, headerHeight - 4, ColorPalette.Background);
                Raylib.DrawLine(contentX, drawY + headerHeight - 6, contentX + sidePanelW - 30, drawY + headerHeight - 6, accentColor);
                DrawGameText(title, contentX + 4, drawY + 4, 14, accentColor);
            }
            drawY += headerHeight;
        }

        if (playerIsEU)
        {
            // EU-Spieler: Kategorisierte Ansicht
            // EU-Länder zuerst (mit blauem EU-Akzent)
            if (euCountries.Count > 0)
            {
                DrawCategoryHeader($"EU-Laender ({euCountries.Count})", new Color(0, 51, 153, 255)); // EU-Blau
                foreach (var (countryId, value) in euCountries)
                {
                    DrawCountryItem(countryId, value);
                }
            }

            // Andere Länder
            if (otherCountries.Count > 0)
            {
                DrawCategoryHeader($"Andere Laender ({otherCountries.Count})", ColorPalette.TextGray);
                foreach (var (countryId, value) in otherCountries)
                {
                    DrawCountryItem(countryId, value);
                }
            }
        }
        else
        {
            // Nicht-EU-Spieler: Einfache Liste ohne Kategorien
            foreach (var (countryId, value) in allCountries)
            {
                DrawCountryItem(countryId, value);
            }
        }

        Raylib.EndScissorMode();

        // Scrollbar
        if (totalHeight > visibleHeight)
        {
            int scrollBarH = Math.Max(20, visibleHeight * visibleHeight / totalHeight);
            int scrollBarY = y + (maxScroll > 0 ? (int)((float)ui.TradeScrollOffset / maxScroll * (visibleHeight - scrollBarH)) : 0);
            int scrollBarX = sidePanelX + sidePanelW - 6;
            Raylib.DrawRectangle(scrollBarX, scrollBarY, 4, scrollBarH, ColorPalette.PanelLight);
        }
    }

    /// <summary>
    /// Zeichnet das Bau-Seiten-Panel rechts neben dem Produktions-Panel
    /// </summary>
    internal static void DrawBuildSidePanel()
    {
        var (panelX, panelY, panelW, panelH) = GetTopMenuPanelRect();
        var player = game.PlayerCountry!;
        Vector2 mousePos = _cachedMousePos;

        // Panel-Position: rechts neben dem Hauptpanel
        int buildPanelX = panelX + panelW + 2;
        int buildPanelW = 280;
        int buildPanelH = panelH;

        // Panel-Hintergrund (abgerundet)
        Rectangle buildSideRect = new(buildPanelX, panelY, buildPanelW, buildPanelH);
        Rectangle buildSideShadow = new(buildPanelX + 2, panelY + 2, buildPanelW, buildPanelH);
        Raylib.DrawRectangleRounded(buildSideShadow, 0.02f, 6, new Color((byte)0, (byte)0, (byte)0, (byte)40));
        Raylib.DrawRectangleRounded(buildSideRect, 0.02f, 6, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(buildSideRect, 0.02f, 6, 2, ColorPalette.Accent);

        int contentX = buildPanelX + 15;
        int y = panelY + 15;

        // Titel
        DrawGameText("BAUEN", contentX, y, 30, ColorPalette.Accent);
        y += 38;

        // Trennlinie
        Raylib.DrawLine(contentX, y, buildPanelX + buildPanelW - 15, y, ColorPalette.Accent);
        y += 15;

        // === FABRIK BAUEN ===
        DrawGameText("FABRIK BAUEN", contentX, y, 14, ColorPalette.Accent);
        y += 24;

        double civilianCost = 1_000;      // 1 Mrd.
        double militaryCost = 1_500;      // 1.5 Mrd.
        double dockyardCost = 900;        // 900 Mio.

        bool DrawFactoryButton(int bx, int by, int bw, int bh, string label, double cost, bool canAfford)
        {
            Rectangle btnRect = new Rectangle(bx, by, bw, bh);
            bool isHovered = Raylib.CheckCollisionPointRec(mousePos, btnRect);

            Color bgColor = !canAfford ? ColorPalette.PanelLight :
                            isHovered ? ColorPalette.Accent : ColorPalette.Panel;
            Color borderColor = !canAfford ? ColorPalette.TextGray : ColorPalette.Accent;
            Color textColor = !canAfford ? ColorPalette.TextGray : ColorPalette.TextWhite;

            Raylib.DrawRectangleRec(btnRect, bgColor);
            Raylib.DrawRectangleLinesEx(btnRect, 1, borderColor);

            DrawGameText(label, bx + 5, by + 3, 14, textColor);
            DrawGameText(Formatting.Money(cost), bx + 5, by + 18, 14, canAfford ? ColorPalette.Green : ColorPalette.Red);

            return isHovered && canAfford && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }

        int btnW = 78;
        int btnH = 34;
        int btnSpacing = 6;

        bool canAffordCivilian = player.Budget >= civilianCost;
        bool canAffordMilitary = player.Budget >= militaryCost;
        bool canAffordDockyard = player.Budget >= dockyardCost;

        if (DrawFactoryButton(contentX, y, btnW, btnH, "Zivil", civilianCost, canAffordCivilian))
        {
            ui.FactoryBuildMode = FactoryType.Civilian;
            ui.ActiveTopMenuPanel = TopMenuPanel.None;
            ui.ShowBuildPanel = false;
            SoundManager.Play(SoundEffect.Click);
        }

        if (DrawFactoryButton(contentX + btnW + btnSpacing, y, btnW, btnH, "Militaer", militaryCost, canAffordMilitary))
        {
            ui.FactoryBuildMode = FactoryType.Military;
            ui.ActiveTopMenuPanel = TopMenuPanel.None;
            ui.ShowBuildPanel = false;
            SoundManager.Play(SoundEffect.Click);
        }

        if (DrawFactoryButton(contentX + (btnW + btnSpacing) * 2, y, btnW, btnH, "Werft", dockyardCost, canAffordDockyard))
        {
            ui.FactoryBuildMode = FactoryType.Dockyard;
            ui.ActiveTopMenuPanel = TopMenuPanel.None;
            ui.ShowBuildPanel = false;
            SoundManager.Play(SoundEffect.Click);
        }

        y += btnH + 20;

        // === MINE BAUEN ===
        DrawGameText("MINE BAUEN", contentX, y, 14, ColorPalette.Accent);
        y += 24;

        bool DrawMineButton(int bx, int by, int bw, int bh, string label, double cost, bool canAfford, MineType mineType)
        {
            Rectangle btnRectMine = new Rectangle(bx, by, bw, bh);
            bool isMineHovered = Raylib.CheckCollisionPointRec(mousePos, btnRectMine);

            Color bgColorMine = !canAfford ? ColorPalette.PanelLight :
                            isMineHovered ? ColorPalette.Accent : ColorPalette.Panel;
            Color borderColorMine = !canAfford ? ColorPalette.TextGray : Mine.GetMapColor(mineType);
            Color textColorMine = !canAfford ? ColorPalette.TextGray : ColorPalette.TextWhite;

            Raylib.DrawRectangleRec(btnRectMine, bgColorMine);
            Raylib.DrawRectangleLinesEx(btnRectMine, 2, borderColorMine);

            DrawGameText(label, bx + 5, by + 3, 14, textColorMine);
            DrawGameText(Formatting.Money(cost), bx + 5, by + 18, 14, canAfford ? ColorPalette.Green : ColorPalette.Red);

            return isMineHovered && canAfford && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }

        int mineBtnW = 78;
        int mineBtnH = 32;
        int mineBtnSpacing = 6;

        var mineTypes = new[]
        {
            (MineType.OilWell, "Oel"),
            (MineType.GasDrill, "Gas"),
            (MineType.CoalMine, "Kohle"),
            (MineType.IronMine, "Eisen"),
            (MineType.CopperMine, "Kupfer"),
            (MineType.UraniumMine, "Uran")
        };

        for (int i = 0; i < mineTypes.Length; i++)
        {
            var (mineType, label) = mineTypes[i];
            double mineCost = Mine.GetBuildCost(mineType);
            bool canAffordMine = player.Budget >= mineCost;

            int col = i % 3;
            int row = i / 3;
            int bx = contentX + col * (mineBtnW + mineBtnSpacing);
            int by = y + row * (mineBtnH + 4);

            if (DrawMineButton(bx, by, mineBtnW, mineBtnH, label, mineCost, canAffordMine, mineType))
            {
                ui.MineBuildMode = mineType;
                ui.ActiveTopMenuPanel = TopMenuPanel.None;
                ui.ShowBuildPanel = false;
                SoundManager.Play(SoundEffect.Click);
            }
        }

        y += (mineBtnH + 4) * 2 + 15;

        // Trennlinie
        Raylib.DrawLine(contentX, y, buildPanelX + buildPanelW - 15, y, ColorPalette.PanelLight);
        y += 12;

        // Hinweistext
        DrawGameText("Klick auf Provinz", contentX, y, 14, ColorPalette.TextGray);
        y += 16;
        DrawGameText("zum Bauen", contentX, y, 14, ColorPalette.TextGray);
    }

    /// <summary>
    /// Holt Beziehungsdaten aus dem DiplomacyManager
    /// </summary>
    internal static List<(string Name, int Relation, string Id)> GetDiplomaticRelationsFromManager(string playerId, DiplomacyManager? diplomacyManager)
    {
        var result = new List<(string Name, int Relation, string Id)>();

        if (diplomacyManager == null) return result;

        // Wichtige Länder für die Anzeige (sortiert nach Relevanz)
        var importantCountries = new[] { "USA", "CHN", "RUS", "DEU", "FRA", "GBR", "JPN", "IND", "BRA", "ITA", "POL", "UKR" };

        foreach (var countryId in importantCountries)
        {
            if (countryId == playerId) continue;
            if (!game.Countries.TryGetValue(countryId, out var country)) continue;

            int relation = diplomacyManager.GetRelation(playerId, countryId);
            result.Add((country.Name, relation, countryId));
        }

        // Nach Beziehungswert sortieren (beste zuerst)
        return result.OrderByDescending(r => r.Relation).Take(12).ToList();
    }

    /// <summary>
    /// Zeichnet einen Button zum Aktivieren des Rekrutierungsmodus
    /// </summary>
    internal static int DrawRecruitModeButton(int x, int y, int width, UnitType type, string name, string info, Vector2 mousePos, Country player)
    {
        int btnH = 32;
        Rectangle btnRect = new Rectangle(x, y, width, btnH);
        bool hovered = Raylib.CheckCollisionPointRec(mousePos, btnRect);

        // Pruefe ob genug Geld vorhanden
        var (_, cost) = MilitaryUnit.GetRecruitmentCost(type);
        bool hasEnoughMoney = player.Budget >= cost;

        Color btnBg = !hasEnoughMoney ? ColorPalette.PanelLight :
                      hovered ? new Color((byte)80, (byte)100, (byte)60, (byte)255) :
                      new Color((byte)60, (byte)80, (byte)50, (byte)255);
        Color btnBorder = hasEnoughMoney ? ColorPalette.Green : ColorPalette.TextGray;
        Color textColor = hasEnoughMoney ? ColorPalette.TextWhite : ColorPalette.TextGray;

        Raylib.DrawRectangleRec(btnRect, btnBg);
        Raylib.DrawRectangleLinesEx(btnRect, 1, btnBorder);

        // Icon (falls vorhanden)
        var icon = TextureManager.GetMilitaryUnit(type);
        int textX = x + 8;
        if (icon != null)
        {
            Raylib.DrawTexturePro(icon.Value,
                new Rectangle(0, 0, icon.Value.Width, icon.Value.Height),
                new Rectangle(x + 4, y + 4, 24, 24),
                new System.Numerics.Vector2(0, 0), 0, Color.White);
            textX = x + 32;
        }

        DrawGameText(name, textX, y + 4, 14, textColor);
        DrawGameText(info, textX, y + 18, 12, ColorPalette.TextGray);

        // Klick aktiviert Rekrutierungsmodus
        if (hasEnoughMoney && hovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.RecruitmentMode = type;
        }

        return y + btnH + 4;
    }
}
