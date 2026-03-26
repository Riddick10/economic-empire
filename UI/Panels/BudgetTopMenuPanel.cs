using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

internal class BudgetTopMenuPanel : ITopMenuPanel
{
    public string Title => "FINANZEN";
    public TopMenuPanel PanelType => TopMenuPanel.Budget;

    public void Draw(TopMenuContext ctx)
    {
        int y = Program.DrawTopMenuPanelHeader("FINANZEN");
        var (panelX, _, panelW, _) = Program.GetTopMenuPanelRect();
        int contentX = panelX + 15;
        var player = Program.game.PlayerCountry!;

        var tradeManager = Program.game.GetSystem<TradeManager>();
        var gameContext = Program.game.GetGameContext();
        double dailyExports = (tradeManager != null && gameContext != null)
            ? tradeManager.GetExpectedDailyExports(player.Id, gameContext)
            : player.DailyExports;
        double dailyImports = (tradeManager != null && gameContext != null)
            ? tradeManager.GetExpectedDailyImports(player.Id, gameContext)
            : player.DailyImports;
        double dailyTradeBalance = dailyExports - dailyImports;
        double monthlyExports = dailyExports * 30;
        double monthlyImports = dailyImports * 30;
        double monthlyTradeBalance = dailyTradeBalance * 30;

        Vector2 mousePos = Program._cachedMousePos;

        Program.DrawGameText("UEBERSICHT", contentX, y, 14, ColorPalette.Accent);
        y += 24;

        Program.DrawGameText("Steuersatz:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"{player.TaxRate * 100:F0}%", contentX + 130, y, 14, ColorPalette.TextWhite);
        y += 20;

        Color unemployColor = player.UnemploymentRate < 0.08 ? ColorPalette.Green : ColorPalette.Red;
        Program.DrawGameText("Arbeitslosigkeit:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText(Formatting.Percentage(player.UnemploymentRate), contentX + 130, y, 14, unemployColor);
        y += 28;

        Program.DrawGameText("EINNAHMEN", contentX, y, 14, ColorPalette.Accent);
        y += 24;

        double monthlyRevenue = (player.GDP * player.TaxRate) / 12.0;
        Program.DrawGameText("Steuern:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"${monthlyRevenue:N0}M", contentX + 130, y, 14, ColorPalette.Green);
        y += 20;

        Program.DrawGameText("Exporte:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"${monthlyExports:N0}M", contentX + 130, y, 14, ColorPalette.Green);
        y += 20;

        double totalRevenue = monthlyRevenue + monthlyExports;
        Program.DrawGameText("Gesamt:", contentX, y, 14, ColorPalette.TextWhite);
        Program.DrawGameText($"${totalRevenue:N0}M/Monat", contentX + 130, y, 14, ColorPalette.Green);
        y += 28;

        Program.DrawGameText("AUSGABEN", contentX, y, 14, ColorPalette.Accent);
        y += 24;

        var expenses = player.GetMonthlyExpenseBreakdown();
        double totalExpenses = 0;

        foreach (var (name, amount) in expenses)
        {
            Program.DrawGameText($"{name}:", contentX, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"${amount:N0}M", contentX + 130, y, 14, ColorPalette.Red);
            y += 18;
            totalExpenses += amount;
        }

        double interestRate = player.GetInterestRate();
        double debtInterest = (player.NationalDebt * interestRate) / 12.0;
        Program.DrawGameText("Zinsen:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"${debtInterest:N0}M", contentX + 130, y, 14, ColorPalette.Red);
        totalExpenses += debtInterest;
        y += 18;

        Program.DrawGameText("Importe:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"${monthlyImports:N0}M", contentX + 130, y, 14, ColorPalette.Red);
        totalExpenses += monthlyImports;
        y += 20;

        Program.DrawGameText("Gesamt:", contentX, y, 14, ColorPalette.TextWhite);
        Program.DrawGameText($"${totalExpenses:N0}M/Monat", contentX + 130, y, 14, ColorPalette.Red);
        y += 28;

        Program.DrawGameText("HANDEL", contentX, y, 14, ColorPalette.Accent);
        y += 24;

        Program.DrawGameText("Exporte:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"+${dailyExports:N0}M/Tag", contentX + 130, y, 14, ColorPalette.Green);
        y += 18;

        Program.DrawGameText("Importe:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"-${dailyImports:N0}M/Tag", contentX + 130, y, 14, ColorPalette.Red);
        y += 18;

        Color tradeColor = dailyTradeBalance >= 0 ? ColorPalette.Green : ColorPalette.Red;
        string tradeSign = dailyTradeBalance >= 0 ? "+" : "";
        Program.DrawGameText("Bilanz:", contentX, y, 14, ColorPalette.TextWhite);
        Program.DrawGameText($"{tradeSign}${dailyTradeBalance:N0}M/Tag", contentX + 130, y, 14, tradeColor);
        y += 28;

        Raylib.DrawLine(contentX, y - 8, panelX + panelW - 15, y - 8, ColorPalette.PanelLight);

        double monthlyBalance = totalRevenue - totalExpenses;
        Program.DrawGameText("BILANZ", contentX, y, 14, ColorPalette.Accent);
        Color balColor = monthlyBalance >= 0 ? ColorPalette.Green : ColorPalette.Red;
        string balSign = monthlyBalance >= 0 ? "+" : "";
        Program.DrawGameText($"{balSign}${monthlyBalance:N0}M", contentX + 130, y, 14, balColor);
        y += 28;

        Program.DrawGameText("SCHULDEN", contentX, y, 14, ColorPalette.Accent);
        y += 24;
        Program.DrawGameText("Gesamt:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"${player.NationalDebt:N0}M", contentX + 130, y, 14, ColorPalette.Red);
        y += 18;
        double debtToGdp = player.GDP > 0 ? player.NationalDebt / player.GDP * 100 : 0;
        Color debtColor = debtToGdp < 60 ? ColorPalette.Green :
                          debtToGdp < 100 ? ColorPalette.Yellow : ColorPalette.Red;
        Program.DrawGameText("Schulden/BIP:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"{debtToGdp:F1}%", contentX + 130, y, 14, debtColor);
        y += 18;

        double currentRate = player.GetInterestRate();
        Color rateColor = currentRate <= 0.025 ? ColorPalette.Green :
                          currentRate <= 0.05 ? ColorPalette.Yellow : ColorPalette.Red;
        Program.DrawGameText("Zinssatz:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"{currentRate * 100:F1}%", contentX + 130, y, 14, rateColor);
        y += 18;

        double yearlyInterest = player.NationalDebt * currentRate;
        double monthlyInterestDisplay = yearlyInterest / 12.0;
        Program.DrawGameText("Zinslast:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"${monthlyInterestDisplay:N0}M/Monat", contentX + 130, y, 14, ColorPalette.Red);
        y += 18;

        double deficitPct = (player.DeficitMultiplier - 1.0) * 100;
        Color deficitColor = deficitPct <= 3 ? ColorPalette.Green :
                             deficitPct <= 8 ? ColorPalette.Yellow : ColorPalette.Red;
        Program.DrawGameText("Defizitquote:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"{deficitPct:F1}%", contentX + 130, y, 14, deficitColor);
        y += 24;

        Program.DrawGameText("Kredit aufnehmen:", contentX, y, 14, ColorPalette.TextGray);
        y += 22;

        int loanBtnW = 60;
        int loanBtnH = 24;
        int loanBtnSpacing = 8;
        int loanBtnX = contentX;

        // +100M
        Rectangle btn100 = new Rectangle(loanBtnX, y, loanBtnW, loanBtnH);
        bool hover100 = Raylib.CheckCollisionPointRec(mousePos, btn100);
        Raylib.DrawRectangleRec(btn100, hover100 ? ColorPalette.Accent : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(btn100, 1, ColorPalette.Accent);
        Program.DrawGameText("+100M", loanBtnX + 8, y + 4, 14, ColorPalette.TextWhite);
        if (hover100 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            player.NationalDebt += 100;
            player.Budget += 100;
            SoundManager.Play(SoundEffect.Click);
        }
        loanBtnX += loanBtnW + loanBtnSpacing;

        // +500M
        Rectangle btn500 = new Rectangle(loanBtnX, y, loanBtnW, loanBtnH);
        bool hover500 = Raylib.CheckCollisionPointRec(mousePos, btn500);
        Raylib.DrawRectangleRec(btn500, hover500 ? ColorPalette.Accent : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(btn500, 1, ColorPalette.Accent);
        Program.DrawGameText("+500M", loanBtnX + 8, y + 4, 14, ColorPalette.TextWhite);
        if (hover500 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            player.NationalDebt += 500;
            player.Budget += 500;
            SoundManager.Play(SoundEffect.Click);
        }
        loanBtnX += loanBtnW + loanBtnSpacing;

        // +1Mrd
        loanBtnW = 58;
        Rectangle btn1000 = new Rectangle(loanBtnX, y, loanBtnW, loanBtnH);
        bool hover1000 = Raylib.CheckCollisionPointRec(mousePos, btn1000);
        Raylib.DrawRectangleRec(btn1000, hover1000 ? ColorPalette.Accent : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(btn1000, 1, ColorPalette.Accent);
        Program.DrawGameText("+1Mrd", loanBtnX + 6, y + 4, 14, ColorPalette.TextWhite);
        if (hover1000 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            player.NationalDebt += 1000;
            player.Budget += 1000;
            SoundManager.Play(SoundEffect.Click);
        }
        loanBtnX += loanBtnW + loanBtnSpacing;

        // +10Mrd
        loanBtnW = 68;
        Rectangle btn10000 = new Rectangle(loanBtnX, y, loanBtnW, loanBtnH);
        bool hover10000 = Raylib.CheckCollisionPointRec(mousePos, btn10000);
        Raylib.DrawRectangleRec(btn10000, hover10000 ? ColorPalette.Accent : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(btn10000, 1, ColorPalette.Accent);
        Program.DrawGameText("+10Mrd", loanBtnX + 4, y + 4, 14, ColorPalette.TextWhite);
        if (hover10000 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            player.NationalDebt += 10000;
            player.Budget += 10000;
            SoundManager.Play(SoundEffect.Click);
        }
        y += loanBtnH + 12;

        // === SCHULDEN TILGEN ===
        Program.DrawGameText("Schulden tilgen:", contentX, y, 14, ColorPalette.TextGray);
        y += 22;

        loanBtnW = 60;
        loanBtnX = contentX;
        bool hasDebt = player.NationalDebt > 0;

        bool canRepay100 = player.Budget >= 100 && player.NationalDebt >= 100;
        Rectangle repay100 = new Rectangle(loanBtnX, y, loanBtnW, loanBtnH);
        bool hoverRepay100 = Raylib.CheckCollisionPointRec(mousePos, repay100);
        Color repay100Bg = !canRepay100 ? ColorPalette.PanelDark : (hoverRepay100 ? ColorPalette.Green : ColorPalette.PanelLight);
        Raylib.DrawRectangleRec(repay100, repay100Bg);
        Raylib.DrawRectangleLinesEx(repay100, 1, canRepay100 ? ColorPalette.Green : ColorPalette.TextGray);
        Program.DrawGameText("-100M", loanBtnX + 10, y + 4, 14, canRepay100 ? ColorPalette.TextWhite : ColorPalette.TextGray);
        if (canRepay100 && hoverRepay100 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            player.NationalDebt -= 100;
            player.Budget -= 100;
            SoundManager.Play(SoundEffect.Click);
        }
        loanBtnX += loanBtnW + loanBtnSpacing;

        bool canRepay500 = player.Budget >= 500 && player.NationalDebt >= 500;
        Rectangle repay500 = new Rectangle(loanBtnX, y, loanBtnW, loanBtnH);
        bool hoverRepay500 = Raylib.CheckCollisionPointRec(mousePos, repay500);
        Color repay500Bg = !canRepay500 ? ColorPalette.PanelDark : (hoverRepay500 ? ColorPalette.Green : ColorPalette.PanelLight);
        Raylib.DrawRectangleRec(repay500, repay500Bg);
        Raylib.DrawRectangleLinesEx(repay500, 1, canRepay500 ? ColorPalette.Green : ColorPalette.TextGray);
        Program.DrawGameText("-500M", loanBtnX + 10, y + 4, 14, canRepay500 ? ColorPalette.TextWhite : ColorPalette.TextGray);
        if (canRepay500 && hoverRepay500 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            player.NationalDebt -= 500;
            player.Budget -= 500;
            SoundManager.Play(SoundEffect.Click);
        }
        loanBtnX += loanBtnW + loanBtnSpacing;

        loanBtnW = 58;
        bool canRepay1000 = player.Budget >= 1000 && player.NationalDebt >= 1000;
        Rectangle repay1000 = new Rectangle(loanBtnX, y, loanBtnW, loanBtnH);
        bool hoverRepay1000 = Raylib.CheckCollisionPointRec(mousePos, repay1000);
        Color repay1000Bg = !canRepay1000 ? ColorPalette.PanelDark : (hoverRepay1000 ? ColorPalette.Green : ColorPalette.PanelLight);
        Raylib.DrawRectangleRec(repay1000, repay1000Bg);
        Raylib.DrawRectangleLinesEx(repay1000, 1, canRepay1000 ? ColorPalette.Green : ColorPalette.TextGray);
        Program.DrawGameText("-1Mrd", loanBtnX + 8, y + 4, 14, canRepay1000 ? ColorPalette.TextWhite : ColorPalette.TextGray);
        if (canRepay1000 && hoverRepay1000 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            player.NationalDebt -= 1000;
            player.Budget -= 1000;
            SoundManager.Play(SoundEffect.Click);
        }
        loanBtnX += loanBtnW + loanBtnSpacing;

        loanBtnW = 68;
        bool canRepay10000 = player.Budget >= 10000 && player.NationalDebt >= 10000;
        Rectangle repay10000 = new Rectangle(loanBtnX, y, loanBtnW, loanBtnH);
        bool hoverRepay10000 = Raylib.CheckCollisionPointRec(mousePos, repay10000);
        Color repay10000Bg = !canRepay10000 ? ColorPalette.PanelDark : (hoverRepay10000 ? ColorPalette.Green : ColorPalette.PanelLight);
        Raylib.DrawRectangleRec(repay10000, repay10000Bg);
        Raylib.DrawRectangleLinesEx(repay10000, 1, canRepay10000 ? ColorPalette.Green : ColorPalette.TextGray);
        Program.DrawGameText("-10Mrd", loanBtnX + 6, y + 4, 14, canRepay10000 ? ColorPalette.TextWhite : ColorPalette.TextGray);
        if (canRepay10000 && hoverRepay10000 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            player.NationalDebt -= 10000;
            player.Budget -= 10000;
            SoundManager.Play(SoundEffect.Click);
        }
        y += loanBtnH + 8;

        if (!hasDebt)
        {
            Program.DrawGameText("Keine Schulden vorhanden", contentX, y, 12, ColorPalette.Green);
            y += 18;
        }
        else if (player.Budget < 100)
        {
            Program.DrawGameText("Nicht genug Geld zur Tilgung", contentX, y, 12, ColorPalette.Yellow);
            y += 18;
        }

        if (debtToGdp >= 100)
        {
            Program.DrawGameText("Warnung: Hohe Verschuldung!", contentX, y, 12, ColorPalette.Red);
            y += 18;
        }
        y += 10;

        Program.DrawGameText("KASSE", contentX, y, 14, ColorPalette.Accent);
        y += 24;
        Color budgetColor2 = player.Budget >= 0 ? ColorPalette.Green : ColorPalette.Red;
        Program.DrawGameText("Aktuell:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText(Formatting.Money(player.Budget), contentX + 130, y, 14, budgetColor2);
        y += 18;

        double dailyBudgetChange = player.CalculateDailyBudgetChange() + dailyTradeBalance;
        string changeSign = dailyBudgetChange >= 0 ? "+" : "";
        Color changeColor = dailyBudgetChange >= 0 ? ColorPalette.Green : ColorPalette.Red;
        Program.DrawGameText("Täglich:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText(changeSign + Formatting.Money(Math.Abs(dailyBudgetChange)), contentX + 130, y, 14, changeColor);
    }
}
