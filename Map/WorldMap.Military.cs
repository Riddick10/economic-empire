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
        // Auswahl-Hervorhebung erfolgt direkt im Glas-Chit (Gold-Ring), nicht mehr separat.
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

        // VisualProgress wird zentral in MilitaryManager.UpdateMovementAnimation
        // fluessig (an die Simulationsuhr gekoppelt) gesetzt - hier nur lesen.
        float t = Math.Clamp(unit.VisualProgress, 0f, 1f);

        Vector2 startScreen = MapToScreen(startProvince.LabelPosition);
        Vector2 targetScreen = MapToScreen(targetProvince.LabelPosition);
        Vector2 curScreen = Vector2.Lerp(startScreen, targetScreen, t);

        double time = Raylib.GetTime();
        float th = Math.Max(1.5f, Zoom * 0.32f);

        Color routeDim = new Color((byte)70, (byte)120, (byte)210, (byte)70);   // Gesamtroute, gedimmt
        Color routeGlow = new Color((byte)130, (byte)175, (byte)255, (byte)225); // fliessende Marschlinie
        Color destCol = new Color((byte)140, (byte)185, (byte)255, (byte)235);   // Zielmarker

        // 1) Gedimmte Gesamtroute als ruhige Basis
        Raylib.DrawLineEx(startScreen, targetScreen, th * 0.55f, routeDim);

        // 2) Animierte Marschlinie auf dem verbleibenden Weg, fliesst zum Ziel
        float dash = Math.Max(6f, Zoom * 0.9f);
        float gap = Math.Max(5f, Zoom * 0.8f);
        float phase = (float)(time * (Zoom * 3f + 22f));   // px/s -> Marschgeschwindigkeit
        DrawDashedLine(curScreen, targetScreen, routeGlow, dash, gap, th, phase);

        // 3) Zielmarker: pulsierender Doppelring + Kern (Fadenkreuz-Anmutung)
        float pulse = (float)(Math.Sin(time * 3.0) * 0.5 + 0.5);
        float rOuter = (2.6f + pulse * 2.0f) * Zoom * 0.42f;
        Raylib.DrawCircleLines((int)targetScreen.X, (int)targetScreen.Y, rOuter, destCol);
        Raylib.DrawCircleLines((int)targetScreen.X, (int)targetScreen.Y, rOuter * 0.55f, destCol);
        Raylib.DrawCircle((int)targetScreen.X, (int)targetScreen.Y, Math.Max(1.5f, Zoom * 0.24f), destCol);

        // 4) Einheit an interpolierter Position (Glas-Chit im Moving-Zustand)
        var unitIcon = TextureManager.GetMilitaryUnit(unit.Type);
        DrawUnitMarkerAtPosition(curScreen, new List<MilitaryUnit> { unit }, unitIcon, playerCountryId, false, true, selectedUnit);
    }

    /// <summary>
    /// Zeichnet eine gestrichelte Linie. Mit <paramref name="phase"/> (in Pixeln)
    /// wandert das Strichmuster Richtung Endpunkt ("marschierende Ameisen").
    /// </summary>
    private void DrawDashedLine(Vector2 start, Vector2 end, Color color, float dashLength, float gapLength, float thickness = 1f, float phase = 0f)
    {
        Vector2 delta = end - start;
        float totalLength = delta.Length();
        if (totalLength < 1) return;

        Vector2 direction = delta / totalLength;
        float pattern = dashLength + gapLength;
        if (pattern <= 0.01f) return;

        // Muster um phase Richtung Ende verschieben; eine Periode vor 0 starten,
        // damit der einlaufende Strich sauber sichtbar ist.
        float offset = phase % pattern;
        for (float pos = offset - pattern; pos < totalLength; pos += pattern)
        {
            float a = Math.Max(0f, pos);
            float b = Math.Min(totalLength, pos + dashLength);
            if (b > a)
                Raylib.DrawLineEx(start + direction * a, start + direction * b, thickness, color);
        }
    }

    /// <summary>
    /// Zeichnet einen Einheiten-Marker an einer bestimmten Position (fuer bewegende Einheiten)
    /// </summary>
    /// <summary>
    /// Zeichnet einen Einheiten-Marker als "Glas-Chit": dunkles Glas-Token mit
    /// Team-/Status-farbenem Rand, hellem Icon, goldenem Anzahl-Badge und
    /// optionalem Auswahl-Ring. Passt zum Living-World-Glas-Stil der UI.
    /// </summary>
    private void DrawUnitChit(Vector2 pos, UnitType type, int count, bool isPlayer,
        bool isRecruiting, bool isMoving, bool isInCombat, bool isSelected, float markerSize,
        string? countryId = null)
    {
        float chit = markerSize * 1.25f;
        float half = chit / 2f;
        Rectangle rect = new Rectangle(pos.X - half, pos.Y - half, chit, chit);
        const float round = 0.30f;
        const int seg = 6;

        // Status-/Team-Farbe traegt den Rand
        Color team;
        if (isInCombat)
        {
            float pulse = (float)(Math.Sin(Raylib.GetTime() * 6) * 0.35 + 0.65);
            team = new Color((byte)255, (byte)Math.Clamp(120 * pulse + 60, 0, 255), (byte)40, (byte)255);
            float glow = (float)(Math.Sin(Raylib.GetTime() * 8) * 0.4 + 0.6);
            Raylib.DrawCircle((int)pos.X, (int)pos.Y, chit * 0.85f,
                new Color((byte)255, (byte)100, (byte)30, (byte)(70 * glow)));
        }
        else if (isRecruiting) team = ColorPalette.Yellow;
        else if (isMoving) team = new Color((byte)90, (byte)150, (byte)255, (byte)255);
        else if (isPlayer) team = new Color((byte)90, (byte)200, (byte)110, (byte)255);
        else team = new Color((byte)225, (byte)90, (byte)80, (byte)255);

        // Schlagschatten
        Raylib.DrawRectangleRounded(new Rectangle(rect.X + chit * 0.06f, rect.Y + chit * 0.12f, chit, chit),
            round, seg, new Color((byte)0, (byte)0, (byte)0, (byte)110));

        // Dunkles Glas + feine Lichtkante oben
        Raylib.DrawRectangleRounded(rect, round, seg, new Color((byte)12, (byte)16, (byte)27, (byte)238));
        Raylib.DrawRectangle((int)(rect.X + chit * 0.18f), (int)(rect.Y + chit * 0.09f),
            (int)(chit * 0.64f), 1, new Color((byte)255, (byte)255, (byte)255, (byte)32));

        // Team-Rand
        float lineTh = Math.Max(1.5f, chit * 0.06f);
        Raylib.DrawRectangleRoundedLinesEx(rect, round, seg, lineTh, team);

        // Helles Icon (weisse Silhouette liest sich sauber auf dem dunklen Glas)
        var white = TextureManager.GetMilitaryUnitOutline(type);
        if (white != null)
        {
            float iconSize = chit * 0.66f;
            Rectangle isrc = new Rectangle(0, 0, white.Value.Width, white.Value.Height);
            Rectangle idst = new Rectangle(pos.X - iconSize / 2, pos.Y - iconSize / 2, iconSize, iconSize);
            Raylib.DrawTexturePro(white.Value, isrc, idst, Vector2.Zero, 0, Color.White);
        }

        // Nationalflagge als kleine gerahmte Fahne unten links (haengt leicht ueber die Ecke)
        var flag = string.IsNullOrEmpty(countryId) ? null : TextureManager.GetFlag(countryId);
        if (flag != null && flag.Value.Width > 0)
        {
            float fw = chit * 0.30f;
            float fh = fw * flag.Value.Height / flag.Value.Width;   // Original-Seitenverhaeltnis erhalten
            float fx = rect.X - fw * 0.15f;
            float fy = rect.Y + rect.Height - fh + fh * 0.15f;
            Rectangle fdst = new Rectangle(fx, fy, fw, fh);

            float fb = Math.Max(1f, chit * 0.022f);
            // Schlagschatten
            Raylib.DrawRectangle((int)(fx + 1), (int)(fy + 2), (int)fw, (int)fh, new Color((byte)0, (byte)0, (byte)0, (byte)120));
            // Dunkle Fassung als Rahmen
            Raylib.DrawRectangle((int)(fx - fb), (int)(fy - fb), (int)(fw + fb * 2), (int)(fh + fb * 2),
                new Color((byte)12, (byte)16, (byte)27, (byte)255));
            // Flagge
            Rectangle fsrc = new Rectangle(0, 0, flag.Value.Width, flag.Value.Height);
            Raylib.DrawTexturePro(flag.Value, fsrc, fdst, Vector2.Zero, 0, Color.White);
        }

        // Anzahl-Badge oben rechts (Gold-Rand)
        if (count > 1)
        {
            float bx = rect.X + rect.Width;
            float by = rect.Y;
            float br = chit * 0.24f;
            Raylib.DrawCircle((int)bx, (int)by, br, new Color((byte)12, (byte)16, (byte)27, (byte)255));
            Raylib.DrawCircleLines((int)bx, (int)by, br, ColorPalette.Accent);
            Raylib.DrawCircleLines((int)bx, (int)by, br - 1, ColorPalette.Accent);
            int fs = Math.Max(8, (int)(br * 1.2f));
            string s = count.ToString();
            int tw = Program.MeasureGameText(s, fs);
            Program.DrawGameText(s, (int)(bx - tw / 2f), (int)(by - fs / 2f), fs, ColorPalette.TextWhite);
        }

        // Auswahl: pulsierender Gold-Ring
        if (isSelected)
        {
            float pulse = (float)(Math.Sin(Raylib.GetTime() * 4) * 0.3 + 0.7);
            Rectangle sel = new Rectangle(rect.X - lineTh, rect.Y - lineTh,
                rect.Width + lineTh * 2, rect.Height + lineTh * 2);
            Raylib.DrawRectangleRoundedLinesEx(sel, round, seg, lineTh,
                new Color((byte)230, (byte)185, (byte)80, (byte)(255 * pulse)));
        }
    }

    private void DrawUnitMarkerAtPosition(Vector2 pos, List<MilitaryUnit> units, Texture2D? icon, string? playerCountryId, bool isRecruiting, bool isMoving, MilitaryUnit? selectedUnit)
    {
        if (units.Count == 0) return;

        float markerSize = 2 * Zoom;
        bool isPlayerUnit = units[0].CountryId == playerCountryId;
        bool isSelected = selectedUnit != null && units.Contains(selectedUnit);
        DrawUnitChit(pos, units[0].Type, units.Count, isPlayerUnit, isRecruiting, isMoving,
            units[0].Status == UnitStatus.InCombat, isSelected, markerSize, units[0].CountryId);
        // Kein separater Fortschrittsbalken: die animierte Route + das gleichmaessige
        // Gleiten der Einheit zeigen den Bewegungsfortschritt deutlich genug.
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

        bool isInCombat = units[0].Status == UnitStatus.InCombat;
        bool isPlayerUnit = units[0].CountryId == playerCountryId;
        bool isSelected = selectedUnit != null && units.Contains(selectedUnit);

        // Glas-Chit-Marker (Team-Rand, helles Icon, Nationalflagge, Anzahl-Badge, Auswahl-Ring)
        DrawUnitChit(pos, units[0].Type, units.Count, isPlayerUnit, isRecruiting, isMoving, isInCombat, isSelected, markerSize, units[0].CountryId);

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
