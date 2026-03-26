using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Speichern-Bildschirm - Ermoeglicht das Speichern des Spielstands in 3 Slots
/// </summary>
internal class SaveGameScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.SaveGame;

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

        Program.ui.ConfirmSaveButtonRect.X = (Program.ScreenWidth - Program.ui.ConfirmSaveButtonRect.Width) / 2;
        Program.ui.ConfirmSaveButtonRect.Y = slotStartY + 3 * (slotHeight + slotSpacing) + 20;
        Program.ui.ConfirmSaveButtonHovered = Program.ui.SelectedSaveSlot >= 0 &&
            Raylib.CheckCollisionPointRec(mousePos, Program.ui.ConfirmSaveButtonRect);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Program.ui.BackButtonHovered)
            {
                Program.currentScreen = GameScreen.Playing;
                return;
            }

            if (Program.ui.ConfirmSaveButtonHovered && Program.ui.SelectedSaveSlot >= 0)
            {
                SaveGameManager.SaveGame(Program.game, Program.worldMap, Program.ui.SelectedSaveSlot + 1);
                Program.ui.SaveSlots = SaveGameManager.GetAllSlots();
                Program.currentScreen = GameScreen.Playing;
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                if (Program.ui.DeleteSlotHovered[i])
                {
                    SaveGameManager.DeleteSlot(i + 1);
                    Program.ui.SaveSlots = SaveGameManager.GetAllSlots();
                    if (Program.ui.SelectedSaveSlot == i) Program.ui.SelectedSaveSlot = -1;
                    return;
                }

                if (Program.ui.SaveSlotHovered[i])
                {
                    Program.ui.SelectedSaveSlot = i;
                    return;
                }
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Program.currentScreen = GameScreen.Playing;
        }
    }

    public void Draw()
    {
        Raylib.DrawRectangle(0, 0, Program.ScreenWidth, Program.ScreenHeight, new Color(0, 0, 0, 200));

        int panelWidth = 680;
        int panelHeight = 550;
        int panelX = (Program.ScreenWidth - panelWidth) / 2;
        int panelY = (Program.ScreenHeight - panelHeight) / 2;
        Rectangle saveScreenRect = new(panelX, panelY, panelWidth, panelHeight);
        Rectangle saveScreenShadow = new(panelX + 3, panelY + 3, panelWidth, panelHeight);
        Raylib.DrawRectangleRounded(saveScreenShadow, 0.03f, 8, new Color((byte)0, (byte)0, (byte)0, (byte)60));
        Raylib.DrawRectangleRounded(saveScreenRect, 0.03f, 8, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(saveScreenRect, 0.03f, 8, 2, ColorPalette.Accent);

        string title = "SPIEL SPEICHERN";
        int titleWidth = Program.MeasureTextCached(title, 36);
        Program.DrawGameText(title, (Program.ScreenWidth - titleWidth) / 2, panelY + 20, 36, ColorPalette.Accent);

        Program.ui.BackButtonRect.X = panelX + 20;
        Program.ui.BackButtonRect.Y = panelY + 20;
        Program.DrawMenuButton(Program.ui.BackButtonRect, "Zurueck", Program.ui.BackButtonHovered);

        for (int i = 0; i < 3; i++)
        {
            bool isSelected = Program.ui.SelectedSaveSlot == i;
            Program.DrawSaveSlot(i, Program.ui.SaveSlotRects[i], Program.ui.SaveSlots[i], Program.ui.SaveSlotHovered[i] || isSelected, Program.ui.DeleteSlotHovered[i], isSelected);
        }

        if (Program.ui.SelectedSaveSlot >= 0)
        {
            Color btnColor = Program.ui.ConfirmSaveButtonHovered ? ColorPalette.ButtonHover : ColorPalette.ButtonNormal;
            Color borderColor = Program.ui.ConfirmSaveButtonHovered ? ColorPalette.Accent : new Color((byte)80, (byte)80, (byte)100, (byte)255);
            Raylib.DrawRectangleRounded(Program.ui.ConfirmSaveButtonRect, GameConfig.BUTTON_ROUNDNESS, 6, btnColor);
            Raylib.DrawRectangleRoundedLinesEx(Program.ui.ConfirmSaveButtonRect, GameConfig.BUTTON_ROUNDNESS, 6, 2, borderColor);

            string saveText = Program.ui.SaveSlots[Program.ui.SelectedSaveSlot] != null ? "Ueberschreiben" : "Speichern";
            int saveTextWidth = Program.MeasureTextCached(saveText, 22);
            int saveTextX = (int)(Program.ui.ConfirmSaveButtonRect.X + (Program.ui.ConfirmSaveButtonRect.Width - saveTextWidth) / 2);
            int saveTextY = (int)(Program.ui.ConfirmSaveButtonRect.Y + (Program.ui.ConfirmSaveButtonRect.Height - 22) / 2);
            Program.DrawGameText(saveText, saveTextX, saveTextY, 18, Program.ui.ConfirmSaveButtonHovered ? ColorPalette.Accent : ColorPalette.TextWhite);
        }
    }
}
