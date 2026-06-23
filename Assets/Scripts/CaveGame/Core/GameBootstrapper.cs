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
            EnsurePhysicalStartPodest();
            ApplyReferencePlatformSize();
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
        /// Die vier CAVE-Sample-Buttons sowie frühere, separat erzeugte Startknöpfe
        /// würden sonst parallel zum neuen einzelnen Podest-Knopf stehen bleiben.
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

        /// <summary>
        /// Verwendet das originale Podest aus dem FKI-HTW/CAVE-Sample, entfernt
        /// dessen vier Demo-Knöpfe und baut genau einen Startknopf in der Mitte auf.
        /// </summary>
        private static void EnsurePhysicalStartPodest()
        {
            if (Object.FindObjectOfType<PhysicalStartPodestController>(true) != null)
            {
                return;
            }

            GameObject podest = null;
            foreach (var transform in Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.name == "Podest")
                {
                    podest = transform.gameObject;
                    break;
                }
            }

            if (podest == null)
            {
                Debug.LogError("[GameBootstrapper] Das CAVE-Podest wurde in der Szene nicht gefunden.");
                return;
            }

            var host = new GameObject("Physical Start Podest Controller");
            host.AddComponent<PhysicalStartPodestController>().Initialize(podest);
        }

        /// <summary>
        /// Das Referenzprojekt verwendet eine 3 x 3 Meter große CAVE-Fläche
        /// (CaveArea-Plane: 10 Einheiten bei Scale 0.3). Unser Plattformmodell ist
        /// im Rohzustand 2 x 2 Meter und benötigt daher Scale 1.5 statt 4.5.
        /// </summary>
        private static void ApplyReferencePlatformSize()
        {
            foreach (var transform in Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.name == "Plane" && transform.parent != null &&
                    transform.parent.name == "World")
                {
                    transform.localScale = new Vector3(1.5f, transform.localScale.y, 1.5f);
                    return;
                }
            }

            Debug.LogWarning("[GameBootstrapper] Spielerplattform 'World/Plane' nicht gefunden.");
        }
    }
}
