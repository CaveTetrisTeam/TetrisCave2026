using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CaveGame
{
    /// <summary>
    /// Zeichnet das tatsächlich von PositionTransferMultiple erzeugte Corpus-in-
    /// Speculo-Skelett als kompakte 2D-Vorschau. Es wird keine zweite Tracking-Pose
    /// berechnet: Vorschau und Kollisionsavatar verwenden dieselben Gelenke.
    /// </summary>
    public sealed class SkeletonPreviewGraphic : MaskableGraphic
    {
        private static readonly string[] JointNames =
        {
            "Head", "Neck", "SpineMid", "SpineBase",
            "ShoulderLeft", "ElbowLeft", "HandLeft",
            "ShoulderRight", "ElbowRight", "HandRight",
            "HipLeft", "KneeLeft", "AnkleLeft", "FootLeft",
            "HipRight", "KneeRight", "AnkleRight", "FootRight"
        };

        private static readonly string[,] Bones =
        {
            { "Head", "Neck" },
            { "Neck", "SpineMid" },
            { "SpineMid", "SpineBase" },
            { "SpineMid", "ShoulderLeft" },
            { "ShoulderLeft", "ElbowLeft" },
            { "ElbowLeft", "HandLeft" },
            { "SpineMid", "ShoulderRight" },
            { "ShoulderRight", "ElbowRight" },
            { "ElbowRight", "HandRight" },
            { "SpineBase", "HipLeft" },
            { "HipLeft", "KneeLeft" },
            { "KneeLeft", "AnkleLeft" },
            { "AnkleLeft", "FootLeft" },
            { "SpineBase", "HipRight" },
            { "HipRight", "KneeRight" },
            { "KneeRight", "AnkleRight" },
            { "AnkleRight", "FootRight" }
        };

        [SerializeField] private Color boneColor = new Color(0.20f, 0.88f, 1f, 0.95f);
        [SerializeField] private Color jointColor = new Color(0.94f, 1f, 0.58f, 1f);
        [SerializeField] private float boneWidth = 5f;
        [SerializeField] private float jointRadius = 5f;

        private readonly Dictionary<string, Vector2> m_Points =
            new Dictionary<string, Vector2>();

        private PositionTransferMultiple m_Tracker;
        private KinectPlayerPresence m_Presence;
        private Camera m_FrontCamera;
        private Dictionary<string, GameObject> m_CurrentParts;

        public bool HasPose { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            ResolveDependencies();
        }

        private void LateUpdate()
        {
            ResolveDependencies();
            m_CurrentParts = ResolveTrackedParts();
            HasPose = CountAvailableJoints(m_CurrentParts) >= 8;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            m_Points.Clear();

            if (!HasPose || m_CurrentParts == null)
            {
                return;
            }

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            foreach (string jointName in JointNames)
            {
                if (!m_CurrentParts.TryGetValue(jointName, out var joint) || joint == null)
                {
                    continue;
                }

                Vector2 point = ProjectToFront(joint.transform.position);
                m_Points[jointName] = point;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            Vector2 poseSize = max - min;
            if (poseSize.x < 0.01f || poseSize.y < 0.01f)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            float padding = 20f;
            float scale = Mathf.Min(
                (rect.width - padding * 2f) / poseSize.x,
                (rect.height - padding * 2f) / poseSize.y);
            Vector2 poseCenter = (min + max) * 0.5f;

            var fitted = new Dictionary<string, Vector2>(m_Points.Count);
            foreach (var pair in m_Points)
            {
                fitted[pair.Key] = (pair.Value - poseCenter) * scale + rect.center;
            }

            for (int i = 0; i < Bones.GetLength(0); i++)
            {
                if (fitted.TryGetValue(Bones[i, 0], out var a) &&
                    fitted.TryGetValue(Bones[i, 1], out var b))
                {
                    AddLine(vh, a, b, boneWidth, boneColor);
                }
            }

            foreach (var pair in fitted)
            {
                float radius = pair.Key == "Head" ? jointRadius * 1.65f : jointRadius;
                AddDisc(vh, pair.Value, radius, jointColor);
            }
        }

        private Vector2 ProjectToFront(Vector3 worldPosition)
        {
            if (m_FrontCamera == null)
            {
                return new Vector2(worldPosition.x, worldPosition.y);
            }

            Vector3 relative = worldPosition - m_FrontCamera.transform.position;
            return new Vector2(
                Vector3.Dot(relative, m_FrontCamera.transform.right),
                Vector3.Dot(relative, m_FrontCamera.transform.up));
        }

        private Dictionary<string, GameObject> ResolveTrackedParts()
        {
            if (m_Tracker == null || m_Tracker.joints == null || m_Tracker.joints.Count == 0)
            {
                return null;
            }

            string primaryName = m_Presence != null && m_Presence.PrimaryActor != null
                ? m_Presence.PrimaryActor.name
                : null;

            if (!string.IsNullOrEmpty(primaryName) &&
                m_Tracker.joints.TryGetValue(primaryName, out var primary))
            {
                return primary;
            }

            foreach (var actor in m_Tracker.joints)
            {
                return actor.Value;
            }

            return null;
        }

        private static int CountAvailableJoints(Dictionary<string, GameObject> parts)
        {
            if (parts == null) return 0;

            int count = 0;
            foreach (string jointName in JointNames)
            {
                if (parts.TryGetValue(jointName, out var joint) && joint != null) count++;
            }
            return count;
        }

        private void ResolveDependencies()
        {
            if (m_Tracker == null) m_Tracker = FindObjectOfType<PositionTransferMultiple>(true);
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

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.001f) return;

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int index = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private static void AddDisc(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            const int segments = 10;
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.one * 0.5f);

            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * radius, color, (direction + Vector2.one) * 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }
    }
}
