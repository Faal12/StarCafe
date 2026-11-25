# StarCafe – Spielsysteme im Überblick

| Bereich | Wo ändern | Anleitung |
|---|---|---|
| Ereignisse, Konflikte, Aufgaben | `Data/Events/*.json` | `Data/Events/README.md` |
| Bar: Zutaten, Rezepte, Gäste, Bestellungen | `Data/Bar/*.json` | `Data/Bar/README.md` |
| Gegenstände & Waffen | `Data/Items/items.json` | unten |

## Steuerung

| Taste | Aktion |
|---|---|
| **WASD** | laufen |
| **Umschalt** | vorsichtig gehen (Tablett) |
| **E** | benutzen / reden / bestellen / servieren / Aufzug |
| **Linksklick** | Schlag in Richtung Mauszeiger |
| **Leertaste** | Schlag in Laufrichtung (in Minispielen: Ventil) |
| **Rechtsklick / F** | ausgewählten Gegenstand benutzen (Getränk trinken = heilen) |
| **1–8 / Mausrad** | Schnellleiste |
| **Tab** | Inventar |
| **Esc** | schließen / Pause |

Alle Tasten sind Aktionen in *Projekteinstellungen → Input Map* (`move_*`, `interact`, `walk_careful`, `attack`, `use_item`, `debug_event`) und dort änderbar.

## Decks & Aufzug

Das Schiff hat drei Decks, alle in `Tavern.tscn` (weit auseinander, damit Zeit, Gäste und Ereignisse weiterlaufen):
**Deck 1 Schankraum**, **Deck 2 Wohndeck** (Bett: Schicht beenden; hier wachst du nach einer Ohnmacht auf;
zwei Personalzimmer für später), **Unterdeck Technik** (Hauptgenerator, Rohre, Kisten, Werkzeugkiste mit der
Rohrzange, Flokis Rechenkern, die **versiegelte Tür zum Frachtraum**). Aufzug: **E** am Aufzug, Deck wählen.

**Licht:** Ohne Strom (Stromausfall) ist das ganze Schiff dunkel und du hast eine Taschenlampe.
Im Technikdeck gibt es auch mit Strom nur Notbeleuchtung.

## Kampf (im Stil von Stardew Valley)

- Die Waffe ist der **ausgewählte Platz der Schnellleiste** (ohne Waffe: Fäuste, 1 Schaden).
- Schläge treffen in einem Bogen vor dir, stoßen zurück, zeigen Schadenszahlen.
- **Leben (HP)** und **Energie (EN)** unten rechts. Schläge kosten Energie; sie erholt sich in Ruhe. Ohne Energie: schwächer und langsamer.
- Nach einem Treffer kurz unverwundbar (Blinken).
- **Getränke heilen** (+12 HP, +20 EN), Rezepte mit Wirkung *Heilung* (Glühender Schleimer) viel mehr (+60 HP).
- **Ohnmacht** bei 0 HP: Floki holt dich ins Bett, du verlierst bis zu ₡30, eine laufende Aufgabe scheitert.
- Gäste werden später (Phase 3) nur k.o. geschlagen, Monster sterben.

**Waffen** (`Data/Items/items.json`): `damage`, `cooldown` (Sekunden zwischen Schlägen), `reach` (Reichweite in Pixeln), `knockback`.
Barhockerbein (Start, schnell, schwach) · Rohrzange (Werkzeugkiste im Technikdeck, kräftig, auch zum Reparieren) · Schockstab (Platzhalter).

## Monster (Technikdeck)

| Monster | Verhalten | Tipp |
|---|---|---|
| **Kabelfresser** | klein, schnell, im Schwarm; zittert kurz, dann **Sprung** | nach dem Zittern zur Seite gehen |
| **Rohrkriecher** | sitzt in Rohröffnungen, kommt heraus, **leuchtet auf und spuckt Säure** | nur angreifbar, solange er draußen ist |
| **Brutknoten** | wächst am Generator, **brütet Kabelfresser**, lässt eine **Schockwelle** los (Ring leuchtet vorher auf) | aus dem Ring gehen, wenn er leuchtet; zuerst ihn töten |

Monster lassen **Alienschleim** fallen (fliegt dir zu, wenn du nah bist) – eine Zutat für die Bar.
Code: `Scripts/Combat/Monsters.cs` (Werte wie Leben, Schaden, Geschwindigkeit stehen oben in jeder Klasse).

## Minispiele

| Minispiel | Ablauf | Wo |
|---|---|---|
| **Am Tresen** | Getränke mixen (Geschmacksachsen, Rezepte) | Bar |
| **Tablett** | Getränke tragen, ohne zu verschütten | überall |
| **Schaltkreis** | Leitungsstücke drehen (Links-/Rechtsklick), bis Strom vom Eingang zum Generator fließt. Rote Stücke sind kaputt, leiten nicht und **sprühen Funken**, wenn man Strom hineinleitet. Immer lösbar, 60 s | Generator |
| **Druckventil** | Druck steigt ruckartig, **Druckstöße** werden angekündigt. Leertaste/Maus halten = Ventil auf. 7 s im (wandernden) grünen Bereich halten. Über 100 bar = Dampfstoß (Schaden, Fortschritt weg). 50 s | Generator |

Geplant: Taschendiebstahl, Schlossknacken (versiegelte Tür), Funkpeilung, Andocken.

## Aufgaben

Ein Ausgang eines Ereignisses kann eine **Aufgabe** starten (`"task"`, siehe `Data/Events/README.md`).
Erste Aufgabe: **„Strom wiederherstellen“** (Ereignis *Stromausfall*): Gäste beruhigen (= mehr Zeit),
mit dem Aufzug ins Technikdeck, Brutknoten vernichten, Rohrzange holen, Generator reparieren (Schaltkreis + Druckventil).
Je nachdem, ob du es in der Zeit schaffst, zu spät kommst oder ohnmächtig wirst, gibt es einen anderen Schluss.
Oben links zeigt ein Aufgabenfenster die Schritte und die Zeit.

Neue Aufgabentypen: Klasse wie `Scripts/Tasks/GeneratorTask.cs` anlegen und in `TaskService.Create` eintragen.

## Test-Schalter (nur Editor/Debug)

| Umgebungsvariable | Wirkung |
|---|---|
| `STARCAFE_EVENT=<id>` | Strg+Umschalt+E öffnet genau dieses Ereignis |
| `STARCAFE_OFFLINE=1` | Ereignisse im Offline-Modus (Schlüsselwörter statt KI) |
| `STARCAFE_NO_EVENTS=1` | keine automatischen Ereignisse |
| `STARCAFE_TEST_AUTOWIN=1` | Minispiele gelten nach 1,5 s als gewonnen (für automatische Tests) |
