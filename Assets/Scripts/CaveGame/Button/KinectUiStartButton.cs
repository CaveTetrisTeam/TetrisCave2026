using UnityEngine;
using UnityEngine.UI;
using Windows.Kinect;

namespace CaveGame
{
    /// <summary>
    /// Ergänzt den sichtbaren "Spiel starten"-Button um eine cursorlose
    /// Kinect-Druckinteraktion. Der klassische UI-Klick bleibt als Fallback erhalten.
    /// Die Hand muss über dem Button liegen, kurz dort bleiben und gegenüber der
    /// Schulter eine definierte Strecke nach vorne ausgestreckt werden.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(Button))]
    public sealed class KinectUiStartButton : MonoBehaviour
    {
        [Header("Kinect-Druck")]
        [Tooltip("Mindeststrecke der Hand vor der Schulter, bevor der Button auslöst (Meter).")]
        public float activationReach = 0.18f;
        [Tooltip("Ab dieser Ausstreckung beginnt die sichtbare Druckbewegung (Meter).")]
        public float touchReach = 0.04f;
        [Tooltip("Mindestzeit der Hand über dem Button gegen Vorbeiwischen (Sekunden).")]
        public float minimumContactTime = 0.12f;
        [Tooltip("Sperrzeit nach einer Auslösung (Sekunden).")]
        public float cooldown = 1.25f;
        [Tooltip("Der Kinect-Start ist erst bei stabil erkanntem Spieler aktiv.")]
        public bool requireReliableTracking = true;

        [Header("Rückmeldung")]
        public float visualPressPixels = 10f;
        public Color idleColor = new Color(1.00f, 0.38f, 0.20f);
        public Color hoverColor = new Color(1.00f, 0.62f, 0.24f);
        public Color pressedColor = new Color(0.35f, 0.88f, 0.48f);

        private RectTransform m_Rect;
        private Image m_Image;
        private Button m_Button;
        private KinectHandInteractor m_Hand;
        private KinectPlayerPresence m_Presence;
        private Camera m_FrontCamera;

        private Vector2 m_RestPosition;
        private float m_ContactTime;
        private float m_LastActivation = -999f;
        private float m_VisualPress;
        private bool m_Started;
        private bool m_WasInMenu = true;

        private void Awake()
        {
            m_Rect = GetComponent<RectTransform>();
            m_Image = GetComponent<Image>();
            m_Button = GetComponent<Button>();
            m_RestPosition = m_Rect.anchoredPosition;
            ResolveDependencies();
        }

        private void OnEnable()
        {
            m_Started = false;
            m_ContactTime = 0f;
        }

        private void Start()
        {
            // BuildMenu setzt die endgültige Position erst direkt nach AddComponent.
            m_RestPosition = m_Rect.anchoredPosition;
        }

        private void LateUpdate()
        {
            ResolveDependencies();

            bool inMenu = GameManager.Instance == null ||
                          GameManager.Instance.CurrentState == GameState.MainMenu;
            if (inMenu && !m_WasInMenu)
            {
                m_Started = false;
            }
            m_WasInMenu = inMenu;

            bool trackingReady = m_Presence != null && m_Presence.HasReliablePlayer;
            bool canInteract = inMenu && !m_Started && m_Hand != null && m_Hand.HasHand &&
                               (!requireReliableTracking || trackingReady);

            float targetPress = 0f;
            bool hovering = false;

            if (canInteract && TryGetHandScreenPoint(out var screenPoint, out var reach) &&
                RectTransformUtility.RectangleContainsScreenPoint(m_Rect, screenPoint, null))
            {
                hovering = true;
                m_ContactTime += Time.unscaledDeltaTime;
                targetPress = Mathf.InverseLerp(touchReach, activationReach, reach);

                bool deepEnough = reach >= activationReach;
                bool dwelled = m_ContactTime >= minimumContactTime;
                bool cooledDown = Time.unscaledTime - m_LastActivation >= cooldown;
                if (deepEnough && dwelled && cooledDown)
                {
                    Activate();
                }
            }
            else
            {
                m_ContactTime = 0f;
            }

            m_VisualPress = Mathf.MoveTowards(
                m_VisualPress, targetPress, Time.unscaledDeltaTime * 8f);
            m_Rect.anchoredPosition = m_RestPosition + Vector2.down * (visualPressPixels * m_VisualPress);
            m_Image.color = hovering
                ? Color.Lerp(hoverColor, pressedColor, m_VisualPress)
                : Color.Lerp(m_Image.color, idleColor, Time.unscaledDeltaTime * 10f);
        }

        private void Activate()
        {
            m_Started = true;
            m_LastActivation = Time.unscaledTime;
            m_ContactTime = 0f;
            m_VisualPress = 1f;

            // Nutzt exakt denselben onClick-Ablauf wie der funktionierende Maus-Button.
            m_Button.onClick.Invoke();
        }

        private bool TryGetHandScreenPoint(out Vector2 screenPoint, out float reach)
        {
            screenPoint = default;
            reach = 0f;

            if (m_FrontCamera == null || m_Hand == null)
            {
                return false;
            }

            var projected = m_FrontCamera.WorldToScreenPoint(m_Hand.HandPosition);
            if (projected.z <= 0f)
            {
                return false;
            }

            screenPoint = projected;

            if (m_Presence == null ||
                !m_Presence.TryGetJointPosition(JointType.SpineShoulder, out var shoulder) ||
                !m_Presence.TryGetHandPositions(out var left, out var leftTracked,
                                                out var right, out var rightTracked))
            {
                return false;
            }

            Vector3 rawHand;
            if (m_Hand.ActiveHand == KinectHandInteractor.TrackedHand.Left && leftTracked)
            {
                rawHand = left;
            }
            else if (m_Hand.ActiveHand == KinectHandInteractor.TrackedHand.Right && rightTracked)
            {
                rawHand = right;
            }
            else
            {
                rawHand = rightTracked ? right : left;
            }

            // Kinect-Z zeigt vom Sensor weg. Eine zum Sensor ausgestreckte Hand hat
            // daher einen kleineren Z-Wert als die Schulter.
            reach = shoulder.z - rawHand.z;
            return true;
        }

        private void ResolveDependencies()
        {
            if (m_Hand == null) m_Hand = FindObjectOfType<KinectHandInteractor>(true);
            if (m_Presence == null) m_Presence = FindObjectOfType<KinectPlayerPresence>(true);
            if (m_FrontCamera == null) m_FrontCamera = FindFrontCamera();
        }

        private static Camera FindFrontCamera()
        {
            Camera fallback = Camera.main;
            Camera frontRight = null;

            foreach (var camera in FindObjectsOfType<Camera>(true))
            {
                string cameraName = camera.name.ToLowerInvariant();
                if (cameraName.Contains("front l")) return camera;
                if (cameraName.Contains("front r")) frontRight = camera;
                if (fallback == null && camera.enabled) fallback = camera;
            }

            return frontRight != null ? frontRight : fallback;
        }
    }
}
