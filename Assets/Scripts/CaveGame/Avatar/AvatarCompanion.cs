using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using HumanTetris;

namespace CaveGame
{
    /// <summary>
    /// Der Begleiter-Avatar des Spiels (Blender-Modell <c>tetris01.fbx</c> aus
    /// <c>Resources/Avatar</c>). Er schwebt in der Umgebung, erklärt beim ersten
    /// Menübesuch per Sprechblasen das Spielprinzip, schwirrt während der Runde
    /// durch die Szene und kommentiert Erfolge, Treffer und das Spielende.
    ///
    /// Wird vom <see cref="GameBootstrapper"/> automatisch erzeugt und hängt sich
    /// an die statischen Events des <see cref="GameManager"/> – es muss nichts
    /// manuell in die Szene gelegt werden. Alle Positionen/Texte sind im
    /// Inspector anpassbar.
    /// </summary>
    public sealed class AvatarCompanion : MonoBehaviour
    {
        [Header("Modell")]
        [Tooltip("Resources-Pfad des Avatar-Modells (ohne Endung).")]
        public string modelResourcePath = "Avatar/tetris01";
        [Tooltip("Zielhöhe des Modells in Metern (Importgröße wird automatisch normalisiert).")]
        public float modelHeight = 0.9f;
        [Tooltip("Zusätzliche Y-Drehung, falls das Modell nicht zum Spieler schaut.")]
        public float modelYawOffset = 0f;
        [Tooltip("Diese FBX-Kindobjekte werden ausgeblendet: die im Modell eingebaute " +
                 "Sprechblase ('Text') sowie Blender-Kamera/-Licht.")]
        public string[] hiddenChildNames = { "Text", "Camera", "Light" };
        [Tooltip("Nur Animations-Clips abspielen, deren Name diesen Teil enthält (leer = alle).")]
        public string animationClipFilter = "";

        [Header("Sprechblase")]
        [Tooltip("Position der Sprechblase relativ zum Avatar.")]
        public Vector3 bubbleOffset = new Vector3(0f, 0.7f, 0f);

        [Header("Ankerpunkte (Weltkoordinaten, Spieler steht im Ursprung)")]
        [Tooltip("Schwebeposition im Menü und bei der Erklärung.")]
        public Vector3 menuAnchor = new Vector3(1.0f, 1.2f, 2.6f);
        [Tooltip("Schwebeposition beim Game-Over-Kommentar.")]
        public Vector3 gameOverAnchor = new Vector3(-1.0f, 1.2f, 2.6f);
        [Tooltip("Mittelpunkt des Bereichs, in dem der Avatar während des Spiels umherschwirrt.")]
        public Vector3 wanderCenter = new Vector3(0f, 2.4f, 4.0f);
        [Tooltip("Halbe Ausdehnung des Schwirr-Bereichs.")]
        public Vector3 wanderExtents = new Vector3(2.4f, 0.7f, 1.5f);
        [Tooltip("Min/Max Sekunden, bis der Avatar sich ein neues Flugziel sucht.")]
        public Vector2 wanderIntervalRange = new Vector2(3.5f, 7f);

        [Header("Feedback-Verhalten")]
        [Tooltip("Mindestabstand zwischen zwei Lob-Blasen (Sekunden).")]
        public float praiseCooldown = 2f;
        [Tooltip("Ab so vielen Wänden in Folge gibt es besondere Serien-Sprüche.")]
        public int streakThreshold = 3;
        [Tooltip("Erinnerungs-Intervall im Menü, wenn gerade keine Blase aktiv ist (Sekunden).")]
        public float menuReminderInterval = 30f;

        [Header("Texte: Erklärung (einmal beim ersten Menübesuch)")]
        [TextArea]
        public string[] tutorialLines =
        {
            "Hallo! Ich bin Blocky – dein Begleiter hier im Tetris-Cave!",
            "Gleich fliegen Wände auf dich zu. Jede Wand hat eine Öffnung in Form einer Pose.",
            "Stell dich genau so hin wie die Öffnung – dann passt du hindurch!",
            "Berührst du die Wand, verlierst du eines deiner drei Leben.",
            "Jede geschaffte Wand bringt dir 100 Punkte.",
            "Drück den Knopf auf dem Podest, sobald du bereit bist!"
        };

        [Header("Texte: Menü / Start")]
        public string menuReturnLine = "Zurück im Menü! Noch eine Runde? Drück den Knopf auf dem Podest!";
        public string menuReminderLine = "Drück den Knopf auf dem Podest, um zu starten!";
        public string trackingLine = "Super! Stell dich in die Mitte der Fläche, damit ich dich sehen kann.";
        public string[] startLines =
        {
            "Los geht's – da kommt die erste Wand!",
            "Auf geht's! Zeig mir deine besten Posen!"
        };

        [Header("Texte: Feedback in der Runde")]
        public string[] praiseLines =
        {
            "Perfekt! +100 Punkte!",
            "Sauber durch die Wand!",
            "Klasse Pose!",
            "Das sah richtig gut aus!",
            "Weiter so!"
        };
        public string[] streakLines =
        {
            "Was für eine Serie – du bist nicht zu stoppen!",
            "Wahnsinn, du bist ein Naturtalent!"
        };
        public string[] hitLines =
        {
            "Autsch! Die Wand war schneller.",
            "Ups, das war knapp daneben.",
            "Kopf hoch – die nächste schaffst du!"
        };
        public string lastLifeLine = "Vorsicht – das ist dein letztes Leben!";

        [Header("Texte: Spielende")]
        [Tooltip("{0} = erreichte Punkte.")]
        public string newRecordFormat = "WOW! Neuer Rekord: {0} Punkte!";
        [Tooltip("{0} = erreichte Punkte, {1} = Rekord.")]
        public string gameOverScoreFormat = "Geschafft! {0} Punkte – dein Rekord: {1}.";
        public string gameOverZeroLine = "Game Over! Beim nächsten Mal klappt's bestimmt.";
        public string gameOverHintLine = "GRÜNER Knopf = Neustart, BLAUER Knopf = Menü.";

        private AvatarHoverMovement m_Movement;
        private AvatarSpeechBubble m_Bubble;
        private GameObject m_Model;

        private PlayableGraph m_Graph;
        private readonly List<AnimationClipPlayable> m_ClipPlayables = new List<AnimationClipPlayable>();

        private GameState m_HandledState = (GameState)(-1);
        private Coroutine m_ReminderRoutine;
        private bool m_TutorialShown;
        private int m_LastScore;
        private int m_LastLives;
        private int m_Streak;
        private int m_BestAtRoundStart;
        private float m_LastPraiseTime = float.NegativeInfinity;
        private int m_LastRandomIndex = -1;

        // ---------------------------------------------------------------------
        // Aufbau
        // ---------------------------------------------------------------------

        private void Awake()
        {
            m_Movement = gameObject.AddComponent<AvatarHoverMovement>();
            m_Movement.modelYawOffset = modelYawOffset;

            LoadModel();
            SetupAnimation();
            SetupBubble();

            // Startaufstellung: direkt am Menü-Anker schweben.
            m_Movement.Teleport(menuAnchor);
        }

        private void LoadModel()
        {
            var prefab = Resources.Load<GameObject>(modelResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[AvatarCompanion] Avatar-Modell nicht gefunden: Resources/" +
                                 modelResourcePath + " – der Begleiter bleibt unsichtbar, " +
                                 "Sprechblasen funktionieren trotzdem.");
                return;
            }

            m_Model = Instantiate(prefab, transform);
            m_Model.name = "Avatar Modell";

            // Eingebaute Sprechblase ('Text') und Blender-Kamera/-Licht ausblenden –
            // die dynamische Sprechblase übernimmt.
            foreach (var child in m_Model.GetComponentsInChildren<Transform>(true))
            {
                foreach (var hidden in hiddenChildNames)
                {
                    if (child.name == hidden || child.name.StartsWith(hidden + "."))
                    {
                        child.gameObject.SetActive(false);
                        break;
                    }
                }
            }

            // Sicherheitsnetz: keine Collider aus dem Import übernehmen, damit der
            // Avatar niemals Wand-Trigger auslöst; ebenso keine Kameras/Lichter.
            foreach (var collider in m_Model.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }
            foreach (var camera in m_Model.GetComponentsInChildren<Camera>(true))
            {
                Destroy(camera);
            }
            foreach (var light in m_Model.GetComponentsInChildren<Light>(true))
            {
                Destroy(light);
            }

            // Größe auf modelHeight normalisieren und Modell um den Wurzelpunkt zentrieren
            // (macht die Ankerpunkte unabhängig von Blender-Einheiten/-Pivots).
            var bounds = CalculateBounds(m_Model);
            if (bounds.size.y > 0.001f)
            {
                m_Model.transform.localScale *= modelHeight / bounds.size.y;
            }
            bounds = CalculateBounds(m_Model);
            m_Model.transform.position -= bounds.center - transform.position;

            m_Movement.bobTarget = m_Model.transform;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        /// <summary>
        /// Spielt die importierten FBX-Clips (geloopte Sprech-Animation) über die
        /// Playables-API ab – so braucht es kein AnimatorController-Asset im Projekt.
        /// </summary>
        private void SetupAnimation()
        {
            if (m_Model == null)
            {
                return;
            }

            var clips = new List<AnimationClip>();
            foreach (var clip in Resources.LoadAll<AnimationClip>(modelResourcePath))
            {
                if (clip == null || clip.name.StartsWith("__preview__"))
                {
                    continue; // Editor-interne Vorschau-Clips überspringen
                }
                if (clip.empty)
                {
                    continue; // Clips ohne Kurven (z. B. 'Body|CubeAction' mit 0 Frames)
                }
                if (!string.IsNullOrEmpty(animationClipFilter) &&
                    clip.name.IndexOf(animationClipFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning("[AvatarCompanion] Keine Animations-Clips in Resources/" +
                                 modelResourcePath + " gefunden – der Avatar bleibt starr.");
                return;
            }

            var animator = m_Model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = m_Model.AddComponent<Animator>();
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            m_Graph = PlayableGraph.Create("Avatar Companion");
            // Unskalierte Zeit: Der Avatar redet/gestikuliert auch während einer Pause weiter.
            m_Graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);

            var output = AnimationPlayableOutput.Create(m_Graph, "Animation", animator);
            // Layer-Mixer statt Blend-Mixer: Die Blender-Clips animieren jeweils nur
            // "ihr" Objekt (Body bzw. Mouth). Als Layer laufen sie ungeschwächt
            // nebeneinander, statt sich gegenseitig Richtung Bind-Pose zu verwässern.
            var mixer = AnimationLayerMixerPlayable.Create(m_Graph, clips.Count);

            for (int i = 0; i < clips.Count; i++)
            {
                var playable = AnimationClipPlayable.Create(m_Graph, clips[i]);
                m_Graph.Connect(playable, 0, mixer, i);
                mixer.SetInputWeight(i, 1f);
                m_ClipPlayables.Add(playable);
            }

            output.SetSourcePlayable(mixer);
            m_Graph.Play();
        }

        private void SetupBubble()
        {
            var bubbleObject = new GameObject("Sprechblase");
            bubbleObject.transform.SetParent(transform, false);
            bubbleObject.transform.localPosition = bubbleOffset;
            m_Bubble = bubbleObject.AddComponent<AvatarSpeechBubble>();
        }

        // ---------------------------------------------------------------------
        // Spiel-Events
        // ---------------------------------------------------------------------

        private void OnEnable()
        {
            GameManager.StateChanged += HandleStateChanged;
            GameManager.ScoreChanged += HandleScoreChanged;
            GameManager.LivesChanged += HandleLivesChanged;
        }

        private void OnDisable()
        {
            GameManager.StateChanged -= HandleStateChanged;
            GameManager.ScoreChanged -= HandleScoreChanged;
            GameManager.LivesChanged -= HandleLivesChanged;
        }

        private void Start()
        {
            // Falls der GameManager seinen Anfangszustand schon gesetzt hat,
            // bevor wir lauschen konnten.
            if (GameManager.Instance != null)
            {
                HandleStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void Update()
        {
            // Loop-Sicherheitsnetz: Falls ein Clip ohne Loop-Flag importiert wurde,
            // wird er hier von Hand zurückgespult.
            for (int i = 0; i < m_ClipPlayables.Count; i++)
            {
                var playable = m_ClipPlayables[i];
                if (!playable.IsValid())
                {
                    continue;
                }

                var clip = playable.GetAnimationClip();
                if (clip != null && !clip.isLooping && playable.GetTime() >= clip.length)
                {
                    playable.SetTime(0.0);
                }
            }
        }

        private void OnDestroy()
        {
            if (m_Graph.IsValid())
            {
                m_Graph.Destroy();
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == m_HandledState)
            {
                return;
            }
            m_HandledState = state;

            StopReminder();

            switch (state)
            {
                case GameState.MainMenu:
                    m_Movement.MoveTo(menuAnchor);
                    if (!m_TutorialShown)
                    {
                        m_TutorialShown = true;
                        m_Bubble.Clear();
                        foreach (var line in tutorialLines)
                        {
                            m_Bubble.Say(line);
                        }
                    }
                    else
                    {
                        m_Bubble.Say(menuReturnLine, -1f, interrupt: true);
                    }
                    m_ReminderRoutine = StartCoroutine(MenuReminderLoop());
                    break;

                case GameState.WaitingForPlayerTracking:
                    m_Movement.MoveTo(menuAnchor);
                    m_Bubble.Say(trackingLine, -1f, interrupt: true);
                    break;

                case GameState.Playing:
                    // Zähler auf den frischen Rundenstand setzen (LivesChanged/
                    // ScoreChanged feuern direkt nach diesem Event noch einmal).
                    m_LastScore = GameManager.Instance != null ? GameManager.Instance.Score : 0;
                    m_LastLives = GameManager.Instance != null ? GameManager.Instance.Lives : 0;
                    m_Streak = 0;
                    m_BestAtRoundStart = HumanTetrisHighscore.BestScore;

                    m_Bubble.Say(PickRandom(startLines), -1f, interrupt: true);
                    m_Movement.WanderWithin(new Bounds(wanderCenter, wanderExtents * 2f),
                                            wanderIntervalRange);
                    break;

                case GameState.GameOver:
                    m_Movement.MoveTo(gameOverAnchor);

                    int score = GameManager.Instance != null ? GameManager.Instance.Score : 0;
                    string resultLine;
                    if (score > m_BestAtRoundStart && score > 0)
                    {
                        resultLine = string.Format(newRecordFormat, score);
                    }
                    else if (score > 0)
                    {
                        resultLine = string.Format(gameOverScoreFormat, score,
                                                   HumanTetrisHighscore.BestScore);
                    }
                    else
                    {
                        resultLine = gameOverZeroLine;
                    }

                    m_Bubble.Say(resultLine, -1f, interrupt: true);
                    m_Bubble.Say(gameOverHintLine, 6f);
                    break;
            }
        }

        private void HandleScoreChanged(int score)
        {
            if (GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameState.Playing)
            {
                m_LastScore = score;
                return;
            }

            if (score > m_LastScore)
            {
                m_Streak++;

                if (Time.unscaledTime - m_LastPraiseTime >= praiseCooldown)
                {
                    m_LastPraiseTime = Time.unscaledTime;
                    string line = m_Streak >= streakThreshold &&
                                  m_Streak % streakThreshold == 0 && streakLines.Length > 0
                        ? PickRandom(streakLines)
                        : PickRandom(praiseLines);
                    m_Bubble.Say(line, 2.5f, interrupt: true);
                }
            }

            m_LastScore = score;
        }

        private void HandleLivesChanged(int lives)
        {
            if (GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameState.Playing)
            {
                m_LastLives = lives;
                return;
            }

            if (lives < m_LastLives)
            {
                m_Streak = 0;
                m_Bubble.Say(PickRandom(hitLines), 2.5f, interrupt: true);
                if (lives == 1)
                {
                    m_Bubble.Say(lastLifeLine, 3f);
                }
            }

            m_LastLives = lives;
        }

        // ---------------------------------------------------------------------
        // Hilfen
        // ---------------------------------------------------------------------

        /// <summary>Erinnert im Menü regelmäßig an den Startknopf, wenn nichts angezeigt wird.</summary>
        private IEnumerator MenuReminderLoop()
        {
            while (true)
            {
                float waited = 0f;
                while (waited < menuReminderInterval)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!m_Bubble.IsBusy)
                {
                    m_Bubble.Say(menuReminderLine);
                }
            }
        }

        private void StopReminder()
        {
            if (m_ReminderRoutine != null)
            {
                StopCoroutine(m_ReminderRoutine);
                m_ReminderRoutine = null;
            }
        }

        /// <summary>Zufälliger Eintrag, ohne denselben zweimal hintereinander zu wählen.</summary>
        private string PickRandom(string[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                return string.Empty;
            }
            if (lines.Length == 1)
            {
                return lines[0];
            }

            int index = Random.Range(0, lines.Length);
            if (index == m_LastRandomIndex)
            {
                index = (index + 1) % lines.Length;
            }
            m_LastRandomIndex = index;
            return lines[index];
        }
    }
}
