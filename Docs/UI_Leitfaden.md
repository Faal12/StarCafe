# Leitfaden für Oberfläche & Grafik (Frontend)

Für alle, die am Aussehen arbeiten: Menüs, HUD, Fenster, Platzhalter durch echte Grafik ersetzen.
Spiellogik und Inhalte (Ereignisse, Rezepte …) stehen in `Data/README.md`.

## Grundsätze

- **Oberflächen werden im Code gebaut**, nicht als Szene (siehe README, „Arbeiten im Team“). Jedes Fenster ist eine
  Klasse in `Scripts/Ui/` (bzw. `Scripts/Minigames/`), die ihre Knoten in `Build…()`-Methoden anlegt.
- **Bildschirmgröße:** 1152 × 648, Stretch-Modus `canvas_items`, Seitenverhältnis `expand`.
- **Schrift:** `UiTheme.BodyFont`, `HeadingFont`, `TitleFont` (`Scripts/Ui/UiTheme.cs`).
- **Logik bleibt getrennt:** Fenster lesen Zustände (`PlayerState`, `EventService`, `TaskService` …) und rufen Methoden auf –
  sie speichern selbst nichts. Wer nur das Aussehen ändert, muss die Logik nicht anfassen.

## Zwei Stile

| Stil | Wo | Datei |
|---|---|---|
| **Holo** (neu): dunkles Glas, dünne türkise Linien, sanftes Leuchten, kleine Großbuchstaben | Inventar, Bar, Bestellung, Aufzug, Minispiele, Kampf-HUD, Aufgaben | `Scripts/Ui/Holo.cs` |
| **Taverne** (bestehend): warmes Bernstein | Launcher, Pause, Einstellungen, Ereignis-Fenster, Schichtende | `Scripts/Ui/UiTheme.cs`, `UiKit.cs` |

`Holo.cs` ist der Baukasten: Farben (`Glass`, `Line`, `Accent`, `Warm`, `Sweet`, `Strong` …), `Panel()`, `Slot()`,
`Button()`, `Caption()`, `Text_()`, `Icon()` (Platzhalter-Symbol), `ItemIcon()`, `AxisBar()`.
**Farben/Abstände dort ändern = überall geändert.**

## Fenster und Ebenen

| Klasse | Zeigt | CanvasLayer |
|---|---|---|
| `GameHud` | Datum, Konjunktion, Schichtende-Bildschirm (Kasse, Radio) | 20 |
| `InventoryHud` | Schnellleiste, Credits, Hinweise („+₡12“), Inventar (Tab) | 15 |
| `CombatHud` | Leben/Energie, roter Treffer-Rand, Aufgabenfenster | 14 |
| `TrayBalance` (Bar) | Tablett-Anzeige über der Schnellleiste | 15 |
| `BarPanel` | Mix-Bildschirm „Am Tresen“ + Rezeptbuch | 28 |
| `OrderPanel` | Bestellung / Bewertung am Tisch | 28 |
| `EventPanel` | Ereignis als Gespräch (Chat, Ausgänge, Floki) | 30 |
| `CircuitGame` / `PressureGame` | Minispiele Schaltkreis / Druckventil (selbst gezeichnet in `CircuitBoard._Draw` / `PressureGauge._Draw`) | 32 |
| `DeckManager` | Aufzug-Menü, Abblende beim Deckwechsel | 40 |
| `PauseMenu` | Pause | 100 |
| `DialogueController` (Szene) | Gespräch mit Floki, Porträts | Szene `Tavern.tscn` → `DialogueUI` |

Fenster, die das Spiel anhalten, setzen `GetTree().Paused = true` und laufen selbst mit `ProcessMode = Always`.

## Platzhalter → echte Grafik

| Was | Wo eintragen |
|---|---|
| **Gegenstands-Symbole** (Schnellleiste, Inventar, Aufheben) | `Data/Items/items.json` → `"texture": "res://…png"` (sonst farbige Kachel mit Buchstaben) |
| **Gegenstände und Figuren als Folgen von Ereignissen** (Stuhl, Kerzen, Nomadenkind …) | `Data/Events/*.json` → in `world` `"texture"` setzen (sonst beschrifteter Kasten / Figur) |
| **Gäste** (Spezies × Fraktion) | `Scripts/Guests/Guest.cs` → `_Ready()` (nutzt `WorldStage.BuildCharacterPlaceholder`) – hier später Sprites je Spezies |
| **Monster** | `Scripts/Combat/Monsters.cs` → `BuildLook()` je Monster (`Blob(...)`) |
| **Technikdeck / Wohndeck** (Generator, Rohre, Kisten, Bett, Aufzug …) | `Scripts/Decks/TechnikDeck.cs`, `WohnDeck.cs` (`DeckBuilder.Block`, `DeckBuilder.Floor`) – Kästen durch Sprites ersetzen |
| **Sprecher in Ereignissen** (Karte mit Anfangsbuchstabe) | `Scripts/Ui/EventPanel.cs` → `_portrait`; Ereignisse haben `speaker.placeholder` |
| **Floki** | fertig: `Assets/Characters/Floki/` – Körper + 4 Augen-Ebenen (gleiche Leinwand), `floki_world.png` für den Schankraum. Neue Mimik: Bild ablegen, in `Resources/Npc/Floki.tres` unter *Emotions* eintragen |
| **Launcher-Hintergrund & Schiff** | `Assets/Art/pastel_background.png`, `spaceship.png` (`Scripts/Launcher/LauncherScreen.cs`) |
| **Bar-Zutaten** (Farbpunkt) | `Data/Bar/ingredients.json` → `color` (Symbol-Grafik später in `BarPanel.RefreshShelf`) |

Orte im Schankraum, an denen Folgen erscheinen: Knoten **WorldSpots** in `Tavern.tscn`; Sitzplätze der Gäste:
Knoten **GuestSeats**. Beide sind Marker – im Editor verschieben genügt.

## Bitte nicht umbenennen

- Knoten, auf die Skripte per Pfad zeigen: `Player`, `Player/Camera2D`, `DialogueUI/NpcPortraitBig/Emotion`, `WorldSpots`, `GuestSeats`, `Events`, `WorldStage`, `Technikdeck`.
- Eingabe-Aktionen: `move_*`, `interact`, `walk_careful`, `attack`, `use_item`, `debug_event` (Tasten dürfen geändert werden).

## Testen ohne Durchspielen

Umgebungsvariablen (nur Editor/Debug): `STARCAFE_EVENT=<id>` + Strg+Umschalt+E öffnet ein bestimmtes Ereignis,
`STARCAFE_OFFLINE=1` (ohne KI), `STARCAFE_NO_EVENTS=1` (keine automatischen Ereignisse). Mehr in `Data/README.md`.
