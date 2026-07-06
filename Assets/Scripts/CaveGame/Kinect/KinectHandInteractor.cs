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

        private KinectPlayerPresence m_Presence;
        private PositionTransferMultiple m_AvatarTracker;
        private Vector3 m_Smoothed;
        private bool m_HasSmoothed;

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
            if (TryResolveHand(out var rawPosition))
            {
                rawPosition += calibrationOffset;

                if (!m_HasSmoothed || positionSmoothingTime <= 0f)
                {
                    m_Smoothed = rawPosition;
                    m_HasSmoothed = true;
                }
                else
                {
                    float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / positionSmoothingTime);
                    m_Smoothed = Vector3.Lerp(m_Smoothed, rawPosition, t);
                }

                HasHand = true;
                HandPosition = m_Smoothed;
                InteractionPoint.position = m_Smoothed;
            }
            else
            {
                HasHand = false;
                ActiveHand = TrackedHand.None;
                m_HasSmoothed = false;
                InteractionPoint.position = ParkedPosition;
            }
        }

        private bool TryResolveHand(out Vector3 position)
        {
            // 1) Gespiegelter Avatar (deckt sich mit dem, was der Spieler sieht).
            if (TryResolveFromAvatar(out position))
            {
                return true;
            }

            // 2) Fallback: rohe Kinect-Gelenke.
            if (m_Presence != null &&
                m_Presence.TryGetHandPositions(out var left, out var leftTracked, out var right, out var rightTracked))
            {
                position = ChooseHand(left, leftTracked, right, rightTracked);
                return true;
            }

            position = default;
            return false;
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

        private bool TryResolveFromAvatar(out Vector3 position)
        {
            position = default;

            if (!TryGetAvatarParts(out var parts))
            {
                return false;
            }

            bool leftTracked = parts.TryGetValue("HandLeft", out var leftGo) && leftGo != null;
            bool rightTracked = parts.TryGetValue("HandRight", out var rightGo) && rightGo != null;

            if (!leftTracked && !rightTracked)
            {
                return false;
            }

            var left = leftTracked ? leftGo.transform.position : Vector3.zero;
            var right = rightTracked ? rightGo.transform.position : Vector3.zero;
            position = ChooseHand(left, leftTracked, right, rightTracked);
            return true;
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
