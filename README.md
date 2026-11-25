# StarCafe – Simulator einer Raumtaverne

KI-gesteuertes 2D-Dialogspiel mit der Godot Engine (C#). Der Spieler führt eine Taverne an Bord eines Raumschiffs,
die Figuren antworten per KI (Gemini). Projektarbeit (FOI) an der BBS am Pottgraben.

## Projekt in Godot öffnen

Benötigt:

| Was | Version |
|---|---|
| Godot **.NET / Mono**-Version (nicht die normale!) | **4.7.2** |
| .NET SDK | **8.0** oder neuer (`dotnet --version`) |

Schritte:

1. Repository klonen: `git clone https://github.com/Faal12/StarCafe.git`
2. Godot **4.7.2 (.NET)** starten → **Import** → die Datei `project.godot` wählen.
3. Beim ersten Öffnen importiert Godot alle Bilder (dauert eine Minute) und baut das C#-Projekt.
   Falls Godot fragt, ob das C#-Projekt gebaut werden soll: **Ja** (oder oben rechts auf den Hammer klicken).
4. Mit **F5** starten. Startszene ist `Launcher.tscn` (Hauptmenü), das Spiel selbst ist `Tavern.tscn`
   (mit **F6** startet nur die Taverne direkt).

Der Ordner `.godot/` entsteht automatisch und ist nicht im Repository.

## Steuerung

| Taste | Aktion |
|---|---|
| **WASD** | laufen (Umschalt = vorsichtig, z. B. mit vollem Tablett) |
| **E** | benutzen / reden (Floki, Gäste, Bar, Aufzug, Generator …) |
| **Linksklick / Leertaste** | Schlag (Richtung Mauszeiger / Laufrichtung) |
| **Rechtsklick / F** | ausgewählten Gegenstand benutzen (Getränk trinken = heilen) |
| **1–8 / Mausrad** | Schnellleiste · **Tab** Inventar |
| **Esc** | Pause: Ansehen, Einstellungen, „KI & Stimme“, „Schicht beenden“ |

Alle Tasten sind Aktionen unter *Projekt → Projekteinstellungen → Input Map* und dort änderbar.
Debug: **Strg+Umschalt+E** löst sofort ein Ereignis aus (nicht F8 – das beendet im Editor das laufende Spiel).

## KI und Sprachausgabe

* **Ohne Schlüssel** startet das Spiel mit der Offline-Demo (feste Antworten).
* **Echte KI-Gespräche:** Hauptmenü → „KI & Stimme“ → Anbieter „Gemini (Google)“ und einen eigenen
  API-Schlüssel eintragen. Alternativ die Umgebungsvariable `GEMINI_API_KEY` setzen (Godot danach komplett neu starten).
  Schlüssel gehören **nie** ins Repository; sie werden nur lokal in `user://launcher.cfg` gespeichert.
* **Stimmen:** Standard ist Piper (offline). Das Voice-Paket (ca. 176 MB) liegt nicht im Repository.
  Laden mit `python Tools/get_voices.py` (legt den Ordner `voices/` an). Ohne Paket spricht die Windows-Stimme.

## Ordner

| Ordner | Inhalt |
|---|---|
| `Scripts/` | C#-Code: `Player`, `Dialogue`, `Llm` (KI-Anbieter), `Voice`, `Reputation`, `Time`, `Ui`, `Launcher`, `Core`, `Events` (Ereignisse), `Bar` (Mixen, Tablett), `Guests`, `Items` (Inventar), `Combat` (Kampf, Monster), `Decks` (Aufzug, Wohn- und Technikdeck), `Tasks` (Aufgaben), `Minigames`, `World` (Folgen im Schankraum) |
| `Data/` | Spielinhalte als JSON, ohne Code änderbar: Ereignisse, Bar (Zutaten, Rezepte, Gäste), Gegenstände – Übersicht in `Data/README.md` |
| `Resources/Npc/` | Figuren (Persona-Prompts), z. B. `Floki.tres` |
| `Assets/` | Grafiken, die das Spiel wirklich lädt (Ship, Sprites/Player, Portraits, UI) |
| `Docs/` | Handlung (`Handlung.md`), Zeichenlisten für Grafik, Demo-README, **Leitfaden für Oberfläche und Grafik** (`UI_Leitfaden.md`) |
| `Tools/` | Hilfsskripte (Python): Voice-Paket laden, Spielerbilder und UI-Grafik aus den Quellbildern bauen |
| `export_presets.cfg` | Export-Einstellung „Windows Demo“ |

## Arbeiten im Team

* **Szenen (`*.tscn`) nur in Godot bearbeiten** und nicht gleichzeitig von zwei Personen ändern, sonst gibt es
  Merge-Konflikte. Deshalb ist die Taverne geteilt: `Tavern.tscn` (Welt, Logik) und `Scenes/Ui/TavernUi.tscn`
  (alle Fenster und HUDs) – jede Seite arbeitet in ihrer eigenen Szene.
* **Frontend & Backend arbeiten parallel:** Spiellogik spricht die Oberfläche nur über `Scripts/Core/UiBus.cs` an.
  Wer was besitzt und wie man zusammenarbeitet: `Docs/UI_Leitfaden.md` → „Zusammenarbeit“. Zuständigkeiten
  stehen in `.github/CODEOWNERS`.
* **Ablauf:** `main` nur über Pull Requests; eigene Branches (`ui/…` fürs Frontend, `feature/…` fürs Backend);
  kleine PRs, `main` täglich in den eigenen Branch holen.
* Neue Grafiken nur einchecken, wenn das Spiel sie wirklich benutzt. Rohdateien und große Quellbilder bleiben lokal.
* Vor dem Push: Projekt in Godot starten und prüfen, dass es baut (`dotnet build` im Projektordner).

## Demo-Build (Windows)

Godot-Exportvorlagen 4.7.2 (.NET) installieren → Projekt → Exportieren → „Windows Demo“. Fertige Demo:
`build/StarCafe_Demo/StarCafe.exe` (Ordner `build/` wird nicht eingecheckt).
