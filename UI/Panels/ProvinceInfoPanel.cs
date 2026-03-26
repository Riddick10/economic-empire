using Raylib_cs;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.UI.Panels;

/// <summary>
/// Provinz-Info-Panel: Wird beim Klick auf eine Provinz angezeigt (rechts)
/// </summary>
internal class ProvinceInfoPanel
{
    public static Rectangle GetPanelRect()
    {
        int panelW = 380;
        int panelX = Program.ScreenWidth - panelW - 10;
        int panelY = 135;
        int panelH = 650;
        return new Rectangle(panelX, panelY, panelW, panelH);
    }

    public void Draw(Province province)
    {
        int panelW = 380;
        int panelX = Program.ScreenWidth - panelW - 10;
        int panelY = 135;
        int panelH = 650;

        Rectangle panelRect = new(panelX, panelY, panelW, panelH);
        Rectangle panelShadow = new(panelX + 2, panelY + 2, panelW, panelH);
        Raylib.DrawRectangleRounded(panelShadow, 0.02f, 6, new Color((byte)0, (byte)0, (byte)0, (byte)40));
        Raylib.DrawRectangleRounded(panelRect, 0.02f, 6, ColorPalette.Panel);
        Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.02f, 6, 2, ColorPalette.Accent);

        int y = panelY + 15;
        int contentX = panelX + 15;

        Program.DrawGameText("PROVINZ", contentX, y, 14, ColorPalette.Accent);
        y += 32;

        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.Accent);
        y += 15;

        Program.DrawGameText(province.Name, contentX, y, 20, ColorPalette.TextWhite);
        y += 32;

        Program.DrawGameText("Gehoert zu:", contentX, y, 14, ColorPalette.TextGray);
        string countryName = province.CountryId;
        if (Program.game.Countries.TryGetValue(province.CountryId, out var country))
        {
            countryName = country.Name;
        }
        Program.DrawGameText(countryName, contentX + 125, y, 14, ColorPalette.TextWhite);
        y += 28;

        Program.DrawGameText("ID:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText(province.Id, contentX + 125, y, 14, ColorPalette.TextGray);
        y += 28;

        y += 5;
        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        // === PROVINZ-STATISTIKEN ===
        Program.DrawGameText("STATISTIKEN", contentX, y, 14, ColorPalette.Accent);
        y += 28;

        float areaKm2 = province.CachedArea * 100;
        Program.DrawGameText("Flaeche:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"~{areaKm2:N0} km²", contentX + 125, y, 14, ColorPalette.TextWhite);
        y += 24;

        int localHour = GetProvinceLocalHour(province);
        Program.DrawGameText("Ortszeit:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"{localHour:D2}:00", contentX + 125, y, 14, ColorPalette.TextWhite);
        y += 24;

        var provinceData = GetProvinceData(province.Name, province.CountryId);

        Program.DrawGameText("Bevoelkerung:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText(provinceData.Population, contentX + 170, y, 14, ColorPalette.TextWhite);
        y += 24;

        Program.DrawGameText("Hauptstadt:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText(provinceData.Capital, contentX + 170, y, 14, ColorPalette.TextWhite);
        y += 24;

        Program.DrawGameText("Wirtschaft:", contentX, y, 14, ColorPalette.TextGray);
        Program.DrawGameText(provinceData.Economy, contentX + 170, y, 14, ColorPalette.TextWhite);
        y += 24;

        if (!string.IsNullOrEmpty(provinceData.GDP))
        {
            Program.DrawGameText("BIP:", contentX, y, 14, ColorPalette.TextGray);
            Program.DrawGameText(provinceData.GDP, contentX + 170, y, 14, ColorPalette.Green);
            y += 24;
        }

        y += 8;

        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        // === BODENSCHAETZE ===
        Program.DrawGameText("BODENSCHAETZE", contentX, y, 14, ColorPalette.Accent);
        y += 28;

        var undergroundResources = Program.worldMap.GetProvinceResourcesWithValues(province.Name, province.CountryId);

        int barMaxW = panelW - 30 - 24 - 80;
        int barH = 16;
        int barX = contentX + 80;

        foreach (var (resType, resName, resValue) in undergroundResources)
        {
            Color iconTint = resValue > 0f ? Color.White : new Color((byte)100, (byte)100, (byte)100, (byte)150);
            Program.DrawResourceIconTinted(resType, contentX, y - 2, 18, iconTint);

            Color nameColor = resValue > 0f ? ColorPalette.TextWhite : ColorPalette.TextDark;
            Program.DrawGameText(resName, contentX + 22, y, 11, nameColor);

            int barY = y + 2;
            Raylib.DrawRectangleRounded(new Rectangle(barX, barY, barMaxW, barH), 0.3f, 4, ColorPalette.PanelDark);

            int fillW = (int)(barMaxW * Math.Clamp(resValue, 0f, 1f));
            if (fillW > 0)
            {
                Color barColor = Program.GetHeatmapColorStatic(resValue);
                Raylib.DrawRectangleRounded(new Rectangle(barX, barY, fillW, barH), 0.3f, 4, barColor);
            }

            Raylib.DrawRectangleRoundedLinesEx(new Rectangle(barX, barY, barMaxW, barH), 0.3f, 4, 1, ColorPalette.PanelLight);

            y += 22;
        }
        y += 10;

        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        // === INDUSTRIE ===
        Program.DrawGameText("INDUSTRIE", contentX, y, 14, ColorPalette.Accent);
        y += 28;

        int civilianFactories = province.CivilianFactories;
        int militaryFactories = province.MilitaryFactories;
        int dockyards = province.Dockyards;
        int totalFactories = civilianFactories + militaryFactories + dockyards;

        if (totalFactories > 0)
        {
            if (civilianFactories > 0)
            {
                Program.DrawGameText($"Zivil-Fabriken: {civilianFactories}", contentX, y, 14, ColorPalette.TextWhite);
                y += 20;
            }
            if (militaryFactories > 0)
            {
                Program.DrawGameText($"Militaer-Fabriken: {militaryFactories}", contentX, y, 14, ColorPalette.TextWhite);
                y += 20;
            }
            if (dockyards > 0)
            {
                Program.DrawGameText($"Werften: {dockyards}", contentX, y, 14, ColorPalette.TextWhite);
                y += 20;
            }
        }
        else
        {
            Program.DrawGameText("Keine Industrie", contentX, y, 14, ColorPalette.TextGray);
        }
        y += 10;

        y = panelY + panelH - 35;
        Program.DrawGameText("[ESC] oder Klick zum Schliessen", contentX, y, 14, ColorPalette.TextGray);
    }

    private static (string Population, string Capital, string Economy, string GDP, List<string> Resources) GetProvinceData(string provinceName, string countryId)
    {
        if (countryId == "DEU")
        {
            return provinceName switch
            {
                "Baden-Württemberg" or "Baden-Wuerttemberg" => ("11,2 Mio.", "Stuttgart", "Industrie, Auto", "€574 Mrd.", new List<string> { "Automobil", "Maschinenbau", "IT" }),
                "Bayern" or "Bavaria" => ("13,4 Mio.", "Muenchen", "High-Tech, Auto", "€704 Mrd.", new List<string> { "Automobil", "Elektronik", "Luftfahrt" }),
                "Berlin" => ("3,9 Mio.", "Berlin", "Dienstleistung", "€180 Mrd.", new List<string> { "IT/Startups", "Tourismus", "Medien" }),
                "Brandenburg" => ("2,6 Mio.", "Potsdam", "Landwirtschaft", "€89 Mrd.", new List<string> { "Landwirtschaft", "Logistik", "Energie" }),
                "Bremen" => ("0,68 Mio.", "Bremen", "Hafen, Handel", "€38 Mrd.", new List<string> { "Schifffahrt", "Automobil", "Luftfahrt" }),
                "Hamburg" => ("1,9 Mio.", "Hamburg", "Hafen, Medien", "€142 Mrd.", new List<string> { "Hafen/Logistik", "Luftfahrt", "Medien" }),
                "Hessen" => ("6,4 Mio.", "Wiesbaden", "Finanzen, Pharma", "€323 Mrd.", new List<string> { "Finanzsektor", "Pharma", "Chemie" }),
                "Mecklenburg-Vorpommern" => ("1,6 Mio.", "Schwerin", "Tourismus, Agrar", "€52 Mrd.", new List<string> { "Tourismus", "Landwirtschaft", "Windenergie" }),
                "Niedersachsen" => ("8,1 Mio.", "Hannover", "Auto, Agrar", "€335 Mrd.", new List<string> { "Automobil (VW)", "Landwirtschaft", "Energie" }),
                "Nordrhein-Westfalen" or "North Rhine-Westphalia" => ("18,2 Mio.", "Duesseldorf", "Industrie, Chemie", "€788 Mrd.", new List<string> { "Chemie", "Maschinenbau", "Energie" }),
                "Rheinland-Pfalz" => ("4,2 Mio.", "Mainz", "Chemie, Wein", "€158 Mrd.", new List<string> { "Chemie (BASF)", "Weinbau", "Pharma" }),
                "Saarland" => ("1,0 Mio.", "Saarbruecken", "Stahl, Auto", "€39 Mrd.", new List<string> { "Stahlindustrie", "Automobil", "IT" }),
                "Sachsen" or "Saxony" => ("4,1 Mio.", "Dresden", "Elektronik, Auto", "€145 Mrd.", new List<string> { "Halbleiter", "Automobil", "Maschinenbau" }),
                "Sachsen-Anhalt" => ("2,2 Mio.", "Magdeburg", "Chemie, Agrar", "€71 Mrd.", new List<string> { "Chemie", "Landwirtschaft", "Logistik" }),
                "Schleswig-Holstein" => ("2,9 Mio.", "Kiel", "Tourismus, Agrar", "€107 Mrd.", new List<string> { "Windenergie", "Tourismus", "Schiffbau" }),
                "Thüringen" or "Thueringen" or "Thuringia" => ("2,1 Mio.", "Erfurt", "Industrie, Optik", "€68 Mrd.", new List<string> { "Optik (Zeiss)", "Automobil", "Maschinenbau" }),
                _ => ("Unbekannt", "Unbekannt", "Unbekannt", "", new List<string>())
            };
        }

        return ("Daten folgen", "Daten folgen", "Daten folgen", "", new List<string>());
    }

    private static int GetProvinceLocalHour(Province province)
    {
        double provinceTimezone = Program.worldMap.Timezones.GetTimezoneOffset(province.CountryId, province.Name);

        double utcHour = Program.game.Hour - Program.worldMap.TimezoneOffset;
        double localHour = utcHour + provinceTimezone;

        while (localHour < 0) localHour += 24;
        while (localHour >= 24) localHour -= 24;

        return (int)Math.Round(localHour) % 24;
    }
}
