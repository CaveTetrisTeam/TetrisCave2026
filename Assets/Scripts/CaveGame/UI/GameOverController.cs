using UnityEngine;
using UnityEngine.UI;
using HumanTetris;

namespace CaveGame
{
    /// <summary>
    /// Game-Over-Bildschirm auf der CAVE-Frontwand: zeigt "Game Over", den
    /// erreichten Score und den aktuellen Highscore. Neustart und Rückkehr zum
    /// Menü werden primär über die beiden farbigen Podestknöpfe ausgelöst.
    /// Baut sich per Code auf und ist nur im Zustand <see cref="GameState.GameOver"/> sichtbar.
    /// Die Buttons sind der Maus-/UI-Fallback laut PDF.
    /// </summary>
    public sealed class GameOverController : MonoBehaviour
    {
        private CanvasGroup m_Group;
        private Text m_ScoreText;
        private Text m_HighscoreText;

        private void Awake()
        {
            CaveUiFactory.EnsureEventSystem();
            CaveUiFactory.CreateFrontWallCanvas(gameObject, 1100);
            m_Group = gameObject.AddComponent<CanvasGroup>();

            // Abdunkelnder Hintergrund.
            var background = CaveUiFactory.CreateImage(transform, "Dim", new Color(0.02f, 0.03f, 0.06f, 0.85f));
            CaveUiFactory.Stretch(background.rectTransform);

            // Zentrale Tafel.
            var panel = CaveUiFactory.CreateImage(transform, "Panel", new Color(0.05f, 0.07f, 0.12f, 0.96f));
            CaveUiFactory.SetAnchored(panel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 620f));

            var title = CaveUiFactory.CreateText(panel.transform, "Title", "Game Over", 90, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.4f));
            CaveUiFactory.SetAnchored(title.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 210f), new Vector2(720f, 120f));

            m_ScoreText = CaveUiFactory.CreateText(panel.transform, "Score", "", 50, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Color(0.92f, 1f, 0.6f));
            CaveUiFactory.SetAnchored(m_ScoreText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 90f), new Vector2(720f, 70f));

            m_HighscoreText = CaveUiFactory.CreateText(panel.transform, "Highscore", "", 44, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Color(0.72f, 0.92f, 1f));
            CaveUiFactory.SetAnchored(m_HighscoreText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(720f, 70f));

            var restart = CaveUiFactory.CreateButton(panel.transform, "Neustart",
                new Color(0.26f, 0.78f, 0.42f), out _);
            CaveUiFactory.SetAnchored(restart.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-190f, -130f), new Vector2(320f, 96f));
            restart.onClick.AddListener(OnRestart);

            var back = CaveUiFactory.CreateButton(panel.transform, "Zurück zum Start",
                new Color(0.30f, 0.45f, 0.85f), out _);
            CaveUiFactory.SetAnchored(back.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(190f, -130f), new Vector2(320f, 96f));
            back.onClick.AddListener(OnBackToMenu);

            var hint = CaveUiFactory.CreateText(panel.transform, "Hint",
                "PODEST:  GRÜN = Neustart   •   BLAU = Menü", 26, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.6f, 0.7f, 0.85f));
            CaveUiFactory.SetAnchored(hint.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -230f), new Vector2(720f, 50f));

            SetVisible(false);
        }

        private void OnEnable()
        {
            GameManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.StateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (m_Group.alpha <= 0f)
            {
                return;
            }

            // Tastatur-Fallback.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnRestart();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnBackToMenu();
            }
        }

        private void HandleStateChanged(GameState state)
        {
            bool over = state == GameState.GameOver;
            SetVisible(over);

            if (over)
            {
                m_ScoreText.text = "Dein Score: " + HumanTetrisHighscore.CurrentScore;
                m_HighscoreText.text = "Highscore: " + HumanTetrisHighscore.BestScore;
            }
        }

        private void OnRestart()
        {
            GameManager.Instance?.RestartGame();
        }

        private void OnBackToMenu()
        {
            GameManager.Instance?.ReturnToMenu();
        }

        private void SetVisible(bool visible)
        {
            m_Group.alpha = visible ? 1f : 0f;
            m_Group.interactable = visible;
            m_Group.blocksRaycasts = visible;
        }
    }
}
