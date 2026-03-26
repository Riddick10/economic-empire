using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.UI;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Hauptmenue-Bildschirm - Zeigt Titel, Buttons und Optionen
/// </summary>
internal class MainMenuScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.MainMenu;

    public void Enter() { }
    public void Exit() { }

    public void Update()
    {
        Vector2 mousePos = Program._cachedMousePos;

        if (Program.ui.ShowMainMenuOptions)
        {
            UpdateMainMenuOptions(mousePos);
            return;
        }

        Program.ui.NewGameButtonRect.X = (Program.ScreenWidth - Program.ui.NewGameButtonRect.Width) / 2;
        Program.ui.NewGameButtonRect.Y = 330;
        Program.ui.LoadGameButtonRect.X = (Program.ScreenWidth - Program.ui.LoadGameButtonRect.Width) / 2;
        Program.ui.LoadGameButtonRect.Y = 400;
        Program.ui.OptionsButtonRect.X = (Program.ScreenWidth - Program.ui.OptionsButtonRect.Width) / 2;
        Program.ui.OptionsButtonRect.Y = 470;
        Program.ui.QuitButtonRect.X = (Program.ScreenWidth - Program.ui.QuitButtonRect.Width) / 2;
        Program.ui.QuitButtonRect.Y = 540;

        Program.ui.NewGameButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.NewGameButtonRect);
        Program.ui.LoadGameButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.LoadGameButtonRect);
        Program.ui.OptionsButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.OptionsButtonRect);
        Program.ui.QuitButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.QuitButtonRect);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Program.ui.NewGameButtonHovered)
            {
                // Neues Spiel: Frisches Game-Objekt erstellen und initialisieren
                Program.game = new Game();
                Program.game.Initialize();
                if (Program.game.GameContext != null)
                {
                    Program.game.GameContext.WorldMap = Program.worldMap;
                }
                Program.ui.SelectedCountryId = null;
                Program.currentScreen = GameScreen.CountrySelect;
            }
            else if (Program.ui.LoadGameButtonHovered)
            {
                Program.ui.SaveSlots = SaveGameManager.GetAllSlots();
                Program.ui.SelectedSaveSlot = -1;
                Program.currentScreen = GameScreen.LoadGame;
            }
            else if (Program.ui.OptionsButtonHovered)
            {
                Program.ui.ShowMainMenuOptions = true;
                Program.ui.OptionsMusicVolume = Program.musicManager.Volume;
                Program.ui.OptionsSoundVolume = SoundManager.Volume;
            }
            else if (Program.ui.QuitButtonHovered)
            {
                Program.shouldQuit = true;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            Program.game = new Game();
            Program.game.Initialize();
            if (Program.game.GameContext != null)
            {
                Program.game.GameContext.WorldMap = Program.worldMap;
            }
            Program.ui.SelectedCountryId = null;
            Program.currentScreen = GameScreen.CountrySelect;
        }
    }

    public void Draw()
    {
        var bgTexture = TextureManager.MenuBackground;
        if (bgTexture != null)
        {
            var tex = bgTexture.Value;
            float scaleX = (float)Program.ScreenWidth / tex.Width;
            float scaleY = (float)Program.ScreenHeight / tex.Height;
            float scale = Math.Max(scaleX, scaleY);

            int drawW = (int)(tex.Width * scale);
            int drawH = (int)(tex.Height * scale);
            int drawX = (Program.ScreenWidth - drawW) / 2;
            int drawY = (Program.ScreenHeight - drawH) / 2;

            Rectangle sourceRec = new Rectangle(0, 0, tex.Width, tex.Height);
            Rectangle destRec = new Rectangle(drawX, drawY, drawW, drawH);
            Raylib.DrawTexturePro(tex, sourceRec, destRec, new System.Numerics.Vector2(0, 0), 0, Color.White);

            Raylib.DrawRectangle(0, 0, Program.ScreenWidth, Program.ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)120));
        }

        Program.DrawGameTitle(Program.ScreenWidth / 2, 160);

        string subtitle = "Build Your Economic Dominance";
        int subWidth = Program.MeasureTextCached(subtitle, 30);
        Program.DrawGameText(subtitle, (Program.ScreenWidth - subWidth) / 2, 240, 30, ColorPalette.TextGray);

        Program.DrawMenuButton(Program.ui.NewGameButtonRect, "Neues Spiel", Program.ui.NewGameButtonHovered);
        Program.DrawMenuButton(Program.ui.LoadGameButtonRect, "Spiel Laden", Program.ui.LoadGameButtonHovered);
        Program.DrawMenuButton(Program.ui.OptionsButtonRect, "Optionen", Program.ui.OptionsButtonHovered);
        Program.DrawMenuButton(Program.ui.QuitButtonRect, "Beenden", Program.ui.QuitButtonHovered);

        Program.DrawGameText("v0.1", 11, Program.ScreenHeight - 25, GameConfig.FONT_SIZE_SMALL + 2, ColorPalette.TextGray);

        if (Program.ui.ShowMainMenuOptions)
        {
            DrawMainMenuOptionsPanel();
        }
    }

    private void UpdateMainMenuOptions(Vector2 mousePos)
    {
        int menuW = 480;
        int menuH = 460;
        int menuX = (Program.ScreenWidth - menuW) / 2;
        int menuY = (Program.ScreenHeight - menuH) / 2;

        int closeBtnSize = 30;
        int closeBtnX = menuX + menuW - closeBtnSize - 10;
        int closeBtnY = menuY + 10;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);

        int backBtnW = 360;
        int backBtnH = 40;
        int backBtnX = menuX + (menuW - backBtnW) / 2;
        int backBtnY = menuY + menuH - backBtnH - 20;
        Rectangle backRect = new Rectangle(backBtnX, backBtnY, backBtnW, backBtnH);

        int sliderX = menuX + 40;
        int sliderW = menuW - 80;
        int sliderH = 12;

        int soundSectionY = menuY + 70;

        int musicSliderY = soundSectionY + 68;
        Rectangle musicSliderRect = new Rectangle(sliderX, musicSliderY - 10, sliderW, sliderH + 20);

        int soundSliderY = soundSectionY + 120;
        Rectangle soundSliderRect = new Rectangle(sliderX, soundSliderY - 10, sliderW, sliderH + 20);

        int gfxSectionY = soundSectionY + 175;
        int toggleY = gfxSectionY + 42;
        int toggleW = 60;
        int toggleH = 26;
        int toggleX = menuX + menuW - 40 - toggleW;
        Rectangle toggleRect = new Rectangle(toggleX, toggleY - 2, toggleW, toggleH);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, musicSliderRect))
        {
            Program.ui.IsDraggingMusicSlider = true;
        }
        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            Program.ui.IsDraggingMusicSlider = false;
        }
        if (Program.ui.IsDraggingMusicSlider)
        {
            float newVolume = (mousePos.X - sliderX) / sliderW;
            Program.ui.OptionsMusicVolume = Math.Clamp(newVolume, 0f, 1f);
            Program.musicManager.Volume = Program.ui.OptionsMusicVolume;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, soundSliderRect))
        {
            Program.ui.IsDraggingSoundSlider = true;
        }
        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            Program.ui.IsDraggingSoundSlider = false;
        }
        if (Program.ui.IsDraggingSoundSlider)
        {
            float newVolume = (mousePos.X - sliderX) / sliderW;
            Program.ui.OptionsSoundVolume = Math.Clamp(newVolume, 0f, 1f);
            SoundManager.Volume = Program.ui.OptionsSoundVolume;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, closeRect) ||
                Raylib.CheckCollisionPointRec(mousePos, backRect))
            {
                Program.ui.ShowMainMenuOptions = false;
                Program.ui.IsDraggingMusicSlider = false;
                Program.ui.IsDraggingSoundSlider = false;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, toggleRect))
            {
                Program.ui.MainMenuDayNightCycleEnabled = !Program.ui.MainMenuDayNightCycleEnabled;
                SoundManager.Play(SoundEffect.Click);
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Program.ui.ShowMainMenuOptions = false;
            Program.ui.IsDraggingMusicSlider = false;
            Program.ui.IsDraggingSoundSlider = false;
        }
    }

    private void DrawMainMenuOptionsPanel()
    {
        Vector2 mousePos = Program._cachedMousePos;

        Raylib.DrawRectangle(0, 0, Program.ScreenWidth, Program.ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)150));

        int menuW = 480;
        int menuH = 460;
        int menuX = (Program.ScreenWidth - menuW) / 2;
        int menuY = (Program.ScreenHeight - menuH) / 2;

        Rectangle optionsRect = new(menuX, menuY, menuW, menuH);
        Rectangle optionsShadow = new(menuX + 3, menuY + 3, menuW, menuH);
        Raylib.DrawRectangleRounded(optionsShadow, 0.03f, 8, new Color((byte)0, (byte)0, (byte)0, (byte)60));
        Raylib.DrawRectangleRounded(optionsRect, 0.03f, 8, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(optionsRect, 0.03f, 8, 2, ColorPalette.Accent);

        string title = "OPTIONEN";
        int titleW = Program.MeasureTextCached(title, 32);
        Program.DrawGameText(title, menuX + (menuW - titleW) / 2, menuY + 20, 26, ColorPalette.Accent);

        int closeBtnSize = 30;
        int closeBtnX = menuX + menuW - closeBtnSize - 10;
        int closeBtnY = menuY + 10;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);
        bool hoverClose = Raylib.CheckCollisionPointRec(mousePos, closeRect);

        Raylib.DrawRectangleRec(closeRect, hoverClose ? ColorPalette.Red : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(closeRect, 1, hoverClose ? ColorPalette.Red : ColorPalette.TextGray);
        int xTextW = Program.MeasureTextCached("X", 20);
        Program.DrawGameText("X", closeBtnX + (closeBtnSize - xTextW) / 2, closeBtnY + 5, 11, ColorPalette.TextWhite);

        int contentX = menuX + 30;
        int contentW = menuW - 60;
        int sliderX = menuX + 40;
        int sliderW = menuW - 80;
        int sliderH = 12;
        int knobSize = 20;

        int soundSectionY = menuY + 70;

        Raylib.DrawRectangle(contentX, soundSectionY, contentW, 28, new Color((byte)30, (byte)35, (byte)50, (byte)255));
        Raylib.DrawRectangleLinesEx(new Rectangle(contentX, soundSectionY, contentW, 28), 1, ColorPalette.Accent);
        Program.DrawGameText("Sound-Einstellungen", contentX + 10, soundSectionY + 5, 18, ColorPalette.Accent);

        int musicLabelY = soundSectionY + 40;
        Program.DrawGameText("Musik-Lautstaerke", sliderX, musicLabelY, 18, ColorPalette.TextWhite);

        string musicPercent = $"{(int)(Program.ui.OptionsMusicVolume * 100)}%";
        int musicPercentW = Program.MeasureTextCached(musicPercent, 18);
        Program.DrawGameText(musicPercent, menuX + menuW - 40 - musicPercentW, musicLabelY, 18, ColorPalette.Accent);

        int musicSliderY = soundSectionY + 68;

        Raylib.DrawRectangle(sliderX, musicSliderY, sliderW, sliderH, ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(new Rectangle(sliderX, musicSliderY, sliderW, sliderH), 1, ColorPalette.PanelLight);

        int musicFillW = (int)(sliderW * Program.ui.OptionsMusicVolume);
        if (musicFillW > 0)
            Raylib.DrawRectangle(sliderX, musicSliderY, musicFillW, sliderH, ColorPalette.Accent);

        int musicKnobX = sliderX + musicFillW - knobSize / 2;
        int musicKnobY = musicSliderY + sliderH / 2 - knobSize / 2;
        Rectangle musicKnobRect = new Rectangle(musicKnobX, musicKnobY, knobSize, knobSize);
        bool hoverMusicKnob = Raylib.CheckCollisionPointRec(mousePos, musicKnobRect) || Program.ui.IsDraggingMusicSlider;
        Raylib.DrawRectangleRec(musicKnobRect, hoverMusicKnob ? ColorPalette.Accent : ColorPalette.TextWhite);
        Raylib.DrawRectangleLinesEx(musicKnobRect, 1, ColorPalette.Accent);

        int soundLabelY = soundSectionY + 92;
        Program.DrawGameText("Sound-Lautstaerke", sliderX, soundLabelY, 18, ColorPalette.TextWhite);

        string soundPercent = $"{(int)(Program.ui.OptionsSoundVolume * 100)}%";
        int soundPercentW = Program.MeasureTextCached(soundPercent, 18);
        Program.DrawGameText(soundPercent, menuX + menuW - 40 - soundPercentW, soundLabelY, 18, ColorPalette.Accent);

        int soundSliderY = soundSectionY + 120;

        Raylib.DrawRectangle(sliderX, soundSliderY, sliderW, sliderH, ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(new Rectangle(sliderX, soundSliderY, sliderW, sliderH), 1, ColorPalette.PanelLight);

        int soundFillW = (int)(sliderW * Program.ui.OptionsSoundVolume);
        if (soundFillW > 0)
            Raylib.DrawRectangle(sliderX, soundSliderY, soundFillW, sliderH, ColorPalette.Accent);

        int soundKnobX = sliderX + soundFillW - knobSize / 2;
        int soundKnobY = soundSliderY + sliderH / 2 - knobSize / 2;
        Rectangle soundKnobRect = new Rectangle(soundKnobX, soundKnobY, knobSize, knobSize);
        bool hoverSoundKnob = Raylib.CheckCollisionPointRec(mousePos, soundKnobRect) || Program.ui.IsDraggingSoundSlider;
        Raylib.DrawRectangleRec(soundKnobRect, hoverSoundKnob ? ColorPalette.Accent : ColorPalette.TextWhite);
        Raylib.DrawRectangleLinesEx(soundKnobRect, 1, ColorPalette.Accent);

        int gfxSectionY = soundSectionY + 175;

        Raylib.DrawRectangle(contentX, gfxSectionY, contentW, 28, new Color((byte)30, (byte)35, (byte)50, (byte)255));
        Raylib.DrawRectangleLinesEx(new Rectangle(contentX, gfxSectionY, contentW, 28), 1, ColorPalette.Accent);
        Program.DrawGameText("Grafik-Einstellungen", contentX + 10, gfxSectionY + 5, 18, ColorPalette.Accent);

        int toggleY = gfxSectionY + 42;
        Program.DrawGameText("Tag/Nacht-Zyklus", sliderX, toggleY, 18, ColorPalette.TextWhite);

        int toggleW = 60;
        int toggleH = 26;
        int toggleX = menuX + menuW - 40 - toggleW;
        Rectangle toggleRect = new Rectangle(toggleX, toggleY - 2, toggleW, toggleH);
        bool hoverToggle = Raylib.CheckCollisionPointRec(mousePos, toggleRect);
        bool dayNightOn = Program.ui.MainMenuDayNightCycleEnabled;

        Color toggleBg = dayNightOn ? ColorPalette.Accent : ColorPalette.Background;
        Raylib.DrawRectangleRec(toggleRect, toggleBg);
        Raylib.DrawRectangleLinesEx(toggleRect, 1, dayNightOn ? ColorPalette.Accent : ColorPalette.PanelLight);

        int knobW = 26;
        int knobX = dayNightOn ? toggleX + toggleW - knobW : toggleX;
        Raylib.DrawRectangle(knobX, (int)toggleRect.Y, knobW, toggleH, hoverToggle ? ColorPalette.TextWhite : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(new Rectangle(knobX, toggleRect.Y, knobW, toggleH), 1, ColorPalette.TextWhite);

        string toggleStatus = dayNightOn ? "AN" : "AUS";
        Color toggleStatusColor = dayNightOn ? new Color((byte)100, (byte)255, (byte)100, (byte)255) : ColorPalette.TextGray;
        int statusW = Program.MeasureTextCached(toggleStatus, 16);
        Program.DrawGameText(toggleStatus, toggleX - statusW - 10, toggleY + 1, 16, toggleStatusColor);

        int backBtnW = 360;
        int backBtnH = 40;
        int backBtnX = menuX + (menuW - backBtnW) / 2;
        int backBtnY = menuY + menuH - backBtnH - 20;
        Rectangle backRect = new Rectangle(backBtnX, backBtnY, backBtnW, backBtnH);
        bool hoverBack = Raylib.CheckCollisionPointRec(mousePos, backRect);
        Program.DrawMenuButton(backRect, "Zurueck", hoverBack);
    }
}
