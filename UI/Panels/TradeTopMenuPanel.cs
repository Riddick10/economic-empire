using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

internal class TradeTopMenuPanel : ITopMenuPanel
{
    public string Title => "HANDEL";
    public TopMenuPanel PanelType => TopMenuPanel.Trade;

    public void Draw(TopMenuContext ctx)
    {
        int y = Program.DrawTopMenuPanelHeader("HANDEL");
        var (panelX, panelY, panelW, panelH) = Program.GetTopMenuPanelRect();
        int contentX = panelX + 15;
        var player = Program.game.PlayerCountry!;
        Vector2 mousePos = Program._cachedMousePos;

        var tradeManager = Program.game.GetSystem<TradeManager>();
        var gameContext = Program.game.GetGameContext();

        // Handelsbilanz aus Country-Daten
        double exports = player.DailyExports;
        double imports = player.DailyImports;
        double balance = player.TradeBalance;

        Program.DrawGameText("HANDELSBILANZ", contentX, y, 14, ColorPalette.Accent);
        y += 24;

        int col1 = contentX;
        int col2 = contentX + 130;

        Program.DrawGameText("Exporte:", col1, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"${exports:N0}M/Tag", col2, y, 14, ColorPalette.Green);
        y += 20;

        Program.DrawGameText("Importe:", col1, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"${imports:N0}M/Tag", col2, y, 14, ColorPalette.Red);
        y += 20;

        Program.DrawGameText("Bilanz:", col1, y, 14, ColorPalette.TextGray);
        Color balanceColor = balance >= 0 ? ColorPalette.Green : ColorPalette.Red;
        string balanceSign = balance >= 0 ? "+" : "";
        Program.DrawGameText($"{balanceSign}${balance:N0}M/Tag", col2, y, 14, balanceColor);
        y += 28;

        // Trennlinie
        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 12;

        // === NEUER HANDEL BUTTON ===
        {
            string btnLabel = Program.ui.ShowTradeCreatePanel ? "Abbrechen" : "+ Neuer Handel";
            int btnW = panelW - 30;
            int btnH = 28;
            Rectangle btnRect = new Rectangle(contentX, y, btnW, btnH);
            bool btnHovered = Raylib.CheckCollisionPointRec(mousePos, btnRect);

            Color btnBg = btnHovered ? ColorPalette.Accent : ColorPalette.PanelLight;
            Raylib.DrawRectangleRec(btnRect, btnBg);
            Raylib.DrawRectangleLinesEx(btnRect, 1, ColorPalette.Accent);

            int textW = Program.MeasureGameText(btnLabel, 14);
            Program.DrawGameText(btnLabel, contentX + (btnW - textW) / 2, y + 6, 14, ColorPalette.TextWhite);

            if (btnHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                Program.ui.ShowTradeCreatePanel = !Program.ui.ShowTradeCreatePanel;
                if (Program.ui.ShowTradeCreatePanel)
                {
                    Program.ui.TradeSelectedResource = null;
                    Program.ui.TradeSelectedCountry = null;
                    Program.ui.TradeAmount = 10;
                }
                SoundManager.Play(SoundEffect.Click);
            }
        }
        y += 36;

        // === HANDEL ERSTELLEN PANEL ===
        if (Program.ui.ShowTradeCreatePanel)
        {
            Program.DrawTradeCreateSubPanel(ref y, contentX, panelX, panelW, player, tradeManager, gameContext, mousePos);
        }

        // Trennlinie
        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 12;

        // === AKTIVE HANDELSABKOMMEN ===
        Program.DrawGameText("AKTIVE ABKOMMEN", contentX, y, 14, ColorPalette.Accent);
        y += 24;

        Program._cache.TradeAgreements.Clear();
        var tradeEnum = tradeManager?.GetTradeAgreementsForCountry(player.Id);
        if (tradeEnum != null) { foreach (var a in tradeEnum) Program._cache.TradeAgreements.Add(a); }
        var agreements = Program._cache.TradeAgreements;

        if (agreements.Count == 0)
        {
            Program.DrawGameText("Keine aktiven Handelsabkommen", contentX, y, 14, ColorPalette.TextGray);
            y += 20;
        }
        else
        {
            int maxVisible = 8;
            foreach (var agreement in agreements.Take(maxVisible))
            {
                bool isExporter = agreement.ExporterId == player.Id;
                string partnerId = isExporter ? agreement.ImporterId : agreement.ExporterId;
                string partnerName = Program.game.Countries.TryGetValue(partnerId, out var partner) ? partner.Name : partnerId;
                string direction = isExporter ? "Export" : "Import";
                Color dirColor = isExporter ? ColorPalette.Green : ColorPalette.Red;
                string resourceName = Resource.GetGermanName(agreement.ResourceType);

                // Kuendigen-Button (X)
                Rectangle cancelRect = new Rectangle(contentX, y - 1, 20, 18);
                bool cancelHovered = Raylib.CheckCollisionPointRec(mousePos, cancelRect);
                Raylib.DrawRectangleRec(cancelRect, cancelHovered ? ColorPalette.Red : ColorPalette.PanelLight);
                Program.DrawGameText("X", (int)cancelRect.X + 5, (int)cancelRect.Y + 1, 14, ColorPalette.TextWhite);

                if (cancelHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    tradeManager?.CancelTradeAgreement(agreement.Id);
                    SoundManager.Play(SoundEffect.Click);
                }

                // Flagge
                Program.DrawFlag(partnerId, contentX + 22, y - 2, 14);

                // Info
                Program.DrawGameText(direction, contentX + 42, y, 14, dirColor);
                Program.DrawGameText(partnerName, contentX + 90, y, 14, ColorPalette.TextWhite);

                // Ressource und Menge
                double price = Program.game.Resources.TryGetValue(agreement.ResourceType, out var res) ? res.CurrentPrice : 0;
                double dailyValue = price * agreement.Amount;
                Program.DrawGameText($"{resourceName}: {agreement.Amount:F0}", contentX + 240, y, 14, ColorPalette.TextGray);
                Program.DrawGameText($"${dailyValue:N0}M", contentX + 360, y, 14, isExporter ? ColorPalette.Green : ColorPalette.Red);

                y += 20;
            }
            if (agreements.Count > maxVisible)
            {
                Program.DrawGameText($"+{agreements.Count - maxVisible} weitere Abkommen", contentX, y, 14, ColorPalette.TextGray);
                y += 18;
            }
        }

        // Zeichne Trade-Seiten-Panel (wenn Handel erstellt wird)
        if (Program.ui.ShowTradeCreatePanel && Program.ui.TradeSelectedResource != null)
        {
            Program.DrawTradeCountrySelectPanel(panelX, panelY, panelW, panelH, player, tradeManager, gameContext, mousePos);
        }
    }
}
