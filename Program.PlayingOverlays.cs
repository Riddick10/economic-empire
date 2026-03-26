using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Data;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.UI;
using GrandStrategyGame.UI.Panels;

namespace GrandStrategyGame;

/// <summary>
/// Program - Speichern, Pause-Menü und Optionen Overlays
/// </summary>
partial class Program
{
    /// <summary>
    /// Zeichnet das Speichern-Panel als Overlay im Spiel
    /// </summary>
    static void DrawSavePanelOverlay()
    {
        // Hintergrund abdunkeln
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)180));

        // Panel-Dimensionen
        int panelWidth = 620;
        int panelHeight = 520;
        int panelX = (ScreenWidth - panelWidth) / 2;
        int panelY = (ScreenHeight - panelHeight) / 2;

        // Panel-Hintergrund (abgerundet)
        Rectangle savePanelRect = new(panelX, panelY, panelWidth, panelHeight);
        Rectangle savePanelShadow = new(panelX + 3, panelY + 3, panelWidth, panelHeight);
        Raylib.DrawRectangleRounded(savePanelShadow, 0.03f, 8, new Color((byte)0, (byte)0, (byte)0, (byte)60));
        Raylib.DrawRectangleRounded(savePanelRect, 0.03f, 8, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(savePanelRect, 0.03f, 8, 2, ColorPalette.Accent);

        // Titel
        string title = "SPIEL SPEICHERN";
        int titleWidth = MeasureTextCached(title, 32);
        DrawGameText(title, (ScreenWidth - titleWidth) / 2, panelY + 20, 26, ColorPalette.Accent);

        // Hinweis
        string hint = "F9 = Schnellspeichern (Slot 1)";
        int hintWidth = MeasureTextCached(hint, 14);
        DrawGameText(hint, (ScreenWidth - hintWidth) / 2, panelY + 52, 11, ColorPalette.TextGray);

        // X-Button (oben rechts)
        int closeBtnSize = 30;
        int closeBtnX = panelX + panelWidth - closeBtnSize - 10;
        int closeBtnY = panelY + 10;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);
        bool hoverClose = Raylib.CheckCollisionPointRec(_cachedMousePos, closeRect);

        Raylib.DrawRectangleRounded(closeRect, 0.2f, 6, hoverClose ? ColorPalette.Red : ColorPalette.PanelLight);
        Raylib.DrawRectangleRoundedLinesEx(closeRect, 0.2f, 6, 1, hoverClose ? ColorPalette.Red : ColorPalette.TextGray);
        int xTextW = MeasureTextCached("X", 20);
        DrawGameText("X", closeBtnX + (closeBtnSize - xTextW) / 2, closeBtnY + 5, 11, ColorPalette.TextWhite);

        // Speicherplaetze zeichnen
        for (int i = 0; i < 3; i++)
        {
            bool isSelected = ui.SelectedSaveSlot == i;
            DrawSaveSlotOverlay(i, ui.SaveSlotRects[i], ui.SaveSlots[i], ui.SaveSlotHovered[i] || isSelected, ui.DeleteSlotHovered[i], isSelected);
        }

        // Speichern-Button
        if (ui.SelectedSaveSlot >= 0)
        {
            Color btnColor = ui.ConfirmSaveButtonHovered ? ColorPalette.ButtonHover : ColorPalette.ButtonNormal;
            Color borderColor = ui.ConfirmSaveButtonHovered ? ColorPalette.Accent : new Color((byte)80, (byte)80, (byte)100, (byte)255);
            Raylib.DrawRectangleRounded(ui.ConfirmSaveButtonRect, GameConfig.BUTTON_ROUNDNESS, 6, btnColor);
            Raylib.DrawRectangleRoundedLinesEx(ui.ConfirmSaveButtonRect, GameConfig.BUTTON_ROUNDNESS, 6, 2, borderColor);

            string saveText = ui.SaveSlots[ui.SelectedSaveSlot] != null ? "Ueberschreiben" : "Speichern";
            int saveTextWidth = MeasureTextCached(saveText, 22);
            int saveTextX = (int)(ui.ConfirmSaveButtonRect.X + (ui.ConfirmSaveButtonRect.Width - saveTextWidth) / 2);
            int saveTextY = (int)(ui.ConfirmSaveButtonRect.Y + (ui.ConfirmSaveButtonRect.Height - 22) / 2);
            DrawGameText(saveText, saveTextX, saveTextY, 18, ui.ConfirmSaveButtonHovered ? ColorPalette.Accent : ColorPalette.TextWhite);
        }
    }

    /// <summary>
    /// Zeichnet einen Speicherslot im Overlay-Panel
    /// </summary>
    static void DrawSaveSlotOverlay(int index, Rectangle rect, SaveSlotInfo? slotInfo, bool isHovered, bool deleteHovered, bool isSelected)
    {
        // Hintergrundfarbe
        Color bgColor = isSelected ? new Color((byte)60, (byte)70, (byte)90, (byte)255) :
                        (isHovered ? ColorPalette.ButtonHover : ColorPalette.ButtonNormal);
        Color borderColor = isSelected ? ColorPalette.Accent :
                           (isHovered ? ColorPalette.Accent : ColorPalette.TextGray);

        Raylib.DrawRectangleRounded(rect, 0.05f, 6, bgColor);
        Raylib.DrawRectangleRoundedLinesEx(rect, 0.05f, 6, isSelected ? 3 : 2, borderColor);

        int x = (int)rect.X + 15;
        int y = (int)rect.Y + 10;

        // Slot-Nummer
        string slotNumber = $"Slot {index + 1}";
        DrawGameText(slotNumber, x, y, 18, ColorPalette.Accent);

        if (slotInfo != null)
        {
            // Spielstand vorhanden
            y += 24;

            // Flagge (falls vorhanden)
            var flagTexture = TextureManager.GetFlag(slotInfo.CountryId);
            if (flagTexture.HasValue)
            {
                float flagScale = 30f / flagTexture.Value.Height;
                Raylib.DrawTextureEx(flagTexture.Value, new System.Numerics.Vector2(x, y), 0, flagScale, Color.White);
                x += (int)(flagTexture.Value.Width * flagScale) + 10;
            }

            // Spielstand-Name
            DrawGameText(slotInfo.Name, x, y, 11, ColorPalette.TextWhite);
            y += 22;

            // Spieldatum und Budget
            string infoLine = $"{slotInfo.GameDate}  |  {Formatting.Money(slotInfo.Budget)}";
            DrawGameText(infoLine, x, y, 11, ColorPalette.TextGray);

            // Gespeichert am (rechts unten)
            string savedAt = $"{slotInfo.SavedAt:dd.MM.yyyy HH:mm}";
            int savedAtWidth = MeasureTextCached(savedAt, 12);
            DrawGameText(savedAt, (int)(rect.X + rect.Width - savedAtWidth - 15),
                (int)(rect.Y + rect.Height - 18), 11, ColorPalette.TextGray);

            // Loeschen-Button (X)
            Rectangle deleteRect = ui.DeleteSlotRects[index];
            Color deleteColor = deleteHovered ? new Color((byte)200, (byte)60, (byte)60, (byte)255) : ColorPalette.TextGray;
            DrawGameText("X", (int)deleteRect.X + 8, (int)deleteRect.Y + 4, 11, deleteColor);
        }
        else
        {
            // Leerer Slot
            y += 35;
            string emptyText = "- Leer -";
            int emptyWidth = MeasureTextCached(emptyText, 18);
            DrawGameText(emptyText, (int)(rect.X + (rect.Width - emptyWidth) / 2), y, 18, ColorPalette.TextGray);
        }
    }

    /// <summary>
    /// Zeichnet das Pause-Menü als Overlay
    /// </summary>
    static void DrawPauseMenu()
    {
        Vector2 mousePos = _cachedMousePos;

        // Hintergrund abdunkeln
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)150));

        // Menü-Dimensionen
        int menuW = 480;
        int menuH = ui.ShowOptionsMenu ? 460 : 305;
        int menuX = (ScreenWidth - menuW) / 2;
        int menuY = (ScreenHeight - menuH) / 2;

        // Menü-Hintergrund (abgerundet)
        Rectangle pauseMenuRect = new(menuX, menuY, menuW, menuH);
        Rectangle pauseMenuShadow = new(menuX + 3, menuY + 3, menuW, menuH);
        Raylib.DrawRectangleRounded(pauseMenuShadow, 0.03f, 8, new Color((byte)0, (byte)0, (byte)0, (byte)60));
        Raylib.DrawRectangleRounded(pauseMenuRect, 0.03f, 8, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(pauseMenuRect, 0.03f, 8, 2, ColorPalette.Accent);

        // X-Button (oben rechts)
        int closeBtnSize = 30;
        int closeBtnX = menuX + menuW - closeBtnSize - 10;
        int closeBtnY = menuY + 10;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);
        bool hoverClose = Raylib.CheckCollisionPointRec(mousePos, closeRect);

        Raylib.DrawRectangleRounded(closeRect, 0.2f, 6, hoverClose ? ColorPalette.Red : ColorPalette.PanelLight);
        Raylib.DrawRectangleRoundedLinesEx(closeRect, 0.2f, 6, 1, hoverClose ? ColorPalette.Red : ColorPalette.TextGray);
        int xTextW = MeasureTextCached("X", 20);
        DrawGameText("X", closeBtnX + (closeBtnSize - xTextW) / 2, closeBtnY + 5, 11, ColorPalette.TextWhite);

        if (ui.ShowOptionsMenu)
        {
            DrawOptionsMenu(menuX, menuY, menuW, menuH, mousePos);
        }
        else
        {
            DrawPauseMainMenu(menuX, menuY, menuW, menuH, mousePos);
        }
    }

    /// <summary>
    /// Zeichnet die Hauptansicht des Pause-Menüs
    /// </summary>
    static void DrawPauseMainMenu(int menuX, int menuY, int menuW, int menuH, Vector2 mousePos)
    {
        // Titel
        string title = "PAUSE";
        int titleW = MeasureTextCached(title, 36);
        DrawGameText(title, menuX + (menuW - titleW) / 2, menuY + 20, 32, ColorPalette.Accent);

        // Buttons
        int btnW = 420;
        int btnH = 50;
        int btnX = menuX + (menuW - btnW) / 2;
        int btnStartY = menuY + 80;
        int btnSpacing = 60;

        // "Speichern" Button
        Rectangle saveRect = new Rectangle(btnX, btnStartY, btnW, btnH);
        bool hoverSave = Raylib.CheckCollisionPointRec(mousePos, saveRect);
        DrawPauseMenuButton(saveRect, "Spiel Speichern (F5)", hoverSave);

        // "Optionen" Button
        Rectangle optionsRect = new Rectangle(btnX, btnStartY + btnSpacing, btnW, btnH);
        bool hoverOptions = Raylib.CheckCollisionPointRec(mousePos, optionsRect);
        DrawPauseMenuButton(optionsRect, "Optionen", hoverOptions);

        // "Zurueck zum Hauptmenue" Button
        Rectangle mainMenuRect = new Rectangle(btnX, btnStartY + btnSpacing * 2, btnW, btnH);
        bool hoverMainMenu = Raylib.CheckCollisionPointRec(mousePos, mainMenuRect);
        DrawPauseMenuButton(mainMenuRect, "Zurueck zum Hauptmenue", hoverMainMenu);
    }

    /// <summary>
    /// Zeichnet das Optionen-Untermenü
    /// </summary>
    static void DrawOptionsMenu(int menuX, int menuY, int menuW, int menuH, Vector2 mousePos)
    {
        // Titel
        string title = "OPTIONEN";
        int titleW = MeasureTextCached(title, 36);
        DrawGameText(title, menuX + (menuW - titleW) / 2, menuY + 20, 32, ColorPalette.Accent);

        int contentX = menuX + 30;
        int contentW = menuW - 60;
        int sliderX = menuX + 40;
        int sliderW = menuW - 80;
        int sliderH = 12;
        int knobSize = 20;

        // ========================================
        // === SOUND-EINSTELLUNGEN (Kategorie) ===
        // ========================================
        int soundSectionY = menuY + 70;

        // Kategorie-Header
        Raylib.DrawRectangle(contentX, soundSectionY, contentW, 28, new Color((byte)30, (byte)35, (byte)50, (byte)255));
        Raylib.DrawRectangleLinesEx(new Rectangle(contentX, soundSectionY, contentW, 28), 1, ColorPalette.Accent);
        DrawGameText("Sound-Einstellungen", contentX + 10, soundSectionY + 5, 18, ColorPalette.Accent);

        // === MUSIK-LAUTSTÄRKE ===
        int musicLabelY = soundSectionY + 40;
        DrawGameText("Musik-Lautstaerke", sliderX, musicLabelY, 18, ColorPalette.TextWhite);

        string musicPercent = $"{(int)(ui.OptionsMusicVolume * 100)}%";
        int musicPercentW = MeasureTextCached(musicPercent, 18);
        DrawGameText(musicPercent, menuX + menuW - 40 - musicPercentW, musicLabelY, 18, ColorPalette.Accent);

        int musicSliderY = soundSectionY + 68;

        // Slider-Hintergrund
        Raylib.DrawRectangle(sliderX, musicSliderY, sliderW, sliderH, ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(new Rectangle(sliderX, musicSliderY, sliderW, sliderH), 1, ColorPalette.PanelLight);

        // Slider-Fuellung
        int musicFillW = (int)(sliderW * ui.OptionsMusicVolume);
        if (musicFillW > 0)
            Raylib.DrawRectangle(sliderX, musicSliderY, musicFillW, sliderH, ColorPalette.Accent);

        // Slider-Knopf
        int musicKnobX = sliderX + musicFillW - knobSize / 2;
        int musicKnobY = musicSliderY + sliderH / 2 - knobSize / 2;
        Rectangle musicKnobRect = new Rectangle(musicKnobX, musicKnobY, knobSize, knobSize);
        bool hoverMusicKnob = Raylib.CheckCollisionPointRec(mousePos, musicKnobRect) || ui.IsDraggingMusicSlider;
        Raylib.DrawRectangleRec(musicKnobRect, hoverMusicKnob ? ColorPalette.Accent : ColorPalette.TextWhite);
        Raylib.DrawRectangleLinesEx(musicKnobRect, 1, ColorPalette.Accent);

        // === SOUND-LAUTSTÄRKE ===
        int soundLabelY = soundSectionY + 92;
        DrawGameText("Sound-Lautstaerke", sliderX, soundLabelY, 18, ColorPalette.TextWhite);

        string soundPercent = $"{(int)(ui.OptionsSoundVolume * 100)}%";
        int soundPercentW = MeasureTextCached(soundPercent, 18);
        DrawGameText(soundPercent, menuX + menuW - 40 - soundPercentW, soundLabelY, 18, ColorPalette.Accent);

        int soundSliderY = soundSectionY + 120;

        // Slider-Hintergrund
        Raylib.DrawRectangle(sliderX, soundSliderY, sliderW, sliderH, ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(new Rectangle(sliderX, soundSliderY, sliderW, sliderH), 1, ColorPalette.PanelLight);

        // Slider-Fuellung
        int soundFillW = (int)(sliderW * ui.OptionsSoundVolume);
        if (soundFillW > 0)
            Raylib.DrawRectangle(sliderX, soundSliderY, soundFillW, sliderH, ColorPalette.Accent);

        // Slider-Knopf
        int soundKnobX = sliderX + soundFillW - knobSize / 2;
        int soundKnobY = soundSliderY + sliderH / 2 - knobSize / 2;
        Rectangle soundKnobRect = new Rectangle(soundKnobX, soundKnobY, knobSize, knobSize);
        bool hoverSoundKnob = Raylib.CheckCollisionPointRec(mousePos, soundKnobRect) || ui.IsDraggingSoundSlider;
        Raylib.DrawRectangleRec(soundKnobRect, hoverSoundKnob ? ColorPalette.Accent : ColorPalette.TextWhite);
        Raylib.DrawRectangleLinesEx(soundKnobRect, 1, ColorPalette.Accent);

        // Aktueller Track-Name
        string? trackName = musicManager.CurrentTrackName;
        if (trackName != null)
        {
            string trackLabel = $"Aktueller Track: {trackName}";
            int trackLabelW = MeasureTextCached(trackLabel, 14);
            DrawGameText(trackLabel, menuX + (menuW - trackLabelW) / 2, soundSectionY + 145, 14, ColorPalette.TextGray);
        }

        // ===========================================
        // === GRAFIK-EINSTELLUNGEN (Kategorie) ===
        // ===========================================
        int gfxSectionY = soundSectionY + 175;

        // Kategorie-Header
        Raylib.DrawRectangle(contentX, gfxSectionY, contentW, 28, new Color((byte)30, (byte)35, (byte)50, (byte)255));
        Raylib.DrawRectangleLinesEx(new Rectangle(contentX, gfxSectionY, contentW, 28), 1, ColorPalette.Accent);
        DrawGameText("Grafik-Einstellungen", contentX + 10, gfxSectionY + 5, 18, ColorPalette.Accent);

        // Tag/Nacht-Zyklus Toggle
        int toggleY = gfxSectionY + 42;
        DrawGameText("Tag/Nacht-Zyklus", sliderX, toggleY, 18, ColorPalette.TextWhite);

        // Toggle-Button
        int toggleW = 60;
        int toggleH = 26;
        int toggleX = menuX + menuW - 40 - toggleW;
        Rectangle toggleRect = new Rectangle(toggleX, toggleY - 2, toggleW, toggleH);
        bool hoverToggle = Raylib.CheckCollisionPointRec(mousePos, toggleRect);
        bool dayNightOn = worldMap.DayNightCycleEnabled;

        Color toggleBg = dayNightOn ? ColorPalette.Accent : ColorPalette.Background;
        Raylib.DrawRectangleRec(toggleRect, toggleBg);
        Raylib.DrawRectangleLinesEx(toggleRect, 1, dayNightOn ? ColorPalette.Accent : ColorPalette.PanelLight);

        // Toggle-Knopf (Schieber)
        int knobW = 26;
        int knobX = dayNightOn ? toggleX + toggleW - knobW : toggleX;
        Raylib.DrawRectangle(knobX, (int)toggleRect.Y, knobW, toggleH, hoverToggle ? ColorPalette.TextWhite : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(new Rectangle(knobX, toggleRect.Y, knobW, toggleH), 1, ColorPalette.TextWhite);

        // Status-Text
        string toggleStatus = dayNightOn ? "AN" : "AUS";
        Color toggleStatusColor = dayNightOn ? new Color((byte)100, (byte)255, (byte)100, (byte)255) : ColorPalette.TextGray;
        int statusW = MeasureTextCached(toggleStatus, 16);
        DrawGameText(toggleStatus, toggleX - statusW - 10, toggleY + 1, 16, toggleStatusColor);

        // Zurueck-Button
        int backBtnW = 360;
        int backBtnH = 50;
        int backBtnX = menuX + (menuW - backBtnW) / 2;
        int backBtnY = menuY + menuH - backBtnH - 20;
        Rectangle backRect = new Rectangle(backBtnX, backBtnY, backBtnW, backBtnH);
        bool hoverBack = Raylib.CheckCollisionPointRec(mousePos, backRect);
        DrawPauseMenuButton(backRect, "Zurueck", hoverBack);
    }

    /// <summary>
    /// Zeichnet einen Button im Pause-Menü
    /// </summary>
    static void DrawPauseMenuButton(Rectangle rect, string text, bool isHovered)
    {
        float roundness = GameConfig.BUTTON_ROUNDNESS;
        int segments = 6;

        Color bgColor = isHovered ? ColorPalette.PanelLight : ColorPalette.Background;
        Color borderColor = isHovered ? ColorPalette.Accent : ColorPalette.PanelLight;
        Color textColor = isHovered ? ColorPalette.TextWhite : new Color((byte)200, (byte)200, (byte)210, (byte)255);

        // Schatten
        Rectangle shadowRect = new(rect.X + 1, rect.Y + 2, rect.Width, rect.Height);
        Raylib.DrawRectangleRounded(shadowRect, roundness, segments, new Color((byte)0, (byte)0, (byte)0, (byte)40));

        Raylib.DrawRectangleRounded(rect, roundness, segments, bgColor);

        // Obere Haelfte heller
        if (isHovered)
        {
            Rectangle topHalf = new(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height / 2 - 2);
            Raylib.DrawRectangleRounded(topHalf, roundness, segments, new Color((byte)255, (byte)255, (byte)255, (byte)12));
        }

        Raylib.DrawRectangleRoundedLinesEx(rect, roundness, segments, 1, borderColor);

        int fontSize = 22;
        int textWidth = MeasureTextCached(text, fontSize);
        int textX = (int)(rect.X + (rect.Width - textWidth) / 2);
        int textY = (int)(rect.Y + (rect.Height - fontSize) / 2);
        DrawGameText(text, textX, textY, fontSize, textColor);
    }

}
