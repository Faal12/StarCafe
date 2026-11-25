# Bar: „Am Tresen“

Das Herz jeder Schicht. Alles hier ist ohne Code in JSON änderbar.

## So läuft es im Spiel

1. **Gäste kommen** durch die Schleuse, setzen sich (Plätze: Knoten *GuestSeats* in `Tavern.tscn`)
   und zeigen **!** – sie wollen bestellen. Darunter läuft ihre Geduld ab.
2. Hingehen, **E** drücken: Bestellung lesen (wird vorgelesen). **Nachfragen** verrät, was gemeint ist.
   **Bestellung aufnehmen** → der Gast zeigt **…** und wartet auf sein Getränk.
3. An der **Bar** (leuchtender Punkt am Tresen) **E**: Mix-Bildschirm.
   Bestellung wählen, bis zu **4 Zutaten** in den Shaker, **Zubereitung** wählen, **Fertig – aufs Tablett**.
   Exakte Rezepte werden beim ersten Mal ins **Rezeptbuch** eingetragen.
4. Das Getränk liegt jetzt im **Inventar** (Schnellleiste unten). **Tragen ist ein Minispiel:**
   Abruptes Losgehen, Stoppen und Zickzack bringen das Tablett ins Schwappen (Anzeige über der Schnellleiste).
   **Umschalt** = vorsichtig gehen. Mehr Getränke = empfindlicher. Zu viel Schwappen → **verschüttet**:
   Getränk weg, Pfütze für eine Woche auf dem Boden, Floki lästert, die Tat wird gemerkt.
5. Beim Gast **E** = servieren. Serviert wird das **ausgewählte** Getränk der Schnellleiste,
   sonst eins, das für diesen Gast gemixt wurde, sonst irgendeins.
   **Bewertung** (0–3 Sterne) → **Trinkgeld** (₡), bei begeisterten/enttäuschten Gästen ±1 Ansehen bei ihrer Fraktion.
6. Wer zu lange wartet, geht verärgert (−1 Ansehen). Am Schichtende gehen alle; die **Kasse** steht auf dem Schichtende-Bildschirm.

**Steuerung:** WASD laufen · Umschalt vorsichtig · E benutzen/reden · 1–8 / Mausrad Schnellleiste · Tab Inventar (Ziehen = umsortieren) · Esc schließen.

## Dateien

### `ingredients.json` – Zutaten

| Feld | Bedeutung |
|---|---|
| `id`, `name`, `description` | wie immer |
| `waerme`, `suesse`, `staerke` | Beitrag zum Geschmack (darf negativ sein, z. B. Kupferbitter −1 Süße) |
| `alcohol` | alkoholisch? (Bestellungen „ohne Alkohol“) |
| `color` | Farbe des Platzhalter-Symbols |
| `price` | Einkaufspreis – **Platzhalter**, noch nicht berechnet (siehe Wirtschaft) |
| `requiresItem` | Zutat ist begrenzt: jede Verwendung verbraucht diesen Gegenstand aus dem Inventar (z. B. Alienschleim) |

### `recipes.json` – Zubereitung & Rezepte

`methods`: Schütteln (+1 Süße), Rühren (+1 Stärke), Erhitzen (+2 Wärme), Auf Eis (−2 Wärme).

`recipes`: **genaue Zutaten** (Reihenfolge egal, doppelte zählen) + **Methode** = benanntes Getränk.

| Feld | Bedeutung |
|---|---|
| `ingredients` | 1–4 Zutaten-IDs |
| `method` | Methoden-ID |
| `price` | Grundpreis → bestimmt das Trinkgeld |
| `effects` | Wirkung für spätere Systeme: `wahrheit` (Figuren reden mehr), `beruhigend`, `heilung` (Kampf), `mutig` |

Geschmack eines Getränks = Summe der Zutaten + Methode, jede Achse 0–9.

### `guests.json` – Spezies, Bestellungen, Reaktionen

**Spezies** mit Regeln, die jeder Bestellung übergeordnet sind:
Myrr `maxStaerke: 1`, Ghel `minStaerke: 4`, Quirren `minSuesse: 2`, Vhorren `maxWaerme: 4`.
Verstößt ein Getränk dagegen, gibt es höchstens 25 Punkte und die passende Beschwerde (`tooStrong` usw.).
Gäste bekommen **nie unmögliche Bestellungen** (ein Myrr bestellt keinen Sandsturm).

**Bestellungen** (`orders`):
- `"kind": "rezept"` + `"recipe"` – ein benanntes Getränk.
- `"kind": "geschmack"` + `"min"` / `"max"` (Achsen `waerme`, `suesse`, `staerke`) und/oder `"noAlcohol": true` – Rätsel-Bestellung („Was Warmes, ich hatte einen miesen Tag“).
- `"kind": "ueberraschung"` – irgendein benanntes Rezept.
- `"hint"` – was „Nachfragen“ verrät.

**Reaktionen** je Bewertung: `perfekt` (≥ 90), `gut` (≥ 65), `mittel` (≥ 40), `schlecht`.

## Gedächtnis (Vorarbeit für Phase 4)

Jede Handlung landet als **Tat** im Spielstand – mit Schlagwörtern:
`serviert` (Gast, Spezies, Fraktion, Rezept, Bewertung, Wirkung), `bestellung_aufgenommen`,
`warten_lassen`, `verschuettet`. Später können Ereignisse und Figuren darauf reagieren.

## Wirtschaft – noch Platzhalter

In `Scripts/Core/Economy.cs` sind alle künftigen Geldflüsse schon vorbereitet, aber ausgeschaltet:
Zutaten-Einkauf (`price`), Reparaturen, Bestechung, Strafen, Liegegebühr, Löhne, Verkauf.
Aktuell gibt es nur **Einnahmen** (Trinkgeld). Startkapital: ₡50.
