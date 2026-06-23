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
        private Text m_TrackingText;
        private SkeletonPreviewGraphic m_SkeletonPreview;

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

            BuildSkeletonPreview();

            SetVisible(false);
        }

        private void Update()
        {
            if (m_TrackingText != null && m_SkeletonPreview != null)
            {
                m_TrackingText.text = m_SkeletonPreview.HasPose
                    ? "Tracking aktiv"
                    : "Warte auf Tracking ...";
                m_TrackingText.color = m_SkeletonPreview.HasPose
                    ? new Color(0.45f, 1f, 0.68f)
                    : new Color(1f, 0.68f, 0.35f);
            }
        }

        private void BuildSkeletonPreview()
        {
            var panel = CaveUiFactory.CreateImage(transform, "Live Skeleton Panel",
                new Color(0.015f, 0.035f, 0.07f, 0.84f));
            CaveUiFactory.SetAnchored(panel.rectTransform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-36f, 36f), new Vector2(330f, 370f));
            panel.raycastTarget = false;

            var title = CaveUiFactory.CreateText(panel.transform, "Title", "CORPUS IN SPECULO",
                24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.55f, 0.94f, 1f));
            CaveUiFactory.SetAnchored(title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -17f), new Vector2(300f, 42f));

            var previewObject = new GameObject("Live Skeleton");
            previewObject.transform.SetParent(panel.transform, false);
            m_SkeletonPreview = previewObject.AddComponent<SkeletonPreviewGraphic>();
            CaveUiFactory.SetAnchored(m_SkeletonPreview.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -5f), new Vector2(286f, 286f));

            m_TrackingText = CaveUiFactory.CreateText(panel.transform, "Tracking Status", "",
                20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            CaveUiFactory.SetAnchored(m_TrackingText.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 13f), new Vector2(300f, 38f));
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
