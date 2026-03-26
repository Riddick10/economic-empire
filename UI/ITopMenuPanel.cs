using System.Numerics;
using Raylib_cs;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.UI;

/// <summary>
/// Kontext-Objekt fuer Top-Menu Panels.
/// Buendelt alle benoetigten Referenzen, damit Panels keine statischen Fields brauchen.
/// </summary>
internal class TopMenuContext
{
    public Game Game { get; init; } = null!;
    public UIState UI { get; init; } = null!;
    public Vector2 MousePos { get; init; }
    public Rectangle Bounds { get; init; }
    public ManagerRefs Managers { get; init; } = null!;
    public WorldMap WorldMap { get; init; } = null!;
    public PerformanceCache Cache { get; init; } = null!;
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public Country? PlayerCountry => Game.PlayerCountry;

    /// <summary>
    /// Inhaltsbereich (Bounds abzueglich Header)
    /// </summary>
    public Rectangle ContentBounds => new(
        Bounds.X + 10,
        Bounds.Y + 45,
        Bounds.Width - 20,
        Bounds.Height - 55
    );
}

/// <summary>
/// Interface fuer alle Top-Menu Panels (HOI4-Stil).
/// Jedes Panel wird als eigene Klasse implementiert.
/// </summary>
internal interface ITopMenuPanel
{
    /// <summary>Panel-Titel (wird im Header angezeigt)</summary>
    string Title { get; }

    /// <summary>Zugehoeriger Panel-Enum-Wert</summary>
    TopMenuPanel PanelType { get; }

    /// <summary>Zeichnet den Panel-Inhalt (Header wird extern gezeichnet)</summary>
    void Draw(TopMenuContext ctx);
}
