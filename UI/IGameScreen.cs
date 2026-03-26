namespace GrandStrategyGame.UI;

/// <summary>
/// Interface fuer alle Spielbildschirme.
/// Jeder Screen (Loading, MainMenu, Playing, etc.) implementiert dieses Interface.
/// </summary>
internal interface IGameScreen
{
    /// <summary>Zugehoeriger Screen-Enum-Wert</summary>
    GameScreen ScreenType { get; }

    /// <summary>Wird beim Wechsel zu diesem Screen aufgerufen</summary>
    void Enter();

    /// <summary>Wird beim Verlassen dieses Screens aufgerufen</summary>
    void Exit();

    /// <summary>Update-Logik (Input, Spiellogik)</summary>
    void Update();

    /// <summary>Rendering (wird zwischen BeginDrawing/EndDrawing aufgerufen)</summary>
    void Draw();
}
