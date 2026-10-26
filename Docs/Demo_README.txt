STARCAFE – Simulator einer Raumtaverne (Demo, Arbeitsstand v0.12)
==================================================================

START
  StarCafe.exe doppelt anklicken (Windows 10/11, 64 Bit). Nichts muss installiert werden.
  Die Ordner und Dateien neben der .exe müssen zusammenbleiben:
    StarCafe.exe, StarCafe.pck, data_ai-game_windows_x86_64/

STEUERUNG
  Pfeiltasten ........ laufen
  Enter .............. mit Floki sprechen (am Roboter in der Schankstube)
  Esc ................ Pause (Ansehen, Einstellungen, KI & Stimme, "Schicht beenden")
  Schicht beenden .... eine Woche vergeht, Datum springt weiter, Spielstand wird gespeichert

KI (Gespräche)
  Ohne Schlüssel startet das Spiel mit der Offline-Demo (feste Antworten, ohne Internet).
  Für echte KI-Gespräche: Hauptmenü -> "KI & Stimme" -> Anbieter "Gemini (Google)",
  bei "Eigener Schlüssel" einen Gemini-API-Schlüssel eintragen, "KI testen" drücken.
  Der Schlüssel wird nur lokal in user://launcher.cfg gespeichert.

SPRACHE
  Diese Demo enthält die Offline-Stimmen (Piper) nicht. Es spricht die Windows-Stimme,
  oder unter "KI & Stimme" den Anbieter "Gemini (Cloud)" wählen (braucht den Schlüssel).
  Piper-Stimmen nachladen: im Projektordner  python Tools/get_voices.py  und den Ordner
  "voices" neben StarCafe.exe legen.

BEKANNTE GRENZEN DER DEMO
  - Spielbar ist die Schankstube mit Floki; weitere Figuren, Decks und Ereignisse folgen.
  - Die Kartengrafik ist teils Platzhalter (Möbel aus Fremdpaketen), die Zeichenliste liegt in Docs/.
