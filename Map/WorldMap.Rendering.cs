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
    /// Zeichnet nur die aeussere Landesgrenze (ersten Ring) ohne interne Provinzgrenzen
    /// Wird verwendet fuer Laender mit Provinzen bei hohem Zoom, um interne Unterteilungen zu verstecken
    /// </summary>
    private void DrawCountryBorderOuterOnly(MapRegion region)
    {
        if (region.PolygonRings.Count == 0) return;
        if (region.TransformedRings == null || region.TransformedRings.Count == 0) return;

        // Nur den ersten (aeussersten) Ring zeichnen
        var transformedPoints = region.TransformedRings[0];
        if (transformedPoints.Length < 3) return;

        // Standard dunkle Grenze
        float baseWidth = GetAdaptiveBorderWidth(1.0f);
        Color borderColor = new Color((byte)20, (byte)20, (byte)30, (byte)255);

        // Maximale Linienlaenge um Artefakte bei Kartenrand-Ueberquerung zu vermeiden
        float maxLineLength = MAP_WIDTH * Zoom * 0.45f;
        float maxLineLengthSq = maxLineLength * maxLineLength;

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
    private void DrawCountryLabel(string countryId, MapRegion region)
    {
        // Nutze gecachte transformierte Punkte
        if (region.TransformedRings == null || region.TransformedRings.Count == 0) return;

        var transformedPoints = region.TransformedRings[0];
        if (transformedPoints.Length < 3) return;

        // Bounding Box des Landes auf dem Bildschirm berechnen
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in transformedPoints)
        {
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
        }
        float countryWidth = maxX - minX;
        float countryHeight = maxY - minY;

        // Laendername holen (falls vorhanden, sonst Code verwenden)
        string displayName = CountryNames.TryGetValue(countryId, out var fullName) ? fullName : countryId;

        // Intelligente Schriftgroessen-Berechnung
        float countrySize = Math.Min(countryWidth, countryHeight);

        // Maximale Textbreite: 70% der Landesbreite
        float maxTextWidth = countryWidth * 0.7f;

        // Schriftgroesse berechnen (iterativ anpassen)
        int fontSize = (int)(countrySize * 0.25f);  // Startgroesse
        fontSize = Math.Clamp(fontSize, 8, 48);     // Grenzen

        int textWidth = Program.MeasureGameText(displayName, fontSize);

        // Text zu breit? Verkleinern oder auf Code wechseln
        if (textWidth > maxTextWidth && fontSize > 10)
        {
            fontSize = (int)(fontSize * maxTextWidth / textWidth);
            fontSize = Math.Max(fontSize, 10);
            textWidth = Program.MeasureGameText(displayName, fontSize);
        }

        // Wenn immer noch zu breit, zeige nur den Code
        if (textWidth > maxTextWidth * 1.2f)
        {
            displayName = countryId;
            textWidth = Program.MeasureGameText(displayName, fontSize);
        }

        // Wenn das Land zu klein ist (Text passt nicht), Label ueberspringen
        if (countryWidth < 30 || countryHeight < 20)
        {
            return;
        }

        // Bei sehr kleinen Laendern nur Code zeigen
        if (countrySize < 60 && displayName != countryId)
        {
            displayName = countryId;
            textWidth = Program.MeasureGameText(displayName, fontSize);
        }

        // Nutze gecachte transformierte Label-Position
        Vector2 screenPos = region.TransformedLabelPos;

        int textX = (int)(screenPos.X - textWidth / 2);
        int textY = (int)(screenPos.Y - fontSize / 2);

        // Schatten rechts-unten fuer 3D-Effekt
        Color shadowColor = new Color(0, 0, 0, 140);
        Program.DrawGameText(displayName, textX + 1, textY + 1, fontSize, shadowColor);

        // Haupttext - leicht cremefarbener Ton wie in HOI4
        Color textColor = new Color(255, 252, 240, 255);
        Program.DrawGameText(displayName, textX, textY, fontSize, textColor);
    }
}
