# Kinect-Cave-Spiel – Einrichtung & Schritt-für-Schritt-Anleitung

Diese Demo setzt die Aufgabe aus `Unity Kinect Cave Game.pdf` um: Der Spieler steht still in der
Höhle, **Wände mit posenförmiger Öffnung** kommen auf ihn zu. Passt sein per Kinect getracktes Skelett
durch die Öffnung → **+100 Punkte**; berührt ein Körperteil die Wand → **ein Leben weg** (Start: 3).
Bei 0 Leben → Game Over + Highscore. Der **Startbildschirm** bleibt erhalten. Gestartet wird über genau
einen digitalen physischen Knopf auf dem originalen CAVE-Podest – **ohne sichtbaren Cursor**.

Unity-Version: **2022.3.59f1** (Built-in Render Pipeline). CAVE-Paket `de.htw-berlin.cave` ist bereits
eingebunden.

---

## 0. Turnkey: pullen → starten (Kurzfassung)

Damit „auf dem CAVE-Rechner pullen und starten" ohne manuelles Aufsetzen klappt, richtet sich das
Projekt beim ersten Öffnen in Unity **automatisch** ein (`Assets/Editor/CaveGameSetup.cs`,
`[InitializeOnLoad]`):

1. legt die Layer **Player** und **Wall** an,
2. stellt die Wand-Texturen lesbar (`Read/Write`, `Alpha Is Transparency`),
3. erzeugt aus eurer funktionierenden `MainScene` eine **saubere Spielszene** `Assets/Scenes/CaveGame.unity`
   (CAVE-Rig + Avatar bleiben; CAVE-Demo-Objekte und die alte Block-Mechanik werden entfernt),
4. trägt diese Szene als Start-Szene in die **Build Settings** ein.

Alles ist idempotent und greift nur, wenn nötig. Manuell erneut auslösbar über
**`Tools ▸ CaveGame ▸ Setup ausführen`** bzw. **`… ▸ Saubere Spielszene neu erzeugen`**.

**Ablauf am Entwicklungs-Rechner (einmalig):**
1. Projekt in Unity 2022.3.59f1 öffnen → Auto-Setup läuft, Konsole zeigt „Einrichtung abgeschlossen".
2. **Alles committen & pushen** – wichtig: die generierte `Assets/Scenes/CaveGame.unity` **inklusive
   `.meta`**, die neuen Skripte + `.meta`, die Wand-PNGs, sowie `ProjectSettings/TagManager.asset`
   und `ProjectSettings/EditorBuildSettings.asset`. (`.gitignore` trackt `.meta` bereits korrekt;
   `Library/` wird nicht versioniert.)

**Ablauf am CAVE-Rechner:**
1. `git pull`.
2. Projekt in Unity öffnen (oder vorhandenes „Build" der `.exe` direkt starten).
3. **Play** drücken bzw. das gebaute Programm starten. Kinect-Sensor angeschlossen, Kinect-v2-Runtime
   installiert (siehe Abschnitt 10).

> Hinweis: Falls du am Dev-Rechner Unity **nicht** öffnest, sondern nur die von mir erstellten Dateien
> pushst, existiert `CaveGame.unity` noch nicht – dann zeigen die Build Settings übergangsweise auf die
> `MainScene` (funktioniert ebenfalls). Beim ersten Öffnen am CAVE-Rechner wird die saubere Szene dort
> erzeugt.

---

## 1. Projektstruktur (was wurde geliefert)

```
Assets/Scripts/CaveGame/
  Core/    GameState.cs, GameManager.cs, GameBootstrapper.cs
  Kinect/  KinectPlayerPresence.cs, KinectHandInteractor.cs
  Button/  PhysicalStartPodestController.cs, PodestVoiceControl.cs
  Gameplay/WallColliderBuilder.cs, Wall.cs, WallMover.cs, WallHitZone.cs,
           WallSpawner.cs, PlayerBodyPart.cs
  UI/      CaveUiFactory.cs, IngameHud.cs, SkeletonPreviewGraphic.cs, GameOverController.cs

Assets/Editor/
  CaveGameSetup.cs   (Auto-Einrichtung: Layer, Texturen, Spielszene, Build Settings)

Assets/TetrisCave/Walls/
  Resources/Walls/   wall_01..10_*_cutout.png   (Wand-Sprites, werden automatisch geladen)
  Hole_Masks/        hole_01..10_*_mask.png     (Referenz/Debug, optional)
  Preview/           wall_cutouts_preview.png
  WALLS_README.md
```

**Wiederverwendet:** `HumanTetrisHighscore` (Score/Highscore via PlayerPrefs),
`HumanTetrisStartMenu` (Startbildschirm), `PositionTransferMultiple` (Kinect-Avatar / „Skelettmodell").

**Hinweis:** Der ältere `BlockSpawner`/`BlockMover` (taumelnde Tetromino-Blöcke) ist eine andere
Mechanik und bleibt unangetastet liegen. Er wird vom neuen GameManager **nicht** verwendet und kann
entfernt werden, wenn er nicht gebraucht wird.

---

## 2. Selbst-Aufbau (das meiste passiert automatisch)

`GameBootstrapper` erzeugt beim Szenenstart automatisch:
`Game Manager`, `Kinect Player Presence`, `Kinect Hand Interactor`, `Wall Spawner`,
`Ingame HUD`, `Game Over UI` und den Controller für das vorhandene CAVE-Podest. Die vier Sample-Knöpfe
werden entfernt und durch einen einzelnen mittigen Startknopf ersetzt.

Du musst also **nur** dafür sorgen, dass die Szene folgendes enthält (siehe Schritt 3):
das **CAVE-Rig** (Kinect) und den **Avatar-Tracker** (`PositionTransferMultiple`).

> **Parameter dauerhaft ändern:** Die Auto-Objekte existieren nur zur Laufzeit. Wenn du Werte fest
> einstellen willst, lege das jeweilige System **selbst** als leeres GameObject in die Szene und hänge
> die Komponente an (z. B. `Wall Spawner` → `WallSpawner`). Der Bootstrapper erkennt vorhandene
> Komponenten und überspringt sie dann – deine Inspector-Werte bleiben erhalten.

---

## 3. Spielszene einrichten

> In der Regel **nicht nötig** – das Auto-Setup (Abschnitt 0) erzeugt die Spielszene bereits aus eurer
> `MainScene`. Dieser Abschnitt beschreibt den **manuellen Aufbau** als Fallback / zum Verständnis.

1. Neue Szene anlegen (oder vorhandene Spielszene öffnen) und in die **Build Settings** aufnehmen
   (`File ▸ Build Settings ▸ Add Open Scenes`).
2. **CAVE-Rig** hinzufügen: das Prefab `Assets/Samples/CAVE/1.1.1/.../CAVE.prefab` in die Szene ziehen.
   Es enthält Kamera, `KinectManager` und `KinectTracker`. (Ohne Kinect-Hardware läuft die Demo per
   Tastatur-Fallback, siehe Schritt 9.)
3. **Avatar-Tracker** hinzufügen: leeres GameObject `Avatar Tracker` anlegen und `PositionTransferMultiple`
   anhängen. Im Inspector zuweisen:
   - `handPrefab`, `headPrefab`, `bodyPrefab` (z. B. die CAVE-Sample-Prefabs `Hand` / `BodyParts`),
   - optional `particlePrefab` und `collisionSound`.
4. **Boden/Höhle** (optional, rein visuell): z. B. die vorhandenen Modelle `bodenT2/bodenT3` platzieren.
5. Für die Handprojektion wird automatisch zuerst `Virtual Camera Front L`, danach `Front R` und
   schließlich die `MainCamera` verwendet.

Beim Start ist die **Szenen-Hierarchie** dann etwa:

```
CAVE (KinectManager, KinectTracker, Kamera …)
Avatar Tracker (PositionTransferMultiple)
[Boden/Höhle]
— zur Laufzeit ergänzt der Bootstrapper —
Game Manager
Kinect Player Presence
Kinect Hand Interactor → "Interaction Point (invisible)"
Wall Spawner → Wall_… (gespawnte Wände)
Ingame HUD / Game Over UI / HumanTetris Start Menu / EventSystem
Physical Start Podest Controller
```

---

## 4. Layer, Tags & Physik

Die Wand-Treffer funktionieren dank Marker-Komponente **sofort** (auch ohne extra Layer). Für eine
saubere Physik wird trotzdem empfohlen:

1. `Edit ▸ Project Settings ▸ Tags and Layers`: Layer **`Player`** und **`Wall`** anlegen.
   (Die Avatar-Körperteile bekommen dann automatisch den Layer `Player`, Wände den Layer `Wall`.)
2. `Edit ▸ Project Settings ▸ Physics ▸ Layer Collision Matrix`: Häkchen **`Player` ↔ `Wall`** aktiviert
   lassen (Standard ist alles aktiv).

Automatisch konfiguriert (per Code):
- Avatar-Körperteile: kinematischer Rigidbody (kein Gravity) + Collider + Marker `PlayerBodyPart`.
- Wände: kinematischer Rigidbody + aus dem Alphakanal erzeugte **BoxCollider (Trigger)**.
- Interaktionspunkt der Hand: SphereCollider (Trigger) + kinematischer Rigidbody, **kein Renderer**
  (unsichtbar → kein Cursor).

> **Trade-off:** Die Körperteile sind jetzt kinematisch (korrekt für positions­gesteuerte Collider).
> Eine evtl. Hand-zu-Hand-Kollision aus dem Echo-Motion-Feature (`BodyCollision`, OnCollisionEnter)
> feuert dadurch nicht mehr. In dieser Demo nicht relevant.

---

## 5. Wand-Grafiken

Die 10 Wand-Sprites liegen bereits unter `Assets/TetrisCave/Walls/Resources/Walls/` und werden vom
`WallSpawner` **automatisch** geladen (als Texture2D, daraus werden zur Laufzeit Sprites erzeugt).

Empfohlene Import-Einstellungen (im Inspector der PNGs, dann `Apply`):
- **Alpha Is Transparency:** an (saubere Kanten an der Öffnung).
- **Read/Write:** optional. Der `WallColliderBuilder` liest den Alphakanal; ist Read/Write aus, erzeugt
  er automatisch eine lesbare Kopie (über `Graphics.Blit`). Funktioniert also so oder so.

Eigene Wände hinzufügen: weitere PNGs (graue Wand, **Pose transparent ausgestanzt**) in
`Resources/Walls/` legen – sie werden automatisch mit aufgenommen. Alternativ im `WallSpawner` das Feld
`wallSprites` manuell füllen (hat Vorrang vor dem Resources-Laden).

---

## 6. Wände auf den Spieler kalibrieren (wichtig!)

Die Öffnung muss dort sitzen, wo der getrackte Avatar steht. Lege dazu einen **`Wall Spawner`** fest in
die Szene (siehe Schritt 2-Hinweis) und stelle ein:

- `wallCenter` (X, Y): Mittelpunkt der Wand auf die Körpermitte des Avatars legen. Tipp: Im Play-Mode
  die Position eines Avatar-Körperteils (z. B. „SpineMid") in der Scene-View ablesen.
- `targetWorldHeight`: Welthöhe einer Wand an die Reichweite/Größe des Spielers anpassen
  (Default 3.2 m). Die Öffnung skaliert mit.
- `playerPlaneZ`: Z-Position, an der der Avatar steht (Default 0). **Achtung:** Der Avatar wird
  gespiegelt (siehe `PositionTransferMultiple.mirrorPlanePoint/mirrorNormal`); seine Z-Position kann
  von 0 abweichen. Entweder hier anpassen **oder** Kinect-Rig/Spiegelung so legen, dass der Avatar bei
  z ≈ 0 steht.
- `spawnZ` / `despawnZ`: Start- bzw. End-Z (Default 40 / -20, wie beim vorhandenen BlockSpawner).
- `colliderThickness`: Welt-Tiefe der Wand-Collider (Default 0.3 m). Bei sehr schnellen Wänden ggf.
  erhöhen, damit kein Körperteil „durchtunnelt".
- `wallYRotation`: Falls die Wand abgewandt erscheint oder die Pose seitenverkehrt zum Avatar ist,
  `180` ausprobieren.
- `gridColumns` / `alphaThreshold`: Auflösung bzw. Schwelle der Collider-Generierung
  (Default 48 / 0.5). Höher = genauere Öffnung, mehr Collider.

Schwierigkeit: `baseSpeed`, `maxSpeed`, `speedGrowthPerSecond`, `baseInterval`, `minInterval`,
`rampSeconds`.

---

## 7. Physischer Podest-Startknopf

Das Podest und seine Maße/Position stammen aus dem offiziellen CAVETools-Sample. Die vier dortigen
`ObjectToggleButton`-Knöpfe werden entfernt. `PhysicalStartPodestController` setzt stattdessen genau
einen mittigen Startknopf ein (bei Game Over zwei Knöpfe: Neustart / Menü). Rot = „Tracking noch nicht
stabil“, Grün = „startbereit“.

**Auslösung (robust):** Statt eines winzigen, schwer zu treffenden Trigger-Cubes wird **distanzbasiert
mit kurzem Halten** ausgelöst. Die Erkennungszone ist ein **Zylinder über dem Knopf**: seitlich
`activationRadius` (Standard 0.24 m), nach oben `verticalTolerance` (Standard 0.35 m) –
die Hand schwebt naturgemäß *über* dem Knopf. Geprüft werden **beide** getrackten Hände (unabhängig
geglättet), nicht mehr nur eine flatternde „aktive“ Handauswahl. Kurz halten (`holdTime`,
Standard 0.5 s – lang genug, dass Vorbeiwischen nicht auslöst), dann löst der Knopf aus. Der Knopf
füllt sich dabei sichtbar von Grün → Druckfarbe und **wächst** leicht mit dem Fortschritt.

Gegen Tracking-Zittern gibt es zwei Sicherungen:
- **Hysterese** (`holdZoneScale`, Standard 1.25): Während des Haltens wächst die Zone – kleines
  Zittern bricht den Fortschritt nicht ab. Der gehaltene Knopf gewinnt außerdem knappe Duelle
  gegen den Nachbarknopf (Game Over), damit der Fortschritt nicht ständig zurückspringt.
- **Aussetzer-Gnadenfrist** (`dropoutGrace`, Standard 0.35 s): Verliert die Kinect die Hand kurz,
  friert der Fortschritt ein, statt sich zu entleeren.

Zu empfindlich (löst versehentlich aus)? → `activationRadius`/`verticalTolerance` verkleinern oder
`holdTime` erhöhen. Zu schwergängig? → umgekehrt.

**Neustart/Menü (Game Over) sind bewusst strenger** als der Start-Knopf, weil der Spieler dort
noch in Bewegung direkt am Podest steht und die zwei Zonen sonst überlappen:
`gameOverZoneScale` (Standard 0.75 = engere Zone), `gameOverHoldTime` (Standard 0.8 s) und
`gameOverAppearGrace` (Standard 1.2 s Sperre nach dem Einblenden, bis die Arme zur Ruhe kommen).

**Falls der Knopf weiterhin schwer erreichbar ist** („etwas weiter nach vorne“):
- `activationRadius` / `verticalTolerance` am `PhysicalStartPodestController` weiter erhöhen.
- Oder das **Podest** in der Szene näher zur Spielerposition / weiter nach vorne (Z) schieben; der
  Knopf folgt dem Podest. (Die Hand bewegt sich im gespiegelten Avatar-Raum – der Knopf muss dort
  liegen, wo die sichtbare Avatar-Hand hinreicht.)
- `holdTime` verringern, wenn das Halten zu lang wirkt.

Der Bildschirm-Button wurde entfernt. Enter/Leertaste bleiben als Entwicklungs-Fallback.

Während `Playing` zeigt das Ingame-HUD unten rechts zusätzlich **Corpus in Speculo**. Die Vorschau
zeichnet dieselben Gelenke wie `PositionTransferMultiple`, also exakt die Pose des Kollisionsavatars.

Die Spielerplattform wird zur Laufzeit auf das Referenzmaß des CAVE-Samples gesetzt: **3 × 3 Meter**.
Das eigene 2 × 2-Meter-Rohmodell verwendet dafür X/Z-Scale **1.5** statt bisher 4.5.

---

## 7b. Sprachsteuerung des Podests

Zusätzlich zur Hand lassen sich die Podest-Knöpfe per **Sprache** auslösen
(`PodestVoiceControl`). Das Mikrofon hört **automatisch** zu, solange das Podest sichtbar ist
(MainMenu / Game Over), und ist sonst aus (keine Fehlauslösung). Die Befehle werden auf
**ENGLISCH** gesprochen (englisches Whisper-Modell, siehe 7c):

- **Startbildschirm:** „start", „go", „play", „begin" … → Spiel starten
- **Game Over:** „restart", „again", „retry" … → Neustart ·
  „menu", „back", „exit" … → Hauptmenü

Auslösung folgt denselben Regeln wie der Hand-Knopf (nur im passenden Zustand, Cooldown,
zuverlässiges Tracking). Es wird VAD-fenstergesteuert transkribiert (nur wenn jemand spricht).

**Voraussetzungen:** ein Mikrofon am Rechner **und** das heruntergeladene Whisper-Modell (Abschnitt 7c).
Fehlt der Sprach-Stack/das Modell, bleibt einfach Hand + Tastatur aktiv.

Der Bootstrapper legt den Sprach-Stack (WhisperManager + MicrophoneRecord + EchoMotionSpeechToText)
automatisch an (Modell **small.en**, Sprache **en**), falls in der Szene keiner vorhanden ist. Ein
selbst platzierter Stack hat Vorrang.

Stellschrauben am `PodestVoiceControl`: die Keyword-Listen `startKeywords` / `restartKeywords` /
`menuKeywords` und `vadStopTime` (Stille bis zur Auswertung).

---

## 7c. Whisper-Modell: Englisch („small.en") & herunterladen

Das Projekt nutzt jetzt überall das **englische small-Modell** (`ggml-small.en.bin`): Die
englische Erkennung ist deutlich zuverlässiger als die deutsche – **alle Sprachbefehle und
Quiz-Antworten werden daher auf ENGLISCH gesprochen**. Die Whisper-Sprache steht auf `en`,
die Quizfragen (`Resources/Quiz/QuizQuestions.asset`) und Podest-Befehle sind auf Englisch
umgestellt. Die Modell-Datei selbst ist **nicht** im Git (zu groß) und muss **pro Rechner
einmal** heruntergeladen werden.

**Modell herunterladen (auf dem CAVE-/Main-Rechner):**

1. Datei `ggml-small.en.bin` (~488 MB, nur Englisch) im Browser herunterladen:
   <https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin>
   und nach `Assets/StreamingAssets/Whisper/` legen.
   (Übersicht aller Modelle: <https://huggingface.co/ggerganov/whisper.cpp/tree/main>.)
   **Auf die Endung `.en.bin` achten** – `ggml-small.bin` ohne `.en` ist das multilinguale Modell.
2. In Unity kurz warten, bis der Import durch ist. Fertig – beim Start lädt der `WhisperManager`
   automatisch `ggml-small.en.bin`.

**Wo das Modell konfiguriert ist (falls du es manuell ändern willst):**
- `WhisperManager`-Komponente → Feld **Model Path** = `Whisper/ggml-small.en.bin`,
  **Is Model Path In Streaming Assets** = an, **Language** = `en`.
- Auto-Stack: `GameBootstrapper.cs` → Konstante `VoiceModelRelativePath`.

**Hinweise:**
- `.gitignore` ignoriert `ggml-*.bin` bereits → wird nicht eingecheckt, jeder Rechner lädt es lokal.
- Das alte `ggml-tiny.bin` (im Git, ~77 MB) wird nicht mehr verwendet. Optional entfernen:
  `git rm Assets/StreamingAssets/Whisper/ggml-tiny.bin` (dann committen).
- Evtl. noch vorhandene `ggml-small.bin` / `ggml-medium.bin` können lokal gelöscht werden.
- Zurück zu Deutsch? Multilinguales Modell ablegen, `ModelPath` + `language = "de"` anpassen und
  Quizfragen/Podest-Befehle wieder auf Deutsch umstellen.

---

## 7d. Begleiter-Avatar (Blocky)

Der animierte Blender-Avatar (`Assets/TetrisCave/Models/Resources/Avatar/tetris01.fbx`)
wird vom `GameBootstrapper` automatisch als **Companion Avatar** in die Szene gelegt –
kein manueller Schritt nötig.

Was er tut:

- **Menü (erster Besuch):** erklärt das Spielprinzip Schritt für Schritt per Sprechblasen,
  danach erinnert er regelmäßig an den Podest-Knopf.
- **Während der Runde:** schwirrt frei umher – im Frontbereich (`wanderCenter`/`wanderExtents`,
  doppelt gewichtet) sowie an der **linken und rechten CAVE-Wand** (`sideWanderCenter`/
  `sideWanderExtents`; rechte Zone, links gespiegelt; `(0,0,0)` schaltet die Seiten ab) –
  und kommentiert jede Wand: Lob bei Erfolg, Serien-Sprüche ab 3 Wänden in Folge, Trost
  bei Treffern, Warnung beim letzten Leben.
- **Game Over:** kommentiert Punktzahl bzw. neuen Rekord und nennt die Knopf-Belegung.

Technik/Anpassung:

- Skripte: `Assets/Scripts/CaveGame/Avatar/` (`AvatarCompanion` = Logik & Texte,
  `AvatarHoverMovement` = Schwebeflug, `AvatarSpeechBubble` = Sprechblase).
  **Alle Texte und Positionen sind am `Companion Avatar`-Objekt im Inspector änderbar.**
- Die Animation aus dem FBX wird per Playables abgespielt (kein AnimatorController-Asset);
  ein `AssetPostprocessor` (`Assets/Editor/AvatarModelImportSettings.cs`) stellt die Clips
  beim Import automatisch auf **Loop** und lässt Blender-Kamera/-Licht weg.
- Die im FBX **einmodellierte Sprechblase** (Mesh `Text`, „Welche Form kam zuletzt?“)
  wird zur Laufzeit ausgeblendet – Texte kommen ausschließlich aus der dynamischen
  Sprechblase. Falls das Modell falsch herum schaut: `modelYawOffset` anpassen.
- Das Modell erhält keine Collider und liegt nicht auf dem Player-Layer – es kann
  also niemals Wand-Treffer auslösen.

---

## 7e. Soundeffekte (Fehler / Game Over)

`Wall.errorSound` und `GameManager.gameOverSound` sind Inspector-Felder – aber GameManager und
Wände werden **zur Laufzeit** erzeugt, daher füllt der `GameBootstrapper` bzw. `WallSpawner` die
Felder automatisch aus `Resources`:

- **Fehler-Sound** (Wandberührung): `Assets/TetrisCave/Sounds/Resources/Sfx/FalseSound.mp3`
- **Game-Over-Sound**: `Assets/TetrisCave/Sounds/Resources/Sfx/GameOverSound.mp3`
  (ehemals „Game Over #2 (Super Mario)…“ aus dem Sounds-Ordner; per `git mv` samt .meta
  verschoben, Referenzen bleiben gültig)

**Lautstärke:** Der Fehler-Sound wird beim Laden um `errorSoundBoost` (Standard 2.5×, am
`WallSpawner`) direkt in den Audiodaten verstärkt, weil die Hintergrundmusik den Original-Clip
übertönt – AudioSources können nicht zuverlässig über 100 % verstärken. Immer noch zu leise/laut?
→ `errorSoundBoost` anpassen (1 = Original).

**Sound tauschen:** einfach die MP3 in `Resources/Sfx/` durch eine gleichnamige Datei ersetzen –
oder am `WallSpawner` (`errorSound`) manuell einen anderen Clip zuweisen. Abgespielt wird über
die AudioSource des GameManagers (`PlayOneShot`), damit der Ton beim Despawnen der Wand nicht
abreißt und laufende Musik nicht unterbrochen wird. Die übrigen Musik-Dateien liegen unverändert
in `Assets/Samples/CAVE/1.1.1/CAVE Tools/Sounds/`.

---

## 7f. Avatar-Quiz mit Whisper und Ollama

Bei 1000, 2000, 3000 usw. Punkten pausiert das Spiel vollständig. Blocky stellt eine zufällige
Frage, Whisper erkennt die deutsche Antwort und Ollama bewertet sie. Punkte und Leben werden durch
das Quiz nie verändert. Jede Frage kommt innerhalb eines Durchlaufs genau einmal vor.

**Einmalige lokale Einrichtung:**

1. Das englische Whisper-Modell wie in Abschnitt 7c als
   `Assets/StreamingAssets/Whisper/ggml-small.en.bin` ablegen. Modell-Dateien sind absichtlich
   ignoriert. Die Quiz-Antworten werden auf **Englisch** gesprochen.
2. [Ollama](https://ollama.com/) installieren und das Standardmodell laden:
   ```powershell
   ollama pull gemma3:4b
   ollama serve
   ```
3. Prüfen, dass `http://localhost:11434` erreichbar ist. Modell, URL und 10-Sekunden-Timeout sind
   am Laufzeitobjekt `Avatar Quiz` (`OllamaQuizClient`) austauschbar.
4. Die aus `fragen.pdf` übernommene Datenbank muss als
   `Assets/Resources/Quiz/QuizQuestions.asset` liegen. Über
   `Assets > Create > Cave Game > Quiz Questions` lässt sie sich im Inspector anlegen und bearbeiten.

Bei fehlendem Modell, nicht laufendem Ollama, Netzwerk-Timeout oder ungültigem JSON wird automatisch
lokal gegen Musterlösung und Antwortvarianten verglichen. Nach acht Sekunden ohne Sprache startet
die Aufnahme neu; nach drei erfolglosen Versuchen zeigt Blocky die Lösung und setzt das Spiel fort.
Game Over und Menüwechsel brechen Aufnahme, Anfrage und Pause unmittelbar ab.

---

## 8. Spielablauf / Zustände (`GameManager`)

`MainMenu` → (Startknopf oder UI/Tastatur) → `WaitingForPlayerTracking` →
(Person zuverlässig getrackt) → `Playing` → (0 Leben) → `GameOver` → (Neustart/Zurück).

Einstellbar am `GameManager`: `maxLives` (3), `pointsPerWall` (100), `requirePlayerTracking`,
`allowKeyboardFallback`. Highscore wird über `HumanTetrisHighscore` (PlayerPrefs) gespeichert und im
Startbildschirm sowie im Game-Over-Screen angezeigt.

---

## 9. Testen

**Ohne Kinect (Entwicklungsrechner):**
- Play drücken. Startbildschirm erscheint.
- Mit `Enter`/`Leertaste` starten; der physische Knopf benötigt echtes Kinect-Tracking.
- In `WaitingForPlayerTracking` startet ohne getrackte Person nichts → mit `Enter`/`Leertaste`
  überspringen (Fallback). Tipp: am `GameManager` `requirePlayerTracking` aus, dann startet es direkt.
- Wände fliegen heran, Score zählt hoch, HUD zeigt Leben/Punkte und unten rechts die Live-Pose. Treffer testest du, indem du den
  Avatar/ein Körperteil in eine Wand bewegst (oder im Editor einen Collider mit Marker `PlayerBodyPart`
  durch eine Wand schiebst). Bei 0 Leben → Game Over (Neustart = `Enter`, Zurück = `Esc`).
- Ohne Wand-Grafiken erscheint automatisch eine **Platzhalter-Wand** mit rechteckiger Öffnung.

**Mit Kinect (CAVE):**
- Person betritt das Trackingfeld → `WaitingForPlayerTracking` wechselt zu `Playing`.
- Hand zum einzelnen grünen Knopf auf dem Podest führen: Der Trigger startet das Spiel und der Knopf
  verschwindet – **kein Cursor** und keine drei zusätzlichen Demo-Knöpfe sichtbar.
- Durch die Öffnung passen = Punkte; Wand berühren = Leben verlieren.

---

## 10. Build-Hinweise (CAVE / Windows)

- Zielplattform **Windows (x86_64)** für Kinect v2.
- Kinect-v2-Runtime/SDK auf dem CAVE-Rechner installiert; Sensor angeschlossen.
- Es liegen keine `UnityEditor`-Abhängigkeiten in den Laufzeit-Skripten (geprüft), der Build sollte
  fehlerfrei kompilieren.
- Szene in den Build Settings aktiv.

---

## 11. Abgleich mit der Aufgabenliste (PDF)

1. Projektstruktur ✓ · 2. C#-Skripte ✓ · 3. Szenen-Hierarchie ✓ (Abschnitt 3) ·
4. Startbildschirm-Setup ✓ (bleibt erhalten) · 5. Physischer Kinect-Druck ohne Cursor ✓ ·
6. Adaption der Button-Interaktion aus dem Referenzprojekt ✓ (`ObjectToggleButton`-Prinzip) ·
7. Collider/Rigidbody-Setup ✓ (Abschnitt 4) · 8. UI Start/Spiel/Game Over ✓ ·
9. Score- & Highscore-System ✓ (`HumanTetrisHighscore`) · 10. Wand-Spawn-Logik ✓ ·
11. Kinect-Skelettsteuerung ✓ (`PositionTransferMultiple` + CAVE) · 12. diese Anleitung ✓
