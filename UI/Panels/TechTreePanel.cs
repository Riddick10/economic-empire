using System.Numerics;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;
using Raylib_cs;

namespace GrandStrategyGame.UI.Panels;

/// <summary>
/// Forschungsbaum - Vollbild-Overlay zum Anzeigen und Steuern der Technologien
/// </summary>
internal class TechTreePanel
{
    // Konstanten fuer Tech Tree Layout
    private const int TECH_NODE_WIDTH = 180;
    private const int TECH_NODE_HEIGHT = 70;
    private const int TECH_TIER_SPACING = 220;
    private const int TECH_CATEGORY_SPACING = 100;

    // Tech-Icon-Cache
    private readonly Dictionary<string, Texture2D> _techIcons = new();

    // Statische Farben (vermeidet per-Frame Color-Allokation)
    private static readonly Color TechConnectionInactiveColor = new((byte)60, (byte)60, (byte)80, (byte)255);

    /// <summary>
    /// Laedt ein Tech-Icon aus dem Cache oder von der Festplatte
    /// </summary>
    private Texture2D? GetTechIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return null;

        if (_techIcons.TryGetValue(iconName, out var cached))
            return cached;

        try
        {
            string basePath = GrandStrategyGame.Data.CountryDataLoader.FindBasePath();
            string iconPath = Path.Combine(basePath, "Data", "Icons", "Tech", iconName);

            if (File.Exists(iconPath))
            {
                var texture = Raylib.LoadTexture(iconPath);
                if (texture.Id != 0)
                {
                    _techIcons[iconName] = texture;
                    return texture;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TechTree] Icon-Ladefehler '{iconName}': {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Oeffnet den Forschungsbaum
    /// </summary>
    public void Open()
    {
        Program.ui.ShowTechTree = true;
        Program.ui.TechTreeScrollX = 0;
        Program.ui.TechTreeScrollY = 0;
        Program.ui.SelectedTechId = null;
    }

    /// <summary>
    /// Schliesst den Forschungsbaum
    /// </summary>
    public void Close()
    {
        Program.ui.ShowTechTree = false;
        Program.ui.SelectedTechId = null;
    }

    /// <summary>
    /// Zeichnet den kompletten Forschungsbaum als Vollbild-Overlay
    /// </summary>
    public void Draw()
    {
        if (!Program.ui.ShowTechTree) return;

        var techManager = Program.game.GetSystem<TechTreeManager>();
        if (techManager == null) return;

        Vector2 mousePos = Program._cachedMousePos;

        // Vollbild-Hintergrund
        Raylib.DrawRectangle(0, 0, Program.ScreenWidth, Program.ScreenHeight, new Color((byte)15, (byte)15, (byte)20, (byte)250));

        // Header
        int headerH = 60;
        Raylib.DrawRectangle(0, 0, Program.ScreenWidth, headerH, ColorPalette.Panel);
        Raylib.DrawLine(0, headerH, Program.ScreenWidth, headerH, ColorPalette.PanelLight);

        Program.DrawGameText("FORSCHUNGSBAUM", 30, 18, 22, ColorPalette.Accent);

        // Forschungs-Uebersicht im Header
        DrawHeaderStats(techManager, headerH);

        // Schliessen-Button
        int closeBtnX = Program.ScreenWidth - 50;
        int closeBtnY = 15;
        int closeBtnSize = 30;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);
        bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);

        Raylib.DrawRectangleRounded(closeRect, 0.3f, 8,
            closeHover ? ColorPalette.Red : ColorPalette.PanelLight);
        int xW = Program.MeasureTextCached("X", 20);
        Program.DrawGameText("X", closeBtnX + (closeBtnSize - xW) / 2, closeBtnY + 5, 11, Color.White);

        // Legende
        DrawLegend();

        // Content-Bereich
        int contentY = headerH + 10;
        int contentH = Program.ScreenHeight - headerH - 20;

        // Scissor fuer Scroll-Clipping
        Raylib.BeginScissorMode(0, contentY, Program.ScreenWidth, contentH);

        // Baum zeichnen
        var techTree = techManager.GetTechTree();
        Program.ui.HoveredTechId = null;

        int startX = 50 - Program.ui.TechTreeScrollX;
        int startY = contentY + 50 - Program.ui.TechTreeScrollY;

        // Tier-Ueberschriften zeichnen
        DrawTierHeaders(startX, contentY + 15 - Program.ui.TechTreeScrollY);

        // Zuerst Verbindungslinien zeichnen
        DrawConnections(techManager, techTree, startX, startY);

        // Dann Knoten zeichnen
        int categoryY = startY;
        foreach (var category in Enum.GetValues<TechCategory>())
        {
            if (!techTree.TryGetValue(category, out var techs) || techs.Count == 0)
                continue;

            // Kategorie-Label
            string catName = GetCategoryName(category);
            Color catColor = GetCategoryColor(category);
            Program.DrawGameText(catName, startX - 10, categoryY + TECH_NODE_HEIGHT / 2 - 10, 11, catColor);

            // Technologien nach Tier
            for (int tier = 1; tier <= 5; tier++)
            {
                int nodeX = startX + 100 + (tier - 1) * TECH_TIER_SPACING;
                int nodeYOffset = 0;
                foreach (var tech in techs)
                {
                    if (tech.Tier != tier) continue;
                    int nodeY = categoryY + nodeYOffset;
                    DrawNode(tech, nodeX, nodeY, techManager, mousePos);
                    nodeYOffset += TECH_NODE_HEIGHT + 10;
                }
            }

            // Naechste Kategorie
            int maxTechsInCategory = 1;
            for (int t = 1; t <= 5; t++)
            {
                int count = 0;
                foreach (var tech in techs) { if (tech.Tier == t) count++; }
                if (count > maxTechsInCategory) maxTechsInCategory = count;
            }
            categoryY += maxTechsInCategory * (TECH_NODE_HEIGHT + 10) + TECH_CATEGORY_SPACING;
        }

        Raylib.EndScissorMode();

        // Gesamt-Hoehe fuer Scrolling
        int totalHeight = categoryY - startY + Program.ui.TechTreeScrollY;
        int totalWidth = 100 + 5 * TECH_TIER_SPACING + TECH_NODE_WIDTH;

        // Tooltip fuer gehoverte Technologie
        if (Program.ui.HoveredTechId != null && techManager.Technologies.TryGetValue(Program.ui.HoveredTechId, out var hoveredTech))
        {
            DrawTooltip(hoveredTech, techManager, mousePos);
        }

        // Detail-Panel fuer ausgewaehlte Technologie
        if (Program.ui.SelectedTechId != null && techManager.Technologies.TryGetValue(Program.ui.SelectedTechId, out var selectedTech))
        {
            DrawDetailPanel(selectedTech, techManager);
        }

        // Input-Handling
        HandleInput(closeHover, totalWidth, totalHeight, contentH, techManager);
    }

    /// <summary>
    /// Zeichnet einen einzelnen Technologie-Knoten
    /// </summary>
    private void DrawNode(Technology tech, int x, int y, TechTreeManager manager, Vector2 mousePos)
    {
        Rectangle nodeRect = new Rectangle(x, y, TECH_NODE_WIDTH, TECH_NODE_HEIGHT);
        bool isHovered = Raylib.CheckCollisionPointRec(mousePos, nodeRect);
        bool isSelected = Program.ui.SelectedTechId == tech.Id;

        TechStatus status = manager.GetTechStatus(tech.Id);

        // Farben je nach Status
        Color bgColor, borderColor, textColor;
        switch (status)
        {
            case TechStatus.Completed:
                bgColor = new Color((byte)20, (byte)60, (byte)20, (byte)255);
                borderColor = ColorPalette.Green;
                textColor = Color.White;
                break;
            case TechStatus.Researching:
                bgColor = new Color((byte)60, (byte)60, (byte)20, (byte)255);
                borderColor = ColorPalette.Yellow;
                textColor = Color.White;
                break;
            case TechStatus.Available:
                bgColor = new Color((byte)30, (byte)40, (byte)60, (byte)255);
                borderColor = ColorPalette.Accent;
                textColor = Color.White;
                break;
            default: // Locked
                bgColor = new Color((byte)30, (byte)30, (byte)35, (byte)255);
                borderColor = new Color((byte)60, (byte)60, (byte)70, (byte)255);
                textColor = ColorPalette.TextGray;
                break;
        }

        // Hover-Effekt
        if (isHovered)
        {
            bgColor = new Color((byte)(bgColor.R + 20), (byte)(bgColor.G + 20), (byte)(bgColor.B + 30), bgColor.A);
            Program.ui.HoveredTechId = tech.Id;
        }

        // Auswahl-Rahmen
        if (isSelected)
        {
            Raylib.DrawRectangleRounded(
                new Rectangle(x - 3, y - 3, TECH_NODE_WIDTH + 6, TECH_NODE_HEIGHT + 6),
                0.1f, 8, ColorPalette.Accent);
        }

        // Hintergrund
        Raylib.DrawRectangleRounded(nodeRect, 0.1f, 8, bgColor);
        Raylib.DrawRectangleRoundedLinesEx(nodeRect, 0.1f, 8, 2, borderColor);

        // Kategorie-Farbstreifen links
        Color catColor = GetCategoryColor(tech.Category);
        Raylib.DrawRectangle(x + 3, y + 3, 4, TECH_NODE_HEIGHT - 6, catColor);

        // Tech-Icon
        int iconSize = 32;
        int iconX = x + 10;
        int iconY = y + (TECH_NODE_HEIGHT - iconSize) / 2;
        int textOffsetX = 12; // Standard ohne Icon

        var icon = GetTechIcon(tech.IconName);
        if (icon.HasValue && icon.Value.Id != 0)
        {
            float scale = (float)iconSize / Math.Max(icon.Value.Width, icon.Value.Height);
            int drawW = (int)(icon.Value.Width * scale);
            int drawH = (int)(icon.Value.Height * scale);

            Color iconTint = status == TechStatus.Locked
                ? new Color((byte)100, (byte)100, (byte)100, (byte)180)
                : Color.White;

            Raylib.DrawTextureEx(icon.Value, new Vector2(iconX, iconY + (iconSize - drawH) / 2), 0, scale, iconTint);
            textOffsetX = 10 + iconSize + 6;
        }

        // Technologie-Name
        int maxNameWidth = TECH_NODE_WIDTH - textOffsetX - 30;
        string name = tech.Name;
        if (Program.MeasureTextCached(name, 14) > maxNameWidth)
        {
            while (Program.MeasureTextCached(name + "...", 14) > maxNameWidth && name.Length > 5)
                name = name[..^1];
            name += "...";
        }
        Program.DrawGameText(name, x + textOffsetX, y + 8, 11, textColor);

        // Tier-Anzeige
        Program.DrawGameText($"Stufe {tech.Tier}", x + textOffsetX, y + 28, 11, ColorPalette.TextGray);

        // Forschungszeit (mit Tech-Bonus)
        int effectiveTime = manager.GetEffectiveResearchTime(tech);
        Program.DrawGameText($"{effectiveTime} Tage", x + textOffsetX, y + 44, 11, ColorPalette.TextGray);

        // Status-Icon rechts oben
        string statusIcon = status switch
        {
            TechStatus.Completed => "[OK]",
            TechStatus.Researching => "",
            TechStatus.Available => "",
            _ => "[X]"
        };
        Color statusColor = status switch
        {
            TechStatus.Completed => ColorPalette.Green,
            TechStatus.Researching => ColorPalette.Yellow,
            TechStatus.Available => ColorPalette.Accent,
            _ => ColorPalette.TextGray
        };
        if (!string.IsNullOrEmpty(statusIcon))
        {
            Program.DrawGameText(statusIcon, x + TECH_NODE_WIDTH - 35, y + 6, 11, statusColor);
        }

        // Fortschrittsbalken bei aktiver Forschung
        if (status == TechStatus.Researching)
        {
            var progress = manager.PlayerProgress[tech.Id];
            float percent = (float)progress.ProgressDays / manager.GetEffectiveResearchTime(tech);
            int barW = TECH_NODE_WIDTH - 20;
            int barH = 6;
            int barX = x + 10;
            int barY = y + TECH_NODE_HEIGHT - 12;

            Raylib.DrawRectangle(barX, barY, barW, barH, new Color((byte)20, (byte)20, (byte)30, (byte)200));
            int fillW = (int)(barW * percent);
            if (fillW > 0)
            {
                Raylib.DrawRectangle(barX, barY, fillW, barH, ColorPalette.Yellow);
            }
            Raylib.DrawRectangleLines(barX, barY, barW, barH, ColorPalette.Yellow);

            string pctText = $"{(int)(percent * 100)}%";
            Program.DrawGameText(pctText, x + TECH_NODE_WIDTH - 35, y + TECH_NODE_HEIGHT - 14, 11, ColorPalette.Yellow);
        }

        // "Forschen"-Button bei verfuegbaren Technologien
        if (status == TechStatus.Available && isHovered)
        {
            int btnW = 70;
            int btnH = 20;
            int btnX = x + TECH_NODE_WIDTH - btnW - 8;
            int btnY = y + TECH_NODE_HEIGHT - btnH - 6;

            Rectangle btnRect = new Rectangle(btnX, btnY, btnW, btnH);
            bool btnHover = Raylib.CheckCollisionPointRec(mousePos, btnRect);

            Color btnBg = btnHover ? ColorPalette.Green : new Color((byte)40, (byte)100, (byte)40, (byte)255);
            Raylib.DrawRectangleRounded(btnRect, 0.3f, 6, btnBg);
            Raylib.DrawRectangleRoundedLinesEx(btnRect, 0.3f, 6, 1, ColorPalette.Green);

            string btnText = "Forschen";
            int btnTextW = Program.MeasureTextCached(btnText, 11);
            Program.DrawGameText(btnText, btnX + (btnW - btnTextW) / 2, btnY + 4, 11, Color.White);

            if (btnHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                manager.StartResearch(tech.Id);
                SoundManager.Play(SoundEffect.Click);
                return;
            }
        }

        // Klick-Erkennung (Auswahl)
        if (isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Program.ui.SelectedTechId = (Program.ui.SelectedTechId == tech.Id) ? null : tech.Id;
            SoundManager.Play(SoundEffect.Click);
        }
    }

    /// <summary>
    /// Zeichnet Verbindungslinien zwischen Technologien
    /// </summary>
    private void DrawConnections(TechTreeManager manager, Dictionary<TechCategory, List<Technology>> techTree, int startX, int startY)
    {
        int categoryY = startY;

        foreach (var category in Enum.GetValues<TechCategory>())
        {
            if (!techTree.TryGetValue(category, out var techs) || techs.Count == 0)
                continue;

            foreach (var tech in techs)
            {
                int techX = startX + 100 + (tech.Tier - 1) * TECH_TIER_SPACING;
                int techY = GetNodeYPosition(tech, techs, categoryY);

                foreach (var prereqId in tech.Prerequisites)
                {
                    if (!manager.Technologies.TryGetValue(prereqId, out var prereq))
                        continue;

                    int prereqCatY = startY;
                    foreach (var cat in Enum.GetValues<TechCategory>())
                    {
                        if (!techTree.TryGetValue(cat, out var catTechs))
                            continue;

                        if (catTechs.Contains(prereq))
                        {
                            int prereqX = startX + 100 + (prereq.Tier - 1) * TECH_TIER_SPACING + TECH_NODE_WIDTH;
                            int prereqY = GetNodeYPosition(prereq, catTechs, prereqCatY) + TECH_NODE_HEIGHT / 2;

                            TechStatus prereqStatus = manager.GetTechStatus(prereqId);
                            Color lineColor = prereqStatus == TechStatus.Completed
                                ? ColorPalette.Green
                                : TechConnectionInactiveColor;

                            int targetX = techX;
                            int targetY = techY + TECH_NODE_HEIGHT / 2;

                            int midX = (prereqX + targetX) / 2;
                            Raylib.DrawLine(prereqX, prereqY, midX, prereqY, lineColor);
                            Raylib.DrawLine(midX, prereqY, midX, targetY, lineColor);
                            Raylib.DrawLine(midX, targetY, targetX, targetY, lineColor);

                            Raylib.DrawTriangle(
                                new Vector2(targetX, targetY),
                                new Vector2(targetX - 8, targetY - 5),
                                new Vector2(targetX - 8, targetY + 5),
                                lineColor);
                            break;
                        }

                        int maxInCat = 1;
                        for (int t = 1; t <= 5; t++)
                        {
                            int count = 0;
                            foreach (var te in catTechs) { if (te.Tier == t) count++; }
                            if (count > maxInCat) maxInCat = count;
                        }
                        prereqCatY += maxInCat * (TECH_NODE_HEIGHT + 10) + TECH_CATEGORY_SPACING;
                    }
                }
            }

            int maxTechsInCategory = 1;
            for (int t = 1; t <= 5; t++)
            {
                int count = 0;
                foreach (var te in techs) { if (te.Tier == t) count++; }
                if (count > maxTechsInCategory) maxTechsInCategory = count;
            }
            categoryY += maxTechsInCategory * (TECH_NODE_HEIGHT + 10) + TECH_CATEGORY_SPACING;
        }
    }

    private static int GetNodeYPosition(Technology tech, List<Technology> categoryTechs, int categoryStartY)
    {
        int index = 0;
        foreach (var t in categoryTechs)
        {
            if (t.Tier != tech.Tier) continue;
            if (t == tech) break;
            index++;
        }
        return categoryStartY + index * (TECH_NODE_HEIGHT + 10);
    }

    private void DrawTooltip(Technology tech, TechTreeManager manager, Vector2 mousePos)
    {
        int tooltipW = 280;
        int tooltipH = 140 + tech.Prerequisites.Count * 18 + tech.Effects.Count * 18;

        int tooltipX = (int)mousePos.X + 15;
        int tooltipY = (int)mousePos.Y + 15;

        if (tooltipX + tooltipW > Program.ScreenWidth - 10)
            tooltipX = (int)mousePos.X - tooltipW - 15;
        if (tooltipY + tooltipH > Program.ScreenHeight - 10)
            tooltipY = Program.ScreenHeight - tooltipH - 10;

        Raylib.DrawRectangleRounded(new Rectangle(tooltipX - 2, tooltipY - 2, tooltipW + 4, tooltipH + 4),
            0.05f, 8, ColorPalette.PanelLight);
        Raylib.DrawRectangleRounded(new Rectangle(tooltipX, tooltipY, tooltipW, tooltipH),
            0.05f, 8, ColorPalette.Panel);

        int y = tooltipY + 10;

        Program.DrawGameText(tech.Name, tooltipX + 10, y, 11, GetCategoryColor(tech.Category));
        y += 24;

        var descLines = WrapText(tech.Description, tooltipW - 20, 12);
        foreach (var line in descLines)
        {
            Program.DrawGameText(line, tooltipX + 10, y, 11, ColorPalette.TextGray);
            y += 16;
        }
        y += 8;

        Program.DrawGameText($"Forschungszeit: {manager.GetEffectiveResearchTime(tech)} Tage", tooltipX + 10, y, 11, ColorPalette.TextWhite);
        y += 18;

        if (tech.Prerequisites.Count > 0)
        {
            Program.DrawGameText("Voraussetzungen:", tooltipX + 10, y, 11, ColorPalette.Yellow);
            y += 16;
            foreach (var prereqId in tech.Prerequisites)
            {
                if (manager.Technologies.TryGetValue(prereqId, out var prereq))
                {
                    TechStatus status = manager.GetTechStatus(prereqId);
                    Color prereqColor = status == TechStatus.Completed ? ColorPalette.Green : ColorPalette.Red;
                    Program.DrawGameText($"  - {prereq.Name}", tooltipX + 10, y, 11, prereqColor);
                    y += 16;
                }
            }
        }

        if (tech.Effects.Count > 0)
        {
            Program.DrawGameText("Effekte:", tooltipX + 10, y, 11, ColorPalette.Accent);
            y += 16;
            foreach (var (effectName, effectValue) in tech.Effects)
            {
                string effectStr = $"  {FormatEffectName(effectName)}: {effectValue:+0.##;-0.##}";
                Program.DrawGameText(effectStr, tooltipX + 10, y, 11, ColorPalette.Green);
                y += 16;
            }
        }
    }

    private void DrawDetailPanel(Technology tech, TechTreeManager manager)
    {
        int panelW = 400;
        int panelH = 400;
        int panelX = Program.ScreenWidth - panelW - 20;
        int panelY = 80;

        Raylib.DrawRectangleRounded(new Rectangle(panelX - 2, panelY - 2, panelW + 4, panelH + 4),
            0.03f, 8, GetCategoryColor(tech.Category));
        Raylib.DrawRectangleRounded(new Rectangle(panelX, panelY, panelW, panelH),
            0.03f, 8, ColorPalette.Panel);

        int y = panelY + 15;
        int contentX = panelX + 15;

        Program.DrawGameText(tech.Name, contentX, y, 11, GetCategoryColor(tech.Category));
        y += 30;

        Program.DrawGameText(GetCategoryName(tech.Category), contentX, y, 11, ColorPalette.TextGray);
        y += 25;

        var descLines = WrapText(tech.Description, panelW - 30, 13);
        foreach (var line in descLines)
        {
            Program.DrawGameText(line, contentX, y, 11, ColorPalette.TextWhite);
            y += 18;
        }
        y += 15;

        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        TechStatus status = manager.GetTechStatus(tech.Id);
        string statusText = status switch
        {
            TechStatus.Completed => "Erforscht",
            TechStatus.Researching => "In Forschung",
            TechStatus.Available => "Verfuegbar",
            _ => "Gesperrt"
        };
        Color statusColor = status switch
        {
            TechStatus.Completed => ColorPalette.Green,
            TechStatus.Researching => ColorPalette.Yellow,
            TechStatus.Available => ColorPalette.Accent,
            _ => ColorPalette.Red
        };
        Program.DrawGameText($"Status: {statusText}", contentX, y, 11, statusColor);
        y += 25;

        Program.DrawGameText($"Stufe: {tech.Tier}", contentX, y, 11, ColorPalette.TextWhite);
        y += 20;
        Program.DrawGameText($"Forschungszeit: {manager.GetEffectiveResearchTime(tech)} Tage", contentX, y, 11, ColorPalette.TextWhite);
        y += 25;

        if (tech.Prerequisites.Count > 0)
        {
            Program.DrawGameText("Voraussetzungen:", contentX, y, 11, ColorPalette.Yellow);
            y += 20;
            foreach (var prereqId in tech.Prerequisites)
            {
                if (manager.Technologies.TryGetValue(prereqId, out var prereq))
                {
                    TechStatus prereqStatus = manager.GetTechStatus(prereqId);
                    string symbol = prereqStatus == TechStatus.Completed ? "[X]" : "[ ]";
                    Color prereqColor = prereqStatus == TechStatus.Completed ? ColorPalette.Green : ColorPalette.Red;
                    Program.DrawGameText($"  {symbol} {prereq.Name}", contentX, y, 11, prereqColor);
                    y += 18;
                }
            }
            y += 10;
        }

        if (tech.Effects.Count > 0)
        {
            Program.DrawGameText("Effekte:", contentX, y, 11, ColorPalette.Accent);
            y += 20;
            foreach (var (effectName, effectValue) in tech.Effects)
            {
                string sign = effectValue >= 0 ? "+" : "";
                string effectStr = $"  {FormatEffectName(effectName)}: {sign}{effectValue * 100:F0}%";
                Program.DrawGameText(effectStr, contentX, y, 11, ColorPalette.Green);
                y += 18;
            }
        }

        // Aktions-Button am unteren Rand
        y = panelY + panelH - 50;
        Vector2 mousePos = Program._cachedMousePos;

        if (status == TechStatus.Available)
        {
            int btnW = panelW - 30;
            int btnH = 35;
            Rectangle btnRect = new Rectangle(contentX, y, btnW, btnH);
            bool btnHover = Raylib.CheckCollisionPointRec(mousePos, btnRect);

            Color btnBg = btnHover ? ColorPalette.Green : new Color((byte)40, (byte)100, (byte)40, (byte)255);
            Raylib.DrawRectangleRounded(btnRect, 0.2f, 8, btnBg);
            Raylib.DrawRectangleRoundedLinesEx(btnRect, 0.2f, 8, 2, ColorPalette.Green);

            string btnText = "Forschung starten";
            int btnTextW = Program.MeasureTextCached(btnText, 16);
            Program.DrawGameText(btnText, contentX + (btnW - btnTextW) / 2, y + 9, 11, Color.White);

            if (btnHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                manager.StartResearch(tech.Id);
                SoundManager.Play(SoundEffect.Click);
            }
        }
        else if (status == TechStatus.Researching)
        {
            if (manager.PlayerProgress.TryGetValue(tech.Id, out var progress))
            {
                int effTime = manager.GetEffectiveResearchTime(tech);
                float percent = (float)progress.ProgressDays / effTime;
                int barW = panelW - 30;
                int barH = 25;

                Raylib.DrawRectangle(contentX, y, barW, barH, ColorPalette.PanelDark);
                int fillW = (int)(barW * percent);
                if (fillW > 0)
                {
                    Raylib.DrawRectangle(contentX, y, fillW, barH, ColorPalette.Yellow);
                }
                Raylib.DrawRectangleLines(contentX, y, barW, barH, ColorPalette.Yellow);

                string progressText = $"{progress.ProgressDays}/{effTime} Tage ({(int)(percent * 100)}%)";
                int textW = Program.MeasureTextCached(progressText, 14);
                Program.DrawGameText(progressText, contentX + (barW - textW) / 2, y + 5, 11, Color.White);
            }
        }
        else if (status == TechStatus.Locked)
        {
            Program.DrawGameText("Voraussetzungen nicht erfuellt", contentX, y + 8, 11, ColorPalette.Red);
        }
    }

    private void DrawLegend()
    {
        int legendX = Program.ScreenWidth - 380;
        int legendY = 15;

        Program.DrawGameText("Status:", legendX, legendY, 11, ColorPalette.TextGray);

        int itemX = legendX + 50;

        Raylib.DrawRectangle(itemX, legendY + 2, 11, 14, ColorPalette.Green);
        Program.DrawGameText("Erforscht", itemX + 16, legendY, 11, ColorPalette.TextGray);
        itemX += 75;

        Raylib.DrawRectangle(itemX, legendY + 2, 11, 14, ColorPalette.Yellow);
        Program.DrawGameText("In Forschung", itemX + 16, legendY, 11, ColorPalette.TextGray);
        itemX += 95;

        Raylib.DrawRectangle(itemX, legendY + 2, 11, 14, ColorPalette.Accent);
        Program.DrawGameText("Verfuegbar", itemX + 16, legendY, 11, ColorPalette.TextGray);
        itemX += 80;

        Raylib.DrawRectangle(itemX, legendY + 2, 11, 14, new Color((byte)60, (byte)60, (byte)70, (byte)255));
        Program.DrawGameText("Gesperrt", itemX + 16, legendY, 11, ColorPalette.TextGray);
    }

    private void DrawTierHeaders(int startX, int y)
    {
        for (int tier = 1; tier <= 5; tier++)
        {
            int headerX = startX + 100 + (tier - 1) * TECH_TIER_SPACING;
            int centerX = headerX + TECH_NODE_WIDTH / 2;

            string tierLabel = $"Stufe {tier}";
            int textW = Program.MeasureTextCached(tierLabel, 16);

            Raylib.DrawRectangle(centerX - textW / 2 - 10, y - 2, textW + 20, 18, ColorPalette.PanelDark);
            Raylib.DrawRectangleLines(centerX - textW / 2 - 10, y - 2, textW + 20, 18, ColorPalette.PanelLight);

            Program.DrawGameText(tierLabel, centerX - textW / 2, y + 2, 11, ColorPalette.Accent);
        }
    }

    private void DrawHeaderStats(TechTreeManager manager, int headerHeight)
    {
        int statsX = 280;
        int statsY = 20;

        int completed = 0, researching = 0, available = 0, locked = 0;
        foreach (var tech in manager.Technologies.Values)
        {
            var status = manager.GetTechStatus(tech.Id);
            switch (status)
            {
                case TechStatus.Completed: completed++; break;
                case TechStatus.Researching: researching++; break;
                case TechStatus.Available: available++; break;
                default: locked++; break;
            }
        }
        int total = manager.Technologies.Count;

        Program.DrawGameText($"Fortschritt: {completed}/{total}", statsX, statsY, 11, ColorPalette.Green);

        statsX += 140;
        if (researching > 0)
        {
            var activeResearch = manager.Technologies.Values
                .FirstOrDefault(t => manager.GetTechStatus(t.Id) == TechStatus.Researching);
            if (activeResearch != null && manager.PlayerProgress.TryGetValue(activeResearch.Id, out var progress))
            {
                float percent = (float)progress.ProgressDays / manager.GetEffectiveResearchTime(activeResearch);
                string progressText = $"Aktiv: {activeResearch.Name} ({(int)(percent * 100)}%)";
                Program.DrawGameText(progressText, statsX, statsY, 11, ColorPalette.Yellow);
            }
        }
        else
        {
            Program.DrawGameText("Keine aktive Forschung", statsX, statsY, 11, ColorPalette.TextGray);
        }
    }

    private void HandleInput(bool closeHover, int totalWidth, int totalHeight, int contentH, TechTreeManager manager)
    {
        if ((Raylib.IsMouseButtonPressed(MouseButton.Left) && closeHover) ||
            Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Close();
            SoundManager.Play(SoundEffect.Click);
            return;
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0)
        {
            if (Raylib.IsKeyDown(KeyboardKey.LeftShift))
            {
                Program.ui.TechTreeScrollX -= (int)(wheel * 50);
            }
            else
            {
                Program.ui.TechTreeScrollY -= (int)(wheel * 50);
            }
        }

        if (Raylib.IsKeyDown(KeyboardKey.Left))
            Program.ui.TechTreeScrollX -= 10;
        if (Raylib.IsKeyDown(KeyboardKey.Right))
            Program.ui.TechTreeScrollX += 10;
        if (Raylib.IsKeyDown(KeyboardKey.Up))
            Program.ui.TechTreeScrollY -= 10;
        if (Raylib.IsKeyDown(KeyboardKey.Down))
            Program.ui.TechTreeScrollY += 10;

        Program.ui.TechTreeScrollX = Math.Clamp(Program.ui.TechTreeScrollX, 0, Math.Max(0, totalWidth - Program.ScreenWidth + 100));
        Program.ui.TechTreeScrollY = Math.Clamp(Program.ui.TechTreeScrollY, 0, Math.Max(0, totalHeight - contentH + 100));
    }

    // ============================================================
    // Hilfsfunktionen
    // ============================================================

    private static string GetCategoryName(TechCategory category) => category switch
    {
        TechCategory.Industry => "INDUSTRIE",
        TechCategory.Infrastructure => "INFRASTRUKTUR",
        TechCategory.Electronics => "ELEKTRONIK",
        TechCategory.Energy => "ENERGIE",
        TechCategory.Military => "MILITAER",
        TechCategory.Society => "GESELLSCHAFT",
        _ => category.ToString().ToUpper()
    };

    private static Color GetCategoryColor(TechCategory category) => category switch
    {
        TechCategory.Industry => new Color((byte)255, (byte)165, (byte)0, (byte)255),
        TechCategory.Infrastructure => new Color((byte)100, (byte)149, (byte)237, (byte)255),
        TechCategory.Electronics => new Color((byte)0, (byte)191, (byte)255, (byte)255),
        TechCategory.Energy => new Color((byte)255, (byte)215, (byte)0, (byte)255),
        TechCategory.Military => new Color((byte)220, (byte)20, (byte)60, (byte)255),
        TechCategory.Society => new Color((byte)144, (byte)238, (byte)144, (byte)255),
        _ => ColorPalette.TextWhite
    };

    private static string FormatEffectName(string effectName) => effectName switch
    {
        "factory_output" => "Fabrikproduktion",
        "mining_output" => "Bergbauproduktion",
        "oil_output" => "Oelproduktion",
        "energy_output" => "Energieproduktion",
        "electronics_output" => "Elektronikproduktion",
        "trade_efficiency" => "Handelseffizienz",
        "research_speed" => "Forschungsgeschwindigkeit",
        "military_strength" => "Militaerstaerke",
        "population_happiness" => "Zufriedenheit",
        "population_growth" => "Bevoelkerungswachstum",
        "education_level" => "Bildungsniveau",
        "unemployment" => "Arbeitslosigkeit",
        "prestige" => "Prestige",
        _ => effectName.Replace("_", " ")
    };

    private static List<string> WrapText(string text, int maxWidth, int fontSize)
    {
        var lines = new List<string>();
        string currentLine = "";

        foreach (var word in text.Split(' '))
        {
            string testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
            if (Program.MeasureTextCached(testLine, fontSize) > maxWidth)
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
