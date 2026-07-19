using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.UI;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.UI.Screens;

/// <summary>
/// Hauptmenue-Bildschirm - "Living World Menu":
/// Live-Weltkarte mit Kamerafahrt und Tag/Nacht-Zyklus als Hintergrund,
/// animierte Buttons mit Icons, Goldpartikel, Zitate und Ressourcen-Ticker
/// </summary>
internal class MainMenuScreen : IGameScreen
{
    public GameScreen ScreenType => GameScreen.MainMenu;

    private const int ButtonWidth = 340;
    private const int ButtonHeight = 56;
    private const int ButtonSpacing = 68;

    // Animations-Zustand
    private float _menuTime;                          // Zeit seit Enter()
    private readonly float[] _hoverAnim = new float[4];
    private float _mapClock = 19.0f;                  // Menue-eigene Uhrzeit (Terminator wandert)
    private float _quoteTimer;
    private int _quoteIndex;
    private static readonly Random _rng = new();

    // Schwebende Goldpartikel (normalisierte Koordinaten 0..1)
    private struct Dust { public float X, Y, Speed, SwayAmp, SwayFreq, Phase, Size; public byte Alpha; }
    private Dust[] _dust = Array.Empty<Dust>();

    // Options-Overlay: Ein-/Ausblenden und weiche Hover-/Schalter-Zustaende
    private float _optionsAnim;                          // 0..1
    private float _dayNightToggleAnim;
    private readonly float[] _optHover = new float[2];   // 0 = Zurueck, 1 = Schliessen

    private static readonly (string Text, string Author)[] Quotes =
    {
        ("Der Preis ist, was du zahlst. Der Wert ist, was du bekommst.", "Warren Buffett"),
        ("Eine Investition in Wissen bringt noch immer die besten Zinsen.", "Benjamin Franklin"),
        ("Geld schläft nie.", "Gordon Gekko"),
        ("Jedes Imperium beginnt mit einer einzigen Fabrik.", "Economic Empire"),
        ("Wer den Handel kontrolliert, kontrolliert die Welt.", "Economic Empire"),
        ("Nicht Gold, sondern Vertrauen ist die Währung der Mächte.", "Economic Empire"),
        ("Das Risiko entsteht, wenn man nicht weiß, was man tut.", "Warren Buffett"),
        ("Handel hat noch keine Nation ruiniert.", "Benjamin Franklin"),
    };

    private static readonly (string Name, float Base)[] TickerItems =
    {
        ("ÖL", 82.4f), ("ERDGAS", 34.1f), ("KOHLE", 51.8f), ("EISEN", 104.6f),
        ("KUPFER", 812.0f), ("URAN", 148.2f), ("STAHL", 216.5f),
        ("ELEKTRONIK", 458.0f), ("MASCHINEN", 690.3f), ("KONSUMGÜTER", 129.7f),
        ("WAFFEN", 970.0f), ("MUNITION", 63.5f),
    };

    public void Enter()
    {
        _menuTime = 0f;
        _quoteTimer = 0f;
        _quoteIndex = _rng.Next(Quotes.Length);
        Array.Clear(_hoverAnim, 0, _hoverAnim.Length);
        _optionsAnim = 0f;
        Array.Clear(_optHover, 0, _optHover.Length);
        _dayNightToggleAnim = Program.ui.MainMenuDayNightCycleEnabled ? 1f : 0f;
        _mapClock = 17.0f + (float)_rng.NextDouble() * 4f; // Abendstimmung ueber Europa

        // Partikel initialisieren
        _dust = new Dust[46];
        for (int i = 0; i < _dust.Length; i++)
            _dust[i] = SpawnDust(randomY: true);
    }

    public void Exit() { }

    private Dust SpawnDust(bool randomY)
    {
        return new Dust
        {
            X = (float)_rng.NextDouble(),
            Y = randomY ? (float)_rng.NextDouble() : 1.05f,
            Speed = 0.012f + (float)_rng.NextDouble() * 0.028f,   // Bildschirmhoehen pro Sekunde
            SwayAmp = 0.002f + (float)_rng.NextDouble() * 0.006f,
            SwayFreq = 0.4f + (float)_rng.NextDouble() * 1.1f,
            Phase = (float)_rng.NextDouble() * MathF.Tau,
            Size = 1f + (float)_rng.NextDouble() * 2.2f,
            Alpha = (byte)(25 + _rng.Next(70)),
        };
    }

    public void Update()
    {
        float dt = Raylib.GetFrameTime();
        _menuTime += dt;
        _quoteTimer += dt;

        // Hintergrund lebt immer weiter (auch wenn Optionen offen sind):
        // sanfte Sinus-Kamerafahrt (bleibt automatisch in Grenzen) + wandernde Uhrzeit
        Program.worldMap.Move(new Vector2(MathF.Cos(_menuTime * 0.055f) * 9f * dt, 0));
        _mapClock = (_mapClock + dt * 0.10f) % 24f; // voller Tag in 4 Minuten
        Program.worldMap.DayNightCycleEnabled = Program.ui.MainMenuDayNightCycleEnabled;

        if (_quoteTimer >= 9f)
        {
            _quoteTimer = 0f;
            _quoteIndex = (_quoteIndex + 1) % Quotes.Length;
        }

        Vector2 mousePos = Program._cachedMousePos;

        // Options-Overlay weich ein-/ausblenden
        float optTarget = Program.ui.ShowMainMenuOptions ? 1f : 0f;
        _optionsAnim += (optTarget - _optionsAnim) * Math.Min(1f, dt * 11f);

        if (Program.ui.ShowMainMenuOptions)
        {
            UpdateMainMenuOptions(mousePos);
            return;
        }

        // Button-Spalte links positionieren
        int colX = GetColumnX();
        int firstY = GetFirstButtonY();
        Program.ui.NewGameButtonRect = new Rectangle(colX, firstY, ButtonWidth, ButtonHeight);
        Program.ui.LoadGameButtonRect = new Rectangle(colX, firstY + ButtonSpacing, ButtonWidth, ButtonHeight);
        Program.ui.OptionsButtonRect = new Rectangle(colX, firstY + ButtonSpacing * 2, ButtonWidth, ButtonHeight);
        Program.ui.QuitButtonRect = new Rectangle(colX, firstY + ButtonSpacing * 3, ButtonWidth, ButtonHeight);

        Program.ui.NewGameButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.NewGameButtonRect);
        Program.ui.LoadGameButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.LoadGameButtonRect);
        Program.ui.OptionsButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.OptionsButtonRect);
        Program.ui.QuitButtonHovered = Raylib.CheckCollisionPointRec(mousePos, Program.ui.QuitButtonRect);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Program.ui.NewGameButtonHovered)
            {
                SoundManager.Play(SoundEffect.Click);
                StartNewGame();
            }
            else if (Program.ui.LoadGameButtonHovered)
            {
                SoundManager.Play(SoundEffect.Click);
                Program.ui.SaveSlots = SaveGameManager.GetAllSlots();
                Program.ui.SelectedSaveSlot = -1;
                Program.currentScreen = GameScreen.LoadGame;
            }
            else if (Program.ui.OptionsButtonHovered)
            {
                SoundManager.Play(SoundEffect.Click);
                Program.ui.ShowMainMenuOptions = true;
                Program.ui.OptionsMusicVolume = Program.musicManager.Volume;
                Program.ui.OptionsSoundVolume = SoundManager.Volume;
            }
            else if (Program.ui.QuitButtonHovered)
            {
                SoundManager.Play(SoundEffect.Click);
                Program.shouldQuit = true;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            StartNewGame();
        }
    }

    private static void StartNewGame()
    {
        // Kamera zurueck auf Europa (die Menue-Kamerafahrt kann gedriftet sein)
        Program.worldMap.Zoom = 2.0f;
        Program.worldMap.CenterOnCountry("DEU", Program.ScreenWidth, Program.ScreenHeight);

        Program.game = new Game();
        Program.game.Initialize();
        if (Program.game.GameContext != null)
        {
            Program.game.GameContext.WorldMap = Program.worldMap;
        }
        Program.ui.SelectedCountryId = null;
        Program.currentScreen = GameScreen.CountrySelect;
    }

    private static int GetColumnX() => Math.Max(48, (int)(Program.ScreenWidth * 0.075f));
    private static int GetFirstButtonY() => Math.Max(280, (int)(Program.ScreenHeight * 0.42f));

    public void Draw()
    {
        float dt = Raylib.GetFrameTime();
        int w = Program.ScreenWidth;
        int h = Program.ScreenHeight;

        DrawLivingWorldBackground(w, h);
        DrawDustParticles(dt, w, h);

        int colX = GetColumnX();

        // === Titel + Untertitel ===
        int titleW = Program.MeasureTextCached("ECONOMIC EMPIRE", GameConfig.FONT_SIZE_TITLE);
        int titleY = Math.Max(70, (int)(h * 0.15f));
        Program.DrawGameTitle(colX + titleW / 2, titleY);

        string subtitle = "Baue dein Wirtschaftsimperium";
        Program.DrawGameText(subtitle, colX, titleY + GameConfig.FONT_SIZE_TITLE + 26, 22,
            new Color((byte)195, (byte)200, (byte)215, (byte)235));

        // === Buttons ===
        string[] labels = { "NEUES SPIEL", "SPIEL LADEN", "OPTIONEN", "BEENDEN" };
        Rectangle[] rects =
        {
            Program.ui.NewGameButtonRect, Program.ui.LoadGameButtonRect,
            Program.ui.OptionsButtonRect, Program.ui.QuitButtonRect,
        };
        bool[] hovered =
        {
            Program.ui.NewGameButtonHovered, Program.ui.LoadGameButtonHovered,
            Program.ui.OptionsButtonHovered, Program.ui.QuitButtonHovered,
        };

        for (int i = 0; i < 4; i++)
        {
            // Hover-Animation weich nachziehen
            float target = hovered[i] && !Program.ui.ShowMainMenuOptions ? 1f : 0f;
            _hoverAnim[i] += (target - _hoverAnim[i]) * Math.Min(1f, dt * 12f);

            // Gestaffelte Einblend-Animation (von links einschieben)
            float entry = Math.Clamp((_menuTime - 0.12f - i * 0.09f) / 0.38f, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - entry, 3f);

            DrawMainButton(rects[i], labels[i], i, _hoverAnim[i], ease);
        }

        // Tastatur-Hinweis unter den Buttons
        int hintY = GetFirstButtonY() + ButtonSpacing * 3 + ButtonHeight + 18;
        Program.DrawGameText("ENTER = Schnellstart", colX + 4, hintY, 14,
            new Color((byte)140, (byte)145, (byte)160, (byte)170));

        DrawQuote(w, h);
        DrawResourceTicker(w, h);

        // Version oben rechts
        string version = "v0.1";
        int verW = Program.MeasureTextCached(version, 16);
        Program.DrawGameText(version, w - verW - 14, 12, 16, new Color((byte)150, (byte)155, (byte)170, (byte)160));

        // Auch waehrend des Ausblendens weiterzeichnen
        if (Program.ui.ShowMainMenuOptions || _optionsAnim > 0.02f)
        {
            DrawMainMenuOptionsPanel();
        }
    }

    /// <summary>
    /// Lebender Hintergrund: echte Weltkarte + Tag/Nacht + kinoreife Verdunkelung
    /// </summary>
    private void DrawLivingWorldBackground(int w, int h)
    {
        Program.worldMap.Draw(null, null, null, null, null);

        if (Program.ui.MainMenuDayNightCycleEnabled)
        {
            int dayOfYear = DateTime.Now.DayOfYear;
            Program.worldMap.DrawDayNightOverlay(_mapClock, dayOfYear);
            Program.worldMap.DrawCityLights(_mapClock, dayOfYear);
        }

        // Grundverdunkelung, damit die Karte den Vordergrund nicht erschlaegt
        Raylib.DrawRectangle(0, 0, w, h, new Color((byte)4, (byte)6, (byte)12, (byte)80));

        // Dunkler Verlauf links als Buehne fuer Titel und Buttons
        Raylib.DrawRectangleGradientH(0, 0, (int)(w * 0.55f), h,
            new Color((byte)6, (byte)8, (byte)16, (byte)225),
            new Color((byte)6, (byte)8, (byte)16, (byte)0));

        // Sanfter Verlauf unten fuer Zitat und Ticker
        Raylib.DrawRectangleGradientV(0, h - 170, w, 170,
            new Color((byte)4, (byte)5, (byte)10, (byte)0),
            new Color((byte)4, (byte)5, (byte)10, (byte)230));

        // Dezente Vignette oben
        Raylib.DrawRectangleGradientV(0, 0, w, 90,
            new Color((byte)4, (byte)5, (byte)10, (byte)140),
            new Color((byte)4, (byte)5, (byte)10, (byte)0));
    }

    /// <summary>
    /// Aufsteigende Goldpartikel fuer Atmosphaere
    /// </summary>
    private void DrawDustParticles(float dt, int w, int h)
    {
        for (int i = 0; i < _dust.Length; i++)
        {
            ref var p = ref _dust[i];
            p.Y -= p.Speed * dt;
            if (p.Y < -0.05f)
                p = SpawnDust(randomY: false);

            float sway = MathF.Sin(_menuTime * p.SwayFreq + p.Phase) * p.SwayAmp;
            float px = (p.X + sway) * w;
            float py = p.Y * h;

            Raylib.DrawCircleV(new Vector2(px, py), p.Size,
                new Color((byte)255, (byte)210, (byte)130, p.Alpha));
        }
    }

    /// <summary>
    /// Neuer Menue-Button: dunkles Glas, goldener Akzentbalken, Icon, Hover-Chevron
    /// </summary>
    private static void DrawMainButton(Rectangle rect, string text, int iconIndex, float hover, float entry)
    {
        if (entry <= 0f) return;

        // Einschub von links + Einblenden
        float xOff = (1f - entry) * -60f;
        var r = new Rectangle(rect.X + xOff, rect.Y, rect.Width, rect.Height);
        float alpha = entry;

        byte A(float baseA) => (byte)Math.Clamp(baseA * alpha, 0, 255);

        // Schatten
        Raylib.DrawRectangle((int)r.X + 3, (int)r.Y + 4, (int)r.Width, (int)r.Height,
            new Color((byte)0, (byte)0, (byte)0, A(70)));

        // Hintergrund (dunkles Glas -> beim Hover heller und blauer)
        Color bg = LerpColor(new Color((byte)14, (byte)18, (byte)30, A(200)),
                             new Color((byte)36, (byte)46, (byte)72, A(240)), hover);
        Raylib.DrawRectangleRec(r, bg);

        // Feiner heller Streifen oben (Glas-Effekt)
        Raylib.DrawRectangle((int)r.X, (int)r.Y, (int)r.Width, 1,
            new Color((byte)255, (byte)255, (byte)255, A(18 + hover * 25)));

        // Rand
        Color border = LerpColor(new Color((byte)80, (byte)88, (byte)110, A(150)), ColorPalette.Accent, hover);
        border.A = A(150 + hover * 105);
        Raylib.DrawRectangleLinesEx(r, 1, border);

        // Goldener Akzentbalken links (waechst beim Hover)
        int barW = (int)(4 + hover * 5);
        Color gold = new Color((byte)230, (byte)185, (byte)80, A(190 + hover * 65));
        Raylib.DrawRectangle((int)r.X, (int)r.Y, barW, (int)r.Height, gold);
        if (hover > 0.05f)
        {
            // Weicher Glow neben dem Balken
            Raylib.DrawRectangleGradientH((int)r.X + barW, (int)r.Y, (int)(34 * hover), (int)r.Height,
                new Color((byte)230, (byte)185, (byte)80, A(45 * hover)),
                new Color((byte)230, (byte)185, (byte)80, (byte)0));
        }

        // Icon
        float iconCx = r.X + 34 + hover * 4;
        float iconCy = r.Y + r.Height / 2f;
        Color iconColor = LerpColor(new Color((byte)170, (byte)178, (byte)195, A(220)),
                                    new Color((byte)255, (byte)215, (byte)130, A(255)), hover);
        DrawButtonIcon(iconIndex, iconCx, iconCy, iconColor);

        // Text (schiebt beim Hover leicht nach rechts)
        Color textColor = LerpColor(new Color((byte)205, (byte)210, (byte)222, A(235)),
                                    new Color((byte)255, (byte)255, (byte)255, A(255)), hover);
        int textX = (int)(r.X + 62 + hover * 6);
        int textY = (int)(r.Y + (r.Height - GameConfig.FONT_SIZE_LARGE) / 2f);
        Program.DrawGameText(text, textX, textY, GameConfig.FONT_SIZE_LARGE, textColor);

        // Chevron rechts, erscheint beim Hover
        if (hover > 0.05f)
        {
            float cx = r.X + r.Width - 26 + hover * 5;
            float cy = r.Y + r.Height / 2f;
            Color chev = new Color((byte)255, (byte)215, (byte)130, A(230 * hover));
            Raylib.DrawLineEx(new Vector2(cx - 5, cy - 7), new Vector2(cx + 2, cy), 2.5f, chev);
            Raylib.DrawLineEx(new Vector2(cx + 2, cy), new Vector2(cx - 5, cy + 7), 2.5f, chev);
        }
    }

    /// <summary>
    /// Zeichnet die Button-Icons aus Primitiven (kein Icon-Font noetig)
    /// </summary>
    private static void DrawButtonIcon(int index, float cx, float cy, Color c)
    {
        var center = new Vector2(cx, cy);
        switch (index)
        {
            case 0: // Play-Dreieck
                Raylib.DrawPoly(center, 3, 10f, 0f, c);
                break;

            case 1: // Speicher-Diskette
                Raylib.DrawRectangleLinesEx(new Rectangle(cx - 9, cy - 9, 18, 18), 2, c);
                Raylib.DrawRectangle((int)cx - 4, (int)cy - 9, 8, 5, c);
                Raylib.DrawRectangle((int)cx - 5, (int)cy + 1, 10, 6, c);
                break;

            case 2: // Zahnrad
                Raylib.DrawRing(center, 4.5f, 7.5f, 0, 360, 24, c);
                for (int t = 0; t < 4; t++)
                {
                    Raylib.DrawRectanglePro(new Rectangle(cx, cy, 4, 21),
                        new Vector2(2, 10.5f), t * 45f, c);
                }
                Raylib.DrawRing(center, 4.5f, 7.5f, 0, 360, 24, c);
                break;

            case 3: // Power-Symbol
                Raylib.DrawRing(center, 6.5f, 9f, 30, 330, 24, c);
                Raylib.DrawRectangle((int)cx - 1, (int)cy - 12, 3, 9, c);
                break;
        }
    }

    /// <summary>
    /// Rotierendes Zitat ueber dem Ticker (mit Ein-/Ausblenden)
    /// </summary>
    private void DrawQuote(int w, int h)
    {
        var (text, author) = Quotes[_quoteIndex];
        float t = _quoteTimer;
        float fade = Math.Min(1f, Math.Min(t / 0.9f, (9f - t) / 0.9f));
        if (fade <= 0f) return;

        string quote = "\"" + text + "\"";
        int qW = Program.MeasureTextCached(quote, 17);
        int qX = (w - qW) / 2;
        int qY = h - 96;
        Program.DrawGameText(quote, qX, qY, 17,
            new Color((byte)200, (byte)205, (byte)218, (byte)(200 * fade)));

        string by = "- " + author;
        int byW = Program.MeasureTextCached(by, 14);
        Program.DrawGameText(by, (w - byW) / 2, qY + 24, 14,
            new Color((byte)230, (byte)185, (byte)80, (byte)(170 * fade)));
    }

    /// <summary>
    /// Laufender Ressourcen-Boersenticker am unteren Bildschirmrand
    /// </summary>
    private void DrawResourceTicker(int w, int h)
    {
        const int barH = 30;
        int barY = h - barH;

        Raylib.DrawRectangle(0, barY, w, barH, new Color((byte)8, (byte)10, (byte)16, (byte)215));
        Raylib.DrawRectangle(0, barY, w, 1, new Color((byte)230, (byte)185, (byte)80, (byte)120));

        const int fontSize = 15;
        const float speed = 46f; // Pixel pro Sekunde
        const string sep = "      ";

        // Gesamtbreite eines Durchlaufs bestimmen
        int totalW = 0;
        var segments = new (string Name, string Price, bool Up)[TickerItems.Length];
        for (int i = 0; i < TickerItems.Length; i++)
        {
            float phase = _menuTime * 0.32f + i * 1.71f;
            float price = TickerItems[i].Base * (1f + 0.045f * MathF.Sin(phase));
            bool up = MathF.Cos(phase) >= 0f;
            segments[i] = (TickerItems[i].Name, price.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), up);
            totalW += Program.MeasureTextCached(TickerItems[i].Name + " ", fontSize)
                    + Program.MeasureTextCached((up ? "+" : "-") + segments[i].Price, fontSize)
                    + Program.MeasureTextCached(sep, fontSize);
        }
        if (totalW <= 0) return;

        float scroll = (_menuTime * speed) % totalW;
        int x = (int)-scroll;
        int textY = barY + (barH - fontSize) / 2 + 1;

        // Zwei Durchlaeufe zeichnen, damit der Ticker nahtlos umbricht
        for (int pass = 0; pass < 2 && x < w; pass++)
        {
            for (int i = 0; i < segments.Length && x < w; i++)
            {
                var (name, priceStr, up) = segments[i];
                string nameStr = name + " ";
                int nameW = Program.MeasureTextCached(nameStr, fontSize);
                if (x + nameW > 0)
                    Program.DrawGameText(nameStr, x, textY, fontSize, new Color((byte)185, (byte)192, (byte)205, (byte)220));
                x += nameW;

                string valStr = (up ? "+" : "-") + priceStr;
                int valW = Program.MeasureTextCached(valStr, fontSize);
                Color valColor = up
                    ? new Color((byte)110, (byte)220, (byte)130, (byte)230)
                    : new Color((byte)235, (byte)110, (byte)100, (byte)230);
                if (x + valW > 0)
                    Program.DrawGameText(valStr, x, textY, fontSize, valColor);
                x += valW + Program.MeasureTextCached(sep, fontSize);
            }
        }
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }

    // === Options-Overlay im Living-World-Stil ===

    /// <summary>
    /// Gemeinsames Layout fuer Update und Draw (eine Quelle, keine Duplikate)
    /// </summary>
    private static (Rectangle Panel, Rectangle Close, Rectangle Back, Rectangle MusicTrack,
        Rectangle SoundTrack, Rectangle Toggle) GetOptionsLayout()
    {
        const int menuW = 520;
        const int menuH = 500;
        const int pad = 36;
        int x = (Program.ScreenWidth - menuW) / 2;
        int y = (Program.ScreenHeight - menuH) / 2;

        int soundHeaderY = y + 96;
        int musicTrackY = soundHeaderY + 64;
        int soundTrackY = musicTrackY + 64;
        int gfxHeaderY = soundTrackY + 44;
        int toggleY = gfxHeaderY + 34;

        return (
            new Rectangle(x, y, menuW, menuH),
            new Rectangle(x + menuW - 44, y + 12, 32, 32),
            new Rectangle(x + pad, y + menuH - 74, menuW - pad * 2, 50),
            new Rectangle(x + pad, musicTrackY, menuW - pad * 2, 10),
            new Rectangle(x + pad, soundTrackY, menuW - pad * 2, 10),
            new Rectangle(x + menuW - pad - 58, toggleY, 58, 28));
    }

    private static Rectangle Inflate(Rectangle r, float dx, float dy) =>
        new(r.X - dx, r.Y - dy, r.Width + dx * 2, r.Height + dy * 2);

    private void UpdateMainMenuOptions(Vector2 mousePos)
    {
        var l = GetOptionsLayout();

        void Close()
        {
            SoundManager.Play(SoundEffect.Click);
            Program.ui.ShowMainMenuOptions = false;
            Program.ui.IsDraggingMusicSlider = false;
            Program.ui.IsDraggingSoundSlider = false;
        }

        // Slider-Drag starten (grosszuegige Hit-Flaeche um die Schiene)
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, Inflate(l.MusicTrack, 10, 14)))
                Program.ui.IsDraggingMusicSlider = true;
            else if (Raylib.CheckCollisionPointRec(mousePos, Inflate(l.SoundTrack, 10, 14)))
                Program.ui.IsDraggingSoundSlider = true;
        }
        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            Program.ui.IsDraggingMusicSlider = false;
            Program.ui.IsDraggingSoundSlider = false;
        }

        if (Program.ui.IsDraggingMusicSlider)
        {
            Program.ui.OptionsMusicVolume = Math.Clamp((mousePos.X - l.MusicTrack.X) / l.MusicTrack.Width, 0f, 1f);
            Program.musicManager.Volume = Program.ui.OptionsMusicVolume;
        }
        if (Program.ui.IsDraggingSoundSlider)
        {
            Program.ui.OptionsSoundVolume = Math.Clamp((mousePos.X - l.SoundTrack.X) / l.SoundTrack.Width, 0f, 1f);
            SoundManager.Volume = Program.ui.OptionsSoundVolume;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) &&
            !Program.ui.IsDraggingMusicSlider && !Program.ui.IsDraggingSoundSlider)
        {
            if (Raylib.CheckCollisionPointRec(mousePos, l.Close) ||
                Raylib.CheckCollisionPointRec(mousePos, l.Back) ||
                !Raylib.CheckCollisionPointRec(mousePos, l.Panel))
            {
                // X, Zurueck oder Klick neben das Panel schliessen
                Close();
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, l.Toggle))
            {
                Program.ui.MainMenuDayNightCycleEnabled = !Program.ui.MainMenuDayNightCycleEnabled;
                SoundManager.Play(SoundEffect.Click);
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            Close();
    }

    private void DrawMainMenuOptionsPanel()
    {
        float dt = Raylib.GetFrameTime();
        Vector2 mousePos = Program._cachedMousePos;
        bool open = Program.ui.ShowMainMenuOptions;

        float ease = 1f - MathF.Pow(1f - Math.Clamp(_optionsAnim, 0f, 1f), 3f);
        if (ease <= 0.01f) return;
        byte A(float v) => (byte)Math.Clamp(v * ease, 0, 255);

        // Menue-Szene abdunkeln (Karte lebt dahinter weiter)
        Raylib.DrawRectangle(0, 0, Program.ScreenWidth, Program.ScreenHeight,
            new Color((byte)2, (byte)3, (byte)7, A(165)));

        var l = GetOptionsLayout();
        int oy = (int)((1f - ease) * 18f); // Panel gleitet beim Oeffnen leicht nach oben
        Rectangle Shift(Rectangle r) => new(r.X, r.Y + oy, r.Width, r.Height);

        var panel = Shift(l.Panel);
        var musicTrack = Shift(l.MusicTrack);
        var soundTrack = Shift(l.SoundTrack);
        var toggle = Shift(l.Toggle);
        var back = Shift(l.Back);
        var close = Shift(l.Close);

        int px = (int)panel.X, py = (int)panel.Y, pw = (int)panel.Width, ph = (int)panel.Height;
        const int pad = 36;
        int cx = px + pad;
        int cw = pw - pad * 2;
        Color gold = new Color((byte)230, (byte)185, (byte)80, A(255));

        // Schatten + dunkles Glas + Lichtkante + Rand (wie die Menue-Buttons)
        Raylib.DrawRectangle(px + 4, py + 6, pw, ph, new Color((byte)0, (byte)0, (byte)0, A(90)));
        Raylib.DrawRectangleRec(panel, new Color((byte)12, (byte)16, (byte)27, A(238)));
        Raylib.DrawRectangle(px, py + 3, pw, 1, new Color((byte)255, (byte)255, (byte)255, A(22)));
        Raylib.DrawRectangleLinesEx(panel, 1, new Color((byte)80, (byte)88, (byte)110, A(170)));

        // Goldene Kante oben
        Raylib.DrawRectangle(px, py, pw, 3, new Color((byte)230, (byte)185, (byte)80, A(210)));

        // === Titel mit Zahnrad ===
        const string title = "OPTIONEN";
        int titleW = Program.MeasureTextCached(title, 30);
        int titleX = px + (pw - titleW) / 2 + 14; // +14 macht Platz fuers Zahnrad
        int titleY = py + 24;
        DrawButtonIcon(2, titleX - 30, titleY + 16, gold);
        Program.DrawGameText(title, titleX, titleY, 30, new Color((byte)245, (byte)240, (byte)228, A(255)));

        // Trennlinie unterm Titel
        Raylib.DrawRectangle(cx, py + 72, cw, 1, new Color((byte)255, (byte)255, (byte)255, A(24)));

        // === Sound ===
        MenuStyle.DrawOptionsSection("SOUND", cx, (int)musicTrack.Y - 64, cw, ease);
        MenuStyle.DrawOptionSlider("Musik-Lautstärke", musicTrack, Program.ui.OptionsMusicVolume,
            Program.ui.IsDraggingMusicSlider || Raylib.CheckCollisionPointRec(mousePos, Inflate(musicTrack, 10, 14)), ease);
        MenuStyle.DrawOptionSlider("Sound-Lautstärke", soundTrack, Program.ui.OptionsSoundVolume,
            Program.ui.IsDraggingSoundSlider || Raylib.CheckCollisionPointRec(mousePos, Inflate(soundTrack, 10, 14)), ease);

        // === Grafik ===
        MenuStyle.DrawOptionsSection("GRAFIK", cx, (int)toggle.Y - 34, cw, ease);

        bool dayNightOn = Program.ui.MainMenuDayNightCycleEnabled;
        _dayNightToggleAnim += ((dayNightOn ? 1f : 0f) - _dayNightToggleAnim) * Math.Min(1f, dt * 14f);
        float t = _dayNightToggleAnim;

        Program.DrawGameText("Tag/Nacht-Zyklus auf der Karte", cx, (int)toggle.Y + 5, 17,
            new Color((byte)210, (byte)215, (byte)228, A(235)));

        string status = dayNightOn ? "AN" : "AUS";
        int statusW = Program.MeasureTextCached(status, 15);
        Program.DrawGameText(status, (int)toggle.X - statusW - 12, (int)toggle.Y + 6, 15,
            dayNightOn ? gold : new Color((byte)140, (byte)146, (byte)162, A(200)));

        // Schalter-Pille mit gleitendem Knopf
        MenuStyle.DrawTogglePill(toggle, t, ease);

        // === Zurueck-Button + Schliessen-Kreuz ===
        bool hoverBack = open && Raylib.CheckCollisionPointRec(mousePos, back);
        _optHover[0] += ((hoverBack ? 1f : 0f) - _optHover[0]) * Math.Min(1f, dt * 12f);
        MenuStyle.DrawBackButton(back, _optHover[0], ease);

        bool hoverClose = open && Raylib.CheckCollisionPointRec(mousePos, close);
        _optHover[1] += ((hoverClose ? 1f : 0f) - _optHover[1]) * Math.Min(1f, dt * 12f);
        MenuStyle.DrawCloseButton(close, _optHover[1], ease);

        // ESC-Hinweis unter dem Panel
        string hint = "ESC zum Schließen";
        int hintW = Program.MeasureTextCached(hint, 13);
        Program.DrawGameText(hint, px + (pw - hintW) / 2, py + ph + 10, 13,
            new Color((byte)150, (byte)155, (byte)170, A(150)));
    }

}
