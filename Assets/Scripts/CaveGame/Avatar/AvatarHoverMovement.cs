using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Schwebe-Bewegung des Begleiter-Avatars: gleitet sanft (SmoothDamp) zu einem
    /// festen Ankerpunkt (<see cref="MoveTo"/>) oder wandert zwischen Zufallspunkten
    /// innerhalb eines Quaders (<see cref="WanderWithin"/>). Dabei wippt er leicht
    /// auf und ab und dreht sich immer zum Spieler in der CAVE-Mitte.
    ///
    /// Läuft mit unskalierter Zeit, damit der Avatar auch während einer Spielpause
    /// (z. B. Sprachfrage mit Time.timeScale = 0) lebendig bleibt.
    /// </summary>
    public sealed class AvatarHoverMovement : MonoBehaviour
    {
        [Header("Bewegung")]
        [Tooltip("Glättungszeit der Bewegung (größer = träger/schwebender).")]
        public float smoothTime = 1.1f;
        [Tooltip("Maximale Fluggeschwindigkeit in m/s.")]
        public float maxSpeed = 2.5f;

        [Header("Schweben")]
        [Tooltip("Amplitude des Auf-und-ab-Wippens in Metern.")]
        public float bobAmplitude = 0.06f;
        [Tooltip("Frequenz des Wippens in Hz.")]
        public float bobFrequency = 0.55f;
        [Tooltip("Leichtes seitliches Pendeln in Grad.")]
        public float swayDegrees = 4f;
        [Tooltip("Kind-Transform, das wippen/pendeln soll (i. d. R. das Modell).")]
        public Transform bobTarget;

        [Header("Ausrichtung")]
        [Tooltip("Kopfhöhe des Spielers, zu dem sich der Avatar dreht.")]
        public float playerHeadHeight = 1.7f;
        [Tooltip("Zusätzliche Y-Drehung, falls das Modell aus Blender nicht Richtung -Z schaut.")]
        public float modelYawOffset = 0f;
        [Tooltip("Drehgeschwindigkeit zum Spieler hin.")]
        public float turnSpeed = 3f;

        private Vector3 m_Anchor;
        private Vector3 m_Velocity;
        private bool m_Wander;
        private Bounds m_WanderBounds;
        private Vector2 m_WanderInterval = new Vector2(3.5f, 7f);
        private float m_NextWanderTime;
        private float m_BobPhase;
        private Vector3 m_BobBase;
        private bool m_BobBaseCaptured;

        private void Awake()
        {
            m_Anchor = transform.position;
        }

        // ---------------------------------------------------------------------
        // Öffentliche API
        // ---------------------------------------------------------------------

        /// <summary>Sanft zu einem festen Punkt fliegen und dort schweben.</summary>
        public void MoveTo(Vector3 anchor)
        {
            m_Anchor = anchor;
            m_Wander = false;
        }

        /// <summary>Sofort an einen Punkt springen (für die Startaufstellung).</summary>
        public void Teleport(Vector3 position)
        {
            transform.position = position;
            m_Anchor = position;
            m_Velocity = Vector3.zero;
            m_Wander = false;
        }

        /// <summary>
        /// Frei innerhalb des Quaders umherschwirren; alle paar Sekunden
        /// (zufällig aus <paramref name="intervalRange"/>) wird ein neues Ziel gewählt.
        /// </summary>
        public void WanderWithin(Bounds bounds, Vector2 intervalRange)
        {
            m_WanderBounds = bounds;
            m_WanderInterval = intervalRange;
            m_Wander = true;
            PickWanderTarget();
        }

        // ---------------------------------------------------------------------
        // Bewegung
        // ---------------------------------------------------------------------

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (m_Wander && Time.unscaledTime >= m_NextWanderTime)
            {
                PickWanderTarget();
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, m_Anchor, ref m_Velocity, smoothTime, maxSpeed, dt);

            // Wippen und Pendeln nur auf dem Modell-Kind, damit die Sprechblase
            // ruhig und gut lesbar bleibt.
            m_BobPhase += dt * bobFrequency * Mathf.PI * 2f;
            if (bobTarget != null)
            {
                if (!m_BobBaseCaptured)
                {
                    m_BobBase = bobTarget.localPosition;
                    m_BobBaseCaptured = true;
                }

                bobTarget.localPosition =
                    m_BobBase + Vector3.up * (Mathf.Sin(m_BobPhase) * bobAmplitude);
                bobTarget.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Sin(m_BobPhase * 0.7f) * swayDegrees);
            }

            // Nur um die Hochachse zum Spieler drehen (Blase bleibt aufrecht).
            Vector3 toPlayer = new Vector3(0f, playerHeadHeight, 0f) - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                var target = Quaternion.LookRotation(toPlayer) *
                             Quaternion.Euler(0f, modelYawOffset, 0f);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * dt));
            }
        }

        private void PickWanderTarget()
        {
            m_Anchor = new Vector3(
                Random.Range(m_WanderBounds.min.x, m_WanderBounds.max.x),
                Random.Range(m_WanderBounds.min.y, m_WanderBounds.max.y),
                Random.Range(m_WanderBounds.min.z, m_WanderBounds.max.z));
            m_NextWanderTime = Time.unscaledTime +
                               Random.Range(m_WanderInterval.x, m_WanderInterval.y);
        }
    }
}
