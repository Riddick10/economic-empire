using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

internal class DiplomacyTopMenuPanel : ITopMenuPanel
{
    public string Title => "DIPLOMATIE";
    public TopMenuPanel PanelType => TopMenuPanel.Diplomacy;

    public void Draw(TopMenuContext ctx)
    {
        int y = Program.DrawTopMenuPanelHeader("DIPLOMATIE");
        var (panelX, _, panelW, _) = Program.GetTopMenuPanelRect();
        int contentX = panelX + 15;
        var player = Program.game.PlayerCountry!;

        var diplomacyManager = Program.game.GetSystem<DiplomacyManager>();

        // Bündnisse aus DiplomacyManager
        Program.DrawGameText("BÜNDNISSE", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        var alliances = diplomacyManager?.GetCountryAlliances(player.Id) ?? new List<string>();

        if (alliances.Count == 0)
        {
            Program.DrawGameText("Keine Bündnisse", contentX, y, 14, ColorPalette.TextGray);
            y += 20;
        }
        else
        {
            foreach (var alliance in alliances)
            {
                Program.DrawAllianceIcon(alliance, contentX, y - 2, 16);
                Program.DrawGameText(alliance, contentX + 22, y, 14, ColorPalette.TextWhite);
                y += 20;
            }
        }
        y += 10;

        // Beziehungen aus DiplomacyManager
        Program.DrawGameText("BEZIEHUNGEN", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        var relations = Program.GetDiplomaticRelationsFromManager(player.Id, diplomacyManager);

        // Hilfsfunktion für Beziehungs-Zeile
        void DrawRelationRow(string countryName, int relation, string countryId)
        {
            Program.DrawFlag(countryId, contentX, y - 2, 14);
            Program.DrawGameText(countryName, contentX + 28, y, 14, ColorPalette.TextWhite);

            Color relColor = relation >= 60 ? ColorPalette.Green :
                             relation >= 30 ? ColorPalette.Yellow :
                             relation >= 0 ? new Color((byte)255, (byte)165, (byte)0, (byte)255) : ColorPalette.Red;

            Program.DrawGameText($"{relation:+0;-0}", contentX + 180, y, 14, relColor);
            y += 20;
        }

        foreach (var (countryName, relation, countryId) in relations)
        {
            DrawRelationRow(countryName, relation, countryId);
        }
    }
}
