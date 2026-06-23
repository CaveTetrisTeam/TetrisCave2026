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
            EnsurePhysicalButton();
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

        private static void EnsurePhysicalButton()
        {
            if (Object.FindObjectOfType<KinectPhysicalButton>(true) != null)
            {
                return;
            }

            var go = new GameObject("Kinect Physical Start Button");

            // Vor der Hauptkamera platzieren; Druckfläche (lokal +Z) zeigt zum Spieler/zur Kamera.
            var cam = Camera.main;
            if (cam != null)
            {
                go.transform.position = cam.transform.position + cam.transform.forward * 1.5f;
                go.transform.rotation = Quaternion.LookRotation(
                    (cam.transform.position - go.transform.position).normalized, Vector3.up);
            }
            else
            {
                go.transform.position = new Vector3(0f, 1.2f, 1.5f);
                go.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            }

            go.AddComponent<KinectPhysicalButton>();
        }
    }
}
