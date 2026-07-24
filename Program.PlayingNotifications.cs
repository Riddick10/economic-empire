using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame;

/// <summary>
/// Program - Benachrichtigungs-Popups als Smartphone mit X-App (Dark Mode).
/// Zeigt die neueste Meldung als grossen Post, aeltere Meldungen darunter
/// als scrollbaren Feed - wie eine echte Timeline.
/// </summary>
partial class Program
{
    // === X Dark-Mode Farbpalette ===
    static readonly Color XBg = new(0, 0, 0, 255);
    static readonly Color XBorder = new((byte)47, (byte)51, (byte)54, (byte)255);
    static readonly Color XText = new((byte)231, (byte)233, (byte)234, (byte)255);
    static readonly Color XGray = new((byte)113, (byte)118, (byte)123, (byte)255);
    static readonly Color XBlue = new((byte)29, (byte)155, (byte)240, (byte)255);
    static readonly Color XLikePink = new((byte)249, (byte)24, (byte)128, (byte)255);
    static readonly Color XRepostGreen = new((byte)0, (byte)186, (byte)124, (byte)255);

    static Color NotificationTypeColor(NotificationType type) => type switch
    {
        NotificationType.Info => XBlue,
        NotificationType.Warning => new Color((byte)255, (byte)212, (byte)0, (byte)255),
        NotificationType.Danger => new Color((byte)244, (byte)33, (byte)46, (byte)255),
        NotificationType.Success => XRepostGreen,
        _ => XGray
    };

    static string NotificationDisplayName(NotificationType type) => type switch
    {
        NotificationType.Info => "Weltnachrichten",
        NotificationType.Warning => "Weltnachrichten",
        NotificationType.Danger => "Weltnachrichten",
        NotificationType.Success => "Weltnachrichten",
        _ => "News"
    };

    static string? NotificationTypeChip(NotificationType type) => type switch
    {
        NotificationType.Warning => "WARNUNG",
        NotificationType.Danger => "EILMELDUNG",
        NotificationType.Success => "ERFOLG",
        _ => null
    };

    /// <summary>
    /// Formatiert Zahlen im X-Stil: 843, 12,4K, 1,3M
    /// </summary>
    static string FormatXCount(int n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000.0:0.#}M".Replace('.', ',');
        if (n >= 10_000) return $"{n / 1000}K";
        if (n >= 1_000) return $"{n / 1000.0:0.#}K".Replace('.', ',');
        return n.ToString();
    }

    /// <summary>
    /// Zeichnet aktive Popup-Nachrichten als Smartphone mit X-App (scrollbar)
    /// </summary>
    static void DrawNotificationPhone()
    {
        var notifMgr = _mgr.Notif;
        if (notifMgr == null) return;

        var popups = notifMgr.ActivePopups;
        bool manual = ui.ShowNotificationPhone;   // per Handy-Button geoeffnet (volle Historie)
        bool showForPopup = popups.Count > 0;      // neue Meldung poppt automatisch auf
        if (!manual && !showForPopup) return;

        var all = notifMgr.Notifications;          // alle Nachrichten, neueste zuerst

        // Hauptpost: aktives Popup, sonst die neueste Nachricht
        GameNotification? main = showForPopup ? popups[0] : (all.Count > 0 ? all[0] : null);

        // Scroll-Reset bei Wechsel des Hauptposts / beim Oeffnen
        int mainId = main?.Id ?? -1;
        if (mainId != ui.LastNotificationId)
        {
            ui.NotificationScrollOffset = 0;
            ui.LastNotificationId = mainId;
        }

        Vector2 mousePos = _cachedMousePos;

        // === SMARTPHONE-DIMENSIONEN (modernes 19.5:9-Format) ===
        int phoneW = 370;
        int phoneH = 730;
        int phoneX = (ScreenWidth - phoneW) / 2;
        int phoneY = (ScreenHeight - phoneH) / 2;

        // Hintergrund abdunkeln
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)165));

        // === RAHMEN (Titan-Look mit Seitentasten) ===
        Raylib.DrawRectangleRounded(new Rectangle(phoneX - 3, phoneY - 3, phoneW + 6, phoneH + 6), 0.09f, 12,
            new Color((byte)96, (byte)96, (byte)100, (byte)255));
        Raylib.DrawRectangleRounded(new Rectangle(phoneX, phoneY, phoneW, phoneH), 0.088f, 12,
            new Color((byte)12, (byte)12, (byte)14, (byte)255));

        // Seitentasten (links: Lautstaerke, rechts: Power)
        Color buttonColor = new((byte)70, (byte)70, (byte)74, (byte)255);
        Raylib.DrawRectangleRounded(new Rectangle(phoneX - 5, phoneY + 130, 3, 34), 0.5f, 4, buttonColor);
        Raylib.DrawRectangleRounded(new Rectangle(phoneX - 5, phoneY + 175, 3, 54), 0.5f, 4, buttonColor);
        Raylib.DrawRectangleRounded(new Rectangle(phoneX + phoneW + 2, phoneY + 160, 3, 70), 0.5f, 4, buttonColor);

        // === DISPLAY ===
        int screenX = phoneX + 8;
        int screenY = phoneY + 8;
        int screenW = phoneW - 16;
        int screenH = phoneH - 16;
        Raylib.DrawRectangleRounded(new Rectangle(screenX, screenY, screenW, screenH), 0.07f, 12, XBg);

        // === STATUSLEISTE (Spielzeit + Empfang/Akku) ===
        int statusY = screenY + 10;
        string clock = $"{game.Hour:D2}:{game.Minute:D2}";
        DrawGameText(clock, screenX + 24, statusY, 13, XText);

        // Empfangsbalken
        int sigX = screenX + screenW - 88;
        for (int i = 0; i < 4; i++)
        {
            int barH = 4 + i * 2;
            Raylib.DrawRectangle(sigX + i * 5, statusY + 11 - barH, 3, barH, i < 3 ? XText : XGray);
        }

        // Akku (Umriss + Fuellstand + Nase)
        int batX = screenX + screenW - 52;
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(batX, statusY + 1, 24, 11), 0.3f, 4, 1, XGray);
        Raylib.DrawRectangleRounded(new Rectangle(batX + 2, statusY + 3, 15, 7), 0.3f, 4, XText);
        Raylib.DrawRectangle(batX + 25, statusY + 4, 2, 5, XGray);

        // Dynamic Island (Pille mit Kamera)
        int islandW = 92;
        int islandH = 24;
        int islandX = screenX + (screenW - islandW) / 2;
        Raylib.DrawRectangleRounded(new Rectangle(islandX, screenY + 6, islandW, islandH), 1f, 10,
            new Color((byte)8, (byte)8, (byte)10, (byte)255));
        Raylib.DrawCircle(islandX + islandW - 16, screenY + 6 + islandH / 2, 5,
            new Color((byte)26, (byte)30, (byte)38, (byte)255));

        // === APP-HEADER: Avatar links, X-Logo mittig ===
        int headerY = screenY + 38;
        int headerH = 42;

        // Spieler-Avatar (eigene Flagge, rund maskiert)
        string? playerId = game.PlayerCountry?.Id;
        DrawRoundAvatar(screenX + 26, headerY + headerH / 2 - 2, 15, playerId, XGray);

        // X-Logo (zwei kraeftige Diagonalstriche)
        DrawXLogo(screenX + screenW / 2, headerY + headerH / 2 - 2, 11, XText);

        // === TABS: "Für dich" | "Folge ich" ===
        int tabsY = headerY + headerH;
        int tabsH = 36;
        string tab1 = "Für dich";
        int tab1W = MeasureTextCached(tab1, 14);
        int tab1X = screenX + screenW / 4 - tab1W / 2;
        DrawGameText(tab1, tab1X, tabsY + 8, 14, XText);
        // Aktiver Tab: blauer Unterstrich (Pille)
        Raylib.DrawRectangleRounded(new Rectangle(tab1X - 4, tabsY + tabsH - 4, tab1W + 8, 3), 0.5f, 4, XBlue);

        string tab2 = "Folge ich";
        int tab2W = MeasureTextCached(tab2, 14);
        DrawGameText(tab2, screenX + screenW * 3 / 4 - tab2W / 2, tabsY + 8, 14, XGray);

        Raylib.DrawLine(screenX, tabsY + tabsH, screenX + screenW, tabsY + tabsH, XBorder);

        // === SCROLLBARER FEED ===
        int navH = 46;
        int contentY = tabsY + tabsH + 1;
        int contentH = screenH - (contentY - screenY) - navH - 16;

        var contentClip = new Rectangle(screenX, contentY, screenW, contentH);
        Raylib.BeginScissorMode(screenX, contentY, screenW, contentH);

        int scrollY = contentY - ui.NotificationScrollOffset;
        int cursorY = scrollY;

        if (main != null)
        {
            // Hauptpost (neueste Meldung, gross)
            cursorY = DrawXPost(main, screenX, cursorY, screenW, isMainPost: true, contentClip);

            // Alle weiteren (auch historischen) Meldungen als Feed, neueste zuerst
            for (int i = 0; i < all.Count; i++)
            {
                var n = all[i];
                if (n.Id == main.Id) continue;
                cursorY = DrawXPost(n, screenX, cursorY, screenW, isMainPost: false, contentClip);
            }
        }
        else
        {
            // Leerzustand (Handy manuell geoeffnet, aber keine Nachrichten)
            string empty = "Keine Nachrichten";
            int ew = MeasureTextCached(empty, 15);
            DrawGameText(empty, screenX + (screenW - ew) / 2, contentY + 40, 15, XGray);
        }

        int totalContentH = cursorY - scrollY + 10;
        int maxScroll = Math.Max(0, totalContentH - contentH);

        Raylib.EndScissorMode();

        // Scroll-Indikator (rechts)
        if (maxScroll > 0)
        {
            int scrollBarH = contentH - 20;
            int scrollThumbH = Math.Max(30, scrollBarH * contentH / totalContentH);
            int scrollThumbY = contentY + 10 + (int)((scrollBarH - scrollThumbH) * ((float)ui.NotificationScrollOffset / maxScroll));
            Raylib.DrawRectangleRounded(new Rectangle(screenX + screenW - 6, scrollThumbY, 4, scrollThumbH), 0.5f, 4,
                new Color((byte)100, (byte)100, (byte)110, (byte)150));
        }

        // === BOTTOM-NAVIGATION (Home, Suche, Glocke, Post) ===
        int bottomNavY = screenY + screenH - navH - 14;
        Raylib.DrawRectangle(screenX, bottomNavY, screenW, navH, XBg);
        Raylib.DrawLine(screenX, bottomNavY, screenX + screenW, bottomNavY, XBorder);

        int navCy = bottomNavY + navH / 2;
        int navStep = screenW / 4;
        DrawHouseIcon(screenX + navStep / 2, navCy, 9, XText);           // Home (aktiv)
        DrawSearchIcon(screenX + navStep + navStep / 2, navCy, 8, XGray);
        DrawBellIcon(screenX + navStep * 2 + navStep / 2, navCy, 9, XGray);
        DrawMailIcon(screenX + navStep * 3 + navStep / 2, navCy, 9, XGray);

        // Home-Indikator
        int homeBarW = 110;
        Raylib.DrawRectangleRounded(new Rectangle(phoneX + (phoneW - homeBarW) / 2, phoneY + phoneH - 12, homeBarW, 4),
            0.5f, 4, new Color((byte)190, (byte)190, (byte)195, (byte)255));

        // === SCHLIESSEN-BUTTON (ausserhalb des Handys) ===
        int closeBtnX = phoneX + phoneW + 15;
        int closeBtnY = phoneY;
        int closeBtnSize = 40;
        Rectangle closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);
        bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);

        Raylib.DrawCircle(closeBtnX + closeBtnSize / 2, closeBtnY + closeBtnSize / 2, closeBtnSize / 2,
            closeHover ? new Color((byte)200, (byte)60, (byte)60, (byte)255) : new Color((byte)60, (byte)60, (byte)65, (byte)255));
        int xW = MeasureTextCached("X", 16);
        DrawGameText("X", closeBtnX + (closeBtnSize - xW) / 2, closeBtnY + 11, 16, Color.White);

        // Scroll-Hinweis
        if (maxScroll > 0 && ui.NotificationScrollOffset < maxScroll)
        {
            string scrollHint = "Scrollen für ältere Meldungen";
            int hintW = MeasureTextCached(scrollHint, 12);
            DrawGameText(scrollHint, phoneX + (phoneW - hintW) / 2, phoneY + phoneH + 10, 12, ColorPalette.TextGray);
        }

        // === INPUT-HANDLING ===
        Rectangle screenRect = new Rectangle(screenX, contentY, screenW, contentH);
        if (Raylib.CheckCollisionPointRec(mousePos, screenRect))
        {
            int wheel = (int)Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                ui.NotificationScrollOffset -= wheel * 30;
                ui.NotificationScrollOffset = Math.Clamp(ui.NotificationScrollOffset, 0, maxScroll);
            }
        }

        if (Raylib.IsKeyDown(KeyboardKey.Down) || Raylib.IsKeyDown(KeyboardKey.S))
            ui.NotificationScrollOffset = Math.Min(ui.NotificationScrollOffset + 5, maxScroll);
        if (Raylib.IsKeyDown(KeyboardKey.Up) || Raylib.IsKeyDown(KeyboardKey.W))
            ui.NotificationScrollOffset = Math.Max(ui.NotificationScrollOffset - 5, 0);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && closeHover)
        {
            if (manual) ui.ShowNotificationPhone = false;
            if (showForPopup) notifMgr.DismissAllPopups();   // ganze Salve auf einmal schliessen
            ui.NotificationScrollOffset = 0;
            SoundManager.Play(SoundEffect.Click);
        }
        // Escape schliesst das Handy - zentral in Program.Update (vor dem Pause-Menue) behandelt.
    }

    /// <summary>
    /// Zeichnet einen einzelnen X-Post und gibt die End-Y-Position zurueck.
    /// Hauptpost: gross, mit Typ-Chip, Bild und voller Interaktionsleiste.
    /// Feed-Post: kompakt (kleinere Schrift, gekuerzter Text, kleine Leiste).
    /// </summary>
    static int DrawXPost(GameNotification post, int screenX, int y, int screenW, bool isMainPost,
        Rectangle contentClip)
    {
        int pad = 14;
        int postX = screenX + pad;
        int postW = screenW - pad * 2;
        int avatarSize = isMainPost ? 44 : 36;
        int nameSize = isMainPost ? 15 : 13;
        int textSize = isMainPost ? 14 : 13;
        int lineHeight = textSize + 6;
        int topY = y + 12;

        Color typeColor = NotificationTypeColor(post.Type);

        // Avatar (Flagge rund maskiert oder Typ-Kreis)
        DrawRoundAvatar(postX + avatarSize / 2, topY + avatarSize / 2, avatarSize / 2,
            post.RelatedCountryId, typeColor, contentClip);

        // Name + Verifiziert-Haken
        int textX = postX + avatarSize + 10;
        string displayName = NotificationDisplayName(post.Type);
        DrawGameText(displayName, textX, topY, nameSize, XText);
        int nameW = MeasureTextCached(displayName, nameSize);
        DrawVerifiedBadge(textX + nameW + 12, topY + nameSize / 2 + 1, isMainPost ? 7 : 6);

        // Handle + Datum
        string handle = post.RelatedCountryId != null ? $"@{post.RelatedCountryId}" : "@EconomicEmpire";
        DrawGameText($"{handle} · {post.DateString}", textX, topY + nameSize + 5, 12, XGray);

        int cursorY = topY + avatarSize + 12;

        // Typ-Chip (WARNUNG/EILMELDUNG/ERFOLG) - nur wenn kein normaler Info-Post
        string? chip = NotificationTypeChip(post.Type);
        if (chip != null)
        {
            int chipW = MeasureTextCached(chip, 11) + 14;
            Raylib.DrawRectangleRounded(new Rectangle(postX, cursorY, chipW, 18), 0.5f, 6,
                new Color(typeColor.R, typeColor.G, typeColor.B, (byte)45));
            DrawGameText(chip, postX + 7, cursorY + 3, 11, typeColor);
            cursorY += 24;
        }

        // Titel (weiss, wie fetter Post-Anfang)
        var titleLines = WrapText(post.Title, postW, isMainPost ? 16 : 14);
        foreach (var line in titleLines)
        {
            DrawGameText(line, postX, cursorY, isMainPost ? 16 : 14, XText);
            cursorY += (isMainPost ? 16 : 14) + 6;
        }
        cursorY += 4;

        // Nachrichtentext (Feed-Posts: auf 3 Zeilen gekuerzt)
        string fullMsg = post.Message;
        string firstPart = fullMsg;
        string restPart = "";

        if (isMainPost && !string.IsNullOrEmpty(post.ImageName))
        {
            int dotIndex = fullMsg.IndexOf(". ");
            if (dotIndex > 0)
            {
                firstPart = fullMsg.Substring(0, dotIndex + 1);
                restPart = fullMsg.Substring(dotIndex + 2).Trim();
            }
        }

        var msgLines = WrapText(firstPart, postW, textSize);
        if (!isMainPost && msgLines.Count > 3)
        {
            msgLines = msgLines.GetRange(0, 3);
            msgLines[2] += " …";
        }
        foreach (var line in msgLines)
        {
            DrawGameText(line, postX, cursorY, textSize, XText);
            cursorY += lineHeight;
        }

        // Bild (nur Hauptpost, mit abgerundetem Rahmen)
        if (isMainPost && !string.IsNullOrEmpty(post.ImageName))
        {
            var newsImage = LoadNewsImage(post.ImageName);
            if (newsImage.HasValue && newsImage.Value.Id != 0)
            {
                int imgY = cursorY + 10;
                var tex = newsImage.Value;
                float scale = Math.Min((float)postW / tex.Width, 180f / tex.Height);
                int drawW = (int)(tex.Width * scale);
                int drawH = (int)(tex.Height * scale);
                int imgX = postX + (postW - drawW) / 2;

                Raylib.DrawRectangleRounded(new Rectangle(imgX - 2, imgY - 2, drawW + 4, drawH + 4), 0.08f, 8, XBorder);
                Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height),
                    new Rectangle(imgX, imgY, drawW, drawH), Vector2.Zero, 0, Color.White);

                cursorY = imgY + drawH + 12;
            }

            // Rest des Textes nach dem Bild
            if (!string.IsNullOrEmpty(restPart))
            {
                foreach (var line in WrapText(restPart, postW, textSize))
                {
                    DrawGameText(line, postX, cursorY, textSize, XText);
                    cursorY += lineHeight;
                }
            }
        }

        // Interaktionsleiste (Antworten, Reposts, Likes, Aufrufe, Teilen)
        cursorY += 10;
        int iconSize = isMainPost ? 17 : 14;
        int fontS = isMainPost ? 12 : 11;
        int iconCy = cursorY + iconSize / 2;
        int step = postW / 5;

        int replies = post.Id * 7 % 89 + 3;
        int reposts = post.Id * 13 % 420 + 12;
        int likes = post.Id * 234 % 8200 + 156;
        int views = post.Id * 1237 % 240_000 + 5400;

        DrawCommentIcon(postX, iconCy, iconSize, XGray);
        DrawGameText(FormatXCount(replies), postX + iconSize + 5, cursorY + 2, fontS, XGray);

        DrawRetweetIcon(postX + step, iconCy, iconSize, XGray);
        DrawGameText(FormatXCount(reposts), postX + step + iconSize + 5, cursorY + 2, fontS, XGray);

        DrawHeartIcon(postX + step * 2, iconCy, iconSize, isMainPost ? XLikePink : XGray);
        DrawGameText(FormatXCount(likes), postX + step * 2 + iconSize + 5, cursorY + 2, fontS,
            isMainPost ? XLikePink : XGray);

        DrawViewsIcon(postX + step * 3, iconCy, iconSize, XGray);
        DrawGameText(FormatXCount(views), postX + step * 3 + iconSize + 5, cursorY + 2, fontS, XGray);

        DrawShareIcon(postX + step * 4 + step / 2, iconCy, iconSize, XGray);

        cursorY += iconSize + 12;

        // Hairline-Trenner zwischen Posts
        Raylib.DrawLine(screenX, cursorY, screenX + screenW, cursorY, XBorder);

        return cursorY;
    }

    /// <summary>
    /// Rundes Profilbild: Flagge mit Kreis-Maske oder farbiger Typ-Kreis.
    /// Die Maske (Ring in Hintergrundfarbe) wird per Scissor auf das Avatar-
    /// Quadrat begrenzt, damit sie keine Nachbar-Inhalte uebermalt.
    /// clipBounds: aktiver aeusserer Scissor-Bereich, der danach
    /// wiederhergestellt wird (Raylib hat keinen Scissor-Stack).
    /// </summary>
    static void DrawRoundAvatar(int cx, int cy, int radius, string? countryId, Color fallbackColor,
        Rectangle? clipBounds = null)
    {
        var center = new Vector2(cx, cy);

        if (countryId != null)
        {
            var flagTex = GetFlagTexture(countryId);
            if (flagTex != null)
            {
                // Avatar-Quadrat, ggf. mit dem aeusseren Clip-Bereich geschnitten
                int bx = cx - radius, by = cy - radius, bw = radius * 2, bh = radius * 2;
                if (clipBounds is Rectangle cb)
                {
                    int nx = Math.Max(bx, (int)cb.X);
                    int ny = Math.Max(by, (int)cb.Y);
                    int nx2 = Math.Min(bx + bw, (int)(cb.X + cb.Width));
                    int ny2 = Math.Min(by + bh, (int)(cb.Y + cb.Height));
                    if (nx2 <= nx || ny2 <= ny) return; // komplett ausserhalb
                    bx = nx; by = ny; bw = nx2 - nx; bh = ny2 - ny;
                }

                Raylib.BeginScissorMode(bx, by, bw, bh);

                var tex = flagTex.Value;
                float scale = (radius * 2f) / Math.Min(tex.Width, tex.Height);
                int drawW = (int)(tex.Width * scale);
                int drawH = (int)(tex.Height * scale);
                Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height),
                    new Rectangle(cx - drawW / 2, cy - drawH / 2, drawW, drawH), Vector2.Zero, 0, Color.White);

                // Kreis-Maske: Ring kaschiert die Ecken (1.6r deckt die
                // Quadrat-Diagonale ab, Scissor begrenzt den Ueberstand)
                Raylib.DrawRing(center, radius, radius * 1.6f, 0, 360, 32, XBg);
                Raylib.DrawCircleLines(cx, cy, radius, XBorder);

                Raylib.EndScissorMode();

                // Aeusseren Scissor wiederherstellen
                if (clipBounds is Rectangle restore)
                    Raylib.BeginScissorMode((int)restore.X, (int)restore.Y, (int)restore.Width, (int)restore.Height);
                return;
            }
        }

        Raylib.DrawCircleV(center, radius, fallbackColor);
        string sym = "!";
        int symW = MeasureTextCached(sym, radius);
        DrawGameText(sym, cx - symW / 2, cy - radius / 2, radius, Color.White);
    }

    /// <summary>
    /// Blauer Verifiziert-Haken im X-Stil
    /// </summary>
    static void DrawVerifiedBadge(int cx, int cy, int radius)
    {
        Raylib.DrawCircle(cx, cy, radius, XBlue);
        float s = radius * 0.55f;
        Raylib.DrawLineEx(new Vector2(cx - s, cy), new Vector2(cx - s * 0.2f, cy + s * 0.8f), 2f, Color.White);
        Raylib.DrawLineEx(new Vector2(cx - s * 0.2f, cy + s * 0.8f), new Vector2(cx + s, cy - s * 0.6f), 2f, Color.White);
    }

    /// <summary>
    /// X-Logo: zwei kraeftige gekreuzte Diagonalstriche
    /// </summary>
    static void DrawXLogo(int cx, int cy, int size, Color color)
    {
        float s = size;
        Raylib.DrawLineEx(new Vector2(cx - s, cy - s), new Vector2(cx + s, cy + s), 3.5f, color);
        Raylib.DrawLineEx(new Vector2(cx + s, cy - s), new Vector2(cx + s * 0.25f, cy - s * 0.15f), 2.5f, color);
        Raylib.DrawLineEx(new Vector2(cx - s * 0.25f, cy + s * 0.15f), new Vector2(cx - s, cy + s), 2.5f, color);
    }

    // === Bottom-Navigation-Icons ===

    static void DrawHouseIcon(int cx, int cy, int size, Color color)
    {
        // Dach
        Raylib.DrawLineEx(new Vector2(cx - size, cy), new Vector2(cx, cy - size), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx, cy - size), new Vector2(cx + size, cy), 2f, color);
        // Korpus
        Raylib.DrawLineEx(new Vector2(cx - size + 2, cy - 1), new Vector2(cx - size + 2, cy + size - 1), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx + size - 2, cy - 1), new Vector2(cx + size - 2, cy + size - 1), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx - size + 2, cy + size - 1), new Vector2(cx + size - 2, cy + size - 1), 2f, color);
        // Tuer
        Raylib.DrawRectangle(cx - 2, cy + size - 6, 4, 5, color);
    }

    static void DrawSearchIcon(int cx, int cy, int size, Color color)
    {
        Raylib.DrawRing(new Vector2(cx - 2, cy - 2), size - 3, size - 1, 0, 360, 24, color);
        Raylib.DrawLineEx(new Vector2(cx + size / 2 - 1, cy + size / 2 - 1), new Vector2(cx + size, cy + size), 2.5f, color);
    }

    static void DrawBellIcon(int cx, int cy, int size, Color color)
    {
        // Glockenkoerper (Bogen oben + Seiten)
        Raylib.DrawRing(new Vector2(cx, cy - 1), size - 3, size - 1, 180, 360, 24, color);
        Raylib.DrawLineEx(new Vector2(cx - size + 2, cy - 1), new Vector2(cx - size + 1, cy + size - 4), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx + size - 2, cy - 1), new Vector2(cx + size - 1, cy + size - 4), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx - size + 1, cy + size - 4), new Vector2(cx + size - 1, cy + size - 4), 2f, color);
        // Kloeppel
        Raylib.DrawCircle(cx, cy + size - 1, 2, color);
    }

    static void DrawMailIcon(int cx, int cy, int size, Color color)
    {
        int w = size + 3;
        int h = size - 1;
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(cx - w, cy - h, w * 2, h * 2), 0.2f, 4, 2, color);
        // Umschlag-Klappe
        Raylib.DrawLineEx(new Vector2(cx - w + 2, cy - h + 2), new Vector2(cx, cy + 1), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx, cy + 1), new Vector2(cx + w - 2, cy - h + 2), 2f, color);
    }

    /// <summary>
    /// Smartphone-Icon (Hochformat) fuer den Nachrichten-Button, zentriert bei (cx,cy).
    /// <paramref name="btnSize"/> = Groesse des Buttons (Icon skaliert daraus).
    /// </summary>
    static void DrawPhoneIcon(int cx, int cy, int btnSize, Color color)
    {
        float h = btnSize * 0.58f;
        float w = h * 0.54f;
        float x = cx - w / 2f;
        float y = cy - h / 2f;
        float thick = Math.Max(1.5f, btnSize * 0.05f);

        // Handy-Umriss
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(x, y, w, h), 0.30f, 8, thick, color);
        // Lautsprecher-Schlitz oben
        Raylib.DrawLineEx(new Vector2(cx - w * 0.16f, y + h * 0.14f),
            new Vector2(cx + w * 0.16f, y + h * 0.14f), Math.Max(1.2f, btnSize * 0.035f), color);
        // Home-Indikator unten
        Raylib.DrawLineEx(new Vector2(cx - w * 0.18f, y + h * 0.87f),
            new Vector2(cx + w * 0.18f, y + h * 0.87f), Math.Max(1.2f, btnSize * 0.035f), color);
    }

    /// <summary>
    /// Teilen-Icon: Pfeil nach oben aus einer Ablage
    /// </summary>
    static void DrawShareIcon(int cx, int centerY, int size, Color color)
    {
        int half = size / 2;
        // Pfeil
        Raylib.DrawLineEx(new Vector2(cx, centerY - half), new Vector2(cx, centerY + 2), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx - 4, centerY - half + 4), new Vector2(cx, centerY - half), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx + 4, centerY - half + 4), new Vector2(cx, centerY - half), 2f, color);
        // Ablage
        Raylib.DrawLineEx(new Vector2(cx - half, centerY), new Vector2(cx - half, centerY + half), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx - half, centerY + half), new Vector2(cx + half, centerY + half), 2f, color);
        Raylib.DrawLineEx(new Vector2(cx + half, centerY + half), new Vector2(cx + half, centerY), 2f, color);
    }

    /// <summary>
    /// Zeichnet ein Kommentar-Icon (Sprechblase) im X-Stil
    /// </summary>
    static void DrawCommentIcon(int x, int centerY, int size, Color color)
    {
        int r = size / 2 - 1;
        int cx = x + size / 2;
        int cy = centerY;

        Raylib.DrawEllipseLines(cx, cy - 1, r, r - 2, color);
        Raylib.DrawLine(cx - r / 2, cy + r - 3, cx - r / 2 - 3, cy + r + 2, color);
        Raylib.DrawLine(cx - r / 2 - 3, cy + r + 2, cx - r / 2 + 2, cy + r - 1, color);
    }

    /// <summary>
    /// Zeichnet ein Repost-Icon (zwei Pfeile) im X-Stil
    /// </summary>
    static void DrawRetweetIcon(int x, int centerY, int size, Color color)
    {
        int h = size / 2 - 2;
        int w = size - 4;
        int cx = x + size / 2;
        int cy = centerY;

        Raylib.DrawLine(cx - w / 2, cy - h / 2, cx + w / 2 - 3, cy - h / 2, color);
        Raylib.DrawLine(cx + w / 2 - 3, cy - h / 2, cx + w / 2 - 6, cy - h / 2 - 3, color);
        Raylib.DrawLine(cx + w / 2 - 3, cy - h / 2, cx + w / 2 - 6, cy - h / 2 + 3, color);
        Raylib.DrawLine(cx + w / 2 - 3, cy - h / 2, cx + w / 2 - 3, cy, color);

        Raylib.DrawLine(cx + w / 2, cy + h / 2, cx - w / 2 + 3, cy + h / 2, color);
        Raylib.DrawLine(cx - w / 2 + 3, cy + h / 2, cx - w / 2 + 6, cy + h / 2 - 3, color);
        Raylib.DrawLine(cx - w / 2 + 3, cy + h / 2, cx - w / 2 + 6, cy + h / 2 + 3, color);
        Raylib.DrawLine(cx - w / 2 + 3, cy + h / 2, cx - w / 2 + 3, cy, color);
    }

    /// <summary>
    /// Zeichnet ein Herz-Icon im X-Stil
    /// </summary>
    static void DrawHeartIcon(int x, int centerY, int size, Color color)
    {
        int cx = x + size / 2;
        int cy = centerY;
        int r = size / 4;

        Raylib.DrawCircleLines(cx - r + 1, cy - r / 2, r, color);
        Raylib.DrawCircleLines(cx + r - 1, cy - r / 2, r, color);
        Raylib.DrawLine(cx - size / 2 + 2, cy, cx, cy + size / 2 - 2, color);
        Raylib.DrawLine(cx + size / 2 - 2, cy, cx, cy + size / 2 - 2, color);
    }

    /// <summary>
    /// Zeichnet ein Views-Icon (Balkendiagramm) im X-Stil
    /// </summary>
    static void DrawViewsIcon(int x, int centerY, int size, Color color)
    {
        int barW = 3;
        int spacing = 2;
        int baseY = centerY + size / 2 - 3;

        int bar1H = size / 3;
        int bar2H = size / 2;
        int bar3H = size - 6;

        int startX = x + 2;

        Raylib.DrawRectangle(startX, baseY - bar1H, barW, bar1H, color);
        Raylib.DrawRectangle(startX + barW + spacing, baseY - bar2H, barW, bar2H, color);
        Raylib.DrawRectangle(startX + (barW + spacing) * 2, baseY - bar3H, barW, bar3H, color);
    }
}
