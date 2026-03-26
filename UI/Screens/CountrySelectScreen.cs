using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Laenderauswahl-Bildschirm - Zeigt die Weltkarte und erlaubt die Auswahl eines Landes
/// </summary>
internal class CountrySelectScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.CountrySelect;

    public void Enter() { }
    public void Exit() { }

    public void Update()
    {
        Vector2 mousePos = Program._cachedMousePos;

        if (Program.ui.SelectedCountryId != null && Program.game.Countries.ContainsKey(Program.ui.SelectedCountryId))
        {
            Program.ui.SelectCountryButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.SelectCountryButtonRect);

            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Program.ui.SelectCountryButtonHovered)
            {
                Program.game.SelectPlayerCountry(Program.ui.SelectedCountryId);
                Program.worldMap.DayNightCycleEnabled = Program.ui.MainMenuDayNightCycleEnabled;
                Program.currentScreen = GameScreen.Playing;
                return;
            }
        }
        else
        {
            Program.ui.SelectCountryButtonHovered = false;
        }

        UpdateCountrySelectMapInteraction();

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Program.currentScreen = GameScreen.MainMenu;
        }
    }

    public void Draw()
    {
        Program.worldMap.Draw(Program.ui.HoveredCountryId, Program.ui.SelectedCountryId, null, null, null);

        Raylib.DrawRectangleGradientV(0, 0, Program.ScreenWidth, GameConfig.TOP_BAR_HEIGHT,
            new Color((byte)50, (byte)50, (byte)65, (byte)255), ColorPalette.Panel);
        Raylib.DrawLine(0, GameConfig.TOP_BAR_HEIGHT - 1, Program.ScreenWidth, GameConfig.TOP_BAR_HEIGHT - 1, new Color((byte)70, (byte)70, (byte)90, (byte)255));
        Program.DrawGameText("WAEHLE DEIN LAND", 11, 18, 26, ColorPalette.Accent);
        Program.DrawGameText("Klicke auf ein Land | Mausrad = Zoom | Mausrad-Klick = Verschieben",
            20, 45, 11, ColorPalette.TextGray);

        if (Program.ui.SelectedCountryId != null && Program.game.Countries.TryGetValue(Program.ui.SelectedCountryId, out var country))
        {
            int panelX = Program.ScreenWidth - GameConfig.PANEL_WIDTH - GameConfig.PANEL_MARGIN;
            int panelY = 70;
            int panelW = GameConfig.PANEL_WIDTH;
            int panelH = 580;

            Rectangle csInfoRect = new(panelX, panelY, panelW, panelH);
            Rectangle csInfoShadow = new(panelX + 2, panelY + 2, panelW, panelH);
            Raylib.DrawRectangleRounded(csInfoShadow, 0.02f, 6, new Color((byte)0, (byte)0, (byte)0, (byte)40));
            Raylib.DrawRectangleRounded(csInfoRect, 0.02f, 6, ColorPalette.Panel);
            Raylib.DrawRectangleRoundedLinesEx(csInfoRect, 0.02f, 6, 2, ColorPalette.Accent);

            int y = panelY + 15;

            var politicsManager = Program.game.GetSystem<GrandStrategyGame.Systems.Managers.PoliticsManager>();
            var politics = politicsManager?.GetPolitics(Program.ui.SelectedCountryId);

            int portraitHeight = 110;
            int portraitWidth = (int)(portraitHeight * 0.75f);

            string? leaderWithPortrait = null;
            string? leaderTitle = null;
            if (politics != null)
            {
                if (politics.HeadOfState != null && Program.HasLeaderPortrait(politics.HeadOfState))
                {
                    leaderWithPortrait = politics.HeadOfState;
                    leaderTitle = "Staatsoberhaupt";
                }
                else if (politics.HeadOfGovernment != null && Program.HasLeaderPortrait(politics.HeadOfGovernment))
                {
                    leaderWithPortrait = politics.HeadOfGovernment;
                    leaderTitle = "Regierungschef";
                }
                else if (politics.HeadOfState != null)
                {
                    leaderWithPortrait = politics.HeadOfState;
                    leaderTitle = "Staatsoberhaupt";
                }
                else if (politics.HeadOfGovernment != null)
                {
                    leaderWithPortrait = politics.HeadOfGovernment;
                    leaderTitle = "Regierungschef";
                }
            }

            if (leaderWithPortrait != null)
            {
                Program.DrawGameText(leaderTitle ?? "Anfuehrer", panelX + 15, y, 14, ColorPalette.TextGray);
                y += 18;

                Program.DrawLeaderPortrait(leaderWithPortrait, panelX + 15, y, portraitHeight);

                Program.DrawGameText(leaderWithPortrait, panelX + 15, y + portraitHeight + 5, 14, ColorPalette.TextWhite);

                int flagHeight = 50;
                int flagX = panelX + 15 + portraitWidth + 20;
                Program.DrawFlag(Program.ui.SelectedCountryId, flagX, y + 10, flagHeight);

                Program.DrawGameText(country.FullName, flagX, y + 10 + flagHeight + 8, 20, ColorPalette.Accent);

                y += portraitHeight + 30;
            }
            else
            {
                int flagHeight = 50;
                Program.DrawFlag(Program.ui.SelectedCountryId, panelX + 15, y, flagHeight);
                y += flagHeight + 10;

                Program.DrawGameText(country.FullName, panelX + 15, y, 18, ColorPalette.Accent);
                y += 30;
            }

            Program.DrawGameText("POLITIK", panelX + 15, y, 14, ColorPalette.Accent);
            y += 24;

            int dataX = panelX + 170;

            if (politics != null)
            {
                Program.DrawGameText("Regierung:", panelX + 15, y, 14, ColorPalette.TextGray);
                Program.DrawGameText(Program.GetGovernmentTypeName(politics.GovernmentType), dataX, y, 14, ColorPalette.TextWhite);
                y += 22;

                if (politics.HeadOfGovernment != null)
                {
                    Program.DrawGameText("Regierungschef:", panelX + 15, y, 14, ColorPalette.TextGray);
                    Program.DrawGameText(politics.HeadOfGovernment, dataX, y, 14, ColorPalette.TextWhite);
                    y += 22;
                }

                if (politics.RulingParty != null)
                {
                    Program.DrawGameText("Partei:", panelX + 15, y, 14, ColorPalette.TextGray);
                    string partyInfo = $"{politics.RulingParty.Name} ({Program.GetIdeologyName(politics.RulingParty.Ideology)})";
                    Program.DrawGameText(partyInfo, dataX, y, 14, ColorPalette.TextWhite);
                    y += 22;
                }

                Program.DrawGameText("Stabilitaet:", panelX + 15, y, 14, ColorPalette.TextGray);
                int stabPercent = (int)(politics.Stability * 100);
                Color stabColor = stabPercent >= 70 ? ColorPalette.Green :
                                  stabPercent >= 40 ? ColorPalette.Yellow : ColorPalette.Red;
                Program.DrawGameText($"{stabPercent}%", dataX, y, 14, stabColor);
                y += 22;
            }
            else
            {
                Program.DrawGameText("Keine politischen Daten", panelX + 15, y, 14, ColorPalette.TextGray);
                y += 22;
            }

            y += 10;

            Program.DrawGameText("WIRTSCHAFT", panelX + 15, y, 14, ColorPalette.Accent);
            y += 24;

            Program.DrawGameText("Bevoelkerung:", panelX + 15, y, 14, ColorPalette.TextGray);
            Program.DrawGameText(Formatting.Population(country.Population), dataX, y, 14, ColorPalette.TextWhite);
            y += 22;

            Program.DrawGameText("BIP:", panelX + 15, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"${Formatting.Number(country.GDP)} Mio.", dataX, y, 14, ColorPalette.TextWhite);
            y += 22;

            Program.DrawGameText("BIP/Kopf:", panelX + 15, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"${country.GetGDPPerCapita():N0}", dataX, y, 14, ColorPalette.TextWhite);
            y += 22;

            Program.DrawGameText("BIP-Wachstum:", panelX + 15, y, 14, ColorPalette.TextGray);
            string growth = Formatting.Percentage(country.GDPGrowthRate, showSign: true);
            Program.DrawGameText(growth, dataX, y, 14, country.GDPGrowthRate >= 0 ? ColorPalette.Green : ColorPalette.Red);
            y += 22;

            Program.DrawGameText("Arbeitslosigkeit:", panelX + 15, y, 14, ColorPalette.TextGray);
            Program.DrawGameText(Formatting.Percentage(country.UnemploymentRate), dataX, y, 14, ColorPalette.TextWhite);
            y += 22;

            y += 10;

            Program.DrawGameText("RESSOURCEN", panelX + 15, y, 14, ColorPalette.Accent);
            y += 24;

            int resCount = 0;
            foreach (var (resType, amount) in country.Stockpile)
            {
                if (amount > 0 && resCount < 4)
                {
                    string resName = Resource.GetGermanName(resType);
                    Program.DrawGameText($"{resName}: {Formatting.Resource(amount)}", panelX + 15, y, 14, ColorPalette.TextGray);
                    y += 20;
                    resCount++;
                }
            }

            int btnW = panelW - 30;
            int btnH = 40;
            int btnX = panelX + 15;
            int btnY = panelY + panelH - btnH - 15;
            Program.ui.SelectCountryButtonRect = new Rectangle(btnX, btnY, btnW, btnH);

            Color btnBgColor = Program.ui.SelectCountryButtonHovered
                ? new Color((byte)40, (byte)100, (byte)40, (byte)255)
                : new Color((byte)30, (byte)80, (byte)30, (byte)255);
            Color btnBorderColor = Program.ui.SelectCountryButtonHovered ? ColorPalette.Green : new Color((byte)60, (byte)120, (byte)60, (byte)255);

            float btnRound = GameConfig.BUTTON_ROUNDNESS;
            Raylib.DrawRectangleRounded(Program.ui.SelectCountryButtonRect, btnRound, 6, btnBgColor);
            Raylib.DrawRectangleRoundedLinesEx(Program.ui.SelectCountryButtonRect, btnRound, 6, 2, btnBorderColor);

            string btnText = "Land auswaehlen";
            int btnTextWidth = Program.MeasureTextCached(btnText, 18);
            int btnTextX = btnX + (btnW - btnTextWidth) / 2;
            int btnTextY = btnY + (btnH - 18) / 2;
            Program.DrawGameText(btnText, btnTextX, btnTextY, 18, ColorPalette.TextWhite);
        }
        else
        {
            int panelX = Program.ScreenWidth - GameConfig.PANEL_WIDTH - GameConfig.PANEL_MARGIN;
            Rectangle emptyInfoRect = new(panelX, 70, GameConfig.PANEL_WIDTH, 70);
            Raylib.DrawRectangleRounded(emptyInfoRect, 0.06f, 6, ColorPalette.Panel);
            Raylib.DrawRectangleRoundedLinesEx(emptyInfoRect, 0.06f, 6, 1, ColorPalette.PanelLight);
            Program.DrawGameText("Klicke auf ein Land", panelX + 15, 85, 18, ColorPalette.TextGray);
            Program.DrawGameText("um Details zu sehen", panelX + 15, 110, 18, ColorPalette.TextGray);

            Program.ui.SelectCountryButtonRect = new Rectangle(0, 0, 0, 0);
        }

        if (Program.ui.HoveredCountryId != null && Program.game.Countries.TryGetValue(Program.ui.HoveredCountryId, out var hovered))
        {
            Rectangle hoverRect = new(0, Program.ScreenHeight - 45, 320, 45);
            Raylib.DrawRectangleRounded(hoverRect, 0.08f, 6, ColorPalette.Panel);
            Raylib.DrawRectangleRoundedLinesEx(hoverRect, 0.08f, 6, 1, ColorPalette.PanelLight);
            Program.DrawGameText(hovered.Name, 18, Program.ScreenHeight - 32, 18, ColorPalette.TextWhite);
        }
    }

    private bool IsMouseOverCountrySelectPanel()
    {
        Vector2 mousePos = Program._cachedMousePos;

        if (mousePos.Y < GameConfig.TOP_BAR_HEIGHT)
            return true;

        if (Program.ui.SelectedCountryId != null)
        {
            int panelX = Program.ScreenWidth - GameConfig.PANEL_WIDTH - GameConfig.PANEL_MARGIN;
            int panelY = 70;
            int panelW = GameConfig.PANEL_WIDTH;
            int panelH = 580;
            if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(panelX, panelY, panelW, panelH)))
                return true;
        }

        return false;
    }

    private void UpdateCountrySelectMapInteraction()
    {
        Vector2 mousePos = Program._cachedMousePos;
        Program.ui.ViewportChanged = false;

        bool overPanel = IsMouseOverCountrySelectPanel();

        if (Raylib.IsMouseButtonPressed(MouseButton.Middle))
        {
            Program.ui.IsDragging = true;
            Program.ui.LastMousePos = mousePos;
        }
        if (Raylib.IsMouseButtonReleased(MouseButton.Middle))
        {
            Program.ui.IsDragging = false;
        }
        if (Program.ui.IsDragging)
        {
            Vector2 delta = mousePos - Program.ui.LastMousePos;
            Program.worldMap.Move(delta);
            Program.ui.LastMousePos = mousePos;
            Program.ui.ViewportChanged = true;
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0 && !overPanel)
        {
            Program.worldMap.ZoomAt(mousePos, wheel * GameConfig.ZOOM_SPEED);
            Program.ui.ViewportChanged = true;
        }

        float panSpeed = GameConfig.PAN_SPEED;
        Vector2 keyboardPan = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) keyboardPan.Y += panSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.S)) keyboardPan.Y -= panSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.A)) keyboardPan.X += panSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.D)) keyboardPan.X -= panSpeed;
        if (keyboardPan != Vector2.Zero)
        {
            Program.worldMap.Move(keyboardPan);
            Program.ui.ViewportChanged = true;
        }

        bool mouseMoved = Math.Abs(mousePos.X - Program.ui.LastHoverCheckPos.X) > 0.5f ||
                          Math.Abs(mousePos.Y - Program.ui.LastHoverCheckPos.Y) > 0.5f;

        if ((mouseMoved || Program.ui.ViewportChanged) && !overPanel)
        {
            Program.ui.HoveredCountryId = Program.worldMap.GetCountryAtPosition(mousePos);
            Program.ui.LastHoverCheckPos = mousePos;
        }
        else if (overPanel)
        {
            Program.ui.HoveredCountryId = null;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Program.ui.HoveredCountryId != null && !overPanel)
        {
            Program.ui.SelectedCountryId = Program.ui.HoveredCountryId;
        }
    }
}
