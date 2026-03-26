using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame;

/// <summary>
/// Program - Haupt-Zeichenmethoden (Draw-Dispatch, gemeinsame UI-Helfer)
/// Screen-spezifische Zeichenmethoden sind in UI/Screens/*.cs verschoben.
/// </summary>
partial class Program
{
    static void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(ColorPalette.Background);

        // Screen-basierter Dispatch
        if (_screens.TryGetValue(currentScreen, out var screen))
        {
            screen.Draw();
        }

        // FPS-Anzeige (Toggle mit F3)
        if (ui.ShowFPS)
        {
            DrawFPSCounter();
        }

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Zeichnet FPS-Counter
    /// </summary>
    static void DrawFPSCounter()
    {
        int fps = Raylib.GetFPS();
        int y = currentScreen == GameScreen.Playing ? 46 : 8;
        DrawGameText($"{fps} fps", ScreenWidth - 100, y, 18, ColorPalette.TextGray);
    }

    /// <summary>
    /// Zeichnet einen Menu-Button (verwendet von mehreren Screens)
    /// </summary>
    internal static void DrawMenuButton(Rectangle rect, string text, bool isHovered)
    {
        float roundness = GameConfig.BUTTON_ROUNDNESS;
        int segments = 6;

        // Schatten (leicht versetzt nach unten)
        Rectangle shadowRect = new(rect.X + 2, rect.Y + 3, rect.Width, rect.Height);
        Raylib.DrawRectangleRounded(shadowRect, roundness, segments, new Color((byte)0, (byte)0, (byte)0, (byte)80));

        // Hintergrund mit Farbverlauf-Effekt (oben heller, unten dunkler)
        if (isHovered)
        {
            // Hover: hellerer Hintergrund
            Raylib.DrawRectangleRounded(rect, roundness, segments, new Color((byte)70, (byte)75, (byte)100, (byte)255));
            // Obere Haelfte etwas heller fuer Gradient-Effekt
            Rectangle topHalf = new(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height / 2 - 2);
            Raylib.DrawRectangleRounded(topHalf, roundness, segments, new Color((byte)85, (byte)90, (byte)115, (byte)100));
        }
        else
        {
            // Normal: dunkler Hintergrund
            Raylib.DrawRectangleRounded(rect, roundness, segments, ColorPalette.ButtonNormal);
            // Obere Haelfte etwas heller fuer Gradient-Effekt
            Rectangle topHalf = new(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height / 2 - 2);
            Raylib.DrawRectangleRounded(topHalf, roundness, segments, new Color((byte)55, (byte)55, (byte)75, (byte)80));
        }

        // Rand
        Color borderColor = isHovered ? ColorPalette.Accent : new Color((byte)80, (byte)80, (byte)100, (byte)255);
        Raylib.DrawRectangleRoundedLinesEx(rect, roundness, segments, GameConfig.BUTTON_BORDER, borderColor);

        // Text
        Color textColor = isHovered ? ColorPalette.TextWhite : new Color((byte)200, (byte)200, (byte)210, (byte)255);
        int textWidth = MeasureTextCached(text, GameConfig.FONT_SIZE_LARGE);
        int textX = (int)(rect.X + (rect.Width - textWidth) / 2);
        int textY = (int)(rect.Y + (rect.Height - GameConfig.FONT_SIZE_LARGE) / 2);
        DrawGameText(text, textX, textY, GameConfig.FONT_SIZE_LARGE, textColor);
    }

    /// <summary>
    /// Zeichnet den "ECONOMIC EMPIRE" Spieltitel mit Glow- und Schatteneffekt
    /// </summary>
    internal static void DrawGameTitle(int centerX, int y)
    {
        string title = "ECONOMIC EMPIRE";
        int fontSize = GameConfig.FONT_SIZE_TITLE;
        int titleWidth = MeasureTextCached(title, fontSize);
        int titleX = centerX - titleWidth / 2;

        // Weicher Glow-Effekt (grosser, transparenter Hintergrund)
        Color glowColor = new Color((byte)100, (byte)130, (byte)200, (byte)25);
        for (int i = 4; i >= 1; i--)
        {
            DrawGameText(title, titleX - i, y - i, fontSize, glowColor);
            DrawGameText(title, titleX + i, y - i, fontSize, glowColor);
            DrawGameText(title, titleX - i, y + i, fontSize, glowColor);
            DrawGameText(title, titleX + i, y + i, fontSize, glowColor);
        }

        // Schatten (dunkel, nach unten-rechts)
        DrawGameText(title, titleX + 3, y + 3, fontSize, new Color((byte)0, (byte)0, (byte)0, (byte)160));
        DrawGameText(title, titleX + 2, y + 2, fontSize, new Color((byte)0, (byte)0, (byte)0, (byte)100));

        // Outline (scharf)
        Color outlineColor = new Color((byte)0, (byte)0, (byte)0, (byte)220);
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                if (ox == 0 && oy == 0) continue;
                DrawGameText(title, titleX + ox, y + oy, fontSize, outlineColor);
            }
        }

        // Haupttext
        DrawGameText(title, titleX, y, fontSize, ColorPalette.Accent);

        // Heller Highlight-Text (leicht nach oben versetzt fuer 3D-Effekt)
        DrawGameText(title, titleX, y - 1, fontSize, new Color((byte)180, (byte)200, (byte)255, (byte)40));

        // Dekorative Linie unter dem Titel
        int lineY = y + fontSize + 8;
        int lineW = titleWidth + 40;
        int lineX = centerX - lineW / 2;

        // Gradient-Linie (von transparent -> Accent -> transparent)
        for (int i = 0; i <= lineW; i++)
        {
            float t = (float)i / lineW; // 0.0 links, 1.0 rechts
            float alpha = 1.0f - 2.0f * Math.Abs(t - 0.5f); // 0 an Raendern, 1 in Mitte
            byte a = (byte)(alpha * 180);
            Color lineCol = new Color(ColorPalette.Accent.R, ColorPalette.Accent.G, ColorPalette.Accent.B, a);
            Raylib.DrawLine(lineX + i, lineY, lineX + i, lineY + 1, lineCol);
        }
    }

    /// <summary>
    /// Zeichnet einen einzelnen Speicherplatz (verwendet von Load/Save Screens)
    /// </summary>
    internal static void DrawSaveSlot(int index, Rectangle rect, SaveSlotInfo? slotInfo, bool isHovered, bool deleteHovered, bool isSelected)
    {
        float roundness = 0.05f;
        int segments = 6;

        Color bgColor = isSelected ? new Color(60, 70, 90, 255) :
                        (isHovered ? ColorPalette.ButtonHover : ColorPalette.ButtonNormal);
        Color borderColor = isSelected ? ColorPalette.Accent :
                           (isHovered ? ColorPalette.Accent : new Color((byte)80, (byte)80, (byte)100, (byte)255));

        // Schatten
        Rectangle shadowRect = new(rect.X + 1, rect.Y + 2, rect.Width, rect.Height);
        Raylib.DrawRectangleRounded(shadowRect, roundness, segments, new Color((byte)0, (byte)0, (byte)0, (byte)40));

        Raylib.DrawRectangleRounded(rect, roundness, segments, bgColor);
        Raylib.DrawRectangleRoundedLinesEx(rect, roundness, segments, isSelected ? 3 : 2, borderColor);

        int x = (int)rect.X + 15;
        int y = (int)rect.Y + 12;

        string slotNumber = $"Slot {index + 1}";
        DrawGameText(slotNumber, x, y, 11, ColorPalette.Accent);

        if (slotInfo != null)
        {
            y += 28;

            var flagTexture = TextureManager.GetFlag(slotInfo.CountryId);
            if (flagTexture.HasValue)
            {
                float flagScale = 35f / flagTexture.Value.Height;
                Raylib.DrawTextureEx(flagTexture.Value, new System.Numerics.Vector2(x, y), 0, flagScale, Color.White);
                x += (int)(flagTexture.Value.Width * flagScale) + 10;
            }

            DrawGameText(slotInfo.Name, x, y, 18, ColorPalette.TextWhite);
            y += 25;

            DrawGameText($"Spieldatum: {slotInfo.GameDate}", x, y, 11, ColorPalette.TextGray);
            y += 20;

            string budgetStr = $"Budget: {Formatting.Money(slotInfo.Budget)}";
            string popStr = $"Bev.: {Formatting.Population(slotInfo.Population)}";
            DrawGameText($"{budgetStr}  |  {popStr}", x, y, 11, ColorPalette.TextGray);

            string savedAt = $"Gespeichert: {slotInfo.SavedAt:dd.MM.yyyy HH:mm}";
            int savedAtWidth = MeasureTextCached(savedAt, 12);
            DrawGameText(savedAt, (int)(rect.X + rect.Width - savedAtWidth - 15),
                (int)(rect.Y + rect.Height - 20), 11, ColorPalette.TextGray);

            Rectangle deleteRect = ui.DeleteSlotRects[index];
            Color deleteColor = deleteHovered ? new Color(200, 60, 60, 255) : ColorPalette.TextGray;
            DrawGameText("X", (int)deleteRect.X + 8, (int)deleteRect.Y + 4, 18, deleteColor);
        }
        else
        {
            y += 45;
            string emptyText = "- Leer -";
            int emptyWidth = MeasureTextCached(emptyText, 20);
            DrawGameText(emptyText, (int)(rect.X + (rect.Width - emptyWidth) / 2), y, 11, ColorPalette.TextGray);
        }
    }
}
