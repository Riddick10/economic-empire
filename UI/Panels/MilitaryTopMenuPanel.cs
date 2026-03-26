using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

internal class MilitaryTopMenuPanel : ITopMenuPanel
{
    public string Title => "MILITAER";
    public TopMenuPanel PanelType => TopMenuPanel.Military;

    public void Draw(TopMenuContext ctx)
    {
        int y = Program.DrawTopMenuPanelHeader("MILITAER");
        var (panelX, _, panelW, panelH) = Program.GetTopMenuPanelRect();
        int contentX = panelX + 15;
        var player = Program.game.PlayerCountry!;
        var militaryManager = Program.game.GetSystem<MilitaryManager>();
        var strength = militaryManager?.GetMilitaryStrength(player.Id);
        Vector2 mousePos = Program._cachedMousePos;

        // === STREITKRAEFTE ===
        Program.DrawGameText("STREITKRAEFTE", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        if (strength != null)
        {
            int col1 = contentX;
            int col2 = contentX + 180;

            Program.DrawGameText("Aktives Personal:", col1, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"{strength.ActivePersonnel:N0}", col2, y, 14, ColorPalette.TextWhite);
            y += 20;

            Program.DrawGameText("Reservisten:", col1, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"{strength.ReservePersonnel:N0}", col2, y, 14, ColorPalette.TextWhite);
            y += 20;

            Program.DrawGameText("Panzer:", col1, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"{strength.TankCount:N0}", col2, y, 14, ColorPalette.TextWhite);
            y += 20;

            Program.DrawGameText("Flugzeuge:", col1, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"{strength.AircraftCount:N0}", col2, y, 14, ColorPalette.TextWhite);
            y += 20;

            Program.DrawGameText("Kriegsschiffe:", col1, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"{strength.NavalVessels:N0}", col2, y, 14, ColorPalette.TextWhite);
            y += 28;

            // === MILITAERBUDGET ===
            Program.DrawGameText("BUDGET", contentX, y, 14, ColorPalette.Accent);
            y += 22;

            Program.DrawGameText("Militaerbudget:", col1, y, 14, ColorPalette.TextGray);
            Program.DrawGameText($"${strength.MilitaryBudget:N0}M", col2, y, 14, ColorPalette.TextWhite);
            y += 28;

            // === MORAL & KRIEGSMUEDIGKEIT ===
            Program.DrawGameText("STATUS", contentX, y, 14, ColorPalette.Accent);
            y += 22;

            int barX = contentX + 130;
            int barW = 150;
            int barH = 16;

            Program.DrawGameText("Moral:", contentX, y, 14, ColorPalette.TextGray);
            Raylib.DrawRectangle(barX, y, barW, barH, ColorPalette.PanelLight);
            Color moraleColor = strength.Morale >= 0.6 ? ColorPalette.Green :
                                strength.Morale >= 0.3 ? ColorPalette.Yellow : ColorPalette.Red;
            Raylib.DrawRectangle(barX, y, (int)(barW * strength.Morale), barH, moraleColor);
            Program.DrawGameText($"{(int)(strength.Morale * 100)}%", barX + barW + 8, y, 14, ColorPalette.TextWhite);
            y += 24;

            Program.DrawGameText("Kriegsmuedigkeit:", contentX, y, 14, ColorPalette.TextGray);
            Raylib.DrawRectangle(barX, y, barW, barH, ColorPalette.PanelLight);
            Color exhaustionColor = strength.WarExhaustion <= 0.3 ? ColorPalette.Green :
                                    strength.WarExhaustion <= 0.6 ? ColorPalette.Yellow : ColorPalette.Red;
            Raylib.DrawRectangle(barX, y, (int)(barW * strength.WarExhaustion), barH, exhaustionColor);
            Program.DrawGameText($"{(int)(strength.WarExhaustion * 100)}%", barX + barW + 8, y, 14, ColorPalette.TextWhite);
            y += 28;
        }
        else
        {
            Program.DrawGameText("Keine Militaerdaten", contentX, y, 14, ColorPalette.TextGray);
            y += 28;
        }

        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        // === DIVISIONEN ===
        Program.DrawGameText("DIVISIONEN", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        var allUnits = militaryManager?.GetUnits(player.Id) ?? Array.Empty<MilitaryUnit>();
        Program._cache.RecruitingUnits.Clear();
        int readyCount = 0, infantryCount = 0, tankCount = 0, artilleryCount = 0;
        foreach (var u in allUnits)
        {
            if (u.Status == UnitStatus.Ready)
            {
                readyCount++;
                switch (u.Type)
                {
                    case UnitType.Infantry: infantryCount++; break;
                    case UnitType.Tank: tankCount++; break;
                    case UnitType.Artillery: artilleryCount++; break;
                }
            }
            else if (u.Status == UnitStatus.Recruiting)
            {
                Program._cache.RecruitingUnits.Add(u);
            }
        }
        var recruitingUnits = Program._cache.RecruitingUnits;

        int divCol1 = contentX;
        int divCol2 = contentX + 150;

        Program.DrawGameText("Infanterie:", divCol1, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"{infantryCount}", divCol2, y, 14, ColorPalette.TextWhite);
        y += 18;

        Program.DrawGameText("Panzer-Divisionen:", divCol1, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"{tankCount}", divCol2, y, 14, ColorPalette.TextWhite);
        y += 18;

        Program.DrawGameText("Artillerie:", divCol1, y, 14, ColorPalette.TextGray);
        Program.DrawGameText($"{artilleryCount}", divCol2, y, 14, ColorPalette.TextWhite);
        y += 22;

        if (recruitingUnits.Count > 0)
        {
            Program.DrawGameText("In Ausbildung:", contentX, y, 14, ColorPalette.Yellow);
            y += 20;

            foreach (var unit in recruitingUnits.Take(3))
            {
                float progress = unit.GetRecruitmentProgress();
                string typeName = unit.Type switch
                {
                    UnitType.Infantry => "Infanterie",
                    UnitType.Tank => "Panzer",
                    UnitType.Artillery => "Artillerie",
                    UnitType.Mechanized => "Mechanisiert",
                    UnitType.Airborne => "Fallschirmjaeger",
                    _ => "Einheit"
                };

                int progBarW = 180;
                int progBarH = 14;
                Raylib.DrawRectangle(contentX, y, progBarW, progBarH, ColorPalette.PanelLight);
                Raylib.DrawRectangle(contentX, y, (int)(progBarW * progress), progBarH, ColorPalette.Yellow);

                Program.DrawGameText($"{typeName}", contentX + progBarW + 10, y, 14, ColorPalette.TextGray);
                Program.DrawGameText($"{unit.RecruitmentDaysLeft}d", contentX + progBarW + 120, y, 14, ColorPalette.TextWhite);
                y += 18;
            }
            if (recruitingUnits.Count > 3)
            {
                Program.DrawGameText($"... und {recruitingUnits.Count - 3} weitere", contentX, y, 14, ColorPalette.TextGray);
                y += 18;
            }
        }
        else
        {
            Program.DrawGameText("Keine Einheiten in Ausbildung", contentX, y, 14, ColorPalette.TextGray);
            y += 18;
        }

        y += 8;
        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        // === REKRUTIERUNG ===
        Program.DrawGameText("REKRUTIERUNG", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        if (Program.ui.RecruitmentMode != null)
        {
            string modeName = Program.ui.RecruitmentMode switch
            {
                UnitType.Infantry => "Infanterie",
                UnitType.Tank => "Panzer-Division",
                UnitType.Artillery => "Artillerie",
                _ => "Einheit"
            };
            Program.DrawGameText($"Modus: {modeName} rekrutieren", contentX, y, 14, ColorPalette.Yellow);
            y += 18;
            Program.DrawGameText("Klicke auf eine eigene Provinz", contentX, y, 14, ColorPalette.TextGray);
            y += 24;

            int cancelBtnW = panelW - 30;
            int cancelBtnH = 28;
            Rectangle cancelRect = new Rectangle(contentX, y, cancelBtnW, cancelBtnH);
            bool cancelHovered = Raylib.CheckCollisionPointRec(mousePos, cancelRect);
            Raylib.DrawRectangleRec(cancelRect, cancelHovered ? ColorPalette.Red : ColorPalette.PanelLight);
            Raylib.DrawRectangleLinesEx(cancelRect, 1, ColorPalette.Red);
            Program.DrawGameText("Abbrechen [ESC]", contentX + 10, y + 6, 14, ColorPalette.TextWhite);

            if ((cancelHovered && Raylib.IsMouseButtonPressed(MouseButton.Left)) || Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                Program.ui.RecruitmentMode = null;
            }
            y += cancelBtnH + 8;
        }
        else
        {
            Program.DrawGameText("Waehle Einheitentyp:", contentX, y, 14, ColorPalette.TextGray);
            y += 22;

            y = Program.DrawRecruitModeButton(contentX, y, panelW - 30, UnitType.Infantry, "Infanterie", "30 Tage | $100k", mousePos, player);
            y = Program.DrawRecruitModeButton(contentX, y, panelW - 30, UnitType.Tank, "Panzer-Division", "60 Tage | $100k", mousePos, player);
            y = Program.DrawRecruitModeButton(contentX, y, panelW - 30, UnitType.Artillery, "Artillerie", "45 Tage | $100k", mousePos, player);
        }

        y += 8;
        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        // === AKTIVE KRIEGE ===
        Program.DrawGameText("AKTIVE KRIEGE", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        Program._cache.Wars.Clear();
        var warsEnum = militaryManager?.GetWars(player.Id);
        if (warsEnum != null) { foreach (var w in warsEnum) Program._cache.Wars.Add(w); }
        var wars = Program._cache.Wars;

        if (wars.Count == 0)
        {
            Program.DrawGameText("Keine aktiven Kriege", contentX, y, 14, ColorPalette.TextGray);
            y += 24;
        }
        else
        {
            foreach (var war in wars)
            {
                bool isAttacker = war.Attackers.Contains(player.Id);
                string role = isAttacker ? "Angreifer" : "Verteidiger";
                var opponents = isAttacker ? war.Defenders : war.Attackers;

                var opponentNames = new List<string>();
                foreach (var oppId in opponents)
                {
                    if (Program.game.Countries.TryGetValue(oppId, out var oppCountry))
                        opponentNames.Add(oppCountry.Name);
                    else
                        opponentNames.Add(oppId);
                }

                Program.DrawGameText($"vs {string.Join(", ", opponentNames)}", contentX, y, 14, ColorPalette.TextWhite);
                y += 18;

                Program.DrawGameText($"Rolle: {role}  |  Ziel: {war.WarGoal}", contentX, y, 14, ColorPalette.TextGray);
                y += 18;
            }
        }

        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        // === KRIEGSERKLAERUNG ===
        Program.DrawGameText("KRIEGSERKLAERUNG", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        bool hasTarget = Program.ui.SelectedCountryId != null && Program.ui.SelectedCountryId != player.Id;
        string targetName = "";
        if (hasTarget && Program.game.Countries.TryGetValue(Program.ui.SelectedCountryId!, out var targetCountry))
        {
            targetName = targetCountry.Name;
        }

        bool alreadyAtWar = hasTarget && wars.Any(w =>
            w.Attackers.Contains(Program.ui.SelectedCountryId!) || w.Defenders.Contains(Program.ui.SelectedCountryId!));

        int btnW2 = panelW - 30;
        int btnH2 = 36;
        Rectangle warBtnRect = new Rectangle(contentX, y, btnW2, btnH2);
        bool warBtnHovered = Raylib.CheckCollisionPointRec(mousePos, warBtnRect);
        bool canDeclare = hasTarget && !alreadyAtWar;

        Color warBtnBg = !canDeclare ? ColorPalette.PanelLight :
                         warBtnHovered ? new Color((byte)180, (byte)60, (byte)60, (byte)255) : ColorPalette.Red;
        Color warBtnBorder = !canDeclare ? ColorPalette.TextGray : ColorPalette.Red;
        Color warBtnText = !canDeclare ? ColorPalette.TextGray : ColorPalette.TextWhite;

        Raylib.DrawRectangleRec(warBtnRect, warBtnBg);
        Raylib.DrawRectangleLinesEx(warBtnRect, 2, warBtnBorder);

        string btnLabel = alreadyAtWar ? $"Bereits im Krieg mit {targetName}" :
                          hasTarget ? $"Krieg erklaeren: {targetName}" :
                          "Land auf Karte auswaehlen";
        Program.DrawGameText(btnLabel, contentX + 10, y + 10, 14, warBtnText);

        if (canDeclare && warBtnHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var context = Program.game.GetGameContext();
            if (context != null)
            {
                militaryManager?.DeclareWar(player.Id, Program.ui.SelectedCountryId!, "Eroberung", context);
            }
        }
    }
}
