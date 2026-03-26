using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

/// <summary>
/// Politik-Panel: Regierung, Parteien, Stabilitaet, Ausrichtung, Entscheidungen
/// </summary>
internal class PoliticsTopMenuPanel : ITopMenuPanel
{
    public string Title => "POLITIK";
    public TopMenuPanel PanelType => TopMenuPanel.Politics;

    public void Draw(TopMenuContext ctx)
    {
        int y = Program.DrawTopMenuPanelHeader("POLITIK");
        var (panelX, panelY, panelW, panelH) = Program.GetTopMenuPanelRect();
        int contentX = panelX + 15;
        var player = Program.game.PlayerCountry!;

        // Politische Daten aus PoliticsManager holen
        var politicsManager = Program.game.GetSystem<PoliticsManager>();
        var politics = politicsManager?.GetPolitics(player.Id);

        // === REGIERUNG ===
        Program.DrawGameText("REGIERUNG", contentX, y, 14, ColorPalette.Accent);
        y += 28;

        // Leader-Portrait
        int portraitW = 90;
        int portraitH = 110;
        int portraitX = contentX;
        int portraitY = y;

        // Bildrahmen-Hintergrund
        Raylib.DrawRectangle(portraitX, portraitY, portraitW, portraitH, ColorPalette.PanelLight);

        // Leader-Portrait aus PoliticsManager laden
        string? leaderImageName = politics?.HeadOfStateImageFile;

        // Falls kein Bild-Dateiname gespeichert, versuche aus dem Namen zu generieren
        if (string.IsNullOrEmpty(leaderImageName) && politics?.HeadOfState != null)
        {
            var variants = Program.GetLeaderFileNameVariants(politics.HeadOfState);
            foreach (var variant in variants)
            {
                if (Program.GetLeaderTexture(variant) != null)
                {
                    leaderImageName = variant;
                    break;
                }
            }
        }

        Texture2D? leaderTexture = leaderImageName != null ? Program.GetLeaderTexture(leaderImageName) : null;

        if (leaderTexture != null)
        {
            var tex = leaderTexture.Value;
            float scale = Math.Max((float)portraitW / tex.Width, (float)portraitH / tex.Height);
            Rectangle sourceRec = new Rectangle(
                (tex.Width - portraitW / scale) / 2,
                (tex.Height - portraitH / scale) / 2,
                portraitW / scale,
                portraitH / scale
            );
            Rectangle destRec = new Rectangle(portraitX, portraitY, portraitW, portraitH);
            Raylib.DrawTexturePro(tex, sourceRec, destRec, new Vector2(0, 0), 0, Color.White);
        }
        else
        {
            // Platzhalter-Icon
            int iconCenterX = portraitX + portraitW / 2;
            int iconCenterY = portraitY + portraitH / 2 - 5;
            Raylib.DrawCircle(iconCenterX, iconCenterY - 10, 14, ColorPalette.TextGray);
            Raylib.DrawCircle(iconCenterX, iconCenterY + 22, 14, ColorPalette.TextGray);
        }

        // Rahmen
        Raylib.DrawRectangleLinesEx(new Rectangle(portraitX, portraitY, portraitW, portraitH), 2, ColorPalette.Accent);

        // Text neben dem Bild
        int textX = portraitX + portraitW + 12;
        int textY = portraitY;

        // Leader-Daten aus PoliticsManager
        string leaderName = politics?.HeadOfState ?? politics?.HeadOfGovernment ?? "-";
        string partyName = politics?.RulingParty?.Name ?? "-";
        string governmentType = politics != null ? Program.GetGovernmentTypeName(politics.GovernmentType) : "Unbekannt";

        Program.DrawGameText("Staatsoberhaupt", textX, textY, 14, ColorPalette.TextGray);
        textY += 16;
        Program.DrawGameText(leaderName, textX, textY, 14, ColorPalette.TextWhite);
        textY += 24;

        Program.DrawGameText("Regierungsform", textX, textY, 14, ColorPalette.TextGray);
        textY += 16;
        Program.DrawGameText(governmentType, textX, textY, 14, ColorPalette.TextWhite);
        textY += 22;

        Program.DrawGameText("Partei", textX, textY, 14, ColorPalette.TextGray);
        textY += 16;
        Program.DrawGameText(partyName, textX, textY, 14, ColorPalette.TextWhite);

        y = portraitY + portraitH + 20;

        // === PARTEIEN-BELIEBTHEIT ===
        Program.DrawGameText("PARTEIEN", contentX, y, 20, ColorPalette.Accent);
        y += 24;

        // Parteien-Daten aus PoliticsManager
        var partyData = Program.GetPartyDataFromPolitics(politics);

        // Kreisdiagramm rechts
        int pieRadius = 40;
        int pieCenterX = panelX + panelW - pieRadius - 25;
        int pieCenterY = y + pieRadius;

        // Legende links (max 5 Parteien anzeigen)
        int legendY = y;
        int maxParties = Math.Min(5, partyData.Count);
        for (int i = 0; i < maxParties; i++)
        {
            var (pName, percentage, color) = partyData[i];
            Raylib.DrawRectangle(contentX, legendY + 2, 14, 12, color);
            Program.DrawGameText($"{pName}", contentX + 14, legendY, 14, ColorPalette.TextWhite);
            Program.DrawGameText($"{percentage:F0}%", contentX + 150, legendY, 14, ColorPalette.TextGray);
            legendY += 16;
        }
        if (partyData.Count > 5)
        {
            Program.DrawGameText($"+{partyData.Count - 5} weitere", contentX + 14, legendY, 14, ColorPalette.TextGray);
        }

        Program.DrawPieChart(pieCenterX, pieCenterY, pieRadius, partyData);

        y = pieCenterY + pieRadius + 20;

        // === STABILITAET ===
        Program.DrawGameText("STABILITAET", contentX, y, 20, ColorPalette.Accent);
        y += 26;

        // Stabilitaets-Balken (aus PoliticsManager)
        float stability = (float)(politics?.Stability ?? 0.7);
        Program.DrawGameText("Stabilitaet:", contentX, y, 14, ColorPalette.TextGray);
        int barX = contentX + 130;
        int barW = 150;
        int barH = 16;
        Color stabColor = stability >= 0.6f ? ColorPalette.Green : stability >= 0.3f ? ColorPalette.Yellow : ColorPalette.Red;
        Raylib.DrawRectangle(barX, y, barW, barH, ColorPalette.PanelLight);
        Raylib.DrawRectangle(barX, y, (int)(barW * stability), barH, stabColor);
        Program.DrawGameText($"{(int)(stability * 100)}%", barX + barW + 8, y, 14, ColorPalette.TextWhite);
        y += 26;

        // Unterstuetzung-Balken (aus PoliticsManager)
        float support = (float)(politics?.PublicSupport ?? 0.6);
        Program.DrawGameText("Zustimmung:", contentX, y, 14, ColorPalette.TextGray);
        Color suppColor = support >= 0.6f ? ColorPalette.Green : support >= 0.3f ? ColorPalette.Yellow : ColorPalette.Red;
        Raylib.DrawRectangle(barX, y, barW, barH, ColorPalette.PanelLight);
        Raylib.DrawRectangle(barX, y, (int)(barW * support), barH, suppColor);
        Program.DrawGameText($"{(int)(support * 100)}%", barX + barW + 8, y, 14, ColorPalette.TextWhite);
        y += 32;

        // === POLITISCHE AUSRICHTUNG ===
        Program.DrawGameText("AUSRICHTUNG", contentX, y, 20, ColorPalette.Accent);
        y += 26;

        // Ausrichtungs-Werte aus PoliticsManager
        float economyAlign = (float)(politics?.EconomyAlignment ?? 0.5);
        float societyAlign = (float)(politics?.SocietyAlignment ?? 0.5);
        float freedomAlign = (float)(politics?.FreedomAlignment ?? 0.5);

        // Wirtschaft: Links <-> Rechts
        Program.DrawGameText("Wirtschaft:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawPoliticalSlider(barX, y, barW, economyAlign);
        y += 24;

        // Gesellschaft: Progressiv <-> Konservativ
        Program.DrawGameText("Gesellschaft:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawPoliticalSlider(barX, y, barW, societyAlign);
        y += 24;

        // Freiheit: Autoritaer <-> Liberal
        Program.DrawGameText("Freiheit:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawPoliticalSlider(barX, y, barW, freedomAlign);
        y += 32;

        // === ENTSCHEIDUNGEN ===
        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.Accent);
        y += 10;
        Program.DrawGameText("ENTSCHEIDUNGEN", contentX, y, 20, ColorPalette.Accent);
        y += 28;

        // Partei-Werbung
        Program.DrawGameText("PARTEI-WERBUNG", contentX, y, 14, ColorPalette.TextGray);
        y += 22;

        var activeCampaign = politicsManager?.GetActiveAdvertising(player.Id);

        if (activeCampaign != null)
        {
            // === Aktive Kampagne anzeigen ===
            Program.DrawGameText("Aktive Kampagne:", contentX, y, 14, ColorPalette.TextWhite);
            y += 20;

            // Parteifarbe + Name
            var campaignPartyData = partyData.FirstOrDefault(p => p.Name == activeCampaign.PartyName);
            Color campaignColor = campaignPartyData.Name != null ? campaignPartyData.Color : ColorPalette.Accent;
            Raylib.DrawRectangle(contentX, y + 2, 14, 14, campaignColor);
            Program.DrawGameText($"{activeCampaign.PartyName} - Werbung", contentX + 20, y, 14, ColorPalette.TextWhite);
            y += 22;

            // Verbleibende Tage
            Program.DrawGameText($"Verbleibend: {activeCampaign.RemainingDays} Tage", contentX, y, 14, ColorPalette.TextGray);
            y += 20;

            // Fortschrittsbalken
            int progBarW = panelW - 60;
            int progBarH = 14;
            float progress = 1.0f - (float)activeCampaign.RemainingDays / activeCampaign.TotalDays;
            Raylib.DrawRectangle(contentX, y, progBarW, progBarH, ColorPalette.PanelLight);
            Raylib.DrawRectangle(contentX, y, (int)(progBarW * progress), progBarH, ColorPalette.Green);
            y += 24;

            // Abbrechen-Button
            int cancelBtnW = 180;
            int cancelBtnH = 32;
            Rectangle cancelRect = new Rectangle(contentX, y, cancelBtnW, cancelBtnH);
            bool cancelHovered = Raylib.CheckCollisionPointRec(Program._cachedMousePos, cancelRect);
            Raylib.DrawRectangleRec(cancelRect, cancelHovered ? ColorPalette.Red : ColorPalette.PanelLight);
            Raylib.DrawRectangleLinesEx(cancelRect, 1, ColorPalette.Red);
            string cancelText = "ABBRECHEN";
            int cancelTextW = Program.MeasureTextCached(cancelText, 14);
            Program.DrawGameText(cancelText, (int)(cancelRect.X + (cancelRect.Width - cancelTextW) / 2), (int)(cancelRect.Y + 8), 14, ColorPalette.TextWhite);

            if (cancelHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                politicsManager?.CancelAdvertising(player.Id);
                SoundManager.Play(SoundEffect.Click);
            }
        }
        else if (politics != null && politics.Parties.Count > 0)
        {
            // === Partei-Auswahl fuer neue Kampagne ===
            Program.DrawGameText("Waehle eine Partei:", contentX, y, 14, ColorPalette.TextWhite);
            y += 22;

            // Parteien als Buttons (2 Spalten)
            int btnW = (panelW - 40) / 2;
            int btnH = 28;
            int col = 0;
            foreach (var (pName, percentage, pColor) in partyData)
            {
                int bx = contentX + col * (btnW + 8);
                int by = y;

                Rectangle partyBtnRect = new Rectangle(bx, by, btnW, btnH);
                bool isSelected = Program.ui.SelectedAdvertisingParty == pName;
                bool isHovered = Raylib.CheckCollisionPointRec(Program._cachedMousePos, partyBtnRect);

                Color btnBg = isSelected ? ColorPalette.Accent :
                              isHovered ? ColorPalette.PanelLight : ColorPalette.Panel;
                Raylib.DrawRectangleRec(partyBtnRect, btnBg);
                Raylib.DrawRectangleLinesEx(partyBtnRect, 1, isSelected ? ColorPalette.Accent : ColorPalette.TextGray);

                // Farbquadrat + Name + Prozent
                Raylib.DrawRectangle(bx + 4, by + 7, 12, 12, pColor);
                Program.DrawGameText(pName, bx + 20, by + 5, 14, ColorPalette.TextWhite);
                string pctText = $"{percentage:F0}%";
                int pctW = Program.MeasureTextCached(pctText, 12);
                Program.DrawGameText(pctText, bx + btnW - pctW - 6, by + 7, 12, ColorPalette.TextGray);

                if (isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    Program.ui.SelectedAdvertisingParty = isSelected ? null : pName;
                    SoundManager.Play(SoundEffect.Click);
                }

                col++;
                if (col >= 2)
                {
                    col = 0;
                    y += btnH + 4;
                }
            }
            if (col != 0) y += btnH + 4; // Letzte unvollstaendige Zeile
            y += 8;

            // Kosten und Dauer
            bool canAfford = player.Budget >= PoliticsManager.AdvertisingCost;
            Color costColor = canAfford ? ColorPalette.Green : ColorPalette.Red;
            Program.DrawGameText($"Kosten: ${PoliticsManager.AdvertisingCost:N0}M", contentX, y, 14, costColor);
            Program.DrawGameText("Dauer: 180 Tage", contentX + 180, y, 14, ColorPalette.TextGray);
            y += 24;

            // Werbung starten Button
            bool hasSelection = Program.ui.SelectedAdvertisingParty != null;
            int startBtnW = panelW - 40;
            int startBtnH = 36;
            Rectangle startRect = new Rectangle(contentX, y, startBtnW, startBtnH);
            bool startHovered = hasSelection && canAfford && Raylib.CheckCollisionPointRec(Program._cachedMousePos, startRect);

            Color startBg = !hasSelection || !canAfford
                ? new Color((byte)50, (byte)50, (byte)50, (byte)255)
                : startHovered ? ColorPalette.Accent : ColorPalette.PanelLight;
            Raylib.DrawRectangleRec(startRect, startBg);
            Raylib.DrawRectangleLinesEx(startRect, 2, hasSelection && canAfford ? ColorPalette.Accent : ColorPalette.TextGray);

            string startText = "WERBUNG STARTEN";
            int startTextW = Program.MeasureTextCached(startText, 16);
            Color startTextColor = hasSelection && canAfford ? ColorPalette.TextWhite : ColorPalette.TextGray;
            Program.DrawGameText(startText, (int)(startRect.X + (startRect.Width - startTextW) / 2), (int)(startRect.Y + 9), 16, startTextColor);

            if (startHovered && Raylib.IsMouseButtonPressed(MouseButton.Left) && Program.ui.SelectedAdvertisingParty != null)
            {
                bool success = politicsManager?.StartAdvertising(player.Id, Program.ui.SelectedAdvertisingParty) ?? false;
                if (success)
                {
                    player.Budget -= PoliticsManager.AdvertisingCost;
                    SoundManager.Play(SoundEffect.Coin);

                    Program._mgr.Notif?.AddNotification(
                        "Werbekampagne gestartet",
                        $"Werbung fuer {Program.ui.SelectedAdvertisingParty} laeuft 180 Tage.",
                        NotificationType.Info,
                        player.Id
                    );

                    Program.ui.SelectedAdvertisingParty = null;
                }
            }
        }
        else
        {
            Program.DrawGameText("Keine Parteien verfuegbar", contentX, y, 14, ColorPalette.TextGray);
        }
    }
}
