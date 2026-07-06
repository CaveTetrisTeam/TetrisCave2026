using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Whisper.Utils;

namespace CaveGame
{
    /// <summary>
    /// Sprachsteuerung für das Start-/Game-Over-Podest. Hört automatisch zu, solange
    /// das Podest sichtbar ist (Zustände <see cref="GameState.MainMenu"/> und
    /// <see cref="GameState.GameOver"/>), und löst per Sprachbefehl dieselben Aktionen
    /// aus wie der Hand-Knopf:
    ///   • MainMenu: "Start" / "Los" / "Spiel" …            → Spiel starten
    ///   • GameOver: "Neustart" / "Nochmal" …               → Neustart
    ///               "Menü" / "Zurück" / "Startbildschirm" … → Hauptmenü
    ///
    /// Nutzt den vorhandenen Echo-Motion-STT (<see cref="EchoMotionSpeechToText"/>) mit
    /// VAD-Fenstern: Es wird nur transkribiert, wenn jemand spricht; nach dem Befehl
    /// wird automatisch neu „scharf gestellt", solange das Podest sichtbar bleibt.
    /// Außerhalb der Podest-Zustände ist das Mikrofon aus (keine Fehlauslösung).
    /// </summary>
    public sealed class PodestVoiceControl : MonoBehaviour
    {
        [Header("Sprachbefehle (Teilwort-Treffer, Groß/Klein & Satzzeichen egal)")]
        public string[] startKeywords =
        {
            "start", "starten", "los", "los gehts", "spiel", "spielen",
            "beginne", "beginnen", "anfangen", "weiter", "go"
        };
        public string[] restartKeywords =
        {
            "neustart", "neu starten", "neustarten", "nochmal", "noch mal",
            "nochmals", "noch einmal", "wiederhol", "wiederholen", "restart"
        };
        public string[] menuKeywords =
        {
            "menü", "menu", "menue", "hauptmenü", "hauptmenu", "hauptmenue",
            "zurück", "zuruck", "startbildschirm", "beenden", "ende"
        };

        [Header("VAD")]
        [Tooltip("Stille (Sek.) nach dem Sprechen, bis der Befehl ausgewertet wird.")]
        public float vadStopTime = 1.2f;

        private EchoMotionSpeechToText m_Stt;
        private MicrophoneRecord m_Mic;
        private PhysicalStartPodestController m_Podest;
        private bool m_Listening;
        private bool m_Subscribed;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnEnable()
        {
            GameManager.StateChanged += HandleStateChanged;
            SubscribeStt();
        }

        private void OnDisable()
        {
            GameManager.StateChanged -= HandleStateChanged;
            UnsubscribeStt();
            StopListening();
        }

        private void Start()
        {
            ResolveDependencies();
            SubscribeStt();
            var manager = GameManager.Instance;
            HandleStateChanged(manager != null ? manager.CurrentState : GameState.MainMenu);
        }

        private void ResolveDependencies()
        {
            if (m_Stt == null) m_Stt = FindObjectOfType<EchoMotionSpeechToText>(true);
            if (m_Stt != null && m_Mic == null) m_Mic = m_Stt.microphoneRecord;
            if (m_Podest == null) m_Podest = FindObjectOfType<PhysicalStartPodestController>(true);
        }

        private void SubscribeStt()
        {
            if (!m_Subscribed && m_Stt != null)
            {
                m_Stt.OnTranscriptionReady += HandleTranscription;
                m_Subscribed = true;
            }
        }

        private void UnsubscribeStt()
        {
            if (m_Subscribed && m_Stt != null)
            {
                m_Stt.OnTranscriptionReady -= HandleTranscription;
                m_Subscribed = false;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            bool podestVisible = state == GameState.MainMenu || state == GameState.GameOver;
            if (podestVisible)
            {
                StartListening();
            }
            else
            {
                StopListening();
            }
        }

        private void Update()
        {
            // Mikrofon läuft, solange zugehört wird und das Modell geladen ist.
            // (Robust gegen Lade-Verzögerung, VAD-Stops und Transkriptionsfehler.)
            if (m_Listening && m_Stt != null && m_Mic != null && !m_Mic.IsRecording && ModelReady())
            {
                m_Stt.StartRecording();
            }
        }

        private bool ModelReady()
        {
            return m_Stt != null && m_Stt.whisper != null && m_Stt.whisper.IsLoaded;
        }

        private void StartListening()
        {
            ResolveDependencies();
            if (m_Stt == null || m_Mic == null)
            {
                return; // kein STT in der Szene -> Sprachsteuerung inaktiv (Hand/Tastatur bleibt)
            }

            ConfigureVad();
            m_Listening = true;
            // Aufnahme startet die Update-Schleife, sobald das Modell geladen ist.
        }

        private void StopListening()
        {
            m_Listening = false;
            if (m_Stt != null)
            {
                m_Stt.StopRecording();
            }
        }

        private void ConfigureVad()
        {
            m_Mic.useVad = true;
            m_Mic.vadStop = true;
            m_Mic.dropVadPart = true;
            m_Mic.vadStopTime = vadStopTime;
        }

        private void HandleTranscription(string text)
        {
            if (!m_Listening)
            {
                return;
            }

            var normalized = Normalize(text);
            if (!string.IsNullOrEmpty(normalized) && m_Podest != null)
            {
                var manager = GameManager.Instance;
                var state = manager != null ? manager.CurrentState : GameState.MainMenu;

                if (state == GameState.MainMenu)
                {
                    if (Matches(normalized, startKeywords))
                    {
                        m_Podest.TryTriggerByVoice(PhysicalStartPodestController.PodestAction.Start);
                    }
                }
                else if (state == GameState.GameOver)
                {
                    // Neustart zuerst prüfen ("neustart" enthält "start").
                    if (Matches(normalized, restartKeywords))
                    {
                        m_Podest.TryTriggerByVoice(PhysicalStartPodestController.PodestAction.Restart);
                    }
                    else if (Matches(normalized, menuKeywords))
                    {
                        m_Podest.TryTriggerByVoice(PhysicalStartPodestController.PodestAction.Menu);
                    }
                }
            }

            // Erneutes Zuhören übernimmt die Update-Schleife (solange m_Listening).
        }

        private static bool Matches(string normalizedText, string[] keywords)
        {
            if (keywords == null)
            {
                return false;
            }

            foreach (var keyword in keywords)
            {
                var needle = Normalize(keyword);
                if (needle.Length > 0 && normalizedText.Contains(needle))
                {
                    return true;
                }
            }

            return false;
        }

        // Kleinschreibung, Satzzeichen entfernen, Mehrfach-Leerzeichen zusammenfassen.
        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(s.Length);
            foreach (var c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (char.IsWhiteSpace(c)) sb.Append(' ');
            }

            return Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
        }
    }
}
