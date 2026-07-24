using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Steuert das originale CAVE-Podest zustandsabhängig: Im Hauptmenü steht genau
    /// ein Startknopf bereit, bei Game Over erscheinen zwei Knöpfe (Neustart / Menü).
    /// Während Tracking-Wartezeit und Spiel verschwindet das Podest.
    ///
    /// Auslösung (robust gemacht): Statt eines winzigen, schwer zu treffenden
    /// OnTriggerEnter-Cubes wird DISTANZBASIERT mit kurzem HALTEN ausgelöst.
    /// Die Erkennungszone ist ein ZYLINDER über dem Knopf: seitlich
    /// <see cref="activationRadius"/>, nach oben großzügig <see cref="verticalTolerance"/> –
    /// die Hand schwebt naturgemäß ÜBER dem Knopf, nicht in ihm. Geprüft werden
    /// BEIDE getrackten Hände. Während des Haltens wächst die Zone um
    /// <see cref="holdZoneScale"/> (Hysterese gegen Zittern), und kurze Tracking-
    /// Aussetzer frieren den Fortschritt ein (<see cref="dropoutGrace"/>), statt ihn
    /// zu entleeren. Der Knopf füllt sich sichtbar (Farbe) und wächst leicht mit.
    /// </summary>
    public sealed class PhysicalStartPodestController : MonoBehaviour
    {
        public enum PodestAction { Start, Restart, Menu }

        [Header("Tracking / Auslösung")]
        public bool requireReliableTracking = true;
        [Tooltip("Seitliche Reichweite: so nah (horizontal) muss eine Hand am Knopf sein (Meter).")]
        public float activationRadius = 0.24f;
        [Tooltip("Höhen-Toleranz: so weit ÜBER dem Knopf darf die Hand schweben (Meter).")]
        public float verticalTolerance = 0.35f;
        [Tooltip("Haltezeit (Sek.), bis ausgelöst wird. Lang genug, dass Vorbeiwischen nicht auslöst.")]
        public float holdTime = 0.5f;
        [Tooltip("Hysterese: Während des Haltens wächst die Erkennungszone um diesen Faktor, " +
                 "damit Tracking-Zittern den Fortschritt nicht abbricht.")]
        public float holdZoneScale = 1.25f;
        [Tooltip("Kurze Tracking-Aussetzer bis zu dieser Dauer (Sek.) frieren den Halte-" +
                 "Fortschritt ein, statt ihn zu entleeren.")]
        public float dropoutGrace = 0.35f;
        [Tooltip("Sperrzeit (Sek.) nach einer Auslösung.")]
        public float cooldown = 1.0f;
        [Tooltip("Kurze Sperre nach dem Einblenden, damit eine bereits dort liegende Hand nicht sofort startet.")]
        public float appearGracePeriod = 0.35f;
        [Tooltip("Horizontaler Abstand der beiden Game-Over-Knöpfe (links/rechts) in Metern, " +
                 "damit Neustart/Menü trotz großem Auslöse-Radius unterscheidbar sind.")]
        public float gameOverButtonSeparation = 0.26f;

        [Header("Game-Over-Knöpfe (strenger als der Start-Knopf)")]
        [Tooltip("Verkleinert die Erkennungszone von Neustart/Menü relativ zum Start-Knopf " +
                 "(0.75 = 75 % von activationRadius/verticalTolerance). Die Zonen der beiden " +
                 "Knöpfe überlappen sonst und reagieren auf jede Hand in Podestnähe.")]
        public float gameOverZoneScale = 0.75f;
        [Tooltip("Haltezeit (Sek.) für Neustart/Menü – bewusst länger, weil der Spieler bei " +
                 "Game Over noch in Bewegung direkt am Podest steht.")]
        public float gameOverHoldTime = 0.8f;
        [Tooltip("Einblende-Sperre (Sek.) speziell nach Game Over: solange reagieren die " +
                 "Knöpfe nicht, damit die noch schwingenden Arme nichts auslösen.")]
        public float gameOverAppearGrace = 1.2f;

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
        private float m_LastHoldContact = -999f;
        private Vector3 m_ButtonBaseScale = Vector3.one;

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

            // Grundvoraussetzungen (unabhängig vom Tracking-Aussetzer).
            float appearGrace = state == GameState.GameOver ? gameOverAppearGrace : appearGracePeriod;
            bool gateOpen = manager != null && ready &&
                            Time.unscaledTime - m_LastActivation >= cooldown &&
                            Time.unscaledTime - m_ButtonShownAt >= appearGrace;

            // Welcher aktive Knopf liegt aktuell unter einer der beiden Hände?
            PodestAction candidate = default;
            bool hasCandidate = gateOpen && m_Interactor != null &&
                                (m_Interactor.HasLeftHand || m_Interactor.HasRightHand) &&
                                TryGetTargetAction(state, out candidate);

            float dt = Time.unscaledDeltaTime;
            float effectiveHoldTime = state == GameState.GameOver ? gameOverHoldTime : holdTime;
            float fill = 1f / Mathf.Max(0.05f, effectiveHoldTime);

            if (hasCandidate)
            {
                if (!m_HasHeld || !m_HeldAction.Equals(candidate))
                {
                    m_HasHeld = true;
                    m_HeldAction = candidate;
                    m_HoldProgress = 0f;
                }

                m_HoldProgress = Mathf.Min(1f, m_HoldProgress + dt * fill);
                m_LastHoldContact = Time.unscaledTime;

                if (m_HoldProgress >= 1f)
                {
                    Activate(m_HeldAction);
                }
            }
            else if (m_HasHeld && Time.unscaledTime - m_LastHoldContact <= dropoutGrace)
            {
                // Kurzer Tracking-Aussetzer (Hand weg oder HasReliablePlayer flattert):
                // Fortschritt einfrieren statt entleeren.
            }
            else
            {
                m_HasHeld = false;
                m_HoldProgress = Mathf.Max(0f, m_HoldProgress - dt * fill);
            }

            UpdateColors(ready);
            UpdateHoldScale();
        }

        // ------------------------------------------------------------------ Auslösung

        private bool TryGetTargetAction(GameState state, out PodestAction action)
        {
            action = PodestAction.Start;
            float bestScore = float.MaxValue;
            bool found = false;

            if (state == GameState.MainMenu)
            {
                found |= Consider(m_StartButton, PodestAction.Start, ref bestScore, ref action);
            }
            else if (state == GameState.GameOver)
            {
                found |= Consider(m_RestartButton, PodestAction.Restart, ref bestScore, ref action);
                found |= Consider(m_MenuButton, PodestAction.Menu, ref bestScore, ref action);
            }

            return found;
        }

        /// <summary>
        /// Prüft, ob eine der beiden Hände in der Zylinder-Zone dieses Knopfes liegt.
        /// Der aktuell gehaltene Knopf bekommt eine größere Zone (Hysterese) und einen
        /// Score-Bonus, damit knappe Duelle zwischen zwei Knöpfen den Fortschritt
        /// nicht ständig zurücksetzen.
        /// </summary>
        private bool Consider(GameObject button, PodestAction candidate,
                              ref float bestScore, ref PodestAction action)
        {
            if (button == null || !button.activeInHierarchy)
            {
                return false;
            }

            bool isHeld = m_HasHeld && m_HeldAction.Equals(candidate);
            float zoneScale = isHeld ? Mathf.Max(1f, holdZoneScale) : 1f;

            // Neustart/Menü bekommen eine engere Zone als der Start-Knopf.
            if (candidate != PodestAction.Start)
            {
                zoneScale *= Mathf.Clamp(gameOverZoneScale, 0.2f, 1f);
            }

            float nearest = float.MaxValue;
            if (m_Interactor.HasLeftHand)
            {
                ConsiderHand(button, m_Interactor.LeftHandPosition, zoneScale, ref nearest);
            }
            if (m_Interactor.HasRightHand)
            {
                ConsiderHand(button, m_Interactor.RightHandPosition, zoneScale, ref nearest);
            }

            if (nearest == float.MaxValue)
            {
                return false;
            }

            float score = isHeld ? nearest * 0.6f : nearest;
            if (score < bestScore)
            {
                bestScore = score;
                action = candidate;
            }

            return true;
        }

        /// <summary>Zylinder-Test: seitlich eng, nach oben großzügig (Hand schwebt über dem Knopf).</summary>
        private void ConsiderHand(GameObject button, Vector3 hand, float zoneScale, ref float nearest)
        {
            Vector3 delta = hand - button.transform.position;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;

            // Leicht unterhalb zulassen (Kalibrier-Ungenauigkeit), nach oben verticalTolerance.
            bool inZone = horizontal <= activationRadius * zoneScale &&
                          delta.y >= -0.10f &&
                          delta.y <= verticalTolerance * zoneScale;
            if (inZone)
            {
                nearest = Mathf.Min(nearest, horizontal);
            }
        }

        /// <summary>
        /// Per Sprachbefehl auslösen (gleiche Regeln wie der Hand-Knopf: nur im passenden
        /// Zustand, Cooldown, Einblende-Sperre und – falls verlangt – zuverlässiges Tracking).
        /// </summary>
        public bool TryTriggerByVoice(PodestAction action)
        {
            ResolveDependencies();

            var manager = GameManager.Instance;
            if (manager == null || !IsActionAvailable(action, manager.CurrentState))
            {
                return false;
            }

            if (Time.unscaledTime - m_LastActivation < cooldown ||
                Time.unscaledTime - m_ButtonShownAt < appearGracePeriod)
            {
                return false;
            }

            if (requireReliableTracking && (m_Presence == null || !m_Presence.HasReliablePlayer))
            {
                return false;
            }

            Activate(action);
            return true;
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
            // Seiten getauscht: Neustart links, Menü rechts.
            m_RestartButton.transform.position = center - right * gameOverButtonSeparation;
            m_MenuButton.transform.position = center + right * gameOverButtonSeparation;

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
            m_ButtonBaseScale = new Vector3(0.0066666664f, 0.0015f, 0.006666667f);
            button.transform.localScale = m_ButtonBaseScale;

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

        /// <summary>Der gehaltene Knopf wächst sichtbar mit dem Fortschritt (bis +35 %).</summary>
        private void UpdateHoldScale()
        {
            ApplyHoldScale(m_StartButton, PodestAction.Start);
            ApplyHoldScale(m_RestartButton, PodestAction.Restart);
            ApplyHoldScale(m_MenuButton, PodestAction.Menu);
        }

        private void ApplyHoldScale(GameObject button, PodestAction action)
        {
            if (button == null)
            {
                return;
            }

            float pulse = m_HasHeld && m_HeldAction.Equals(action)
                ? 1f + 0.35f * m_HoldProgress
                : 1f;
            button.transform.localScale = m_ButtonBaseScale * pulse;
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
