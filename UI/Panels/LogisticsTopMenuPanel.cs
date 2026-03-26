using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

internal class LogisticsTopMenuPanel : ITopMenuPanel
{
    public string Title => "LOGISTIK";
    public TopMenuPanel PanelType => TopMenuPanel.Logistics;

    public void Draw(TopMenuContext ctx)
    {
        var (panelX, panelY, panelW, panelH) = Program.GetTopMenuPanelRect();

        Raylib.DrawRectangle(panelX, panelY, panelW, panelH, ColorPalette.Panel);
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelW, panelH), 2, ColorPalette.Accent);

        int headerY = panelY + 15;
        int contentX = panelX + 15;
        Program.DrawGameText("LOGISTIK", contentX, headerY, 30, ColorPalette.Accent);
        headerY += 38;
        Raylib.DrawLine(contentX, headerY, panelX + panelW - 15, headerY, ColorPalette.Accent);
        headerY += 10;

        int scrollAreaY = headerY;
        int scrollAreaH = panelH - (scrollAreaY - panelY) - 10;
        int scrollBarWidth = 12;

        Vector2 mousePos = Program._cachedMousePos;
        var player = Program.game.PlayerCountry!;

        // Tatsaechliche Handelsmengen vom TradeManager abfragen (nicht Vereinbarungsmengen)
        var tradeManager = Program._mgr.Trade;
        var tradeNet = new Dictionary<ResourceType, double>();
        if (tradeManager != null)
        {
            foreach (var rt in Enum.GetValues<ResourceType>())
            {
                double vol = tradeManager.GetDailyTradeVolume(player.Id, rt);
                if (Math.Abs(vol) > 0.001)
                    tradeNet[rt] = vol;
            }
        }

        int totalContentHeight = Program._logisticsPanelContentHeight;
        int maxScroll = Math.Max(0, totalContentHeight - scrollAreaH);
        Rectangle scrollAreaRect = new Rectangle(panelX, scrollAreaY, panelW, scrollAreaH);
        bool mouseOverPanel = Raylib.CheckCollisionPointRec(mousePos, scrollAreaRect);

        if (mouseOverPanel)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                Program.ui.LogisticsScrollOffset -= (int)(wheel * 40);
                Program.ui.LogisticsScrollOffset = Math.Clamp(Program.ui.LogisticsScrollOffset, 0, maxScroll);
            }
        }

        Raylib.BeginScissorMode(panelX, scrollAreaY, panelW - scrollBarWidth, scrollAreaH);

        int y = scrollAreaY - Program.ui.LogisticsScrollOffset;

        string GetResName(ResourceType type) => type switch
        {
            ResourceType.Oil => "Erdoel",
            ResourceType.NaturalGas => "Erdgas",
            ResourceType.Coal => "Kohle",
            ResourceType.Iron => "Eisen",
            ResourceType.Copper => "Kupfer",
            ResourceType.Uranium => "Uran",
            ResourceType.Food => "Nahrung",
            ResourceType.Steel => "Stahl",
            ResourceType.Electronics => "Elektr.",
            ResourceType.Machinery => "Maschin.",
            ResourceType.ConsumerGoods => "Konsum.",
            ResourceType.Weapons => "Waffen",
            ResourceType.Ammunition => "Munition",
            _ => type.ToString()
        };

        int colName = contentX + 20;
        int colStock = contentX + 88;
        int colProd = contentX + 152;
        int colCons = contentX + 216;
        int colTrade = contentX + 280;
        int colNet = contentX + 348;

        Program.DrawGameText("Ressource", contentX, y, 11, ColorPalette.TextGray);
        Program.DrawGameText("Lager", colStock, y, 11, ColorPalette.TextGray);
        Program.DrawGameText("Prod.", colProd, y, 11, ColorPalette.TextGray);
        Program.DrawGameText("Verbr.", colCons, y, 11, ColorPalette.TextGray);
        Program.DrawGameText("Handel", colTrade, y, 11, ColorPalette.TextGray);
        Program.DrawGameText("Netto", colNet, y, 11, ColorPalette.TextGray);
        y += 18;

        Raylib.DrawLine(contentX, y, panelX + panelW - 15 - scrollBarWidth, y, ColorPalette.PanelLight);
        y += 8;

        double totalProd = 0, totalCons = 0, totalTradeNet = 0;

        void DrawLogisticsRow(ResourceType resType, ref int rowY)
        {
            double stock = player.GetResource(resType);
            double prod = player.DailyProduction.GetValueOrDefault(resType, 0);
            double cons = player.DailyConsumption.GetValueOrDefault(resType, 0);
            double trade = tradeNet.GetValueOrDefault(resType, 0);
            double net = prod - cons + trade;

            totalProd += prod;
            totalCons += cons;
            totalTradeNet += trade;

            Program.DrawResourceIcon(resType, contentX, rowY, 16);
            Program.DrawGameText(GetResName(resType), colName, rowY, 13, ColorPalette.TextWhite);
            Program.DrawGameText(Program.FormatGermanNumber(stock), colStock, rowY, 13, ColorPalette.TextWhite);
            Program.DrawGameText($"+{Program.FormatGermanNumber(prod)}", colProd, rowY, 13, prod > 0 ? ColorPalette.Green : ColorPalette.TextGray);

            string consStr = cons > 0 ? $"-{Program.FormatGermanNumber(cons)}" : "0";
            Program.DrawGameText(consStr, colCons, rowY, 13, cons > 0 ? ColorPalette.Red : ColorPalette.TextGray);

            if (Math.Abs(trade) > 0.01)
            {
                string tradeStr = trade >= 0 ? $"+{Program.FormatGermanNumber(trade)}" : Program.FormatGermanNumber(trade);
                Color tradeColor = trade >= 0 ? ColorPalette.Green : ColorPalette.Red;
                Program.DrawGameText(tradeStr, colTrade, rowY, 13, tradeColor);
            }
            else
            {
                Program.DrawGameText("-", colTrade, rowY, 13, ColorPalette.TextGray);
            }

            Color netColor = net > 0.01 ? ColorPalette.Green : (net < -0.01 ? ColorPalette.Red : ColorPalette.TextGray);
            string netStr = net >= 0 ? $"+{Program.FormatGermanNumber(net)}" : Program.FormatGermanNumber(net);
            Program.DrawGameText(netStr, colNet, rowY, 13, netColor);

            rowY += 22;
        }

        Program.DrawGameText("ROHSTOFFE", contentX, y, 14, ColorPalette.Accent);
        y += 20;

        foreach (var res in ResourceConfig.LogisticsRaw)
            DrawLogisticsRow(res, ref y);

        y += 8;

        Program.DrawGameText("AGRAR", contentX, y, 14, ColorPalette.Accent);
        y += 20;

        DrawLogisticsRow(ResourceType.Food, ref y);

        y += 8;

        Program.DrawGameText("VERARBEITETE GUETER", contentX, y, 14, ColorPalette.Accent);
        y += 20;

        foreach (var res in ResourceConfig.LogisticsProcessed)
            DrawLogisticsRow(res, ref y);

        y += 8;

        Program.DrawGameText("MILITAERGUETER", contentX, y, 14, ColorPalette.Accent);
        y += 20;

        foreach (var res in ResourceConfig.LogisticsMilitary)
            DrawLogisticsRow(res, ref y);

        Program._logisticsPanelContentHeight = (y + 20) - (scrollAreaY - Program.ui.LogisticsScrollOffset);

        Raylib.EndScissorMode();

        if (maxScroll > 0)
        {
            int sbX = panelX + panelW - scrollBarWidth;
            int sbH = scrollAreaH;
            Raylib.DrawRectangle(sbX, scrollAreaY, scrollBarWidth, sbH, ColorPalette.Background);

            float thumbRatio = (float)scrollAreaH / totalContentHeight;
            int thumbH = Math.Max(20, (int)(sbH * thumbRatio));
            int thumbY = scrollAreaY + (int)((float)Program.ui.LogisticsScrollOffset / maxScroll * (sbH - thumbH));
            Raylib.DrawRectangle(sbX + 2, thumbY, scrollBarWidth - 4, thumbH, ColorPalette.PanelLight);
        }
    }
}
