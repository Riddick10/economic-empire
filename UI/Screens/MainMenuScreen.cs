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

    private static readonly (string Text, string Author)[] Quotes =
    {
        ("Der Preis ist, was du zahlst. Der Wert ist, was du bekommst.", "Warren Buffett"),
        ("Eine Investition in Wissen bringt noch immer die besten Zinsen.", "Benjamin Franklin"),
        ("Geld schlaeft nie.", "Gordon Gekko"),
        ("Jedes Imperium beginnt mit einer einzigen Fabrik.", "Economic Empire"),
        ("Wer den Handel kontrolliert, kontrolliert die Welt.", "Economic Empire"),
        ("Nicht Gold, sondern Vertrauen ist die Waehrung der Maechte.", "Economic Empire"),
        ("Das Risiko entsteht, wenn man nicht weiss, was man tut.", "Warren Buffett"),
        ("Handel hat noch keine Nation ruiniert.", "Benjamin Franklin"),
    };

    private static readonly (string Name, float Base)[] TickerItems =
    {
        ("OEL", 82.4f), ("ERDGAS", 34.1f), ("KOHLE", 51.8f), ("EISEN", 104.6f),
        ("KUPFER", 812.0f), ("URAN", 148.2f), ("NAHRUNG", 24.9f), ("STAHL", 216.5f),
        ("ELEKTRONIK", 458.0f), ("MASCHINEN", 690.3f), ("KONSUMGUETER", 129.7f),
        ("WAFFEN", 970.0f), ("MUNITION", 63.5f),
    };

    public void Enter()
    {
        _menuTime = 0f;
        _quoteTimer = 0f;
        _quoteIndex = _rng.Next(Quotes.Length);
        Array.Clear(_hoverAnim, 0, _hoverAnim.Length);
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

        if (Program.ui.ShowMainMenuOptions)
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

    private void UpdateMainMenuOptions(Vector2 mousePos)
    {
        int menuW = 480;
        int menuH = 460;
        int menuX = (Program.ScreenWidth - menuW) / 2;
        int menuY = (Program.ScreenHeight - menuH) / 2;

        int closeBtnSize = 30;
        int closeBtnX = menuX + menuW - closeBtnSize - 10;
        int closeBtnY = menuY + 10;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);

        int backBtnW = 360;
        int backBtnH = 40;
        int backBtnX = menuX + (menuW - backBtnW) / 2;
        int backBtnY = menuY + menuH - backBtnH - 20;
        Rectangle backRect = new Rectangle(backBtnX, backBtnY, backBtnW, backBtnH);

        int sliderX = menuX + 40;
        int sliderW = menuW - 80;
        int sliderH = 12;

        int soundSectionY = menuY + 70;

        int musicSliderY = soundSectionY + 68;
        Rectangle musicSliderRect = new Rectangle(sliderX, musicSliderY - 10, sliderW, sliderH + 20);

        int soundSliderY = soundSectionY + 120;
        Rectangle soundSliderRect = new Rectangle(sliderX, soundSliderY - 10, sliderW, sliderH + 20);

        int gfxSectionY = soundSectionY + 175;
        int toggleY = gfxSectionY + 42;
        int toggleW = 60;
        int toggleH = 26;
        int toggleX = menuX + menuW - 40 - toggleW;
        Rectangle toggleRect = new Rectangle(toggleX, toggleY - 2, toggleW, toggleH);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, musicSliderRect))
        {
            Program.ui.IsDraggingMusicSlider = true;
        }
        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            Program.ui.IsDraggingMusicSlider = false;
        }
        if (Program.ui.IsDraggingMusicSlider)
        {
            float newVolume = (mousePos.X - sliderX) / sliderW;
            Program.ui.OptionsMusicVolume = Math.Clamp(newVolume, 0f, 1f);
            Program.musicManager.Volume = Program.ui.OptionsMusicVolume;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, soundSliderRect))
        {
            Program.ui.IsDraggingSoundSlider = true;
        }
        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            Program.ui.IsDraggingSoundSlider = false;
        }
        if (Program.ui.IsDraggingSoundSlider)
        {
            float newVolume = (mousePos.X - sliderX) / sliderW;
            Program.ui.OptionsSoundVolume = Math.Clamp(newVolume, 0f, 1f);
            SoundManager.Volume = Program.ui.OptionsSoundVolume;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, closeRect) ||
                Raylib.CheckCollisionPointRec(mousePos, backRect))
            {
                Program.ui.ShowMainMenuOptions = false;
                Program.ui.IsDraggingMusicSlider = false;
                Program.ui.IsDraggingSoundSlider = false;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, toggleRect))
            {
                Program.ui.MainMenuDayNightCycleEnabled = !Program.ui.MainMenuDayNightCycleEnabled;
                SoundManager.Play(SoundEffect.Click);
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Program.ui.ShowMainMenuOptions = false;
            Program.ui.IsDraggingMusicSlider = false;
            Program.ui.IsDraggingSoundSlider = false;
        }
    }

    private void DrawMainMenuOptionsPanel()
    {
        Vector2 mousePos = Program._cachedMousePos;

        Raylib.DrawRectangle(0, 0, Program.ScreenWidth, Program.ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)150));

        int menuW = 480;
        int menuH = 460;
        int menuX = (Program.ScreenWidth - menuW) / 2;
        int menuY = (Program.ScreenHeight - menuH) / 2;

        Rectangle optionsRect = new(menuX, menuY, menuW, menuH);
        Rectangle optionsShadow = new(menuX + 3, menuY + 3, menuW, menuH);
        Raylib.DrawRectangleRounded(optionsShadow, 0.03f, 8, new Color((byte)0, (byte)0, (byte)0, (byte)60));
        Raylib.DrawRectangleRounded(optionsRect, 0.03f, 8, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(optionsRect, 0.03f, 8, 2, ColorPalette.Accent);

        string title = "OPTIONEN";
        int titleW = Program.MeasureTextCached(title, 32);
        Program.DrawGameText(title, menuX + (menuW - titleW) / 2, menuY + 20, 26, ColorPalette.Accent);

        int closeBtnSize = 30;
        int closeBtnX = menuX + menuW - closeBtnSize - 10;
        int closeBtnY = menuY + 10;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);
        bool hoverClose = Raylib.CheckCollisionPointRec(mousePos, closeRect);

        Raylib.DrawRectangleRec(closeRect, hoverClose ? ColorPalette.Red : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(closeRect, 1, hoverClose ? ColorPalette.Red : ColorPalette.TextGray);
        int xTextW = Program.MeasureTextCached("X", 20);
        Program.DrawGameText("X", closeBtnX + (closeBtnSize - xTextW) / 2, closeBtnY + 5, 11, ColorPalette.TextWhite);

        int contentX = menuX + 30;
        int contentW = menuW - 60;
        int sliderX = menuX + 40;
        int sliderW = menuW - 80;
        int sliderH = 12;
        int knobSize = 20;

        int soundSectionY = menuY + 70;

        Raylib.DrawRectangle(contentX, soundSectionY, contentW, 28, new Color((byte)30, (byte)35, (byte)50, (byte)255));
        Raylib.DrawRectangleLinesEx(new Rectangle(contentX, soundSectionY, contentW, 28), 1, ColorPalette.Accent);
        Program.DrawGameText("Sound-Einstellungen", contentX + 10, soundSectionY + 5, 18, ColorPalette.Accent);

        int musicLabelY = soundSectionY + 40;
        Program.DrawGameText("Musik-Lautstaerke", sliderX, musicLabelY, 18, ColorPalette.TextWhite);

        string musicPercent = $"{(int)(Program.ui.OptionsMusicVolume * 100)}%";
        int musicPercentW = Program.MeasureTextCached(musicPercent, 18);
        Program.DrawGameText(musicPercent, menuX + menuW - 40 - musicPercentW, musicLabelY, 18, ColorPalette.Accent);

        int musicSliderY = soundSectionY + 68;

        Raylib.DrawRectangle(sliderX, musicSliderY, sliderW, sliderH, ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(new Rectangle(sliderX, musicSliderY, sliderW, sliderH), 1, ColorPalette.PanelLight);

        int musicFillW = (int)(sliderW * Program.ui.OptionsMusicVolume);
        if (musicFillW > 0)
            Raylib.DrawRectangle(sliderX, musicSliderY, musicFillW, sliderH, ColorPalette.Accent);

        int musicKnobX = sliderX + musicFillW - knobSize / 2;
        int musicKnobY = musicSliderY + sliderH / 2 - knobSize / 2;
        Rectangle musicKnobRect = new Rectangle(musicKnobX, musicKnobY, knobSize, knobSize);
        bool hoverMusicKnob = Raylib.CheckCollisionPointRec(mousePos, musicKnobRect) || Program.ui.IsDraggingMusicSlider;
        Raylib.DrawRectangleRec(musicKnobRect, hoverMusicKnob ? ColorPalette.Accent : ColorPalette.TextWhite);
        Raylib.DrawRectangleLinesEx(musicKnobRect, 1, ColorPalette.Accent);

        int soundLabelY = soundSectionY + 92;
        Program.DrawGameText("Sound-Lautstaerke", sliderX, soundLabelY, 18, ColorPalette.TextWhite);

        string soundPercent = $"{(int)(Program.ui.OptionsSoundVolume * 100)}%";
        int soundPercentW = Program.MeasureTextCached(soundPercent, 18);
        Program.DrawGameText(soundPercent, menuX + menuW - 40 - soundPercentW, soundLabelY, 18, ColorPalette.Accent);

        int soundSliderY = soundSectionY + 120;

        Raylib.DrawRectangle(sliderX, soundSliderY, sliderW, sliderH, ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(new Rectangle(sliderX, soundSliderY, sliderW, sliderH), 1, ColorPalette.PanelLight);

        int soundFillW = (int)(sliderW * Program.ui.OptionsSoundVolume);
        if (soundFillW > 0)
            Raylib.DrawRectangle(sliderX, soundSliderY, soundFillW, sliderH, ColorPalette.Accent);

        int soundKnobX = sliderX + soundFillW - knobSize / 2;
        int soundKnobY = soundSliderY + sliderH / 2 - knobSize / 2;
        Rectangle soundKnobRect = new Rectangle(soundKnobX, soundKnobY, knobSize, knobSize);
        bool hoverSoundKnob = Raylib.CheckCollisionPointRec(mousePos, soundKnobRect) || Program.ui.IsDraggingSoundSlider;
        Raylib.DrawRectangleRec(soundKnobRect, hoverSoundKnob ? ColorPalette.Accent : ColorPalette.TextWhite);
        Raylib.DrawRectangleLinesEx(soundKnobRect, 1, ColorPalette.Accent);

        int gfxSectionY = soundSectionY + 175;

        Raylib.DrawRectangle(contentX, gfxSectionY, contentW, 28, new Color((byte)30, (byte)35, (byte)50, (byte)255));
        Raylib.DrawRectangleLinesEx(new Rectangle(contentX, gfxSectionY, contentW, 28), 1, ColorPalette.Accent);
        Program.DrawGameText("Grafik-Einstellungen", contentX + 10, gfxSectionY + 5, 18, ColorPalette.Accent);

        int toggleY = gfxSectionY + 42;
        Program.DrawGameText("Tag/Nacht-Zyklus", sliderX, toggleY, 18, ColorPalette.TextWhite);

        int toggleW = 60;
        int toggleH = 26;
        int toggleX = menuX + menuW - 40 - toggleW;
        Rectangle toggleRect = new Rectangle(toggleX, toggleY - 2, toggleW, toggleH);
        bool hoverToggle = Raylib.CheckCollisionPointRec(mousePos, toggleRect);
        bool dayNightOn = Program.ui.MainMenuDayNightCycleEnabled;

        Color toggleBg = dayNightOn ? ColorPalette.Accent : ColorPalette.Background;
        Raylib.DrawRectangleRec(toggleRect, toggleBg);
        Raylib.DrawRectangleLinesEx(toggleRect, 1, dayNightOn ? ColorPalette.Accent : ColorPalette.PanelLight);

        int knobW = 26;
        int knobX = dayNightOn ? toggleX + toggleW - knobW : toggleX;
        Raylib.DrawRectangle(knobX, (int)toggleRect.Y, knobW, toggleH, hoverToggle ? ColorPalette.TextWhite : ColorPalette.PanelLight);
        Raylib.DrawRectangleLinesEx(new Rectangle(knobX, toggleRect.Y, knobW, toggleH), 1, ColorPalette.TextWhite);

        string toggleStatus = dayNightOn ? "AN" : "AUS";
        Color toggleStatusColor = dayNightOn ? new Color((byte)100, (byte)255, (byte)100, (byte)255) : ColorPalette.TextGray;
        int statusW = Program.MeasureTextCached(toggleStatus, 16);
        Program.DrawGameText(toggleStatus, toggleX - statusW - 10, toggleY + 1, 16, toggleStatusColor);

        int backBtnW = 360;
        int backBtnH = 40;
        int backBtnX = menuX + (menuW - backBtnW) / 2;
        int backBtnY = menuY + menuH - backBtnH - 20;
        Rectangle backRect = new Rectangle(backBtnX, backBtnY, backBtnW, backBtnH);
        bool hoverBack = Raylib.CheckCollisionPointRec(mousePos, backRect);
        Program.DrawMenuButton(backRect, "Zurueck", hoverBack);
    }
}
