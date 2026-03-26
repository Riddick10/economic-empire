using Raylib_cs;

namespace GrandStrategyGame.UI;

/// <summary>
/// Hilfsfunktionen fuer UI-Zeichenoperationen.
/// </summary>
public static class UIHelper
{
    /// <summary>
    /// Zeichnet einen Fortschrittsbalken
    /// </summary>
    public static void DrawProgressBar(int x, int y, int width, int height, float progress, Color fillColor)
    {
        float roundness = 0.4f;
        int segments = 8;
        progress = Math.Clamp(progress, 0f, 1f);

        Rectangle barRect = new(x, y, width, height);

        // Schatten
        Rectangle shadowRect = new(x + 1, y + 2, width, height);
        Raylib.DrawRectangleRounded(shadowRect, roundness, segments, new Color((byte)0, (byte)0, (byte)0, (byte)60));

        // Hintergrund (eingelassen)
        Raylib.DrawRectangleRounded(barRect, roundness, segments, new Color((byte)20, (byte)20, (byte)30, (byte)255));
        Raylib.DrawRectangleRoundedLinesEx(barRect, roundness, segments, 1, new Color((byte)60, (byte)60, (byte)80, (byte)255));

        // Fuellung
        int fillWidth = (int)(width * progress);
        if (fillWidth > 6)
        {
            Rectangle fillRect = new(x + 2, y + 2, fillWidth - 4, height - 4);
            Raylib.DrawRectangleRounded(fillRect, roundness, segments, fillColor);

            // Obere Haelfte heller fuer Glanz-Effekt
            Rectangle glossRect = new(x + 3, y + 3, fillWidth - 6, (height - 4) / 2);
            Color glossColor = new((byte)Math.Min(255, fillColor.R + 40),
                                   (byte)Math.Min(255, fillColor.G + 40),
                                   (byte)Math.Min(255, fillColor.B + 40), (byte)80);
            Raylib.DrawRectangleRounded(glossRect, roundness, segments, glossColor);

            // Animierter Schimmer-Effekt
            float time = (float)Raylib.GetTime();
            float shimmerPos = ((time * 0.5f) % 1.4f) - 0.2f; // Wandert von links nach rechts
            int shimmerX = x + 2 + (int)((fillWidth - 4) * shimmerPos);
            int shimmerW = Math.Max(20, (int)((fillWidth - 4) * 0.15f));
            if (shimmerX > x + 2 && shimmerX + shimmerW < x + fillWidth - 2)
            {
                Rectangle shimmerRect = new(shimmerX, y + 3, shimmerW, height - 6);
                Raylib.DrawRectangleRounded(shimmerRect, 0.5f, 4,
                    new Color((byte)255, (byte)255, (byte)255, (byte)30));
            }
        }
    }
}
