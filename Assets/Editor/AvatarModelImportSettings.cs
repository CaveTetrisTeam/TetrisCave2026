using UnityEditor;

namespace CaveGame.EditorTools
{
    /// <summary>
    /// Import-Einstellungen für den Begleiter-Avatar (alles unter einem
    /// <c>Resources/Avatar</c>-Ordner): Animations-Clips werden auf Loop gestellt
    /// (die Sprech-Animation soll endlos laufen) und Blender-Kameras/-Lichter
    /// werden nicht mitimportiert.
    ///
    /// Läuft automatisch beim (Re-)Import – kein manueller Schritt nötig,
    /// gleiches "pull &amp; run"-Prinzip wie <see cref="CaveGameSetup"/>.
    /// </summary>
    public sealed class AvatarModelImportSettings : AssetPostprocessor
    {
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
    }
}
