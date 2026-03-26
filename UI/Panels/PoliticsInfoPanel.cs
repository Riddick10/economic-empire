using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Panels;

/// <summary>
/// Politik-Info-Panel: Wird beim Klick auf ein Land angezeigt (links)
/// </summary>
internal class PoliticsInfoPanel
{
    // Panel-Rect fuer Klick-Blocking (wird von Update.cs abgefragt)
    public static Rectangle GetPanelRect()
    {
        int panelX = 10;
        int panelY = 80;
        int panelW = 440;
        int panelH = Program.ScreenHeight - panelY - GameConfig.BOTTOM_BAR_HEIGHT - 10;
        return new Rectangle(panelX, panelY, panelW, panelH);
    }

    public void Draw(Country country, bool isOwnCountry)
    {
        int panelX = 10;
        int panelY = 80; // Unter der Top-Bar
        int panelW = 440;
        int panelH = Program.ScreenHeight - panelY - GameConfig.BOTTOM_BAR_HEIGHT - 10;

        // Panel-Hintergrund - andere Farbe fuer fremde Laender
        Color borderColor = isOwnCountry ? ColorPalette.Accent : ColorPalette.Yellow;
        Rectangle panelRect = new(panelX, panelY, panelW, panelH);
        Rectangle panelShadow = new(panelX + 2, panelY + 2, panelW, panelH);
        Raylib.DrawRectangleRounded(panelShadow, 0.02f, 6, new Color((byte)0, (byte)0, (byte)0, (byte)40));
        Raylib.DrawRectangleRounded(panelRect, 0.02f, 6, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.02f, 6, 2, borderColor);

        int y = panelY + 15;
        int contentX = panelX + 15;

        // Flagge und Landesname
        int flagH = 30;
        Program.DrawFlag(country.Id, contentX, y, flagH);
        var flagTex = Program.GetFlagTexture(country.Id);
        int flagW = flagTex != null ? (int)(flagTex.Value.Width * ((float)flagH / flagTex.Value.Height)) : 45;
        int titleX = contentX + flagW + 12;
        Program.DrawGameText(country.Name, titleX, y + (flagH - 20) / 2, 20, isOwnCountry ? ColorPalette.Accent : ColorPalette.TextWhite);
        y += flagH + 15;

        // Trennlinie
        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, borderColor);
        y += 15;

        // === REGIERUNG ===
        Program.DrawGameText("REGIERUNG", contentX, y, 20, ColorPalette.Accent);
        y += 32;

        // Praesident/Staatsoberhaupt mit Bildrahmen
        int portraitW = 120;
        int portraitH = 145;
        int portraitX = contentX;
        int portraitY = y;

        // Bildrahmen-Hintergrund
        Raylib.DrawRectangleRounded(new Rectangle(portraitX, portraitY, portraitW, portraitH), 0.05f, 6, ColorPalette.PanelLight);

        // Leader-Portrait laden und anzeigen (basierend auf Land)
        string? leaderImageName = country.Id switch
        {
            "DEU" => "merz_friedrich",
            "RUS" => "putin_vladimir",
            "USA" => "trump_donald",
            "UKR" => "zelensky_volodymyr",
            "GBR" => "starmer_keir",
            "FRA" => "macron_emmanuel",
            "JPN" => "ishiba_shigeru",
            "CHN" => "xi_jinping",
            "IND" => "modi_narendra",
            "ITA" => "meloni_giorgia",
            "POL" => "tusk_donald",
            "BRA" => "lula_da_silva",
            "CAN" => "carney_mark",
            "ESP" => "sanchez_pedro",
            "TUR" => "erdogan_recep_tayyip",
            "KOR" => "han_duck_soo",
            "AUS" => "albanese_anthony",
            "MEX" => "sheinbaum_claudia",
            "NLD" => "schoof_dick",
            "PRK" => "kim_jong_un",
            "FIN" => "stubb_alexander",
            "NOR" => "store_jonas_gahr",
            "SWE" => "kristersson_ulf",
            "AUT" => "kickl_herbert",
            "CZE" => "fiala_petr",
            "CHE" => "keller_sutter_karin",
            "SAU" => "bin_salman_mohammed",
            "IRN" => "pezeshkian_masoud",
            "ISR" => "netanyahu_benjamin",
            "BLR" => "lukashenko_alexander",
            "KAZ" => "tokayev_kassym_jomart",
            _ => null
        };

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
            int iconCenterX = portraitX + portraitW / 2;
            int iconCenterY = portraitY + portraitH / 2 - 5;
            Raylib.DrawCircle(iconCenterX, iconCenterY - 12, 20, ColorPalette.TextGray);
            Raylib.DrawCircle(iconCenterX, iconCenterY + 30, 30, ColorPalette.TextGray);
        }

        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(portraitX, portraitY, portraitW, portraitH), 0.05f, 6, 2, ColorPalette.Accent);

        int textX = portraitX + portraitW + 15;
        int textY = portraitY;

        (string leaderName, string party) = country.Id switch
        {
            "DEU" => ("Friedrich Merz", "CDU/CSU"),
            "USA" => ("Donald Trump", "Republican"),
            "FRA" => ("Emmanuel Macron", "Ensemble"),
            "GBR" => ("Keir Starmer", "Labour"),
            "RUS" => ("Wladimir Putin", "Einiges Russland"),
            "CHN" => ("Xi Jinping", "KPCh"),
            "UKR" => ("Wolodymyr Selenskyj", "Diener des Volkes"),
            "JPN" => ("Shigeru Ishiba", "LDP"),
            "IND" => ("Narendra Modi", "BJP"),
            "ITA" => ("Giorgia Meloni", "Fratelli d'Italia"),
            "POL" => ("Donald Tusk", "Civic Coalition"),
            "BRA" => ("Lula da Silva", "PT"),
            "CAN" => ("Mark Carney", "Liberal Party"),
            "ESP" => ("Pedro Sanchez", "PSOE"),
            "TUR" => ("Recep Tayyip Erdogan", "AKP"),
            "KOR" => ("Han Duck-soo", "PPP"),
            "AUS" => ("Anthony Albanese", "Labor"),
            "MEX" => ("Claudia Sheinbaum", "MORENA"),
            "NLD" => ("Dick Schoof", "Unabhaengig"),
            "PRK" => ("Kim Jong-un", "PdAK"),
            "FIN" => ("Alexander Stubb", "Kokoomus"),
            "NOR" => ("Jonas Gahr Store", "Labour Party"),
            "SWE" => ("Ulf Kristersson", "Moderate Party"),
            "AUT" => ("Herbert Kickl", "FPOe"),
            "CZE" => ("Petr Fiala", "SPOLU"),
            "CHE" => ("Karin Keller-Sutter", "FDP"),
            "SAU" => ("Mohammed bin Salman", "Koenigshaus"),
            "IRN" => ("Masoud Pezeshkian", "Reformisten"),
            "ISR" => ("Benjamin Netanyahu", "Likud"),
            "BLR" => ("Alexander Lukashenko", "Belaya Rus"),
            "KAZ" => ("Kassym-Jomart Tokayev", "Amanat"),
            _ => ("-", "-")
        };

        string governmentType = country.Id switch
        {
            "RUS" or "BLR" or "KAZ" => "Autoritaeres Regime",
            "CHN" => "Kommunistischer Staat",
            "PRK" => "Totalitaere Diktatur",
            "GBR" or "JPN" or "ESP" or "NLD" or "CAN" or "AUS" or "NOR" or "SWE" => "Konstitutionelle Monarchie",
            "SAU" => "Absolute Monarchie",
            "IRN" => "Theokratie",
            "TUR" => "Praesidialrepublik",
            "CHE" => "Bundesrat-System",
            _ => "Demokratie"
        };

        Program.DrawGameText("Staatsoberhaupt", textX, textY, 14, ColorPalette.TextGray);
        textY += 18;
        Program.DrawGameText(leaderName, textX, textY, 20, ColorPalette.TextWhite);
        textY += 26;

        Program.DrawGameText("Regierungsform", textX, textY, 14, ColorPalette.TextGray);
        textY += 18;
        Program.DrawGameText(governmentType, textX, textY, 14, ColorPalette.TextWhite);
        textY += 24;

        Program.DrawGameText("Partei", textX, textY, 14, ColorPalette.TextGray);
        textY += 18;
        Program.DrawGameText(party, textX, textY, 14, ColorPalette.TextWhite);

        y = portraitY + portraitH + 20;

        // === PARTEIEN-BELIEBTHEIT ===
        Program.DrawGameText("PARTEIEN-BELIEBTHEIT", contentX, y, 14, ColorPalette.Accent);
        y += 26;

        var politicsManager = Program.game.GetSystem<PoliticsManager>();
        var politics = politicsManager?.GetPolitics(country.Id);
        var partyData = (politics != null && politics.Parties.Count > 0)
            ? Program.GetPartyDataFromPolitics(politics)
            : GetPartyData(country.Id);

        int pieRadius = 50;
        int pieCenterX = panelX + panelW - pieRadius - 25;
        int pieCenterY = y + pieRadius + 5;

        int legendX = contentX;
        int legendY = y;
        foreach (var (partyName, percentage, color) in partyData)
        {
            Raylib.DrawRectangle(legendX, legendY + 2, 14, 14, color);
            Program.DrawGameText($"{partyName}", legendX + 18, legendY, 14, ColorPalette.TextWhite);
            Program.DrawGameText($"{percentage:F1}%", legendX + 140, legendY, 14, ColorPalette.TextGray);
            legendY += 18;
        }

        Program.DrawPieChart(pieCenterX, pieCenterY, pieRadius, partyData);

        y = pieCenterY + pieRadius + 35;

        // === STABILITÄT ===
        Program.DrawGameText("STABILITAET", contentX, y, 20, ColorPalette.Accent);
        y += 32;

        float stability = (float)(politics?.Stability ?? 0.75);
        Program.DrawGameText("Stabilitaet:", contentX, y, 14, ColorPalette.TextGray);
        int barX = contentX + 150;
        int barW = 140;
        int barH = 18;
        Color stabColor = stability >= 0.6f ? ColorPalette.Green : stability >= 0.3f ? ColorPalette.Yellow : ColorPalette.Red;
        Raylib.DrawRectangleRounded(new Rectangle(barX, y, barW, barH), 0.3f, 4, ColorPalette.PanelLight);
        if ((int)(barW * stability) > 0)
            Raylib.DrawRectangleRounded(new Rectangle(barX, y, (int)(barW * stability), barH), 0.3f, 4, stabColor);
        Program.DrawGameText($"{(int)(stability * 100)}%", barX + barW + 10, y, 14, ColorPalette.TextWhite);
        y += 32;

        float support = (float)(politics?.PublicSupport ?? 0.60);
        Program.DrawGameText("Unterstuetzung:", contentX, y, 14, ColorPalette.TextGray);
        Color suppColor = support >= 0.6f ? ColorPalette.Green : support >= 0.3f ? ColorPalette.Yellow : ColorPalette.Red;
        Raylib.DrawRectangleRounded(new Rectangle(barX, y, barW, barH), 0.3f, 4, ColorPalette.PanelLight);
        if ((int)(barW * support) > 0)
            Raylib.DrawRectangleRounded(new Rectangle(barX, y, (int)(barW * support), barH), 0.3f, 4, suppColor);
        Program.DrawGameText($"{(int)(support * 100)}%", barX + barW + 10, y, 14, ColorPalette.TextWhite);
        y += 40;

        // === POLITISCHE WERTE ===
        Program.DrawGameText("POLITISCHE AUSRICHTUNG", contentX, y, 20, ColorPalette.Accent);
        y += 32;

        float economyAlign = (float)(politics?.EconomyAlignment ?? 0.5);
        Program.DrawGameText("Wirtschaft:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawPoliticalSlider(barX, y, barW, economyAlign);
        y += 28;

        float societyAlign = (float)(politics?.SocietyAlignment ?? 0.5);
        Program.DrawGameText("Gesellschaft:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawPoliticalSlider(barX, y, barW, societyAlign);
        y += 28;

        float freedomAlign = (float)(politics?.FreedomAlignment ?? 0.5);
        Program.DrawGameText("Freiheit:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawPoliticalSlider(barX, y, barW, freedomAlign);

        y += 45;

        // === ENTSCHEIDUNGEN (kompakt, nur fuer eigenes Land) ===
        if (isOwnCountry)
        {
            // === ENTSCHEIDUNGEN (kompakt) ===
            Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, borderColor);
            y += 10;
            Program.DrawGameText("ENTSCHEIDUNGEN", contentX, y, 14, ColorPalette.Accent);
            y += 22;

            var activeCampaign = politicsManager?.GetActiveAdvertising(country.Id);
            if (activeCampaign != null)
            {
                var campaignPartyInfo = partyData.FirstOrDefault(p => p.Name == activeCampaign.PartyName);
                Color campColor = campaignPartyInfo.Name != null ? campaignPartyInfo.Color : ColorPalette.Accent;
                Raylib.DrawRectangleRounded(new Rectangle(contentX, y + 2, 12, 12), 0.2f, 4, campColor);
                Program.DrawGameText($"{activeCampaign.PartyName} - {activeCampaign.RemainingDays} Tage", contentX + 18, y, 14, ColorPalette.TextWhite);
                y += 20;

                int miniBarW = panelW - 40;
                float campProgress = 1.0f - (float)activeCampaign.RemainingDays / activeCampaign.TotalDays;
                Raylib.DrawRectangleRounded(new Rectangle(contentX, y, miniBarW, 8), 0.4f, 4, ColorPalette.PanelLight);
                if ((int)(miniBarW * campProgress) > 0)
                    Raylib.DrawRectangleRounded(new Rectangle(contentX, y, (int)(miniBarW * campProgress), 8), 0.4f, 4, ColorPalette.Green);
            }
            else
            {
                Rectangle decisionBtnRect = new Rectangle(contentX, y, panelW - 30, 34);
                bool decisionHovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), decisionBtnRect);

                Color decBtnBg = decisionHovered ? ColorPalette.Accent : ColorPalette.PanelLight;
                Raylib.DrawRectangleRounded(decisionBtnRect, GameConfig.BUTTON_ROUNDNESS, 6, decBtnBg);
                Raylib.DrawRectangleRoundedLinesEx(decisionBtnRect, GameConfig.BUTTON_ROUNDNESS, 6, 1, ColorPalette.Accent);

                string decText = "PARTEI-WERBUNG STARTEN";
                int decTextW = Program.MeasureTextCached(decText, 14);
                Program.DrawGameText(decText, (int)(decisionBtnRect.X + (decisionBtnRect.Width - decTextW) / 2),
                    (int)(decisionBtnRect.Y + 9), 14, ColorPalette.TextWhite);

                if (decisionHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    Program.ui.ActiveTopMenuPanel = TopMenuPanel.Politics;
                    SoundManager.Play(SoundEffect.Click);
                }
            }
        }
    }

    /// <summary>
    /// Gibt die Parteien-Daten fuer ein Land zurueck (Name, Prozent, Farbe) - gecached
    /// </summary>
    private List<(string Name, float Percentage, Color Color)> GetPartyData(string countryId)
    {
        if (Program._cache.PartyData.TryGetValue(countryId, out var cached))
            return cached;

        var result = countryId switch
        {
            "DEU" => new List<(string, float, Color)>
            {
                ("CDU/CSU", 31.5f, new Color((byte)30, (byte)30, (byte)30, (byte)255)),
                ("SPD", 15.2f, new Color((byte)220, (byte)50, (byte)50, (byte)255)),
                ("Gruene", 11.8f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("AfD", 21.3f, new Color((byte)0, (byte)158, (byte)224, (byte)255)),
                ("FDP", 4.5f, new Color((byte)255, (byte)220, (byte)0, (byte)255)),
                ("Linke", 3.2f, new Color((byte)230, (byte)50, (byte)130, (byte)255)),
                ("BSW", 8.5f, new Color((byte)140, (byte)50, (byte)160, (byte)255)),
                ("Sonstige", 4.0f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "USA" => new List<(string, float, Color)>
            {
                ("Republican", 48.5f, new Color((byte)220, (byte)50, (byte)50, (byte)255)),
                ("Democrat", 46.2f, new Color((byte)50, (byte)100, (byte)200, (byte)255)),
                ("Independent", 5.3f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "GBR" => new List<(string, float, Color)>
            {
                ("Labour", 33.8f, new Color((byte)220, (byte)50, (byte)50, (byte)255)),
                ("Conservative", 23.7f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("Reform UK", 14.3f, new Color((byte)40, (byte)180, (byte)180, (byte)255)),
                ("Lib Dem", 12.2f, new Color((byte)250, (byte)180, (byte)50, (byte)255)),
                ("Green", 6.8f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("SNP", 2.5f, new Color((byte)255, (byte)240, (byte)80, (byte)255)),
                ("Other", 6.7f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "FRA" => new List<(string, float, Color)>
            {
                ("RN", 31.4f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("Renaissance", 20.8f, new Color((byte)255, (byte)220, (byte)0, (byte)255)),
                ("NFP", 28.1f, new Color((byte)220, (byte)50, (byte)80, (byte)255)),
                ("LR", 10.2f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("Autre", 9.5f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "RUS" => new List<(string, float, Color)>
            {
                ("Einiges Russland", 76.2f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("KPRF", 11.5f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("LDPR", 6.8f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("Andere", 5.5f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "CHN" => new List<(string, float, Color)>
            {
                ("KPCh", 100.0f, new Color((byte)200, (byte)30, (byte)30, (byte)255))
            },
            "JPN" => new List<(string, float, Color)>
            {
                ("LDP", 36.2f, new Color((byte)0, (byte)150, (byte)70, (byte)255)),
                ("CDP", 21.5f, new Color((byte)50, (byte)100, (byte)180, (byte)255)),
                ("Komeito", 12.8f, new Color((byte)220, (byte)50, (byte)120, (byte)255)),
                ("JCP", 7.3f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Ishin", 10.5f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("Other", 11.7f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "IND" => new List<(string, float, Color)>
            {
                ("BJP", 44.2f, new Color((byte)255, (byte)150, (byte)50, (byte)255)),
                ("INC", 26.5f, new Color((byte)50, (byte)150, (byte)200, (byte)255)),
                ("AAP", 8.3f, new Color((byte)50, (byte)100, (byte)180, (byte)255)),
                ("TMC", 5.8f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("Other", 15.2f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "UKR" => new List<(string, float, Color)>
            {
                ("Diener d. Volkes", 42.5f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("ES", 18.3f, new Color((byte)50, (byte)100, (byte)180, (byte)255)),
                ("Batkivshchyna", 12.8f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("OPFL", 8.5f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("Other", 17.9f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "POL" => new List<(string, float, Color)>
            {
                ("PiS", 42.2f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("KO", 34.1f, new Color((byte)255, (byte)150, (byte)50, (byte)255)),
                ("Trzecia Droga", 14.1f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("Lewica", 5.7f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Konfederacja", 3.9f, new Color((byte)50, (byte)50, (byte)50, (byte)255))
            },
            "ITA" => new List<(string, float, Color)>
            {
                ("FdI", 29.8f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("PD", 17.3f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("M5S", 13.0f, new Color((byte)255, (byte)220, (byte)0, (byte)255)),
                ("Lega", 16.5f, new Color((byte)0, (byte)120, (byte)50, (byte)255)),
                ("FI", 11.3f, new Color((byte)50, (byte)150, (byte)220, (byte)255)),
                ("Altri", 12.1f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "BRA" => new List<(string, float, Color)>
            {
                ("PL", 19.3f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("PT", 13.3f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Uniao Brasil", 11.5f, new Color((byte)50, (byte)100, (byte)180, (byte)255)),
                ("MDB", 8.2f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("PSD", 8.8f, new Color((byte)255, (byte)150, (byte)50, (byte)255)),
                ("Outros", 38.9f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "CAN" => new List<(string, float, Color)>
            {
                ("Liberal", 45.3f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Conservative", 35.2f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("Bloc Quebecois", 9.5f, new Color((byte)50, (byte)150, (byte)220, (byte)255)),
                ("NDP", 7.4f, new Color((byte)255, (byte)150, (byte)50, (byte)255)),
                ("Other", 2.6f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "ESP" => new List<(string, float, Color)>
            {
                ("PP", 39.1f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("PSOE", 34.6f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Vox", 9.4f, new Color((byte)100, (byte)200, (byte)50, (byte)255)),
                ("Sumar", 8.9f, new Color((byte)180, (byte)50, (byte)120, (byte)255)),
                ("Otros", 8.0f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "TUR" => new List<(string, float, Color)>
            {
                ("AKP", 44.7f, new Color((byte)255, (byte)150, (byte)50, (byte)255)),
                ("CHP", 28.2f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("MHP", 8.3f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("IYI", 7.3f, new Color((byte)50, (byte)150, (byte)220, (byte)255)),
                ("Diger", 11.5f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "KOR" => new List<(string, float, Color)>
            {
                ("PPP", 36.0f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Democratic", 58.3f, new Color((byte)50, (byte)100, (byte)180, (byte)255)),
                ("Other", 5.7f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "AUS" => new List<(string, float, Color)>
            {
                ("Labor", 51.0f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Liberal", 31.8f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("National", 10.6f, new Color((byte)0, (byte)120, (byte)50, (byte)255)),
                ("Greens", 2.6f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("Other", 4.0f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "MEX" => new List<(string, float, Color)>
            {
                ("MORENA", 51.2f, new Color((byte)150, (byte)30, (byte)30, (byte)255)),
                ("PAN", 14.4f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("PT", 10.2f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("PVEM", 11.8f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("PRI", 7.0f, new Color((byte)0, (byte)120, (byte)50, (byte)255)),
                ("MC", 5.4f, new Color((byte)255, (byte)150, (byte)50, (byte)255))
            },
            "NLD" => new List<(string, float, Color)>
            {
                ("PVV", 24.7f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("GL-PvdA", 16.7f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("VVD", 16.0f, new Color((byte)255, (byte)150, (byte)50, (byte)255)),
                ("NSC", 13.3f, new Color((byte)50, (byte)150, (byte)220, (byte)255)),
                ("D66", 6.0f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("BBB", 4.7f, new Color((byte)150, (byte)200, (byte)50, (byte)255)),
                ("Overig", 18.6f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "PRK" => new List<(string, float, Color)>
            {
                ("PdAK", 88.3f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("KSDP", 7.3f, new Color((byte)50, (byte)100, (byte)180, (byte)255)),
                ("Chondoist", 3.2f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("Unabhaengig", 1.2f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "CHE" => new List<(string, float, Color)>
            {
                ("SVP", 27.9f, new Color((byte)0, (byte)120, (byte)50, (byte)255)),
                ("SP", 18.3f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("FDP", 14.3f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("Die Mitte", 14.1f, new Color((byte)255, (byte)150, (byte)50, (byte)255)),
                ("Gruene", 9.8f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("GLP", 7.6f, new Color((byte)180, (byte)220, (byte)50, (byte)255)),
                ("Andere", 8.0f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "AUT" => new List<(string, float, Color)>
            {
                ("FPOe", 29.2f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("OeVP", 26.5f, new Color((byte)50, (byte)50, (byte)50, (byte)255)),
                ("SPOe", 21.1f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("NEOS", 9.1f, new Color((byte)230, (byte)50, (byte)130, (byte)255)),
                ("Gruene", 8.3f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("Andere", 5.8f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "FIN" => new List<(string, float, Color)>
            {
                ("Kokoomus", 24.0f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("Perussuom.", 23.0f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("SDP", 22.0f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Keskusta", 12.0f, new Color((byte)0, (byte)120, (byte)50, (byte)255)),
                ("Vihreat", 7.0f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("Vasemmisto", 6.0f, new Color((byte)230, (byte)50, (byte)130, (byte)255)),
                ("Andere", 6.0f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "NOR" => new List<(string, float, Color)>
            {
                ("Labour", 31.4f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Fremskritt", 27.8f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("Hoeyre", 14.2f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("SV", 5.3f, new Color((byte)230, (byte)50, (byte)130, (byte)255)),
                ("Senter", 5.3f, new Color((byte)0, (byte)120, (byte)50, (byte)255)),
                ("Roedt", 5.3f, new Color((byte)150, (byte)30, (byte)30, (byte)255)),
                ("MDG", 4.7f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("KrF", 4.1f, new Color((byte)255, (byte)220, (byte)0, (byte)255)),
                ("Venstre", 1.8f, new Color((byte)50, (byte)150, (byte)220, (byte)255))
            },
            "SWE" => new List<(string, float, Color)>
            {
                ("Socialdem.", 30.7f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Sverigedem.", 20.9f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("Moderaterna", 19.5f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("Vaensterp.", 6.9f, new Color((byte)150, (byte)30, (byte)30, (byte)255)),
                ("Centerp.", 6.9f, new Color((byte)0, (byte)120, (byte)50, (byte)255)),
                ("Kristdem.", 5.4f, new Color((byte)0, (byte)50, (byte)150, (byte)255)),
                ("Miljoep.", 5.2f, new Color((byte)80, (byte)180, (byte)80, (byte)255)),
                ("Liberalerna", 4.6f, new Color((byte)50, (byte)150, (byte)220, (byte)255))
            },
            "SAU" => new List<(string, float, Color)>
            {
                ("Koenigshaus", 100.0f, new Color((byte)0, (byte)120, (byte)50, (byte)255))
            },
            "IRN" => new List<(string, float, Color)>
            {
                ("Prinzipientreue", 53.4f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("Unabhaengige", 31.0f, new Color((byte)120, (byte)120, (byte)120, (byte)255)),
                ("Reformisten", 15.5f, new Color((byte)80, (byte)180, (byte)80, (byte)255))
            },
            "ISR" => new List<(string, float, Color)>
            {
                ("Likud", 26.7f, new Color((byte)0, (byte)100, (byte)180, (byte)255)),
                ("Yesh Atid", 20.0f, new Color((byte)50, (byte)150, (byte)220, (byte)255)),
                ("Relig. Zion.", 11.7f, new Color((byte)0, (byte)50, (byte)120, (byte)255)),
                ("Nat. Unity", 10.0f, new Color((byte)0, (byte)80, (byte)150, (byte)255)),
                ("Shas", 9.2f, new Color((byte)0, (byte)50, (byte)50, (byte)255)),
                ("Otzma Yeh.", 5.0f, new Color((byte)150, (byte)30, (byte)30, (byte)255)),
                ("Andere", 17.4f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "BLR" => new List<(string, float, Color)>
            {
                ("Belaya Rus", 81.8f, new Color((byte)200, (byte)30, (byte)30, (byte)255)),
                ("Unabhaengige", 18.2f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            "KAZ" => new List<(string, float, Color)>
            {
                ("Amanat", 63.3f, new Color((byte)50, (byte)150, (byte)220, (byte)255)),
                ("Auyl", 8.2f, new Color((byte)0, (byte)120, (byte)50, (byte)255)),
                ("Respublica", 6.1f, new Color((byte)255, (byte)150, (byte)50, (byte)255)),
                ("Unabhaengige", 22.4f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            },
            _ => new List<(string, float, Color)>
            {
                ("Regierung", 55.0f, new Color((byte)100, (byte)150, (byte)200, (byte)255)),
                ("Opposition", 35.0f, new Color((byte)200, (byte)100, (byte)100, (byte)255)),
                ("Sonstige", 10.0f, new Color((byte)120, (byte)120, (byte)120, (byte)255))
            }
        };

        Program._cache.PartyData[countryId] = result;
        return result;
    }
}
