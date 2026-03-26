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
/// Program - Karten-Rendering: Handelsrouten, Allianzen, Heatmap, MapView, Minen, Konflikte
/// </summary>
partial class Program
{
    /// <summary>
    /// Zeichnet Handelsrouten als Pfeile auf der Karte
    /// </summary>
    static void DrawTradeRoutes(string playerId)
    {
        var tradeManager = _mgr.Trade;
        if (tradeManager == null) return;

        var agreements = tradeManager.GetTradeAgreements();
        if (agreements.Count == 0) return;

        // Aktiver Ressourcen-Filter
        var filter = ui.TradeRouteFilter;

        // Gruppiere Handelsabkommen nach Routen (wiederverwendeter Cache)
        _cache.TradeRoutes.Clear();
        foreach (var agreement in agreements)
        {
            if (!agreement.IsActive) continue;

            // Ressourcen-Filter anwenden
            if (filter != null && agreement.ResourceType != filter.Value) continue;

            var key = (agreement.ExporterId, agreement.ImporterId);
            if (!_cache.TradeRoutes.TryGetValue(key, out var list))
            {
                list = new List<TradeAgreement>();
                _cache.TradeRoutes[key] = list;
            }
            list.Add(agreement);
        }

        // Zeichne jede Route als Pfeil
        foreach (var ((fromId, toId), agreementList) in _cache.TradeRoutes)
        {
            // Hole Laender-Mittelpunkte
            if (!worldMap.Regions.TryGetValue(fromId, out var fromRegion)) continue;
            if (!worldMap.Regions.TryGetValue(toId, out var toRegion)) continue;

            Vector2 fromPos = worldMap.MapToScreen(fromRegion.LabelPosition);
            Vector2 toPos = worldMap.MapToScreen(toRegion.LabelPosition);

            // Berechne Gesamtwert dieser Route
            double totalValue = 0;
            foreach (var agr in agreementList)
            {
                double price = game.Resources.TryGetValue(agr.ResourceType, out var res) ? res.CurrentPrice : 0;
                totalValue += price * agr.Amount;
            }

            // Pfeilfarbe basierend auf Spieler-Beteiligung
            Color arrowColor;
            float lineWidth;
            bool isPlayerRoute = fromId == playerId || toId == playerId;

            if (fromId == playerId)
            {
                // Export vom Spieler - Gruen
                arrowColor = new Color((byte)50, (byte)230, (byte)50, (byte)255);
                lineWidth = 3f;
            }
            else if (toId == playerId)
            {
                // Import zum Spieler - Rot/Orange
                arrowColor = new Color((byte)240, (byte)100, (byte)40, (byte)255);
                lineWidth = 3f;
            }
            else
            {
                // Handel zwischen anderen Laendern - Blau
                arrowColor = new Color((byte)80, (byte)160, (byte)255, (byte)200);
                lineWidth = 1.5f;
            }

            // Liniendicke basierend auf Handelswert skalieren
            float valueScale = Math.Min(1f + (float)(totalValue / 1000), 3f);
            lineWidth *= valueScale;

            // Zeichne den Pfeil
            DrawTradeArrow(fromPos, toPos, arrowColor, lineWidth, isPlayerRoute);

            // Ressourcen-Icons und Wert-Label am Mittelpunkt der Kurve anzeigen
            if (worldMap.Zoom >= 0.5f)
            {
                // Bezier-Kurvenmittelpunkt berechnen (wie in DrawTradeArrow)
                Vector2 dir = toPos - fromPos;
                float length = dir.Length();
                if (length >= 20)
                {
                    Vector2 dirN = Vector2.Normalize(dir);
                    Vector2 normal = new Vector2(-dirN.Y, dirN.X);
                    float curveAmount = length * 0.15f;
                    Vector2 midPoint = (fromPos + toPos) / 2 + normal * curveAmount * 0.5f;

                    // Sammle einzigartige Ressourcen-Typen dieser Route (wiederverwendeter Cache)
                    _cache.TradeRouteResTypes.Clear();
                    for (int ai = 0; ai < agreementList.Count; ai++)
                    {
                        var rt = agreementList[ai].ResourceType;
                        if (!_cache.TradeRouteResTypes.Contains(rt))
                            _cache.TradeRouteResTypes.Add(rt);
                    }
                    var resourceTypes = _cache.TradeRouteResTypes;

                    // Icons zeichnen
                    int iconSize = 18;
                    int iconSpacing = 2;
                    int totalIconsW = resourceTypes.Count * iconSize + (resourceTypes.Count - 1) * iconSpacing;
                    int iconStartX = (int)midPoint.X - totalIconsW / 2;
                    int iconY = (int)midPoint.Y - iconSize - 4;

                    // Hintergrund fuer Icons
                    Raylib.DrawRectangle(iconStartX - 3, iconY - 2, totalIconsW + 6, iconSize + 4,
                        new Color((byte)20, (byte)20, (byte)30, (byte)210));
                    Raylib.DrawRectangleLinesEx(
                        new Rectangle(iconStartX - 3, iconY - 2, totalIconsW + 6, iconSize + 4),
                        1, new Color(arrowColor.R, arrowColor.G, arrowColor.B, (byte)120));

                    for (int ri = 0; ri < resourceTypes.Count; ri++)
                    {
                        DrawResourceIcon(resourceTypes[ri],
                            iconStartX + ri * (iconSize + iconSpacing), iconY, iconSize);
                    }

                    // Wert-Label fuer ALLE Routen anzeigen
                    {
                        // Laendernamen fuer KI-KI-Routen anzeigen
                        string fromName = game.Countries.TryGetValue(fromId, out var fromC) ? fromC.Name : fromId;
                        string toName = game.Countries.TryGetValue(toId, out var toC) ? toC.Name : toId;
                        string routeLabel = isPlayerRoute
                            ? $"${totalValue:N0}M"
                            : $"{fromName} -> {toName}  ${totalValue:N0}M";
                        int textW = MeasureTextCached(routeLabel, 10);

                        Raylib.DrawRectangle((int)midPoint.X - textW / 2 - 2, (int)midPoint.Y - 1, textW + 4, 13,
                            new Color((byte)20, (byte)20, (byte)30, (byte)200));
                        DrawGameText(routeLabel, (int)midPoint.X - textW / 2, (int)midPoint.Y, 11, arrowColor);
                    }
                }
            }
        }

        // Legende zeichnen
        DrawTradeRouteLegend();
    }

    /// <summary>
    /// Zeichnet einen einzelnen Handelspfeil
    /// </summary>
    static void DrawTradeArrow(Vector2 from, Vector2 to, Color color, float lineWidth, bool highlight)
    {
        // Richtungsvektor
        Vector2 dir = to - from;
        float length = dir.Length();
        if (length < 20) return; // Zu kurz

        dir = Vector2.Normalize(dir);

        // Pfeil kuerzen, damit er nicht in der Mitte der Laender endet
        float shortenAmount = 20f * worldMap.Zoom;
        shortenAmount = Math.Min(shortenAmount, length * 0.3f);

        Vector2 start = from + dir * shortenAmount;
        Vector2 end = to - dir * shortenAmount;

        // Gebogene Linie (Bezier) fuer bessere Optik
        Vector2 normal = new Vector2(-dir.Y, dir.X);
        float curveAmount = length * 0.15f;
        Vector2 control = (start + end) / 2 + normal * curveAmount;

        // Zeichne gebogene Linie als Segmente
        int segments = Math.Max(20, (int)(length / 10));

        Vector2 prevPoint = start;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float u = 1 - t;
            Vector2 point = u * u * start + 2 * u * t * control + t * t * end;

            // Basis-Linie (leicht transparent)
            Color baseColor = new Color(color.R, color.G, color.B, (byte)(color.A * 0.4f));
            Raylib.DrawLineEx(prevPoint, point, lineWidth, baseColor);
            prevPoint = point;
        }

        // Kurvenlänge in Map-Koordinaten (zoom-unabhaengig) fuer stabile Timing-Berechnung
        float mapLength = 0;
        Vector2 prevPt = start;
        for (int ci = 1; ci <= 20; ci++)
        {
            float ct = (float)ci / 20;
            float cu = 1 - ct;
            Vector2 pt = cu * cu * start + 2 * cu * ct * control + ct * ct * end;
            mapLength += Vector2.Distance(prevPt, pt);
            prevPt = pt;
        }
        // Durch Zoom teilen -> stabile Laenge unabhaengig vom Zoom-Level
        float stableLength = mapLength / Math.Max(0.01f, worldMap.Zoom);

        // Konstante Geschwindigkeit in Map-Einheiten pro Spielstunde
        const float ballSpeed = 20f;
        float travelHours = Math.Max(1f, stableLength / ballSpeed);
        float totalHours = game.TotalDays * 24f + game.Hour + game.Minute / 60f;
        float ballT = (totalHours % travelHours) / travelHours;

        float bu = 1 - ballT;
        Vector2 ballPos = bu * bu * start + 2 * bu * ballT * control + ballT * ballT * end;

        // Aeusserer Glow
        float glowRadius = lineWidth * 1.25f + 3f;
        Color glowCol = new Color(color.R, color.G, color.B, (byte)35);
        Raylib.DrawCircleV(ballPos, glowRadius, glowCol);

        // Mittlerer Glow
        float midRadius = lineWidth * 0.75f + 1.5f;
        Color midCol = new Color(color.R, color.G, color.B, (byte)100);
        Raylib.DrawCircleV(ballPos, midRadius, midCol);

        // Heller Kern
        float coreRadius = lineWidth * 0.4f + 0.75f;
        Color brightColor = new Color(
            (byte)Math.Min(color.R + 120, 255),
            (byte)Math.Min(color.G + 120, 255),
            (byte)Math.Min(color.B + 120, 255),
            (byte)255);
        Raylib.DrawCircleV(ballPos, coreRadius, brightColor);

        // Weisser Mittelpunkt
        Raylib.DrawCircleV(ballPos, coreRadius * 0.4f, new Color((byte)255, (byte)255, (byte)255, (byte)200));

        // Pfeilspitze am Ende
        Vector2 arrowDir = Vector2.Normalize(end - control);
        float arrowSize = Math.Max(12f, lineWidth * 4);

        Vector2 arrowLeft = end - arrowDir * arrowSize + new Vector2(-arrowDir.Y, arrowDir.X) * arrowSize * 0.6f;
        Vector2 arrowRight = end - arrowDir * arrowSize - new Vector2(-arrowDir.Y, arrowDir.X) * arrowSize * 0.6f;

        // Raylib DrawTriangle braucht counter-clockwise Reihenfolge
        Raylib.DrawTriangle(end, arrowRight, arrowLeft, color);

        // Highlight-Effekt fuer Spieler-Routen
        if (highlight)
        {
            Color glowColor = new Color(color.R, color.G, color.B, (byte)50);
            Raylib.DrawLineEx(end - arrowDir * arrowSize, end, lineWidth + 4, glowColor);
        }
    }

    /// <summary>
    /// Zeichnet die Legende fuer die Handelsansicht
    /// </summary>
    static void DrawTradeRouteLegend()
    {
        int legendX = 15;
        int legendY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT - 100;
        int legendW = 150;
        int legendH = 85;

        // Hintergrund
        Raylib.DrawRectangle(legendX, legendY, legendW, legendH, new Color((byte)30, (byte)30, (byte)40, (byte)220));
        Raylib.DrawRectangleLinesEx(new Rectangle(legendX, legendY, legendW, legendH), 1, ColorPalette.PanelLight);

        int y = legendY + 8;
        DrawGameText("HANDELSROUTEN", legendX + 8, y, 11, ColorPalette.Accent);
        y += 18;

        // Export (Gruen)
        Color exportColor = new Color((byte)100, (byte)200, (byte)100, (byte)255);
        Raylib.DrawLine(legendX + 8, y + 6, legendX + 30, y + 6, exportColor);
        Raylib.DrawTriangle(
            new Vector2(legendX + 34, y + 6),
            new Vector2(legendX + 28, y + 10),
            new Vector2(legendX + 28, y + 2),
            exportColor
        );
        DrawGameText("Deine Exporte", legendX + 42, y, 11, ColorPalette.TextWhite);
        y += 18;

        // Import (Orange)
        Color importColor = new Color((byte)200, (byte)120, (byte)80, (byte)255);
        Raylib.DrawLine(legendX + 8, y + 6, legendX + 30, y + 6, importColor);
        Raylib.DrawTriangle(
            new Vector2(legendX + 34, y + 6),
            new Vector2(legendX + 28, y + 10),
            new Vector2(legendX + 28, y + 2),
            importColor
        );
        DrawGameText("Deine Importe", legendX + 42, y, 11, ColorPalette.TextWhite);
        y += 18;

        // Andere (Blau/Grau)
        Color otherColor = new Color((byte)100, (byte)150, (byte)200, (byte)200);
        Raylib.DrawLine(legendX + 8, y + 6, legendX + 30, y + 6, otherColor);
        Raylib.DrawTriangle(
            new Vector2(legendX + 34, y + 6),
            new Vector2(legendX + 28, y + 10),
            new Vector2(legendX + 28, y + 2),
            otherColor
        );
        DrawGameText("Anderer Handel", legendX + 42, y, 11, ColorPalette.TextGray);
    }

    // Ressourcen-Filter fuer Handelsrouten
    private static readonly (ResourceType? Type, string Name)[] _tradeRouteFilters =
    {
        (null, "Alle"),
        (ResourceType.Oil, "Erdoel"),
        (ResourceType.NaturalGas, "Erdgas"),
        (ResourceType.Coal, "Kohle"),
        (ResourceType.Iron, "Eisen"),
        (ResourceType.Copper, "Kupfer"),
        (ResourceType.Uranium, "Uran"),
        (ResourceType.Food, "Nahrung"),
        (ResourceType.Steel, "Stahl"),
        (ResourceType.Electronics, "Elektronik"),
        (ResourceType.Machinery, "Maschinen"),
        (ResourceType.ConsumerGoods, "Konsumgueter")
    };

    /// <summary>
    /// Zeichnet den Ressourcen-Filter fuer Handelsrouten links unten (unter der Legende)
    /// </summary>
    static void DrawTradeRouteFilter()
    {
        Vector2 mousePos = _cachedMousePos;

        int panelX = 15;
        int itemH = 24;
        int panelW = 150;
        int headerH = 22;
        int panelH = headerH + _tradeRouteFilters.Length * itemH + 8;
        // Positioniere unter der Handelsrouten-Legende (die ist 85px hoch + 20px Abstand)
        int panelY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT - 100 - panelH - 8;

        // Hintergrund
        Raylib.DrawRectangle(panelX, panelY, panelW, panelH, new Color((byte)30, (byte)30, (byte)40, (byte)220));
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelW, panelH), 1, ColorPalette.PanelLight);

        int y = panelY + 4;
        DrawGameText("FILTER", panelX + 8, y, 11, ColorPalette.Accent);
        y += headerH;

        for (int i = 0; i < _tradeRouteFilters.Length; i++)
        {
            var (type, name) = _tradeRouteFilters[i];
            bool isActive = ui.TradeRouteFilter == type;

            Rectangle itemRect = new Rectangle(panelX + 4, y, panelW - 8, itemH - 2);
            bool isHovered = Raylib.CheckCollisionPointRec(mousePos, itemRect);

            if (isActive)
            {
                Raylib.DrawRectangleRec(itemRect, ColorPalette.Accent);
            }
            else if (isHovered)
            {
                Raylib.DrawRectangleRec(itemRect, ColorPalette.PanelLight);
            }

            // Ressourcen-Icon (kleines Quadrat mit Farbe)
            Color indicatorColor = type switch
            {
                ResourceType.Oil => new Color((byte)40, (byte)40, (byte)40, (byte)255),
                ResourceType.NaturalGas => new Color((byte)100, (byte)180, (byte)255, (byte)255),
                ResourceType.Coal => new Color((byte)80, (byte)80, (byte)80, (byte)255),
                ResourceType.Iron => new Color((byte)180, (byte)120, (byte)80, (byte)255),
                ResourceType.Copper => new Color((byte)200, (byte)130, (byte)50, (byte)255),
                ResourceType.Uranium => new Color((byte)80, (byte)220, (byte)80, (byte)255),
                ResourceType.Food => new Color((byte)180, (byte)200, (byte)60, (byte)255),
                ResourceType.Steel => new Color((byte)160, (byte)160, (byte)180, (byte)255),
                ResourceType.Electronics => new Color((byte)60, (byte)140, (byte)220, (byte)255),
                ResourceType.Machinery => new Color((byte)180, (byte)160, (byte)100, (byte)255),
                ResourceType.ConsumerGoods => new Color((byte)220, (byte)120, (byte)180, (byte)255),
                _ => ColorPalette.Accent
            };
            Raylib.DrawRectangle(panelX + 10, y + 4, 12, 14, indicatorColor);
            Raylib.DrawRectangleLinesEx(new Rectangle(panelX + 10, y + 4, 12, 14), 1, ColorPalette.PanelLight);

            // Name
            Color textColor = isActive ? Color.White : (isHovered ? ColorPalette.TextWhite : ColorPalette.TextGray);
            DrawGameText(name, panelX + 28, y + 4, 11, textColor);

            if (isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                ui.TradeRouteFilter = type;
                SoundManager.Play(SoundEffect.Click);
            }

            y += itemH;
        }
    }

    /// <summary>
    /// Zeichnet die Legende fuer die Buendnis-Ansicht
    /// </summary>
    static void DrawAllianceLegend()
    {
        int legendX = 15;
        int legendW = 150;
        int itemH = 18;
        int headerH = 22;
        int tabH = 28;

        Vector2 mousePos = _cachedMousePos;

        // Buendnisse basierend auf aktuellem Typ filtern
        var alliances = ui.CurrentAllianceView == AllianceViewType.Military
            ? new (string Name, Color Color)[]
            {
                ("NATO", WorldMap.AllianceColors["NATO"]),
                ("CSTO", WorldMap.AllianceColors["CSTO"]),
                ("SCO", WorldMap.AllianceColors["SCO"]),
                ("Neutral", WorldMap.AllianceColors["None"])
            }
            : new (string Name, Color Color)[]
            {
                ("EU", WorldMap.AllianceColors["EU"]),
                ("BRICS", WorldMap.AllianceColors["BRICS"]),
                ("ASEAN", WorldMap.AllianceColors["ASEAN"]),
                ("AU", WorldMap.AllianceColors["AU"]),
                ("OPEC", WorldMap.AllianceColors["OPEC"]),
                ("Neutral", WorldMap.AllianceColors["None"])
            };

        int legendH = headerH + alliances.Length * itemH + tabH + 18;
        int legendY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT - legendH - 8;

        // Hintergrund
        Raylib.DrawRectangle(legendX, legendY, legendW, legendH, new Color((byte)30, (byte)30, (byte)40, (byte)220));
        Raylib.DrawRectangleLinesEx(new Rectangle(legendX, legendY, legendW, legendH), 1, ColorPalette.PanelLight);

        int y = legendY + 6;
        DrawGameText("BUENDNISSE", legendX + 8, y, 11, ColorPalette.Accent);
        y += headerH;

        // Zuerst die Buendnis-Liste zeichnen
        foreach (var (name, color) in alliances)
        {
            // Farbquadrat
            Raylib.DrawRectangle(legendX + 8, y + 2, 11, 14, color);
            Raylib.DrawRectangleLinesEx(new Rectangle(legendX + 8, y + 2, 11, 14), 1, ColorPalette.PanelLight);

            // Name
            DrawGameText(name, legendX + 28, y + 1, 11, ColorPalette.TextWhite);
            y += itemH;
        }

        // Trennlinie
        y += 6;
        Raylib.DrawLine(legendX + 8, y, legendX + legendW - 8, y, ColorPalette.PanelLight);
        y += 6;

        // Tab-Buttons fuer Militaer/Wirtschaft (unter der Liste)
        int tabW = (legendW - 16) / 2;
        Rectangle milTab = new Rectangle(legendX + 6, y, tabW, tabH - 6);
        Rectangle ecoTab = new Rectangle(legendX + 8 + tabW, y, tabW, tabH - 6);

        bool hoverMil = Raylib.CheckCollisionPointRec(mousePos, milTab);
        bool hoverEco = Raylib.CheckCollisionPointRec(mousePos, ecoTab);
        bool isMilActive = ui.CurrentAllianceView == AllianceViewType.Military;

        // Militaer-Tab
        Color milBg = isMilActive ? ColorPalette.Accent : (hoverMil ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(milTab, milBg);
        Raylib.DrawRectangleLinesEx(milTab, 1, isMilActive ? ColorPalette.Accent : ColorPalette.PanelLight);
        DrawGameText("Militaer", (int)milTab.X + 8, (int)milTab.Y + 5, 11, isMilActive ? Color.White : ColorPalette.TextGray);

        // Wirtschaft-Tab
        Color ecoBg = !isMilActive ? ColorPalette.Accent : (hoverEco ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(ecoTab, ecoBg);
        Raylib.DrawRectangleLinesEx(ecoTab, 1, !isMilActive ? ColorPalette.Accent : ColorPalette.PanelLight);
        DrawGameText("Wirtschaft", (int)ecoTab.X + 4, (int)ecoTab.Y + 5, 11, !isMilActive ? Color.White : ColorPalette.TextGray);

        // Tab-Klicks verarbeiten
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (hoverMil && !isMilActive)
            {
                ui.CurrentAllianceView = AllianceViewType.Military;
                WorldMap.CurrentAllianceViewType = AllianceViewType.Military;
                SoundManager.Play(SoundEffect.Click);
            }
            else if (hoverEco && isMilActive)
            {
                ui.CurrentAllianceView = AllianceViewType.Economic;
                WorldMap.CurrentAllianceViewType = AllianceViewType.Economic;
                SoundManager.Play(SoundEffect.Click);
            }
        }
    }

    // Ressourcen-Filter fuer Heatmap
    private static readonly (ResourceType? Type, string Name)[] _heatmapFilters =
    {
        (null, "Alle"),
        (ResourceType.Oil, "Erdoel"),
        (ResourceType.NaturalGas, "Erdgas"),
        (ResourceType.Coal, "Kohle"),
        (ResourceType.Iron, "Eisen"),
        (ResourceType.Copper, "Kupfer"),
        (ResourceType.Uranium, "Uran")
    };

    /// <summary>
    /// Zeichnet den Ressourcen-Heatmap-Filter links unten
    /// </summary>
    static void DrawResourceHeatmapFilter()
    {
        Vector2 mousePos = _cachedMousePos;

        int panelX = 15;
        int itemH = 26;
        int panelW = 150;
        int headerH = 24;
        int panelH = headerH + _heatmapFilters.Length * itemH + 12;
        int panelY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT - panelH - 20;

        // Hintergrund
        Raylib.DrawRectangle(panelX, panelY, panelW, panelH, new Color((byte)30, (byte)30, (byte)40, (byte)220));
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelW, panelH), 1, ColorPalette.PanelLight);

        int y = panelY + 6;
        DrawGameText("RESSOURCEN", panelX + 8, y, 11, ColorPalette.Accent);
        y += headerH;

        for (int i = 0; i < _heatmapFilters.Length; i++)
        {
            var (type, name) = _heatmapFilters[i];
            bool isActive = ui.HeatmapResource == type;

            Rectangle itemRect = new Rectangle(panelX + 4, y, panelW - 8, itemH - 2);
            bool isHovered = Raylib.CheckCollisionPointRec(mousePos, itemRect);

            // Hintergrund bei Hover/Active
            if (isActive)
            {
                Raylib.DrawRectangleRec(itemRect, ColorPalette.Accent);
            }
            else if (isHovered)
            {
                Raylib.DrawRectangleRec(itemRect, ColorPalette.PanelLight);
            }

            // Farb-Indikator (kleines Quadrat)
            Color indicatorColor = type switch
            {
                ResourceType.Oil => new Color((byte)40, (byte)40, (byte)40, (byte)255),
                ResourceType.NaturalGas => new Color((byte)100, (byte)180, (byte)255, (byte)255),
                ResourceType.Coal => new Color((byte)80, (byte)80, (byte)80, (byte)255),
                ResourceType.Iron => new Color((byte)180, (byte)120, (byte)80, (byte)255),
                ResourceType.Copper => new Color((byte)200, (byte)130, (byte)50, (byte)255),
                ResourceType.Uranium => new Color((byte)80, (byte)220, (byte)80, (byte)255),
                _ => ColorPalette.Accent
            };
            Raylib.DrawRectangle(panelX + 10, y + 5, 12, 14, indicatorColor);
            Raylib.DrawRectangleLinesEx(new Rectangle(panelX + 10, y + 5, 12, 14), 1, ColorPalette.PanelLight);

            // Name
            Color textColor = isActive ? Color.White : (isHovered ? ColorPalette.TextWhite : ColorPalette.TextGray);
            DrawGameText(name, panelX + 28, y + 5, 11, textColor);

            // Klick
            if (isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                ui.HeatmapResource = type;
                SoundManager.Play(SoundEffect.Click);
            }

            y += itemH;
        }

        // Heatmap-Skala zeichnen
        y += 4;
        int scaleX = panelX;
        int scaleW = panelW;
        int scaleH = 24;
        int scaleY = panelY + panelH + 4;

        // Farbverlauf zeichnen
        Raylib.DrawRectangle(scaleX, scaleY - 2, scaleW, scaleH + 18, new Color((byte)30, (byte)30, (byte)40, (byte)220));
        for (int px = 0; px < scaleW; px++)
        {
            float val = (float)px / scaleW;
            Color c = GetHeatmapColorStatic(val);
            Raylib.DrawLine(scaleX + px, scaleY, scaleX + px, scaleY + scaleH, c);
        }
        Raylib.DrawRectangleLinesEx(new Rectangle(scaleX, scaleY, scaleW, scaleH), 1, ColorPalette.PanelLight);

        // Beschriftung
        DrawGameText("Gering", scaleX + 2, scaleY + scaleH + 2, 11, ColorPalette.TextGray);
        string hochText = "Hoch";
        int hochW = MeasureTextCached(hochText, 12);
        DrawGameText(hochText, scaleX + scaleW - hochW - 2, scaleY + scaleH + 2, 11, ColorPalette.TextGray);
    }

    /// <summary>
    /// Statische Version der Heatmap-Farbberechnung fuer UI-Skala
    /// </summary>
    internal static Color GetHeatmapColorStatic(float value)
    {
        value = Math.Clamp(value, 0f, 1f);

        byte r, g, b;
        if (value <= 0.005f)
        {
            r = 18; g = 20; b = 28;
        }
        else if (value < 0.25f)
        {
            float t = value / 0.25f;
            r = (byte)(18 + t * 10);
            g = (byte)(20 + t * 40);
            b = (byte)(50 + t * 120);
        }
        else if (value < 0.5f)
        {
            float t = (value - 0.25f) / 0.25f;
            r = (byte)(28 + t * 190);
            g = (byte)(60 + t * 150);
            b = (byte)(170 - t * 130);
        }
        else if (value < 0.75f)
        {
            float t = (value - 0.5f) / 0.25f;
            r = (byte)(218 + t * 37);
            g = (byte)(210 - t * 80);
            b = (byte)(40 - t * 20);
        }
        else
        {
            float t = (value - 0.75f) / 0.25f;
            r = 255;
            g = (byte)(130 - t * 100);
            b = (byte)(20 - t * 10);
        }

        return new Color(r, g, b, (byte)220);
    }

    /// <summary>
    /// Zeichnet die Kartenansicht-Buttons unten rechts
    /// </summary>
    static void DrawMapViewButtons()
    {
        int btnSize = 44;
        int btnSpacing = 8;
        int btnY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT - btnSize - 15;
        int btnX = ScreenWidth - btnSize - 15;

        Vector2 mousePos = _cachedMousePos;

        // Rahmen um alle Kartenansicht-Buttons zeichnen
        int numButtons = 4;
        int totalBtnsWidth = numButtons * btnSize + (numButtons - 1) * btnSpacing;
        int framePadding = 8;
        int labelHeight = 20;
        int frameX = btnX + btnSize - totalBtnsWidth - framePadding;
        int frameY = btnY - framePadding - labelHeight;
        int frameW = totalBtnsWidth + framePadding * 2;
        int frameH = btnSize + framePadding * 2 + labelHeight;

        Raylib.DrawRectangle(frameX, frameY, frameW, frameH, new Color((byte)20, (byte)22, (byte)30, (byte)180));
        Raylib.DrawRectangleLines(frameX, frameY, frameW, frameH, ColorPalette.PanelLight);
        DrawGameText("Kartenansichten:", frameX + framePadding, frameY + 4, 13, new Color((byte)180, (byte)190, (byte)210, (byte)255));

        // Politische Ansicht Button (rechts)
        Rectangle politicalBtn = new Rectangle(btnX, btnY, btnSize, btnSize);
        bool hoverPolitical = Raylib.CheckCollisionPointRec(mousePos, politicalBtn);
        bool activePolitical = ui.CurrentMapView == MapViewMode.Political;

        Color polBtnColor = activePolitical ? ColorPalette.Accent : (hoverPolitical ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(politicalBtn, polBtnColor);
        Raylib.DrawRectangleLinesEx(politicalBtn, 2, activePolitical ? ColorPalette.Accent : ColorPalette.PanelLight);

        DrawViewIcon(LoadCachedIcon(ref _mapViewIcon, "map_view.png"), btnX, btnY, btnSize, activePolitical, hoverPolitical);

        if (hoverPolitical && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.CurrentMapView = MapViewMode.Political;
            SoundManager.Play(SoundEffect.Click);
        }

        // Handels-Ansicht Button (links davon)
        btnX -= btnSize + btnSpacing;
        Rectangle tradeBtn = new Rectangle(btnX, btnY, btnSize, btnSize);
        bool hoverTrade = Raylib.CheckCollisionPointRec(mousePos, tradeBtn);
        bool activeTrade = ui.CurrentMapView == MapViewMode.Trade;

        Color tradeBtnColor = activeTrade ? ColorPalette.Accent : (hoverTrade ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(tradeBtn, tradeBtnColor);
        Raylib.DrawRectangleLinesEx(tradeBtn, 2, activeTrade ? ColorPalette.Accent : ColorPalette.PanelLight);

        var tradeIcon = LoadCachedIcon(ref _tradeViewIcon, "trade_view.png");
        if (tradeIcon != null)
        {
            DrawViewIcon(tradeIcon, btnX, btnY, btnSize, activeTrade, hoverTrade);
        }
        else
        {
            int cx = btnX + btnSize / 2;
            int cy = btnY + btnSize / 2;
            Color arrowColor = activeTrade || hoverTrade ? Color.White : new Color((byte)200, (byte)200, (byte)200, (byte)255);
            Raylib.DrawLineEx(new Vector2(cx - 12, cy), new Vector2(cx + 6, cy), 2, arrowColor);
            Raylib.DrawTriangle(new Vector2(cx + 14, cy), new Vector2(cx + 4, cy + 6), new Vector2(cx + 4, cy - 6), arrowColor);
        }

        if (hoverTrade && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.CurrentMapView = MapViewMode.Trade;
            SoundManager.Play(SoundEffect.Click);
        }

        // Buendnis-Ansicht Button (links davon)
        btnX -= btnSize + btnSpacing;
        Rectangle allianceBtn = new Rectangle(btnX, btnY, btnSize, btnSize);
        bool hoverAlliance = Raylib.CheckCollisionPointRec(mousePos, allianceBtn);
        bool activeAlliance = ui.CurrentMapView == MapViewMode.Alliance;

        Color allianceBtnColor = activeAlliance ? ColorPalette.Accent : (hoverAlliance ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(allianceBtn, allianceBtnColor);
        Raylib.DrawRectangleLinesEx(allianceBtn, 2, activeAlliance ? ColorPalette.Accent : ColorPalette.PanelLight);

        var allianceIcon = LoadCachedIcon(ref _allianceViewIcon, "alliance_view.png");
        if (allianceIcon != null)
        {
            DrawViewIcon(allianceIcon, btnX, btnY, btnSize, activeAlliance, hoverAlliance);
        }
        else
        {
            int cx = btnX + btnSize / 2;
            int cy = btnY + btnSize / 2;
            Color handColor = activeAlliance || hoverAlliance ? Color.White : new Color((byte)200, (byte)200, (byte)200, (byte)255);
            Raylib.DrawCircle(cx - 8, cy, 6, handColor);
            Raylib.DrawCircle(cx + 8, cy, 6, handColor);
            Raylib.DrawLineEx(new Vector2(cx - 8, cy), new Vector2(cx + 8, cy), 3, handColor);
        }

        if (hoverAlliance && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.CurrentMapView = MapViewMode.Alliance;
            SoundManager.Play(SoundEffect.Click);
        }

        // Ressourcen-Ansicht Button (links davon)
        btnX -= btnSize + btnSpacing;
        Rectangle resourceBtn = new Rectangle(btnX, btnY, btnSize, btnSize);
        bool hoverResource = Raylib.CheckCollisionPointRec(mousePos, resourceBtn);
        bool activeResource = ui.CurrentMapView == MapViewMode.Resources;

        Color resBtnColor = activeResource ? ColorPalette.Accent : (hoverResource ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(resourceBtn, resBtnColor);
        Raylib.DrawRectangleLinesEx(resourceBtn, 2, activeResource ? ColorPalette.Accent : ColorPalette.PanelLight);

        var resIcon = LoadCachedIcon(ref _resourceViewIcon, "resource_view.png");
        if (resIcon != null)
        {
            DrawViewIcon(resIcon, btnX, btnY, btnSize, activeResource, hoverResource);
        }
        else
        {
            int cx = btnX + btnSize / 2;
            int cy = btnY + btnSize / 2;
            Color gemColor = activeResource || hoverResource ? Color.White : new Color((byte)200, (byte)200, (byte)200, (byte)255);
            Raylib.DrawPoly(new Vector2(cx, cy - 4), 6, 10, 0, gemColor);
        }

        if (hoverResource && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.CurrentMapView = MapViewMode.Resources;
            SoundManager.Play(SoundEffect.Click);
        }

        // === NACHRICHTEN-BUTTON (ueber dem Kartenansichten-Rahmen) ===
        int newsBtnX = ScreenWidth - btnSize - 15;
        int newsBtnY = frameY - btnSize - btnSpacing;
        Rectangle newsBtn = new Rectangle(newsBtnX, newsBtnY, btnSize, btnSize);
        bool hoverNews = Raylib.CheckCollisionPointRec(mousePos, newsBtn);
        bool activeNews = ui.ActiveTopMenuPanel == TopMenuPanel.News;

        Color newsBtnColor = activeNews ? ColorPalette.Accent : (hoverNews ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(newsBtn, newsBtnColor);
        Raylib.DrawRectangleLinesEx(newsBtn, 2, activeNews ? ColorPalette.Accent : ColorPalette.PanelLight);

        // News-Icon zeichnen
        DrawViewIcon(LoadCachedIcon(ref _newsIcon, "news.png"), newsBtnX, newsBtnY, btnSize, activeNews, hoverNews);

        // Badge (ungelesene Nachrichten)
        int unread = _mgr.Notif?.UnreadCount ?? 0;
        if (unread > 0)
        {
            string badgeText = unread > 99 ? "99+" : unread.ToString();
            int badgeW = Math.Max(MeasureTextCached(badgeText, 12) + 8, 18);
            int badgeX = newsBtnX + btnSize - badgeW + 2;
            int badgeY2 = newsBtnY - 2;
            Raylib.DrawRectangleRounded(new Rectangle(badgeX, badgeY2, badgeW, 16), 0.5f, 4, ColorPalette.Red);
            DrawGameText(badgeText, badgeX + (badgeW - MeasureTextCached(badgeText, 12)) / 2, badgeY2 + 2, 11, ColorPalette.TextWhite);
        }

        // Klick-Handler fuer News-Button
        if (hoverNews && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.ActiveTopMenuPanel = activeNews ? TopMenuPanel.None : TopMenuPanel.News;
            ui.ShowBuildPanel = false;
            SoundManager.Play(SoundEffect.Click);
            if (ui.ActiveTopMenuPanel != TopMenuPanel.None)
            {
                ui.ShowPoliticsPanel = false;
                ui.ShowProvincePanel = false;
            }
        }

        // === TUTORIAL-BUTTON (ueber dem Nachrichten-Button) ===
        int tutorialBtnX = newsBtnX;
        int tutorialBtnY = newsBtnY - btnSize - btnSpacing;
        Rectangle tutorialBtn = new Rectangle(tutorialBtnX, tutorialBtnY, btnSize, btnSize);
        bool hoverTutorial = Raylib.CheckCollisionPointRec(mousePos, tutorialBtn);

        Color tutorialBtnColor = ui.ShowTutorialPanel ? ColorPalette.Accent : (hoverTutorial ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(tutorialBtn, tutorialBtnColor);
        Raylib.DrawRectangleLinesEx(tutorialBtn, 2, ui.ShowTutorialPanel ? ColorPalette.Accent : ColorPalette.PanelLight);

        // Tutorial-Icon zeichnen
        var tutIcon = LoadCachedIcon(ref _tutorialIcon, "tutorial.png");
        if (tutIcon != null)
        {
            DrawViewIcon(tutIcon, tutorialBtnX, tutorialBtnY, btnSize, ui.ShowTutorialPanel, hoverTutorial);
        }
        else
        {
            int cx = tutorialBtnX + btnSize / 2;
            int cy = tutorialBtnY + btnSize / 2;
            Color textColor = ui.ShowTutorialPanel || hoverTutorial ? Color.White : new Color((byte)200, (byte)200, (byte)200, (byte)255);
            DrawGameText("?", cx - 8, cy - 12, 18, textColor);
        }

        // Klick-Handler fuer Tutorial-Button
        if (hoverTutorial && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.ShowTutorialPanel = !ui.ShowTutorialPanel;
            ui.TutorialScrollOffset = 0;
            SoundManager.Play(SoundEffect.Click);
        }

        // === WIRTSCHAFT-BUTTON (ueber dem Tutorial-Button) ===
        int econBtnX = tutorialBtnX;
        int econBtnY = tutorialBtnY - btnSize - btnSpacing;
        Rectangle econBtn = new Rectangle(econBtnX, econBtnY, btnSize, btnSize);
        bool hoverEcon = Raylib.CheckCollisionPointRec(mousePos, econBtn);

        Color econBtnColor = ui.ShowEconomyRanking ? ColorPalette.Accent : (hoverEcon ? ColorPalette.PanelLight : ColorPalette.Panel);
        Raylib.DrawRectangleRec(econBtn, econBtnColor);
        Raylib.DrawRectangleLinesEx(econBtn, 2, ui.ShowEconomyRanking ? ColorPalette.Accent : ColorPalette.PanelLight);

        // Wirtschaft-Icon ($ Symbol als Fallback)
        {
            int cx = econBtnX + btnSize / 2;
            int cy = econBtnY + btnSize / 2;
            Color textColor = ui.ShowEconomyRanking || hoverEcon ? Color.White : new Color((byte)200, (byte)200, (byte)200, (byte)255);
            DrawGameText("$", cx - 6, cy - 12, 18, textColor);
        }

        // Klick-Handler fuer Wirtschaft-Button
        if (hoverEcon && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.ShowEconomyRanking = !ui.ShowEconomyRanking;
            ui.EconomyRankingScrollOffset = 0;
            SoundManager.Play(SoundEffect.Click);
        }

        // Tooltip bei Hover
        if (hoverEcon)
        {
            string tooltip = "Wirtschafts-Rangliste";
            int tw = MeasureTextCached(tooltip, 14);
            Raylib.DrawRectangle((int)mousePos.X + 15, (int)mousePos.Y - 25, tw + 10, 18, ColorPalette.Panel);
            DrawGameText(tooltip, (int)mousePos.X + 20, (int)mousePos.Y - 22, 11, ColorPalette.TextWhite);
        }
        else if (hoverResource)
        {
            string tooltip = "Ressourcen-Ansicht";
            int tw = MeasureTextCached(tooltip, 14);
            Raylib.DrawRectangle((int)mousePos.X + 15, (int)mousePos.Y - 25, tw + 10, 18, ColorPalette.Panel);
            DrawGameText(tooltip, (int)mousePos.X + 20, (int)mousePos.Y - 22, 11, ColorPalette.TextWhite);
        }
        else if (hoverPolitical)
        {
            string tooltip = "Politische Ansicht";
            int tw = MeasureTextCached(tooltip, 14);
            Raylib.DrawRectangle((int)mousePos.X + 15, (int)mousePos.Y - 25, tw + 10, 18, ColorPalette.Panel);
            DrawGameText(tooltip, (int)mousePos.X + 20, (int)mousePos.Y - 22, 11, ColorPalette.TextWhite);
        }
        else if (hoverTrade)
        {
            string tooltip = "Handels-Ansicht";
            int tw = MeasureTextCached(tooltip, 14);
            Raylib.DrawRectangle((int)mousePos.X + 15, (int)mousePos.Y - 25, tw + 10, 18, ColorPalette.Panel);
            DrawGameText(tooltip, (int)mousePos.X + 20, (int)mousePos.Y - 22, 11, ColorPalette.TextWhite);
        }
        else if (hoverAlliance)
        {
            string tooltip = "Buendnis-Ansicht";
            int tw = MeasureTextCached(tooltip, 14);
            Raylib.DrawRectangle((int)mousePos.X + 15, (int)mousePos.Y - 25, tw + 10, 18, ColorPalette.Panel);
            DrawGameText(tooltip, (int)mousePos.X + 20, (int)mousePos.Y - 22, 11, ColorPalette.TextWhite);
        }
        else if (hoverNews)
        {
            string tooltip = "Nachrichten";
            int tw = MeasureTextCached(tooltip, 14);
            Raylib.DrawRectangle((int)mousePos.X + 15, (int)mousePos.Y - 25, tw + 10, 18, ColorPalette.Panel);
            DrawGameText(tooltip, (int)mousePos.X + 20, (int)mousePos.Y - 22, 11, ColorPalette.TextWhite);
        }
        else if (hoverTutorial)
        {
            string tooltip = "Spielanleitung";
            int tw = MeasureTextCached(tooltip, 14);
            Raylib.DrawRectangle((int)mousePos.X + 15, (int)mousePos.Y - 25, tw + 10, 18, ColorPalette.Panel);
            DrawGameText(tooltip, (int)mousePos.X + 20, (int)mousePos.Y - 22, 11, ColorPalette.TextWhite);
        }
    }

    static void DrawInfoRowAt(int x, string label, string value, int y, Color? valueColor = null)
    {
        DrawGameText(label, x, y, 11, ColorPalette.TextGray);
        DrawGameText(value, x + 120, y, 11, valueColor ?? ColorPalette.TextWhite);
    }

    /// <summary>
    /// Zeichnet eine Info-Zeile mit groesserer Schrift (fuer VCR OSD Mono)
    /// </summary>
    static void DrawInfoRowAtLarge(int x, string label, string value, int y, Color? valueColor = null)
    {
        DrawGameText(label, x, y, 14, ColorPalette.TextGray);
        DrawGameText(value, x + 140, y, 14, valueColor ?? ColorPalette.TextWhite);
    }

    /// <summary>
    /// Zeichnet eine Info-Box in der Top-Bar (Label + Wert)
    /// </summary>
    static void DrawTopBarInfoBox(ref int x, int rowY, string label, string value, Color? valueColor = null, int extraWidth = 0)
    {
        const int boxHeight = 26;
        const int boxPadding = 8;
        const int boxSpacing = 6;
        const int fontSize = 17;

        int labelWidth = MeasureTextCached(label, fontSize);
        int valueWidth = MeasureTextCached(value, fontSize);
        int boxW = boxPadding + labelWidth + 4 + valueWidth + boxPadding + extraWidth;

        Raylib.DrawRectangle(x, rowY - 2, boxW, boxHeight, ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(new Rectangle(x, rowY - 2, boxW, boxHeight), 1, ColorPalette.PanelLight);

        int textX = x + boxPadding;
        DrawGameText(label, textX, rowY + 2, fontSize, ColorPalette.TextGray);
        DrawGameText(value, textX + labelWidth + 4, rowY + 2, fontSize, valueColor ?? ColorPalette.TextWhite);

        x += boxW + boxSpacing;
    }

    const int RES_BOX_WIDTH = 105;

    static void DrawTopBarResource(ref int x, int rowY, Country player, ResourceType resType)
    {
        const int iconSize = 14;
        const int boxHeight = 20;
        const int boxSpacing = 4;

        double amount = player.GetResource(resType);
        double prod = player.DailyProduction.GetValueOrDefault(resType, 0);
        double cons = player.DailyConsumption.GetValueOrDefault(resType, 0);
        double net = prod - cons;

        string amountStr = Formatting.Resource(amount);
        int boxW = resType == ResourceType.Food ? RES_BOX_WIDTH + 10 : RES_BOX_WIDTH;

        Raylib.DrawRectangle(x, rowY - 2, boxW, boxHeight, ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(new Rectangle(x, rowY - 2, boxW, boxHeight), 1, ColorPalette.PanelLight);

        DrawResourceIcon(resType, x + 4, rowY + 1, iconSize);
        int valueX = x + 4 + iconSize + 3;
        DrawGameText(amountStr, valueX, rowY + 2, 13, ColorPalette.TextWhite);

        // Netto-Aenderung
        if (Math.Abs(net) >= 0.1)
        {
            int amountW = MeasureTextCached(amountStr, 13);
            string netStr = net >= 0 ? $"+{net:F0}" : $"{net:F0}";
            Color netColor = net >= 0 ? ColorPalette.Green : ColorPalette.Red;
            DrawGameText(netStr, valueX + amountW + 1, rowY + 4, 10, netColor);
        }

        x += boxW + boxSpacing;
    }

    /// <summary>
    /// Zeichnet alle Minen auf der Karte als Icons
    /// </summary>
    static void DrawMines()
    {
        // Durch alle Provinzen iterieren und Minen zeichnen
        foreach (var (_, province) in worldMap.Provinces)
        {
            if (province.Mines.Count == 0) continue;

            // Transformierte Position verwenden (Screen-Koordinaten)
            province.UpdateTransformedPoints(worldMap.Zoom, worldMap.Offset, worldMap.MapToScreen);
            Vector2 basePos = province.TransformedLabelPos;

            // Icon-Groesse basierend auf Zoom (winzig)
            int iconSize = Math.Max(1, (int)(1.5f * worldMap.Zoom));
            int iconSpacing = iconSize + 1;

            int mineIndex = 0;
            foreach (var mine in province.Mines)
            {
                // Icon laden falls noch nicht vorhanden
                if (!_mineIcons.ContainsKey(mine.Type))
                {
                    string iconPath = GetMineIconPath(mine.Type);
                    if (File.Exists(iconPath))
                    {
                        var tex = Raylib.LoadTexture(iconPath);
                        Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
                        _mineIcons[mine.Type] = tex;
                    }
                }

                // Position berechnen (horizontal nebeneinander)
                int offsetX = (mineIndex - province.Mines.Count / 2) * iconSpacing;
                int posX = (int)basePos.X + offsetX - iconSize / 2;
                int posY = (int)basePos.Y + 10;

                // Icon zeichnen falls vorhanden
                if (_mineIcons.TryGetValue(mine.Type, out var mineTexture))
                {
                    Rectangle source = new(0, 0, mineTexture.Width, mineTexture.Height);
                    Rectangle dest = new(posX, posY, iconSize, iconSize);
                    Raylib.DrawTexturePro(mineTexture, source, dest, Vector2.Zero, 0, Color.White);
                }
                else
                {
                    // Fallback: Farbiger Kreis
                    Color mineColor = Mine.GetMapColor(mine.Type);
                    int radius = iconSize / 2;
                    Raylib.DrawCircle(posX + radius, posY + radius, radius, mineColor);
                }

                mineIndex++;
            }
        }
    }

    /// <summary>
    /// Gibt den Pfad zum Minen-Icon zurueck
    /// </summary>
    static string GetMineIconPath(MineType type)
    {
        string fileName = type switch
        {
            MineType.OilWell => "oil_well.png",
            MineType.GasDrill => "gas_drill.png",
            MineType.CoalMine => "coal_mine.png",
            MineType.IronMine => "iron_mine.png",
            MineType.CopperMine => "copper_mine.png",
            MineType.UraniumMine => "uranium_mine.png",
            _ => "coal_mine.png"
        };
        return Path.Combine("Data", "Icons", "Mines", fileName);
    }

    /// <summary>
    /// Zeichnet Konflikt-Icons (Explosion) auf Laendern/Provinzen mit aktivem Krieg
    /// </summary>
    static void DrawConflictIcons()
    {
        var conflictManager = _mgr.Conflict;
        if (conflictManager == null) return;

        // Blast-Icon laden falls noch nicht vorhanden
        if (_blastIcon == null)
        {
            string blastPath = Path.Combine("Data", "Icons", "blast.png");
            if (File.Exists(blastPath))
            {
                var tex = Raylib.LoadTexture(blastPath);
                if (tex.Id != 0)
                {
                    Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
                    _blastIcon = tex;
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        // Icon-Groesse basierend auf Zoom
        float baseSize = 24f;
        float iconSize = baseSize * Math.Max(0.5f, worldMap.Zoom / 2f);
        iconSize = Math.Clamp(iconSize, 16f, 64f);

        // Durch alle Laender mit Konflikten iterieren
        foreach (var countryId in conflictManager.CountriesAtWar)
        {
            // Konflikt-Intensitaet fuer Farbe
            string intensity = conflictManager.GetConflictIntensity(countryId);
            Color tint = intensity switch
            {
                "high" => Color.White,
                "medium" => new Color((byte)255, (byte)200, (byte)200, (byte)230),
                "low" => new Color((byte)255, (byte)180, (byte)180, (byte)180),
                _ => Color.White
            };

            // Pruefe ob Konflikt-Zonen (Provinzen) definiert sind
            var conflictZones = conflictManager.GetConflictZones(countryId);

            // Nur Icons anzeigen wenn Konflikt-Zonen definiert sind
            if (conflictZones.Count == 0) continue;

            // Zeichne Icons auf den definierten Konflikt-Provinzen
            foreach (var zoneName in conflictZones)
            {
                // Finde Provinz mit passendem Namen
                var province = worldMap.Provinces.Values
                    .FirstOrDefault(p => p.CountryId == countryId && p.Name == zoneName);

                if (province == null) continue;

                // Berechne Screen-Position
                var screenPos = worldMap.MapToScreen(province.LabelPosition);

                // Pruefen ob sichtbar
                if (screenPos.X < -iconSize || screenPos.X > ScreenWidth + iconSize ||
                    screenPos.Y < -iconSize || screenPos.Y > ScreenHeight + iconSize)
                    continue;

                // Icon zeichnen
                DrawConflictIcon(screenPos, iconSize, tint);
            }
        }
    }

    /// <summary>
    /// Zeichnet ein einzelnes Konflikt-Icon
    /// </summary>
    static void DrawConflictIcon(Vector2 screenPos, float iconSize, Color tint)
    {
        if (_blastIcon == null) return;

        Rectangle srcRect = new Rectangle(0, 0, _blastIcon.Value.Width, _blastIcon.Value.Height);
        Rectangle dstRect = new Rectangle(
            screenPos.X - iconSize / 2,
            screenPos.Y - iconSize / 2,
            iconSize,
            iconSize
        );
        Raylib.DrawTexturePro(_blastIcon.Value, srcRect, dstRect, Vector2.Zero, 0, tint);
    }
}
