# Leitfaden für Oberfläche & Grafik (Frontend)

Für alle, die am Aussehen arbeiten: Menüs, HUD, Fenster, Platzhalter durch echte Grafik ersetzen.
Spiellogik und Inhalte (Ereignisse, Rezepte …) stehen in `Data/README.md`.

## Grundsätze

- **Alle Fenster der Taverne hängen in einer eigenen Szene: `Scenes/Ui/TavernUi.tscn`** (Wurzel `UiHost`).
  `Tavern.tscn` bindet sie nur ein (Knoten `Ui`). Die bestehenden Fenster sind Klassen in `Scripts/Ui/`
  (bzw. `Scripts/Minigames/`), die ihre Knoten im Code anlegen (`Build…()`).
- **Neue oder umgebaute Fenster dürfen als Szene im Godot-Editor gebaut werden** (`Scenes/Ui/*.tscn` mit Skript in
  `Scripts/Ui/`) – siehe „Zusammenarbeit“ unten.
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
| `DialogueController` (Szene) | Gespräch mit Floki, Porträts | `Scenes/Ui/TavernUi.tscn` → `DialogueUI` |

Fenster, die das Spiel anhalten, setzen `GetTree().Paused = true` und laufen selbst mit `ProcessMode = Always`.
Alle Fenster außer Minispielen und Aufzug-Menü sind Kinder von `TavernUi.tscn`.

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

- Knoten, auf die Skripte per Pfad zeigen: in `Tavern.tscn` `Player`, `Player/Camera2D`, `WorldSpots`, `GuestSeats`,
  `Events`, `WorldStage`, `Technikdeck`, `Ui`; in `TavernUi.tscn` `DialogueUI`, `EventPanel` (Pfade als Export in `UiHost`
  änderbar) und `DialogueUI/NpcPortraitBig/Emotion`.
- Die Namen in `UiBus` (Methoden, Events, `UiWindow`, `MinigameId`) – nur nach Absprache beider Seiten.
- Eingabe-Aktionen: `move_*`, `interact`, `walk_careful`, `attack`, `use_item`, `debug_event` (Tasten dürfen geändert werden).

## Zusammenarbeit Frontend ↔ Backend

**Ziel:** beide arbeiten gleichzeitig, ohne sich gegenseitig Dateien kaputtzumachen.

### Wer besitzt was

| Frontend | Backend | Gemeinsam (nur nach Absprache) |
|---|---|---|
| `Scenes/Ui/` (TavernUi.tscn, neue Fenster-Szenen) | `Tavern.tscn` (Welt, Möbel, Marker, Systeme) | `Scripts/Core/UiBus.cs` (der Vertrag) |
| `Scripts/Ui/`, Zeichnen der Minispiele (`…Board._Draw`, `…Gauge._Draw`) | `Scripts/Events`, `Bar`, `Guests`, `Items`, `Combat`, `Decks`, `Tasks`, `World`, `Core`, `Llm`, `Voice` | `project.godot` (Input Map) |
| `Assets/` (Grafik, Schriften) | `Data/` (JSON-Inhalte), `Docs/Handlung.md` | dieser Leitfaden |

Festgehalten in `.github/CODEOWNERS`: ändert jemand eine Datei der anderen Seite, fragt GitHub die andere Seite automatisch um ein Review.

### Der Vertrag: `UiBus`

Spiellogik ruft **nie** ein Fenster direkt auf, sondern nur `UiBus` (`Scripts/Core/UiBus.cs`).
`UiHost` (Wurzel von `TavernUi.tscn`) hört zu und reicht weiter an das passende Fenster.

| Logik ruft … | bedeutet | Fenster heute |
|---|---|---|
| `UiBus.Toast(text, farbe)` | kurzer Hinweis unten | `InventoryHud.Toast` |
| `UiBus.OpenDialogue(persona)` | freies Gespräch (Floki) | `DialogueController` |
| `UiBus.OpenBar()` | Mix-Bildschirm | `BarPanel` |
| `UiBus.ShowOrder / ShowWaiting / ShowRating` | Bestellung, Warten, Bewertung am Tisch | `OrderPanel` |
| `UiBus.ShowEvent(def)` | Ereignis als Gespräch | `EventPanel` |
| `UiBus.ShowAftermath(def, ausgang, ergebnis)` | Ergebnis nach einer Aufgabe oder Schlägerei | `EventPanel` |
| `UiBus.NotifyPlayerHurt()` | Spieler getroffen | `CombatHud.FlashDamage` |
| `UiBus.NotifyBrawl(läuft, gegner)` → Event `BrawlChanged` | Schlägerei im Schankraum beginnt / endet | (noch frei – Banner, Musik …) |
| `UiBus.RunMinigame(MinigameId.…, fertig)` | Minispiel starten, `fertig(true)` = geschafft | `CircuitGame`, `PressureGame` |

Umgekehrt melden Fenster, ob sie offen sind: `UiBus.SetOpen(UiWindow.Bar, true)`. Die Logik fragt nur
`UiBus.IsOpen(UiWindow.Dialogue)` oder `UiBus.AnyOpen` (z. B. „kein Ereignis während eines Gesprächs“).
Wer ein Fenster neu baut, muss nur diese Meldung beibehalten – bei den bestehenden Fenstern steckt sie in der
Eigenschaft `IsOpen`.

Daten lesen die Fenster direkt aus der Logik (`PlayerState`, `OrderBook.Open`, `TaskService` …), ändern sie aber nur
über deren Methoden.

**Handlungen im Ereignis-Gespräch** (Täuschen, Einschüchtern, Bestehlen, Angreifen) – das Fenster ruft nur diese
Methoden aus `Scripts/Events/ConversationActions.cs` und zeigt das Ergebnis:

| Fenster ruft … | liefert |
|---|---|
| `ConversationActions.CanUse(gespräch, aktion, out grund)` | Knopf an/aus + Tooltip-Grund (z. B. „nicht im Raum“) |
| `ConversationActions.Label(aktion)`, `Hint(aktion)`, `NeedsText(aktion)` | Beschriftung, Erklärung, ob Text aus dem Eingabefeld nötig ist |
| `await ConversationActions.DeceiveAsync(gespräch, text)` / `IntimidateAsync(…)` | `ActionReport` |
| `ConversationActions.Steal(gespräch)` / `Attack(gespräch)` | `ActionReport` |

`ActionReport`: `Narration` (Erzählertext mit Chance), `Success`, `Chance`, `Step.SpeakerLine` (Antwort der Figur),
`Outcome` (≠ null → Ereignis entschieden, wie beim Reden anzeigen), `StartsBrawl` (→ Fenster schließen; die Schlägerei
beginnt, sobald es zu ist). Weitere Zustände für die Anzeige: `gespräch.TurnsLeft`, `gespräch.Anger`, `BrawlService.IsActive`.

### Ein Fenster neu bauen (z. B. als Szene)

1. Neue Szene in `Scenes/Ui/` anlegen, Skript in `Scripts/Ui/`.
2. In `TavernUi.tscn` den alten Fenster-Knoten durch die neue Szene ersetzen.
3. In `UiHost.Connect()` die passende Zeile auf das neue Fenster umstellen.
4. Offen/zu über `UiBus.SetOpen(UiWindow.…, …)` melden.

Die Spiellogik muss dafür nicht angefasst werden.

### Etwas Neues braucht beide Seiten

1. Backend fügt in `UiBus` **eine Methode + ein Event** hinzu und ruft sie auf (ohne Fenster passiert nichts,
   ein Toast landet dann in der Konsole).
2. Frontend hängt das Event in `UiHost` an ein Fenster.
3. Beides kann in getrennten PRs passieren – der Vertrag ist der Treffpunkt.

### Arbeitsablauf (Git)

- `main` nur über Pull Requests. Eigene Branches: `ui/…` (Frontend), `feature/…` (Backend).
- Kleine PRs (1–3 Tage Arbeit), `main` täglich in den eigenen Branch holen.
- Neue Inhalte zuerst mit **Platzhaltern** (Backend); das Frontend ersetzt sie später (Tabelle oben) – keiner wartet auf den anderen.

## Testen ohne Durchspielen

Umgebungsvariablen (nur Editor/Debug): `STARCAFE_EVENT=<id>` + Strg+Umschalt+E öffnet ein bestimmtes Ereignis,
`STARCAFE_OFFLINE=1` (ohne KI), `STARCAFE_NO_EVENTS=1` (keine automatischen Ereignisse). Mehr in `Data/README.md`.
