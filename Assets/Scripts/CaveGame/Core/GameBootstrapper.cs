using System.IO;
using UnityEngine;
using HTW.CAVE;
using Whisper;
using Whisper.Utils;

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
            ConfigureMonoCaveProjection();
            EnsureWallSpawner();
            EnsureGameAudio();
            EnsureUi();
            EnsureCompanionAvatar();
            RemoveSampleToggleButtons();
            // Sprachsteuerung vorerst deaktiviert (kein Spielstart per Stimme).
            // Zum Reaktivieren einfach die beiden folgenden Aufrufe wieder einkommentieren:
            // EnsureSpeechToText();
            EnsurePhysicalStartPodest();
            // EnsureVoiceControl();
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
            var existing = Object.FindObjectsOfType<WallSpawner>(true);
            if (existing.Length > 0)
            {
                // Bei zusammengeführten Szenen darf nur ein Spawner übrig bleiben.
                for (int i = 1; i < existing.Length; i++)
                {
                    Object.Destroy(existing[i].gameObject);
                }
                return;
            }

            var go = new GameObject("Wall Spawner");
            go.AddComponent<WallSpawner>();
        }

        /// <summary>
        /// Beide Frontprojektoren bleiben aktiv, rendern aber denselben Mono-
        /// Blickpunkt. Damit werden die bisherigen links/rechts versetzten
        /// Stereo-Silhouetten deckungsgleich statt als zwei Wände wahrgenommen.
        /// </summary>
        private static void ConfigureMonoCaveProjection()
        {
            var environment = Object.FindObjectOfType<VirtualEnvironment>(true);
            if (environment != null)
            {
                environment.eyeSeperation = 0f;
            }

            foreach (var virtualCamera in Object.FindObjectsOfType<VirtualCamera>(true))
            {
                virtualCamera.stereoTarget = VirtualEyes.StereoTarget.Mono;
            }
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
        /// Verdrahtet die Soundeffekte: Da GameManager und Wände zur Laufzeit erzeugt
        /// werden, können die AudioClip-Felder nicht im Inspector gesetzt werden –
        /// die Clips kommen deshalb aus <c>Resources/Sfx</c>. Der Fehler-Sound wird
        /// vom <see cref="WallSpawner"/> an jede Wand weitergereicht.
        /// </summary>
        private static void EnsureGameAudio()
        {
            var manager = Object.FindObjectOfType<GameManager>(true);
            if (manager != null && manager.gameOverSound == null)
            {
                manager.gameOverSound = Resources.Load<AudioClip>("Sfx/GameOverSound");
                if (manager.gameOverSound == null)
                {
                    Debug.LogWarning("[GameBootstrapper] Game-Over-Sound 'Resources/Sfx/GameOverSound' " +
                                     "nicht gefunden – Game Over bleibt stumm.");
                }
            }
        }

        /// <summary>
        /// Erzeugt den Begleiter-Avatar (schwebendes Blender-Modell mit Sprechblasen),
        /// der das Spielprinzip erklärt und die Runde kommentiert.
        /// </summary>
        private static void EnsureCompanionAvatar()
        {
            if (Object.FindObjectOfType<AvatarCompanion>(true) != null)
            {
                return;
            }

            var go = new GameObject("Companion Avatar");
            go.AddComponent<AvatarCompanion>();
        }

        /// <summary>
        /// Entfernt die vier CAVE-Sample-Knöpfe (ObjectToggleButton), die sonst parallel
        /// zum eigenen Podest-Knopf stehen blieben.
        /// </summary>
        private static void RemoveSampleToggleButtons()
        {
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
        /// Legt den Echo-Motion-Sprach-Stack (WhisperManager + MicrophoneRecord +
        /// EchoMotionSpeechToText) an, falls in der Szene keiner vorhanden ist – konfiguriert
        /// auf das kleine Modell und Deutsch. Das GameObject wird inaktiv erzeugt, damit
        /// Modellpfad/Sprache gesetzt werden, BEVOR der WhisperManager im Awake lädt.
        /// </summary>
        private const string VoiceModelRelativePath = "Whisper/ggml-small.bin";

        private static void EnsureSpeechToText()
        {
            if (Object.FindObjectOfType<EchoMotionSpeechToText>(true) != null)
            {
                return; // bereits vorhanden (z. B. manuell platziert) -> respektieren
            }

            // Ohne vorhandene Modell-Datei NICHT initialisieren – sonst wirft der
            // WhisperManager beim Laden eine NullReferenceException. Voice bleibt dann
            // einfach aus (Hand/Tastatur funktionieren weiter).
            string modelFullPath = Path.Combine(Application.streamingAssetsPath, VoiceModelRelativePath);
            if (!File.Exists(modelFullPath))
            {
                Debug.LogWarning("[GameBootstrapper] Sprachmodell nicht gefunden: " + modelFullPath +
                                 "\nSprachsteuerung ist deaktiviert. Lege 'ggml-small.bin' in " +
                                 "Assets/StreamingAssets/Whisper/ ab (siehe CaveGame_SETUP_DE.md, Abschnitt 7c).");
                return;
            }

            try
            {
                var go = new GameObject("Echo-Motion Speech (Voice Control)");
                go.SetActive(false);

                var whisper = go.AddComponent<WhisperManager>();
                whisper.IsModelPathInStreamingAssets = true;
                whisper.ModelPath = VoiceModelRelativePath;
                whisper.language = "de";

                var mic = go.AddComponent<MicrophoneRecord>();

                var stt = go.AddComponent<EchoMotionSpeechToText>();
                stt.whisper = whisper;
                stt.microphoneRecord = mic;
                stt.autoInitWhisper = true;

                go.SetActive(true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GameBootstrapper] Sprach-Stack konnte nicht angelegt werden – " +
                                 "Sprachsteuerung inaktiv (Hand/Tastatur bleibt). " + e.Message);
            }
        }

        private static void EnsureVoiceControl()
        {
            if (Object.FindObjectOfType<PodestVoiceControl>(true) != null)
            {
                return;
            }

            var go = new GameObject("Podest Voice Control");
            go.AddComponent<PodestVoiceControl>();
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
