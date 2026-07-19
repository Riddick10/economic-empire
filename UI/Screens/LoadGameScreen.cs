using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Spielstand-Laden-Bildschirm im "Living World"-Stil:
/// Weltkarte als Kulisse, Glas-Slotkarten mit Goldakzent, Flagge und
/// gestaffelter Einblend-Animation wie im Hauptmenue
/// </summary>
internal class LoadGameScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.LoadGame;

    private const int SlotWidth = 640;
    private const int SlotHeight = 128;
    private const int SlotSpacing = 18;

    private float _time;
    private readonly float[] _slotHover = new float[4];
    private float _backHover;

    public void Enter()
    {
        _time = 0f;
        Array.Clear(_slotHover, 0, _slotHover.Length);
        _backHover = 0f;
    }

    public void Exit() { }

    private static int SlotStartY() => Math.Max(170, (int)(Program.ScreenHeight * 0.22f));

    public void Update()
    {
        float dt = Raylib.GetFrameTime();
        _time += dt;

        // Karte lebt weiter (gleiche sanfte Kamerafahrt wie im Hauptmenue)
        Program.worldMap.Move(new Vector2(MathF.Cos(_time * 0.055f) * 9f * dt, 0));

        Vector2 mousePos = Program._cachedMousePos;

        Program.ui.BackButtonRect = new Rectangle(48, 40, 170, 48);
        Program.ui.BackButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.BackButtonRect);

        int slotStartY = SlotStartY();

        // 4 Slots: Index 0-2 = manuelle Slots 1-3, Index 3 = Autosave
        for (int i = 0; i < 4; i++)
        {
            Program.ui.SaveSlotRects[i] = new Rectangle(
                (Program.ScreenWidth - SlotWidth) / 2,
                slotStartY + i * (SlotHeight + SlotSpacing),
                SlotWidth,
                SlotHeight
            );

            Program.ui.DeleteSlotRects[i] = new Rectangle(
                Program.ui.SaveSlotRects[i].X + SlotWidth - 46,
                Program.ui.SaveSlotRects[i].Y + 12,
                32, 32
            );

            Program.ui.SaveSlotHovered[i] = Raylib.CheckCollisionPointRec(mousePos, Program.ui.SaveSlotRects[i]);
            Program.ui.DeleteSlotHovered[i] = Program.ui.SaveSlots[i] != null &&
                Raylib.CheckCollisionPointRec(mousePos, Program.ui.DeleteSlotRects[i]);
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Program.ui.BackButtonHovered)
            {
                SoundManager.Play(SoundEffect.Click);
                Program.currentScreen = GameScreen.MainMenu;
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                // UI-Index auf Slot-Nummer abbilden (Index 3 = Autosave-Slot 0)
                int slotNumber = i == 3 ? SaveGameManager.AutosaveSlot : i + 1;

                if (Program.ui.DeleteSlotHovered[i])
                {
                    SoundManager.Play(SoundEffect.Click);
                    SaveGameManager.DeleteSlot(slotNumber);
                    Program.ui.SaveSlots = SaveGameManager.GetAllSlots();
                    return;
                }

                if (Program.ui.SaveSlotHovered[i] && Program.ui.SaveSlots[i] != null)
                {
                    SoundManager.Play(SoundEffect.Click);
                    var saveData = SaveGameManager.LoadGame(slotNumber);
                    if (saveData != null)
                    {
                        Program.game = new Game();
                        Program.game.Initialize();

                        // WorldMap-Referenz VOR ApplySaveData setzen, damit
                        // Provinz-Resync, Minenproduktion und KI funktionieren
                        if (Program.game.GameContext != null)
                        {
                            Program.game.GameContext.WorldMap = Program.worldMap;
                        }

                        SaveGameManager.ApplySaveData(saveData, Program.game, Program.worldMap);

                        Program.game.SelectPlayerCountry(saveData.PlayerCountryId);

                        Program.worldMap.DayNightCycleEnabled = Program.ui.MainMenuDayNightCycleEnabled;

                        Program.currentScreen = GameScreen.Playing;
                    }
                    return;
                }
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Program.currentScreen = GameScreen.MainMenu;
        }
    }

    public void Draw()
    {
        float dt = Raylib.GetFrameTime();
        int w = Program.ScreenWidth;
        int h = Program.ScreenHeight;

        DrawBackground(w, h);

        MenuStyle.DrawScreenTitle("SPIEL LADEN", w / 2, Math.Max(60, (int)(h * 0.08f)));

        _backHover += ((Program.ui.BackButtonHovered ? 1f : 0f) - _backHover) * Math.Min(1f, dt * 12f);
        MenuStyle.DrawBackButton(Program.ui.BackButtonRect, _backHover);

        bool hasAnySave = false;
        for (int i = 0; i < 4; i++)
        {
            hasAnySave |= Program.ui.SaveSlots[i] != null;

            float target = Program.ui.SaveSlotHovered[i] && Program.ui.SaveSlots[i] != null ? 1f : 0f;
            _slotHover[i] += (target - _slotHover[i]) * Math.Min(1f, dt * 12f);

            // Gestaffelt von links einschieben (wie die Hauptmenue-Buttons)
            float entry = Math.Clamp((_time - 0.05f - i * 0.08f) / 0.35f, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - entry, 3f);

            DrawSlotCard(i, Program.ui.SaveSlotRects[i], Program.ui.SaveSlots[i],
                _slotHover[i], Program.ui.DeleteSlotHovered[i], ease);
        }

        if (!hasAnySave)
        {
            string noSaves = "Noch keine Spielstände - starte ein neues Spiel!";
            int noSavesW = Program.MeasureTextCached(noSaves, 17);
            int msgY = SlotStartY() + 4 * (SlotHeight + SlotSpacing) + 12;
            Program.DrawGameText(noSaves, (w - noSavesW) / 2, msgY, 17,
                new Color((byte)165, (byte)172, (byte)188, (byte)220));
        }

        // Tastatur-Hinweis unten
        string hint = "ESC = Zurück";
        int hintW = Program.MeasureTextCached(hint, 14);
        Program.DrawGameText(hint, (w - hintW) / 2, h - 36, 14,
            new Color((byte)140, (byte)145, (byte)160, (byte)170));
    }

    /// <summary>
    /// Weltkarte als Kulisse mit Vignetten und dunklem Band hinter den Slots
    /// </summary>
    private static void DrawBackground(int w, int h)
    {
        Program.worldMap.Draw(null, null, null, null, null);

        // Grundabdunkelung + Vignetten oben/unten
        Raylib.DrawRectangle(0, 0, w, h, new Color((byte)4, (byte)6, (byte)12, (byte)120));
        Raylib.DrawRectangleGradientV(0, 0, w, 150,
            new Color((byte)4, (byte)5, (byte)10, (byte)210), new Color((byte)4, (byte)5, (byte)10, (byte)0));
        Raylib.DrawRectangleGradientV(0, h - 150, w, 150,
            new Color((byte)4, (byte)5, (byte)10, (byte)0), new Color((byte)4, (byte)5, (byte)10, (byte)210));

        // Dunkles Band hinter der Slot-Spalte als Buehne
        int bandW = SlotWidth + 200;
        int bandX = (w - bandW) / 2;
        Raylib.DrawRectangleGradientH(bandX, 0, bandW / 2, h,
            new Color((byte)4, (byte)6, (byte)12, (byte)0), new Color((byte)4, (byte)6, (byte)12, (byte)185));
        Raylib.DrawRectangleGradientH(bandX + bandW / 2, 0, bandW / 2, h,
            new Color((byte)4, (byte)6, (byte)12, (byte)185), new Color((byte)4, (byte)6, (byte)12, (byte)0));
    }

    /// <summary>
    /// Glas-Slotkarte: Badge, Flagge, Name, Spieldatum, Budget/Bevoelkerung,
    /// Zeitstempel, Loeschen-Kreuz und Hover-Chevron
    /// </summary>
    private static void DrawSlotCard(int index, Rectangle rect, SaveSlotInfo? slot,
        float hover, bool deleteHovered, float entry)
    {
        if (entry <= 0f) return;

        float xOff = (1f - entry) * -50f;
        var r = new Rectangle(rect.X + xOff, rect.Y, rect.Width, rect.Height);
        bool isAutosave = index == 3;
        bool empty = slot == null;

        MenuStyle.DrawGlassCard(r, hover, empty ? entry * 0.55f : entry);

        byte A(float v) => (byte)Math.Clamp(v * entry, 0, 255);

        // Slot-Badge oben links
        string badge = isAutosave ? "AUTOSAVE" : $"SLOT {index + 1}";
        Color badgeColor = isAutosave
            ? new Color((byte)120, (byte)220, (byte)140, A(230))
            : MenuStyle.Gold(A(230));
        Program.DrawGameText(badge, (int)r.X + 18, (int)r.Y + 12, 13, badgeColor);

        if (empty)
        {
            string emptyText = "- LEER -";
            int tw = Program.MeasureTextCached(emptyText, 18);
            Program.DrawGameText(emptyText, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 18) / 2),
                18, new Color((byte)120, (byte)126, (byte)142, A(170)));
            return;
        }

        int x = (int)r.X + 18;
        int y = (int)r.Y + 38;

        // Flagge mit feinem Rand
        var flag = TextureManager.GetFlag(slot!.CountryId);
        if (flag.HasValue)
        {
            float scale = 46f / flag.Value.Height;
            int fw = (int)(flag.Value.Width * scale);
            Raylib.DrawTextureEx(flag.Value, new Vector2(x, y), 0, scale,
                new Color((byte)255, (byte)255, (byte)255, A(255)));
            Raylib.DrawRectangleLines(x, y, fw, 46, new Color((byte)255, (byte)255, (byte)255, A(50)));
            x += fw + 14;
        }

        // Name + Spieldatum + Wirtschaftsdaten
        Program.DrawGameText(slot.Name, x, y, 19, new Color((byte)240, (byte)243, (byte)250, A(255)));
        Program.DrawGameText(slot.GameDate, x, y + 26, 14, MenuStyle.Gold(A(210)));

        string info = $"{Formatting.Money(slot.Budget)}   Bev. {Formatting.Population(slot.Population)}";
        Program.DrawGameText(info, x, y + 46, 13, new Color((byte)165, (byte)172, (byte)188, A(220)));

        // Zeitstempel unten rechts
        string savedAt = $"{slot.SavedAt:dd.MM.yyyy HH:mm}";
        int saW = Program.MeasureTextCached(savedAt, 12);
        Program.DrawGameText(savedAt, (int)(r.X + r.Width - saW - 18), (int)(r.Y + r.Height - 24), 12,
            new Color((byte)140, (byte)146, (byte)162, A(190)));

        // Loeschen-Kreuz oben rechts (roter Kreis beim Hover)
        var del = Program.ui.DeleteSlotRects[index];
        var delCenter = new Vector2(del.X + del.Width / 2f + xOff, del.Y + del.Height / 2f);
        if (deleteHovered)
            Raylib.DrawCircleV(delCenter, del.Width / 2f, new Color((byte)205, (byte)80, (byte)70, A(150)));
        Color xc = deleteHovered
            ? new Color((byte)255, (byte)255, (byte)255, A(255))
            : new Color((byte)150, (byte)156, (byte)172, A(200));
        const float s = 4.5f;
        Raylib.DrawLineEx(new Vector2(delCenter.X - s, delCenter.Y - s), new Vector2(delCenter.X + s, delCenter.Y + s), 2f, xc);
        Raylib.DrawLineEx(new Vector2(delCenter.X - s, delCenter.Y + s), new Vector2(delCenter.X + s, delCenter.Y - s), 2f, xc);

        // Hover-Chevron rechts (Laden)
        if (hover > 0.05f)
        {
            MenuStyle.DrawChevron(r.X + r.Width - 26 + hover * 5, r.Y + r.Height / 2f, 1,
                new Color((byte)255, (byte)215, (byte)130, A(230 * hover)));
        }
    }
}
