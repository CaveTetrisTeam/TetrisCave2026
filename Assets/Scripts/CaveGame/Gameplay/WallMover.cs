using System;
using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Bewegt eine Wand kontinuierlich Richtung Spieler (entlang -Z, wie der
    /// vorhandene <c>BlockMover</c>). Sobald die Wand die Spielerebene passiert hat,
    /// wird sie aufgelöst (Punkte bei keinem Treffer) und anschließend freigegeben.
    /// </summary>
    [RequireComponent(typeof(Wall))]
    public sealed class WallMover : MonoBehaviour
    {
        private float m_Speed;
        private float m_PlayerPlaneZ;
        private float m_DespawnZ;
        private float m_ResolveMargin;
        private Action<GameObject> m_Release;

        private Wall m_Wall;
        private bool m_Active;
        private bool m_Frozen;

        private void Awake()
        {
            m_Wall = GetComponent<Wall>();
        }

        public void Launch(float speed, float playerPlaneZ, float despawnZ,
                           float resolveMargin, Action<GameObject> releaseCallback)
        {
            m_Speed = speed;
            m_PlayerPlaneZ = playerPlaneZ;
            m_DespawnZ = despawnZ;
            m_ResolveMargin = resolveMargin;
            m_Release = releaseCallback;

            m_Active = true;
            m_Frozen = false;
        }

        /// <summary>Stoppt die Bewegung (Game Over: "Wandbewegung stoppen").</summary>
        public void Freeze()
        {
            m_Frozen = true;
        }

        private void Update()
        {
            if (!m_Active || m_Frozen)
            {
                return;
            }

            transform.position += Vector3.back * (m_Speed * Time.deltaTime);

            // Hat die Wand den Spieler passiert? -> auflösen (Resolve ist idempotent).
            if (transform.position.z <= m_PlayerPlaneZ - m_ResolveMargin)
            {
                m_Wall.Resolve();
            }

            if (transform.position.z < m_DespawnZ)
            {
                m_Active = false;
                m_Release?.Invoke(gameObject);
            }
        }
    }
}
