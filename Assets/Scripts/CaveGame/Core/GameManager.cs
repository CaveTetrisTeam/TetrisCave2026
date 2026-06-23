using System;
using UnityEngine;
using HumanTetris;

namespace CaveGame
{
    /// <summary>
    /// Zentrale Spielsteuerung (Zustandsautomat) für das Kinect-Cave-Spiel.
    ///
    /// Hält den aktuellen <see cref="GameState"/>, die Leben und – über
    /// <see cref="HumanTetrisHighscore"/> – den Punktestand. Aktiviert/deaktiviert
    /// den <see cref="WallSpawner"/>, zeigt/versteckt den Startbildschirm
    /// (<see cref="HumanTetrisStartMenu"/>) und sendet Events, auf die UI-Skripte hören.
    ///
    /// Wird vom <see cref="GameBootstrapper"/> automatisch erzeugt – es muss also
    /// nichts manuell in die Szene gelegt werden.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // Statische Events (entkoppelt von der Erzeugungs-Reihenfolge der UI).
        public static event Action<GameState> StateChanged;
        public static event Action<int> LivesChanged;
        public static event Action<int> ScoreChanged;

        [Header("Regeln")]
        [Tooltip("Startanzahl der Leben.")]
        public int maxLives = 3;
        [Tooltip("Punkte pro erfolgreich überquerter Wand.")]
        public int pointsPerWall = 100;

        [Header("Tracking")]
        [Tooltip("Wenn aktiv, darf das Spiel nur starten, wenn eine Person zuverlässig getrackt wird. " +
                 "Per Tastatur (Enter/Leertaste) kann zum Testen trotzdem gestartet werden (UI-Fallback).")]
        public bool requirePlayerTracking = true;
        [Tooltip("Erlaubt das Starten/Überspringen per Enter oder Leertaste (Maus-/Tastatur-Fallback laut PDF).")]
        public bool allowKeyboardFallback = true;

        [Header("Optionale Referenzen (werden sonst automatisch gesucht)")]
        public WallSpawner wallSpawner;
        public KinectPlayerPresence playerPresence;

        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public int Lives { get; private set; }
        public int Score => HumanTetrisHighscore.CurrentScore;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            HumanTetrisStartMenu.StartRequested += HandleStartRequested;
        }

        private void OnDisable()
        {
            HumanTetrisStartMenu.StartRequested -= HandleStartRequested;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            ResolveReferences();
            // Initialzustand sauber setzen (Startbildschirm sichtbar, Wände aus).
            EnterState(GameState.MainMenu, force: true);
        }

        private void Update()
        {
            switch (CurrentState)
            {
                case GameState.WaitingForPlayerTracking:
                    UpdateWaitingForPlayer();
                    break;
            }
        }

        /// <summary>
        /// Sucht fehlende Referenzen in der Szene zusammen.
        /// </summary>
        private void ResolveReferences()
        {
            if (wallSpawner == null) wallSpawner = FindObjectOfType<WallSpawner>(true);
            if (playerPresence == null) playerPresence = FindObjectOfType<KinectPlayerPresence>(true);
        }

        // ---------------------------------------------------------------------
        // Zustandsübergänge
        // ---------------------------------------------------------------------

        /// <summary>Reagiert auf den Start-Wunsch des Startmenüs (UI-Button / Tastatur).</summary>
        private void HandleStartRequested()
        {
            RequestStart();
        }

        /// <summary>Wird vom Start-Knopf (physisch oder UI) aufgerufen.</summary>
        public void RequestStart()
        {
            if (CurrentState == GameState.MainMenu)
            {
                EnterState(GameState.WaitingForPlayerTracking);
            }
        }

        public void StartPlaying()
        {
            Lives = Mathf.Max(1, maxLives);
            HumanTetrisHighscore.ResetCurrentScore();

            EnterState(GameState.Playing);

            LivesChanged?.Invoke(Lives);
            ScoreChanged?.Invoke(Score);
        }

        public void RestartGame()
        {
            StartPlaying();
        }

        public void ReturnToMenu()
        {
            EnterState(GameState.MainMenu);
        }

        /// <summary>Zieht ein Leben ab; bei 0 → Game Over.</summary>
        public void LoseLife()
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            Lives = Mathf.Max(0, Lives - 1);
            LivesChanged?.Invoke(Lives);

            if (Lives <= 0)
            {
                EnterState(GameState.GameOver);
            }
        }

        /// <summary>Punkte für eine erfolgreich durchquerte Wand gutschreiben.</summary>
        public void AddWallScore()
        {
            AddScore(pointsPerWall);
        }

        public void AddScore(int points)
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            HumanTetrisHighscore.AddToCurrentScore(points);
            ScoreChanged?.Invoke(Score);
        }

        private void UpdateWaitingForPlayer()
        {
            bool ready = !requirePlayerTracking;

            if (requirePlayerTracking && playerPresence != null && playerPresence.HasReliablePlayer)
            {
                ready = true;
            }

            // Fallback ohne Kinect / zum Testen.
            if (allowKeyboardFallback &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
            {
                ready = true;
            }

            if (ready)
            {
                StartPlaying();
            }
        }

        private void EnterState(GameState next, bool force = false)
        {
            if (!force && next == CurrentState)
            {
                return;
            }

            CurrentState = next;

            switch (next)
            {
                case GameState.MainMenu:
                    Time.timeScale = 0f; // Startmenü pausiert ohnehin – Konsistenz herstellen.
                    HumanTetrisStartMenu.ShowMenu();
                    if (wallSpawner != null)
                    {
                        wallSpawner.StopSpawning();
                        wallSpawner.ClearAllWalls();
                    }
                    break;

                case GameState.WaitingForPlayerTracking:
                    Time.timeScale = 1f;
                    HumanTetrisStartMenu.HideMenu();
                    if (wallSpawner != null)
                    {
                        wallSpawner.StopSpawning();
                        wallSpawner.ClearAllWalls();
                    }
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    HumanTetrisStartMenu.HideMenu();
                    if (wallSpawner != null)
                    {
                        wallSpawner.ClearAllWalls();
                        wallSpawner.StartSpawning();
                    }
                    break;

                case GameState.GameOver:
                    Time.timeScale = 1f;
                    if (wallSpawner != null)
                    {
                        wallSpawner.StopSpawning();
                        wallSpawner.FreezeAllWalls(); // Wandbewegung stoppen (PDF).
                    }
                    // Highscore sichern (AddToCurrentScore speichert zwar bereits laufend,
                    // dies ist die ausdrückliche Game-Over-Sicherung laut PDF).
                    HumanTetrisHighscore.SaveIfHigher(HumanTetrisHighscore.CurrentScore);
                    break;
            }

            StateChanged?.Invoke(next);
        }
    }
}
