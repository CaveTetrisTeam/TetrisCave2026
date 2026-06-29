using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Steuert das originale CAVE-Podest zustandsabhängig: Im Hauptmenü steht genau
    /// ein Startknopf bereit, bei Game Over erscheinen zwei Knöpfe (Neustart / Menü).
    /// Während Tracking-Wartezeit und Spiel verschwindet das Podest.
    ///
    /// Auslösung (robust gemacht): Statt eines winzigen, schwer zu treffenden
    /// OnTriggerEnter-Cubes wird DISTANZBASIERT mit kurzem HALTEN ausgelöst. Die
    /// (unsichtbare) getrackte Hand muss nur in die Nähe des Knopfes kommen
    /// (<see cref="activationRadius"/>) und dort kurz bleiben (<see cref="holdTime"/>).
    /// Der Knopf füllt sich dabei sichtbar (Ready-Farbe → Druckfarbe). Das ist
    /// unempfindlich gegen Tracking-Jitter und verhindert versehentliches Auslösen.
    /// </summary>
    public sealed class PhysicalStartPodestController : MonoBehaviour
    {
        public enum PodestAction { Start, Restart, Menu }

        [Header("Tracking / Auslösung")]
        public bool requireReliableTracking = true;
        [Tooltip("Reichweite: so nah muss die Hand am Knopf sein, damit er reagiert (Meter).")]
        public float activationRadius = 0.22f;
        [Tooltip("Haltezeit (Sek.), bis ausgelöst wird.")]
        public float holdTime = 0.5f;
        [Tooltip("Sperrzeit (Sek.) nach einer Auslösung.")]
        public float cooldown = 1.0f;
        [Tooltip("Kurze Sperre nach dem Einblenden, damit eine bereits dort liegende Hand nicht sofort startet.")]
        public float appearGracePeriod = 0.35f;
        [Tooltip("Horizontaler Abstand der beiden Game-Over-Knöpfe (links/rechts) in Metern, " +
                 "damit Neustart/Menü trotz großem Auslöse-Radius unterscheidbar sind.")]
        public float gameOverButtonSeparation = 0.18f;

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

        private bool m_HasHeld;
        private PodestAction m_HeldAction;
        private float m_HoldProgress;

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

            var manager = GameManager.Instance;
            GameState state = manager != null ? manager.CurrentState : GameState.MainMenu;
            bool ready = !requireReliableTracking || (m_Presence != null && m_Presence.HasReliablePlayer);

            // Welcher aktive Knopf liegt aktuell unter der Hand?
            bool canInteract = manager != null && ready &&
                               Time.unscaledTime - m_LastActivation >= cooldown &&
                               Time.unscaledTime - m_ButtonShownAt >= appearGracePeriod &&
                               m_Interactor != null && m_Interactor.HasHand;

            PodestAction candidate = default;
            bool hasCandidate = canInteract &&
                                TryGetTargetAction(state, m_Interactor.HandPosition, out candidate);

            float dt = Time.unscaledDeltaTime;
            float fill = 1f / Mathf.Max(0.05f, holdTime);

            if (hasCandidate)
            {
                if (!m_HasHeld || !m_HeldAction.Equals(candidate))
                {
                    m_HasHeld = true;
                    m_HeldAction = candidate;
                    m_HoldProgress = 0f;
                }

                m_HoldProgress = Mathf.Min(1f, m_HoldProgress + dt * fill);

                if (m_HoldProgress >= 1f)
                {
                    Activate(m_HeldAction);
                }
            }
            else
            {
                m_HasHeld = false;
                m_HoldProgress = Mathf.Max(0f, m_HoldProgress - dt * fill * 2f);
            }

            UpdateColors(ready);
        }

        // ------------------------------------------------------------------ Auslösung

        private bool TryGetTargetAction(GameState state, Vector3 handPos, out PodestAction action)
        {
            action = PodestAction.Start;
            float best = activationRadius;
            bool found = false;

            if (state == GameState.MainMenu)
            {
                found |= Consider(m_StartButton, PodestAction.Start, handPos, ref best, ref action);
            }
            else if (state == GameState.GameOver)
            {
                found |= Consider(m_RestartButton, PodestAction.Restart, handPos, ref best, ref action);
                found |= Consider(m_MenuButton, PodestAction.Menu, handPos, ref best, ref action);
            }

            return found;
        }

        private static bool Consider(GameObject button, PodestAction candidate, Vector3 handPos,
                                     ref float best, ref PodestAction action)
        {
            if (button == null || !button.activeInHierarchy)
            {
                return false;
            }

            float distance = Vector3.Distance(handPos, button.transform.position);
            if (distance <= best)
            {
                best = distance;
                action = candidate;
                return true;
            }

            return false;
        }

        private void Activate(PodestAction action)
        {
            var manager = GameManager.Instance;
            if (manager == null || !IsActionAvailable(action, manager.CurrentState))
            {
                return;
            }

            m_LastActivation = Time.unscaledTime;
            m_HasHeld = false;
            m_HoldProgress = 0f;

            SetButtonColor(MaterialFor(action), pressedColor);
            if (m_Audio != null)
            {
                m_Audio.Play();
            }

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

        // ------------------------------------------------------------------ Podest-Zustand

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

            // Halte-Fortschritt zurücksetzen und Einblende-Sperre starten.
            m_HasHeld = false;
            m_HoldProgress = 0f;
            if (showPodest && !wasInteractive)
            {
                m_ButtonShownAt = Time.unscaledTime;
            }
        }

        // ------------------------------------------------------------------ Aufbau / Optik

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

            m_StartButton = CreateButton("Start Button (Physical)", out m_StartMaterial);
            m_RestartButton = CreateButton("Restart Button (Physical)", out m_RestartMaterial);
            m_MenuButton = CreateButton("Menu Button (Physical)", out m_MenuMaterial);

            // Start = einzeln/mittig. Game-Over-Knöpfe klar nach links/rechts trennen,
            // damit der große Auslöse-Radius Neustart und Menü eindeutig unterscheidet.
            Vector3 center = m_StartButton.transform.position;
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            // Seiten an die gespiegelte CAVE-Ansicht angepasst: Neustart rechts, Menü links.
            m_RestartButton.transform.position = center + right * gameOverButtonSeparation;
            m_MenuButton.transform.position = center - right * gameOverButtonSeparation;

            SetButtonColor(m_StartMaterial, waitingColor);
            SetButtonColor(m_RestartMaterial, waitingColor);
            SetButtonColor(m_MenuMaterial, waitingColor);
        }

        private GameObject CreateButton(string name, out Material material)
        {
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(m_Podest.transform, false);

            // Position/Maße entsprechen dem mittleren Knopf des CAVE-Referenzpodests
            // (dessen Modellachsen sind gedreht).
            button.transform.localPosition = new Vector3(-0.0002f, 0f, 0.04529f);
            button.transform.localRotation =
                new Quaternion(0.3265056f, 0.3265056f, 0.6272114f, 0.6272114f);
            button.transform.localScale = new Vector3(0.0066666664f, 0.0015f, 0.006666667f);

            // Collider bleibt als Trigger erhalten (schadet nicht); ausgelöst wird per Distanz+Halten.
            button.GetComponent<BoxCollider>().isTrigger = true;

            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = name + " Material" };
            button.GetComponent<Renderer>().material = material;

            return button;
        }

        private void BuildAudio()
        {
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 0f;
            m_Audio.clip = GenerateClickClip();
        }

        private void UpdateColors(bool ready)
        {
            UpdateButtonColor(m_StartMaterial, PodestAction.Start, ready ? readyColor : waitingColor);
            UpdateButtonColor(m_RestartMaterial, PodestAction.Restart, ready ? readyColor : waitingColor);
            UpdateButtonColor(m_MenuMaterial, PodestAction.Menu, ready ? menuColor : waitingColor);
        }

        private void UpdateButtonColor(Material material, PodestAction action, Color baseColor)
        {
            // Beim Halten füllt der Knopf sichtbar von Grundfarbe → Druckfarbe.
            Color color = m_HasHeld && m_HeldAction.Equals(action)
                ? Color.Lerp(baseColor, pressedColor, m_HoldProgress)
                : baseColor;
            SetButtonColor(material, color);
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
}
