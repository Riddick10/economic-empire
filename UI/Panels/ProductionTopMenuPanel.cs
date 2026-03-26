using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Models;
using GrandStrategyGame.Map;
using GrandStrategyGame.Systems.Managers;

namespace GrandStrategyGame.UI.Panels;

internal class ProductionTopMenuPanel : ITopMenuPanel
{
    public string Title => "PRODUKTION";
    public TopMenuPanel PanelType => TopMenuPanel.Production;

    public void Draw(TopMenuContext ctx)
    {
        var (panelX, panelY, panelW, panelH) = Program.GetTopMenuPanelRect();

        // Panel-Hintergrund
        Raylib.DrawRectangle(panelX, panelY, panelW, panelH, ColorPalette.Panel);
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelW, panelH), 2, ColorPalette.Accent);

        // Header (fest, nicht scrollend)
        int headerY = panelY + 15;
        int contentX = panelX + 15;
        Program.DrawGameText("PRODUKTION", contentX, headerY, 30, ColorPalette.Accent);
        headerY += 38;
        Raylib.DrawLine(contentX, headerY, panelX + panelW - 15, headerY, ColorPalette.Accent);
        headerY += 10;

        // Scrollbarer Bereich
        int scrollAreaY = headerY;
        int scrollAreaH = panelH - (scrollAreaY - panelY) - 10;
        int scrollBarWidth = 12;
        int contentWidth = panelW - 30 - scrollBarWidth;

        Vector2 mousePos = Program._cachedMousePos;
        var player = Program.game.PlayerCountry!;

        int totalContentHeight = 800;

        int maxScroll = Math.Max(0, totalContentHeight - scrollAreaH);
        Rectangle scrollAreaRect = new Rectangle(panelX, scrollAreaY, panelW, scrollAreaH);
        bool mouseOverPanel = Raylib.CheckCollisionPointRec(mousePos, scrollAreaRect);

        if (mouseOverPanel)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                Program.ui.ProductionScrollOffset -= (int)(wheel * 40);
                Program.ui.ProductionScrollOffset = Math.Clamp(Program.ui.ProductionScrollOffset, 0, maxScroll);
            }
        }

        Raylib.BeginScissorMode(panelX, scrollAreaY, panelW - scrollBarWidth, scrollAreaH);

        int y = scrollAreaY - Program.ui.ProductionScrollOffset;

        var productionManager = Program.game.GetSystem<ProductionManager>();
        var industryData = productionManager?.GetIndustryData(player.Id);

        Program.DrawGameText("INDUSTRIE", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        if (industryData != null)
        {
            int col1 = contentX;
            int col2 = contentX + 110;
            int col3 = contentX + 220;

            Program.DrawGameText("Zivil", col1, y, 14, ColorPalette.TextGray);
            Program.DrawGameText("Militaer", col2, y, 14, ColorPalette.TextGray);
            Program.DrawGameText("Werften", col3, y, 14, ColorPalette.TextGray);
            y += 16;

            Program.DrawGameText($"{industryData.CivilianFactories}", col1, y, 20, ColorPalette.TextWhite);
            Program.DrawGameText($"{industryData.MilitaryFactories}", col2, y, 20, ColorPalette.TextWhite);
            Program.DrawGameText($"{industryData.Dockyards}", col3, y, 20, ColorPalette.TextWhite);
            y += 24;

            Program.DrawGameText("Effizienz:", contentX, y, 14, ColorPalette.TextGray);
            int effBarX = contentX + 100;
            int effBarW = 100;
            int effBarH = 12;
            double efficiency = industryData.IndustrialEfficiency;

            Raylib.DrawRectangle(effBarX, y, effBarW, effBarH, ColorPalette.PanelLight);
            Raylib.DrawRectangle(effBarX, y, (int)(effBarW * efficiency), effBarH, ColorPalette.Green);
            Program.DrawGameText($"{(int)(efficiency * 100)}%", effBarX + effBarW + 8, y, 14, ColorPalette.TextWhite);
            y += 20;

            int queueCount = industryData.ProductionQueue.Count;
            Program.DrawGameText($"Auftraege: {queueCount}", contentX, y, 14, ColorPalette.TextGray);
            y += 18;
        }
        else
        {
            Program.DrawGameText("Keine Industriedaten", contentX, y, 14, ColorPalette.TextGray);
            y += 20;
        }

        y += 8;

        // === MINEN-UEBERSICHT ===
        Program.DrawGameText("MINEN", contentX, y, 14, ColorPalette.Accent);
        y += 22;

        var mineCounts = new Dictionary<MineType, int>();
        foreach (MineType mt in Enum.GetValues<MineType>())
            mineCounts[mt] = 0;

        foreach (var province in Program.worldMap.Provinces.Values)
        {
            if (province.CountryId != player.Id) continue;
            foreach (var mine in province.Mines)
                mineCounts[mine.Type]++;
        }

        int totalMines = mineCounts.Values.Sum();

        if (totalMines > 0)
        {
            var mineDisplayData = new (MineType Type, string Name)[]
            {
                (MineType.OilWell, "Oel"),
                (MineType.GasDrill, "Gas"),
                (MineType.CoalMine, "Kohle"),
                (MineType.IronMine, "Eisen"),
                (MineType.CopperMine, "Kupfer"),
                (MineType.UraniumMine, "Uran")
            };

            int mCol1 = contentX;
            int mCol2 = contentX + 120;
            int mCol3 = contentX + 240;

            for (int i = 0; i < mineDisplayData.Length; i++)
            {
                var (mType, mName) = mineDisplayData[i];
                int count = mineCounts[mType];
                int col = i % 3;
                int mX = col == 0 ? mCol1 : col == 1 ? mCol2 : mCol3;

                Color mColor = count > 0 ? ColorPalette.TextWhite : ColorPalette.TextGray;
                Program.DrawGameText($"{mName}: {count}", mX, y, 14, mColor);

                if (col == 2 || i == mineDisplayData.Length - 1)
                    y += 16;
            }

            y += 4;
            Program.DrawGameText($"Gesamt: {totalMines} Minen", contentX, y, 14, ColorPalette.TextGray);
            y += 18;
        }
        else
        {
            Program.DrawGameText("Keine Minen gebaut", contentX, y, 14, ColorPalette.TextGray);
            y += 18;
        }

        y += 8;

        // === BAU-PANEL TOGGLE ===
        {
            string toggleLabel = Program.ui.ShowBuildPanel ? "<< Bauen" : "Bauen >>";
            int toggleW = panelW - 30;
            int toggleH = 30;
            Rectangle toggleRect = new Rectangle(contentX, y, toggleW, toggleH);
            bool toggleHovered = Raylib.CheckCollisionPointRec(mousePos, toggleRect);

            Color toggleBg = toggleHovered ? ColorPalette.Accent : (Program.ui.ShowBuildPanel ? ColorPalette.PanelLight : ColorPalette.Panel);
            Raylib.DrawRectangleRec(toggleRect, toggleBg);
            Raylib.DrawRectangleLinesEx(toggleRect, 1, ColorPalette.Accent);

            int toggleTextW = Program.MeasureTextCached(toggleLabel, 14);
            Program.DrawGameText(toggleLabel, contentX + (toggleW - toggleTextW) / 2, y + 8, 14, ColorPalette.TextWhite);

            if (toggleHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                Program.ui.ShowBuildPanel = !Program.ui.ShowBuildPanel;
                SoundManager.Play(SoundEffect.Click);
            }
        }
        y += 38;

        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        // Ressourcen-Namen Mapping
        string GetResName(ResourceType type) => type switch
        {
            ResourceType.Oil => "Oel",
            ResourceType.NaturalGas => "Erdgas",
            ResourceType.Coal => "Kohle",
            ResourceType.Iron => "Eisen",
            ResourceType.Copper => "Kupfer",
            ResourceType.Uranium => "Uran",
            ResourceType.Food => "Nahrung",
            ResourceType.Steel => "Stahl",
            ResourceType.Electronics => "Elektronik",
            ResourceType.Machinery => "Maschinen",
            ResourceType.ConsumerGoods => "Konsumgueter",
            ResourceType.Weapons => "Waffen",
            ResourceType.Ammunition => "Munition",
            _ => type.ToString()
        };

        int diagramX = contentX;
        int nodeW = 100;
        int nodeH = 55;
        int arrowLen = 15;
        int rowSpacing = 65;

        void DrawRecipeRows(Dictionary<ResourceType, ProductionRecipe> recipes,
            Dictionary<ResourceType, int> assignments, ref int ry)
        {
            foreach (var (outputType, recipe) in recipes)
            {
                int assigned = assignments.TryGetValue(outputType, out var a) ? a : 0;
                int productionBlocks = assigned / 10;
                bool isActive = productionBlocks > 0;
                int rowY = ry;

                int inputX = diagramX;

                if (recipe.Inputs.Length == 0)
                {
                    int noInputY = rowY + nodeH / 2 - 7;
                    Program.DrawGameText("---", inputX + 25, noInputY, 14, ColorPalette.TextGray);
                }
                else
                {
                    int inputBlockH = recipe.Inputs.Length * 24;
                    int inputStartY = rowY + (nodeH - inputBlockH) / 2;

                    for (int i = 0; i < recipe.Inputs.Length; i++)
                    {
                        var (inputType, inputAmount) = recipe.Inputs[i];
                        double totalInput = inputAmount * productionBlocks;
                        int iy = inputStartY + i * 24;
                        double stock = player.GetResource(inputType);
                        bool hasEnough = assigned == 0 || stock >= inputAmount;

                        Color inputBg = hasEnough ? new Color((byte)30, (byte)50, (byte)30, (byte)255) : new Color((byte)50, (byte)30, (byte)30, (byte)255);
                        Color inputBorder = hasEnough ? ColorPalette.Green : ColorPalette.Red;
                        Raylib.DrawRectangle(inputX, iy, 70, 20, inputBg);
                        Raylib.DrawRectangleLines(inputX, iy, 70, 20, inputBorder);

                        Program.DrawResourceIcon(inputType, inputX + 2, iy + 2, 16);
                        Program.DrawGameText($"{totalInput:F1}", inputX + 22, iy + 3, 14, ColorPalette.TextWhite);
                    }
                }

                int arrowStartX = inputX + 73;
                int arrowY = rowY + nodeH / 2;
                Color arrowColor = isActive ? ColorPalette.Yellow : ColorPalette.TextGray;

                Raylib.DrawLine(arrowStartX, arrowY, arrowStartX + arrowLen, arrowY, arrowColor);
                Raylib.DrawTriangle(
                    new Vector2(arrowStartX + arrowLen + 6, arrowY),
                    new Vector2(arrowStartX + arrowLen, arrowY - 4),
                    new Vector2(arrowStartX + arrowLen, arrowY + 4),
                    arrowColor);

                int factoryX = arrowStartX + arrowLen + 6;
                int factoryW = 70;
                Color factoryBg = isActive ? new Color((byte)50, (byte)50, (byte)20, (byte)255) : ColorPalette.PanelDark;
                Color factoryBorder = isActive ? ColorPalette.Yellow : ColorPalette.PanelLight;

                Rectangle factoryRect = new Rectangle(factoryX, rowY, factoryW, nodeH);
                Raylib.DrawRectangleRec(factoryRect, factoryBg);
                Raylib.DrawRectangleLinesEx(factoryRect, 2, factoryBorder);

                int gearX = factoryX + factoryW / 2;
                int gearY = rowY + 20;
                Raylib.DrawCircle(gearX, gearY, 8, factoryBorder);
                Raylib.DrawCircle(gearX, gearY, 4, factoryBg);

                int ctrlY = rowY + nodeH - 20;

                Rectangle minusRect = new Rectangle(factoryX + 5, ctrlY, 16, 16);
                bool minusHover = Raylib.CheckCollisionPointRec(mousePos, minusRect);
                Raylib.DrawRectangleRec(minusRect, minusHover ? ColorPalette.Red : ColorPalette.PanelLight);
                Program.DrawGameText("-", (int)minusRect.X + 4, ctrlY + 1, 14, Color.White);
                if (minusHover && Raylib.IsMouseButtonPressed(MouseButton.Left) && assigned >= 10)
                {
                    productionManager?.SetFactoryAssignment(player.Id, outputType, assigned - 10);
                    SoundManager.Play(SoundEffect.Click);
                }

                string countStr = $"{assigned}";
                int countW2 = Program.MeasureTextCached(countStr, 14);
                Program.DrawGameText(countStr, factoryX + factoryW / 2 - countW2 / 2, ctrlY + 1, 14, Color.White);

                Rectangle plusRect = new Rectangle(factoryX + factoryW - 21, ctrlY, 16, 16);
                bool plusHover = Raylib.CheckCollisionPointRec(mousePos, plusRect);
                Raylib.DrawRectangleRec(plusRect, plusHover ? ColorPalette.Green : ColorPalette.PanelLight);
                Program.DrawGameText("+", (int)plusRect.X + 4, ctrlY + 1, 14, Color.White);
                if (plusHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    productionManager?.SetFactoryAssignment(player.Id, outputType, assigned + 10);
                    SoundManager.Play(SoundEffect.Click);
                }

                int arrow2StartX = factoryX + factoryW + 3;
                Raylib.DrawLine(arrow2StartX, arrowY, arrow2StartX + arrowLen, arrowY, arrowColor);
                Raylib.DrawTriangle(
                    new Vector2(arrow2StartX + arrowLen + 6, arrowY),
                    new Vector2(arrow2StartX + arrowLen, arrowY - 4),
                    new Vector2(arrow2StartX + arrowLen, arrowY + 4),
                    arrowColor);

                int outputX = arrow2StartX + arrowLen + 8;
                Color outputBg = isActive ? new Color((byte)30, (byte)40, (byte)60, (byte)255) : ColorPalette.PanelDark;
                Color outputBorder = isActive ? ColorPalette.Accent : ColorPalette.PanelLight;

                Raylib.DrawRectangle(outputX, rowY + 3, nodeW, nodeH - 6, outputBg);
                Raylib.DrawRectangleLines(outputX, rowY + 3, nodeW, nodeH - 6, outputBorder);

                Program.DrawResourceIcon(outputType, outputX + 5, rowY + 10, 20);
                Program.DrawGameText(GetResName(outputType), outputX + 28, rowY + 12, 14, ColorPalette.TextWhite);

                double eff = industryData?.IndustrialEfficiency ?? 0.8;
                double totalOutput = productionBlocks * recipe.OutputAmount * eff;
                string outputAmountStr = $"+{totalOutput:F1}/Tag";
                Color outputAmountColor = isActive ? ColorPalette.Green : ColorPalette.TextGray;
                Program.DrawGameText(outputAmountStr, outputX + 5, rowY + 32, 14, outputAmountColor);

                ry += rowSpacing;
            }
        }

        Program.DrawGameText("ZIVILE PRODUKTION", contentX, y, 14, ColorPalette.Accent);
        y += 5;

        var civRecipes = ProductionManager.GetCivilianRecipes();
        var civAssignments = productionManager?.GetFactoryAssignments(player.Id) ?? new Dictionary<ResourceType, int>();
        int civTotalAssigned = civAssignments.Values.Sum();
        int civAvailable = industryData?.CivilianFactories ?? 0;
        bool civOver = civTotalAssigned > civAvailable;

        Color civCountColor = civOver ? ColorPalette.Red : ColorPalette.Green;
        string civCountText = $"{civTotalAssigned}/{civAvailable}";
        int civCountW = Program.MeasureTextCached(civCountText, 14);
        Program.DrawGameText(civCountText, panelX + panelW - 20 - civCountW, y, 14, civCountColor);
        y += 22;

        DrawRecipeRows(new Dictionary<ResourceType, ProductionRecipe>(civRecipes), civAssignments, ref y);

        Raylib.DrawLine(contentX, y, panelX + panelW - 15, y, ColorPalette.PanelLight);
        y += 15;

        Program.DrawGameText("MILITAER. PRODUKTION", contentX, y, 14, ColorPalette.Red);
        y += 5;

        var milRecipes = ProductionManager.GetMilitaryRecipes();
        var milAssignments = productionManager?.GetMilitaryAssignments(player.Id) ?? new Dictionary<ResourceType, int>();
        int milTotalAssigned = milAssignments.Values.Sum();
        int milAvailable = industryData?.MilitaryFactories ?? 0;
        bool milOver = milTotalAssigned > milAvailable;

        Color milCountColor = milOver ? ColorPalette.Red : ColorPalette.Green;
        string milCountText = $"{milTotalAssigned}/{milAvailable}";
        int milCountW = Program.MeasureTextCached(milCountText, 14);
        Program.DrawGameText(milCountText, panelX + panelW - 20 - milCountW, y, 14, milCountColor);
        y += 22;

        DrawRecipeRows(new Dictionary<ResourceType, ProductionRecipe>(milRecipes), milAssignments, ref y);

        y += 15;

        Raylib.EndScissorMode();

        if (maxScroll > 0)
        {
            int scrollBarX = panelX + panelW - scrollBarWidth - 2;
            int scrollBarH = scrollAreaH - 4;

            Raylib.DrawRectangle(scrollBarX, scrollAreaY + 2, scrollBarWidth - 2, scrollBarH, ColorPalette.PanelDark);

            int thumbH = Math.Max(30, (int)((float)scrollAreaH / totalContentHeight * scrollBarH));
            int thumbY = scrollAreaY + 2 + (int)((float)Program.ui.ProductionScrollOffset / maxScroll * (scrollBarH - thumbH));

            Rectangle thumbRect = new Rectangle(scrollBarX, thumbY, scrollBarWidth - 2, thumbH);
            bool thumbHover = Raylib.CheckCollisionPointRec(mousePos, thumbRect);
            Raylib.DrawRectangleRec(thumbRect, thumbHover ? ColorPalette.Accent : ColorPalette.PanelLight);
        }

        if (Program.ui.ShowBuildPanel)
        {
            Program.DrawBuildSidePanel();
        }
    }
}
