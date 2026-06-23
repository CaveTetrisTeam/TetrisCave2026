using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Steuert das originale CAVE-Podest mit genau einem digitalen physischen
    /// Startknopf. Die Trigger-Idee stammt direkt aus ObjectToggleButton des
    /// FKI-HTW/CAVE-Samples; ausgelöst wird ausschließlich durch den unsichtbaren
    /// Kinect-Hand-Collider.
    /// </summary>
    public sealed class PhysicalStartPodestController : MonoBehaviour
    {
        [Header("Tracking")]
        public bool requireReliableTracking = true;
        public float cooldown = 1.0f;
        [Tooltip("Kurze Sperre nach dem Einblenden, damit eine bereits dort liegende Hand nicht sofort startet.")]
        public float appearGracePeriod = 0.35f;

        [Header("Farben")]
        public Color waitingColor = new Color(0.85f, 0.16f, 0.10f);
        public Color readyColor = new Color(0.15f, 0.85f, 0.35f);
        public Color pressedColor = new Color(1.0f, 0.72f, 0.18f);

        private GameObject m_Podest;
        private GameObject m_Button;
        private Renderer m_ButtonRenderer;
        private Material m_ButtonMaterial;
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
            BuildSingleButton();
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
            SetButtonColor(ready || !requireReliableTracking ? readyColor : waitingColor);
        }

        public void TryActivate(Collider other)
        {
            ResolveDependencies();

            var manager = GameManager.Instance;
            if (manager == null || manager.CurrentState != GameState.MainMenu ||
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
            SetButtonColor(pressedColor);
            if (m_Audio != null) m_Audio.Play();

            // Derselbe Zustandswechsel wie früher beim UI-Button.
            manager.RequestStart();
        }

        private void HandleStateChanged(GameState state)
        {
            SetButtonVisible(state == GameState.MainMenu);
        }

        private void RefreshVisibility()
        {
            var manager = GameManager.Instance;
            SetButtonVisible(manager == null || manager.CurrentState == GameState.MainMenu);
        }

        private void SetButtonVisible(bool visible)
        {
            // Das Podest bleibt Teil der Umgebung; nur der Startknopf folgt dem Menüstatus.
            bool wasVisible = m_Podest != null && m_Button != null &&
                              m_Podest.activeInHierarchy && m_Button.activeInHierarchy;

            if (m_Podest != null && !m_Podest.activeSelf)
            {
                m_Podest.SetActive(true);
            }

            if (m_Button != null && m_Button.activeSelf != visible)
            {
                m_Button.SetActive(visible);
            }

            if (visible && !wasVisible) m_ButtonShownAt = Time.unscaledTime;
        }

        private void BuildSingleButton()
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

            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "Start Button (Physical)";
            m_Button = button;
            button.transform.SetParent(m_Podest.transform, false);

            // Mittlere Position und Maße aus dem CAVETools-Podest. Im Original
            // liegen die vier Knöpfe bei X = -0.3, -0.1, 0.1, 0.3 Metern.
            button.transform.localPosition = new Vector3(-0.0002f, 0f, 0.04529f);
            button.transform.localRotation = new Quaternion(0.3265056f, 0.3265056f, 0.6272114f, 0.6272114f);
            button.transform.localScale = new Vector3(0.0066666664f, 0.0015f, 0.006666667f);

            var collider = button.GetComponent<BoxCollider>();
            collider.isTrigger = true;

            m_ButtonRenderer = button.GetComponent<Renderer>();
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            m_ButtonMaterial = new Material(shader) { name = "Physical Start Button Material" };
            m_ButtonRenderer.material = m_ButtonMaterial;

            var trigger = button.AddComponent<PhysicalStartButtonTrigger>();
            trigger.Initialize(this);
            SetButtonColor(waitingColor);
        }

        private void BuildAudio()
        {
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 0f;
            m_Audio.clip = GenerateClickClip();
        }

        private void SetButtonColor(Color color)
        {
            if (m_ButtonMaterial == null) return;

            m_ButtonMaterial.color = color;
            if (m_ButtonMaterial.HasProperty("_EmissionColor"))
            {
                m_ButtonMaterial.EnableKeyword("_EMISSION");
                m_ButtonMaterial.SetColor("_EmissionColor", color * 0.45f);
            }
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

    /// <summary>Leitet den Trigger-Kontakt des einzelnen Knopfes an das Podest weiter.</summary>
    public sealed class PhysicalStartButtonTrigger : MonoBehaviour
    {
        private PhysicalStartPodestController m_Controller;

        public void Initialize(PhysicalStartPodestController controller)
        {
            m_Controller = controller;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_Controller != null) m_Controller.TryActivate(other);
        }
    }
}
