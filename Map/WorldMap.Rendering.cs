using System.Numerics;
using Raylib_cs;

using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Rendering-Methoden (Laender, Hintergrund, Grid, Fluesse, Oel)
/// </summary>
public partial class WorldMap
{
    /// <summary>
    /// Prueft ob eine Region im sichtbaren Bereich liegt (Frustum Culling)
    /// Nutzt vorberechnete Bounding Box statt per-Frame Iteration
    /// </summary>
    private static bool IsRegionVisible(MapRegion region, float viewMinX, float viewMinY, float viewMaxX, float viewMaxY)
    {
        if (region.PolygonRings.Count == 0) return false;
        return !(region.BoundsMaxX < viewMinX || region.BoundsMinX > viewMaxX ||
                 region.BoundsMaxY < viewMinY || region.BoundsMinY > viewMaxY);
    }

    /// <summary>
    /// Prueft ob eine Provinz im sichtbaren Bereich liegt
    /// Nutzt vorberechnete Bounding Box statt per-Frame Iteration
    /// </summary>
    private static bool IsProvinceVisible(Province province, float viewMinX, float viewMinY, float viewMaxX, float viewMaxY)
    {
        if (province.PolygonRings.Count == 0) return false;
        return !(province.BoundsMaxX < viewMinX || province.BoundsMinX > viewMaxX ||
                 province.BoundsMaxY < viewMinY || province.BoundsMinY > viewMaxY);
    }

    // Konfig fuer Ressourcen-Vorkommen (Index in _depositIcons, Icon-Datei, Icon-Groesse, Offset-Multiplikator)
    private static readonly (string IconFile, int IconSize, float OffsetMult)[] DepositIconConfigs = {
        ("oil_deposit.png", 28, 0.8f),  // 0: Oil
        ("natural_gas.png", 26, 0.8f),  // 1: NaturalGas
        ("coal.png",        26, 0.8f),  // 2: Coal
        ("iron.png",        26, 0.8f),  // 3: Iron
        ("uran.png",        26, 0.8f),  // 4: Uranium
        ("copper.png",      24, 0.7f),  // 5: Copper
    };

    // Statische OffsetCheck-Arrays (vermeiden per-Frame Allokation in DrawAllResourceDeposits)
    private HashSet<string>[]? _offsetChecks0;
    private HashSet<string>[]? _offsetChecks1;
    private HashSet<string>[]? _offsetChecks2;
    private HashSet<string>[]? _offsetChecks3;
    private HashSet<string>[]? _offsetChecks4;
    private HashSet<string>[]? _offsetChecks5;

    /// <summary>
    /// Zeichnet alle Ressourcen-Vorkommen-Icons auf der Karte
    /// Vermeidet per-Frame Array-Allokationen durch gecachte OffsetChecks
    /// </summary>
    private void DrawAllResourceDeposits(float viewMinX, float viewMinY, float viewMaxX, float viewMaxY)
    {
        // OffsetCheck-Arrays einmalig erstellen
        _offsetChecks0 ??= Array.Empty<HashSet<string>>();
        _offsetChecks1 ??= new[] { OilProvinceNames };
        _offsetChecks2 ??= new[] { OilProvinceNames };
        _offsetChecks3 ??= new[] { OilProvinceNames, CoalProvinceNames };
        _offsetChecks4 ??= new[] { OilProvinceNames, CoalProvinceNames, IronProvinceNames };
        _offsetChecks5 ??= new[] { IronProvinceNames, CoalProvinceNames, UraniumProvinceNames };

        DrawResourceDeposits(0, DepositIconConfigs[0].IconFile, DepositIconConfigs[0].IconSize, DepositIconConfigs[0].OffsetMult,
            OilProvinceNames, OilDeposits, _offsetChecks0, viewMinX, viewMinY, viewMaxX, viewMaxY);
        DrawResourceDeposits(1, DepositIconConfigs[1].IconFile, DepositIconConfigs[1].IconSize, DepositIconConfigs[1].OffsetMult,
            NaturalGasProvinceNames, NaturalGasDeposits, _offsetChecks1, viewMinX, viewMinY, viewMaxX, viewMaxY);
        DrawResourceDeposits(2, DepositIconConfigs[2].IconFile, DepositIconConfigs[2].IconSize, DepositIconConfigs[2].OffsetMult,
            CoalProvinceNames, CoalDeposits, _offsetChecks2, viewMinX, viewMinY, viewMaxX, viewMaxY);
        DrawResourceDeposits(3, DepositIconConfigs[3].IconFile, DepositIconConfigs[3].IconSize, DepositIconConfigs[3].OffsetMult,
            IronProvinceNames, IronDeposits, _offsetChecks3, viewMinX, viewMinY, viewMaxX, viewMaxY);
        DrawResourceDeposits(4, DepositIconConfigs[4].IconFile, DepositIconConfigs[4].IconSize, DepositIconConfigs[4].OffsetMult,
            UraniumProvinceNames, UraniumDeposits, _offsetChecks4, viewMinX, viewMinY, viewMaxX, viewMaxY);
        DrawResourceDeposits(5, DepositIconConfigs[5].IconFile, DepositIconConfigs[5].IconSize, DepositIconConfigs[5].OffsetMult,
            CopperProvinceNames, CopperDeposits, _offsetChecks5, viewMinX, viewMinY, viewMaxX, viewMaxY);
    }

    /// <summary>
    /// Generische Methode zum Zeichnen von Ressourcen-Vorkommen-Icons
    /// </summary>
    private void DrawResourceDeposits(int iconIndex, string iconFile, int iconSize, float offsetMult,
        HashSet<string> provinceNames, List<(string CountryId, double Lon, double Lat)> deposits,
        HashSet<string>[] offsetChecks,
        float viewMinX, float viewMinY, float viewMaxX, float viewMaxY)
    {
        // Icon laden falls noch nicht geschehen
        if (_depositIcons[iconIndex] == null)
        {
            string iconPath = Path.Combine("Data", "Icons", iconFile);
            if (!File.Exists(iconPath)) return;
            var loadedTex = Raylib.LoadTexture(iconPath);
            if (loadedTex.Id == 0) return;
            Raylib.SetTextureFilter(loadedTex, TextureFilter.Bilinear);
            _depositIcons[iconIndex] = loadedTex;
        }

        var tex = _depositIcons[iconIndex]!.Value;

        // 1. Zeichne Icons in Provinzen
        foreach (var (provinceId, province) in Provinces)
        {
            if (!provinceNames.Contains(province.Name))
                continue;

            float mapX = province.LabelPosition.X;
            float mapY = province.LabelPosition.Y;

            if (mapX < viewMinX - 50 || mapX > viewMaxX + 50 ||
                mapY < viewMinY - 50 || mapY > viewMaxY + 50)
                continue;

            Vector2 screenPos = MapToScreen(province.LabelPosition);

            // Versetzen falls andere Ressourcen am gleichen Ort
            int offsetCount = 0;
            for (int j = 0; j < offsetChecks.Length; j++)
            {
                if (offsetChecks[j].Contains(province.Name)) offsetCount++;
            }
            if (offsetCount > 0)
            {
                screenPos.X += iconSize * offsetMult * offsetCount;
            }

            int drawX = (int)(screenPos.X - iconSize / 2);
            int drawY = (int)(screenPos.Y - iconSize / 2);

            Rectangle source = new(0, 0, tex.Width, tex.Height);
            Rectangle dest = new(drawX, drawY, iconSize, iconSize);
            Raylib.DrawTexturePro(tex, source, dest, Vector2.Zero, 0, Color.White);
        }

        // 2. Zeichne Icons fuer Laender ohne Provinzen
        foreach (var (countryId, lon, lat) in deposits)
        {
            float mapX = (float)((lon - (-180)) / 360.0 * MAP_WIDTH);
            float mapY = (float)((85 - lat) / 145.0 * MAP_HEIGHT);

            if (mapX < viewMinX - 20 || mapX > viewMaxX + 20 ||
                mapY < viewMinY - 20 || mapY > viewMaxY + 20)
                continue;

            Vector2 screenPos = MapToScreen(new Vector2(mapX, mapY));

            int drawX = (int)(screenPos.X - iconSize / 2);
            int drawY = (int)(screenPos.Y - iconSize / 2);

            Rectangle source = new(0, 0, tex.Width, tex.Height);
            Rectangle dest = new(drawX, drawY, iconSize, iconSize);
            Raylib.DrawTexturePro(tex, source, dest, Vector2.Zero, 0, Color.White);
        }
    }

    private void DrawOceanBackground()
    {
        int x = (int)Offset.X;
        int y = (int)Offset.Y;
        int w = (int)(MAP_WIDTH * Zoom);
        int h = (int)(MAP_HEIGHT * Zoom);

        // Einheitliche dunkle Ozeanfarbe ohne sichtbare Linien
        Color oceanColor = new Color(8, 25, 45, 255);  // Noch dunkler
        Raylib.DrawRectangle(x, y, w, h, oceanColor);
    }

    private void DrawGrid()
    {
        Color gridColor = new Color(60, 90, 120, 60);

        // Laengengrade alle 30 Grad
        for (double lon = -180; lon <= 180; lon += 30)
        {
            float mapX = (float)((lon - MIN_LON) / (MAX_LON - MIN_LON) * MAP_WIDTH);
            Vector2 top = MapToScreen(new Vector2(mapX, 0));
            Vector2 bottom = MapToScreen(new Vector2(mapX, MAP_HEIGHT));
            Raylib.DrawLineV(top, bottom, gridColor);
        }

        // Breitengrade alle 30 Grad
        for (double lat = -60; lat <= 90; lat += 30)
        {
            float mapY = (float)((MAX_LAT - lat) / (MAX_LAT - MIN_LAT) * MAP_HEIGHT);
            Vector2 left = MapToScreen(new Vector2(0, mapY));
            Vector2 right = MapToScreen(new Vector2(MAP_WIDTH, mapY));
            Raylib.DrawLineV(left, right, gridColor);
        }
    }

    /// <summary>
    /// Zeichnet die Flaechenfuellung eines Landes mit Dreiecken
    /// </summary>
    private void DrawCountryFill(MapRegion region, bool isHovered, bool isSelected, bool isPlayer)
    {
        if (region.PolygonRings.Count == 0) return;

        // Farbe bestimmen (volle Deckkraft)
        // Kein Hover-Effekt fuer Flaechenfuellung - nur Grenze wird hervorgehoben
        // Keine Farbveraenderung bei Auswahl - Land behaelt immer seine Originalfarbe
        Color fillColor = region.BaseColor;

        // Nutze gecachte transformierte Punkte
        if (region.TransformedRings == null) return;

        // Maximale Kantenlaenge um Artefakte bei Kartenrand-Ueberquerung zu vermeiden
        float maxEdgeLength = MAP_WIDTH * Zoom * 0.45f;
        float maxEdgeLengthSq = maxEdgeLength * maxEdgeLength;

        // Zeichne alle Polygon-Ringe (Hauptland + Inseln) - nutze LOD-Triangles
        var trianglesList = region.CurrentTriangles ?? region.TrianglesPerRing;
        for (int ringIndex = 0; ringIndex < region.TransformedRings.Count; ringIndex++)
        {
            var transformed = region.TransformedRings[ringIndex];
            if (transformed.Length < 3) continue;
            if (ringIndex >= trianglesList.Count) continue;

            var triangles = trianglesList[ringIndex];

            // Zeichne alle Dreiecke
            foreach (var (i0, i1, i2) in triangles)
            {
                if (i0 >= transformed.Length || i1 >= transformed.Length || i2 >= transformed.Length)
                    continue;

                var p0 = transformed[i0];
                var p1 = transformed[i1];
                var p2 = transformed[i2];

                // Ueberspringe Dreiecke mit zu langen Kanten (Kartenrand-Artefakte)
                float dx01 = p1.X - p0.X, dy01 = p1.Y - p0.Y;
                float dx12 = p2.X - p1.X, dy12 = p2.Y - p1.Y;
                float dx20 = p0.X - p2.X, dy20 = p0.Y - p2.Y;

                if (dx01 * dx01 + dy01 * dy01 > maxEdgeLengthSq ||
                    dx12 * dx12 + dy12 * dy12 > maxEdgeLengthSq ||
                    dx20 * dx20 + dy20 * dy20 > maxEdgeLengthSq)
                    continue;

                Raylib.DrawTriangle(p0, p1, p2, fillColor);
            }
        }
    }

    /// <summary>
    /// Faerbt ein Land in der Ressourcen-Ansicht ein (einheitliche, neutrale Farbe)
    /// </summary>
    private void DrawCountryFillResources(MapRegion region, string countryId, bool isHovered, bool isSelected, ResourceType? heatmapResource = null)
    {
        if (region.PolygonRings.Count == 0) return;

        float value = ResourceAbundance.GetHeatmapValue(countryId, heatmapResource);
        Color fillColor = GetHeatmapColor(value);

        if (isHovered)
        {
            fillColor = new Color(
                (byte)Math.Min(fillColor.R + 25, 255),
                (byte)Math.Min(fillColor.G + 25, 255),
                (byte)Math.Min(fillColor.B + 25, 255),
                fillColor.A);
        }

        if (isSelected)
        {
            fillColor = new Color(
                (byte)Math.Min(fillColor.R + 40, 255),
                (byte)Math.Min(fillColor.G + 40, 255),
                (byte)Math.Min(fillColor.B + 40, 255),
                (byte)240);
        }

        // Nutze gecachte transformierte Punkte
        if (region.TransformedRings == null) return;

        float maxEdgeLength = MAP_WIDTH * Zoom * 0.45f;
        float maxEdgeLengthSq = maxEdgeLength * maxEdgeLength;

        // Zeichne alle Polygon-Ringe - nutze LOD-Triangles
        var trianglesList = region.CurrentTriangles ?? region.TrianglesPerRing;
        for (int ringIndex = 0; ringIndex < region.TransformedRings.Count; ringIndex++)
        {
            var transformed = region.TransformedRings[ringIndex];
            if (transformed.Length < 3) continue;
            if (ringIndex >= trianglesList.Count) continue;

            var triangles = trianglesList[ringIndex];

            foreach (var (i0, i1, i2) in triangles)
            {
                if (i0 >= transformed.Length || i1 >= transformed.Length || i2 >= transformed.Length)
                    continue;

                var p0 = transformed[i0];
                var p1 = transformed[i1];
                var p2 = transformed[i2];

                float dx01 = p1.X - p0.X, dy01 = p1.Y - p0.Y;
                float dx12 = p2.X - p1.X, dy12 = p2.Y - p1.Y;
                float dx20 = p0.X - p2.X, dy20 = p0.Y - p2.Y;

                if (dx01 * dx01 + dy01 * dy01 > maxEdgeLengthSq ||
                    dx12 * dx12 + dy12 * dy12 > maxEdgeLengthSq ||
                    dx20 * dx20 + dy20 * dy20 > maxEdgeLengthSq)
                    continue;

                Raylib.DrawTriangle(p0, p1, p2, fillColor);
            }
        }
    }

    /// <summary>
    /// Faerbt ein Land in der Handelsansicht ein (warmer Bernstein-Ton)
    /// </summary>
    private void DrawCountryFillTrade(MapRegion region, string countryId, bool isHovered, bool isSelected)
    {
        if (region.PolygonRings.Count == 0) return;

        Color fillColor = new Color((byte)22, (byte)18, (byte)14, (byte)235);

        if (isSelected)
        {
            fillColor = new Color((byte)38, (byte)32, (byte)25, (byte)240);
        }

        if (region.TransformedRings == null) return;

        float maxEdgeLength = MAP_WIDTH * Zoom * 0.45f;
        float maxEdgeLengthSq = maxEdgeLength * maxEdgeLength;

        var trianglesList = region.CurrentTriangles ?? region.TrianglesPerRing;
        for (int ringIndex = 0; ringIndex < region.TransformedRings.Count; ringIndex++)
        {
            var transformed = region.TransformedRings[ringIndex];
            if (transformed.Length < 3) continue;
            if (ringIndex >= trianglesList.Count) continue;

            var triangles = trianglesList[ringIndex];

            foreach (var (i0, i1, i2) in triangles)
            {
                if (i0 >= transformed.Length || i1 >= transformed.Length || i2 >= transformed.Length)
                    continue;

                var p0 = transformed[i0];
                var p1 = transformed[i1];
                var p2 = transformed[i2];

                float dx01 = p1.X - p0.X, dy01 = p1.Y - p0.Y;
                float dx12 = p2.X - p1.X, dy12 = p2.Y - p1.Y;
                float dx20 = p0.X - p2.X, dy20 = p0.Y - p2.Y;

                if (dx01 * dx01 + dy01 * dy01 > maxEdgeLengthSq ||
                    dx12 * dx12 + dy12 * dy12 > maxEdgeLengthSq ||
                    dx20 * dx20 + dy20 * dy20 > maxEdgeLengthSq)
                    continue;

                Raylib.DrawTriangle(p0, p1, p2, fillColor);
            }
        }
    }

    /// <summary>
    /// Faerbt ein Land in der Buendnis-Ansicht ein (nach Buendnis-Zugehoerigkeit)
    /// </summary>
    private void DrawCountryFillAlliance(MapRegion region, string countryId, bool isHovered, bool isSelected)
    {
        if (region.PolygonRings.Count == 0) return;

        // Farbe basierend auf Buendnis-Zugehoerigkeit
        // Kein Hover-Effekt fuer Flaechenfuellung
        Color baseColor = GetAllianceColor(countryId);
        Color fillColor = baseColor;

        // Selected Effekt
        if (isSelected)
        {
            fillColor = new Color(
                (byte)Math.Min(baseColor.R + 40, 255),
                (byte)Math.Min(baseColor.G + 40, 255),
                (byte)Math.Min(baseColor.B + 40, 255),
                (byte)255
            );
        }

        // Nutze gecachte transformierte Punkte
        if (region.TransformedRings == null) return;

        float maxEdgeLength = MAP_WIDTH * Zoom * 0.45f;
        float maxEdgeLengthSq = maxEdgeLength * maxEdgeLength;

        // Zeichne alle Polygon-Ringe - nutze LOD-Triangles
        var trianglesList = region.CurrentTriangles ?? region.TrianglesPerRing;
        for (int ringIndex = 0; ringIndex < region.TransformedRings.Count; ringIndex++)
        {
            var transformed = region.TransformedRings[ringIndex];
            if (transformed.Length < 3) continue;
            if (ringIndex >= trianglesList.Count) continue;

            var triangles = trianglesList[ringIndex];

            foreach (var (i0, i1, i2) in triangles)
            {
                if (i0 >= transformed.Length || i1 >= transformed.Length || i2 >= transformed.Length)
                    continue;

                var p0 = transformed[i0];
                var p1 = transformed[i1];
                var p2 = transformed[i2];

                float dx01 = p1.X - p0.X, dy01 = p1.Y - p0.Y;
                float dx12 = p2.X - p1.X, dy12 = p2.Y - p1.Y;
                float dx20 = p0.X - p2.X, dy20 = p0.Y - p2.Y;

                if (dx01 * dx01 + dy01 * dy01 > maxEdgeLengthSq ||
                    dx12 * dx12 + dy12 * dy12 > maxEdgeLengthSq ||
                    dx20 * dx20 + dy20 * dy20 > maxEdgeLengthSq)
                    continue;

                Raylib.DrawTriangle(p0, p1, p2, fillColor);
            }
        }
    }

    /// <summary>
    /// Heatmap-Farbe: Dunkelblau (0) -> Gelb (0.5) -> Rot (1.0)
    /// </summary>
    private static Color GetHeatmapColor(float value)
    {
        value = Math.Clamp(value, 0f, 1f);

        byte r, g, b;
        if (value <= 0.005f)
        {
            // Kein Vorkommen -> sehr dunkel
            r = 18; g = 20; b = 28;
        }
        else if (value < 0.25f)
        {
            // Gering -> Dunkelblau zu Blau
            float t = value / 0.25f;
            r = (byte)(18 + t * 10);
            g = (byte)(20 + t * 40);
            b = (byte)(50 + t * 120);
        }
        else if (value < 0.5f)
        {
            // Mittel -> Blau zu Gelb/Gruen
            float t = (value - 0.25f) / 0.25f;
            r = (byte)(28 + t * 190);
            g = (byte)(60 + t * 150);
            b = (byte)(170 - t * 130);
        }
        else if (value < 0.75f)
        {
            // Hoch -> Gelb zu Orange
            float t = (value - 0.5f) / 0.25f;
            r = (byte)(218 + t * 37);
            g = (byte)(210 - t * 80);
            b = (byte)(40 - t * 20);
        }
        else
        {
            // Sehr hoch -> Orange zu Rot
            float t = (value - 0.75f) / 0.25f;
            r = (byte)(255);
            g = (byte)(130 - t * 100);
            b = (byte)(20 - t * 10);
        }

        return new Color(r, g, b, (byte)220);
    }

    /// <summary>
    /// Berechnet den Heatmap-Wert fuer eine einzelne Provinz.
    /// Provinzen mit Rohstoff-Deposits bekommen den vollen Landeswert,
    /// andere nur einen niedrigen Basiswert.
    /// </summary>
    private float GetProvinceHeatmapValue(Province province, ResourceType? resourceFilter)
    {
        string countryId = province.CountryId;
        string provName = province.Name;

        if (resourceFilter.HasValue)
        {
            // Einzelne Ressource: Pruefen ob Provinz Deposits hat
            bool hasDeposit = resourceFilter.Value switch
            {
                ResourceType.Oil => OilProvinceNames.Contains(provName),
                ResourceType.NaturalGas => NaturalGasProvinceNames.Contains(provName),
                ResourceType.Coal => CoalProvinceNames.Contains(provName),
                ResourceType.Iron => IronProvinceNames.Contains(provName),
                ResourceType.Copper => CopperProvinceNames.Contains(provName),
                ResourceType.Uranium => UraniumProvinceNames.Contains(provName),
                _ => false
            };

            float countryValue = ResourceAbundance.GetHeatmapValue(countryId, resourceFilter);
            return hasDeposit ? countryValue : countryValue * 0.05f;
        }
        else
        {
            // "Alle" Modus: Hoechsten Wert finden
            float maxVal = 0f;
            var res = ResourceAbundance.GetCountryResources(countryId);

            if (OilProvinceNames.Contains(provName))
                maxVal = Math.Max(maxVal, res.Oil);
            if (NaturalGasProvinceNames.Contains(provName))
                maxVal = Math.Max(maxVal, res.NaturalGas);
            if (CoalProvinceNames.Contains(provName))
                maxVal = Math.Max(maxVal, res.Coal);
            if (IronProvinceNames.Contains(provName))
                maxVal = Math.Max(maxVal, res.Iron);
            if (CopperProvinceNames.Contains(provName))
                maxVal = Math.Max(maxVal, res.Copper);
            if (UraniumProvinceNames.Contains(provName))
                maxVal = Math.Max(maxVal, res.Uranium);

            // Wenn keine Deposits, niedrigen Basiswert
            if (maxVal <= 0f)
                maxVal = res.GetMaxValue() * 0.05f;

            return maxVal;
        }
    }

    /// <summary>
    /// Zeichnet eine Provinz mit Heatmap-Farbe
    /// </summary>
    private void DrawProvinceFillHeatmap(Province province, float heatmapValue, bool isHovered, bool isSelected)
    {
        if (province.TransformedRings == null || province.TransformedRings.Count == 0) return;

        Color fillColor = GetHeatmapColor(heatmapValue);
        // Volle Opazitaet damit die Provinz den Laender-Basis-Fill komplett ueberdeckt
        fillColor = new Color(fillColor.R, fillColor.G, fillColor.B, (byte)255);

        if (isHovered)
        {
            fillColor = new Color(
                (byte)Math.Min(fillColor.R + 25, 255),
                (byte)Math.Min(fillColor.G + 25, 255),
                (byte)Math.Min(fillColor.B + 25, 255),
                (byte)255);
        }
        if (isSelected)
        {
            fillColor = new Color(
                (byte)Math.Min(fillColor.R + 40, 255),
                (byte)Math.Min(fillColor.G + 40, 255),
                (byte)Math.Min(fillColor.B + 40, 255),
                (byte)255);
        }

        float maxEdgeLength = MAP_WIDTH * Zoom * 0.45f;
        float maxEdgeLengthSq = maxEdgeLength * maxEdgeLength;

        for (int ringIndex = 0; ringIndex < province.TransformedRings.Count; ringIndex++)
        {
            var transformed = province.TransformedRings[ringIndex];
            if (transformed.Length < 3) continue;
            if (ringIndex >= province.TrianglesPerRing.Count) continue;

            var triangles = province.TrianglesPerRing[ringIndex];
            foreach (var (i0, i1, i2) in triangles)
            {
                if (i0 >= transformed.Length || i1 >= transformed.Length || i2 >= transformed.Length)
                    continue;

                var p0 = transformed[i0];
                var p1 = transformed[i1];
                var p2 = transformed[i2];

                float dx01 = p1.X - p0.X, dy01 = p1.Y - p0.Y;
                float dx12 = p2.X - p1.X, dy12 = p2.Y - p1.Y;
                float dx20 = p0.X - p2.X, dy20 = p0.Y - p2.Y;

                if (dx01 * dx01 + dy01 * dy01 > maxEdgeLengthSq ||
                    dx12 * dx12 + dy12 * dy12 > maxEdgeLengthSq ||
                    dx20 * dx20 + dy20 * dy20 > maxEdgeLengthSq)
                    continue;

                Raylib.DrawTriangle(p0, p1, p2, fillColor);
            }
        }
    }

    /// <summary>
    /// Berechnet adaptive Grenzlinien-Dicke basierend auf Zoom-Level
    /// </summary>
    private float GetAdaptiveBorderWidth(float multiplier = 1.0f)
    {
        // Wurzel-Skalierung: waechst langsamer als linear
        float adaptiveWidth = BorderWidth * (float)Math.Sqrt(Zoom);

        // Begrenzen auf sinnvolle Werte
        const float minWidth = 0.5f;
        const float maxWidth = 4.0f;

        return Math.Clamp(adaptiveWidth * multiplier, minWidth, maxWidth);
    }

    private void DrawCountryBorder(MapRegion region, bool isHovered, bool isSelected, bool isPlayer)
    {
        if (region.PolygonRings.Count == 0) return;

        // Grenzen-Stil bestimmen
        float baseWidth;
        byte r, g, b;

        if (isPlayer)
        {
            r = 255; g = 255; b = 255;
            baseWidth = GetAdaptiveBorderWidth(1.5f);
        }
        else if (isSelected)
        {
            r = 80; g = 150; b = 255;
            baseWidth = GetAdaptiveBorderWidth(1.3f);
        }
        else
        {
            // Standard: Dunkle Grenze
            r = 20; g = 20; b = 30;
            baseWidth = GetAdaptiveBorderWidth(1.0f);
        }

        // Nutze gecachte transformierte Punkte
        if (region.TransformedRings == null) return;

        // Maximale Linienlaenge um Artefakte bei Kartenrand-Ueberquerung zu vermeiden
        float maxLineLength = MAP_WIDTH * Zoom * 0.45f;
        float maxLineLengthSq = maxLineLength * maxLineLength;
        Color borderColor = new Color(r, g, b, (byte)255);

        // Zeichne ALLE Polygon-Ringe (Hauptland + Alaska, Inseln, etc.)
        foreach (var transformedPoints in region.TransformedRings)
        {
            if (transformedPoints.Length < 3) continue;

            for (int i = 0; i < transformedPoints.Length; i++)
            {
                int next = (i + 1) % transformedPoints.Length;

                // Ueberspringe Linien die zu lang sind (Kartenrand-Artefakte)
                float dx = transformedPoints[next].X - transformedPoints[i].X;
                float dy = transformedPoints[next].Y - transformedPoints[i].Y;
                if (dx * dx + dy * dy > maxLineLengthSq)
                    continue;

                Raylib.DrawLineEx(transformedPoints[i], transformedPoints[next], baseWidth, borderColor);
            }
        }
    }

    /// <summary>
    /// Zeichnet die Spieler-Grenze als Overlay ueber alle anderen Grenzen
    /// Mit leichter Transparenz und dickerer Linie fuer bessere Sichtbarkeit
    /// </summary>
    private void DrawPlayerBorderOverlay(MapRegion region)
    {
        if (region.PolygonRings.Count == 0) return;
        if (region.TransformedRings == null) return;

        // Weisse Umrandung mit 40% Deckkraft (Grenzen darunter sichtbar)
        float baseWidth = GetAdaptiveBorderWidth(1.5f);  // Etwas dicker als normale Grenzen
        Color borderColor = new Color((byte)255, (byte)255, (byte)255, (byte)100);  // 40% Deckkraft

        float maxLineLength = MAP_WIDTH * Zoom * 0.45f;
        float maxLineLengthSq = maxLineLength * maxLineLength;

        // Zeichne ALLE Polygon-Ringe (Hauptland + Alaska, Inseln, etc.)
        foreach (var transformedPoints in region.TransformedRings)
        {
            if (transformedPoints.Length < 3) continue;

            for (int i = 0; i < transformedPoints.Length; i++)
            {
                int next = (i + 1) % transformedPoints.Length;

                float dx = transformedPoints[next].X - transformedPoints[i].X;
                float dy = transformedPoints[next].Y - transformedPoints[i].Y;
                if (dx * dx + dy * dy > maxLineLengthSq)
                    continue;

                Raylib.DrawLineEx(transformedPoints[i], transformedPoints[next], baseWidth, borderColor);
            }
        }
    }

    /// <summary>
    /// Zeichnet die Highlight-Grenze fuer selected/hovered Laender als Overlay
    /// Mit Transparenz damit Grenzen darunter sichtbar bleiben
    /// </summary>
    private void DrawHighlightBorderOverlay(MapRegion region, bool isSelected)
    {
        if (region.PolygonRings.Count == 0) return;
        if (region.TransformedRings == null) return;

        // Farbe und Breite basierend auf Zustand
        float baseWidth = GetAdaptiveBorderWidth(1.5f);
        Color borderColor;

        if (isSelected)
        {
            // Gelb fuer selected - 50% Deckkraft
            borderColor = new Color((byte)255, (byte)220, (byte)50, (byte)128);
        }
        else
        {
            // Weiss fuer hovered - volle Deckkraft
            borderColor = new Color((byte)255, (byte)255, (byte)255, (byte)255);
        }

        float maxLineLength = MAP_WIDTH * Zoom * 0.45f;
        float maxLineLengthSq = maxLineLength * maxLineLength;

        foreach (var transformedPoints in region.TransformedRings)
        {
            if (transformedPoints.Length < 3) continue;

            for (int i = 0; i < transformedPoints.Length; i++)
            {
                int next = (i + 1) % transformedPoints.Length;

                float dx = transformedPoints[next].X - transformedPoints[i].X;
                float dy = transformedPoints[next].Y - transformedPoints[i].Y;
                if (dx * dx + dy * dy > maxLineLengthSq)
                    continue;

                Raylib.DrawLineEx(transformedPoints[i], transformedPoints[next], baseWidth, borderColor);
            }
        }
    }

    /// <summary>
    /// Zeichnet alle sichtbaren Fluesse
    /// </summary>
    private void DrawRivers(float viewMinX, float viewMinY, float viewMaxX, float viewMaxY)
    {
        // Bestimme minimalen ScaleRank basierend auf Zoom
        // ScaleRank 0-1 = groesste Fluesse (Amazon, Nil, Mississippi, etc.)
        // ScaleRank 2-3 = grosse Fluesse
        // ScaleRank 4-5 = mittlere Fluesse
        // ScaleRank 6+ = kleine Fluesse
        int minScaleRank = Zoom switch
        {
            < 0.3f => 0,   // Weltkarte: nur die absolut groessten Fluesse
            < 0.5f => 1,   // Stark rausgezoomt: sehr grosse Fluesse
            < 1.0f => 2,   // Rausgezoomt: grosse Fluesse
            < 2.0f => 3,   // Mittel: grosse bis mittlere Fluesse
            < 4.0f => 4,   // Nah: mittlere Fluesse
            < 8.0f => 5,   // Sehr nah: mehr Detail
            _ => 6         // Maximal reingezoomt: alle Fluesse
        };

        // Maximale Linienlaenge um Wrap-Around-Artefakte zu vermeiden
        float maxLineLength = MAP_WIDTH * Zoom * 0.45f;
        float maxLineLengthSq = maxLineLength * maxLineLength;

        foreach (var river in _rivers)
        {
            // Nur Fluesse mit ausreichender Wichtigkeit zeichnen
            if (river.ScaleRank > minScaleRank) continue;

            // Pruefe ob Fluss sichtbar ist (einfache Bounding-Box-Pruefung)
            bool isVisible = false;
            foreach (var segment in river.LineSegments)
            {
                foreach (var point in segment)
                {
                    if (point.X >= viewMinX && point.X <= viewMaxX &&
                        point.Y >= viewMinY && point.Y <= viewMaxY)
                    {
                        isVisible = true;
                        break;
                    }
                }
                if (isVisible) break;
            }

            if (!isVisible) continue;

            // Cache aktualisieren
            river.UpdateTransformedPoints(Zoom, Offset, MapToScreen);
            if (river.TransformedSegments == null) continue;

            // Linienbreite basierend auf Wichtigkeit und Zoom
            float lineWidth = river.GetLineWidth(Zoom);

            // Zeichne alle Segmente
            foreach (var segment in river.TransformedSegments)
            {
                if (segment.Length < 2) continue;

                for (int i = 0; i < segment.Length - 1; i++)
                {
                    float dx = segment[i + 1].X - segment[i].X;
                    float dy = segment[i + 1].Y - segment[i].Y;

                    // Ueberspringe Wrap-Around-Linien
                    if (dx * dx + dy * dy > maxLineLengthSq)
                        continue;

                    Raylib.DrawLineEx(segment[i], segment[i + 1], lineWidth, RiverColor);
                }
            }
        }
    }

    /// <summary>
    /// Zeichnet das Laenderlabel
    /// </summary>
    // === HOI4-artige Laendernamen: passen sich an Form und Ausrichtung des ===
    // === aktuell besessenen Territoriums an (auch nach Eroberungen).       ===

    /// <summary>
    /// Vorberechnetes Label-Layout eines Landes in Map-Koordinaten (zoom-unabhaengig).
    /// Der Name folgt einer Parabel-"Mittellinie" durchs Territorium (HOI4-Kurve):
    /// AngleRad = Basisausrichtung, StdAlong/StdPerp = Ausdehnung entlang/quer,
    /// CurveA/B/C = Parabel y=A*x^2+B*x+C im gedrehten Bezugssystem (Map-Einheiten).
    /// </summary>
    private struct CountryLabelLayout
    {
        public Vector2 Center;
        public float AngleRad;
        public float StdAlong;
        public float StdPerp;
        public float CurveA;
        public float CurveB;
        public float CurveC;
        public bool Valid;
    }

    private readonly Dictionary<string, CountryLabelLayout> _labelLayouts = new();
    private int _labelRecomputeCountdown = 0;

    /// <summary>
    /// Berechnet die Label-Layouts bei Bedarf neu (gedrosselt, da sich das
    /// Territorium nur bei Eroberungen aendert - ein kurzer Verzug ist unkritisch).
    /// </summary>
    private void EnsureCountryLabelLayouts()
    {
        if (_labelLayouts.Count > 0 && _labelRecomputeCountdown > 0)
        {
            _labelRecomputeCountdown--;
            return;
        }
        _labelRecomputeCountdown = 30; // ~alle 30 Frames neu
        RecomputeCountryLabelLayouts();
    }

    // Wiederverwendbare Punktlisten (vermeidet Allokationen pro Recompute)
    private readonly Dictionary<string, List<(Vector2 P, float W)>> _labelPoints = new();

    private void RecomputeCountryLabelLayouts()
    {
        // 1) Gewichtete Punktwolke pro aktuellem Besitzer sammeln.
        //    Gewicht = Provinzflaeche / Vertexzahl -> grosse Flaechen dominieren,
        //    winzige ferne Inseln zaehlen kaum (verhindert dass Exklaven wie
        //    US-Alaska oder franzoesische Ueberseegebiete das Label ins Meer ziehen).
        foreach (var list in _labelPoints.Values) list.Clear();

        void AddRing(List<(Vector2, float)> list, Vector2[] ring)
        {
            if (ring.Length < 3) return;
            float w = Math.Abs(PolygonUtils.CalculateRingArea(ring)) / ring.Length;
            if (w <= 0) w = 1e-4f;
            foreach (var p in ring) list.Add((p, w));
        }

        List<(Vector2, float)> ListFor(string cid)
        {
            if (!_labelPoints.TryGetValue(cid, out var list))
                _labelPoints[cid] = list = new List<(Vector2, float)>();
            return list;
        }

        foreach (var province in Provinces.Values)
        {
            var cid = province.CountryId;
            if (string.IsNullOrEmpty(cid)) continue;
            var list = ListFor(cid);
            foreach (var ring in province.PolygonRings)
                AddRing(list, ring);
        }

        // 2) Pro Land ein robustes Layout (Winkel + Kurve) ableiten
        _labelLayouts.Clear();
        foreach (var (cid, region) in Regions)
        {
            _labelPoints.TryGetValue(cid, out var pts);

            // Laender ohne eigene Provinzdaten: Heimatgebiet ueber das Region-Polygon.
            // Provinz-Laender dagegen ausschliesslich aus ihren Provinzen -> Label
            // schrumpft/waechst korrekt bei Gebietsverlust/-gewinn (Eroberung).
            if (!CountriesWithProvinces.Contains(cid))
            {
                pts ??= ListFor(cid);
                foreach (var ring in region.PolygonRings)
                    AddRing(pts, ring);
            }

            if (pts == null || pts.Count < 3) continue;

            var layout = ComputeLabelLayout(pts);
            if (layout.Valid)
                _labelLayouts[cid] = layout;
        }
    }

    /// <summary>
    /// Berechnet Ausrichtung + Parabel-Kurve fuer ein Land aus seiner gewichteten
    /// Punktwolke: robuste Ausreisser-Verwerfung, Elongations-dosierte Rotation,
    /// und eine flaechengewichtete Parabel als gekruemmte Mittellinie.
    /// </summary>
    private static CountryLabelLayout ComputeLabelLayout(List<(Vector2 P, float W)> input)
    {
        const double maha = 2.0;

        // Robuster Schwerpunkt + Kovarianz (2 Iterationen: fernes Territorium raus)
        var pts = input;
        ComputeWeighted(pts, out double mx, out double my, out double cxx, out double cyy, out double cxy);
        for (int iter = 0; iter < 2; iter++)
        {
            double det0 = cxx * cyy - cxy * cxy;
            if (det0 <= 1e-9) break;
            double ixx = cyy / det0, iyy = cxx / det0, ixy = -cxy / det0;
            var kept = new List<(Vector2, float)>(pts.Count);
            foreach (var (p, w) in pts)
            {
                double ddx = p.X - mx, ddy = p.Y - my;
                double m2 = ddx * ddx * ixx + 2 * ddx * ddy * ixy + ddy * ddy * iyy;
                if (m2 <= maha * maha) kept.Add((p, w));
            }
            if (kept.Count < 3 || kept.Count == pts.Count) break;
            pts = kept;
            ComputeWeighted(pts, out mx, out my, out cxx, out cyy, out cxy);
        }

        // Hauptachse + Elongation -> dosierte, begrenzte Rotation
        double pcaAngle = 0.5 * Math.Atan2(2 * cxy, cxx - cyy);
        double trace = (cxx + cyy) / 2;
        double dev = Math.Sqrt(Math.Max(0, (cxx - cyy) * (cxx - cyy) / 4 + cxy * cxy));
        double elong = Math.Sqrt((trace + dev) / Math.Max(1e-9, trace - dev));
        float factor = (float)Math.Clamp((elong - 1.15) / 0.5, 0.0, 1.0);
        double angle = pcaAngle * factor;
        double maxAngle = 52.0 * Math.PI / 180.0;
        angle = Math.Clamp(angle, -maxAngle, maxAngle);

        // Ausdehnung entlang der gewaehlten Achse
        double c = Math.Cos(angle), s = Math.Sin(angle);
        double varAlong = cxx * c * c + 2 * cxy * c * s + cyy * s * s;
        double varPerp = cxx * s * s - 2 * cxy * c * s + cyy * c * c;
        float stdAlong = (float)Math.Sqrt(Math.Max(0, varAlong));
        float stdPerp = (float)Math.Sqrt(Math.Max(0, varPerp));

        // Flaechengewichtete Parabel y = A x^2 + B x + C im gedrehten Bezugssystem
        // (x entlang Achse, y quer). Nur fuer laengliche Laender (per factor gedaempft).
        double s0 = 0, s1 = 0, s2 = 0, s3 = 0, s4 = 0, sy0 = 0, sy1 = 0, sy2 = 0;
        double cA = Math.Cos(-angle), sA = Math.Sin(-angle);
        foreach (var (p, w) in pts)
        {
            double dx = p.X - mx, dy = p.Y - my;
            double u = dx * cA - dy * sA;   // entlang Achse
            double v = dx * sA + dy * cA;   // quer
            double u2 = u * u;
            s0 += w; s1 += w * u; s2 += w * u2; s3 += w * u2 * u; s4 += w * u2 * u2;
            sy0 += w * v; sy1 += w * v * u; sy2 += w * v * u2;
        }
        double A = 0, B = 0, C = 0;
        Solve3(s4, s3, s2, s3, s2, s1, s2, s1, s0, sy2, sy1, sy0, ref A, ref B, ref C);
        A *= factor; B *= factor; C *= factor;

        // Kruemmung begrenzen: Auslenkung an den Enden <= 0.9 * Querausdehnung
        float halfLen = stdAlong * 1.55f;
        double endDefl = Math.Abs(A * halfLen * halfLen + B * halfLen + C);
        double maxDefl = 0.9 * stdPerp;
        if (endDefl > maxDefl && endDefl > 1e-6)
        {
            double sc = maxDefl / endDefl;
            A *= sc; B *= sc; C *= sc;
        }

        return new CountryLabelLayout
        {
            Center = new Vector2((float)mx, (float)my),
            AngleRad = (float)angle,
            StdAlong = stdAlong,
            StdPerp = stdPerp,
            CurveA = (float)A,
            CurveB = (float)B,
            CurveC = (float)C,
            Valid = true,
        };
    }

    /// <summary>Loest ein 3x3-Gleichungssystem (Cramersche Regel) fuer die Parabel.</summary>
    private static void Solve3(double a11, double a12, double a13,
        double a21, double a22, double a23, double a31, double a32, double a33,
        double b1, double b2, double b3, ref double x1, ref double x2, ref double x3)
    {
        double det = a11 * (a22 * a33 - a23 * a32)
                   - a12 * (a21 * a33 - a23 * a31)
                   + a13 * (a21 * a32 - a22 * a31);
        if (Math.Abs(det) < 1e-12) { x1 = x2 = x3 = 0; return; }
        double dx = b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3);
        double dy = a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31);
        double dz = a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31);
        x1 = dx / det; x2 = dy / det; x3 = dz / det;
    }

    private static void ComputeWeighted(List<(Vector2 P, float W)> pts,
        out double mx, out double my, out double cxx, out double cyy, out double cxy)
    {
        double sw = 0, sx = 0, sy = 0;
        foreach (var (p, w) in pts) { sw += w; sx += p.X * w; sy += p.Y * w; }
        if (sw <= 0) sw = 1;
        mx = sx / sw; my = sy / sw;
        double axx = 0, ayy = 0, axy = 0;
        foreach (var (p, w) in pts)
        {
            double dx = p.X - mx, dy = p.Y - my;
            axx += w * dx * dx; ayy += w * dy * dy; axy += w * dx * dy;
        }
        cxx = axx / sw; cyy = ayy / sw; cxy = axy / sw;
    }

    /// <summary>
    /// Zeichnet den Laendernamen HOI4-artig: entlang einer gekruemmten Mittellinie
    /// durchs Territorium, buchstabenweise tangential gedreht.
    /// </summary>
    private void DrawCountryLabel(string countryId, MapRegion region, float alpha = 1f)
    {
        if (!_labelLayouts.TryGetValue(countryId, out var layout) || !layout.Valid) return;

        double angle = layout.AngleRad;
        float halfMajor = layout.StdAlong * 1.55f * Zoom;   // halbe Ausdehnung in Screen-Pixeln
        float halfMinor = layout.StdPerp * 1.55f * Zoom;

        // Zu kleines Territorium auf dem Bildschirm -> kein Label
        if (halfMajor < 14f || halfMinor < 5f) return;

        Vector2 screenCenter = MapToScreen(layout.Center);
        string displayName = CountryNames.TryGetValue(countryId, out var fullName) ? fullName : countryId;

        // Schriftgroesse PROPORTIONAL zur Querausdehnung des Landes - KEINE feste
        // Obergrenze, damit der Name beim Reinzoomen mitwaechst und das Land immer
        // gleich gut fuellt (nur eine Sanity-Grenze). Zu kleines Label wird verworfen.
        const int fontFloor = 6;
        int fontSize = Math.Min((int)(halfMinor * 0.9f), 240);
        if (fontSize < fontFloor) return;

        float targetWidth = halfMajor * 2f * 0.72f;

        // Name zu breit? Erst Schrift verkleinern (statt sofort auf den Code),
        // damit deutlich mehr Laender mit vollem Namen erscheinen.
        while (fontSize > fontFloor && Program.MeasureLabelText(displayName, fontSize) > targetWidth)
            fontSize--;

        float naturalWidth = Program.MeasureLabelText(displayName, fontSize);

        // Immer noch zu breit -> auf Laendercode ausweichen (und ggf. weiter verkleinern)
        if (naturalWidth > targetWidth * 1.25f && displayName != countryId)
        {
            displayName = countryId;
            while (fontSize > fontFloor && Program.MeasureLabelText(displayName, fontSize) > targetWidth)
                fontSize--;
            naturalWidth = Program.MeasureLabelText(displayName, fontSize);
        }

        if (fontSize < fontFloor) return;

        int charCount = displayName.Length;
        if (charCount == 0) return;

        // Buchstaben spreizen (gedeckelt, damit keine absurden Luecken)
        float spacing = 0f;
        if (charCount > 1 && naturalWidth < targetWidth)
            spacing = (targetWidth - naturalWidth) / (charCount - 1);
        spacing = Math.Min(spacing, 0.42f * fontSize);

        // Der Label-Font ist PROPORTIONAL (nicht monospace) -> jede Glyphe hat eine
        // eigene Breite. Vorab messen, damit Platzierung + Zentrierung stimmen.
        var charWidths = new float[charCount];
        float sumW = 0f;
        for (int i = 0; i < charCount; i++)
        {
            charWidths[i] = Program.MeasureLabelText(displayName.Substring(i, 1), fontSize);
            sumW += charWidths[i];
        }
        float total = sumW + spacing * (charCount - 1);

        // Parabel in Screen-lokalen Koordinaten (relativ zu screenCenter, Achse=angle):
        // aus y_map = A x_map^2 + B x_map + C wird y_s = (A/Zoom) x_s^2 + B x_s + C*Zoom
        double aS = layout.CurveA / Zoom, bS = layout.CurveB, cS = layout.CurveC * Zoom;
        double cosA = Math.Cos(angle), sinA = Math.Sin(angle);

        byte A(float baseA) => (byte)Math.Clamp(baseA * alpha, 0, 255);
        Color shadow = new Color((byte)0, (byte)0, (byte)0, A(150));
        Color cream = new Color((byte)255, (byte)252, (byte)240, A(255));

        float pen = -total / 2f;
        for (int i = 0; i < charCount; i++)
        {
            string ch = displayName.Substring(i, 1);
            double xs = pen + charWidths[i] / 2f;
            double y = aS * xs * xs + bS * xs + cS;
            double slope = 2 * aS * xs + bS;
            float glyphAngleDeg = (float)((angle + Math.Atan(slope)) * (180.0 / Math.PI));

            // lokale (xs, y) um angle rotieren + Zentrum
            double lx = xs * cosA - y * sinA;
            double ly = xs * sinA + y * cosA;
            Vector2 gpos = new Vector2(screenCenter.X + (float)lx, screenCenter.Y + (float)ly);

            Program.DrawLabelGlyph(ch, gpos + new Vector2(1.5f, 1.5f), fontSize, glyphAngleDeg, shadow);
            Program.DrawLabelGlyph(ch, gpos, fontSize, glyphAngleDeg, cream);

            pen += charWidths[i] + spacing;
        }
    }
}
