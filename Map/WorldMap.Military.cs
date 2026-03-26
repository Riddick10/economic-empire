using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Systems.Managers;

using GrandStrategyGame.Models;
using GrandStrategyGame.UI;

namespace GrandStrategyGame.Map;

/// <summary>
/// WorldMap - Militaereinheiten auf der Karte rendern
/// </summary>
public partial class WorldMap
{
    // Wiederverwendbare Collections fuer Military-Rendering (vermeidet GC-Druck)
    private readonly Dictionary<string, List<MilitaryUnit>> _tempUnitsByProvince = new();
    private readonly Dictionary<(UnitType, bool), List<MilitaryUnit>> _tempUnitGroup = new();
    private readonly List<MilitaryUnit> _tempMovingUnits = new();
    private readonly Stack<List<MilitaryUnit>> _tempProvinceListPool = new();

    // Bullet-Partikel fuer Engaging-Kampfanimation
    private readonly List<BulletParticle> _bulletParticles = new();
    private double _lastBulletSpawnTime = 0;

    private struct BulletParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;      // 0-1, 0 = tot
        public byte Type;       // 0 = Kugel, 1 = Muendungsfeuer, 2 = Einschlag
    }


    /// <summary>
    /// Zeichnet alle Militaereinheiten auf der Karte
    /// </summary>
    public void DrawMilitaryUnits(MilitaryManager? militaryManager, string? playerCountryId, MilitaryUnit? selectedUnit = null)
    {
        if (militaryManager == null) return;

        var allUnits = militaryManager.GetAllUnits();
        if (allUnits.Count == 0) return;

        // Wiederverwendbare Listen fuer Gruppierung (vermeidet per-Frame Allokationen)
        _tempUnitsByProvince.Clear();
        _tempUnitGroup.Clear();
        _tempMovingUnits.Clear();

        // Trenne Einheiten in einem Durchgang
        for (int i = 0; i < allUnits.Count; i++)
        {
            var unit = allUnits[i];
            if (unit.Status == UnitStatus.Moving)
            {
                _tempMovingUnits.Add(unit);
                continue;
            }

            if (!_tempUnitsByProvince.TryGetValue(unit.ProvinceId, out var provinceList))
            {
                provinceList = _tempProvinceListPool.Count > 0
                    ? _tempProvinceListPool.Pop()
                    : new List<MilitaryUnit>();
                provinceList.Clear();
                _tempUnitsByProvince[unit.ProvinceId] = provinceList;
            }
            provinceList.Add(unit);
        }

        // Zeichne stationaere Einheiten pro Provinz
        foreach (var (provinceId, units) in _tempUnitsByProvince)
        {
            if (!Provinces.TryGetValue(provinceId, out var province))
                continue;

            Vector2 mapPos = province.LabelPosition;
            Vector2 screenPos = MapToScreen(mapPos);

            float offsetX = 0;

            // Gruppiere nach Typ und Status in einem Durchgang
            _tempUnitGroup.Clear();
            foreach (var unit in units)
            {
                var key = (unit.Type, unit.Status == UnitStatus.Recruiting);
                if (!_tempUnitGroup.TryGetValue(key, out var group))
                {
                    group = new List<MilitaryUnit>();
                    _tempUnitGroup[key] = group;
                }
                group.Add(unit);
            }

            // Zuerst Ready, dann Recruiting zeichnen
            foreach (var ((unitType, isRecruiting), group) in _tempUnitGroup)
            {
                if (isRecruiting) continue; // Ready zuerst
                var unitIcon = TextureManager.GetMilitaryUnit(unitType);
                DrawUnitMarker(screenPos, offsetX, group, unitIcon, playerCountryId, false, false, selectedUnit);
                offsetX += 36 * Zoom * 0.5f;
            }
            foreach (var ((unitType, isRecruiting), group) in _tempUnitGroup)
            {
                if (!isRecruiting) continue; // Dann Recruiting
                var unitIcon = TextureManager.GetMilitaryUnit(unitType);
                DrawUnitMarker(screenPos, offsetX, group, unitIcon, playerCountryId, true, false, selectedUnit);
                offsetX += 36 * Zoom * 0.5f;
            }

            // Listen zum Pool zurueckgeben
            _tempProvinceListPool.Push(units);
        }

        // Zeichne bewegende Einheiten mit Animation
        foreach (var unit in _tempMovingUnits)
        {
            DrawMovingUnit(unit, playerCountryId, selectedUnit);
        }

        // Zeichne Engaging-Verbindungen und Bullet-Effekte
        DrawEngagingEffects(allUnits);

        // Zeichne Auswahl-Indikator wenn eine Einheit ausgewaehlt ist
        if (selectedUnit != null && Provinces.TryGetValue(selectedUnit.ProvinceId, out var selProvince))
        {
            Vector2 selMapPos = selProvince.LabelPosition;
            Vector2 selScreenPos = MapToScreen(selMapPos);
            float selMarkerSize = 2 * Zoom;

            // Pulsierender Auswahlkreis
            float pulse = (float)(Math.Sin(Raylib.GetTime() * 4) * 0.3 + 0.7);
            Color selColor = new Color((byte)255, (byte)255, (byte)100, (byte)(200 * pulse));
            Raylib.DrawCircleLines((int)selScreenPos.X, (int)selScreenPos.Y, selMarkerSize * 1.2f, selColor);
            Raylib.DrawCircleLines((int)selScreenPos.X, (int)selScreenPos.Y, selMarkerSize * 1.4f, selColor);
        }
    }

    /// <summary>
    /// Zeichnet eine bewegende Einheit mit interpolierter Position
    /// </summary>
    private void DrawMovingUnit(MilitaryUnit unit, string? playerCountryId, MilitaryUnit? selectedUnit)
    {
        // Hole Start- und Zielprovinz
        string? startId = unit.StartProvinceId ?? unit.ProvinceId;
        string? targetId = unit.TargetProvinceId;

        if (targetId == null) return;
        if (!Provinces.TryGetValue(startId, out var startProvince)) return;
        if (!Provinces.TryGetValue(targetId, out var targetProvince)) return;

        // Synchronisiere visuellen Fortschritt mit Spielfortschritt
        // Direkte Synchronisation fuer konsistente Geschwindigkeit bei jeder Spielgeschwindigkeit
        unit.VisualProgress = unit.GetMovementProgress();

        // Interpoliere Map-Position mit visuellem Fortschritt
        Vector2 startMapPos = startProvince.LabelPosition;
        Vector2 targetMapPos = targetProvince.LabelPosition;
        Vector2 currentMapPos = Vector2.Lerp(startMapPos, targetMapPos, unit.VisualProgress);

        // Konvertiere zu Screen-Position
        Vector2 startScreenPos = MapToScreen(startMapPos);
        Vector2 targetScreenPos = MapToScreen(targetMapPos);
        Vector2 currentScreenPos = MapToScreen(currentMapPos);

        // Zeichne Bewegungslinie (gestrichelt)
        Color lineColor = new Color((byte)100, (byte)150, (byte)255, (byte)150);
        DrawDashedLine(startScreenPos, targetScreenPos, lineColor, 8, 4);

        // Zeichne Zielpunkt
        Raylib.DrawCircle((int)targetScreenPos.X, (int)targetScreenPos.Y, 4 * Zoom * 0.3f, lineColor);

        // Zeichne Einheit an interpolierter Position
        var unitIcon = TextureManager.GetMilitaryUnit(unit.Type);
        DrawUnitMarkerAtPosition(currentScreenPos, new List<MilitaryUnit> { unit }, unitIcon, playerCountryId, false, true, selectedUnit);
    }

    /// <summary>
    /// Zeichnet eine gestrichelte Linie
    /// </summary>
    private void DrawDashedLine(Vector2 start, Vector2 end, Color color, float dashLength, float gapLength)
    {
        Vector2 direction = end - start;
        float totalLength = direction.Length();
        if (totalLength < 1) return;

        direction = Vector2.Normalize(direction);
        float currentPos = 0;
        bool drawing = true;

        while (currentPos < totalLength)
        {
            float segmentLength = drawing ? dashLength : gapLength;
            float endPos = Math.Min(currentPos + segmentLength, totalLength);

            if (drawing)
            {
                Vector2 segmentStart = start + direction * currentPos;
                Vector2 segmentEnd = start + direction * endPos;
                Raylib.DrawLineV(segmentStart, segmentEnd, color);
            }

            currentPos = endPos;
            drawing = !drawing;
        }
    }

    /// <summary>
    /// Zeichnet einen Einheiten-Marker an einer bestimmten Position (fuer bewegende Einheiten)
    /// </summary>
    private void DrawUnitMarkerAtPosition(Vector2 pos, List<MilitaryUnit> units, Texture2D? icon, string? playerCountryId, bool isRecruiting, bool isMoving, MilitaryUnit? selectedUnit)
    {
        if (units.Count == 0) return;

        // Marker-Groesse skaliert mit Zoom
        float markerSize = 2 * Zoom;

        // Hintergrund-Kreis
        bool isPlayerUnit = units[0].CountryId == playerCountryId;
        Color bgColor = isMoving
            ? new Color((byte)40, (byte)60, (byte)100, (byte)200)
            : isPlayerUnit
                ? new Color((byte)60, (byte)100, (byte)60, (byte)220)
                : new Color((byte)100, (byte)60, (byte)60, (byte)220);

        Raylib.DrawCircle((int)pos.X, (int)pos.Y, markerSize * 0.6f, bgColor);

        // Rahmen
        Color borderColor = isMoving
            ? new Color((byte)100, (byte)150, (byte)255, (byte)255)
            : isPlayerUnit
                ? ColorPalette.Green
                : ColorPalette.Red;
        Raylib.DrawCircleLines((int)pos.X, (int)pos.Y, markerSize * 0.6f, borderColor);

        // Icon zeichnen
        if (icon != null)
        {
            float iconSize = markerSize * 0.8f;
            Rectangle srcRect = new Rectangle(0, 0, icon.Value.Width, icon.Value.Height);
            Rectangle destRect = new Rectangle(pos.X - iconSize / 2, pos.Y - iconSize / 2, iconSize, iconSize);
            Raylib.DrawTexturePro(icon.Value, srcRect, destRect, Vector2.Zero, 0, Color.White);
        }

        // Progress-Bar fuer bewegende Einheiten
        if (isMoving && units.Count > 0)
        {
            var firstUnit = units[0];
            float progress = firstUnit.GetMovementProgress();

            int barW = (int)(markerSize * 1.2f);
            int barH = 4;
            int barX = (int)(pos.X - barW / 2);
            int barY = (int)(pos.Y + markerSize * 0.5f);

            Color barColor = new Color((byte)100, (byte)150, (byte)255, (byte)255);
            Raylib.DrawRectangle(barX, barY, barW, barH, ColorPalette.PanelLight);
            Raylib.DrawRectangle(barX, barY, (int)(barW * progress), barH, barColor);
        }
    }

    /// <summary>
    /// Prueft ob eine Einheit an der angegebenen Screen-Position angeklickt wurde
    /// </summary>
    public MilitaryUnit? GetUnitAtScreenPosition(Vector2 screenPos, MilitaryManager? militaryManager, string? playerCountryId)
    {
        if (militaryManager == null) return null;

        var allUnits = militaryManager.GetAllUnits();
        if (allUnits.Count == 0) return null;

        float markerSize = 2 * Zoom;
        float clickRadius = Math.Max(20, markerSize * 3); // Mindestens 20 Pixel fuer einfaches Klicken

        // Pruefe zuerst bewegende Einheiten (an interpolierter Position)
        for (int i = 0; i < allUnits.Count; i++)
        {
            var unit = allUnits[i];
            if (unit.Status != UnitStatus.Moving || unit.CountryId != playerCountryId) continue;
            Vector2 unitPos = GetMovingUnitScreenPosition(unit);
            float distance = Vector2.Distance(screenPos, unitPos);
            if (distance <= clickRadius)
                return unit;
        }

        // Gruppiere stationaere eigene Einheiten nach Provinz
        var unitsByProvince = new Dictionary<string, List<MilitaryUnit>>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            var unit = allUnits[i];
            if (unit.Status == UnitStatus.Moving || unit.CountryId != playerCountryId) continue;
            if (!unitsByProvince.TryGetValue(unit.ProvinceId, out var list))
            {
                list = new List<MilitaryUnit>();
                unitsByProvince[unit.ProvinceId] = list;
            }
            list.Add(unit);
        }

        // Pruefe stationaere Einheiten pro Provinz
        foreach (var (provinceId, units) in unitsByProvince)
        {
            if (!Provinces.TryGetValue(provinceId, out var province))
                continue;

            Vector2 mapPos = province.LabelPosition;
            Vector2 unitScreenPos = MapToScreen(mapPos);

            // Trenne nach Status und gruppiere nach Typ
            float offsetX = 0;
            UnitType lastReadyType = (UnitType)(-1);
            MilitaryUnit? firstReadyOfType = null;

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Status != UnitStatus.Ready) continue;
                if (u.Type != lastReadyType)
                {
                    if (firstReadyOfType != null)
                    {
                        Vector2 markerPos = new Vector2(unitScreenPos.X + offsetX, unitScreenPos.Y);
                        if (Vector2.Distance(screenPos, markerPos) <= clickRadius)
                            return firstReadyOfType;
                        offsetX += 36 * Zoom * 0.5f;
                    }
                    lastReadyType = u.Type;
                    firstReadyOfType = u;
                }
            }
            if (firstReadyOfType != null)
            {
                Vector2 markerPos = new Vector2(unitScreenPos.X + offsetX, unitScreenPos.Y);
                if (Vector2.Distance(screenPos, markerPos) <= clickRadius)
                    return firstReadyOfType;
                offsetX += 36 * Zoom * 0.5f;
            }

            UnitType lastRecrType = (UnitType)(-1);
            MilitaryUnit? firstRecrOfType = null;

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Status != UnitStatus.Recruiting) continue;
                if (u.Type != lastRecrType)
                {
                    if (firstRecrOfType != null)
                    {
                        Vector2 markerPos = new Vector2(unitScreenPos.X + offsetX, unitScreenPos.Y);
                        if (Vector2.Distance(screenPos, markerPos) <= clickRadius)
                            return firstRecrOfType;
                        offsetX += 36 * Zoom * 0.5f;
                    }
                    lastRecrType = u.Type;
                    firstRecrOfType = u;
                }
            }
            if (firstRecrOfType != null)
            {
                Vector2 markerPos = new Vector2(unitScreenPos.X + offsetX, unitScreenPos.Y);
                if (Vector2.Distance(screenPos, markerPos) <= clickRadius)
                    return firstRecrOfType;
            }
        }

        return null;
    }

    /// <summary>
    /// Berechnet die aktuelle Screen-Position einer bewegenden Einheit
    /// </summary>
    private Vector2 GetMovingUnitScreenPosition(MilitaryUnit unit)
    {
        string? startId = unit.StartProvinceId ?? unit.ProvinceId;
        string? targetId = unit.TargetProvinceId;

        if (targetId == null || !Provinces.TryGetValue(startId, out var startProvince) || !Provinces.TryGetValue(targetId, out var targetProvince))
        {
            // Fallback: aktuelle Provinz-Position
            if (Provinces.TryGetValue(unit.ProvinceId, out var currentProvince))
                return MapToScreen(currentProvince.LabelPosition);
            return Vector2.Zero;
        }

        // Nutze visuellen Fortschritt fuer konsistente Position
        Vector2 currentMapPos = Vector2.Lerp(startProvince.LabelPosition, targetProvince.LabelPosition, unit.VisualProgress);
        return MapToScreen(currentMapPos);
    }

    private void DrawUnitMarker(Vector2 basePos, float offsetX, List<MilitaryUnit> units, Texture2D? icon, string? playerCountryId, bool isRecruiting, bool isMoving, MilitaryUnit? selectedUnit)
    {
        if (units.Count == 0) return;

        // Position anpassen (direkt im Provinz-Zentrum)
        Vector2 pos = new Vector2(basePos.X + offsetX, basePos.Y);

        // Marker-Groesse skaliert mit Zoom (klein gehalten)
        float markerSize = 2 * Zoom;

        bool isPlayerUnit = units[0].CountryId == playerCountryId;
        bool isInCombat = units[0].Status == UnitStatus.InCombat;

        // Hintergrund-Kreis
        Color bgColor;
        if (isInCombat)
        {
            // Kampf: Rot/Orange pulsierend
            float pulse = (float)(Math.Sin(Raylib.GetTime() * 6) * 0.3 + 0.7);
            byte r = (byte)(180 * pulse);
            byte g = (byte)(60 * pulse);
            bgColor = new Color(r, g, (byte)30, (byte)240);
        }
        else if (isRecruiting)
            bgColor = new Color((byte)80, (byte)80, (byte)40, (byte)200);
        else if (isMoving)
            bgColor = new Color((byte)40, (byte)60, (byte)100, (byte)200);
        else if (isPlayerUnit)
            bgColor = new Color((byte)60, (byte)100, (byte)60, (byte)220);
        else
            bgColor = new Color((byte)100, (byte)60, (byte)60, (byte)220);

        Raylib.DrawCircle((int)pos.X, (int)pos.Y, markerSize * 0.6f, bgColor);

        // Kampf-Glüheffekt
        if (isInCombat)
        {
            float glow = (float)(Math.Sin(Raylib.GetTime() * 8) * 0.4 + 0.6);
            byte ga = (byte)(80 * glow);
            Raylib.DrawCircle((int)pos.X, (int)pos.Y, markerSize * 1.0f, new Color((byte)255, (byte)100, (byte)30, ga));
        }

        // Rahmen
        Color borderColor;
        if (isInCombat)
            borderColor = new Color((byte)255, (byte)150, (byte)50, (byte)255);
        else if (isRecruiting)
            borderColor = ColorPalette.Yellow;
        else if (isMoving)
            borderColor = new Color((byte)100, (byte)150, (byte)255, (byte)255);
        else if (isPlayerUnit)
            borderColor = ColorPalette.Green;
        else
            borderColor = ColorPalette.Red;
        Raylib.DrawCircleLines((int)pos.X, (int)pos.Y, markerSize * 0.6f, borderColor);

        // Icon zeichnen
        if (icon != null)
        {
            float iconSize = markerSize * 0.8f;
            Rectangle srcRect = new Rectangle(0, 0, icon.Value.Width, icon.Value.Height);
            Rectangle destRect = new Rectangle(pos.X - iconSize / 2, pos.Y - iconSize / 2, iconSize, iconSize);
            Raylib.DrawTexturePro(icon.Value, srcRect, destRect, Vector2.Zero, 0, Color.White);
        }

        // Anzahl anzeigen (nur wenn > 1)
        if (units.Count > 1)
        {
            string countText = units.Count.ToString();
            int fontSize = (int)(markerSize * 0.4f);
            fontSize = Math.Max(8, Math.Min(14, fontSize));

            // Hintergrund fuer Zahl
            int textW = Raylib.MeasureText(countText, fontSize);
            int textX = (int)(pos.X + markerSize * 0.4f);
            int textY = (int)(pos.Y - markerSize * 0.5f);

            Raylib.DrawCircle(textX + textW / 2, textY + fontSize / 2, fontSize * 0.7f, ColorPalette.Panel);
            Raylib.DrawText(countText, textX, textY, fontSize, ColorPalette.TextWhite);
        }

        // Progress-Bar fuer rekrutierende oder bewegende Einheiten
        if ((isRecruiting || isMoving) && units.Count > 0)
        {
            var firstUnit = units[0];
            float progress = isRecruiting ? firstUnit.GetRecruitmentProgress() : firstUnit.GetMovementProgress();

            int barW = (int)(markerSize * 1.2f);
            int barH = 4;
            int barX = (int)(pos.X - barW / 2);
            int barY = (int)(pos.Y + markerSize * 0.5f);

            Color barColor = isMoving ? new Color((byte)100, (byte)150, (byte)255, (byte)255) : ColorPalette.Yellow;
            Raylib.DrawRectangle(barX, barY, barW, barH, ColorPalette.PanelLight);
            Raylib.DrawRectangle(barX, barY, (int)(barW * progress), barH, barColor);
        }

        // HP-Bar fuer Einheiten im Kampf (Organisation)
        if (isInCombat && units.Count > 0)
        {
            var firstUnit = units[0];
            float orgRatio = firstUnit.Organization;
            float hpRatio = firstUnit.MaxManpower > 0 ? (float)firstUnit.Manpower / firstUnit.MaxManpower : 0;

            int barW = (int)(markerSize * 1.4f);
            int barH = 3;
            int barX = (int)(pos.X - barW / 2);

            // Organisation (gelb)
            int orgBarY = (int)(pos.Y + markerSize * 0.55f);
            Raylib.DrawRectangle(barX, orgBarY, barW, barH, ColorPalette.PanelLight);
            Raylib.DrawRectangle(barX, orgBarY, (int)(barW * orgRatio), barH, ColorPalette.Yellow);

            // HP (gruen/rot)
            int hpBarY = orgBarY + barH + 1;
            Color hpColor = hpRatio > 0.5f ? ColorPalette.Green : hpRatio > 0.25f ? ColorPalette.Yellow : ColorPalette.Red;
            Raylib.DrawRectangle(barX, hpBarY, barW, barH, ColorPalette.PanelLight);
            Raylib.DrawRectangle(barX, hpBarY, (int)(barW * hpRatio), barH, hpColor);
        }
    }

    /// <summary>
    /// Zeichnet Engaging-Verbindungen (gestrichelte rote Linie + Bullets) zwischen
    /// Angreifer-Provinz und Zielprovinz
    /// </summary>
    private void DrawEngagingEffects(IReadOnlyList<MilitaryUnit> allUnits)
    {
        double currentTime = Raylib.GetTime();
        float deltaTime = Raylib.GetFrameTime();
        bool shouldSpawn = currentTime - _lastBulletSpawnTime > 0.06; // Alle 60ms neue Kugeln

        // Finde alle Engaging-Einheiten und zeichne Verbindungslinien
        for (int i = 0; i < allUnits.Count; i++)
        {
            var unit = allUnits[i];
            if (unit.Status != UnitStatus.Engaging || unit.EngageTargetProvinceId == null)
                continue;

            if (!Provinces.TryGetValue(unit.ProvinceId, out var attackerProv))
                continue;
            if (!Provinces.TryGetValue(unit.EngageTargetProvinceId, out var targetProv))
                continue;

            Vector2 attackerScreen = MapToScreen(attackerProv.LabelPosition);
            Vector2 targetScreen = MapToScreen(targetProv.LabelPosition);

            // Gestrichelte rote Angriffslinie
            Color attackLineColor = new Color((byte)255, (byte)80, (byte)50, (byte)120);
            DrawDashedLine(attackerScreen, targetScreen, attackLineColor, 6, 4);

            // Angriffspfeil am Ziel
            Vector2 dir = Vector2.Normalize(targetScreen - attackerScreen);
            float arrowSize = Zoom * 2;
            Vector2 arrowTip = targetScreen - dir * (Zoom * 3);
            Vector2 arrowLeft = arrowTip - dir * arrowSize + new Vector2(-dir.Y, dir.X) * arrowSize * 0.5f;
            Vector2 arrowRight = arrowTip - dir * arrowSize + new Vector2(dir.Y, -dir.X) * arrowSize * 0.5f;
            Raylib.DrawTriangle(arrowTip, arrowRight, arrowLeft, attackLineColor);

            // Spawne Bullets entlang der Linie
            if (shouldSpawn)
            {
                float distance = Vector2.Distance(attackerScreen, targetScreen);
                float speed = 120 + (float)(Random.Shared.NextDouble() * 60);

                // Kugel vom Angreifer zum Ziel
                float spread = (float)(Random.Shared.NextDouble() - 0.5) * 10;
                Vector2 perpendicular = new Vector2(-dir.Y, dir.X);
                _bulletParticles.Add(new BulletParticle
                {
                    Position = attackerScreen + perpendicular * spread,
                    Velocity = dir * speed,
                    Life = Math.Min(1.0f, distance / speed),
                    Type = 0
                });

                // Muendungsfeuer am Angreifer
                _bulletParticles.Add(new BulletParticle
                {
                    Position = attackerScreen + perpendicular * spread,
                    Velocity = dir * 5,
                    Life = 0.3f,
                    Type = 1
                });

                // Gegenkugel vom Verteidiger zum Angreifer (seltener)
                if (Random.Shared.NextDouble() < 0.6)
                {
                    float spread2 = (float)(Random.Shared.NextDouble() - 0.5) * 10;
                    _bulletParticles.Add(new BulletParticle
                    {
                        Position = targetScreen + perpendicular * spread2,
                        Velocity = -dir * (speed * 0.8f),
                        Life = Math.Min(1.0f, distance / (speed * 0.8f)),
                        Type = 0
                    });
                }

                // Einschlag-Funken am Ziel
                if (Random.Shared.NextDouble() < 0.4)
                {
                    float sparkAngle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                    _bulletParticles.Add(new BulletParticle
                    {
                        Position = targetScreen + new Vector2(
                            (float)(Random.Shared.NextDouble() - 0.5) * Zoom * 3,
                            (float)(Random.Shared.NextDouble() - 0.5) * Zoom * 3),
                        Velocity = new Vector2(MathF.Cos(sparkAngle) * 25, MathF.Sin(sparkAngle) * 25),
                        Life = 0.5f,
                        Type = 2
                    });
                }
            }
        }

        if (shouldSpawn)
            _lastBulletSpawnTime = currentTime;

        // Update und zeichne Partikel
        for (int i = _bulletParticles.Count - 1; i >= 0; i--)
        {
            var p = _bulletParticles[i];
            p.Life -= deltaTime * 2.0f;

            if (p.Life <= 0)
            {
                _bulletParticles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * deltaTime;
            _bulletParticles[i] = p;

            byte alpha = (byte)(Math.Min(1f, p.Life * 2) * 255);

            switch (p.Type)
            {
                case 0: // Kugel: gelber Leuchtpunkt mit Trail
                    Color bulletColor = new Color((byte)255, (byte)240, (byte)150, alpha);
                    Raylib.DrawCircle((int)p.Position.X, (int)p.Position.Y, 1.5f, bulletColor);
                    Color trailColor = new Color((byte)255, (byte)200, (byte)100, (byte)(alpha / 3));
                    Raylib.DrawCircle((int)p.Position.X, (int)p.Position.Y, 3f, trailColor);
                    break;

                case 1: // Muendungsfeuer: orange Blitz
                    byte mr = (byte)(255 * Math.Min(1f, p.Life * 3));
                    byte mg = (byte)(180 * Math.Min(1f, p.Life * 3));
                    Color muzzleColor = new Color(mr, mg, (byte)50, alpha);
                    Raylib.DrawCircle((int)p.Position.X, (int)p.Position.Y, 3f * p.Life * 3, muzzleColor);
                    break;

                case 2: // Einschlag-Funken: rote Funken
                    Color sparkColor = new Color((byte)255, (byte)(100 + (int)(100 * p.Life)), (byte)30, alpha);
                    Raylib.DrawCircle((int)p.Position.X, (int)p.Position.Y, 2f * p.Life * 2, sparkColor);
                    break;
            }
        }
    }
}
