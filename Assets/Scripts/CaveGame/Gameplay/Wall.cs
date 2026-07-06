using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Eine einzelne Wand-Instanz. Verwaltet ihren Zustand:
    ///   • Pending  – noch nicht an der Spielerebene vorbei.
    ///   • Hit      – ein Körperteil hat die Wand berührt (Leben verloren).
    ///   • Resolved – Wand ist am Spieler vorbei; bei "kein Treffer" gab es Punkte.
    ///
    /// Treffer werden von <see cref="WallHitZone"/> gemeldet, das Auflösen vom
    /// <see cref="WallMover"/>, sobald die Wand die Spielerebene passiert hat.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Wall : MonoBehaviour
    {
        public enum WallStatus { Pending, Resolved }

        [Tooltip("Unverwundbarkeit nach Treffer ist hier irrelevant, da jede Wand nur 1x Schaden macht.")]
        public Color hitFlashColor = new Color(1f, 0.25f, 0.2f);

        [Header("Audio Einstellungen")]
        [Tooltip("Der Sound, der abgespielt wird, wenn der Spieler die Wand berührt.")]
        public AudioClip errorSound;
        [Tooltip("Die AudioSource, über die der Sound abgespielt wird. Kann leer bleiben, wenn eine AudioSource auf diesem Objekt liegt.")]
        public AudioSource globalAudioSource;

        public WallStatus Status { get; private set; } = WallStatus.Pending;
        public bool WasHit { get; private set; }

        private SpriteRenderer m_Renderer;
        private Color m_BaseColor;

        private void Awake()
        {
            m_Renderer = GetComponent<SpriteRenderer>();
            m_BaseColor = m_Renderer.color;

            // Falls keine globale AudioSource zugewiesen wurde, schauen wir, ob dieses Objekt selbst eine hat
            if (globalAudioSource == null)
            {
                globalAudioSource = GetComponent<AudioSource>();
            }
        }

        /// <summary>Setzt die Wand für eine Wiederverwendung zurück (Pooling-freundlich).</summary>
        public void ResetState()
        {
            Status = WallStatus.Pending;
            WasHit = false;
            if (m_Renderer != null)
            {
                m_Renderer.color = m_BaseColor;
            }
        }

        /// <summary>Ein Körperteil hat die Wand berührt → genau einmal ein Leben abziehen und Sound abspielen.</summary>
        public void RegisterHit()
        {
            if (Status != WallStatus.Pending || WasHit)
            {
                return;
            }

            WasHit = true;

            if (m_Renderer != null)
            {
                m_Renderer.color = hitFlashColor;
            }

            // --- HIER WIRD DER SOUND ABGESPIELT ---
            if (globalAudioSource != null && errorSound != null)
            {
                // PlayOneShot spielt den Sound einmalig ab, ohne laufende Musik zu unterbrechen
                globalAudioSource.PlayOneShot(errorSound);
            }
            // --------------------------------------

            if (GameManager.Instance != null)
            {
                // Hier fügen wir vorsichtshalber einen Log-Eintrag hinzu, um zu sehen, ob es klappt
                Debug.Log("[Wall] Fehler registriert! Sound wird abgespielt.");
                GameManager.Instance.LoseLife();
            }
        }

        /// <summary>Wird vom Mover aufgerufen, wenn die Wand den Spieler passiert hat.</summary>
        public void Resolve()
        {
            if (Status != WallStatus.Pending)
            {
                return;
            }

            Status = WallStatus.Resolved;

            // Ohne Treffer erfolgreich durchquert → Punkte.
            if (!WasHit && GameManager.Instance != null)
            {
                GameManager.Instance.AddWallScore();
            }
        }
    }
}