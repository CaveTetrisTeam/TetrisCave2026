using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Erzeugt beim Szenenstart automatisch alle Spielsysteme, damit nichts
    /// manuell in die Szene gezogen werden muss (gleiches Muster wie
    /// <c>HumanTetrisStartMenu</c>). Bereits vorhandene Komponenten haben Vorrang –
    /// wer ein System lieber selbst in der Szene platziert/konfiguriert, kann das tun.
    ///
    /// Voraussetzung in der Szene: das CAVE-Rig (KinectManager + KinectTracker) und
    /// der Avatar-Tracker (PositionTransferMultiple) sind vorhanden.
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureGameManager();
            EnsureKinect();
            EnsureWallSpawner();
            EnsureUi();
            RemoveLegacyStartButtons();
        }

        private static void EnsureGameManager()
        {
            if (Object.FindObjectOfType<GameManager>(true) != null)
            {
                return;
            }

            var go = new GameObject("Game Manager");
            go.AddComponent<GameManager>();
        }

        private static void EnsureKinect()
        {
            if (Object.FindObjectOfType<KinectPlayerPresence>(true) == null)
            {
                var go = new GameObject("Kinect Player Presence");
                go.AddComponent<KinectPlayerPresence>();
            }

            if (Object.FindObjectOfType<KinectHandInteractor>(true) == null)
            {
                var go = new GameObject("Kinect Hand Interactor");
                go.AddComponent<KinectHandInteractor>();
            }
        }

        private static void EnsureWallSpawner()
        {
            if (Object.FindObjectOfType<WallSpawner>(true) != null)
            {
                return;
            }

            var go = new GameObject("Wall Spawner");
            go.AddComponent<WallSpawner>();
        }

        private static void EnsureUi()
        {
            if (Object.FindObjectOfType<IngameHud>(true) == null)
            {
                var go = new GameObject("Ingame HUD");
                go.AddComponent<IngameHud>();
            }

            if (Object.FindObjectOfType<GameOverController>(true) == null)
            {
                var go = new GameObject("Game Over UI");
                go.AddComponent<GameOverController>();
            }
        }

        /// <summary>
        /// Der Startbildschirm besitzt jetzt genau einen Button: den beschrifteten
        /// UI-Button, der Maus und Kinect-Handdruck gemeinsam verarbeitet. Die alten
        /// CAVE-Sample-Buttons sowie frühere, separat erzeugte 3D-Startknöpfe würden
        /// sonst als leere, funktionslose Knöpfe in der Szene stehen bleiben.
        /// </summary>
        private static void RemoveLegacyStartButtons()
        {
            foreach (var physicalButton in Object.FindObjectsOfType<KinectPhysicalButton>(true))
            {
                if (physicalButton != null)
                {
                    Object.Destroy(physicalButton.gameObject);
                }
            }

            foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "ObjectToggleButton")
                {
                    Object.Destroy(behaviour.gameObject);
                }
            }
        }
    }
}
