using System.Collections.Generic;
using HTW.CAVE.Kinect;
using UnityEngine;
using Windows.Kinect;

/// <summary>
/// Erzeugt den gespiegelten Corpus-in-Speculo-Avatar direkt aus den verfolgten
/// <see cref="KinectActor"/>-Daten. Avatarvisualisierung, HUD-Vorschau und
/// Gameplay-Collider verwenden dadurch garantiert dieselbe Pose.
/// </summary>
public sealed class PositionTransferMultiple : MonoBehaviour
{
    private struct JointBinding
    {
        public readonly string name;
        public readonly JointType type;

        public JointBinding(string name, JointType type)
        {
            this.name = name;
            this.type = type;
        }
    }

    private struct BoneBinding
    {
        public readonly string name;
        public readonly JointType from;
        public readonly JointType to;

        public BoneBinding(string name, JointType from, JointType to)
        {
            this.name = name;
            this.from = from;
            this.to = to;
        }
    }

    private static readonly JointBinding[] JointBindings =
    {
        new JointBinding("Head", JointType.Head),
        new JointBinding("Neck", JointType.Neck),
        new JointBinding("SpineBase", JointType.SpineBase),
        new JointBinding("SpineMid", JointType.SpineMid),
        new JointBinding("HipLeft", JointType.HipLeft),
        new JointBinding("HipRight", JointType.HipRight),
        new JointBinding("KneeLeft", JointType.KneeLeft),
        new JointBinding("KneeRight", JointType.KneeRight),
        new JointBinding("AnkleLeft", JointType.AnkleLeft),
        new JointBinding("AnkleRight", JointType.AnkleRight),
        new JointBinding("FootLeft", JointType.FootLeft),
        new JointBinding("FootRight", JointType.FootRight),
        new JointBinding("ShoulderLeft", JointType.ShoulderLeft),
        new JointBinding("ShoulderRight", JointType.ShoulderRight),
        new JointBinding("ElbowLeft", JointType.ElbowLeft),
        new JointBinding("ElbowRight", JointType.ElbowRight),
        new JointBinding("HandLeft", JointType.HandLeft),
        new JointBinding("HandRight", JointType.HandRight)
    };

    private static readonly BoneBinding[] BoneBindings =
    {
        new BoneBinding("Line_Neck_Head", JointType.Neck, JointType.Head),
        new BoneBinding("Line_SpineMid_Neck", JointType.SpineMid, JointType.Neck),
        new BoneBinding("Line_SpineBase_SpineMid", JointType.SpineBase, JointType.SpineMid),
        new BoneBinding("Line_SpineMid_ShoulderLeft", JointType.SpineMid, JointType.ShoulderLeft),
        new BoneBinding("Line_SpineMid_ShoulderRight", JointType.SpineMid, JointType.ShoulderRight),
        new BoneBinding("Line_ShoulderLeft_ElbowLeft", JointType.ShoulderLeft, JointType.ElbowLeft),
        new BoneBinding("Line_ShoulderRight_ElbowRight", JointType.ShoulderRight, JointType.ElbowRight),
        new BoneBinding("Line_ElbowLeft_HandLeft", JointType.ElbowLeft, JointType.HandLeft),
        new BoneBinding("Line_ElbowRight_HandRight", JointType.ElbowRight, JointType.HandRight),
        new BoneBinding("Line_SpineBase_HipLeft", JointType.SpineBase, JointType.HipLeft),
        new BoneBinding("Line_SpineBase_HipRight", JointType.SpineBase, JointType.HipRight),
        new BoneBinding("Line_HipLeft_KneeLeft", JointType.HipLeft, JointType.KneeLeft),
        new BoneBinding("Line_HipRight_KneeRight", JointType.HipRight, JointType.KneeRight),
        new BoneBinding("Line_KneeLeft_AnkleLeft", JointType.KneeLeft, JointType.AnkleLeft),
        new BoneBinding("Line_KneeRight_AnkleRight", JointType.KneeRight, JointType.AnkleRight),
        new BoneBinding("Line_AnkleLeft_FootLeft", JointType.AnkleLeft, JointType.FootLeft),
        new BoneBinding("Line_AnkleRight_FootRight", JointType.AnkleRight, JointType.FootRight)
    };

    private readonly Dictionary<string, KinectActor> m_Actors = new Dictionary<string, KinectActor>();
    private readonly Dictionary<string, GameObject> m_AvatarRoots = new Dictionary<string, GameObject>();
    public readonly Dictionary<string, Dictionary<string, GameObject>> joints =
        new Dictionary<string, Dictionary<string, GameObject>>();
    private readonly Dictionary<string, Dictionary<string, LineRenderer>> m_JointLines =
        new Dictionary<string, Dictionary<string, LineRenderer>>();

    public Vector3 mirrorPlanePoint = Vector3.zero;
    public Vector3 mirrorNormal = Vector3.forward;
    public GameObject handPrefab;
    public GameObject headPrefab;
    public GameObject bodyPrefab;
    public ParticleSystem particlePrefab;
    public AudioClip collisionSound;

    [Tooltip("Blendet die rohe KinectActor-Prefab-Visualisierung aus, damit nur der gespiegelte Corpus sichtbar ist.")]
    public bool hideSourceActorRenderers = true;

    private KinectTracker m_Tracker;

    private void Awake()
    {
        ResolveTracker();
    }

    private void Update()
    {
        ResolveTracker();
        if (m_Tracker == null)
        {
            return;
        }

        var currentActorIds = new HashSet<string>();
        var trackedActors = m_Tracker.actors;

        for (int i = 0; i < trackedActors.Count; i++)
        {
            var actor = trackedActors[i];
            if (actor == null) continue;

            string actorId = actor.name;
            currentActorIds.Add(actorId);

            if (!m_Actors.ContainsKey(actorId))
            {
                RegisterActor(actorId, actor);
            }

            UpdateAvatar(actorId, actor);
        }

        var knownActorIds = new List<string>(m_Actors.Keys);
        foreach (string actorId in knownActorIds)
        {
            if (!currentActorIds.Contains(actorId))
            {
                RemoveActor(actorId);
            }
        }
    }

    private void ResolveTracker()
    {
        if (m_Tracker == null)
        {
            m_Tracker = FindObjectOfType<KinectTracker>(true);
        }
    }

    private void RegisterActor(string actorId, KinectActor actor)
    {
        m_Actors[actorId] = actor;
        joints[actorId] = new Dictionary<string, GameObject>();
        m_JointLines[actorId] = new Dictionary<string, LineRenderer>();

        var avatarRoot = new GameObject("Avatar_" + actorId);
        m_AvatarRoots[actorId] = avatarRoot;

        if (hideSourceActorRenderers)
        {
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }

        Debug.Log("[Corpus in Speculo] Kinect-Aktor übernommen: " + actorId);
    }

    private void RemoveActor(string actorId)
    {
        if (m_AvatarRoots.TryGetValue(actorId, out var root) && root != null)
        {
            Destroy(root);
        }

        m_Actors.Remove(actorId);
        m_AvatarRoots.Remove(actorId);
        joints.Remove(actorId);
        m_JointLines.Remove(actorId);
    }

    private void UpdateAvatar(string actorId, KinectActor actor)
    {
        foreach (var binding in JointBindings)
        {
            UpdateBodyPart(actorId, binding.name, actor.GetJoint(binding.type));
        }

        foreach (var bone in BoneBindings)
        {
            UpdateLine(actorId, bone.name, actor.GetJoint(bone.from), actor.GetJoint(bone.to));
        }
    }

    private void UpdateBodyPart(string actorId, string partName, KinectJoint joint)
    {
        var bodyParts = joints[actorId];
        if (joint.trackingState == TrackingState.NotTracked)
        {
            if (bodyParts.TryGetValue(partName, out var hidden) && hidden != null)
            {
                hidden.SetActive(false);
            }
            return;
        }

        if (!bodyParts.TryGetValue(partName, out var bodyPart) || bodyPart == null)
        {
            bodyPart = CreateBodyPart(partName);
            bodyPart.transform.SetParent(m_AvatarRoots[actorId].transform, true);
            bodyParts[partName] = bodyPart;
        }

        if (!bodyPart.activeSelf) bodyPart.SetActive(true);
        bodyPart.transform.position = MirrorJoint(joint.position);
    }

    private GameObject CreateBodyPart(string partName)
    {
        GameObject prefab = partName == "HandLeft" || partName == "HandRight"
            ? handPrefab
            : partName == "Head" ? headPrefab : bodyPrefab;

        GameObject bodyPart;
        if (prefab != null)
        {
            bodyPart = Instantiate(prefab);
        }
        else
        {
            bodyPart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bodyPart.transform.localScale = Vector3.one * 0.12f;
            Debug.LogWarning("[Corpus in Speculo] Prefab für " + partName + " fehlt; nutze Kugel-Fallback.");
        }

        bodyPart.name = partName;

        bool hasUsableCollider = false;
        foreach (var collider in bodyPart.GetComponents<Collider>())
        {
            if (collider is MeshCollider meshCollider && meshCollider.sharedMesh == null)
            {
                meshCollider.enabled = false;
                continue;
            }

            collider.isTrigger = false;
            hasUsableCollider |= collider.enabled;
        }

        if (!hasUsableCollider)
        {
            bodyPart.AddComponent<SphereCollider>().radius = 0.5f;
        }

        var rigidbody = bodyPart.GetComponent<Rigidbody>();
        if (rigidbody == null) rigidbody = bodyPart.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.detectCollisions = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (bodyPart.GetComponent<CaveGame.PlayerBodyPart>() == null)
        {
            bodyPart.AddComponent<CaveGame.PlayerBodyPart>();
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) bodyPart.layer = playerLayer;

        var bodyCollision = bodyPart.GetComponent<BodyCollision>();
        if (bodyCollision == null) bodyCollision = bodyPart.AddComponent<BodyCollision>();
        if (particlePrefab != null)
        {
            bodyCollision.SetParticleEffect(particlePrefab);
            bodyCollision.SetCollisionSound(collisionSound);
        }

        return bodyPart;
    }

    private void UpdateLine(string actorId, string lineName, KinectJoint from, KinectJoint to)
    {
        var lines = m_JointLines[actorId];
        if (!lines.TryGetValue(lineName, out var line) || line == null)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(m_AvatarRoots[actorId].transform, false);
            line = lineObject.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.75f, 0.75f, 0.75f);
            line.endColor = line.startColor;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            lines[lineName] = line;
        }

        bool tracked = from.trackingState != TrackingState.NotTracked &&
                       to.trackingState != TrackingState.NotTracked;
        line.enabled = tracked;
        if (!tracked) return;

        line.SetPosition(0, MirrorJoint(from.position));
        line.SetPosition(1, MirrorJoint(to.position));
    }

    private Vector3 MirrorJoint(Vector3 point)
    {
        Vector3 normal = mirrorNormal.sqrMagnitude > 0.0001f
            ? mirrorNormal.normalized
            : Vector3.forward;
        float projection = Vector3.Dot(point - mirrorPlanePoint, normal);
        return point - 2f * projection * normal;
    }
}
