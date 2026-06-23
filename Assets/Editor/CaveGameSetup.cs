using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaveGame.EditorTools
{
    /// <summary>
    /// Macht das Projekt "pull & run"-fertig: legt die Layer "Player"/"Wall" an, stellt
    /// die Wand-Texturen lesbar, erzeugt aus der funktionierenden CAVE-MainScene eine
    /// SAUBERE Spielszene (Sample-Demo-Objekte werden entfernt) und trägt sie in die
    /// Build Settings ein.
    ///
    /// Läuft beim Öffnen des Projekts automatisch (einmal pro Rechner, abgesichert) und
    /// ist zusätzlich über das Menü "Tools ▸ CaveGame" aufrufbar. Alle Schritte sind
    /// idempotent und in try/catch gekapselt – ein Fehler bricht nichts ab.
    /// </summary>
    [InitializeOnLoad]
    public static class CaveGameSetup
    {
        private const string GameScenePath = "Assets/Scenes/CaveGame.unity";
        private const string TemplateScenePath = "Assets/Samples/CAVE/1.1.1/CAVE Tools/MainScene.unity";
        private const string WallTextureFolder = "Assets/TetrisCave/Walls/Resources/Walls";

        private const string SetupVersionKey = "CaveGame.SetupVersion";
        private const int SetupVersion = 1;

        // Komponenten, die in der Spielszene nichts zu suchen haben:
        //  - CAVE-Beispiel-Demos (Würfel/Flammen/Sample-Buttons/Distanztest)
        //  - die ALTE Tetromino-Block-Mechanik (BlockSpawner/BlockMover/TetrominoBuilder)
        //  - die Bewegungsaufnahme (MovementEchoMultiple)
        // Es werden nur die KOMPONENTEN entfernt (nicht die GameObjects), damit Rig,
        // Avatar (PositionTransferMultiple) und Höhlen-Geometrie garantiert erhalten bleiben.
        private static readonly HashSet<string> RemovableComponentNames = new HashSet<string>
        {
            "CubeSpawner", "FlameSpawner", "DistanceCubeMover", "DespawnScript",
            "FireSpawnState", "CubeSpawnState", "ObjectToggleButton",
            "BlockSpawner", "BlockMover", "TetrominoBuilder", "MovementEchoMultiple"
        };

        static CaveGameSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorPrefs.GetInt(SetupVersionKey, 0) >= SetupVersion)
            {
                return;
            }

            // Nicht eingreifen, während kompiliert/abgespielt wird oder ungespeicherte Änderungen offen sind.
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            RunSetup(interactive: false);
        }

        [MenuItem("Tools/CaveGame/Setup ausführen", priority = 0)]
        public static void RunSetupMenu()
        {
            RunSetup(interactive: true);
        }

        private static void RunSetup(bool interactive)
        {
            try
            {
                EnsureLayer("Player");
                EnsureLayer("Wall");
                EnsureWallTexturesReadable();
                EnsureGameScene(interactive);
                EnsureSceneInBuild();

                EditorPrefs.SetInt(SetupVersionKey, SetupVersion);
                Debug.Log("[CaveGameSetup] Einrichtung abgeschlossen. Spielszene: " + ActiveBuildScenePath());
            }
            catch (Exception e)
            {
                Debug.LogError("[CaveGameSetup] Einrichtung fehlgeschlagen: " + e);
            }
        }

        // ------------------------------------------------------------------ Layer

        private static void EnsureLayer(string layerName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers == null)
            {
                return;
            }

            // Schon vorhanden?
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return;
                }
            }

            // Ersten freien Benutzer-Slot (ab Index 8) belegen.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }

            Debug.LogWarning("[CaveGameSetup] Kein freier Layer-Slot für '" + layerName + "'.");
        }

        // ------------------------------------------------------------------ Texturen

        private static void EnsureWallTexturesReadable()
        {
            if (!AssetDatabase.IsValidFolder(WallTextureFolder))
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { WallTextureFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    bool changed = false;
                    if (!importer.isReadable) { importer.isReadable = true; changed = true; }
                    if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                    if (changed)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        // ------------------------------------------------------------------ Spielszene

        private static void EnsureGameScene(bool interactive)
        {
            if (File.Exists(GameScenePath))
            {
                return;
            }

            if (!File.Exists(TemplateScenePath))
            {
                Debug.LogWarning("[CaveGameSetup] Vorlage-Szene nicht gefunden: " + TemplateScenePath +
                                 " – bitte Spielszene manuell anlegen (siehe CaveGame_SETUP_DE.md).");
                return;
            }

            // Im Auto-Modus keine ungespeicherten Änderungen überschreiben.
            if (!interactive && EditorSceneManager.GetActiveScene().isDirty)
            {
                Debug.Log("[CaveGameSetup] Aktive Szene hat ungespeicherte Änderungen – Spielszene wird nicht " +
                          "automatisch erzeugt. Bitte 'Tools ▸ CaveGame ▸ Saubere Spielszene neu erzeugen' nutzen.");
                return;
            }

            if (!AssetDatabase.CopyAsset(TemplateScenePath, GameScenePath))
            {
                Debug.LogWarning("[CaveGameSetup] Konnte Spielszene nicht aus der Vorlage kopieren.");
                return;
            }

            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            int removed = StripSampleObjects(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[CaveGameSetup] Saubere Spielszene erzeugt ({GameScenePath}); {removed} Sample-Objekt(e) entfernt.");
        }

        [MenuItem("Tools/CaveGame/Saubere Spielszene neu erzeugen", priority = 1)]
        public static void RegenerateGameScene()
        {
            try
            {
                if (File.Exists(GameScenePath))
                {
                    AssetDatabase.DeleteAsset(GameScenePath);
                }
                EnsureGameScene(interactive: true);
                EnsureSceneInBuild();
            }
            catch (Exception e)
            {
                Debug.LogError("[CaveGameSetup] Neuerzeugung fehlgeschlagen: " + e);
            }
        }

        /// <summary>
        /// Entfernt die Demo-/Alt-Komponenten (siehe <see cref="RemovableComponentNames"/>) anhand des
        /// Typnamens. Es werden nur Komponenten gelöscht – CAVE-Rig, Avatar und Höhlen-Geometrie bleiben.
        /// </summary>
        private static int StripSampleObjects(Scene scene)
        {
            var toRemove = new List<MonoBehaviour>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var comp in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp != null && RemovableComponentNames.Contains(comp.GetType().Name))
                    {
                        toRemove.Add(comp);
                    }
                }
            }

            int count = 0;
            foreach (var comp in toRemove)
            {
                if (comp != null)
                {
                    UnityEngine.Object.DestroyImmediate(comp);
                    count++;
                }
            }

            return count;
        }

        // ------------------------------------------------------------------ Build Settings

        private static void EnsureSceneInBuild()
        {
            string target = File.Exists(GameScenePath) ? GameScenePath : TemplateScenePath;
            if (!File.Exists(target))
            {
                return;
            }

            // Single-Scene-Demo: genau die Spielszene als Start-Szene eintragen.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(target, true) };
        }

        private static string ActiveBuildScenePath()
        {
            var scenes = EditorBuildSettings.scenes;
            return scenes != null && scenes.Length > 0 ? scenes[0].path : "(keine)";
        }
    }
}
