using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Führt einen UNSICHTBAREN Interaktionspunkt (Collider, kein Renderer) an die
    /// getrackte Hand des Spielers. Damit "drückt" die echte Hand den physischen
    /// Startknopf, ohne dass ein Cursor angezeigt wird.
    ///
    /// Quelle der Handposition (in dieser Reihenfolge):
    /// 1. Der gespiegelte Avatar (<see cref="PositionTransferMultiple"/>) – stimmt exakt
    ///    mit der Hand überein, die der Spieler auf der CAVE-Wand sieht.
    /// 2. Fallback: die rohen Kinect-Gelenke aus <see cref="KinectPlayerPresence"/>.
    /// </summary>
    public sealed class KinectHandInteractor : MonoBehaviour
    {
        public enum HandPreference { Either, Left, Right }
        public enum TrackedHand { None, Left, Right }

        [Tooltip("Welche Hand steuert den Interaktionspunkt? 'Either' nimmt die am weitesten nach vorne gestreckte.")]
        public HandPreference handPreference = HandPreference.Either;
        [Tooltip("Feinjustierung der Handposition (Kalibrierung) in Weltkoordinaten.")]
        public Vector3 calibrationOffset = Vector3.zero;
        [Tooltip("Glättungszeit der Bewegung in Sekunden (0 = keine Glättung).")]
        public float positionSmoothingTime = 0.05f;
        [Tooltip("Radius des unsichtbaren Interaktions-Colliders.")]
        public float interactionPointRadius = 0.06f;

        /// <summary>Liefert true, wenn aktuell eine Hand getrackt ist.</summary>
        public bool HasHand { get; private set; }
        /// <summary>Welche Hand aktuell den Interaktionspunkt steuert.</summary>
        public TrackedHand ActiveHand { get; private set; }
        /// <summary>Geglättete Weltposition der aktiven Hand.</summary>
        public Vector3 HandPosition { get; private set; }
        /// <summary>Der unsichtbare Interaktionspunkt (Transform mit Collider).</summary>
        public Transform InteractionPoint { get; private set; }

        // Beide Hände einzeln (unabhängig geglättet) – so kann z. B. der Podest-Knopf
        // auf JEDE Hand reagieren, statt auf eine flatternde "aktive" Handauswahl.
        /// <summary>Linke Hand aktuell getrackt?</summary>
        public bool HasLeftHand { get; private set; }
        /// <summary>Rechte Hand aktuell getrackt?</summary>
        public bool HasRightHand { get; private set; }
        /// <summary>Geglättete Weltposition der linken Hand (nur gültig bei <see cref="HasLeftHand"/>).</summary>
        public Vector3 LeftHandPosition { get; private set; }
        /// <summary>Geglättete Weltposition der rechten Hand (nur gültig bei <see cref="HasRightHand"/>).</summary>
        public Vector3 RightHandPosition { get; private set; }

        private KinectPlayerPresence m_Presence;
        private PositionTransferMultiple m_AvatarTracker;
        private Vector3 m_SmoothedLeft;
        private Vector3 m_SmoothedRight;
        private bool m_HasSmoothedLeft;
        private bool m_HasSmoothedRight;

        private static readonly Vector3 ParkedPosition = new Vector3(0f, -1000f, 0f);

        private void Awake()
        {
            m_Presence = FindObjectOfType<KinectPlayerPresence>(true);
            m_AvatarTracker = FindObjectOfType<PositionTransferMultiple>(true);
            BuildInteractionPoint();
        }

        private void BuildInteractionPoint()
        {
            var point = new GameObject("Interaction Point (invisible)");
            point.transform.SetParent(transform, false);
            point.transform.position = ParkedPosition;

            // Bewusst KEIN Renderer -> kein sichtbarer Cursor (PDF-Anforderung).
            var collider = point.AddComponent<SphereCollider>();
            collider.radius = interactionPointRadius;
            collider.isTrigger = true;

            var body = point.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            InteractionPoint = point.transform;
        }

        private void Update()
        {
            ResolveHands(out var leftRaw, out bool leftTracked, out var rightRaw, out bool rightTracked);

            HasLeftHand = SmoothHand(leftTracked, leftRaw + calibrationOffset,
                                     ref m_SmoothedLeft, ref m_HasSmoothedLeft);
            HasRightHand = SmoothHand(rightTracked, rightRaw + calibrationOffset,
                                      ref m_SmoothedRight, ref m_HasSmoothedRight);
            LeftHandPosition = m_SmoothedLeft;
            RightHandPosition = m_SmoothedRight;

            // Kompatibilität: zusätzlich weiterhin EINE "aktive" Hand bestimmen.
            if (HasLeftHand || HasRightHand)
            {
                HasHand = true;
                HandPosition = ChooseHand(m_SmoothedLeft, HasLeftHand, m_SmoothedRight, HasRightHand);
                InteractionPoint.position = HandPosition;
            }
            else
            {
                HasHand = false;
                ActiveHand = TrackedHand.None;
                InteractionPoint.position = ParkedPosition;
            }
        }

        /// <summary>Glättet eine Handposition; bei Tracking-Verlust wird die Glättung zurückgesetzt.</summary>
        private bool SmoothHand(bool tracked, Vector3 raw, ref Vector3 smoothed, ref bool hasSmoothed)
        {
            if (!tracked)
            {
                hasSmoothed = false;
                return false;
            }

            if (!hasSmoothed || positionSmoothingTime <= 0f)
            {
                smoothed = raw;
                hasSmoothed = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / positionSmoothingTime);
                smoothed = Vector3.Lerp(smoothed, raw, t);
            }

            return true;
        }

        private void ResolveHands(out Vector3 left, out bool leftTracked,
                                  out Vector3 right, out bool rightTracked)
        {
            left = right = default;
            leftTracked = rightTracked = false;

            // 1) Gespiegelter Avatar (deckt sich mit dem, was der Spieler sieht).
            if (TryGetAvatarParts(out var parts))
            {
                if (parts.TryGetValue("HandLeft", out var leftGo) && leftGo != null)
                {
                    left = leftGo.transform.position;
                    leftTracked = true;
                }
                if (parts.TryGetValue("HandRight", out var rightGo) && rightGo != null)
                {
                    right = rightGo.transform.position;
                    rightTracked = true;
                }

                if (leftTracked || rightTracked)
                {
                    return;
                }
            }

            // 2) Fallback: rohe Kinect-Gelenke.
            if (m_Presence != null)
            {
                m_Presence.TryGetHandPositions(out left, out leftTracked, out right, out rightTracked);
            }
        }

        /// <summary>
        /// Liefert das Körperteil-Wörterbuch des primären Avatars (gespiegelter Raum),
        /// sonst des ersten vorhandenen Avatars.
        /// </summary>
        private bool TryGetAvatarParts(out System.Collections.Generic.Dictionary<string, GameObject> parts)
        {
            parts = null;

            if (m_AvatarTracker == null || m_AvatarTracker.joints == null || m_AvatarTracker.joints.Count == 0)
            {
                return false;
            }

            var primaryName = m_Presence != null && m_Presence.PrimaryActor != null ? m_Presence.PrimaryActor.name : null;

            if (primaryName != null && m_AvatarTracker.joints.TryGetValue(primaryName, out var named))
            {
                parts = named;
                return true;
            }

            foreach (var kvp in m_AvatarTracker.joints)
            {
                parts = kvp.Value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Weltposition eines Avatar-Gelenks (gespiegelter Raum – gleicher Raum wie
        /// <see cref="HandPosition"/>). Gültige Namen z. B. "SpineMid", "ShoulderLeft",
        /// "ShoulderRight", "Head". Liefert false, wenn (noch) kein Avatar getrackt wird.
        /// </summary>
        public bool TryGetAvatarJoint(string partName, out Vector3 worldPos)
        {
            worldPos = default;
            if (!TryGetAvatarParts(out var parts))
            {
                return false;
            }

            if (parts.TryGetValue(partName, out var go) && go != null)
            {
                worldPos = go.transform.position;
                return true;
            }

            return false;
        }

        private Vector3 ChooseHand(Vector3 left, bool leftTracked, Vector3 right, bool rightTracked)
        {
            switch (handPreference)
            {
                case HandPreference.Left:
                    ActiveHand = leftTracked ? TrackedHand.Left : TrackedHand.Right;
                    return leftTracked ? left : right;
                case HandPreference.Right:
                    ActiveHand = rightTracked ? TrackedHand.Right : TrackedHand.Left;
                    return rightTracked ? right : left;
                default:
                    if (leftTracked && rightTracked)
                    {
                        // Die am weitesten nach vorne (Kamera-Blickrichtung) gestreckte Hand greift.
                        var forward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                        if (Vector3.Dot(right, forward) >= Vector3.Dot(left, forward))
                        {
                            ActiveHand = TrackedHand.Right;
                            return right;
                        }

                        ActiveHand = TrackedHand.Left;
                        return left;
                    }

                    ActiveHand = rightTracked ? TrackedHand.Right : TrackedHand.Left;
                    return rightTracked ? right : left;
            }
        }
    }
}
