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
/// Program - Spieler-UI: Musikplayer, Berater-Leiste, Tutorial, Wirtschaftsranking
/// </summary>
partial class Program
{
    /// <summary>
    /// Zeichnet den Mini-Musikplayer oben rechts unter der Top-Bar
    /// </summary>
    static void DrawMiniMusicPlayer()
    {
        Vector2 mousePos = _cachedMousePos;

        // Position und Groesse
        int playerW = 220;
        int playerH = ui.ShowMusicTrackList ? 280 : 50;
        int playerX = ScreenWidth - playerW - 10;
        int playerY = 75;

        // Hintergrund (abgerundet)
        Rectangle musicRect = new(playerX, playerY, playerW, playerH);
        Raylib.DrawRectangleRounded(musicRect, 0.06f, 6, ColorPalette.PanelTransparent());
        Raylib.DrawRectangleRoundedLinesEx(musicRect, 0.06f, 6, 1, ColorPalette.PanelLight);

        // === STEUERUNGSLEISTE ===
        int btnSize = 28;
        int btnSpacing = 6;
        int btnY = playerY + 10;
        int btnStartX = playerX + 10;

        // Previous Button (<<)
        Rectangle prevBtn = new Rectangle(btnStartX, btnY, btnSize, btnSize);
        bool hoverPrev = Raylib.CheckCollisionPointRec(mousePos, prevBtn);
        Raylib.DrawRectangleRec(prevBtn, hoverPrev ? ColorPalette.PanelLight : ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(prevBtn, 1, ColorPalette.Accent);
        DrawPrevIcon(btnStartX + 6, btnY + btnSize / 2, btnSize - 12, ColorPalette.TextWhite);
        if (hoverPrev && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            musicManager.Previous();
            SoundManager.Play(SoundEffect.Click);
        }

        // Play/Pause Button
        int playBtnX = btnStartX + btnSize + btnSpacing;
        Rectangle playBtn = new Rectangle(playBtnX, btnY, btnSize, btnSize);
        bool hoverPlay = Raylib.CheckCollisionPointRec(mousePos, playBtn);
        Raylib.DrawRectangleRec(playBtn, hoverPlay ? ColorPalette.PanelLight : ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(playBtn, 1, ColorPalette.Accent);

        if (musicManager.IsPaused)
        {
            // Play-Icon (Dreieck)
            DrawPlayIcon(playBtnX + 8, btnY + btnSize / 2, btnSize - 14, ColorPalette.Green);
        }
        else
        {
            // Pause-Icon (zwei Balken)
            DrawPauseIcon(playBtnX + 8, btnY + btnSize / 2, btnSize - 14, ColorPalette.TextWhite);
        }

        if (hoverPlay && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            musicManager.TogglePause();
            SoundManager.Play(SoundEffect.Click);
        }

        // Next Button (>>)
        int nextBtnX = playBtnX + btnSize + btnSpacing;
        Rectangle nextBtn = new Rectangle(nextBtnX, btnY, btnSize, btnSize);
        bool hoverNext = Raylib.CheckCollisionPointRec(mousePos, nextBtn);
        Raylib.DrawRectangleRec(nextBtn, hoverNext ? ColorPalette.PanelLight : ColorPalette.Background);
        Raylib.DrawRectangleLinesEx(nextBtn, 1, ColorPalette.Accent);
        DrawNextIcon(nextBtnX + 6, btnY + btnSize / 2, btnSize - 12, ColorPalette.TextWhite);
        if (hoverNext && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            musicManager.Skip();
            SoundManager.Play(SoundEffect.Click);
        }

        // Track-Titel (rechts neben Buttons, klickbar fuer Track-Liste)
        int titleX = nextBtnX + btnSize + btnSpacing + 5;
        int titleW = playerW - (titleX - playerX) - 10;
        Rectangle titleRect = new Rectangle(titleX, btnY, titleW, btnSize);
        bool hoverTitle = Raylib.CheckCollisionPointRec(mousePos, titleRect);

        string trackName = musicManager.CurrentTrackName ?? "Keine Musik";
        // Kuerzen wenn zu lang
        while (MeasureTextCached(trackName, 12) > titleW - 10 && trackName.Length > 3)
        {
            trackName = trackName.Substring(0, trackName.Length - 4) + "...";
        }

        Raylib.DrawRectangleRec(titleRect, hoverTitle ? ColorPalette.PanelLight : ColorPalette.Background);
        DrawGameText(trackName, titleX + 5, btnY + 8, 11, ColorPalette.TextWhite);

        // Kleines Icon fuer "Liste oeffnen"
        string listIcon = ui.ShowMusicTrackList ? "^" : "v";
        DrawGameText(listIcon, titleX + titleW - 12, btnY + 8, 11, ColorPalette.TextGray);

        if (hoverTitle && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.ShowMusicTrackList = !ui.ShowMusicTrackList;
            ui.MusicTrackListScroll = 0;
            SoundManager.Play(SoundEffect.Click);
        }

        // === TRACK-LISTE (wenn ausgeklappt) ===
        if (ui.ShowMusicTrackList)
        {
            int listY = playerY + 50;
            int listH = playerH - 55;
            int listX = playerX + 5;
            int listW = playerW - 10;

            // Trennlinie
            Raylib.DrawLine(playerX + 5, listY - 5, playerX + playerW - 5, listY - 5, ColorPalette.PanelLight);

            // Track-Liste mit Scrolling
            var tracks = musicManager.GetAllTrackNames();
            int trackH = 24;
            int visibleTracks = listH / trackH;
            int maxScroll = Math.Max(0, tracks.Count - visibleTracks);

            // Scroll-Eingabe (nur wenn Maus ueber Liste)
            Rectangle listRect = new Rectangle(listX, listY, listW, listH);
            if (Raylib.CheckCollisionPointRec(mousePos, listRect))
            {
                int wheel = (int)Raylib.GetMouseWheelMove();
                ui.MusicTrackListScroll = Math.Clamp(ui.MusicTrackListScroll - wheel, 0, maxScroll);
            }

            // Scissor fuer Clipping
            Raylib.BeginScissorMode(listX, listY, listW, listH);

            int currentIndex = musicManager.CurrentTrackIndex;

            for (int i = 0; i < tracks.Count; i++)
            {
                int trackY = listY + (i - ui.MusicTrackListScroll) * trackH;
                if (trackY < listY - trackH || trackY > listY + listH) continue;

                Rectangle trackRect = new Rectangle(listX, trackY, listW, trackH - 2);
                bool hoverTrack = Raylib.CheckCollisionPointRec(mousePos, trackRect);
                bool isCurrentTrack = (i == currentIndex);

                // Hintergrund
                if (isCurrentTrack)
                {
                    Raylib.DrawRectangleRec(trackRect, ColorPalette.Accent);
                }
                else if (hoverTrack)
                {
                    Raylib.DrawRectangleRec(trackRect, ColorPalette.PanelLight);
                }

                // Track-Name
                string name = tracks[i];
                while (MeasureTextCached(name, 12) > listW - 30 && name.Length > 3)
                {
                    name = name.Substring(0, name.Length - 4) + "...";
                }

                Color textColor = isCurrentTrack ? ColorPalette.TextWhite : (hoverTrack ? ColorPalette.TextWhite : ColorPalette.TextGray);
                DrawGameText($"{i + 1}.", listX + 5, trackY + 5, 11, textColor);
                DrawGameText(name, listX + 25, trackY + 5, 11, textColor);

                // Klick zum Abspielen
                if (hoverTrack && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    musicManager.PlayTrackByIndex(i);
                    SoundManager.Play(SoundEffect.Click);
                }
            }

            Raylib.EndScissorMode();

            // Scrollbar wenn noetig
            if (tracks.Count > visibleTracks)
            {
                int scrollbarX = playerX + playerW - 8;
                int scrollbarH = listH;
                int thumbH = Math.Max(20, scrollbarH * visibleTracks / tracks.Count);
                int thumbY = listY + (scrollbarH - thumbH) * ui.MusicTrackListScroll / maxScroll;

                Raylib.DrawRectangle(scrollbarX, listY, 4, scrollbarH, ColorPalette.Background);
                Raylib.DrawRectangle(scrollbarX, thumbY, 4, thumbH, ColorPalette.Accent);
            }
        }
    }

    /// <summary>
    /// Zeichnet ein Previous-Icon (<<)
    /// </summary>
    static void DrawPrevIcon(int x, int centerY, int size, Color color)
    {
        int h = size / 2;

        // Erster Pfeil
        Raylib.DrawLine(x + size / 2, centerY - h, x, centerY, color);
        Raylib.DrawLine(x, centerY, x + size / 2, centerY + h, color);

        // Zweiter Pfeil
        Raylib.DrawLine(x + size, centerY - h, x + size / 2, centerY, color);
        Raylib.DrawLine(x + size / 2, centerY, x + size, centerY + h, color);

        // Strich links
        Raylib.DrawLine(x, centerY - h, x, centerY + h, color);
    }

    /// <summary>
    /// Zeichnet ein Next-Icon (>>)
    /// </summary>
    static void DrawNextIcon(int x, int centerY, int size, Color color)
    {
        int h = size / 2;

        // Erster Pfeil
        Raylib.DrawLine(x, centerY - h, x + size / 2, centerY, color);
        Raylib.DrawLine(x + size / 2, centerY, x, centerY + h, color);

        // Zweiter Pfeil
        Raylib.DrawLine(x + size / 2, centerY - h, x + size, centerY, color);
        Raylib.DrawLine(x + size, centerY, x + size / 2, centerY + h, color);

        // Strich rechts
        Raylib.DrawLine(x + size, centerY - h, x + size, centerY + h, color);
    }

    /// <summary>
    /// Zeichnet ein Play-Icon (Dreieck nach rechts)
    /// </summary>
    static void DrawPlayIcon(int x, int centerY, int size, Color color)
    {
        int h = size / 2;
        Vector2 p1 = new Vector2(x, centerY - h);
        Vector2 p2 = new Vector2(x + size, centerY);
        Vector2 p3 = new Vector2(x, centerY + h);
        Raylib.DrawTriangle(p1, p3, p2, color);
    }

    /// <summary>
    /// Zeichnet ein Pause-Icon (zwei vertikale Balken)
    /// </summary>
    static void DrawPauseIcon(int x, int centerY, int size, Color color)
    {
        int h = size / 2;
        int barW = size / 3;
        int gap = size / 5;

        Raylib.DrawRectangle(x, centerY - h, barW, size, color);
        Raylib.DrawRectangle(x + barW + gap, centerY - h, barW, size, color);
    }

    /// <summary>
    /// Zeichnet das Tutorial-Panel mit scrollbarem Inhalt
    /// </summary>
    static void DrawTutorialPanel()
    {
        Vector2 mousePos = _cachedMousePos;

        // Panel-Dimensionen
        int panelW = 880;
        int panelH = 600;
        int panelX = (ScreenWidth - panelW) / 2;
        int panelY = (ScreenHeight - panelH) / 2;

        // Hintergrund abdunkeln
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)150));

        // Panel-Hintergrund
        Raylib.DrawRectangleRounded(new Rectangle(panelX, panelY, panelW, panelH), 0.02f, 8, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(panelX, panelY, panelW, panelH), 0.02f, 8, 2, ColorPalette.Accent);

        // Header
        int headerH = 50;
        Raylib.DrawRectangle(panelX, panelY, panelW, headerH, ColorPalette.PanelLight);
        DrawGameText("Spielanleitung", panelX + 20, panelY + 14, 11, ColorPalette.Accent);

        // Schliessen-Button (X)
        int closeBtnSize = 30;
        int closeBtnX = panelX + panelW - closeBtnSize - 10;
        int closeBtnY = panelY + 10;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);
        bool hoverClose = Raylib.CheckCollisionPointRec(mousePos, closeRect);

        Raylib.DrawRectangleRec(closeRect, hoverClose ? ColorPalette.Red : ColorPalette.Panel);
        DrawGameText("X", closeBtnX + 9, closeBtnY + 5, 11, ColorPalette.TextWhite);

        if (hoverClose && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.ShowTutorialPanel = false;
            SoundManager.Play(SoundEffect.Click);
        }

        // Content-Bereich mit Scrolling
        int contentX = panelX + 20;
        int contentY = panelY + headerH + 10;
        int contentW = panelW - 40;
        int contentH = panelH - headerH - 20;

        // Scissor-Mode fuer Clipping
        Raylib.BeginScissorMode(contentX, contentY, contentW, contentH);

        // Tutorial-Inhalt zeichnen
        int yOffset = contentY - ui.TutorialScrollOffset;
        int lineSpacing = 24;
        int sectionSpacing = 40;
        int iconSize = 48;

        // === WILLKOMMEN ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Willkommen bei Economic Empire!",
            "tutorial.png", new string[]
        {
            "In diesem Strategiespiel uebernimmst du die Kontrolle ueber ein Land.",
            "Dein Ziel ist es, deine Nation zu wirtschaftlicher und politischer",
            "Staerke zu fuehren. Verwalte Ressourcen, baue Industrie auf,",
            "schliesse Handelsabkommen und navigiere durch die Weltpolitik."
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        // === RESSOURCEN ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Ressourcen",
            "tutorial_resources.png", new string[]
        {
            "Es gibt verschiedene Ressourcentypen:",
            "",
            "ROHSTOFFE (werden in Minen abgebaut):",
            "  - Erdoel: Wichtig fuer Transport und Energie",
            "  - Erdgas: Heizung und Stromerzeugung",
            "  - Kohle: Strom und Stahlproduktion",
            "  - Eisenerz: Grundlage fuer Stahlproduktion",
            "  - Kupfer: Wird fuer Elektronik benoetigt",
            "  - Uran: Kernenergie und strategische Bedeutung",
            "  - Nahrung: Grundversorgung der Bevoelkerung",
            "",
            "VERARBEITETE GUETER (in Fabriken produziert):",
            "  - Stahl: Aus Eisen + Kohle (fuer Maschinen/Militaer)",
            "  - Elektronik: Aus Kupfer + Kohle (Hightech-Produkte)",
            "  - Maschinen: Aus Stahl + Elektronik (Industrie)",
            "  - Konsumgueter: Fuer die Bevoelkerungszufriedenheit",
            "",
            "MILITAERGUETER (in Militaerfabriken produziert):",
            "  - Waffen: Aus Stahl + Elektronik",
            "  - Munition: Aus Stahl + Kupfer",
            "",
            "ZAHLENFORMAT: Mrd=Milliarden, Mio=Millionen, Tsd=Tausend"
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        // === PRODUKTION ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Produktion & Industrie",
            "tutorial_production.png", new string[]
        {
            "Deine Wirtschaft basiert auf zwei Produktionsarten:",
            "",
            "MINEN (Rohstoffgewinnung):",
            "  - Muessen in Provinzen mit passenden Ressourcen gebaut werden",
            "  - Jede Mine produziert taeglich eine bestimmte Menge",
            "  - Minen koennen aufgewertet werden fuer mehr Produktion",
            "",
            "FABRIKEN (Weiterverarbeitung):",
            "  - Zivile Fabriken: Produzieren Konsumgueter, Stahl, etc.",
            "  - Militaerfabriken: Produzieren Waffen und Munition",
            "  - Fabriken sind auf Provinzen verteilt",
            "",
            "Produktionsketten:",
            "  - Stahl = 0.75 Eisen + 0.4 Kohle",
            "  - Elektronik = 0.4 Kupfer + 0.15 Kohle",
            "  - Maschinen = 0.5 Stahl + 0.25 Elektronik",
            "  - Konsumgueter = 0.15 Nahrung + 0.1 Stahl + 0.1 Elektronik",
            "  - Waffen = 1.0 Stahl + 0.5 Elektronik",
            "  - Munition = 0.5 Stahl + 0.3 Kupfer"
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        // === HANDEL ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Handel & Weltmarkt",
            "tutorial_trade.png", new string[]
        {
            "Der Weltmarkt verbindet alle Laender:",
            "",
            "MARKTPREISE:",
            "  - Preise aendern sich basierend auf Angebot/Nachfrage",
            "  - Rohstoffe sind volatiler als verarbeitete Gueter",
            "  - Preistrends werden mit Pfeilen angezeigt",
            "",
            "HANDELSABKOMMEN:",
            "  - Import: Kaufe Ressourcen die du brauchst",
            "  - Export: Verkaufe Ueberschuesse fuer Geld",
            "  - Regionale Gruppen (EU, NAFTA etc.) handeln effizienter",
            "",
            "EMBARGOS:",
            "  - Laender koennen Handel verweigern",
            "  - Diplomatische Beziehungen beeinflussen Handel"
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        // === BEVOELKERUNG ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Bevoelkerung & Nahrung",
            "tutorial_population.png", new string[]
        {
            "Deine Bevoelkerung hat taegliche Beduerfnisse:",
            "",
            "VERBRAUCH PRO MILLION EINWOHNER/TAG:",
            "  - Nahrung: 1.0 Einheiten",
            "  - Konsumgueter: 0.3 Einheiten",
            "  - Oel: 0.1 Einheiten (Transport/Heizung)",
            "  - Gas: 0.08 Einheiten (Heizung/Strom)",
            "  - Kohle: 0.05 Einheiten (Strom)",
            "",
            "HUNGERSNOETE:",
            "  - Ohne Nahrung beginnt nach 7 Tagen das Sterben",
            "  - Die Sterberate steigt mit der Dauer",
            "  - Jedes Land startet mit 1 Jahr Nahrungsvorrat",
            "",
            "Warnung: Achte auf deine Nahrungsreserven!"
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        // === WIRTSCHAFT ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Wirtschaft & Finanzen",
            "tutorial_economy.png", new string[]
        {
            "Die Wirtschaft deines Landes:",
            "",
            "BUDGET:",
            "  - Einnahmen durch Steuern und Exporte",
            "  - Ausgaben fuer Importe, Militaer, Bauprojekte",
            "  - Halte dein Budget positiv!",
            "",
            "BIP (Bruttoinlandsprodukt):",
            "  - Misst die Wirtschaftskraft deines Landes",
            "  - Waechst durch Industrie und Handel",
            "",
            "Tipp: Exportiere Ueberschuesse fuer schnelles Geld!"
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        // === DIPLOMATIE ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Diplomatie",
            "tutorial_diplomacy.png", new string[]
        {
            "Beziehungen zu anderen Laendern:",
            "",
            "DIPLOMATISCHE AKTIONEN:",
            "  - Handelsabkommen verbessern Wirtschaft",
            "  - Buendnisse schuetzen vor Angriffen",
            "  - Garantien schuetzen andere Laender",
            "",
            "BEZIEHUNGEN:",
            "  - -100 bis +100 (Feind bis Verbuendeter)",
            "  - Handlungen beeinflussen die Beziehung",
            "  - Ideologie spielt eine Rolle"
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        // === MILITAER ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Militaer & Krieg",
            "tutorial_military.png", new string[]
        {
            "Deine Streitkraefte:",
            "",
            "EINHEITEN (5 Typen):",
            "  - Infanterie, Panzer, Artillerie",
            "  - Mechanisiert, Fallschirmjaeger",
            "  - Rekrutierung in Provinzen",
            "  - Bewegungszeit haengt von der Entfernung ab",
            "",
            "KRIEGSFUEHRUNG:",
            "  - Vor einer Eroberung: KRIEG ERKLAEREN!",
            "  - Im Politik-Panel des Ziellandes klicken",
            "  - Erst dann koennen Provinzen erobert werden",
            "",
            "KONFLIKTE:",
            "  - Kriege erfordern Ressourcen und Ausruestung",
            "  - Verluste an Bevoelkerung und Material",
            "  - Diplomatie ist oft die bessere Wahl!",
            "",
            "Tipp: Eine starke Wirtschaft ist die Basis fuer Militaer."
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        // === STEUERUNG ===
        yOffset = DrawTutorialSection(contentX, yOffset, contentW, "Steuerung",
            "tutorial.png", new string[]
        {
            "Wichtige Tastenkuerzel:",
            "",
            "SPIELSTEUERUNG:",
            "  [LEERTASTE] - Spiel pausieren/fortsetzen",
            "  [+] / [-] - Geschwindigkeit erhoehen/senken",
            "  [ESC] - Pause-Menue oeffnen",
            "  [F5] - Speicher-Panel oeffnen",
            "  [F9] - Schnellspeichern (Slot 1)",
            "",
            "PANELS (1-8):",
            "  [1] Politik  [2] Handel  [3] Produktion",
            "  [4] Logistik  [5] Forschung  [6] Diplomatie",
            "  [7] Budget  [8] Militaer",
            "",
            "KARTE:",
            "  [W][A][S][D] - Karte verschieben",
            "  [Mausrad] - Karte zoomen",
            "  [Mittlere Maustaste] - Karte ziehen",
            "  [Linksklick] - Provinz auswaehlen",
            "  [Rechtsklick] - Land-Infos anzeigen",
            "",
            "Viel Erfolg bei der Fuehrung deines Landes!"
        }, iconSize, lineSpacing);
        yOffset += sectionSpacing;

        Raylib.EndScissorMode();

        // Berechne Gesamthoehe des Inhalts
        int totalContentH = yOffset - (contentY - ui.TutorialScrollOffset) + 50;
        int maxScroll = Math.Max(0, totalContentH - contentH);

        // Scrollbar zeichnen (nur wenn noetig)
        if (maxScroll > 0)
        {
            int scrollbarW = 8;
            int scrollbarX = panelX + panelW - scrollbarW - 5;
            int scrollbarY = contentY;
            int scrollbarH = contentH;

            // Scrollbar-Hintergrund
            Raylib.DrawRectangle(scrollbarX, scrollbarY, scrollbarW, scrollbarH, ColorPalette.PanelLight);

            // Scrollbar-Thumb
            float thumbRatio = (float)contentH / totalContentH;
            int thumbH = Math.Max(30, (int)(scrollbarH * thumbRatio));
            float scrollRatio = (float)ui.TutorialScrollOffset / maxScroll;
            int thumbY = scrollbarY + (int)((scrollbarH - thumbH) * scrollRatio);

            Raylib.DrawRectangleRounded(new Rectangle(scrollbarX, thumbY, scrollbarW, thumbH), 0.5f, 4, ColorPalette.Accent);
        }

        // Mausrad-Scrolling (nur wenn Maus ueber Panel)
        Rectangle panelRect = new Rectangle(panelX, panelY, panelW, panelH);
        if (Raylib.CheckCollisionPointRec(mousePos, panelRect))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                ui.TutorialScrollOffset -= (int)(wheel * 40);
                ui.TutorialScrollOffset = Math.Clamp(ui.TutorialScrollOffset, 0, maxScroll);
            }
        }

        // ESC schliesst das Panel
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            ui.ShowTutorialPanel = false;
            SoundManager.Play(SoundEffect.Click);
        }
    }

    static void DrawEconomyRankingPanel()
    {
        Vector2 mousePos = _cachedMousePos;

        // Panel-Dimensionen
        int panelW = 920;
        int panelH = 620;
        int panelX = (ScreenWidth - panelW) / 2;
        int panelY = (ScreenHeight - panelH) / 2;

        // Hintergrund abdunkeln
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)150));

        // Panel-Hintergrund
        Raylib.DrawRectangleRounded(new Rectangle(panelX, panelY, panelW, panelH), 0.02f, 8, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(panelX, panelY, panelW, panelH), 0.02f, 8, 2, ColorPalette.Accent);

        // Header
        int headerH = 50;
        Raylib.DrawRectangle(panelX, panelY, panelW, headerH, ColorPalette.PanelLight);

        // Tab-Buttons im Header
        int tabW = 200;
        int tabH = 30;
        int tabY = panelY + 10;
        int tabSpacing = 8;
        int tab0X = panelX + 15;
        int tab1X = tab0X + tabW + tabSpacing;

        // Tab 0: BIP-Rangliste
        Rectangle tab0Rect = new Rectangle(tab0X, tabY, tabW, tabH);
        bool hoverTab0 = Raylib.CheckCollisionPointRec(mousePos, tab0Rect);
        bool activeTab0 = ui.EconomyRankingTab == 0;
        Color tab0Color = activeTab0 ? ColorPalette.Accent : (hoverTab0 ? ColorPalette.PanelDark : ColorPalette.Panel);
        Raylib.DrawRectangleRounded(tab0Rect, GameConfig.BUTTON_ROUNDNESS, 8, tab0Color);
        DrawGameText("BIP-Rangliste", tab0X + 40, tabY + 7, 11, activeTab0 ? ColorPalette.TextWhite : ColorPalette.TextGray);

        if (hoverTab0 && Raylib.IsMouseButtonPressed(MouseButton.Left) && !activeTab0)
        {
            ui.EconomyRankingTab = 0;
            ui.EconomyRankingScrollOffset = 0;
            SoundManager.Play(SoundEffect.Click);
        }

        // Tab 1: Globale Wirtschaft
        Rectangle tab1Rect = new Rectangle(tab1X, tabY, tabW, tabH);
        bool hoverTab1 = Raylib.CheckCollisionPointRec(mousePos, tab1Rect);
        bool activeTab1 = ui.EconomyRankingTab == 1;
        Color tab1Color = activeTab1 ? ColorPalette.Accent : (hoverTab1 ? ColorPalette.PanelDark : ColorPalette.Panel);
        Raylib.DrawRectangleRounded(tab1Rect, GameConfig.BUTTON_ROUNDNESS, 8, tab1Color);
        DrawGameText("Globale Wirtschaft", tab1X + 22, tabY + 7, 11, activeTab1 ? ColorPalette.TextWhite : ColorPalette.TextGray);

        if (hoverTab1 && Raylib.IsMouseButtonPressed(MouseButton.Left) && !activeTab1)
        {
            ui.EconomyRankingTab = 1;
            ui.EconomyRankingScrollOffset = 0;
            SoundManager.Play(SoundEffect.Click);
        }

        // Schliessen-Button (X)
        int closeBtnSize = 30;
        int closeBtnX = panelX + panelW - closeBtnSize - 10;
        int closeBtnY = panelY + 10;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);
        bool hoverClose = Raylib.CheckCollisionPointRec(mousePos, closeRect);

        Raylib.DrawRectangleRec(closeRect, hoverClose ? ColorPalette.Red : ColorPalette.Panel);
        DrawGameText("X", closeBtnX + 9, closeBtnY + 5, 11, ColorPalette.TextWhite);

        if (hoverClose && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ui.ShowEconomyRanking = false;
            SoundManager.Play(SoundEffect.Click);
        }

        // Inhalt je nach Tab
        if (ui.EconomyRankingTab == 0)
            DrawEconomyRankingTab(panelX, panelY, panelW, panelH, headerH, mousePos);
        else
            DrawGlobalEconomyTab(panelX, panelY, panelW, panelH, headerH, mousePos);

        // ESC schliesst das Panel
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            ui.ShowEconomyRanking = false;
            SoundManager.Play(SoundEffect.Click);
        }
    }

    /// <summary>
    /// Tab 0: BIP-Rangliste (bisherige Ansicht)
    /// </summary>
    static void DrawEconomyRankingTab(int panelX, int panelY, int panelW, int panelH, int headerH, Vector2 mousePos)
    {
        // Spalten-Header
        int colRank = panelX + 15;
        int colFlag = panelX + 45;
        int colName = panelX + 85;
        int colGDP = panelX + 320;
        int colPerCapita = panelX + 470;
        int colGrowth = panelX + 620;
        int colPop = panelX + 740;
        int headerY = panelY + headerH + 8;

        Raylib.DrawRectangle(panelX + 5, headerY - 2, panelW - 10, 22, ColorPalette.PanelDark);
        DrawGameText("#", colRank, headerY, 11, ColorPalette.TextGray);
        DrawGameText("Land", colName, headerY, 11, ColorPalette.TextGray);
        DrawGameText("BIP", colGDP, headerY, 11, ColorPalette.TextGray);
        DrawGameText("BIP/Kopf", colPerCapita, headerY, 11, ColorPalette.TextGray);
        DrawGameText("Wachstum", colGrowth, headerY, 11, ColorPalette.TextGray);
        DrawGameText("Bevoelkerung", colPop, headerY, 11, ColorPalette.TextGray);

        // Content-Bereich mit Scrolling
        int contentY = headerY + 26;
        int contentH = panelH - headerH - 56;
        int rowH = 28;

        // Laender nach GDP sortieren
        var sortedCountries = game.Countries.Values
            .OrderByDescending(c => c.GDP)
            .ToList();

        int totalContentH = sortedCountries.Count * rowH;
        int maxScroll = Math.Max(0, totalContentH - contentH);
        ui.EconomyRankingScrollOffset = Math.Clamp(ui.EconomyRankingScrollOffset, 0, maxScroll);

        // Scroll via Mausrad
        Rectangle contentRect = new Rectangle(panelX, contentY, panelW, contentH);
        if (Raylib.CheckCollisionPointRec(mousePos, contentRect))
        {
            int wheel = (int)Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                ui.EconomyRankingScrollOffset -= wheel * rowH * 2;
                ui.EconomyRankingScrollOffset = Math.Clamp(ui.EconomyRankingScrollOffset, 0, maxScroll);
            }
        }

        // Scissor-Mode fuer Clipping
        Raylib.BeginScissorMode(panelX + 5, contentY, panelW - 10, contentH);

        string? playerId = game.PlayerCountry?.Id;

        for (int i = 0; i < sortedCountries.Count; i++)
        {
            var country = sortedCountries[i];
            int rowY = contentY + i * rowH - ui.EconomyRankingScrollOffset;

            // Sichtbarkeits-Check
            if (rowY + rowH < contentY || rowY > contentY + contentH)
                continue;

            // Alternierende Zeilenfarbe + Hervorhebung eigenes Land
            bool isPlayer = country.Id == playerId;
            Color rowColor;
            if (isPlayer)
                rowColor = new Color((byte)40, (byte)50, (byte)80, (byte)200);
            else if (i % 2 == 0)
                rowColor = new Color((byte)40, (byte)40, (byte)55, (byte)150);
            else
                rowColor = new Color((byte)35, (byte)35, (byte)48, (byte)150);

            Raylib.DrawRectangle(panelX + 5, rowY, panelW - 10, rowH, rowColor);

            if (isPlayer)
                Raylib.DrawRectangleLinesEx(new Rectangle(panelX + 5, rowY, panelW - 10, rowH), 1, ColorPalette.Accent);

            int textY = rowY + 6;

            // Rang
            DrawGameText((i + 1).ToString(), colRank, textY, 11, isPlayer ? ColorPalette.Accent : ColorPalette.TextGray);

            // Flagge
            DrawFlag(country.Id, colFlag, rowY + 4, 20);

            // Name
            Color nameColor = isPlayer ? ColorPalette.Accent : ColorPalette.TextWhite;
            DrawGameText(country.Name, colName, textY, 11, nameColor);

            // BIP
            DrawGameText(Formatting.Money(country.GDP), colGDP, textY, 11, ColorPalette.TextWhite);

            // BIP pro Kopf
            double gdpPerCapita = country.GetGDPPerCapita();
            string perCapitaStr = "$" + gdpPerCapita.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            DrawGameText(perCapitaStr, colPerCapita, textY, 11, ColorPalette.TextWhite);

            // Wachstum
            Color growthColor = country.GDPGrowthRate >= 0 ? ColorPalette.Green : ColorPalette.Red;
            DrawGameText(Formatting.Percentage(country.GDPGrowthRate, true), colGrowth, textY, 11, growthColor);

            // Bevoelkerung
            DrawGameText(Formatting.Population(country.Population), colPop, textY, 11, ColorPalette.TextWhite);
        }

        Raylib.EndScissorMode();

        // Scrollbar
        if (totalContentH > contentH)
        {
            int scrollBarX = panelX + panelW - 12;
            int scrollBarH = contentH;
            float scrollRatio = (float)contentH / totalContentH;
            int thumbH = Math.Max(20, (int)(scrollBarH * scrollRatio));
            int thumbY = contentY + (int)((float)ui.EconomyRankingScrollOffset / maxScroll * (scrollBarH - thumbH));

            Raylib.DrawRectangle(scrollBarX, contentY, 6, scrollBarH, ColorPalette.PanelDark);
            Raylib.DrawRectangle(scrollBarX, thumbY, 6, thumbH, ColorPalette.TextGray);
        }
    }

    /// <summary>
    /// Tab 1: Globale Wirtschaft - Ressourcen aller Laender
    /// </summary>
    static void DrawGlobalEconomyTab(int panelX, int panelY, int panelW, int panelH, int headerH, Vector2 mousePos)
    {
        int leftCol = panelX + 20;
        int rightCol = panelX + panelW / 2 + 10;
        int sectionW = panelW / 2 - 30;
        int chartFullW = panelW - 40;

        // Ressourcen-Hoehe berechnen (7 Zeilen a 28px + 50px Header/Padding)
        int resRowH = 28;
        int resSectionH = 7 * resRowH + 50;

        int resourceChartH = 220;

        // Gesamte Inhaltshoehe
        int totalContentH = 200 + 15 + resourceChartH + 15 + resSectionH + 15; // Stats + ResourceChart + Resources + Padding

        // Sichtbarer Bereich
        int contentY = panelY + headerH + 5;
        int contentH = panelH - headerH - 10;

        // Scroll-Logik
        int maxScroll = Math.Max(0, totalContentH - contentH);
        ui.EconomyRankingScrollOffset = Math.Clamp(ui.EconomyRankingScrollOffset, 0, maxScroll);

        Rectangle contentRect = new Rectangle(panelX, contentY, panelW, contentH);
        if (Raylib.CheckCollisionPointRec(mousePos, contentRect))
        {
            int wheel = (int)Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                ui.EconomyRankingScrollOffset -= wheel * 40;
                ui.EconomyRankingScrollOffset = Math.Clamp(ui.EconomyRankingScrollOffset, 0, maxScroll);
            }
        }

        int scroll = ui.EconomyRankingScrollOffset;

        // Scissor-Mode fuer Clipping
        Raylib.BeginScissorMode(panelX, contentY, panelW, contentH);

        int y = contentY + 10 - scroll;
        var countries = game.Countries.Values;

        // === OBERE SEITE: Weltwirtschafts-Uebersicht ===
        Raylib.DrawRectangleRounded(new Rectangle(leftCol, y, sectionW, 200), 0.03f, 8, ColorPalette.PanelDark);
        DrawGameText("Weltwirtschaft", leftCol + 15, y + 12, 13, ColorPalette.Accent);

        int infoY = y + 42;
        int lineH = 30;

        double totalGDP = countries.Sum(c => c.GDP);
        DrawGameText("Welt-BIP:", leftCol + 15, infoY, 11, ColorPalette.TextGray);
        DrawGameText(Formatting.Money(totalGDP), leftCol + 220, infoY, 11, ColorPalette.TextWhite);
        infoY += lineH;

        double totalDebt = countries.Sum(c => c.NationalDebt);
        DrawGameText("Gesamte Schulden:", leftCol + 15, infoY, 11, ColorPalette.TextGray);
        DrawGameText(Formatting.Money(totalDebt), leftCol + 220, infoY, 11, ColorPalette.Red);
        infoY += lineH;

        long totalPop = countries.Sum(c => c.Population);
        DrawGameText("Weltbevoelkerung:", leftCol + 15, infoY, 11, ColorPalette.TextGray);
        DrawGameText(Formatting.Population(totalPop), leftCol + 220, infoY, 11, ColorPalette.TextWhite);
        infoY += lineH;

        double avgGdpPerCapita = totalPop > 0 ? (totalGDP * 1_000_000) / totalPop : 0;
        DrawGameText("BIP/Kopf (Welt):", leftCol + 15, infoY, 11, ColorPalette.TextGray);
        DrawGameText("$" + avgGdpPerCapita.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), leftCol + 220, infoY, 11, ColorPalette.TextWhite);

        // === RECHTE SEITE: Top 5 Budget ===
        Raylib.DrawRectangleRounded(new Rectangle(rightCol, y, sectionW, 200), 0.03f, 8, ColorPalette.PanelDark);
        DrawGameText("Top 5 - Budget", rightCol + 15, y + 12, 13, ColorPalette.Accent);

        var topBudget = countries.OrderByDescending(c => c.Budget).Take(5).ToList();
        int topY = y + 42;
        string? playerId = game.PlayerCountry?.Id;

        for (int i = 0; i < topBudget.Count; i++)
        {
            var c = topBudget[i];
            bool isPlayer = c.Id == playerId;
            Color textColor = isPlayer ? ColorPalette.Accent : ColorPalette.TextWhite;

            DrawGameText($"{i + 1}.", rightCol + 15, topY, 11, ColorPalette.TextGray);
            DrawFlag(c.Id, rightCol + 38, topY - 2, 18);
            DrawGameText(c.Name, rightCol + 65, topY, 11, textColor);
            DrawGameText(Formatting.Money(c.Budget), rightCol + sectionW - 150, topY, 11, textColor);
            topY += lineH;
        }

        // === MITTE: Guetermengen-Liniendiagramm (alle Ressourcen) ===
        int resChartY = y + 215;
        Raylib.DrawRectangleRounded(new Rectangle(leftCol, resChartY, chartFullW, resourceChartH), 0.03f, 8, ColorPalette.PanelDark);
        DrawGameText("Guetermengen (Verlauf)", leftCol + 15, resChartY + 12, 13, ColorPalette.Accent);
        DrawResourceChart(leftCol + 15, resChartY + 35, chartFullW - 30, resourceChartH - 50, mousePos);

        // === UNTERE SEITE: Ressourcen im Umlauf ===
        int resY = resChartY + resourceChartH + 15;
        Raylib.DrawRectangleRounded(new Rectangle(leftCol, resY, chartFullW, resSectionH), 0.03f, 8, ColorPalette.PanelDark);
        DrawGameText("Ressourcen im Umlauf (alle Laender summiert)", leftCol + 15, resY + 12, 13, ColorPalette.Accent);

        // Ressourcen summieren
        var resourceTypes = Enum.GetValues<ResourceType>();
        var totalResources = new Dictionary<ResourceType, double>();
        foreach (var rt in resourceTypes)
            totalResources[rt] = 0;

        foreach (var country in countries)
        {
            foreach (var rt in resourceTypes)
                totalResources[rt] += country.GetResource(rt);
        }

        // Ressourcen in 2 Spalten zeichnen
        int resStartY = resY + 40;
        int col1X = leftCol + 15;
        int col2X = rightCol;
        int resIndex = 0;

        foreach (var rt in resourceTypes)
        {
            double amount = totalResources[rt];
            int colX = resIndex < 7 ? col1X : col2X;
            int rowY = resStartY + (resIndex % 7) * resRowH;

            var icon = TextureManager.GetResource(rt);
            if (icon.HasValue)
            {
                Raylib.DrawTexturePro(icon.Value,
                    new Rectangle(0, 0, icon.Value.Width, icon.Value.Height),
                    new Rectangle(colX, rowY, 18, 18), System.Numerics.Vector2.Zero, 0, Color.White);
            }

            string name = Models.Resource.GetGermanName(rt);
            DrawGameText(name, colX + 22, rowY + 2, 11, ColorPalette.TextGray);
            DrawGameText(Formatting.Resource(amount), colX + 130, rowY + 2, 11, ColorPalette.TextWhite);

            resIndex++;
        }

        Raylib.EndScissorMode();

        // Scrollbar zeichnen (ausserhalb Scissor)
        if (maxScroll > 0)
        {
            int scrollBarX = panelX + panelW - 14;
            float scrollRatio = (float)contentH / totalContentH;
            int thumbH = Math.Max(20, (int)(contentH * scrollRatio));
            int thumbY = contentY + (int)((float)scroll / maxScroll * (contentH - thumbH));

            Raylib.DrawRectangle(scrollBarX, contentY, 6, contentH, ColorPalette.PanelDark);
            Raylib.DrawRectangleRounded(new Rectangle(scrollBarX, thumbY, 6, thumbH), 0.5f, 4, ColorPalette.TextGray);
        }
    }

    /// <summary>
    /// Farben fuer jede Ressource im Guetermengen-Diagramm
    /// </summary>
    static readonly Dictionary<ResourceType, Color> ResourceChartColors = new()
    {
        { ResourceType.Oil,          new Color((byte)0,  (byte)0,  (byte)0,  (byte)255) },  // Schwarz (Erdoel)
        { ResourceType.NaturalGas,   new Color((byte)100, (byte)180, (byte)255, (byte)255) },  // Hellblau
        { ResourceType.Coal,         new Color((byte)120, (byte)100, (byte)80,  (byte)255) },  // Braun
        { ResourceType.Iron,         new Color((byte)180, (byte)180, (byte)190, (byte)255) },  // Silber
        { ResourceType.Copper,       new Color((byte)200, (byte)130, (byte)50,  (byte)255) },  // Kupferfarbe
        { ResourceType.Uranium,      new Color((byte)80,  (byte)220, (byte)80,  (byte)255) },  // Giftgruen
        { ResourceType.Food,         new Color((byte)200, (byte)180, (byte)50,  (byte)255) },  // Gelb/Weizen
        { ResourceType.Steel,        new Color((byte)160, (byte)170, (byte)200, (byte)255) },  // Stahlblau
        { ResourceType.Electronics,  new Color((byte)50,  (byte)200, (byte)200, (byte)255) },  // Cyan
        { ResourceType.Machinery,    new Color((byte)200, (byte)100, (byte)200, (byte)255) },  // Magenta
        { ResourceType.ConsumerGoods,new Color((byte)255, (byte)150, (byte)100, (byte)255) },  // Lachs
        { ResourceType.Weapons,      new Color((byte)200, (byte)60,  (byte)60,  (byte)255) },  // Rot
        { ResourceType.Ammunition,   new Color((byte)180, (byte)120, (byte)60,  (byte)255) },  // Dunkelorange
    };

    /// <summary>
    /// Zeichnet ein Liniendiagramm der globalen Guetermengen ueber Zeit (alle Ressourcen)
    /// mit klickbarer Legende zum Filtern
    /// </summary>
    static void DrawResourceChart(int x, int y, int w, int h, Vector2 mousePos)
    {
        var history = _mgr.Economy?.MoneyHistory;
        if (history == null || history.Count < 2 || history[0].ResourceTotals.Count == 0)
        {
            DrawGameText("Noch keine Daten (mind. 2 Wochen Spielzeit)", x + 10, y + h / 2 - 6, 11, ColorPalette.TextGray);
            return;
        }

        var filter = ui.ResourceChartFilter;
        int legendW = 120;
        int marginLeft = 80;
        int marginBottom = 20;
        int chartX = x + marginLeft;
        int chartW = w - marginLeft - legendW - 15;
        int chartH = h - marginBottom;

        // Min/Max nur ueber aktive Ressourcen bestimmen
        double maxVal = 0;
        var resourceTypes = Enum.GetValues<ResourceType>();
        foreach (var snap in history)
        {
            foreach (var rt in resourceTypes)
            {
                if (!filter.Contains(rt)) continue;
                if (snap.ResourceTotals.TryGetValue(rt, out var val) && val > maxVal)
                    maxVal = val;
            }
        }

        if (maxVal < 1) maxVal = 1;
        double displayMax = maxVal * 1.1;

        // Horizontale Rasterlinien (5 Stueck)
        for (int i = 0; i <= 4; i++)
        {
            float ratio = i / 4f;
            int lineY = y + chartH - (int)(ratio * chartH);
            double val = ratio * displayMax;

            Raylib.DrawLine(chartX, lineY, chartX + chartW, lineY, new Color((byte)60, (byte)60, (byte)80, (byte)100));
            DrawGameText(Formatting.Resource(val), x, lineY - 5, 9, ColorPalette.TextGray);
        }

        // Achsenlinien
        Raylib.DrawLine(chartX, y, chartX, y + chartH, new Color((byte)80, (byte)80, (byte)100, (byte)180));
        Raylib.DrawLine(chartX, y + chartH, chartX + chartW, y + chartH, new Color((byte)80, (byte)80, (byte)100, (byte)180));

        // X-Achse Labels
        int count = history.Count;
        int labelCount = Math.Min(5, count);
        var labelIndices = new HashSet<int>();
        for (int i = 0; i < labelCount; i++)
        {
            int idx = (int)((double)i / (labelCount - 1) * (count - 1));
            labelIndices.Add(idx);
        }

        for (int i = 0; i < count; i++)
        {
            if (labelIndices.Contains(i))
            {
                float xRatio = (float)i / (count - 1);
                int px = chartX + (int)(xRatio * chartW);
                DrawGameText(history[i].DateLabel, px - 15, y + chartH + 4, 8, ColorPalette.TextGray);
            }
        }

        // Linien nur fuer aktive Ressourcen zeichnen
        foreach (var rt in resourceTypes)
        {
            if (!filter.Contains(rt)) continue;

            if (!ResourceChartColors.TryGetValue(rt, out var color))
                color = ColorPalette.TextGray;

            int prevPx = 0, prevPy = 0;
            for (int i = 0; i < count; i++)
            {
                double val = history[i].ResourceTotals.GetValueOrDefault(rt, 0);
                float xRatio = (float)i / (count - 1);
                float yRatio = (float)(val / displayMax);

                int px = chartX + (int)(xRatio * chartW);
                int py = y + chartH - (int)(yRatio * chartH);

                if (i > 0)
                {
                    Raylib.DrawLine(prevPx, prevPy, px, py, color);
                }

                prevPx = px;
                prevPy = py;
            }
        }

        // === Klickbare Legende rechts neben dem Chart ===
        int legendX = chartX + chartW + 15;
        int legendY = y;
        int legendLineH = (int)((float)chartH / resourceTypes.Length);
        legendLineH = Math.Clamp(legendLineH, 14, 18);
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        foreach (var rt in resourceTypes)
        {
            if (!ResourceChartColors.TryGetValue(rt, out var color))
                color = ColorPalette.TextGray;

            bool active = filter.Contains(rt);
            Rectangle legendRect = new Rectangle(legendX - 2, legendY, legendW, legendLineH);
            bool hovered = Raylib.CheckCollisionPointRec(mousePos, legendRect);

            // Klick: Ressource ein-/ausschalten
            if (hovered && clicked)
            {
                if (active)
                    filter.Remove(rt);
                else
                    filter.Add(rt);
                active = !active;
            }

            // Hover-Hintergrund
            if (hovered)
                Raylib.DrawRectangleRounded(legendRect, 0.3f, 4, new Color((byte)60, (byte)60, (byte)80, (byte)120));

            // Checkbox
            Color boxColor = active ? color : new Color((byte)60, (byte)60, (byte)70, (byte)255);
            Raylib.DrawRectangle(legendX, legendY + 2, 10, 10, boxColor);
            Raylib.DrawRectangleLines(legendX, legendY + 2, 10, 10, active ? color : ColorPalette.TextDark);

            // Kurzname
            string name = Models.Resource.GetGermanName(rt);
            if (name.Length > 12) name = name[..12];
            Color textColor = active ? ColorPalette.TextWhite : ColorPalette.TextDark;
            DrawGameText(name, legendX + 14, legendY, 8, textColor);

            legendY += legendLineH;
        }
    }

    /// <summary>
    /// Zeichnet einen Tutorial-Abschnitt mit Icon und Text
    /// </summary>
    static int DrawTutorialSection(int x, int y, int width, string title, string iconName, string[] lines, int iconSize, int lineSpacing)
    {
        // Icon laden falls vorhanden
        Texture2D? icon = null;
        if (!_tutorialIcons.TryGetValue(iconName, out var cachedIcon))
        {
            string iconPath = Path.Combine("Data", "Icons", iconName);
            if (File.Exists(iconPath))
            {
                var tex = Raylib.LoadTexture(iconPath);
                if (tex.Id != 0)
                {
                    Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
                    _tutorialIcons[iconName] = tex;
                    icon = tex;
                }
            }
        }
        else
        {
            icon = cachedIcon;
        }

        int currentY = y;

        // Titel-Zeile mit Icon
        if (icon != null)
        {
            // Icon zeichnen
            float scale = (float)iconSize / Math.Max(icon.Value.Width, icon.Value.Height);
            int drawW = (int)(icon.Value.Width * scale);
            int drawH = (int)(icon.Value.Height * scale);
            Rectangle source = new(0, 0, icon.Value.Width, icon.Value.Height);
            Rectangle dest = new(x, currentY, drawW, drawH);
            Raylib.DrawTexturePro(icon.Value, source, dest, Vector2.Zero, 0, ColorPalette.Accent);

            // Titel neben Icon
            DrawGameText(title, x + iconSize + 15, currentY + iconSize / 2 - 10, 11, ColorPalette.Accent);
            currentY += iconSize + 10;
        }
        else
        {
            // Nur Titel
            DrawGameText(title, x, currentY, 11, ColorPalette.Accent);
            currentY += 30;
        }

        // Trennlinie
        Raylib.DrawLine(x, currentY, x + width - 20, currentY, ColorPalette.PanelLight);
        currentY += 10;

        // Text-Zeilen
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                currentY += lineSpacing / 2;
            }
            else
            {
                Color textColor = line.StartsWith("  -") ? ColorPalette.TextGray : ColorPalette.TextWhite;
                if (line.StartsWith("  ") && !line.StartsWith("  -"))
                {
                    textColor = new Color((byte)180, (byte)180, (byte)180, (byte)255);
                }
                DrawGameText(line, x + 10, currentY, 11, textColor);
                currentY += lineSpacing;
            }
        }

        return currentY;
    }

    /// <summary>
    /// Berechnet den Tag des Jahres (1-365) aus Monat und Tag
    /// </summary>
    static int GetDayOfYear(int month, int day)
    {
        int[] daysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        int dayOfYear = day;
        for (int i = 0; i < month - 1; i++)
        {
            dayOfYear += daysInMonth[i];
        }
        return dayOfYear;
    }
}
