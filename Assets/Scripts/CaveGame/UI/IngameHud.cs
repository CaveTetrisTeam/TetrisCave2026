using UnityEngine;
using UnityEngine.UI;

namespace CaveGame
{
    /// <summary>
    /// In-Game-HUD: zeigt Leben (oben links) und aktuellen Score (oben rechts).
    /// Baut sich komplett per Code auf und ist nur im Zustand
    /// <see cref="GameState.Playing"/> sichtbar.
    /// </summary>
    public sealed class IngameHud : MonoBehaviour
    {
        private CanvasGroup m_Group;
        private Text m_LivesText;
        private Text m_ScoreText;

        private void Awake()
        {
            CaveUiFactory.EnsureEventSystem();
            CaveUiFactory.CreateOverlayCanvas(gameObject, 900);
            m_Group = gameObject.AddComponent<CanvasGroup>();

            var lives = CaveUiFactory.CreateText(transform, "Lives", "", 48, FontStyle.Bold,
                TextAnchor.UpperLeft, new Color(1f, 0.45f, 0.45f));
            CaveUiFactory.SetAnchored(lives.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -30f), new Vector2(600f, 80f));
            m_LivesText = lives;

            var score = CaveUiFactory.CreateText(transform, "Score", "", 48, FontStyle.Bold,
                TextAnchor.UpperRight, new Color(0.92f, 1f, 0.6f));
            CaveUiFactory.SetAnchored(score.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -30f), new Vector2(600f, 80f));
            m_ScoreText = score;

            SetVisible(false);
        }

        private void OnEnable()
        {
            GameManager.StateChanged += HandleStateChanged;
            GameManager.LivesChanged += HandleLivesChanged;
            GameManager.ScoreChanged += HandleScoreChanged;
        }

        private void OnDisable()
        {
            GameManager.StateChanged -= HandleStateChanged;
            GameManager.LivesChanged -= HandleLivesChanged;
            GameManager.ScoreChanged -= HandleScoreChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            SetVisible(state == GameState.Playing);

            if (state == GameState.Playing && GameManager.Instance != null)
            {
                HandleLivesChanged(GameManager.Instance.Lives);
                HandleScoreChanged(GameManager.Instance.Score);
            }
        }

        private void HandleLivesChanged(int lives)
        {
            m_LivesText.text = "Leben: " + new string('♥', Mathf.Max(0, lives));
        }

        private void HandleScoreChanged(int score)
        {
            m_ScoreText.text = "Punkte: " + score;
        }

        private void SetVisible(bool visible)
        {
            m_Group.alpha = visible ? 1f : 0f;
            m_Group.interactable = false;     // HUD ist nur Anzeige
            m_Group.blocksRaycasts = false;
        }
    }
}
