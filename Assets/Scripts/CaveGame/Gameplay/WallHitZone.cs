using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Liegt zusammen mit den generierten Wand-Triggern auf der Wand. Berührt ein
    /// Avatar-Körperteil (Layer "Player") einen massiven Teil der Wand, wird ein
    /// Treffer an die <see cref="Wall"/> gemeldet.
    /// </summary>
    [RequireComponent(typeof(Wall))]
    public sealed class WallHitZone : MonoBehaviour
    {
        [Tooltip("Welche Layer zählen als Spieler-Körperteile? Leer = automatisch der Layer 'Player'.")]
        public LayerMask playerLayers;

        private Wall m_Wall;
        private int m_PlayerLayer = -1;

        private void Awake()
        {
            m_Wall = GetComponent<Wall>();
            m_PlayerLayer = LayerMask.NameToLayer("Player");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayerPart(other.gameObject))
            {
                m_Wall.RegisterHit();
            }
        }

        // Auch OnTriggerStay, falls ein Körperteil bereits beim Eintreten exakt in der
        // Öffnung war und sich danach in die Wand bewegt.
        private void OnTriggerStay(Collider other)
        {
            if (!m_Wall.WasHit && IsPlayerPart(other.gameObject))
            {
                m_Wall.RegisterHit();
            }
        }

        private bool IsPlayerPart(GameObject go)
        {
            // Zuverlässigste Erkennung: Marker-Komponente am Körperteil.
            if (go.GetComponent<PlayerBodyPart>() != null)
            {
                return true;
            }

            if (playerLayers.value != 0)
            {
                return (playerLayers.value & (1 << go.layer)) != 0;
            }

            return m_PlayerLayer >= 0 && go.layer == m_PlayerLayer;
        }
    }
}
