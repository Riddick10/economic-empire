using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Spielstand-Laden-Bildschirm - Zeigt gespeicherte Spielstaende zum Laden an
/// </summary>
internal class LoadGameScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.LoadGame;

    public void Enter() { }
    public void Exit() { }

    public void Update()
    {
        Vector2 mousePos = Program._cachedMousePos;

        Program.ui.BackButtonRect.X = 30;
        Program.ui.BackButtonRect.Y = 30;
        Program.ui.BackButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.BackButtonRect);

        int slotWidth = 580;
        int slotHeight = 120;
        int slotStartY = 180;
        int slotSpacing = 20;

        for (int i = 0; i < 3; i++)
        {
            Program.ui.SaveSlotRects[i] = new Rectangle(
                (Program.ScreenWidth - slotWidth) / 2,
                slotStartY + i * (slotHeight + slotSpacing),
                slotWidth,
                slotHeight
            );

            Program.ui.DeleteSlotRects[i] = new Rectangle(
                Program.ui.SaveSlotRects[i].X + slotWidth - 40,
                Program.ui.SaveSlotRects[i].Y + 10,
                30, 30
            );

            Program.ui.SaveSlotHovered[i] = Raylib.CheckCollisionPointRec(mousePos, Program.ui.SaveSlotRects[i]);
            Program.ui.DeleteSlotHovered[i] = Program.ui.SaveSlots[i] != null &&
                Raylib.CheckCollisionPointRec(mousePos, Program.ui.DeleteSlotRects[i]);
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Program.ui.BackButtonHovered)
            {
                Program.currentScreen = GameScreen.MainMenu;
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                if (Program.ui.DeleteSlotHovered[i])
                {
                    SaveGameManager.DeleteSlot(i + 1);
                    Program.ui.SaveSlots = SaveGameManager.GetAllSlots();
                    return;
                }

                if (Program.ui.SaveSlotHovered[i] && Program.ui.SaveSlots[i] != null)
                {
                    var saveData = SaveGameManager.LoadGame(i + 1);
                    if (saveData != null)
                    {
                        Program.game = new Game();
                        Program.game.Initialize();

                        SaveGameManager.ApplySaveData(saveData, Program.game, Program.worldMap);

                        Program.game.SelectPlayerCountry(saveData.PlayerCountryId);

                        Program.worldMap.DayNightCycleEnabled = Program.ui.MainMenuDayNightCycleEnabled;

                        Program.currentScreen = GameScreen.Playing;
                    }
                    return;
                }
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Program.currentScreen = GameScreen.MainMenu;
        }
    }

    public void Draw()
    {
        string title = "SPIEL LADEN";
        int titleWidth = Program.MeasureTextCached(title, GameConfig.FONT_SIZE_TITLE);
        Program.DrawGameText(title, (Program.ScreenWidth - titleWidth) / 2, 100, GameConfig.FONT_SIZE_TITLE, ColorPalette.Accent);

        Program.DrawMenuButton(Program.ui.BackButtonRect, "Zurueck", Program.ui.BackButtonHovered);

        for (int i = 0; i < 3; i++)
        {
            Program.DrawSaveSlot(i, Program.ui.SaveSlotRects[i], Program.ui.SaveSlots[i], Program.ui.SaveSlotHovered[i], Program.ui.DeleteSlotHovered[i], false);
        }

        bool hasAnySave = Program.ui.SaveSlots[0] != null || Program.ui.SaveSlots[1] != null || Program.ui.SaveSlots[2] != null;
        if (!hasAnySave)
        {
            string noSaves = "Keine Spielstaende vorhanden";
            int noSavesWidth = Program.MeasureTextCached(noSaves, 24);
            Program.DrawGameText(noSaves, (Program.ScreenWidth - noSavesWidth) / 2, 550, 11, ColorPalette.TextGray);
        }
    }
}
