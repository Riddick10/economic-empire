using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Spielbildschirm - Delegiert an bestehende Program-Methoden.
/// Wird spaeter schrittweise mit eigenem Code gefuellt.
/// </summary>
internal class PlayingScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.Playing;

    public void Enter() { }
    public void Exit() { }

    public void Update()
    {
        Program.UpdatePlaying();
    }

    public void Draw()
    {
        Program.DrawPlaying();
    }
}
