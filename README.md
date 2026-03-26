# Economic Empire

Ein Grand-Strategy-Wirtschaftssimulator inspiriert von Europa Universalis und Hearts of Iron. Übernimm die Kontrolle über eines von 223 realen Ländern, verwalte Ressourcen, baue Industrie auf, schließe Handelsabkommen und navigiere durch die Weltpolitik.

## Voraussetzungen

- .NET 10.0 SDK
- Windows

## Bauen & Starten

```bash
dotnet build
dotnet run
```

## Steuerung

| Taste | Aktion |
|-------|--------|
| Leertaste | Pause/Fortsetzen |
| +/- | Geschwindigkeit |
| ESC | Pause-Menü |
| F5 | Speicher-Panel |
| F9 | Schnellspeichern |
| 1-8 | Panels öffnen |
| WASD | Karte verschieben |
| Mausrad | Zoomen |
| Linksklick | Provinz auswählen |
| Rechtsklick | Land-Infos |

---

## Projektstatistiken

| Metrik | Wert |
|--------|------|
| **C#-Quelldateien** | 96 (ohne obj/bin) |
| **Gesamte Codezeilen** | ~28.600 |
| **JSON-Datendateien** | 17 (Kern) + 135 Highways + 252 GeoJSON |
| **Framework** | .NET 10.0 (Raylib-cs) |
| **Projektname** | GrandStrategyGame |

### Top 10 größte Dateien

| Datei | Zeilen |
|-------|--------|
| WorldMap.Provinces.cs | 1.259 |
| Program.PlayingUI.cs | 1.191 |
| Program.PlayingMap.cs | 1.183 |
| Program.TopMenu.cs | 1.045 |
| WorldMap.Rendering.cs | 947 |
| Program.Update.cs | 946 |
| TechTreePanel.cs | 851 |
| WorldMap.ProvinceLoading.cs | 762 |
| SaveGameManager.cs | 740 |
| AIManager.cs | 703 |

---

## 2. Architektur-Übersicht

### Partial Classes
Das Projekt nutzt intensiv Partial Classes zur Dateiaufteilung:

**Program (13 Dateien, ~7.100+ Zeilen):**
- Program.cs - Main(), Felder, Enums, Caches (412 Z.)
- Program.Update.cs - Input-Handling, Spiellogik (946 Z.)
- Program.Draw.cs - Haupt-Zeichenmethoden (209 Z.)
- Program.DrawPlaying.cs - Spielbildschirm, Tutorial (509 Z.)
- Program.TopMenu.cs - Top-Menü-Panels (1.045 Z.)
- Program.Panels.cs - Panel-Dispatcher (82 Z.)
- Program.PlayingUI.cs - Wirtschaftsranking, Ressourcendiagramme (1.191 Z.)
- Program.PlayingMap.cs - Kartenrendering im Spiel (1.183 Z.)
- Program.PlayingNotifications.cs - Benachrichtigungs-UI (485 Z.)
- Program.PlayingOverlays.cs - Overlays, Pause-Menü (397 Z.)
- Program.Font.cs - Custom Font (158 Z.)
- Program.Loading.cs - Ladebildschirm (9 Z.)
- Program.Resources.cs - Textur-Loading (504 Z.)

**WorldMap (9 Dateien, ~4.700+ Zeilen):**
- WorldMap.cs - Kern, Initialize, Draw (642 Z.)
- WorldMap.Data.cs - Städte, Zeitzonen (157 Z.)
- WorldMap.Coordinates.cs - Geo-Transformation (97 Z.)
- WorldMap.Camera.cs - Zoom/Pan (125 Z.)
- WorldMap.ProvinceLoading.cs - Provinzen laden (762 Z.)
- WorldMap.Provinces.cs - Provinz-Rendering (1.259 Z.)
- WorldMap.Rendering.cs - Länder-Rendering (947 Z.)
- WorldMap.Terrain.cs - Relief/Terrain (239 Z.)
- WorldMap.Military.cs - Militäreinheiten auf Karte (451 Z.)

**MilitaryManager (5 Dateien, ~840 Zeilen):**
- MilitaryManager.cs - Kern, Koordination (135 Z.)
- MilitaryManager.Combat.cs - Schlachtenlogik (229 Z.)
- MilitaryManager.Movement.cs - Einheitenbewegung (127 Z.)
- MilitaryManager.Units.cs - Rekrutierung, Verwaltung (158 Z.)
- MilitaryManager.Wars.cs - Kriegsmanagement (191 Z.)

---

## 3. UI-System (UI/)

### Screens (UI/Screens/)
| Screen | Zeilen | Beschreibung |
|--------|--------|-------------|
| CountrySelectScreen | 337 | Länderwahl beim Spielstart |
| MainMenuScreen | 333 | Hauptmenü (Neues Spiel, Laden, Optionen) |
| LoadingScreen | 173 | Asynchrones Laden von Ressourcen |
| SaveGameScreen | 137 | Spielstand speichern |
| LoadGameScreen | 116 | Spielstand laden |
| PlayingScreen | 25 | Hauptspielbildschirm (delegiert an Program) |

### Panels (UI/Panels/)
| Panel | Zeilen | Beschreibung |
|-------|--------|-------------|
| TechTreePanel | 851 | Technologiebaum-UI |
| PoliticsInfoPanel | 576 | Detaillierte Länder-Politikansicht |
| ProductionTopMenuPanel | 394 | Fabriken, Produktionsketten |
| PoliticsTopMenuPanel | 340 | Regierung, Gesetze, Stabilität |
| BudgetTopMenuPanel | 318 | Staatshaushalt, Steuern |
| MilitaryTopMenuPanel | 307 | Streitkräfte, Kriege |
| ProvinceInfoPanel | 210 | Provinzdetails |
| LogisticsTopMenuPanel | 194 | Ressourcenübersicht, Lieferketten |
| TradeTopMenuPanel | 158 | Import/Export, Handelsabkommen |
| NewsTopMenuPanel | 137 | Benachrichtigungen, Ereignisse |
| ResearchTopMenuPanel | 125 | Forschungsübersicht |
| DiplomacyTopMenuPanel | 69 | Beziehungen, Bündnisse |

---

## 4. Manager-System (Systems/Managers/)

Alle Manager erben von `GameSystemBase` und implementieren `IGameSystem`. Koordiniert durch `SystemManager` mit Prioritäten-System.

| Manager | Priorität | Ticks | Zeilen | Funktion |
|---------|-----------|-------|--------|----------|
| **PopulationManager** | 5 | Daily, Monthly, Yearly | 303 | Bevölkerung, Hunger, Migration |
| **EconomyManager** | 10 | Daily, Monthly, Yearly | 214 | BIP, Inflation, Budget |
| **ProductionManager** | 25 | Daily, Weekly | 616 | Fabriken, Minen, Produktionsketten |
| **DiplomacyManager** | 30 | Daily, Monthly | 254 | Beziehungen, Bündnisse |
| **TradeManager** | 30 | Daily, Weekly | 528 | Import/Export, Marktpreise, EU-Handel |
| **PoliticsManager** | 40 | Daily, Monthly, Yearly | 553 | Parteien, Wahlen, Stabilität |
| **MilitaryManager** | 50 | Hourly, Daily, Weekly | 840* | Einheiten, Kriege, Eroberung |
| **TechTreeManager** | 150 | Daily | 330 | Forschungsbaum |
| **ConflictManager** | 160 | Monthly | 238 | Vordefinierte Konflikte |
| **AIManager** | 180 | Daily, Monthly | 703 | KI-Entscheidungen für alle Länder |
| **NotificationManager** | 200 | Daily | 220 | Nachrichten, Events |

*MilitaryManager verteilt auf 5 Partial-Class-Dateien

### Tick-System
```
Hourly  -> Militärbewegung
Daily   -> Produktion, Handel, Nahrung, Rekrutierung, Kämpfe, Forschung, KI
Weekly  -> Marktpreise, Industriekapazität, Kriegsfortschritt
Monthly -> Wirtschaft, Migration, Stabilität, Diplomatie, Konflikte, KI
Yearly  -> BIP-Wachstum, Wahlen, Bevölkerung, Demografie
```

---

## 5. Alle Enums

| Enum | Datei | Werte |
|------|-------|-------|
| `GameScreen` | Program.cs | Loading, MainMenu, LoadGame, CountrySelect, Playing, SaveGame |
| `MapViewMode` | Program.cs | Political, Resources, Trade, Alliance |
| `AllianceViewType` | Program.cs | Military, Economic |
| `TopMenuPanel` | Program.cs | None, Politics, Trade, Production, Research, Diplomacy, Budget, Military, News, Logistics |
| `ResourceType` | Resource.cs | Oil, NaturalGas, Coal, Iron, Copper, Uranium, Food, Steel, Electronics, Machinery, ConsumerGoods, Weapons, Ammunition |
| `FactoryType` | ProductionManager.cs | Civilian, Military, Dockyard |
| `MineType` | Mine.cs | OilWell, GasDrill, CoalMine, IronMine, CopperMine, UraniumMine |
| `UnitType` | MilitaryUnit.cs | Infantry, Tank, Artillery, Mechanized, Airborne |
| `UnitStatus` | MilitaryUnit.cs | Recruiting, Ready, Moving, InCombat, Recovering |
| `GovernmentType` | PoliticsManager.cs | Democracy, AuthoritarianRegime, Monarchy, CommunistState, Theocracy, MilitaryJunta |
| `Ideology` | PoliticsManager.cs | Democratic, Conservative, Socialist, Communist, Fascist, Nationalist, Liberal, Green |
| `AllianceType` | DiplomacyManager.cs | DefensivePact, MilitaryAlliance, EconomicUnion, Federation |
| `WarResult` | MilitaryManager.cs | AttackerVictory, DefenderVictory, WhitePeace, Stalemate |
| `NotificationType` | GameNotification.cs | Info, Warning, Danger, Success |
| `TechCategory` | Technology.cs | Industry, Infrastructure, Electronics, Energy, Military, Society |
| `TechStatus` | Technology.cs | Locked, Available, Researching, Completed |
| `TickType` | TickType.cs | Hourly, Daily, Weekly, Monthly, Yearly |
| `SoundEffect` | SoundManager.cs | Click, Build, NotificationInfo/Warning/Danger/Success/Twitter, Pause, Unpause, SpeedChange, Coin |

---

## 6. Alle Klassen/Datenstrukturen

### Kern-Klassen
| Klasse | Datei | Beschreibung |
|--------|-------|-------------|
| `Game` | Game.cs | Spielzustand, Zeit, Manager-System |
| `WorldMap` | WorldMap.cs (partial) | Karte, Regionen, Provinzen, Rendering |
| `Country` | Country.cs | Land mit Wirtschaft, Bevölkerung, Ressourcen |
| `Province` | Province.cs | Provinz mit Polygonen, Fabriken, Minen |
| `MapRegion` | MapRegion.cs | Kartenregion mit Polygonen und Farbe |
| `Resource` | Resource.cs | Ressource mit Marktdaten |
| `Mine` | Mine.cs | Förderanlage in Provinz |
| `MilitaryUnit` | MilitaryUnit.cs | Militäreinheit mit Kampfwerten |
| `Technology` | Technology.cs | Technologie im Forschungsbaum |
| `GameNotification` | GameNotification.cs | Spielnachricht/Benachrichtigung |
| `AIProfile` | AIProfile.cs | KI-Profil pro Land |
| `River` | River.cs | Flussdaten für Karte |

### Manager-Datenklassen
| Klasse | Manager | Beschreibung |
|--------|---------|-------------|
| `CountryEconomyData` | EconomyManager | Inflation, Steuern, Budget |
| `MoneySnapshot` | EconomyManager | Ressourcen-Verlaufsdaten (Diagramm) |
| `IndustryData` | ProductionManager | Fabriken, Effizienz |
| `ProductionRecipe` | ProductionManager | Input -> Output Rezept |
| `ProductionOrder` | ProductionManager | Produktionsauftrag |
| `TradeAgreement` | TradeManager | Handelsabkommen |
| `MilitaryStrength` | MilitaryManager | Militärische Stärke |
| `War` | MilitaryManager | Aktiver Krieg |
| `RecruitmentOrder` | MilitaryManager | Rekrutierungsauftrag |
| `Demographics` | PopulationManager | Bevölkerungsdaten |
| `CountryPolitics` | PoliticsManager | Politische Daten |
| `PoliticalParty` | PoliticsManager | Partei |
| `AdvertisingCampaign` | PoliticsManager | Wahlkampagne |
| `Alliance` | DiplomacyManager | Bündnis |
| `Conflict` | ConflictManager | Vordefinierter Konflikt |
| `TechProgress` | TechTreeManager | Forschungsfortschritt |

### Balance-Konfiguration (Data/BalanceConfig.cs)
| Klasse | Beschreibung |
|--------|-------------|
| `EconomyBalance` | Wirtschaftskonstanten (Wachstum, Inflation, Steuern) |
| `MilitaryBalance` | Militärkonstanten (Einheitenkosten, Kampfwerte) |
| `UnitStats` | Einheitenstatistiken pro Typ |
| `ProductionBalance` | Produktionskonstanten |
| `RecipeConfig` | Rezept-Konfiguration |
| `TradeBalance` | Handelskonstanten |
| `ResourceInfo` | Ressourceninformationen |

### System-Klassen
| Klasse | Beschreibung |
|--------|-------------|
| `SystemManager` | Verwaltet alle IGameSystem-Instanzen |
| `GameContext` | Zentraler Kontext für Manager |
| `GameEventBus` | Publisher/Subscriber Event-System |
| `IGameEvent` | Marker-Interface für Events |

### Events (Records)
- `WarDeclaredEvent(AggressorId, DefenderId)`
- `PopulationMigratedEvent(FromCountryId, ToCountryId, Amount)`
- `UnitRecruitedEvent(Unit)`
- `StarvationEvent(CountryId, Deaths)`
- `ElectionResultEvent(CountryId, WinningParty)`

### Save-System-Klassen (Utils/SaveGameManager.cs)
| Klasse | Beschreibung |
|--------|-------------|
| `SaveSlotInfo` | Speicherslot-Metadaten |
| `SaveGameData` | Gesamter Spielstand |
| `CountrySaveData` | Land-Speicherdaten |
| `ResourceSaveData` | Ressourcen-Speicherdaten |
| `ProvinceSaveData` | Provinz-Speicherdaten |
| `MineSaveData` | Minen-Speicherdaten |
| `TradeAgreementSaveData` | Handelsabkommen-Speicherdaten |
| `RelationSaveData` | Beziehungs-Speicherdaten |
| `MilitaryUnitSaveData` | Militäreinheiten-Speicherdaten |
| `TechProgressSaveData` | Forschungsfortschritt-Speicherdaten |

### Utility-Klassen
| Klasse | Datei | Beschreibung |
|--------|-------|-------------|
| `Formatting` | Formatting.cs | Zahlenformatierung (Mrd/Mio/Tsd) |
| `ColorPalette` | ColorPalette.cs | UI-Farbkonstanten |
| `GameConfig` | GameConfig.cs | Spielkonstanten |
| `CountryIds` | GameConfig.cs | Länder-ID-Konstanten |
| `GermanStateIds` | GameConfig.cs | Bundesland-ID-Konstanten |
| `UIHelper` | UIHelper.cs | UI-Hilfsfunktionen |
| `TextureManager` | TextureManager.cs | Textur-Cache |
| `SoundManager` | SoundManager.cs | Sound-Effekte (11 Sounds) |
| `MusicManager` | MusicManager.cs | Hintergrundmusik |
| `SaveGameManager` | SaveGameManager.cs | Spielstand-System (3 Slots) |
| `PerformanceCache` | PerformanceCache.cs | Frame-Performance-Caching |
| `ManagerRefs` | ManagerRefs.cs | Gecachte Manager-Referenzen |
| `LoadingState` | LoadingState.cs | Ladezustand-Tracking |
| `PolygonUtils` | PolygonUtils.cs | Polygon-Geometrie |
| `GeoData` | GeoData.cs | Geographische Länderdaten |
| `GeoJsonLoader` | GeoJsonLoader.cs | GeoJSON-Parser |
| `MapLookup` | MapLookup.cs | Bitmap-basierte Positionsabfrage |
| `TerrainGenerator` | TerrainGenerator.cs | Relief-Shading |

---

## 7. JSON-Datendateien

### Kern-Daten (17 Dateien)
| Datei | Zweck |
|-------|-------|
| `countries.json` | Länderdaten (Bevölkerung, BIP, etc.) |
| `countries-meta.json` | Länder-Metadaten |
| `country-colors.json` | Länderfarben für Karte |
| `production.json` | Produktionsdaten (Minen, Fabriken) |
| `diplomacy.json` | Diplomatische Beziehungen, Bündnisse |
| `alliances.json` | Bündnisse (NATO, EU, BRICS, etc.) |
| `politics.json` | Politische Daten (Parteien, Regierungsform) |
| `conflicts.json` | Aktive Konflikte (Ukraine, etc.) |
| `tech_tree.json` | Technologiebaum |
| `ai-profiles.json` | KI-Profile pro Land |
| `balance-config.json` | Spielbalance-Konfiguration |
| `resource-abundance.json` | Ressourcenvorkommen pro Land |
| `resource-deposits.json` | Ressourcen-Lagerstätten in Provinzen |
| `cabinet_germany.json` | Deutsches Kabinett |
| `cities.json` | Städte mit Koordinaten |
| `timezones.json` | Zeitzonen pro Land |
| `manual-coordinates.json` | Manuelle Koordinatenkorrekturen |

### Autobahnen (135 Dateien)
Länderspezifische Autobahn-/Straßennetze (z.B. `german_highways.json`, `japan_highways.json`, `*_highways.json`)

### GeoJSON-Provinzdaten (252 Dateien)
Provinz-/Bundesland-Polygone für 223 Länder (z.B. `germany_states.geojson`, `albania_counties.geojson`)

---

## 8. Spielmechanik-Übersicht

### Produktionsketten
```
Minen -> Rohstoffe:
  Eisenmine -> Eisen
  Kohlemine -> Kohle
  Kupfermine -> Kupfer
  Ölquelle -> Öl
  Gasbohrung -> Erdgas
  Uranmine -> Uran

Fabriken -> Verarbeitete Güter:
  Eisen + Kohle -> Stahl
  Kupfer + Kohle -> Elektronik
  Stahl + Elektronik -> Maschinen
  Nahrung + Stahl + Elektronik -> Konsumgüter

Zivilfabriken -> Nahrung:
  (keine Inputs) -> Nahrung

Militärfabriken -> Militärgüter:
  Stahl + Elektronik -> Waffen
  Stahl + Kupfer -> Munition
```

### Ressourcentypen (13)
- **Rohstoffe (6):** Öl, Erdgas, Kohle, Eisen, Kupfer, Uran
- **Agrar (1):** Nahrung
- **Verarbeitete Güter (4):** Stahl, Elektronik, Maschinen, Konsumgüter
- **Militärgüter (2):** Waffen, Munition

### Bevölkerungsverbrauch (pro Mio. Einwohner/Tag)
- Nahrung: 1.0 (via Hungersystem in PopulationManager)
- Konsumgüter: 0.3
- Öl: 0.1
- Erdgas: 0.08
- Kohle: 0.05

### Zeitgeschwindigkeit
```
Speed 1: 10 min/s
Speed 2: 1h/s
Speed 3: 8h/s
Speed 4: 1 Tag/s
Speed 5: 3 Tage/s
```

---

## 9. Länder im Spiel

Das Spiel enthält **223 Länder** mit GeoJSON-Provinzdaten in 19 Regionen von Nordamerika bis Ozeanien. Deutschland hat **16 Bundesländer** als Provinzen.

---

## 10. Features

- Weltkarte mit echten Koordinaten und 223 Ländern
- Tag/Nacht-Zyklus mit realistischem Terminator
- Terrain-Relief-Shading
- 4 Kartenansichten (Politisch, Ressourcen, Handel, Bündnisse)
- 11 Manager-Systeme mit KI
- 13 Ressourcentypen mit Marktdynamik
- Produktionsketten (Minen -> Rohstoffe -> Fabriken -> Güter)
- Handelsystem mit EU-Binnenmarkt-Bonus
- Militärsystem (5 Einheitentypen, Bewegung, Eroberung)
- Politiksystem (Parteien, Wahlen, Werbekampagnen)
- Technologiebaum (6 Kategorien)
- Diplomatiesystem (Beziehungen, Bündnisse, Embargos)
- Bevölkerungssystem (Wachstum, Migration, Hungersnot)
- KI-System mit länderspezifischen Profilen
- Benachrichtigungssystem mit historischen Events
- Speichern/Laden-System (3 Slots)
- Musik-System, Sound-Effekte (11 Sounds)
- Gütermengen-Diagramm mit filterbarer Legende
- 12 UI-Panels, 6 Bildschirme
- Balance-Konfiguration via JSON

### Performance
- Frustum Culling für Karte
- HOI4-Style Bitmap-Lookup für O(1) Positionsabfragen
- Transformation-Caching für Provinzen/Regionen
- Day/Night-Overlay mit RenderTexture-Cache
- `CollectionsMarshal.GetValueRefOrAddDefault` für schnellen Ressourcen-Zugriff
- Frame-gecachte Mausposition und Manager-Referenzen

---

## Technologie

- **Sprache:** C# (.NET 10)
- **Grafik-Framework:** Raylib-cs v7.0.2
- **Plattform:** Windows

## Autor

Maik
