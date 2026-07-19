# Auftrag: Windows-Bildschirmschoner aus Three.js-Artefakten

Dieses Dokument ist ein vollständiger Arbeitsauftrag für eine neue Claude-Code-Session
auf einem Windows-Rechner mit Visual Studio Code. 
## Kontext

Es existieren fertige, eigenständig lauffähige HTML-Dateien mit Three.js-Szenen
(dunkle, düstere Wasseroberflächen mit Wellen, Gischt, lila-blauen Blitzen und
Nebel; interaktiv per Maus/Touch drehbar, zoombar, mit Splash-Effekt bei Klick/Tipp
und beim Ziehen). Diese sollen zu einem echten Windows-Bildschirmschoner (`.scr`)
verpackt werden.

## Mitgelieferte Dateien

Im selben Verzeichnis wie dieses Dokument liegen:

- `dark-water-cube-interactive.html` — Würfel
- `dark-water-cube.html` — Würfel
- `dark-water-knot-alive.html` — dieselbe Knoten-Formation mit organischer Eigenbewegung
- `dark-water-knot.html` — Torusknoten mit zwei gekreuzten Ringen
- `dark-water-sphere.html` — Sphäre

Alle teilen dieselbe Optik, dieselben Shader-Prinzipien und dieselbe
Interaktionsmechanik (Drag = Orbit-Kamera + Wasser aufwirbeln, Tap/Klick = Splash,
Wheel/Pinch = Zoom). Sie laden Three.js r128 aktuell per CDN
(`https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js`).

## Ziel

Eine `.scr`-Datei, die:

1. sich wie ein normaler Windows-Bildschirmschoner verhält (Vollbild, beendet sich
   bei Eingabe, unterstützt Vorschau und Einstellungsdialog),
2. eine der Szenen als WebView2-Inhalt rendert,
3. über den Einstellungsdialog auswählen lässt, **welche** Szene läuft, **oder**
   ob die Szenen **zufällig alle X Minuten wechseln** (X vom Nutzer einstellbar),
4. komplett offline funktioniert (kein CDN-Zugriff nötig, da Bildschirmschoner oft
   im gesperrten Zustand ohne Internetzugriff laufen).

## Anforderungen an die Umsetzung ##
verwende nur aktuelle .NET LTS, WPF und das WebView2-NuGet-Paket, keine anderen Frameworks oder Bibliotheken. Alle Artefakte müssen lokal im Projekt eingebunden werden, keine
externen Abhängigkeiten. Der Screensaver muss auf allen Windows-Versionen ab Windows 10 zuverlässig laufen. Für alle Pakete oder Software, die du verwenden möchtest, frage mich vorher, ob ich sie genehmige.
Wenn es Abhängigkeiten zu .NET Frameworks gibt, liste mit vorher auf, welche ich lokal installieren muss.
Führe keine Befehle für die Anlage des .NET Projektes selber aus, sondern erstelle mit zuerst eine Liste der Software, die für die Entwicklung benötigt wird und Liste mir dei Befehle zum Erstellen des Projektes auf, die ich dann selber ausführen werde.

## Funktionale Anforderungen

### 1. Bildschirmschoner-Grundverhalten (Windows-Konvention)

Eine `.scr`-Datei ist technisch eine `.exe`, die auf feste Kommandozeilenargumente
reagiert:

| Aufruf | Bedeutung |
|---|---|
| `Name.scr` (ohne Argument) | wie `/c` behandeln |
| `Name.scr /s` | Vollbild-Modus starten (aktiver Bildschirmschoner) |
| `Name.scr /c` oder `/c:<HWND>` | Einstellungsdialog öffnen (modal) |
| `Name.scr /p <HWND>` | Miniaturvorschau in das übergebene Fensterhandle rendern (Anzeigeeinstellungen-Dialog von Windows) |

Verhalten im `/s`-Modus:

- Randloses Vollbildfenster ohne Taskleiste, **ein Fenster pro angeschlossenem
  Monitor** (Mehrschirm-Setup: gleiche Szene auf allen Monitoren spiegeln, kein
  Sync-Zwang zwischen den Instanzen nötig).
- Mauszeiger ausblenden.
- **Bei jeder relevanten Eingabe sofort beenden**: Mausbewegung oberhalb einer
  kleinen Toleranzschwelle (z. B. > 10 px, um Sensor-Rauschen abzufangen),
  Mausklick, Tastendruck. Dieser Exit-Mechanismus muss auf **Fenster-/OS-Ebene**
  abgefangen werden (WPF-Events auf dem Host-Fenster), nicht im WebView-Inhalt —
  die Artefakte selbst reagieren mit eigener Kamerasteuerung auf Maus/Touch, das
  ist im Screensaver-Kontext irrelevant und soll die Beendigung nicht verhindern.
- Kein Kontextmenü, keine DevTools (F12) im WebView2 zulassen.

### 2. Szenen-Auswahl & Zufallswechsel (Einstellungsdialog, `/c`)

Ein einfacher WPF-Dialog mit:

- Radio-Buttons oder Dropdown zur Auswahl einer festen Szene (Würfel / Sphäre /
  Knoten / Knoten lebendig).
- Checkbox „Zufällig wechseln“ mit einem daneben liegenden Zahlenfeld
  „alle **X** Minuten“ (sinnvoller Default: 10, Grenzen z. B. 1–120).
- Ist „Zufällig wechseln“ aktiv, wird die feste Szenenauswahl deaktiviert/ignoriert.
- Einstellungen werden persistiert unter
  `HKEY_CURRENT_USER\Software\DarkWaterScreensaver` mit Werten etwa:
  `Mode` (`Fixed` / `Random`), `SceneFile` (Dateiname), `IntervalMinutes` (int).
- Im `/s`-Modus: falls `Mode = Random`, beim Start eine zufällige Szene laden und
  per Timer alle `IntervalMinutes` Minuten auf eine andere zufällige Szene
  wechseln (nicht zweimal hintereinander dieselbe, falls mehr als eine Szene
  verfügbar ist). Der Szenenwechsel soll die WebView2-Quelle neu laden (Navigate),
  ohne dass Nutzer-Interaktion nötig ist — das darf **nicht** als „Eingabe“ zum
  Beenden des Screensavers zählen.

### 3. Rendering / WebView2-Einbindung

- .NET LTS + WPF, NuGet-Paket `Microsoft.Web.WebView2`.
- Ein `WebView2`-Steuerelement pro Fenster, das per `Navigate` bzw.
  `CoreWebView2.Navigate(...)` auf eine lokale Datei zeigt
  (`file:///.../Assets/scenes/<datei>.html`).
- WebView2 muss die volle Fenstergröße einnehmen und bei Resize mitskalieren
  (die Artefakte lesen `window.innerWidth/innerHeight`, das funktioniert
  automatisch korrekt, solange das WebView2-Element die Fenstergröße ausfüllt).
- Für den `/p`-Vorschau-Modus: WebView2 in ein Child-Fenster einbetten, das als
  Kind-Fenster des übergebenen `<HWND>` gesetzt wird (`SetParent`-API), passend
  auf dessen Client-Rect skaliert.

### 4. Offline-Fähigkeit (lokales Three.js)

Alle vier HTML-Dateien laden Three.js aktuell per `<script src="https://cdnjs...">`.
Für den Screensaver-Build:

- `three.min.js` (Revision r128, identisch zur aktuell verwendeten CDN-Version)
  lokal herunterladen und im Projekt unter `Assets/scenes/vendor/three.min.js`
  ablegen.
- In allen HTML-Dateien den `<script src="...cdnjs...">`-Tag durch einen
  relativen lokalen Pfad ersetzen, z. B. `<script src="vendor/three.min.js">`.
- Die angepassten HTML-Dateien landen zusammen mit dem `vendor`-Ordner unter
  `Assets/scenes/` im Projekt und werden als Content mit „Copy to Output
  Directory“ eingebunden (bzw. als eingebettete Ressourcen, falls eine
  Single-File-Distribution gewünscht ist).

## Technische Vorgaben

- **Projektname:** frei wählbar, Vorschlag `DarkWaterScreensaver`.
- **Struktur (Vorschlag):**
  ```
  DarkWaterScreensaver/
    Program.cs                 -- Einstiegspunkt, parst Kommandozeile, verzweigt in /s /c /p
    ScreensaverWindow.xaml(.cs) -- Vollbild-Fenster mit WebView2, Input-Exit-Logik, Zufalls-Timer
    SettingsWindow.xaml(.cs)   -- Einstellungsdialog (/c)
    Settings.cs                -- Registry-Zugriff (lesen/schreiben)
    Assets/scenes/
      dark-water-cube-interactive.html
      dark-water-sphere.html
      dark-water-knot.html
      dark-water-knot-alive.html
      vendor/three.min.js
    DarkWaterScreensaver.csproj
  ```
- **Kommandozeilen-Parsing:** Windows ruft `.scr`-Dateien mit Argumenten auf, die
  je nach Aufrufer unterschiedlich formatiert sein können (`/s`, `-s`, `/S`,
  `/c:12345`, `/c 12345`, `/p 12345` …) — robust gegen Groß-/Kleinschreibung und
  Trennzeichen parsen.
- **Build-Output:** Nach dem Build die erzeugte `DarkWaterScreensaver.exe` in
  `DarkWaterScreensaver.scr` umbenennen (per Post-Build-Event im `.csproj`
  automatisierbar). Installation testweise per Rechtsklick → „Installieren“ auf
  die `.scr`-Datei, oder manuell nach `C:\Windows\System32` kopieren.

## Bekannte Anpassungen an den Artefakten

Diese Änderungen an den HTML-Dateien sind für den Screensaver-Betrieb nötig
und sollen als Kopien im `Assets/scenes/`-Ordner erfolgen (Originale unangetastet
lassen):

1. CDN-Script-Tag durch lokalen Pfad ersetzen (siehe oben).
2. Der Hinweistext unten im Bild (`#hint`, „Tippen: Splash · Ziehen: drehen &
   aufwirbeln · Rad / Pinch: Zoom“) kann im Screensaver-Kontext optional entfernt
   oder ausgeblendet werden, da er im `/s`-Modus keinen Sinn ergibt (jede Eingabe
   beendet den Screensaver ohnehin sofort). Einfachste Lösung: Per URL-Parameter
   steuern, z. B. `?mode=saver` an die Navigate-URL anhängen und im Script mit
   `new URLSearchParams(location.search).get('mode')` auslesen, dann den Hint-Div
   per JS ausblenden (`display: none`).

## Akzeptanzkriterien

- `Name.scr /s` läuft im Vollbild auf allen Monitoren, zeigt die konfigurierte
  Szene (bzw. bei „Zufällig“ eine zufällige, mit Wechsel nach X Minuten), und
  beendet sich zuverlässig bei Mausbewegung, Klick oder Tastendruck.
- `Name.scr /c` öffnet den Einstellungsdialog, Auswahl wird in der Registry
  gespeichert und beim nächsten `/s`-Aufruf korrekt angewendet.
- `Name.scr /p <HWND>` zeigt eine funktionierende Miniaturvorschau im
  Windows-Anzeigeeinstellungen-Dialog.
- Läuft ohne Internetverbindung fehlerfrei (lokales Three.js).
- Rechtsklick-Kontextmenü und DevTools sind im `/s`-Modus deaktiviert.

## Vorgehen

1. Projekt anlegen (.NET LTS WPF), WebView2-NuGet-Paket einbinden.
2. Vier HTML-Dateien gemäß obigem Abschnitt anpassen und mit lokalem
   `three.min.js` unter `Assets/scenes/` ablegen.
3. Kommandozeilen-Parsing in `Program.cs` implementieren, Verzweigung in die drei
   Modi.
4. `ScreensaverWindow` mit WebView2, Vollbild-Verhalten, Input-Exit-Logik und
   Zufalls-Timer für den Szenenwechsel bauen.
5. `SettingsWindow` mit den beschriebenen Steuerelementen und Registry-Anbindung
   bauen.
6. `/p`-Vorschau-Einbettung implementieren (`SetParent` auf das übergebene HWND).
7. Post-Build-Umbenennung `.exe` → `.scr` einrichten, lokal installieren und alle
   vier Modi durchtesten (inkl. Mehrschirm, falls verfügbar).
