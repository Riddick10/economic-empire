using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.UI;

namespace GrandStrategyGame;

/// <summary>
/// Program - Chart-Hilfsfunktionen (Kreisdiagramm, Politische Slider)
/// Werden von PoliticsInfoPanel und PoliticsTopMenuPanel genutzt
/// </summary>
partial class Program
{
    /// <summary>
    /// Zeichnet ein Kreisdiagramm
    /// </summary>
    internal static void DrawPieChart(int centerX, int centerY, int radius, List<(string Name, float Percentage, Color Color)> data)
    {
        float startAngle = -90f; // Start oben

        foreach (var (_, percentage, color) in data)
        {
            float sweepAngle = percentage / 100f * 360f;
            float endAngle = startAngle + sweepAngle;

            // Zeichne Kreissegment
            DrawPieSlice(centerX, centerY, radius, startAngle, endAngle, color);

            startAngle = endAngle;
        }

        // Rand um das Diagramm
        Raylib.DrawCircleLines(centerX, centerY, radius, ColorPalette.TextGray);
    }

    /// <summary>
    /// Zeichnet ein einzelnes Kreissegment
    /// </summary>
    internal static void DrawPieSlice(int centerX, int centerY, int radius, float startAngle, float endAngle, Color color)
    {
        // Konvertiere Winkel zu Radians
        float startRad = startAngle * MathF.PI / 180f;
        float endRad = endAngle * MathF.PI / 180f;

        // Anzahl der Segmente fuer glatte Kurve
        int segments = Math.Max(3, (int)((endAngle - startAngle) / 3));

        for (int i = 0; i < segments; i++)
        {
            float angle1 = startRad + (endRad - startRad) * i / segments;
            float angle2 = startRad + (endRad - startRad) * (i + 1) / segments;

            Vector2 p1 = new Vector2(centerX, centerY);
            Vector2 p2 = new Vector2(
                centerX + MathF.Cos(angle1) * radius,
                centerY + MathF.Sin(angle1) * radius
            );
            Vector2 p3 = new Vector2(
                centerX + MathF.Cos(angle2) * radius,
                centerY + MathF.Sin(angle2) * radius
            );

            Raylib.DrawTriangle(p1, p3, p2, color);
        }
    }

    /// <summary>
    /// Zeichnet einen politischen Schieberegler (0 = links, 1 = rechts)
    /// </summary>
    internal static void DrawPoliticalSlider(int x, int y, int width, float value)
    {
        int height = 18;

        // Hintergrund mit Gradient-Effekt (Links=Rot, Mitte=Grau, Rechts=Blau)
        Raylib.DrawRectangle(x, y, width / 2, height, new Color((byte)150, (byte)80, (byte)80, (byte)255));
        Raylib.DrawRectangle(x + width / 2, y, width / 2, height, new Color((byte)80, (byte)80, (byte)150, (byte)255));

        // Markierung
        int markerX = x + (int)(width * value);
        Raylib.DrawRectangle(markerX - 3, y - 2, 6, height + 4, ColorPalette.TextWhite);
        Raylib.DrawRectangle(markerX - 2, y - 1, 4, height + 2, ColorPalette.Accent);
    }
}
