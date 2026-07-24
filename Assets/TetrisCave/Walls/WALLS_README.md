# Human Tetris – Wände mit ausgestanzten Formen

Dieses Paket enthält Wandgrafiken für ein Human-Tetris-Spiel:
Die Körperform ist transparent aus der Wand ausgestanzt.
Die Wand kann in Unity als Sprite auf den Spieler zufliegen.

## Inhalt

- `Wall_PNGs/`
  - 10 Wand-Sprites mit transparentem Human-Tetris-Loch.
  - Auflösung: 1024 × 768 px.
- `Hole_Masks/`
  - Weiße Lochmasken auf transparentem Hintergrund.
  - Nützlich für Debugging, UI, spätere Pose-Erkennung oder Kollisionslogik.
- `Unity_Scripts/`
  - `WallMoveTowardsPlayer.cs`
  - `HumanTetrisWallSpawner.cs`
  - `SimpleHoleFitCheck2D.cs`
- `Preview/wall_cutouts_preview.png`
  - Übersicht aller Wandformen.

## Unity Import

1. Ordner in dein Unity-Projekt unter `Assets/` ziehen.
2. PNGs in `Wall_PNGs/` auswählen.
3. Im Inspector einstellen:
   - Texture Type: `Sprite (2D and UI)`
   - Sprite Mode: `Single`
   - Alpha Is Transparency: aktiviert
   - Filter Mode: `Bilinear`
4. Apply drücken.

## Einfacher Aufbau in Unity

1. Erstelle ein leeres GameObject `WallPrefab`.
2. Füge einen `SpriteRenderer` hinzu.
3. Füge `WallMoveTowardsPlayer.cs` hinzu.
4. Ziehe eine Wand-PNG testweise in den SpriteRenderer.
5. Speichere das Objekt als Prefab.
6. Erstelle ein leeres GameObject `WallSpawner`.
7. Füge `HumanTetrisWallSpawner.cs` hinzu.
8. Weise dein `WallPrefab` zu.
9. Ziehe alle Wand-Sprites in das `wallSprites`-Array.
10. Setze einen SpawnPoint vor den Spieler, z. B. auf `Z = 20`.

## Wichtig für Kollisionen

Die transparenten Löcher sind visuell ausgestanzt.
Für echte Spiellogik brauchst du später zusätzlich eine Kollisions- oder Pose-Erkennung.

Für einen frühen Prototyp:
- Wand trifft Spieler = Fehler
- Spieler steht korrekt im Lochbereich = Erfolg

Später besser:
- MediaPipe / Webcam-Pose-Erkennung
- mehrere Collider um den ausgestanzten Bereich
- oder eigene Masken-/Pixelprüfung mit den `Hole_Masks`.
