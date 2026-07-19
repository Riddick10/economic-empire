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
    // === Speichern-Panel im Living-World-Stil ===

    // Ein-/Ausblenden und weiche Hover-/Auswahl-Zustaende
    internal static float _savePanelAnim;
    static readonly float[] _saveSlotAnim = new float[3];
    static float _saveConfirmHover;
    static float _saveCloseHover;

    /// <summary>
    /// Gemeinsames Layout des Speichern-Panels fuer Update und Draw
    /// </summary>
    internal static (Rectangle Panel, Rectangle Close, Rectangle[] Slots, Rectangle Confirm)
        GetSavePanelLayout()
    {
        const int panelW = 620;
        const int panelH = 560;
        const int pad = 36;
        int x = (ScreenWidth - panelW) / 2;
        int y = (ScreenHeight - panelH) / 2;

        const int slotH = 110;
        const int slotSpacing = 16;
        int slotW = panelW - pad * 2;
        int startY = y + 92;

        var slots = new Rectangle[3];
        for (int i = 0; i < 3; i++)
            slots[i] = new Rectangle(x + pad, startY + i * (slotH + slotSpacing), slotW, slotH);

        return (
            new Rectangle(x, y, panelW, panelH),
            new Rectangle(x + panelW - 44, y + 12, 32, 32),
            slots,
            new Rectangle(x + (panelW - 360) / 2, startY + 3 * (slotH + slotSpacing) + 6, 360, 52));
    }

    /// <summary>
    /// Zeichnet das Speichern-Panel als Overlay im Spiel
    /// </summary>
    static void DrawSavePanelOverlay()
    {
        float dt = Raylib.GetFrameTime();
        Vector2 mousePos = _cachedMousePos;
        bool open = ui.ShowSavePanel;

        float target = open ? 1f : 0f;
        _savePanelAnim += (target - _savePanelAnim) * Math.Min(1f, dt * 11f);
        float ease = 1f - MathF.Pow(1f - Math.Clamp(_savePanelAnim, 0f, 1f), 3f);
        if (ease <= 0.01f) return;
        byte A(float v) => (byte)Math.Clamp(v * ease, 0, 255);

        // Spielszene abdunkeln
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight,
            new Color((byte)2, (byte)3, (byte)7, A(175)));

        var l = GetSavePanelLayout();
        int oy = (int)((1f - ease) * 18f);
        Rectangle Shift(Rectangle r) => new(r.X, r.Y + oy, r.Width, r.Height);

        var panel = Shift(l.Panel);
        var close = Shift(l.Close);
        var confirm = Shift(l.Confirm);

        DrawGlassPanelFrame(panel, ease);

        int px = (int)panel.X, py = (int)panel.Y, pw = (int)panel.Width, ph = (int)panel.Height;
        Color gold = MenuStyle.Gold(A(255));

        // Titel mit Disketten-Icon
        const string title = "SPIEL SPEICHERN";
        int titleW = MeasureTextCached(title, 30);
        int titleX = px + (pw - titleW) / 2 + 14;
        int titleY = py + 24;
        float icx = titleX - 32, icy = titleY + 15;
        Raylib.DrawRectangleLinesEx(new Rectangle(icx - 9, icy - 9, 18, 18), 2, gold);
        Raylib.DrawRectangle((int)icx - 4, (int)icy - 9, 8, 5, gold);
        Raylib.DrawRectangle((int)icx - 5, (int)icy + 1, 10, 6, gold);
        DrawGameText(title, titleX, titleY, 30, new Color((byte)245, (byte)240, (byte)228, A(255)));

        // Trennlinie unterm Titel
        Raylib.DrawRectangle(px + 36, py + 72, pw - 72, 1, new Color((byte)255, (byte)255, (byte)255, A(24)));

        // Slot-Karten
        for (int i = 0; i < 3; i++)
        {
            var slotRect = Shift(l.Slots[i]);
            bool isSelected = ui.SelectedSaveSlot == i;
            bool hovered = open && ui.SaveSlotHovered[i];

            float t = isSelected ? 1f : (hovered ? 0.55f : 0f);
            _saveSlotAnim[i] += (t - _saveSlotAnim[i]) * Math.Min(1f, dt * 12f);

            DrawSaveSlotCard(i, slotRect, ui.SaveSlots[i], _saveSlotAnim[i], isSelected,
                open && ui.DeleteSlotHovered[i], ease, oy);
        }

        // Bestaetigen-Button (nur wenn Slot ausgewaehlt)
        if (ui.SelectedSaveSlot >= 0)
        {
            bool hoverConfirm = open && ui.ConfirmSaveButtonHovered;
            _saveConfirmHover += ((hoverConfirm ? 1f : 0f) - _saveConfirmHover) * Math.Min(1f, dt * 12f);
            string saveText = ui.SaveSlots[ui.SelectedSaveSlot] != null ? "UEBERSCHREIBEN" : "SPEICHERN";
            DrawPauseButton(confirm, saveText, null, _saveConfirmHover, ease);
        }

        // Schliessen-Kreuz
        bool hoverClose = open && Raylib.CheckCollisionPointRec(mousePos, close);
        _saveCloseHover += ((hoverClose ? 1f : 0f) - _saveCloseHover) * Math.Min(1f, dt * 12f);
        MenuStyle.DrawCloseButton(close, _saveCloseHover, ease);

        // Hinweis unter dem Panel
        string hint = "F9 = Schnellspeichern (Slot 1)   |   ESC = Zurueck";
        int hintW = MeasureTextCached(hint, 13);
        DrawGameText(hint, px + (pw - hintW) / 2, py + ph + 10, 13,
            new Color((byte)150, (byte)155, (byte)170, A(150)));
    }

    /// <summary>
    /// Glas-Slotkarte im Stil des Lade-Screens, mit Auswahl-Zustand
    /// </summary>
    static void DrawSaveSlotCard(int index, Rectangle r, SaveSlotInfo? slot, float hover,
        bool isSelected, bool deleteHovered, float ease, int oy)
    {
        byte A(float v) => (byte)Math.Clamp(v * ease, 0, 255);
        bool empty = slot == null;

        // Leere Slots gedimmt, ausser sie sind ausgewaehlt (Speicherziel)
        MenuStyle.DrawGlassCard(r, hover, empty && !isSelected ? ease * 0.7f : ease);

        // Slot-Badge oben links
        DrawGameText($"SLOT {index + 1}", (int)r.X + 18, (int)r.Y + 12, 13, MenuStyle.Gold(A(230)));

        // Auswahl-Markierung oben rechts (links neben dem Loeschen-Kreuz)
        if (isSelected)
        {
            const string sel = "AUSGEWAEHLT";
            int selW = MeasureTextCached(sel, 12);
            DrawGameText(sel, (int)(r.X + r.Width - selW - 58), (int)r.Y + 14, 12, MenuStyle.Gold(A(220)));
        }

        if (empty)
        {
            const string emptyText = "- LEER -";
            int tw = MeasureTextCached(emptyText, 18);
            DrawGameText(emptyText, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 18) / 2 + 6),
                18, new Color((byte)150, (byte)156, (byte)172, A(190)));
            return;
        }

        int x = (int)r.X + 18;
        int y = (int)r.Y + 34;

        // Flagge mit feinem Rand
        var flag = TextureManager.GetFlag(slot!.CountryId);
        if (flag.HasValue)
        {
            float scale = 40f / flag.Value.Height;
            int fw = (int)(flag.Value.Width * scale);
            Raylib.DrawTextureEx(flag.Value, new Vector2(x, y), 0, scale,
                new Color((byte)255, (byte)255, (byte)255, A(255)));
            Raylib.DrawRectangleLines(x, y, fw, 40, new Color((byte)255, (byte)255, (byte)255, A(50)));
            x += fw + 14;
        }

        // Name + Spieldatum + Budget
        DrawGameText(slot.Name, x, y, 18, new Color((byte)240, (byte)243, (byte)250, A(255)));
        DrawGameText(slot.GameDate, x, y + 24, 13, MenuStyle.Gold(A(210)));
        DrawGameText(Formatting.Money(slot.Budget), x, y + 42, 12,
            new Color((byte)165, (byte)172, (byte)188, A(220)));

        // Zeitstempel unten rechts
        string savedAt = $"{slot.SavedAt:dd.MM.yyyy HH:mm}";
        int saW = MeasureTextCached(savedAt, 12);
        DrawGameText(savedAt, (int)(r.X + r.Width - saW - 18), (int)(r.Y + r.Height - 22), 12,
            new Color((byte)140, (byte)146, (byte)162, A(190)));

        // Loeschen-Kreuz (roter Kreis beim Hover)
        var del = ui.DeleteSlotRects[index];
        var delCenter = new Vector2(del.X + del.Width / 2f, del.Y + del.Height / 2f + oy);
        if (deleteHovered)
            Raylib.DrawCircleV(delCenter, del.Width / 2f, new Color((byte)205, (byte)80, (byte)70, A(150)));
        Color xc = deleteHovered
            ? new Color((byte)255, (byte)255, (byte)255, A(255))
            : new Color((byte)150, (byte)156, (byte)172, A(200));
        const float s = 4.5f;
        Raylib.DrawLineEx(new Vector2(delCenter.X - s, delCenter.Y - s), new Vector2(delCenter.X + s, delCenter.Y + s), 2f, xc);
        Raylib.DrawLineEx(new Vector2(delCenter.X - s, delCenter.Y + s), new Vector2(delCenter.X + s, delCenter.Y - s), 2f, xc);
    }

    // === Pause-Menue im Living-World-Stil ===

    // Ein-/Ausblenden und weiche Hover-/Schalter-Zustaende
    internal static float _pauseMenuAnim;
    static readonly float[] _pauseBtnHover = new float[3];
    static float _pauseCloseHover;
    static float _pauseBackHover;
    static float _pauseDayNightAnim;

    /// <summary>
    /// Gemeinsames Layout der Pause-Hauptansicht fuer Update und Draw
    /// (eine Quelle - vorher wichen Klickflaechen und Zeichnung voneinander ab)
    /// </summary>
    internal static (Rectangle Panel, Rectangle Close, Rectangle Save, Rectangle Options, Rectangle MainMenu)
        GetPauseMenuLayout()
    {
        const int menuW = 480;
        const int menuH = 340;
        const int pad = 36;
        int x = (ScreenWidth - menuW) / 2;
        int y = (ScreenHeight - menuH) / 2;

        int btnW = menuW - pad * 2;
        const int btnH = 54;
        const int spacing = 66;
        int startY = y + 96;

        return (
            new Rectangle(x, y, menuW, menuH),
            new Rectangle(x + menuW - 44, y + 12, 32, 32),
            new Rectangle(x + pad, startY, btnW, btnH),
            new Rectangle(x + pad, startY + spacing, btnW, btnH),
            new Rectangle(x + pad, startY + spacing * 2, btnW, btnH));
    }

    /// <summary>
    /// Gemeinsames Layout der Pause-Optionen (wie Hauptmenue-Optionen,
    /// plus Zeile fuer den aktuellen Musik-Track)
    /// </summary>
    internal static (Rectangle Panel, Rectangle Close, Rectangle Back, Rectangle MusicTrack,
        Rectangle SoundTrack, Rectangle Toggle) GetPauseOptionsLayout()
    {
        const int menuW = 520;
        const int menuH = 520;
        const int pad = 36;
        int x = (ScreenWidth - menuW) / 2;
        int y = (ScreenHeight - menuH) / 2;

        int soundHeaderY = y + 96;
        int musicTrackY = soundHeaderY + 64;
        int soundTrackY = musicTrackY + 64;
        int gfxHeaderY = soundTrackY + 78;   // +Track-Namen-Zeile
        int toggleY = gfxHeaderY + 34;

        return (
            new Rectangle(x, y, menuW, menuH),
            new Rectangle(x + menuW - 44, y + 12, 32, 32),
            new Rectangle(x + pad, y + menuH - 74, menuW - pad * 2, 50),
            new Rectangle(x + pad, musicTrackY, menuW - pad * 2, 10),
            new Rectangle(x + pad, soundTrackY, menuW - pad * 2, 10),
            new Rectangle(x + menuW - pad - 58, toggleY, 58, 28));
    }

    /// <summary>
    /// Glas-Panel-Rahmen: Schatten, dunkles Glas, Lichtkante, Rand, Goldkante
    /// </summary>
    static void DrawGlassPanelFrame(Rectangle panel, float ease)
    {
        byte A(float v) => (byte)Math.Clamp(v * ease, 0, 255);
        int px = (int)panel.X, py = (int)panel.Y, pw = (int)panel.Width, ph = (int)panel.Height;

        Raylib.DrawRectangle(px + 4, py + 6, pw, ph, new Color((byte)0, (byte)0, (byte)0, A(90)));
        Raylib.DrawRectangleRec(panel, new Color((byte)12, (byte)16, (byte)27, A(238)));
        Raylib.DrawRectangle(px, py + 3, pw, 1, new Color((byte)255, (byte)255, (byte)255, A(22)));
        Raylib.DrawRectangleLinesEx(panel, 1, new Color((byte)80, (byte)88, (byte)110, A(170)));
        Raylib.DrawRectangle(px, py, pw, 3, MenuStyle.Gold(A(210)));
    }

    /// <summary>
    /// Zeichnet das Pause-Menü als Overlay (mit Ein-/Ausblend-Animation)
    /// </summary>
    static void DrawPauseMenu()
    {
        float dt = Raylib.GetFrameTime();
        Vector2 mousePos = _cachedMousePos;
        bool open = ui.ShowPauseMenu;

        float target = open ? 1f : 0f;
        _pauseMenuAnim += (target - _pauseMenuAnim) * Math.Min(1f, dt * 11f);
        float ease = 1f - MathF.Pow(1f - Math.Clamp(_pauseMenuAnim, 0f, 1f), 3f);
        if (ease <= 0.01f) return;

        // Spielszene abdunkeln
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight,
            new Color((byte)2, (byte)3, (byte)7, (byte)Math.Clamp(165 * ease, 0, 255)));

        if (ui.ShowOptionsMenu)
            DrawPauseOptionsView(dt, mousePos, ease, open);
        else
            DrawPauseMainView(dt, mousePos, ease, open);
    }

    /// <summary>
    /// Hauptansicht: Titel + drei Glas-Buttons
    /// </summary>
    static void DrawPauseMainView(float dt, Vector2 mousePos, float ease, bool open)
    {
        byte A(float v) => (byte)Math.Clamp(v * ease, 0, 255);

        var l = GetPauseMenuLayout();
        int oy = (int)((1f - ease) * 18f);
        Rectangle Shift(Rectangle r) => new(r.X, r.Y + oy, r.Width, r.Height);

        var panel = Shift(l.Panel);
        var close = Shift(l.Close);
        var buttons = new[] { Shift(l.Save), Shift(l.Options), Shift(l.MainMenu) };

        DrawGlassPanelFrame(panel, ease);

        int px = (int)panel.X, py = (int)panel.Y, pw = (int)panel.Width, ph = (int)panel.Height;

        // Titel mit Pause-Balken
        const string title = "PAUSE";
        int titleW = MeasureTextCached(title, 30);
        int titleX = px + (pw - titleW) / 2 + 12;
        int titleY = py + 24;
        Color gold = MenuStyle.Gold(A(255));
        Raylib.DrawRectangle(titleX - 30, titleY + 6, 6, 20, gold);
        Raylib.DrawRectangle(titleX - 19, titleY + 6, 6, 20, gold);
        DrawGameText(title, titleX, titleY, 30, new Color((byte)245, (byte)240, (byte)228, A(255)));

        // Trennlinie unterm Titel
        Raylib.DrawRectangle(px + 36, py + 72, pw - 72, 1, new Color((byte)255, (byte)255, (byte)255, A(24)));

        // Buttons
        (string Label, string? Shortcut)[] entries =
        {
            ("SPIEL SPEICHERN", "F5"),
            ("OPTIONEN", null),
            ("ZURUECK ZUM HAUPTMENUE", null),
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            bool hovered = open && Raylib.CheckCollisionPointRec(mousePos, buttons[i]);
            _pauseBtnHover[i] += ((hovered ? 1f : 0f) - _pauseBtnHover[i]) * Math.Min(1f, dt * 12f);
            DrawPauseButton(buttons[i], entries[i].Label, entries[i].Shortcut, _pauseBtnHover[i], ease);
        }

        // Schliessen-Kreuz
        bool hoverClose = open && Raylib.CheckCollisionPointRec(mousePos, close);
        _pauseCloseHover += ((hoverClose ? 1f : 0f) - _pauseCloseHover) * Math.Min(1f, dt * 12f);
        MenuStyle.DrawCloseButton(close, _pauseCloseHover, ease);

        // ESC-Hinweis unter dem Panel
        string hint = "ESC = Fortsetzen";
        int hintW = MeasureTextCached(hint, 13);
        DrawGameText(hint, px + (pw - hintW) / 2, py + ph + 10, 13,
            new Color((byte)150, (byte)155, (byte)170, A(150)));
    }

    /// <summary>
    /// Pause-Button: Glas-Karte, zentrierter Text, optionales Tastenkuerzel,
    /// Hover-Chevron rechts (Stil der Hauptmenue-Buttons)
    /// </summary>
    static void DrawPauseButton(Rectangle r, string text, string? shortcut, float hover, float ease)
    {
        byte A(float v) => (byte)Math.Clamp(v * ease, 0, 255);

        MenuStyle.DrawGlassCard(r, hover, ease);

        const int fontSize = 19;
        int textW = MeasureTextCached(text, fontSize);
        int textX = (int)(r.X + (r.Width - textW) / 2f + hover * 3);
        int textY = (int)(r.Y + (r.Height - fontSize) / 2f);
        Color textColor = MenuStyle.Lerp(new Color((byte)205, (byte)210, (byte)222, A(235)),
                                         new Color((byte)255, (byte)255, (byte)255, A(255)), hover);
        DrawGameText(text, textX, textY, fontSize, textColor);

        if (shortcut != null)
        {
            int scW = MeasureTextCached(shortcut, 13);
            DrawGameText(shortcut, (int)(r.X + r.Width - scW - 40), textY + 4, 13,
                new Color((byte)140, (byte)146, (byte)162, A(180)));
        }

        if (hover > 0.05f)
        {
            MenuStyle.DrawChevron(r.X + r.Width - 22 + hover * 4, r.Y + r.Height / 2f, 1,
                new Color((byte)255, (byte)215, (byte)130, A(230 * hover)));
        }
    }

    /// <summary>
    /// Options-Ansicht des Pause-Menues (gleicher Stil wie im Hauptmenue)
    /// </summary>
    static void DrawPauseOptionsView(float dt, Vector2 mousePos, float ease, bool open)
    {
        byte A(float v) => (byte)Math.Clamp(v * ease, 0, 255);

        var l = GetPauseOptionsLayout();
        int oy = (int)((1f - ease) * 18f);
        Rectangle Shift(Rectangle r) => new(r.X, r.Y + oy, r.Width, r.Height);

        var panel = Shift(l.Panel);
        var close = Shift(l.Close);
        var back = Shift(l.Back);
        var musicTrack = Shift(l.MusicTrack);
        var soundTrack = Shift(l.SoundTrack);
        var toggle = Shift(l.Toggle);

        DrawGlassPanelFrame(panel, ease);

        int px = (int)panel.X, py = (int)panel.Y, pw = (int)panel.Width, ph = (int)panel.Height;
        const int pad = 36;
        int cx = px + pad;
        int cw = pw - pad * 2;
        Color gold = MenuStyle.Gold(A(255));

        // Titel mit Zahnrad
        const string title = "OPTIONEN";
        int titleW = MeasureTextCached(title, 30);
        int titleX = px + (pw - titleW) / 2 + 14;
        int titleY = py + 24;
        MenuStyle.DrawGearIcon(titleX - 30, titleY + 16, gold);
        DrawGameText(title, titleX, titleY, 30, new Color((byte)245, (byte)240, (byte)228, A(255)));

        // Trennlinie unterm Titel
        Raylib.DrawRectangle(cx, py + 72, cw, 1, new Color((byte)255, (byte)255, (byte)255, A(24)));

        // === Sound ===
        MenuStyle.DrawOptionsSection("SOUND", cx, (int)musicTrack.Y - 64, cw, ease);
        MenuStyle.DrawOptionSlider("Musik-Lautstaerke", musicTrack, ui.OptionsMusicVolume,
            ui.IsDraggingMusicSlider || Raylib.CheckCollisionPointRec(mousePos, MenuStyle.Inflate(musicTrack, 10, 14)), ease);
        MenuStyle.DrawOptionSlider("Sound-Lautstaerke", soundTrack, ui.OptionsSoundVolume,
            ui.IsDraggingSoundSlider || Raylib.CheckCollisionPointRec(mousePos, MenuStyle.Inflate(soundTrack, 10, 14)), ease);

        // Aktueller Musik-Track
        string? trackName = musicManager.CurrentTrackName;
        if (trackName != null)
        {
            string trackLabel = $"Aktueller Track: {trackName}";
            int trackLabelW = MeasureTextCached(trackLabel, 13);
            DrawGameText(trackLabel, px + (pw - trackLabelW) / 2, (int)soundTrack.Y + 28, 13,
                new Color((byte)140, (byte)146, (byte)162, A(190)));
        }

        // === Grafik ===
        MenuStyle.DrawOptionsSection("GRAFIK", cx, (int)toggle.Y - 34, cw, ease);

        bool dayNightOn = worldMap.DayNightCycleEnabled;
        _pauseDayNightAnim += ((dayNightOn ? 1f : 0f) - _pauseDayNightAnim) * Math.Min(1f, dt * 14f);

        DrawGameText("Tag/Nacht-Zyklus auf der Karte", cx, (int)toggle.Y + 5, 17,
            new Color((byte)210, (byte)215, (byte)228, A(235)));

        string status = dayNightOn ? "AN" : "AUS";
        int statusW = MeasureTextCached(status, 15);
        DrawGameText(status, (int)toggle.X - statusW - 12, (int)toggle.Y + 6, 15,
            dayNightOn ? gold : new Color((byte)140, (byte)146, (byte)162, A(200)));

        MenuStyle.DrawTogglePill(toggle, _pauseDayNightAnim, ease);

        // Zurueck + Schliessen
        bool hoverBack = open && Raylib.CheckCollisionPointRec(mousePos, back);
        _pauseBackHover += ((hoverBack ? 1f : 0f) - _pauseBackHover) * Math.Min(1f, dt * 12f);
        MenuStyle.DrawBackButton(back, _pauseBackHover, ease);

        bool hoverClose = open && Raylib.CheckCollisionPointRec(mousePos, close);
        _pauseCloseHover += ((hoverClose ? 1f : 0f) - _pauseCloseHover) * Math.Min(1f, dt * 12f);
        MenuStyle.DrawCloseButton(close, _pauseCloseHover, ease);

        // ESC-Hinweis unter dem Panel
        string hint = "ESC = Zurueck";
        int hintW = MeasureTextCached(hint, 13);
        DrawGameText(hint, px + (pw - hintW) / 2, py + ph + 10, 13,
            new Color((byte)150, (byte)155, (byte)170, A(150)));
    }
}
