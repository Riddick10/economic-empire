using Raylib_cs;
using System.Numerics;
using GrandStrategyGame.Systems.Managers;
using GrandStrategyGame.Map;
using GrandStrategyGame.Models;
using GrandStrategyGame.UI;
using GrandStrategyGame.UI.Panels;

namespace GrandStrategyGame;

/// <summary>
/// Program - Update-Logik und Benutzerinteraktion
/// </summary>
partial class Program
{
    // Screen-State -> siehe ui (UIState)

    /// <summary>
    /// Prueft ob ein Nachrichten-Popup (Handy) aktiv ist - blockiert dann ALLE Klicks
    /// </summary>
    static bool HasActiveNotificationPopup()
    {
        var notifMgr = _mgr.Notif;
        if (notifMgr == null) return false;
        return notifMgr.ActivePopups.Count > 0;
    }

    /// <summary>
    /// Prueft ob die Maus ueber einem UI-Element ist (verhindert Durchklicken auf Karte)
    /// </summary>
    static bool IsMouseOverUI()
    {
        Vector2 mousePos = _cachedMousePos;

        // Wenn ein Nachrichten-Popup (Handy) aktiv ist, ALLES blockieren
        if (HasActiveNotificationPopup())
        {
            return true;
        }

        // Top-Bar (Flagge, Landesname, Datum, Geschwindigkeit)
        if (mousePos.Y < 70)
        {
            return true;
        }

        // Mini-Musikplayer (oben rechts unter der Top-Bar)
        if (IsMouseOverMusicPlayer(mousePos))
        {
            return true;
        }

        // Bottom-Bar (Ressourcen-Anzeige)
        if (mousePos.Y > ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT)
        {
            return true;
        }

        // Top-Menu Buttons (links)
        if (IsMouseOverTopMenuButtons())
        {
            return true;
        }

        // Top-Menu Panel (wenn offen)
        if (IsMouseOverTopMenuPanel())
        {
            return true;
        }

        // Notification-Popups (unten rechts) - nur noch fuer alte Popups
        if (IsMouseOverNotificationPopups(mousePos))
        {
            return true;
        }

        // Politik-Panel - nutze korrekte Panel-Koordinaten
        if (ui.ShowPoliticsPanel)
        {
            if (Raylib.CheckCollisionPointRec(mousePos, PoliticsInfoPanel.GetPanelRect()))
            {
                return true;
            }
        }

        // Provinz-Panel (rechte Seite)
        if (ui.ShowProvincePanel)
        {
            if (Raylib.CheckCollisionPointRec(mousePos, ProvinceInfoPanel.GetPanelRect()))
            {
                return true;
            }
        }

        // Kartenansicht-Buttons + Nachrichten-Button + Tutorial-Button (unten rechts)
        {
            int btnSize = 44;
            int btnSpacing = 8;
            int mapBtnY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT - btnSize - 15;
            int framePadding = 8;
            int labelHeight = 20;
            int frameY = mapBtnY - framePadding - labelHeight;
            int newsBtnY = frameY - btnSize - btnSpacing;
            int tutorialBtnY = newsBtnY - btnSize - btnSpacing;
            int econBtnY = tutorialBtnY - btnSize - btnSpacing;
            // Gesamter Bereich: Kartenbuttons + Rahmen + News + Tutorial + Wirtschaft-Button
            int numMapButtons = 4; // Resources, Political, Trade, Alliance
            int areaX = ScreenWidth - btnSize * numMapButtons - btnSpacing * (numMapButtons - 1) - 15;
            int areaW = btnSize * numMapButtons + btnSpacing * (numMapButtons - 1) + 15;
            int areaH = mapBtnY + btnSize - econBtnY;
            Rectangle bottomRightArea = new Rectangle(areaX, econBtnY, areaW, areaH);
            if (Raylib.CheckCollisionPointRec(mousePos, bottomRightArea))
            {
                return true;
            }
        }

        // Tutorial-Panel (zentriert)
        if (ui.ShowTutorialPanel)
        {
            int panelW = 880;
            int panelH = 600;
            int panelX = (ScreenWidth - panelW) / 2;
            int panelY = (ScreenHeight - panelH) / 2;
            if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(panelX, panelY, panelW, panelH)))
            {
                return true;
            }
        }

        // Wirtschafts-Rangliste (zentriert)
        if (ui.ShowEconomyRanking)
        {
            int panelW = 920;
            int panelH = 620;
            int panelX = (ScreenWidth - panelW) / 2;
            int panelY = (ScreenHeight - panelH) / 2;
            if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(panelX, panelY, panelW, panelH)))
            {
                return true;
            }
        }

        return false;
    }

    static void Update()
    {
        // Globale Tastenkuerzel
        if (Raylib.IsKeyPressed(KeyboardKey.F3))
        {
            ui.ShowFPS = !ui.ShowFPS;
        }

        // F11 = Randloses Vollbild umschalten (funktioniert in jedem Bildschirm)
        if (Raylib.IsKeyPressed(KeyboardKey.F11))
        {
            Raylib.ToggleBorderlessWindowed();
        }

        // Screen-basierter Dispatch (mit Enter/Exit-Lifecycle)
        if (_screens.TryGetValue(currentScreen, out var screen))
        {
            if (_lastDispatchedScreen != currentScreen)
            {
                if (_lastDispatchedScreen is { } last && _screens.TryGetValue(last, out var prevScreen))
                    prevScreen.Exit();
                screen.Enter();
                _lastDispatchedScreen = currentScreen;
            }
            screen.Update();
        }
    }

    /// <summary>
    /// Gemeinsame Karteninteraktion (Verschieben, Zoomen, Hover, Auswahl)
    /// </summary>
    static void UpdateMapInteraction()
    {
        // Keine Karteninteraktion wenn Nachrichten-Popup aktiv
        if (HasActiveNotificationPopup())
        {
            ui.HoveredCountryId = null;
            ui.HoveredProvinceId = null;
            return;
        }

        Vector2 mousePos = _cachedMousePos;
        ui.ViewportChanged = false;

        // Karte verschieben mit mittlerer Maustaste
        if (Raylib.IsMouseButtonPressed(MouseButton.Middle))
        {
            ui.IsDragging = true;
            ui.LastMousePos = mousePos;
        }
        if (Raylib.IsMouseButtonReleased(MouseButton.Middle))
        {
            ui.IsDragging = false;
        }
        if (ui.IsDragging)
        {
            Vector2 delta = mousePos - ui.LastMousePos;
            worldMap.Move(delta);
            ui.LastMousePos = mousePos;
            ui.ViewportChanged = true;
        }

        // Zoomen mit Mausrad (nicht wenn Maus ueber UI - verhindert Zoom beim Panel-Scrollen)
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0 && !IsMouseOverUI())
        {
            worldMap.ZoomAt(mousePos, wheel * GameConfig.ZOOM_SPEED);
            ui.ViewportChanged = true;
        }

        // Karte verschieben mit WASD
        float panSpeed = GameConfig.PAN_SPEED;
        Vector2 keyboardPan = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) keyboardPan.Y += panSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.S)) keyboardPan.Y -= panSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.A)) keyboardPan.X += panSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.D)) keyboardPan.X -= panSpeed;
        if (keyboardPan != Vector2.Zero)
        {
            worldMap.Move(keyboardPan);
            ui.ViewportChanged = true;
        }

        // Land und Provinz unter Maus finden - nur wenn Maus bewegt oder Viewport geaendert
        // Und nur wenn nicht ueber UI
        bool mouseMoved = Math.Abs(mousePos.X - ui.LastHoverCheckPos.X) > 0.5f ||
                          Math.Abs(mousePos.Y - ui.LastHoverCheckPos.Y) > 0.5f;

        bool overUI = IsMouseOverUI();

        if ((mouseMoved || ui.ViewportChanged) && !overUI)
        {
            ui.HoveredCountryId = worldMap.GetCountryAtPosition(mousePos);
            ui.HoveredProvinceId = worldMap.GetProvinceAtPosition(mousePos);
            ui.LastHoverCheckPos = mousePos;
        }
        else if (overUI)
        {
            // Hover zuruecksetzen wenn ueber UI
            ui.HoveredCountryId = null;
            ui.HoveredProvinceId = null;
        }

        // Land auswaehlen mit linker Maustaste - nur wenn nicht ueber UI
        // (Provinz-Auswahl wird in der Panel-Logik weiter unten behandelt)
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && ui.HoveredCountryId != null && !overUI)
        {
            ui.SelectedCountryId = ui.HoveredCountryId;
        }
    }

    /// <summary>
    /// Update fuer Save Panel Overlay (im Spiel als Panel).
    /// Layout kommt aus GetSavePanelLayout - dieselbe Quelle wie das Zeichnen.
    /// </summary>
    static void UpdateSavePanelOverlay()
    {
        Vector2 mousePos = _cachedMousePos;
        var l = GetSavePanelLayout();

        for (int i = 0; i < 3; i++)
        {
            ui.SaveSlotRects[i] = l.Slots[i];
            ui.DeleteSlotRects[i] = new Rectangle(
                l.Slots[i].X + l.Slots[i].Width - 46,
                l.Slots[i].Y + 12,
                32, 32);

            ui.SaveSlotHovered[i] = Raylib.CheckCollisionPointRec(mousePos, l.Slots[i]);
            ui.DeleteSlotHovered[i] = ui.SaveSlots[i] != null &&
                Raylib.CheckCollisionPointRec(mousePos, ui.DeleteSlotRects[i]);
        }

        ui.ConfirmSaveButtonRect = l.Confirm;
        ui.ConfirmSaveButtonHovered = ui.SelectedSaveSlot >= 0 &&
            Raylib.CheckCollisionPointRec(mousePos, l.Confirm);

        // Klick-Erkennung
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            // X oder Klick neben das Panel schliesst
            if (Raylib.CheckCollisionPointRec(mousePos, l.Close) ||
                !Raylib.CheckCollisionPointRec(mousePos, l.Panel))
            {
                SoundManager.Play(SoundEffect.Click);
                ui.ShowSavePanel = false;
                return;
            }

            // Speichern bestaetigen
            if (ui.ConfirmSaveButtonHovered && ui.SelectedSaveSlot >= 0)
            {
                SaveGameManager.SaveGame(game, worldMap, ui.SelectedSaveSlot + 1);
                ui.SaveSlots = SaveGameManager.GetAllSlots();
                SoundManager.Play(SoundEffect.NotificationSuccess);
                ui.ShowSavePanel = false;
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                // Loeschen-Button
                if (ui.DeleteSlotHovered[i])
                {
                    SoundManager.Play(SoundEffect.Click);
                    SaveGameManager.DeleteSlot(i + 1);
                    ui.SaveSlots = SaveGameManager.GetAllSlots();
                    if (ui.SelectedSaveSlot == i) ui.SelectedSaveSlot = -1;
                    return;
                }

                // Slot auswaehlen
                if (ui.SaveSlotHovered[i])
                {
                    SoundManager.Play(SoundEffect.Click);
                    ui.SelectedSaveSlot = i;
                    return;
                }
            }
        }

        // ESC schliesst Panel
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            SoundManager.Play(SoundEffect.Click);
            ui.ShowSavePanel = false;
        }
    }

    internal static void UpdatePlaying()
    {
        // Speichern-Panel hat hoechste Prioritaet
        if (ui.ShowSavePanel)
        {
            UpdateSavePanelOverlay();
            return;
        }

        // Pause-Menü hat Prioritaet ueber alles andere
        if (ui.ShowPauseMenu)
        {
            UpdatePauseMenu();
            return;
        }

        // Forschungsbaum blockiert alle anderen Interaktionen
        // (Input wird in DrawTechTree behandelt)
        if (ui.ShowTechTree)
        {
            return;
        }

        // Wenn Nachrichten-Popup aktiv, nur beschraenkte Interaktion erlauben
        // (Die Popup-Interaktion wird in DrawNotificationPopups behandelt)
        if (HasActiveNotificationPopup())
        {
            // Tick-basierte Simulation laeuft weiter (oder pausiert je nach Einstellung)
            double dt = Raylib.GetFrameTime();
            game.Update(dt);
            return; // Keine weitere Interaktion erlauben
        }

        UpdateMapInteraction();

        // UI-Button Klicks pruefen
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            // Fabrik-Bau-Modus: Fabrik in Provinz bauen
            if (ui.FactoryBuildMode != null && ui.HoveredProvinceId != null && !IsMouseOverUI())
            {
                var province = worldMap.GetProvinceById(ui.HoveredProvinceId);
                if (province != null && game.PlayerCountry != null && province.CountryId == game.PlayerCountry.Id)
                {
                    // Kosten pruefen und abziehen
                    double cost = ui.FactoryBuildMode switch
                    {
                        FactoryType.Civilian => 1_000,      // 1 Mrd.
                        FactoryType.Military => 1_500,      // 1.5 Mrd.
                        FactoryType.Dockyard => 900,        // 900 Mio.
                        _ => 0
                    };

                    double machineryStock = game.PlayerCountry.Stockpile.GetValueOrDefault(ResourceType.Machinery, 0);
                    if (game.PlayerCountry.Budget >= cost && machineryStock >= 5)
                    {
                        game.PlayerCountry.Budget -= cost;
                        game.PlayerCountry.UseResource(ResourceType.Machinery, 5);
                        SoundManager.Play(SoundEffect.Coin);
                        SoundManager.Play(SoundEffect.Build);

                        // Fabrik zur Provinz hinzufuegen (immer 10er Schritte)
                        switch (ui.FactoryBuildMode)
                        {
                            case FactoryType.Civilian:
                                province.CivilianFactories += 10;
                                break;
                            case FactoryType.Military:
                                province.MilitaryFactories += 10;
                                break;
                            case FactoryType.Dockyard:
                                province.Dockyards += 10;
                                break;
                        }

                        // Auch im ProductionManager aktualisieren (fuer Gesamtzahlen)
                        _mgr.Production?.BuildFactory(game.PlayerCountry.Id, ui.FactoryBuildMode.Value);

                        // Bau-Modus bleibt aktiv fuer weitere Platzierungen
                        return; // Keine weitere Verarbeitung
                    }
                    else
                    {
                        // Nicht genug Budget oder Maschinen - Bau-Modus trotzdem beenden
                        ui.FactoryBuildMode = null;
                        return;
                    }
                }
            }

            // Minen-Bau-Modus: Mine in Provinz bauen
            if (ui.MineBuildMode != null && ui.HoveredProvinceId != null && !IsMouseOverUI())
            {
                var province = worldMap.GetProvinceById(ui.HoveredProvinceId);
                if (province != null && game.PlayerCountry != null && province.CountryId == game.PlayerCountry.Id)
                {
                    // Prüfe ob bereits eine Mine in der Provinz existiert (max 1 pro Provinz)
                    if (province.Mines.Count == 0)
                    {
                        double cost = Mine.GetBuildCost(ui.MineBuildMode.Value);

                        if (game.PlayerCountry.Budget >= cost)
                        {
                            game.PlayerCountry.Budget -= cost;
                            SoundManager.Play(SoundEffect.Coin);
                            SoundManager.Play(SoundEffect.Build);

                            // Mine zur Provinz hinzufuegen
                            province.Mines.Add(new Mine(ui.MineBuildMode.Value));

                            // Bau-Modus bleibt aktiv fuer weitere Platzierungen
                            return;
                        }
                        else
                        {
                            // Nicht genug Budget - Bau-Modus beenden
                            ui.MineBuildMode = null;
                            return;
                        }
                    }
                    // Provinz hat bereits eine Mine
                }
            }

            // Rekrutierungsmodus - Einheit in Provinz rekrutieren
            if (ui.RecruitmentMode != null && ui.HoveredProvinceId != null && !IsMouseOverUI())
            {
                var province = worldMap.GetProvinceById(ui.HoveredProvinceId);
                if (province != null && game.PlayerCountry != null && province.CountryId == game.PlayerCountry.Id)
                {
                    // Eigene Provinz - rekrutieren
                    var context = game.GetGameContext();
                    if (_mgr.Military != null && context != null)
                    {
                        // Pruefe Gruende fuer moegliches Scheitern VOR dem Versuch
                        var unitType = ui.RecruitmentMode.Value;
                        var (_, moneyCost) = MilitaryUnit.GetRecruitmentCost(unitType);
                        double weaponsNeeded = MilitaryUnit.GetWeaponsCost(unitType);
                        double weaponsAvailable = game.PlayerCountry.GetResource(ResourceType.Weapons);

                        var newUnit = _mgr.Military.StartRecruitment(unitType, game.PlayerCountry.Id, ui.HoveredProvinceId, context);
                        if (newUnit != null)
                        {
                            // Erfolg - Sound abspielen und Modus beenden
                            SoundManager.Play(SoundEffect.Coin);
                            ui.RecruitmentMode = null;
                        }
                        else if (game.PlayerCountry.Budget < moneyCost && weaponsAvailable < weaponsNeeded)
                        {
                            _mgr.Notif?.AddNotification(
                                "Rekrutierung nicht moeglich",
                                $"Nicht genug Budget und Waffen! Benoetigt: {Formatting.Money(moneyCost)} und {weaponsNeeded:F0} Waffen (vorhanden: {weaponsAvailable:F0})",
                                NotificationType.Warning,
                                game.PlayerCountry.Id);
                            ui.RecruitmentMode = null;
                        }
                        else if (game.PlayerCountry.Budget < moneyCost)
                        {
                            _mgr.Notif?.AddNotification(
                                "Rekrutierung nicht moeglich",
                                $"Nicht genug Budget! Benoetigt: {Formatting.Money(moneyCost)}",
                                NotificationType.Warning,
                                game.PlayerCountry.Id);
                            ui.RecruitmentMode = null;
                        }
                        else if (weaponsAvailable < weaponsNeeded)
                        {
                            _mgr.Notif?.AddNotification(
                                "Rekrutierung nicht moeglich",
                                $"Nicht genug Waffen! Benoetigt: {weaponsNeeded:F0} (vorhanden: {weaponsAvailable:F0}). Baue Militaerfabriken fuer Waffenproduktion.",
                                NotificationType.Warning,
                                game.PlayerCountry.Id);
                            ui.RecruitmentMode = null;
                        }
                    }
                }
                return; // Keine weitere Verarbeitung
            }

            // Einheiten-Bewegung: Wenn Einheit ausgewaehlt und Provinz angeklickt
            if (ui.SelectedUnit != null && ui.HoveredProvinceId != null)
            {
                if (_mgr.Military != null && ui.SelectedUnit.ProvinceId != ui.HoveredProvinceId)
                {
                    // Berechne Entfernung zwischen Start- und Zielprovinz
                    int movementHours = 6; // Mindestzeit
                    if (worldMap.Provinces.TryGetValue(ui.SelectedUnit.ProvinceId, out var startProv) &&
                        worldMap.Provinces.TryGetValue(ui.HoveredProvinceId, out var targetProv))
                    {
                        float dx = targetProv.LabelPosition.X - startProv.LabelPosition.X;
                        float dy = targetProv.LabelPosition.Y - startProv.LabelPosition.Y;
                        float distance = MathF.Sqrt(dx * dx + dy * dy);

                        // Geschwindigkeit: ~50 Map-Einheiten pro Stunde
                        // Mindestens 6 Stunden, maximal 72 Stunden (3 Tage)
                        movementHours = Math.Clamp((int)(distance / 50f) + 6, 6, 72);
                    }

                    if (_mgr.Military.MoveUnit(ui.SelectedUnit, ui.HoveredProvinceId, movementHours))
                    {
                        // Erfolg - Auswahl aufheben
                        ui.SelectedUnit = null;
                    }
                }
                else
                {
                    // Gleiche Provinz angeklickt - Auswahl aufheben
                    ui.SelectedUnit = null;
                }
                return; // Keine weitere Verarbeitung
            }

            // Einheiten-Auswahl: Pruefe ob auf eine Einheit geklickt wurde
            if (ui.SelectedUnit == null && game.PlayerCountry != null)
            {
                if (_mgr.Military != null)
                {
                    var clickedUnit = worldMap.GetUnitAtScreenPosition(
                        Raylib.GetMousePosition(),
                        _mgr.Military,
                        game.PlayerCountry.Id
                    );
                    if (clickedUnit != null)
                    {
                        // Pruefe ob Einheit einsatzbereit ist
                        if (clickedUnit.Status == UnitStatus.Ready)
                        {
                            ui.SelectedUnit = clickedUnit;
                        }
                        else if (clickedUnit.Status == UnitStatus.Recruiting)
                        {
                            // Zeige Hinweis dass Einheit noch rekrutiert wird
                            int daysLeft = clickedUnit.RecruitmentDaysLeft;
                            _mgr.Notif?.AddNotification("Rekrutierung", $"Einheit wird noch rekrutiert ({daysLeft} Tage)", NotificationType.Warning);
                        }
                        // Bewegende Einheiten: Klick wird still verbraucht - bewusst KEINE
                        // Handy-Nachricht (stoert nur, waehrend die Einheit unterwegs ist).
                        return; // Keine weitere Verarbeitung
                    }
                }
            }

            // Provinz-Panel oeffnen bei Klick auf Provinz (nur wenn nicht ueber UI)
            if (ui.HoveredProvinceId != null && !IsMouseOverUI())
            {
                if (ui.ShowProvincePanel && ui.SelectedProvinceId == ui.HoveredProvinceId)
                {
                    // Gleiches Panel schliessen - alles abwaehlen
                    ui.ShowProvincePanel = false;
                    ui.SelectedProvinceId = null;
                    ui.SelectedCountryId = null;
                    ui.ShowPoliticsPanel = false;
                    ui.PoliticsPanelCountryId = null;
                }
                else
                {
                    // Provinz-Panel oeffnen
                    ui.ShowProvincePanel = true;
                    ui.SelectedProvinceId = ui.HoveredProvinceId;
                    // Politik-Panel schliessen
                    ui.ShowPoliticsPanel = false;
                    ui.PoliticsPanelCountryId = null;
                    // SelectedCountryId auf das Land der angeklickten Provinz setzen
                    if (ui.HoveredCountryId != null)
                        ui.SelectedCountryId = ui.HoveredCountryId;
                }
            }
            else if (ui.HoveredCountryId == null && !IsMouseOverUI())
            {
                // Klick auf leeren Bereich - alles schliessen
                ui.ShowProvincePanel = false;
                ui.SelectedProvinceId = null;
                ui.SelectedCountryId = null;
                ui.ShowPoliticsPanel = false;
                ui.PoliticsPanelCountryId = null;
            }
        }

        // Rechtsklick - Einheit abwaehlen, Land-Info anzeigen oder Politik-Panel oeffnen
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            // Wenn eine Einheit ausgewaehlt ist, zuerst abwaehlen
            if (ui.SelectedUnit != null)
            {
                ui.SelectedUnit = null;
                return;
            }

            if (ui.HoveredCountryId != null && game.Countries.ContainsKey(ui.HoveredCountryId))
            {
                // Eigenes Land ignorieren - nur fremde Laender per Rechtsklick
                if (game.PlayerCountry != null && ui.HoveredCountryId == game.PlayerCountry.Id)
                {
                    // Nichts tun bei Rechtsklick auf eigenes Land
                }
                else
                {
                    // Toggle: gleiches Land erneut rechtsklicken schliesst alles
                    if (ui.SelectedCountryId == ui.HoveredCountryId)
                    {
                        ui.SelectedCountryId = null;
                        ui.ShowPoliticsPanel = false;
                        ui.PoliticsPanelCountryId = null;
                    }
                    else
                    {
                        // Land-Info Panel (oben rechts) und Politik-Panel oeffnen
                        ui.SelectedCountryId = ui.HoveredCountryId;
                        ui.ShowPoliticsPanel = true;
                        ui.PoliticsPanelCountryId = ui.HoveredCountryId;
                    }
                }
            }
        }

        // Pause
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            game.IsPaused = !game.IsPaused;
            SoundManager.Play(game.IsPaused ? SoundEffect.Pause : SoundEffect.Unpause);
        }

        // Geschwindigkeit mit + und - steuern
        if (Raylib.IsKeyPressed(KeyboardKey.Equal) || Raylib.IsKeyPressed(KeyboardKey.KpAdd))
        {
            if (game.GameSpeed < 5) { game.GameSpeed++; game.IsPaused = false; SoundManager.Play(SoundEffect.SpeedChange); }
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Minus) || Raylib.IsKeyPressed(KeyboardKey.KpSubtract))
        {
            if (game.GameSpeed > 1) { game.GameSpeed--; SoundManager.Play(SoundEffect.SpeedChange); }
        }

        // Panel-Shortcuts mit Tasten 1-8
        if (Raylib.IsKeyPressed(KeyboardKey.One)) { ToggleTopMenuPanel(TopMenuPanel.Politics); }
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) { ToggleTopMenuPanel(TopMenuPanel.Trade); }
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) { ToggleTopMenuPanel(TopMenuPanel.Production); }
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) { ToggleTopMenuPanel(TopMenuPanel.Logistics); }
        if (Raylib.IsKeyPressed(KeyboardKey.Five)) { ToggleTopMenuPanel(TopMenuPanel.Research); }
        if (Raylib.IsKeyPressed(KeyboardKey.Six)) { ToggleTopMenuPanel(TopMenuPanel.Diplomacy); }
        if (Raylib.IsKeyPressed(KeyboardKey.Seven)) { ToggleTopMenuPanel(TopMenuPanel.Budget); }
        if (Raylib.IsKeyPressed(KeyboardKey.Eight)) { ToggleTopMenuPanel(TopMenuPanel.Military); }

        // F5 = Speicher-Panel oeffnen (Overlay im Spiel)
        if (Raylib.IsKeyPressed(KeyboardKey.F5))
        {
            ui.SaveSlots = SaveGameManager.GetAllSlots();
            ui.SelectedSaveSlot = -1;
            game.IsPaused = true;
            ui.ShowSavePanel = true;
            return;
        }

        // F9 = Schnellspeichern (Slot 1)
        if (Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            if (SaveGameManager.SaveGame(game, worldMap, 1))
            {
                Console.WriteLine("[Quicksave] Spiel in Slot 1 gespeichert");
            }
        }

        // Tick-basierte Simulation
        double deltaTime = Raylib.GetFrameTime();
        game.Update(deltaTime);

        // Autosave alle 90 Spieltage (ca. 1 Quartal)
        if (_lastAutosaveDay > game.TotalDays)
        {
            // Neues Spiel nach vorheriger Session: Zaehler zuruecksetzen
            _lastAutosaveDay = game.TotalDays;
        }
        else if (game.TotalDays - _lastAutosaveDay >= 90 && game.PlayerCountry != null)
        {
            _lastAutosaveDay = game.TotalDays;
            if (SaveGameManager.Autosave(game, worldMap))
            {
                // Bewusst ohne Benachrichtigung - Autosave soll den Spieler nicht stoeren
                Console.WriteLine($"[Autosave] Automatisch gespeichert (Tag {game.TotalDays})");
            }
        }

        // Fluessige Truppenbewegung aktualisieren + angekommene Einheiten abschliessen
        _mgr.Military?.UpdateMovementAnimation(game.FractionalHour);

        // ESC-Taste
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            // Wenn Bau-Modus aktiv, erst diesen abbrechen
            if (ui.FactoryBuildMode != null)
            {
                ui.FactoryBuildMode = null;
                return;
            }
            else if (ui.MineBuildMode != null)
            {
                ui.MineBuildMode = null;
                return;
            }
            else if (ui.RecruitmentMode != null)
            {
                ui.RecruitmentMode = null;
                return;
            }
            else if (ui.SelectedUnit != null)
            {
                ui.SelectedUnit = null;
                return;
            }
            // Wenn Bau-Panel offen, erst dieses schliessen
            else if (ui.ShowBuildPanel)
            {
                ui.ShowBuildPanel = false;
                return;
            }
            // Wenn ein Panel offen ist, schliesse es zuerst
            else if (ui.ShowProvincePanel)
            {
                ui.ShowProvincePanel = false;
                ui.SelectedProvinceId = null;
            }
            else if (ui.ShowPoliticsPanel)
            {
                ui.ShowPoliticsPanel = false;
            }
            else
            {
                // Pause-Menü oeffnen
                ui.ShowPauseMenu = true;
                ui.ShowOptionsMenu = false;
                game.IsPaused = true;
            }
        }
    }

    /// <summary>
    /// Update-Logik fuer das Pause-Menü.
    /// Layout kommt aus GetPauseMenuLayout/GetPauseOptionsLayout - dieselbe
    /// Quelle wie das Zeichnen, damit Klickflaechen exakt stimmen.
    /// </summary>
    static void UpdatePauseMenu()
    {
        Vector2 mousePos = _cachedMousePos;

        // ESC schliesst das Menü (bzw. die Options-Ansicht)
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            SoundManager.Play(SoundEffect.Click);
            if (ui.ShowOptionsMenu)
                ui.ShowOptionsMenu = false;
            else
                ui.ShowPauseMenu = false;
            return;
        }

        if (ui.ShowOptionsMenu)
        {
            // === OPTIONEN-ANSICHT ===
            var l = GetPauseOptionsLayout();

            // Slider-Drag starten (grosszuegige Hit-Flaeche um die Schiene)
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (Raylib.CheckCollisionPointRec(mousePos, MenuStyle.Inflate(l.MusicTrack, 10, 14)))
                    ui.IsDraggingMusicSlider = true;
                else if (Raylib.CheckCollisionPointRec(mousePos, MenuStyle.Inflate(l.SoundTrack, 10, 14)))
                    ui.IsDraggingSoundSlider = true;
            }
            if (Raylib.IsMouseButtonReleased(MouseButton.Left))
            {
                ui.IsDraggingMusicSlider = false;
                ui.IsDraggingSoundSlider = false;
            }

            if (ui.IsDraggingMusicSlider)
            {
                ui.OptionsMusicVolume = Math.Clamp((mousePos.X - l.MusicTrack.X) / l.MusicTrack.Width, 0f, 1f);
                musicManager.Volume = ui.OptionsMusicVolume;
            }
            if (ui.IsDraggingSoundSlider)
            {
                ui.OptionsSoundVolume = Math.Clamp((mousePos.X - l.SoundTrack.X) / l.SoundTrack.Width, 0f, 1f);
                SoundManager.Volume = ui.OptionsSoundVolume;
            }

            if (Raylib.IsMouseButtonPressed(MouseButton.Left) &&
                !ui.IsDraggingMusicSlider && !ui.IsDraggingSoundSlider)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, l.Close))
                {
                    SoundManager.Play(SoundEffect.Click);
                    ui.ShowPauseMenu = false;
                    ui.ShowOptionsMenu = false;
                }
                else if (Raylib.CheckCollisionPointRec(mousePos, l.Back) ||
                         !Raylib.CheckCollisionPointRec(mousePos, l.Panel))
                {
                    // Zurueck oder Klick neben das Panel: zur Pause-Ansicht
                    SoundManager.Play(SoundEffect.Click);
                    ui.ShowOptionsMenu = false;
                }
                else if (Raylib.CheckCollisionPointRec(mousePos, l.Toggle))
                {
                    worldMap.DayNightCycleEnabled = !worldMap.DayNightCycleEnabled;
                    SoundManager.Play(SoundEffect.Click);
                }
            }
        }
        else
        {
            // === HAUPTANSICHT ===
            var l = GetPauseMenuLayout();

            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (Raylib.CheckCollisionPointRec(mousePos, l.Close) ||
                    !Raylib.CheckCollisionPointRec(mousePos, l.Panel))
                {
                    // X oder Klick neben das Panel: weiterspielen
                    SoundManager.Play(SoundEffect.Click);
                    ui.ShowPauseMenu = false;
                }
                else if (Raylib.CheckCollisionPointRec(mousePos, l.Save))
                {
                    // Speicher-Panel oeffnen (Overlay im Spiel)
                    SoundManager.Play(SoundEffect.Click);
                    ui.ShowPauseMenu = false;
                    ui.SaveSlots = SaveGameManager.GetAllSlots();
                    ui.SelectedSaveSlot = -1;
                    ui.ShowSavePanel = true;
                }
                else if (Raylib.CheckCollisionPointRec(mousePos, l.Options))
                {
                    SoundManager.Play(SoundEffect.Click);
                    ui.ShowOptionsMenu = true;
                    ui.OptionsMusicVolume = musicManager.Volume;
                    ui.OptionsSoundVolume = SoundManager.Volume;
                }
                else if (Raylib.CheckCollisionPointRec(mousePos, l.MainMenu))
                {
                    SoundManager.Play(SoundEffect.Click);
                    ui.ResetPlayingState();
                    currentScreen = GameScreen.MainMenu;
                }
            }
        }
    }

    /// <summary>
    /// Prueft ob die Maus ueber einem Notification-Popup ist
    /// </summary>
    static bool IsMouseOverNotificationPopups(Vector2 mousePos)
    {
        var notifMgr = _mgr.Notif;
        if (notifMgr == null) return false;

        var popups = notifMgr.ActivePopups;
        if (popups.Count == 0) return false;

        int popupW = 320;
        int popupH = 60;
        int popupSpacing = 5;
        int popupX = ScreenWidth - popupW - 15;
        int baseY = ScreenHeight - GameConfig.BOTTOM_BAR_HEIGHT - 10;

        // Gesamten Popup-Bereich pruefen
        int totalH = popups.Count * (popupH + popupSpacing);
        int topY = baseY - totalH;
        Rectangle popupArea = new Rectangle(popupX, topY, popupW, totalH);
        return Raylib.CheckCollisionPointRec(mousePos, popupArea);
    }

    /// <summary>
    /// Prueft ob die Maus ueber dem Mini-Musikplayer ist
    /// </summary>
    static bool IsMouseOverMusicPlayer(Vector2 mousePos)
    {
        int playerW = 220;
        int playerH = ui.ShowMusicTrackList ? 280 : 50;
        int playerX = ScreenWidth - playerW - 10;
        int playerY = 75;

        Rectangle playerRect = new Rectangle(playerX, playerY, playerW, playerH);
        return Raylib.CheckCollisionPointRec(mousePos, playerRect);
    }

    static void ToggleTopMenuPanel(TopMenuPanel panel)
    {
        bool isActive = ui.ActiveTopMenuPanel == panel;
        ui.ActiveTopMenuPanel = isActive ? TopMenuPanel.None : panel;
        SoundManager.Play(SoundEffect.Click);
        ui.ShowBuildPanel = false;
        if (ui.ActiveTopMenuPanel != TopMenuPanel.None)
        {
            ui.ShowPoliticsPanel = false;
            ui.ShowProvincePanel = false;
        }
    }
}
