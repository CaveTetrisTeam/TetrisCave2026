using System;
using UnityEngine;
using HTW.CAVE.Kinect;
using Windows.Kinect;

namespace CaveGame
{
    /// <summary>
    /// Kapselt die "Ist eine Person zuverlässig getrackt?"-Logik über dem CAVE-
    /// <see cref="KinectTracker"/>. Liefert den primären Aktor und Gelenk-/Handpositionen
    /// und meldet per Event, wenn ein Spieler erkannt oder verloren wird.
    ///
    /// "Zuverlässig" bedeutet: derselbe Aktor wird seit mindestens
    /// <see cref="stabilizeSeconds"/> durchgehend verfolgt, seine Kerngelenke
    /// (Becken + Kopf) werden getrackt und seine geschätzte Körpergröße liegt über
    /// <see cref="minHeight"/>.
    ///
    /// Wichtig: Die Stabilisierung nutzt <see cref="Time.unscaledTime"/>, damit sie
    /// auch auf dem pausierten Startbildschirm (timeScale = 0) funktioniert – nur so
    /// kann der physische Knopf dort überhaupt aktiv werden.
    /// </summary>
    public sealed class KinectPlayerPresence : MonoBehaviour
    {
        [Tooltip("Mindestzeit (Sek.), die derselbe Aktor durchgehend verfolgt werden muss.")]
        public float stabilizeSeconds = 1.0f;
        [Tooltip("Mindest-Körpergröße (Meter), um Fehl-Tracking (z. B. Stühle) auszuschließen.")]
        public float minHeight = 0.8f;

        /// <summary>Wird ausgelöst, sobald erstmals zuverlässig getrackt wird.</summary>
        public event Action OnPlayerAcquired;
        /// <summary>Wird ausgelöst, sobald die zuverlässige Person verloren geht.</summary>
        public event Action OnPlayerLost;

        public bool HasReliablePlayer { get; private set; }
        public KinectActor PrimaryActor { get; private set; }

        private KinectTracker m_Tracker;
        private KinectActor m_Candidate;
        private float m_CandidateSinceUnscaled;

        private void Awake()
        {
            m_Tracker = FindObjectOfType<KinectTracker>(true);
            if (m_Tracker == null)
            {
                Debug.LogWarning("[KinectPlayerPresence] Kein KinectTracker in der Szene gefunden – " +
                                 "Tracking-Gate ist inaktiv. (CAVE-Rig fehlt? Test per Tastatur-Fallback möglich.)");
            }
        }

        private void Update()
        {
            var previous = HasReliablePlayer;

            // Identität des Kandidaten verfolgen; bei Wechsel den Stabilisierungs-Timer zurücksetzen.
            var best = SelectPrimaryActor();
            if (best != m_Candidate)
            {
                m_Candidate = best;
                m_CandidateSinceUnscaled = Time.unscaledTime;
            }

            PrimaryActor = m_Candidate;

            bool stabilized = m_Candidate != null &&
                              Time.unscaledTime - m_CandidateSinceUnscaled >= stabilizeSeconds;
            HasReliablePlayer = stabilized && MeetsCriteria(m_Candidate);

            if (HasReliablePlayer && !previous)
            {
                OnPlayerAcquired?.Invoke();
            }
            else if (!HasReliablePlayer && previous)
            {
                OnPlayerLost?.Invoke();
            }
        }

        /// <summary>
        /// Wählt den am längsten verfolgten vorhandenen Aktor (stabilster Kandidat).
        /// Die endgültige Zuverlässigkeit prüft <see cref="MeetsCriteria"/>.
        /// </summary>
        private KinectActor SelectPrimaryActor()
        {
            if (m_Tracker == null)
            {
                return null;
            }

            KinectActor best = null;
            var bestCreatedAt = float.MaxValue;

            var actors = m_Tracker.actors;
            for (int i = 0; i < actors.Count; ++i)
            {
                var actor = actors[i];
                if (actor != null && actor.createdAt < bestCreatedAt)
                {
                    best = actor;
                    bestCreatedAt = actor.createdAt;
                }
            }

            return best;
        }

        private bool MeetsCriteria(KinectActor actor)
        {
            if (actor == null || actor.height < minHeight)
            {
                return false;
            }

            return IsTracked(actor, JointType.SpineBase) && IsTracked(actor, JointType.Head);
        }

        private static bool IsTracked(KinectActor actor, JointType type)
        {
            return actor.GetJoint(type).trackingState != TrackingState.NotTracked;
        }

        /// <summary>Weltposition eines Gelenks des primären Aktors (roh, ohne Spiegelung).</summary>
        public bool TryGetJointPosition(JointType type, out Vector3 position)
        {
            if (PrimaryActor != null)
            {
                var joint = PrimaryActor.GetJoint(type);
                if (joint.trackingState != TrackingState.NotTracked)
                {
                    position = joint.position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Liefert beide Handpositionen (roh) des primären Aktors, je mit Tracking-Flag.
        /// </summary>
        public bool TryGetHandPositions(out Vector3 left, out bool leftTracked,
                                        out Vector3 right, out bool rightTracked)
        {
            left = right = default;
            leftTracked = rightTracked = false;

            if (PrimaryActor == null)
            {
                return false;
            }

            var l = PrimaryActor.GetJoint(JointType.HandLeft);
            var r = PrimaryActor.GetJoint(JointType.HandRight);
            left = l.position;
            right = r.position;
            leftTracked = l.trackingState != TrackingState.NotTracked;
            rightTracked = r.trackingState != TrackingState.NotTracked;

            return leftTracked || rightTracked;
        }
    }
}
