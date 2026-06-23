using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Steuert das originale CAVE-Podest zustandsabhängig: Im Hauptmenü steht
    /// genau ein Startknopf bereit, bei Game Over erscheinen zwei Knöpfe für
    /// Neustart und Menü. Während Tracking-Wartezeit und Spiel verschwindet das
    /// komplette Podest. Ausgelöst wird ausschließlich durch den unsichtbaren
    /// Kinect-Hand-Collider.
    /// </summary>
    public sealed class PhysicalStartPodestController : MonoBehaviour
    {
        public enum PodestAction { Start, Restart, Menu }

        [Header("Tracking")]
        public bool requireReliableTracking = true;
        public float cooldown = 1.0f;
        [Tooltip("Kurze Sperre nach dem Einblenden, damit eine bereits dort liegende Hand nicht sofort startet.")]
        public float appearGracePeriod = 0.35f;

        [Header("Farben")]
        public Color waitingColor = new Color(0.85f, 0.16f, 0.10f);
        public Color readyColor = new Color(0.15f, 0.85f, 0.35f);
        public Color menuColor = new Color(0.20f, 0.48f, 1.0f);
        public Color pressedColor = new Color(1.0f, 0.72f, 0.18f);

        private GameObject m_Podest;
        private GameObject m_StartButton;
        private GameObject m_RestartButton;
        private GameObject m_MenuButton;
        private Material m_StartMaterial;
        private Material m_RestartMaterial;
        private Material m_MenuMaterial;
        private KinectPlayerPresence m_Presence;
        private KinectHandInteractor m_Interactor;
        private AudioSource m_Audio;
        private float m_LastActivation = -999f;
        private float m_ButtonShownAt = -999f;
        private bool m_Initialized;

        public void Initialize(GameObject podest)
        {
            m_Podest = podest;
            ResolveDependencies();
            BuildButtons();
            BuildAudio();
            m_Initialized = true;
            RefreshVisibility();
        }

        private void OnEnable()
        {
            GameManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.StateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            if (!m_Initialized)
            {
                ResolveDependencies();
                RefreshVisibility();
            }
        }

        private void Update()
        {
            ResolveDependencies();
            bool ready = m_Presence != null && m_Presence.HasReliablePlayer;
            bool canPress = ready || !requireReliableTracking;
            SetButtonColor(m_StartMaterial, canPress ? readyColor : waitingColor);
            SetButtonColor(m_RestartMaterial, canPress ? readyColor : waitingColor);
            SetButtonColor(m_MenuMaterial, canPress ? menuColor : waitingColor);
        }

        public void TryActivate(PodestAction action, Collider other)
        {
            ResolveDependencies();

            var manager = GameManager.Instance;
            if (manager == null || !IsActionAvailable(action, manager.CurrentState) ||
                Time.unscaledTime - m_LastActivation < cooldown ||
                Time.unscaledTime - m_ButtonShownAt < appearGracePeriod)
            {
                return;
            }

            if (m_Interactor == null || m_Interactor.InteractionPoint == null ||
                other.transform != m_Interactor.InteractionPoint)
            {
                return;
            }

            if (requireReliableTracking &&
                (m_Presence == null || !m_Presence.HasReliablePlayer))
            {
                return;
            }

            m_LastActivation = Time.unscaledTime;
            SetButtonColor(MaterialFor(action), pressedColor);
            if (m_Audio != null) m_Audio.Play();

            switch (action)
            {
                case PodestAction.Start:
                    manager.RequestStart();
                    break;
                case PodestAction.Restart:
                    manager.RestartGame();
                    break;
                case PodestAction.Menu:
                    manager.ReturnToMenu();
                    break;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            ApplyPodestState(state);
        }

        private void RefreshVisibility()
        {
            var manager = GameManager.Instance;
            ApplyPodestState(manager != null ? manager.CurrentState : GameState.MainMenu);
        }

        private void ApplyPodestState(GameState state)
        {
            if (m_Podest == null) return;

            bool showStart = state == GameState.MainMenu;
            bool showGameOverActions = state == GameState.GameOver;
            bool showPodest = showStart || showGameOverActions;
            bool wasInteractive = m_Podest.activeInHierarchy &&
                                  ((m_StartButton != null && m_StartButton.activeInHierarchy) ||
                                   (m_RestartButton != null && m_RestartButton.activeInHierarchy));

            if (m_Podest.activeSelf != showPodest)
            {
                m_Podest.SetActive(showPodest);
            }

            if (m_StartButton != null) m_StartButton.SetActive(showStart);
            if (m_RestartButton != null) m_RestartButton.SetActive(showGameOverActions);
            if (m_MenuButton != null) m_MenuButton.SetActive(showGameOverActions);

            if (showPodest && !wasInteractive) m_ButtonShownAt = Time.unscaledTime;
        }

        private void BuildButtons()
        {
            if (m_Podest == null) return;

            // Falls direkt in der Referenzszene gestartet wird: alle vier
            // ObjectToggleButton-Exemplare zuerst zuverlässig ausblenden.
            for (int i = m_Podest.transform.childCount - 1; i >= 0; i--)
            {
                var child = m_Podest.transform.GetChild(i);
                if (child.name.StartsWith("Button"))
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            m_StartButton = CreateButton("Start Button (Physical)", 0f, PodestAction.Start,
                out m_StartMaterial);
            m_RestartButton = CreateButton("Restart Button (Physical)", -0.008f, PodestAction.Restart,
                out m_RestartMaterial);
            m_MenuButton = CreateButton("Menu Button (Physical)", 0.008f, PodestAction.Menu,
                out m_MenuMaterial);

            SetButtonColor(m_StartMaterial, waitingColor);
            SetButtonColor(m_RestartMaterial, waitingColor);
            SetButtonColor(m_MenuMaterial, waitingColor);
        }

        private GameObject CreateButton(string name, float localY, PodestAction action,
                                        out Material material)
        {
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(m_Podest.transform, false);

            // Positionen/Maße entsprechen den beiden mittleren Knöpfen des
            // CAVE-Referenzpodests (dessen Modellachsen sind gedreht).
            button.transform.localPosition = new Vector3(-0.0002f, localY, 0.04529f);
            button.transform.localRotation =
                new Quaternion(0.3265056f, 0.3265056f, 0.6272114f, 0.6272114f);
            button.transform.localScale = new Vector3(0.0066666664f, 0.0015f, 0.006666667f);

            button.GetComponent<BoxCollider>().isTrigger = true;

            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = name + " Material" };
            button.GetComponent<Renderer>().material = material;

            button.AddComponent<PhysicalStartButtonTrigger>().Initialize(this, action);
            return button;
        }

        private void BuildAudio()
        {
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 0f;
            m_Audio.clip = GenerateClickClip();
        }

        private void SetButtonColor(Material material, Color color)
        {
            if (material == null) return;

            material.color = color;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.45f);
            }
        }

        private Material MaterialFor(PodestAction action)
        {
            switch (action)
            {
                case PodestAction.Restart: return m_RestartMaterial;
                case PodestAction.Menu: return m_MenuMaterial;
                default: return m_StartMaterial;
            }
        }

        private static bool IsActionAvailable(PodestAction action, GameState state)
        {
            return action == PodestAction.Start
                ? state == GameState.MainMenu
                : state == GameState.GameOver;
        }

        private void ResolveDependencies()
        {
            if (m_Presence == null) m_Presence = FindObjectOfType<KinectPlayerPresence>(true);
            if (m_Interactor == null) m_Interactor = FindObjectOfType<KinectHandInteractor>(true);
        }

        private static AudioClip GenerateClickClip()
        {
            const int sampleRate = 44100;
            const int samples = 2205;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 60f);
                data[i] = 0.45f * envelope * Mathf.Sign(
                    Mathf.Sin(2f * Mathf.PI * 1100f * time));
            }

            var clip = AudioClip.Create("PhysicalStartClick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>Leitet den Trigger-Kontakt eines Knopfes samt Aktion an das Podest weiter.</summary>
    public sealed class PhysicalStartButtonTrigger : MonoBehaviour
    {
        private PhysicalStartPodestController m_Controller;
        private PhysicalStartPodestController.PodestAction m_Action;

        public void Initialize(PhysicalStartPodestController controller,
                               PhysicalStartPodestController.PodestAction action)
        {
            m_Controller = controller;
            m_Action = action;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_Controller != null) m_Controller.TryActivate(m_Action, other);
        }
    }
}
