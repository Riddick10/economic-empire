using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

/// <summary>
/// Forschungs-Panel: Aktuelle Forschung, Fortschritt, Kategorien-Uebersicht
/// </summary>
internal class ResearchTopMenuPanel : ITopMenuPanel
{
    public string Title => "FORSCHUNG";
    public TopMenuPanel PanelType => TopMenuPanel.Research;

    public void Draw(TopMenuContext ctx)
    {
        int y = Program.DrawTopMenuPanelHeader("FORSCHUNG");
        var (panelX, _, panelW, _) = Program.GetTopMenuPanelRect();
        int contentX = panelX + 15;
        int contentW = panelW - 30;
        Vector2 mousePos = Program._cachedMousePos;

        var techManager = Program.game.GetSystem<TechTreeManager>();

        // Button: Forschungsbaum oeffnen
        int btnW = contentW;
        int btnH = 40;
        Rectangle openTreeBtn = new Rectangle(contentX, y, btnW, btnH);
        bool btnHover = Raylib.CheckCollisionPointRec(mousePos, openTreeBtn);

        Raylib.DrawRectangleRounded(openTreeBtn, 0.15f, 8,
            btnHover ? ColorPalette.Accent : ColorPalette.PanelLight);
        string btnText = "FORSCHUNGSBAUM OEFFNEN";
        int btnTextW = Program.MeasureTextCached(btnText, 16);
        Program.DrawGameText(btnText, contentX + (btnW - btnTextW) / 2, y + 12, 14,
            btnHover ? Color.White : ColorPalette.TextWhite);

        if (btnHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Program._techTreePanel.Open();
            Program.ui.ActiveTopMenuPanel = TopMenuPanel.None;
            SoundManager.Play(SoundEffect.Click);
        }

        y += btnH + 20;

        // Aktuelle Forschung
        Program.DrawGameText("AKTUELLE FORSCHUNG", contentX, y, 14, ColorPalette.Accent);
        y += 25;

        if (techManager?.CurrentResearch != null &&
            techManager.Technologies.TryGetValue(techManager.CurrentResearch, out var currentTech))
        {
            Program.DrawGameText(currentTech.Name, contentX, y, 14, ColorPalette.TextWhite);
            y += 20;

            // Fortschrittsbalken
            var progress = techManager.PlayerProgress.GetValueOrDefault(currentTech.Id);
            int effResearchTime = techManager.GetEffectiveResearchTime(currentTech);
            float progressPercent = progress != null ? (float)progress.ProgressDays / effResearchTime : 0;
            Raylib.DrawRectangle(contentX, y, contentW, 14, ColorPalette.Background);
            Raylib.DrawRectangle(contentX, y, (int)(contentW * progressPercent), 14, ColorPalette.Accent);
            Raylib.DrawRectangleLinesEx(new Rectangle(contentX, y, contentW, 12), 1, ColorPalette.PanelLight);
            y += 20;

            Program.DrawGameText($"{progress?.ProgressDays ?? 0} / {effResearchTime} Tage",
                contentX, y, 14, ColorPalette.TextGray);
        }
        else
        {
            Program.DrawGameText("Keine aktive Forschung", contentX, y, 14, ColorPalette.TextGray);
        }
        y += 35;

        // Statistik
        if (techManager != null)
        {
            int completedCount = techManager.PlayerProgress.Count(p => p.Value.Status == TechStatus.Completed);
            int totalCount = techManager.Technologies.Count;

            Program.DrawGameText("FORSCHUNGSFORTSCHRITT", contentX, y, 14, ColorPalette.Accent);
            y += 25;

            Program.DrawGameText($"Erforscht: {completedCount} / {totalCount}", contentX, y, 14, ColorPalette.TextWhite);
            y += 20;

            // Fortschrittsbalken gesamt
            float totalProgress = totalCount > 0 ? (float)completedCount / totalCount : 0;
            Raylib.DrawRectangle(contentX, y, contentW, 8, ColorPalette.Background);
            Raylib.DrawRectangle(contentX, y, (int)(contentW * totalProgress), 8, ColorPalette.Green);
            Raylib.DrawRectangleLinesEx(new Rectangle(contentX, y, contentW, 8), 1, ColorPalette.PanelLight);
            y += 20;

            // Kategorien-Uebersicht
            y += 10;
            Program.DrawGameText("KATEGORIEN", contentX, y, 14, ColorPalette.Accent);
            y += 25;

            foreach (TechCategory cat in Enum.GetValues<TechCategory>())
            {
                int catTechCount = 0, catCompleted = 0;
                foreach (var t in techManager.GetTechsByCategory(cat))
                {
                    catTechCount++;
                    if (techManager.GetTechStatus(t.Id) == TechStatus.Completed) catCompleted++;
                }

                string catName = cat switch
                {
                    TechCategory.Industry => "Industrie",
                    TechCategory.Infrastructure => "Infrastruktur",
                    TechCategory.Electronics => "Elektronik",
                    TechCategory.Energy => "Energie",
                    TechCategory.Military => "Militaer",
                    TechCategory.Society => "Gesellschaft",
                    _ => cat.ToString()
                };

                Program.DrawGameText($"{catName}: {catCompleted}/{catTechCount}", contentX, y, 14, ColorPalette.TextWhite);
                y += 18;
            }
        }
    }
}
