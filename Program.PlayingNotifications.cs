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
/// Program - Benachrichtigungs-Popups (Smartphone/X-Twitter-Stil)
/// </summary>
partial class Program
{
    /// <summary>
    /// Zeichnet aktive Popup-Nachrichten als Smartphone mit X/Twitter-App (scrollbar)
    /// </summary>
    static void DrawNotificationPopups()
    {
        var notifMgr = _mgr.Notif;
        if (notifMgr == null) return;

        var popups = notifMgr.ActivePopups;
        if (popups.Count == 0) return;

        // Nur das neueste Popup anzeigen
        var popup = popups[0];

        // Scroll-Reset wenn neue Nachricht
        if (popup.Id != ui.LastNotificationId)
        {
            ui.NotificationScrollOffset = 0;
            ui.LastNotificationId = popup.Id;
        }

        Vector2 mousePos = _cachedMousePos;

        // === SMARTPHONE-DIMENSIONEN (angepasst fuer VCR OSD Mono) ===
        int phoneW = 360;
        int phoneH = 640;
        int phoneX = (ScreenWidth - phoneW) / 2;
        int phoneY = (ScreenHeight - phoneH) / 2;
        int bezelWidth = 12;

        // Bildschirm-Bereich innerhalb des Handys
        int screenX = phoneX + bezelWidth;
        int screenY = phoneY + bezelWidth + 30; // +30 fuer Notch-Bereich
        int screenW = phoneW - bezelWidth * 2;
        int screenH = phoneH - bezelWidth * 2 - 60; // -60 fuer Notch + Home-Button

        // Hintergrund abdunkeln
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color((byte)0, (byte)0, (byte)0, (byte)150));

        // === SMARTPHONE-RAHMEN ===
        // Aeusserer Rahmen (Silber/Grau)
        Raylib.DrawRectangleRounded(new Rectangle(phoneX - 4, phoneY - 4, phoneW + 8, phoneH + 8), 0.08f, 11,
            new Color((byte)80, (byte)80, (byte)85, (byte)255));

        // Handy-Koerper (Schwarz)
        Raylib.DrawRectangleRounded(new Rectangle(phoneX, phoneY, phoneW, phoneH), 0.07f, 11,
            new Color((byte)20, (byte)20, (byte)22, (byte)255));

        // === NOTCH (Kamera/Lautsprecher) ===
        int notchW = 120;
        int notchH = 28;
        int notchX = phoneX + (phoneW - notchW) / 2;
        int notchY = phoneY + 8;
        Raylib.DrawRectangleRounded(new Rectangle(notchX, notchY, notchW, notchH), 0.5f, 8,
            new Color((byte)10, (byte)10, (byte)12, (byte)255));

        // Kamera-Punkt
        Raylib.DrawCircle(notchX + 30, notchY + notchH / 2, 6, new Color((byte)30, (byte)30, (byte)35, (byte)255));
        Raylib.DrawCircle(notchX + 30, notchY + notchH / 2, 3, new Color((byte)20, (byte)40, (byte)60, (byte)255));

        // Lautsprecher
        Raylib.DrawRectangleRounded(new Rectangle(notchX + 50, notchY + 10, 40, 8), 0.5f, 4,
            new Color((byte)40, (byte)40, (byte)45, (byte)255));

        // === BILDSCHIRM-HINTERGRUND (X/Twitter Dark Mode) ===
        Raylib.DrawRectangle(screenX, screenY, screenW, screenH, new Color((byte)0, (byte)0, (byte)0, (byte)255));

        // Farbe je nach Typ
        Color typeColor = popup.Type switch
        {
            NotificationType.Info => new Color((byte)29, (byte)155, (byte)240, (byte)255),    // Twitter-Blau
            NotificationType.Warning => new Color((byte)255, (byte)212, (byte)0, (byte)255),  // Gelb
            NotificationType.Danger => new Color((byte)244, (byte)33, (byte)46, (byte)255),   // Rot
            NotificationType.Success => new Color((byte)0, (byte)186, (byte)124, (byte)255),  // Gruen
            _ => ColorPalette.TextGray
        };

        // === APP-HEADER (oben im Screen) ===
        int appHeaderH = 50;
        Raylib.DrawRectangle(screenX, screenY, screenW, appHeaderH, new Color((byte)0, (byte)0, (byte)0, (byte)255));
        Raylib.DrawLine(screenX, screenY + appHeaderH, screenX + screenW, screenY + appHeaderH,
            new Color((byte)47, (byte)51, (byte)54, (byte)255));

        // X-Logo (links)
        DrawGameText("X", screenX + 15, screenY + 12, 18, Color.White);

        // Titel in der Mitte
        string headerTitle = "Fuer dich";
        int headerTitleW = MeasureTextCached(headerTitle, 12);
        DrawGameText(headerTitle, screenX + (screenW - headerTitleW) / 2, screenY + 18, 12, Color.White);

        // === SCROLLBARER CONTENT-BEREICH ===
        int contentY = screenY + appHeaderH;
        int contentH = screenH - appHeaderH - 50; // -50 fuer Bottom-Nav
        int contentPadding = 15;

        // Scissor-Modus fuer Clipping
        Raylib.BeginScissorMode(screenX, contentY, screenW, contentH);

        // Scroll-Offset anwenden
        int scrollY = contentY - ui.NotificationScrollOffset;

        // === TWEET/POST KARTE ===
        int postX = screenX + contentPadding;
        int postY = scrollY + 15;
        int postW = screenW - contentPadding * 2;

        // Profilbild
        int avatarSize = 44;
        int avatarX = postX;
        int avatarY = postY;

        if (popup.RelatedCountryId != null)
        {
            var flagTex = GetFlagTexture(popup.RelatedCountryId);
            if (flagTex != null)
            {
                Raylib.DrawCircle(avatarX + avatarSize / 2, avatarY + avatarSize / 2, avatarSize / 2 + 2, typeColor);
                var tex = flagTex.Value;
                float scale = (float)(avatarSize - 4) / Math.Max(tex.Width, tex.Height);
                int drawW = (int)(tex.Width * scale);
                int drawH = (int)(tex.Height * scale);
                Rectangle src = new(0, 0, tex.Width, tex.Height);
                Rectangle dst = new(avatarX + (avatarSize - drawW) / 2, avatarY + (avatarSize - drawH) / 2, drawW, drawH);
                Raylib.DrawTexturePro(tex, src, dst, Vector2.Zero, 0, Color.White);
            }
        }
        else
        {
            Raylib.DrawCircle(avatarX + avatarSize / 2, avatarY + avatarSize / 2, avatarSize / 2, typeColor);
            string sym = popup.Type == NotificationType.Danger ? "!" : "i";
            int symW = MeasureTextCached(sym, 18);
            DrawGameText(sym, avatarX + (avatarSize - symW) / 2, avatarY + 12, 18, Color.White);
        }

        // Name und Handle
        int textX = avatarX + avatarSize + 10;
        int nameY = postY + 2;

        string displayName = popup.Type switch
        {
            NotificationType.Info => "Weltnachrichten",
            NotificationType.Warning => "WARNUNG",
            NotificationType.Danger => "EILMELDUNG",
            NotificationType.Success => "Erfolg",
            _ => "News"
        };
        DrawGameText(displayName, textX, nameY, 16, Color.White);

        // Verifiziert-Badge
        int nameW = MeasureTextCached(displayName, 16);
        Raylib.DrawCircle(textX + nameW + 10, nameY + 8, 6, typeColor);
        DrawGameText("*", textX + nameW + 7, nameY, 12, Color.White);

        // Handle
        string handle = popup.RelatedCountryId != null ? $"@{popup.RelatedCountryId}" : "@EconomicEmpire";
        DrawGameText(handle, textX, nameY + 20, 12, new Color((byte)113, (byte)118, (byte)123, (byte)255));

        // Datum
        DrawGameText(popup.DateString, textX + 110, nameY + 20, 12, new Color((byte)113, (byte)118, (byte)123, (byte)255));

        // === TITEL (gross, farbig) ===
        int titleY = postY + avatarSize + 22;
        DrawGameText(popup.Title, postX, titleY, 16, typeColor);

        // === NACHRICHTENTEXT mit Bild nach erstem Satz ===
        int msgY = titleY + 28;
        int maxMsgW = postW - 10;
        int lineHeight = 22;
        Color textColor = new Color((byte)231, (byte)233, (byte)234, (byte)255);

        // Text in ersten Satz und Rest aufteilen (wenn Bild vorhanden)
        string fullMsg = popup.Message;
        string firstSentence = "";
        string restText = "";

        if (!string.IsNullOrEmpty(popup.ImageName))
        {
            // Suche nach dem ersten Satzende (. gefolgt von Leerzeichen oder Ende)
            int dotIndex = fullMsg.IndexOf(". ");
            if (dotIndex > 0)
            {
                firstSentence = fullMsg.Substring(0, dotIndex + 1);
                restText = fullMsg.Substring(dotIndex + 2).Trim();
            }
            else
            {
                firstSentence = fullMsg;
                restText = "";
            }
        }
        else
        {
            // Kein Bild - gesamten Text anzeigen
            firstSentence = fullMsg;
            restText = "";
        }

        // Ersten Satz umbrechen und zeichnen
        List<string> firstLines = WrapText(firstSentence, maxMsgW, 15);
        for (int i = 0; i < firstLines.Count; i++)
        {
            DrawGameText(firstLines[i], postX, msgY + i * lineHeight, 15, textColor);
        }

        int currentY = msgY + firstLines.Count * lineHeight;

        // === BILD (nach erstem Satz, wenn vorhanden) ===
        if (!string.IsNullOrEmpty(popup.ImageName))
        {
            var newsImage = LoadNewsImage(popup.ImageName);
            if (newsImage.HasValue && newsImage.Value.Id != 0)
            {
                int imgY = currentY + 15;
                int imgMaxW = postW;
                int imgMaxH = 180;

                var tex = newsImage.Value;
                float scale = Math.Min((float)imgMaxW / tex.Width, (float)imgMaxH / tex.Height);
                int drawW = (int)(tex.Width * scale);
                int drawH = (int)(tex.Height * scale);
                int imgX = postX + (postW - drawW) / 2;

                // Abgerundeter Rahmen
                Raylib.DrawRectangleRounded(new Rectangle(imgX - 2, imgY - 2, drawW + 4, drawH + 4), 0.05f, 8,
                    new Color((byte)47, (byte)51, (byte)54, (byte)255));

                // Bild zeichnen
                Rectangle src = new(0, 0, tex.Width, tex.Height);
                Rectangle dst = new(imgX, imgY, drawW, drawH);
                Raylib.DrawTexturePro(tex, src, dst, System.Numerics.Vector2.Zero, 0, Color.White);

                currentY = imgY + drawH + 15;
            }
        }

        // === REST DES TEXTES (nach dem Bild) ===
        if (!string.IsNullOrEmpty(restText))
        {
            List<string> restLines = WrapText(restText, maxMsgW, 15);
            for (int i = 0; i < restLines.Count; i++)
            {
                DrawGameText(restLines[i], postX, currentY + i * lineHeight, 15, textColor);
            }
            currentY += restLines.Count * lineHeight;
        }

        int imageEndY = currentY;

        // === INTERAKTIONS-LEISTE ===
        int interactionY = imageEndY + 25;
        Color iconColor = new Color((byte)113, (byte)118, (byte)123, (byte)255);
        int iconSize = 18;
        int iconCenterY = interactionY + iconSize / 2;

        // Kommentar-Icon (Sprechblase)
        int commentX = postX;
        DrawCommentIcon(commentX, iconCenterY, iconSize, iconColor);
        DrawGameText("12", commentX + iconSize + 6, interactionY + 2, 12, iconColor);

        // Retweet-Icon (zwei Pfeile)
        int retweetX = postX + 75;
        DrawRetweetIcon(retweetX, iconCenterY, iconSize, iconColor);
        DrawGameText("48", retweetX + iconSize + 6, interactionY + 2, 12, iconColor);

        // Heart-Icon (Herz)
        int heartX = postX + 150;
        DrawHeartIcon(heartX, iconCenterY, iconSize, iconColor);
        int likes = popup.Id * 234 + 156;
        DrawGameText($"{likes}", heartX + iconSize + 6, interactionY + 2, 12, iconColor);

        // Views-Icon (Balkendiagramm)
        int viewsX = postX + 230;
        DrawViewsIcon(viewsX, iconCenterY, iconSize, iconColor);
        int views = popup.Id * 1234 + 5000;
        DrawGameText($"{views:N0}", viewsX + iconSize + 6, interactionY + 2, 12, iconColor);

        // Trennlinie
        int separatorY = interactionY + 35;
        Raylib.DrawLine(screenX, separatorY, screenX + screenW, separatorY, new Color((byte)47, (byte)51, (byte)54, (byte)255));

        // Gesamt-Content-Hoehe fuer Scroll-Berechnung
        int totalContentH = separatorY - scrollY + 20;
        int maxScroll = Math.Max(0, totalContentH - contentH);

        Raylib.EndScissorMode();

        // === SCROLL-INDIKATOR (rechts) ===
        if (maxScroll > 0)
        {
            int scrollBarH = contentH - 20;
            int scrollThumbH = Math.Max(30, scrollBarH * contentH / totalContentH);
            int scrollThumbY = contentY + 10 + (int)((scrollBarH - scrollThumbH) * ((float)ui.NotificationScrollOffset / maxScroll));

            Raylib.DrawRectangleRounded(new Rectangle(screenX + screenW - 6, scrollThumbY, 4, scrollThumbH), 0.5f, 4,
                new Color((byte)100, (byte)100, (byte)110, (byte)150));
        }

        // === BOTTOM NAVIGATION BAR ===
        int bottomNavY = screenY + screenH - 50;
        Raylib.DrawRectangle(screenX, bottomNavY, screenW, 50, new Color((byte)0, (byte)0, (byte)0, (byte)255));
        Raylib.DrawLine(screenX, bottomNavY, screenX + screenW, bottomNavY, new Color((byte)47, (byte)51, (byte)54, (byte)255));

        // Nav-Icons (vereinfacht)
        int navIconSpacing = screenW / 5;
        Color navColor = new Color((byte)200, (byte)200, (byte)200, (byte)255);
        DrawGameText("@", screenX + navIconSpacing * 0 + 28, bottomNavY + 16, 12, navColor);
        DrawGameText("O", screenX + navIconSpacing * 1 + 28, bottomNavY + 16, 12, navColor);
        DrawGameText("+", screenX + navIconSpacing * 2 + 28, bottomNavY + 16, 12, navColor);
        DrawGameText("#", screenX + navIconSpacing * 3 + 28, bottomNavY + 16, 12, navColor);
        DrawGameText("*", screenX + navIconSpacing * 4 + 28, bottomNavY + 16, 12, navColor);

        // === HOME-BUTTON/INDICATOR (unten am Handy) ===
        int homeBarW = 120;
        int homeBarH = 5;
        int homeBarX = phoneX + (phoneW - homeBarW) / 2;
        int homeBarY = phoneY + phoneH - 20;
        Raylib.DrawRectangleRounded(new Rectangle(homeBarX, homeBarY, homeBarW, homeBarH), 0.5f, 4,
            new Color((byte)180, (byte)180, (byte)180, (byte)255));

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

        // === SCROLL-HINWEIS ===
        if (maxScroll > 0 && ui.NotificationScrollOffset < maxScroll)
        {
            string scrollHint = "Scrollen zum Weiterlesen";
            int hintW = MeasureTextCached(scrollHint, 12);
            DrawGameText(scrollHint, phoneX + (phoneW - hintW) / 2, phoneY + phoneH + 10, 12, ColorPalette.TextGray);

            // Pfeil nach unten
            Raylib.DrawTriangle(
                new Vector2(phoneX + phoneW / 2, phoneY + phoneH + 35),
                new Vector2(phoneX + phoneW / 2 - 8, phoneY + phoneH + 28),
                new Vector2(phoneX + phoneW / 2 + 8, phoneY + phoneH + 28),
                ColorPalette.TextGray
            );
        }

        // === INPUT-HANDLING ===
        // Mausrad zum Scrollen (nur wenn Maus ueber Handy-Screen)
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

        // Pfeiltasten zum Scrollen
        if (Raylib.IsKeyDown(KeyboardKey.Down) || Raylib.IsKeyDown(KeyboardKey.S))
        {
            ui.NotificationScrollOffset = Math.Min(ui.NotificationScrollOffset + 5, maxScroll);
        }
        if (Raylib.IsKeyDown(KeyboardKey.Up) || Raylib.IsKeyDown(KeyboardKey.W))
        {
            ui.NotificationScrollOffset = Math.Max(ui.NotificationScrollOffset - 5, 0);
        }

        // Schliessen
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && closeHover)
        {
            notifMgr.DismissPopup(popup.Id);
            ui.NotificationScrollOffset = 0;
            SoundManager.Play(SoundEffect.Click);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            notifMgr.DismissPopup(popup.Id);
            ui.NotificationScrollOffset = 0;
        }
    }

    /// <summary>
    /// Zeichnet ein Kommentar-Icon (Sprechblase) im X/Twitter-Stil
    /// </summary>
    static void DrawCommentIcon(int x, int centerY, int size, Color color)
    {
        int r = size / 2 - 1;
        int cx = x + size / 2;
        int cy = centerY;

        // Sprechblase als Ellipse
        Raylib.DrawEllipseLines(cx, cy - 1, r, r - 2, color);

        // Kleiner Pfeil unten links
        Raylib.DrawLine(cx - r / 2, cy + r - 3, cx - r / 2 - 3, cy + r + 2, color);
        Raylib.DrawLine(cx - r / 2 - 3, cy + r + 2, cx - r / 2 + 2, cy + r - 1, color);
    }

    /// <summary>
    /// Zeichnet ein Retweet-Icon (zwei Pfeile) im X/Twitter-Stil
    /// </summary>
    static void DrawRetweetIcon(int x, int centerY, int size, Color color)
    {
        int h = size / 2 - 2;
        int w = size - 4;
        int cx = x + size / 2;
        int cy = centerY;

        // Oberer Pfeil (nach rechts)
        Raylib.DrawLine(cx - w / 2, cy - h / 2, cx + w / 2 - 3, cy - h / 2, color);
        Raylib.DrawLine(cx + w / 2 - 3, cy - h / 2, cx + w / 2 - 6, cy - h / 2 - 3, color);
        Raylib.DrawLine(cx + w / 2 - 3, cy - h / 2, cx + w / 2 - 6, cy - h / 2 + 3, color);

        // Verbindung rechts
        Raylib.DrawLine(cx + w / 2 - 3, cy - h / 2, cx + w / 2 - 3, cy, color);

        // Unterer Pfeil (nach links)
        Raylib.DrawLine(cx + w / 2, cy + h / 2, cx - w / 2 + 3, cy + h / 2, color);
        Raylib.DrawLine(cx - w / 2 + 3, cy + h / 2, cx - w / 2 + 6, cy + h / 2 - 3, color);
        Raylib.DrawLine(cx - w / 2 + 3, cy + h / 2, cx - w / 2 + 6, cy + h / 2 + 3, color);

        // Verbindung links
        Raylib.DrawLine(cx - w / 2 + 3, cy + h / 2, cx - w / 2 + 3, cy, color);
    }

    /// <summary>
    /// Zeichnet ein Herz-Icon im X/Twitter-Stil
    /// </summary>
    static void DrawHeartIcon(int x, int centerY, int size, Color color)
    {
        int cx = x + size / 2;
        int cy = centerY;
        int r = size / 4;

        // Zwei Kreise oben
        Raylib.DrawCircleLines(cx - r + 1, cy - r / 2, r, color);
        Raylib.DrawCircleLines(cx + r - 1, cy - r / 2, r, color);

        // Spitze unten (Dreieck)
        Raylib.DrawLine(cx - size / 2 + 2, cy, cx, cy + size / 2 - 2, color);
        Raylib.DrawLine(cx + size / 2 - 2, cy, cx, cy + size / 2 - 2, color);
    }

    /// <summary>
    /// Zeichnet ein Views-Icon (Balkendiagramm) im X/Twitter-Stil
    /// </summary>
    static void DrawViewsIcon(int x, int centerY, int size, Color color)
    {
        int barW = 3;
        int spacing = 2;
        int baseY = centerY + size / 2 - 3;

        // Drei Balken unterschiedlicher Hoehe
        int bar1H = size / 3;
        int bar2H = size / 2;
        int bar3H = size - 6;

        int startX = x + 2;

        Raylib.DrawRectangle(startX, baseY - bar1H, barW, bar1H, color);
        Raylib.DrawRectangle(startX + barW + spacing, baseY - bar2H, barW, bar2H, color);
        Raylib.DrawRectangle(startX + (barW + spacing) * 2, baseY - bar3H, barW, bar3H, color);
    }
}
