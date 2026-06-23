using UnityEngine;
using HumanTetris;

namespace CaveGame
{
    /// <summary>
    /// Physischer 3D-Startknopf, der sich mit der getrackten Hand "drücken" lässt –
    /// ohne sichtbaren Cursor. Adaptiert das Trigger-/Cooldown-Prinzip aus dem
    /// CAVE-Referenz-Sample <c>ObjectToggleButton</c> und erweitert es um:
    ///   • sichtbares Reagieren beim Berühren (Hover-Leuchten),
    ///   • sichtbares Eindrücken der Kappe nach hinten,
    ///   • Auslösen erst ab einer definierten Drucktiefe,
    ///   • Schutz gegen versehentliches Auslösen beim Vorbeiwischen
    ///     (laterale Grenze + Mindest-Kontaktzeit + Cooldown),
    ///   • Klick-Sound beim Auslösen,
    ///   • Aktivierung nur bei zuverlässig getrackter Person.
    ///
    /// Die Drucktiefe wird aus der (unsichtbaren) Handposition des
    /// <see cref="KinectHandInteractor"/> berechnet. Die lokale +Z-Achse zeigt zum
    /// Spieler (Druckrichtung = lokal -Z).
    /// </summary>
    public sealed class KinectPhysicalButton : MonoBehaviour
    {
        [Header("Druck-Mechanik (Meter)")]
        [Tooltip("Maximale sichtbare Eindrücktiefe der Kappe.")]
        public float maxPressDepth = 0.05f;
        [Tooltip("Ab dieser Drucktiefe wird ausgelöst.")]
        public float activationDepth = 0.035f;
        [Tooltip("Abstand vor der Kappe, ab dem der Knopf visuell auf Berührung reagiert (Hover).")]
        public float touchDistance = 0.06f;
        [Tooltip("Maximaler seitlicher Abstand der Hand zur Knopfachse, der noch zählt (Anti-Vorbeiwischen).")]
        public float lateralRadius = 0.14f;

        [Header("Anti-Fehlauslösung")]
        [Tooltip("Mindestzeit (Sek.), die die Hand am Knopf bleiben muss, bevor ausgelöst wird.")]
        public float minContactTime = 0.10f;
        [Tooltip("Sperrzeit (Sek.) nach einer Auslösung.")]
        public float cooldown = 1.5f;

        [Header("Verhalten")]
        [Tooltip("Nur auslösen, wenn eine Person zuverlässig getrackt wird (PDF). Zum Testen ohne Kinect deaktivieren.")]
        public bool requirePlayerTracking = true;
        [Tooltip("Geschwindigkeit, mit der die Kappe folgt/zurückfedert.")]
        public float capFollowSpeed = 18f;

        [Header("Optik")]
        public Color idleColor = new Color(0.85f, 0.25f, 0.18f);
        public Color hoverColor = new Color(1.0f, 0.55f, 0.20f);
        public Color pressedColor = new Color(0.30f, 0.85f, 0.40f);
        [Tooltip("Bewegliche Kappe. Wird automatisch erzeugt, wenn leer.")]
        public Transform cap;

        [Header("Audio")]
        [Tooltip("Klick-Sound beim Auslösen. Wird sonst prozedural erzeugt.")]
        public AudioClip clickSound;

        private KinectHandInteractor m_Interactor;
        private KinectPlayerPresence m_Presence;
        private AudioSource m_Audio;
        private Renderer m_CapRenderer;
        private MaterialPropertyBlock m_Mpb;

        private float m_RestLocalZ;
        private float m_CurrentDepth;
        private float m_ContactTime;
        private float m_LastActivation = -999f;

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            m_Interactor = FindObjectOfType<KinectHandInteractor>(true);
            m_Presence = FindObjectOfType<KinectPlayerPresence>(true);

            if (cap == null)
            {
                BuildDefaultVisuals();
            }

            m_CapRenderer = cap.GetComponent<Renderer>();
            m_Mpb = new MaterialPropertyBlock();
            m_RestLocalZ = cap.localPosition.z;

            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 1f; // 3D-Sound am Knopf
            if (clickSound == null)
            {
                clickSound = GenerateClickClip();
            }

            ApplyCapColor(idleColor, 0f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime; // funktioniert auch bei pausiertem Startmenü (timeScale 0)

            float targetDepth = 0f;
            bool hovering = false;

            if (ButtonIsActive() && m_Interactor != null && m_Interactor.HasHand)
            {
                Vector3 local = transform.InverseTransformPoint(m_Interactor.HandPosition);
                float lateral = new Vector2(local.x, local.y).magnitude;

                bool withinLateral = lateral <= lateralRadius;
                bool withinReach = local.z <= touchDistance && local.z >= -(maxPressDepth + 0.06f);

                if (withinLateral && withinReach)
                {
                    hovering = true;
                    targetDepth = Mathf.Clamp(-local.z, 0f, maxPressDepth);
                    m_ContactTime += dt;

                    bool deepEnough = targetDepth >= activationDepth;
                    bool dwelled = m_ContactTime >= minContactTime;
                    bool ready = Time.unscaledTime - m_LastActivation >= cooldown;

                    if (deepEnough && dwelled && ready)
                    {
                        Activate();
                    }
                }
                else
                {
                    m_ContactTime = 0f;
                }
            }
            else
            {
                m_ContactTime = 0f;
            }

            // Kappe weich zur Zieltiefe bewegen / zurückfedern.
            m_CurrentDepth = Mathf.MoveTowards(m_CurrentDepth, targetDepth, capFollowSpeed * maxPressDepth * dt);
            var lp = cap.localPosition;
            lp.z = m_RestLocalZ - m_CurrentDepth;
            cap.localPosition = lp;

            UpdateCapVisual(hovering);
        }

        private bool ButtonIsActive()
        {
            var gm = GameManager.Instance;
            bool inMenu = gm == null || gm.CurrentState == GameState.MainMenu;
            if (!inMenu)
            {
                return false;
            }

            if (requirePlayerTracking && m_Presence != null)
            {
                return m_Presence.HasReliablePlayer;
            }

            return true;
        }

        private void Activate()
        {
            m_LastActivation = Time.unscaledTime;
            m_ContactTime = 0f;

            if (clickSound != null)
            {
                m_Audio.PlayOneShot(clickSound);
            }

            // Start auslösen – GameManager bevorzugt, sonst direkt das Startmenü.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RequestStart();
            }
            else
            {
                HumanTetrisStartMenu.RequestStartStatic();
            }
        }

        private void UpdateCapVisual(bool hovering)
        {
            float pressT = maxPressDepth > 0f ? Mathf.Clamp01(m_CurrentDepth / maxPressDepth) : 0f;
            Color baseColor = hovering ? Color.Lerp(hoverColor, pressedColor, pressT) : idleColor;
            float emission = hovering ? Mathf.Lerp(0.25f, 0.9f, pressT) : 0f;
            ApplyCapColor(baseColor, emission);
        }

        private void ApplyCapColor(Color color, float emissionIntensity)
        {
            if (m_CapRenderer == null)
            {
                return;
            }

            m_CapRenderer.GetPropertyBlock(m_Mpb);
            m_Mpb.SetColor(ColorID, color);
            m_Mpb.SetColor(EmissionID, color * emissionIntensity);
            m_CapRenderer.SetPropertyBlock(m_Mpb);
        }

        // ---------------------------------------------------------------------
        // Standard-Optik (Gehäuse + Kappe), falls keine Kappe zugewiesen ist.
        // ---------------------------------------------------------------------
        private void BuildDefaultVisuals()
        {
            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Housing";
            Destroy(housing.GetComponent<Collider>());
            housing.transform.SetParent(transform, false);
            housing.transform.localPosition = new Vector3(0f, 0f, -0.07f);
            housing.transform.localScale = new Vector3(0.32f, 0.32f, 0.06f);
            var hr = housing.GetComponent<Renderer>();
            hr.material.color = new Color(0.12f, 0.13f, 0.16f);

            var capGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            capGo.name = "Cap";
            Destroy(capGo.GetComponent<Collider>());
            capGo.transform.SetParent(transform, false);
            // Vorderfläche der Kappe liegt bei lokal z = 0 (Druckebene).
            capGo.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            capGo.transform.localScale = new Vector3(0.24f, 0.24f, 0.06f);

            cap = capGo.transform;
        }

        /// <summary>Erzeugt einen kurzen Klick-Sound, falls keiner zugewiesen ist.</summary>
        private static AudioClip GenerateClickClip()
        {
            const int sampleRate = 44100;
            const int samples = 2205; // ~50 ms
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 60f);             // schnelles Abklingen
                float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1100f * t)); // knackiger Square-Klick
                data[i] = 0.5f * envelope * tone;
            }

            var clip = AudioClip.Create("ButtonClick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
