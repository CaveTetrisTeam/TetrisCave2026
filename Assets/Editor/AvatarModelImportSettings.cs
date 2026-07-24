using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CaveGame.EditorTools
{
    /// <summary>
    /// Import-Einstellungen für den Begleiter-Avatar (alles unter einem
    /// <c>Resources/Avatar</c>-Ordner): Animations-Clips werden auf Loop gestellt
    /// (die Sprech-Animation soll endlos laufen), Blender-Kameras/-Lichter werden
    /// nicht mitimportiert und die im Modell eingebaute statische Sprechblase
    /// ('BézierCircle' + 'Text') wird entfernt – Texte kommen ausschließlich aus
    /// der dynamischen <see cref="AvatarSpeechBubble"/>.
    ///
    /// Läuft automatisch beim (Re-)Import – kein manueller Schritt nötig,
    /// gleiches "pull &amp; run"-Prinzip wie <see cref="CaveGameSetup"/>.
    /// </summary>
    public sealed class AvatarModelImportSettings : AssetPostprocessor
    {
        // 'zierCircle' statt 'BézierCircle', damit der Vergleich unabhängig von der
        // Kodierung des 'é' funktioniert (Contains-Match).
        private static readonly string[] RemovedChildNames = { "Text", "zierCircle" };

        /// <summary>Versions-Bump erzwingt den Reimport auf Rechnern mit altem Library-Cache.</summary>
        public override uint GetVersion()
        {
            return 2;
        }

        private static bool IsAvatarModel(string path)
        {
            return path.Replace('\\', '/').Contains("/Resources/Avatar/");
        }

        private void OnPreprocessModel()
        {
            if (!IsAvatarModel(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsAvatarModel(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;

            // defaultClipAnimations liefert die aus den FBX-Takes abgeleiteten Clips,
            // falls noch keine eigenen Clip-Definitionen existieren.
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            foreach (var clip in clips)
            {
                clip.loopTime = true;
            }

            importer.clipAnimations = clips;
        }

        /// <summary>
        /// Entfernt die fest einmodellierte Sprechblase (Ballon 'BézierCircle' und
        /// Mesh 'Text' mit „Welche Form kam zuletzt?") direkt aus dem importierten
        /// Modell, damit sie nirgendwo mehr auftaucht.
        /// </summary>
        private void OnPostprocessModel(GameObject root)
        {
            if (!IsAvatarModel(assetPath))
            {
                return;
            }

            var toRemove = new List<GameObject>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (var name in RemovedChildNames)
                {
                    if (child.name.Contains(name))
                    {
                        toRemove.Add(child.gameObject);
                        break;
                    }
                }
            }

            foreach (var go in toRemove)
            {
                Object.DestroyImmediate(go);
            }

            if (toRemove.Count > 0)
            {
                Debug.Log("[AvatarModelImportSettings] " + toRemove.Count +
                          " eingebaute Sprechblasen-Objekt(e) aus dem Avatar-Modell entfernt.");
            }
        }
    }
}
