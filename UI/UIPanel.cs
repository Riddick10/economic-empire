using System.Numerics;
using Raylib_cs;

namespace GrandStrategyGame.UI;

/// <summary>
/// Abstrakte Basisklasse fuer UI-Panels.
/// Bietet gemeinsame Funktionalitaet wie Bounds, Sichtbarkeit und Hover-Erkennung.
/// </summary>
public abstract class UIPanel
{
    public Rectangle Bounds { get; set; }
    public bool IsVisible { get; set; }
    public bool IsHovered { get; private set; }

    public void Update(Vector2 mousePos)
    {
        IsHovered = Raylib.CheckCollisionPointRec(mousePos, Bounds);
        OnUpdate(mousePos);
    }

    public void Draw()
    {
        if (IsVisible) OnDraw();
    }

    protected abstract void OnDraw();
    protected virtual void OnUpdate(Vector2 mousePos) { }
}
