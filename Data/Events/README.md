# Ereignisse & Konflikte

Alle Ereignisse stehen in JSON-Dateien in diesem Ordner (`Data/Events/*.json`).
Das Spiel lädt **jede** `.json`-Datei hier – man kann also auch neue Dateien anlegen
(z. B. `konjunktion.json`, `imperium.json`). Kein Code nötig.

## So läuft es im Spiel

- Jede **Schicht** (= eine Woche) bringt ein paar Ereignisse:
  Einstellung *Konflikte* „Selten“ = 1, „Normal“ = 2, „Oft“ = 3; in der **Konjunktion** eins mehr.
  Das erste kommt nach ein paar Sekunden, die weiteren mit Abstand – nie mitten in einem Gespräch,
  im Pausenmenü oder in einem anderen Ereignis.
- **Ein Ereignis ist ein Gespräch.** Der Spieler sieht die Situation und die **möglichen Ausgänge** –
  aber es gibt **keine Knöpfe dafür**. Er muss die Figur im Gespräch dorthin bringen
  (überzeugen, verhandeln, drohen, bestechen, eine Tat beschreiben wie „Ich schenke ihm nach“).
- **Mit KI (Gemini):** Die KI spielt die Figur und entscheidet, wann ein Ausgang erreicht ist.
  Sie gibt nicht sofort nach: Es braucht Argumente, Angebote, Druck oder eine klare Tat.
- **Ohne KI (Offline-Demo):** **Schlüsselwörter** in der Antwort des Spielers entscheiden (`keywords`).
  Fällt die KI mitten im Gespräch aus, geht es automatisch offline weiter.
- **Rundenlimit** (`maxTurns`): Nach so vielen Antworten muss sich die Figur entscheiden.
  Ohne Entscheidung gilt der **Standard-Ausgang** (`"default": true`).
- **„Gehen lassen“**: Der Spieler hält sich heraus → sofort der Standard-Ausgang.
- Danach: Ansehen und Gastrecht ändern sich, Floki kommentiert, und die Radio-Meldung erscheint
  beim **Schichtende** unter „Galaktisches Radio“.
- **Alles wird vorgelesen** (wenn im Launcher „Antworten vorlesen“ an ist), schön nacheinander:
  der **Erzähler** liest Situation und Ergebnis, die **Figur** ihre Sätze (jede Figur hat ihre eigene feste Stimme),
  **Floki** seine Einwürfe. Antwortet der Spieler, verstummt die laufende Ansage. Die Erzählerstimme lässt sich
  im Launcher unter „Stimme: Erzähler“ wählen.
- **Floki hört über das Headset mit:** Manchmal wirft er ungefragt etwas ein (Häufigkeit: *Floki → Kommentare*),
  und mit **„Floki fragen“** (2× pro Ereignis) gibt er einen echten Tipp, wie man einen Ausgang erreicht.
  Die Figur hört ihn nie.
- **Folgen im Schankraum** (`world`, siehe unten): Gegenstände und Figuren tauchen auf, Animationen spielen,
  Story-Flags schalten Folge-Ereignisse frei.
- **Testen:** Im Editor/Debug-Build löst **Strg+Umschalt+E** sofort ein zufälliges Ereignis aus (ignoriert Abklingzeiten).
  Mit der Umgebungsvariable `STARCAFE_EVENT=<id>` öffnet die Taste genau dieses Ereignis (ignoriert alle Bedingungen).
  (Nicht F8: Im Godot-Editor beendet F8 das laufende Spiel. Die Taste ist die Aktion `debug_event` in der Input Map.)
- **Ohne KI testen:** Umgebungsvariable `STARCAFE_OFFLINE=1` erzwingt den Offline-Modus (Schlüsselwörter),
  auch wenn ein Gemini-Schlüssel eingetragen ist.
- Fehler in einer Datei stehen in der Godot-Ausgabe als Warnung „Ereignisse: …“.
  Das kaputte Ereignis wird übersprungen, der Rest läuft.

## Aufbau eines Ereignisses

```json
{
  "id": "eindeutige_id",
  "kind": "Konflikt",
  "title": "Überschrift",
  "speaker": {
    "name": "Imperialer Offizier",
    "faction": "Imperium",
    "persona": "Charakter, Ziel, was die Figur überzeugt und was sie nie akzeptiert.",
    "placeholder": true
  },
  "text": "Die Situation (sieht der Spieler, bekommt auch die KI).",
  "opening": "Erster Satz der Figur.",
  "floki": "Optionaler Spruch von Floki zu Beginn.",
  "maxTurns": 5,
  "offlineLines": [ "Antwort der Figur ohne KI, solange nichts entschieden ist." ],
  "weight": 2,
  "once": false,
  "cooldownWeeks": 3,
  "minWeek": 1,
  "maxWeek": 0,
  "conjunction": "egal",
  "minReputation": { "Rebellen": 20 },
  "maxReputation": { "Imperium": 59 },
  "minGastrecht": 0,
  "maxGastrecht": 100,
  "requiresAlive": [ "Imperium" ],
  "outcomes": [
    {
      "id": "verraten",
      "label": "Du verrätst den Rebellen",
      "condition": "Für die KI: wann genau dieser Ausgang eintritt.",
      "default": false,
      "keywords": [ "verrat", "sein name" ],
      "reputation": { "Imperium": 10 },
      "rivalry": true,
      "gastrecht": -10,
      "result": "Was danach passiert.",
      "floki": "Optionale Reaktion von Floki.",
      "radio": "Optionale Meldung im Galaktischen Radio.",
      "killKeyFigure": ""
    }
  ]
}
```

### Felder des Ereignisses

| Feld | Bedeutung | Standard |
|---|---|---|
| `id` | eindeutig, **nicht mehr ändern**, sobald es Spielstände gibt | – (Pflicht) |
| `kind` | `"Konflikt"` (rot markiert) oder `"Ereignis"` | `"Ereignis"` |
| `title`, `text` | Überschrift und Situation | – (Pflicht) |
| `opening` | erster Satz der Figur | – (Pflicht) |
| `speaker.name` | Name der Figur | „Unbekannter Gast“ |
| `speaker.faction` | färbt die Platzhalter-Karte; leer = neutral grau | leer |
| `speaker.persona` | **für die KI**: Charakter, Ziel, was überzeugt, was nie geht. Je genauer, desto besser spielt die KI | leer |
| `speaker.placeholder` | `true` zeigt „Platzhalter“ – später durch echte Figur ersetzen | `true` |
| `speaker.strength` | Kampfkraft 1–3: Leben und Schläge in der Schlägerei, wie schwer einzuschüchtern | 2 |
| `speaker.wits` | Gerissenheit 1–3: wie schwer zu täuschen und zu bestehlen | 2 |
| `speaker.remote` | `true`: nicht im Raum (Funk, Holo) – kein Angreifen, kein Bestehlen | `false` |
| `speaker.pockets` | was sich stehlen lässt: `creditsMin`, `creditsMax`, `items` (Ids aus `Data/Items/items.json`) | ₡5–25 |
| `floki` | Flokis Spruch zu Beginn (in \*Sternchen\*) | keiner |
| `maxTurns` | Antworten des Spielers, bis entschieden wird | 6 |
| `offlineLines` | Sätze der Figur im Offline-Modus | ein Standardsatz |
| `weight` | Häufigkeit im Vergleich zu anderen Ereignissen | 1 |
| `once` | nur einmal pro Spiel | `false` |
| `cooldownWeeks` | Wochen bis zur Wiederholung | 3 |
| `minWeek` / `maxWeek` | ab / bis Woche (0 = ohne Ende) | 1 / 0 |
| `conjunction` | `"egal"`, `"nur"` (nur in der Konjunktion), `"nie"` | `"egal"` |
| `minReputation` / `maxReputation` | Ansehen-Bedingungen je Fraktion (−100 … 100) | keine |
| `minGastrecht` / `maxGastrecht` | Gastrecht-Bedingung (0 … 100) | 0 / 100 |
| `requiresAlive` | Schlüsselfiguren dieser Fraktionen müssen leben | keine |
| `outcomes` | **mindestens zwei** Ausgänge, **genau einer** mit `"default": true` | – (Pflicht) |

### Felder eines Ausgangs

| Feld | Bedeutung | Standard |
|---|---|---|
| `id` | kurz, nur Buchstaben/Ziffern/`_` (keine Umlaute, kein ß); eindeutig im Ereignis | – (Pflicht) |
| `label` | was der Spieler unter „Mögliche Ausgänge“ sieht | – (Pflicht) |
| `condition` | **für die KI**: was der Wirt tun/sagen muss, damit es so ausgeht | – (Pflicht) |
| `default` | dieser Ausgang gilt bei „Gehen lassen“ oder wenn das Rundenlimit erreicht ist | `false` |
| `keywords` | Offline-Modus: Wortanfänge in der Antwort des Spielers (klein, Umlaute egal) | keine |
| `reputation` | Ansehen-Änderungen je Fraktion | keine |
| `rivalry` | `true`: Imperium/Rebellen ziehen die Gegenseite um die Hälfte mit (Handlung 10.1). `false`, wenn die Werte genau so gemeint sind | `true` |
| `gastrecht` | Änderung des Gastrechts – negativ bei Gewalt oder Verrat in der Taverne | 0 |
| `result` | Erzähltext danach | keiner |
| `floki` | Flokis Reaktion | keine |
| `radio` | Meldung beim Schichtende | keine |
| `killKeyFigure` | Schlüsselfigur dieser Fraktion stirbt (**endgültig**, schließt ihren Pfad) | keine |
| `world` | Folgen im Schankraum – Liste, siehe nächster Abschnitt | keine |
| `action` | dieser Ausgang wird durch eine **Handlung** erreicht, nicht durch Reden: `taeuschen`, `einschuechtern`, `bestehlen`, `angreifen` (dann ist `condition` nicht nötig) | keine |
| `actionResult` | welches Ergebnis der Handlung: `erfolg` / `fehlschlag` – bei `angreifen`: `sieg` / `niederlage` / `flucht`. Leer = jedes | leer |

Zusätzlich beim **Ereignis**: `requiresFlags` (diese Story-Flags müssen gesetzt sein) und
`blockedByFlags` (diese Flags verhindern es) – damit baut man **Folge-Ereignisse**.

## Handlungen im Gespräch: Täuschen, Einschüchtern, Bestehlen, Angreifen

Neben dem Reden kann der Wirt in **jedem** Ereignis handeln. Jede Handlung ist ein Wagnis mit einer Chance in Prozent:

| Handlung | Chance hängt ab von | Gelingt | Misslingt |
|---|---|---|---|
| **Täuschen** (Lüge ins Feld schreiben) | `wits` (1: 70 %, 2: 50 %, 3: 30 %), −15 je durchschauter Lüge; mit KI: wie glaubwürdig die KI die Lüge findet (Mittelwert) | Figur hält die Lüge für wahr (steht im KI-Prompt), Gespräch geht weiter | misstrauisch, **eine Runde weniger**, Fraktion −2 |
| **Einschüchtern** (Drohung ins Feld schreiben) | `strength` (1: 75 %, 2: 50 %, 3: 20 %), Ansehen bei der Fraktion, Waffe in der Hand +10, gewonnene Schlägereien; mit KI: Einschätzung der KI | Figur hat Angst und gibt eher nach; Gastrecht −1 | wütend, eine Runde weniger, Gastrecht −2, Fraktion −3 |
| **Bestehlen** (einmal pro Gespräch) | `wits` (wie Täuschen), −10 je Ärger | Beute aus `pockets` (Credits, Gegenstände), niemand merkt es | erwischt – Gespräch endet („Beim Stehlen erwischt“) oder eine **Schlägerei** beginnt |
| **Angreifen** | – | Das Fenster schließt, es beginnt eine **echte Schlägerei im Schankraum** (Kampfsystem wie im Technikdeck). Ergebnis: `sieg`, `niederlage` (du wirst ohnmächtig) oder `flucht` (die Figur rennt zur Schleuse) | |

**Ärger:** Zweimal durchschaut oder abgeblitzt – dann reicht es der Figur: Starke Figuren (`strength` ≥ 2) greifen an,
schwache gehen wütend („… geht wütend“). Sitzen Gäste derselben Fraktion im Schankraum, springen sie in der Schlägerei
bei (einer bei starken Figuren, bei Söldnern einer mehr).
**Niemand stirbt:** Gäste gehen k.o. und werden hinausgetragen.

**Allgemeine Ausgänge** – gelten automatisch, wenn das Ereignis keinen eigenen Ausgang mit `action` hat:

| Ausgang | Folgen |
|---|---|
| Schlägerei gewonnen | Gastrecht −10, Fraktion −8, zerbrochener Stuhl (1 Woche), Flag `schlaegerei` (2 Wochen), Radio |
| Figur flieht | Gastrecht −8, Fraktion −6, zerbrochener Stuhl, Flag `schlaegerei` |
| Du gehst k.o. | Gastrecht −15, Fraktion −2, zerbrochener Stuhl, Flag `schlaegerei` (+ Bergungsgebühr der Ohnmacht) |
| Beim Stehlen erwischt | Gastrecht −8, Fraktion −6, Flag `dieb_erwischt` (3 Wochen) |
| Figur geht wütend | Gastrecht −3, Fraktion −5 |

Alle Handlungen werden als **Taten** gespeichert (`luege`, `drohung`, `diebstahl`, `schlaegerei` mit Opfer, Fraktion,
Ergebnis, Zeugen) – Grundlage für Phase 4 (Gedächtnis, Fahndung). Die Flags `schlaegerei` und `dieb_erwischt` können
schon jetzt Folge-Ereignisse auslösen (`requiresFlags`).

**Eigener Ausgang für eine Handlung** (Beispiel aus `schlaegerei_tisch_drei`):

```json
{
  "id": "hauptmann_niedergeschlagen",
  "label": "Du schlägst den Hauptmann selbst nieder",
  "action": "angreifen",
  "actionResult": "sieg",
  "reputation": { "Soeldner": 4 },
  "gastrecht": -6,
  "result": "Der Söldnerhauptmann geht zu Boden – vor seinen eigenen Leuten. …"
}
```

Test ohne Würfelglück: `STARCAFE_ACTION_ROLL=hit` (gelingt immer) oder `=miss` (misslingt immer).

## Gedächtnis: verborgene Ausgänge, Gerüchte, Fahndung

**Verborgene Ausgänge** (`requires` am Ausgang): Solange die Bedingung nicht erfüllt ist, sieht der Spieler nur
„??? (verborgen)“ mit dem `hint`, und die KI kennt den Ausgang nicht. Sobald sie erfüllt ist, erscheint er mit ★
und die KI erfährt den `reason`.

```json
"requires": {
  "items": ["dienstchip"],              // Gegenstände im Inventar
  "flags": ["schlaegerei"],             // Story-Flags
  "deeds": ["diebstahl|erwischt"],      // frühere Taten: "art" oder "art|schlagwort"
  "minWanted": 0, "maxWanted": 0,       // Fahndungsstufe (0 = egal)
  "hint": "Hättest du etwas gegen ihn in der Hand …",
  "reason": "Der Wirt hat den Dienstchip des Offiziers."
}
```

Taten-Arten: `serviert`, `warten_lassen`, `verschuettet`, `getrunken`, `ohnmacht`, `luege`, `drohung`, `diebstahl`,
`schlaegerei`. Schlagwörter z. B. `geglaubt`, `durchschaut`, `erwischt`, `unbemerkt`, `sieg`, `niederlage`, `flucht`,
`fraktion:Imperium`, `opfer:Imperialer Offizier`, `angefangen:wirt`.

**Figuren erinnern sich:** Die KI bekommt, was die Figur selbst erlebt hat (gleicher Name) und was ihre Fraktion über
den Wirt weiß. Beobachtete Taten erfährt die Fraktion des Opfers sofort; am Schichtende verbreiten sie sich als
**Gerüchte** an andere Fraktionen, Schlägereien und erwischte Diebstähle landen manchmal im Radio („Gerüchteküche“).
Floki weiß ohnehin alles, was an Bord passiert.

**Fahndung bei der Sternenwacht (0–100):**

| Tat | Fahndung |
|---|---|
| Beim Stehlen erwischt | +15 |
| Schlägerei | +10, selbst angefangen +5 |
| Drohung vor Zeugen | +3 |
| Lüge durchschaut | +1 |
| alles gegen die Sternenwacht | doppelt (Diebstahl bei ihr auch unbemerkt +6) |
| jedes Schichtende | −8 |

Ab **30** kommen Ereignisse mit `"wantedBoost": true` öfter (Streifen, Ermittler: ×2 bei 30, ×4 bei 90),
ab **60** ist die **Durchsuchung** möglich, ab **90** die **Anhörung zur Schanklizenz**.
Felder: Ereignis `minWanted` / `maxWanted` / `wantedBoost`, Ausgang `wanted` (z. B. `-15`, wenn der Verdacht sinkt).

## Folgen im Schankraum (`world`)

Jeder Ausgang kann eine Liste von Folgen haben. Gegenstände und Figuren **bleiben** (auch im Spielstand),
bis ihre Zeit abgelaufen ist; Animationen spielen einmal, **nachdem** das Ereignisfenster geschlossen wurde.

```json
"world": [
  { "type": "animation", "animation": "shake" },
  { "type": "prop", "id": "zerbrochener_stuhl", "label": "Zerbrochener Stuhl", "spot": "Tisch3", "weeks": 2 },
  { "type": "character", "id": "nomadenkind", "label": "Nomadenkind", "faction": "Weitwanderer", "spot": "Sitzecke", "weeks": 1, "scale": 0.75 },
  { "type": "flag", "id": "licht_fuer_die_keiner_sucht" }
]
```

| `type` | Was passiert | Wichtige Felder |
|---|---|---|
| `prop` | ein Gegenstand erscheint | `id`, `label`, `spot`, `weeks`, `texture`, `color`, `scale`, `appear` |
| `character` | eine Figur erscheint und bleibt | `id`, `label`, `spot`, `weeks`, `faction`, `texture`, `scale`, `appear` |
| `animation` | einmaliger Effekt | `animation`: `shake` (Kamera wackelt), `flash` (Blitz, `color` optional), `blackout` (Licht aus, kurz), `confetti` (an `spot`) |
| `flag` | setzt ein Story-Flag | `id` |
| `remove` | entfernt Gegenstand/Figur | `id` |
| `removeFlag` | löscht ein Story-Flag | `id` |

| Feld | Bedeutung | Standard |
|---|---|---|
| `id` | eindeutig; gleiche `id` ersetzt das Alte | – |
| `label` | Name auf dem Platzhalter und in der Zusammenfassung | `id` |
| `spot` | Ort im Schankraum (siehe unten) | Raummitte |
| `offsetX` / `offsetY` | Verschiebung in Pixeln, um mehrere Dinge an einen Ort zu stellen | 0 |
| `weeks` | wie viele Wochen es bleibt (0 = für immer) | 0 |
| `texture` | Bild (`res://…png`); leer = **Platzhalter** (Kasten bzw. Figur mit Namen) | leer |
| `color` | Farbe des Platzhalters bzw. Blitzes, z. B. `"#ff4040"` | passend |
| `faction` | färbt Figuren-Platzhalter | – |
| `scale` | Größe | 1 |
| `appear` | Auftritt: `fade`, `drop` (fällt herein), `pop` (ploppt auf), `none` | `fade` |

**Orte (`spot`)** – Marker im Knoten *WorldSpots* in `Tavern.tscn` (im Editor verschiebbar, neue einfach hinzufügen):
`Tresen`, `TresenRechts`, `Tisch1`, `Tisch2`, `Tisch3`, `Tisch4`, `Sitzecke`, `Schleuse`, `Kapitaensecke`,
`Technikecke`, `Aufzug`, `Wand`, `Mitte`.

**Platzhalter → echte Grafik:** Bild in `Assets/` legen und bei `texture` eintragen – fertig.
Später können Figuren aus `character` auch ansprechbar werden (eigene Persona).

Flokis Sprüche erscheinen je nach Einstellung *Floki → Kommentare*: Aus = nie, Selten ≈ ⅓, Normal ≈ ¾, Oft = immer.

**Fraktionen** (genau so schreiben): `Imperium`, `Rebellen`, `Soeldner`, `Silberkiel`,
`GraueHaende`, `Sternenwacht`, `Funkenwerkstatt`, `Weitwanderer`.

**Radio-Stil** (Handlung 10.3): „Reichssender: …“ (Imperium, Propaganda),
„Piratensender: …“ (Rebellen), „Knotensender: …“ (lokaler Klatsch).

## Aufgaben (`task`) – vom Gespräch zur Tat

Ein Ausgang kann eine **Aufgabe** starten. Nach dem Gespräch steht dann **„Los!“** statt „Weiter“,
und der Spieler muss etwas tun (z. B. ins Technikdeck, Monster besiegen, Generator reparieren).
Das **Ergebnis der Aufgabe** wählt den endgültigen Ausgang:

```json
{
  "id": "beruhigt",
  "label": "Die Gäste warten geduldig (mehr Zeit)",
  "condition": "…",
  "task": { "id": "generator", "title": "Strom wiederherstellen", "timeLimit": 170,
            "success": "strom_schnell", "late": "strom_spaet", "fail": "strom_ohnmacht" }
},
{ "id": "strom_schnell", "label": "Licht! Du rettest den Abend", "taskOnly": true, "gastrecht": 4, "result": "…" }
```

| Feld | Bedeutung |
|---|---|
| `task.id` | Aufgabentyp (derzeit: `generator`) |
| `task.title` | Titel im Aufgabenfenster |
| `task.timeLimit` | Sekunden; wer länger braucht, bekommt `late` (0 = ohne Zeitlimit) |
| `task.success` / `late` / `fail` | Ausgänge für: in der Zeit geschafft / zu spät / gescheitert (z. B. Ohnmacht, Schichtende) |
| `taskOnly: true` | Ausgang ist **nur** Ergebnis einer Aufgabe: erscheint nicht in der Liste und die KI kann ihn nicht wählen. Braucht keine `condition`. |

So kann das Gespräch die Aufgabe beeinflussen: Wer die Gäste gut beruhigt, bekommt mehr Zeit.
Der Ausgang mit der Aufgabe wirkt sofort (z. B. Gastrecht), der Aufgaben-Ausgang danach zusätzlich.

## Tipps für gute Ereignisse

- **Jeder Ausgang braucht einen Weg im Gespräch.** Schreib in `condition`, *was der Wirt tun muss* –
  nicht nur, was passiert. Gut: „Der Wirt schlägt eine faire Lösung für die Rechnung vor und beide nehmen sie an.“
- **Die `persona` sagt der KI, wie schwer es ist:** Was überzeugt die Figur? Was lehnt sie immer ab?
- **Der Standard-Ausgang ist die Folge von Nichtstun** – meist nicht der beste.
- **Vermittlung kann scheitern:** Ein eigener Ausgang „Die Vermittlung scheitert“ für unfaire oder vage Vorschläge
  macht Konflikte spannender.
- **Keine Begriffe aus bekannten Filmen/Serien** (eigene Welt, siehe Handlung 2). Die KI ist darauf hingewiesen.
- `keywords` nur für den Offline-Modus: lieber Wortanfänge („verrat“ trifft „verraten“, „Verräter“).

## Platzhalter → echte Figuren

Solange es keine Figuren gibt, zeigt das Spiel eine farbige Karte mit dem Anfangsbuchstaben.
Später kann `speaker` auf eine echte Figur (NpcPersona mit Porträt und Stimme) zeigen –
die Ereignisse selbst müssen dafür nicht neu geschrieben werden.
