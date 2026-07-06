using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CaveGame
{
    /// <summary>
    /// Dynamische Sprechblase über dem Begleiter-Avatar. Baut sich – wie die übrige
    /// Spiel-UI – komplett per Code auf: World-Space-Canvas mit abgerundetem weißen
    /// Panel, kleinem Zeigedreieck nach unten und Text mit Schreibmaschinen-Effekt.
    ///
    /// Texte werden über <see cref="Say"/> in eine Warteschlange gelegt und
    /// nacheinander angezeigt (so entstehen mehrteilige Erklärungen). Mit
    /// <c>interrupt = true</c> ersetzt ein Text die aktuelle Blase sofort
    /// (z. B. für Treffer-Feedback). Die Blase dreht sich immer zum Spieler.
    /// </summary>
    public sealed class AvatarSpeechBubble : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Breite/Höhe des Textpanels in Canvas-Pixeln.")]
        public Vector2 bubbleSize = new Vector2(720f, 260f);
        [Tooltip("Höhe des Zeigedreiecks unter dem Panel (Canvas-Pixel).")]
        public float tailHeight = 46f;
        [Tooltip("Breite der Blase in Weltmetern (bestimmt die Canvas-Skalierung).")]
        public float worldWidth = 1.5f;
        public int fontSize = 46;
        public Color panelColor = new Color(1f, 1f, 1f, 0.96f);
        public Color textColor = new Color(0.08f, 0.09f, 0.13f, 1f);

        [Header("Verhalten")]
        [Tooltip("Zeichen pro Sekunde beim Schreibmaschinen-Effekt (0 = Text sofort komplett).")]
        public float charactersPerSecond = 35f;
        [Tooltip("Mindest-Anzeigedauer einer Blase in Sekunden (bei automatischer Dauer).")]
        public float minHoldSeconds = 2.4f;
        [Tooltip("Maximale automatische Anzeigedauer in Sekunden.")]
        public float maxHoldSeconds = 8f;
        [Tooltip("Kopfhöhe des Spielers – die Blase richtet sich dorthin aus.")]
        public float playerHeadHeight = 1.7f;

        /// <summary>Wird gerade ein Text angezeigt oder wartet einer in der Schlange?</summary>
        public bool IsBusy => m_Routine != null;

        private struct Line
        {
            public string text;
            public float hold;
        }

        private readonly Queue<Line> m_Queue = new Queue<Line>();
        private Coroutine m_Routine;
        private GameObject m_CanvasObject;
        private RectTransform m_CanvasRect;
        private Text m_Label;
        private float m_BaseScale;

        private static Sprite s_PanelSprite;
        private static Sprite s_TailSprite;

        private void Awake()
        {
            BuildUi();
        }

        // ---------------------------------------------------------------------
        // Öffentliche API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Zeigt einen Text in der Sprechblase an bzw. reiht ihn ein.
        /// </summary>
        /// <param name="text">Anzuzeigender Text.</param>
        /// <param name="holdSeconds">Anzeigedauer; &lt;= 0 = automatisch anhand der Textlänge.</param>
        /// <param name="interrupt">true = Warteschlange verwerfen und sofort diesen Text zeigen.</param>
        public void Say(string text, float holdSeconds = -1f, bool interrupt = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (interrupt)
            {
                m_Queue.Clear();
                if (m_Routine != null)
                {
                    StopCoroutine(m_Routine);
                    m_Routine = null;
                }
            }

            m_Queue.Enqueue(new Line { text = text, hold = holdSeconds });

            if (m_Routine == null)
            {
                m_Routine = StartCoroutine(ProcessQueue());
            }
        }

        /// <summary>Blendet die Blase aus und leert die Warteschlange.</summary>
        public void Clear()
        {
            m_Queue.Clear();
            if (m_Routine != null)
            {
                StopCoroutine(m_Routine);
                m_Routine = null;
            }
            if (m_CanvasObject != null)
            {
                m_CanvasObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------------
        // Anzeige-Ablauf
        // ---------------------------------------------------------------------

        private IEnumerator ProcessQueue()
        {
            m_CanvasObject.SetActive(true);
            m_Label.text = string.Empty;
            yield return ScaleTo(1f, 0.15f);

            while (m_Queue.Count > 0)
            {
                var line = m_Queue.Dequeue();
                float hold = line.hold > 0f
                    ? line.hold
                    : Mathf.Clamp(1.4f + line.text.Length * 0.055f, minHoldSeconds, maxHoldSeconds);

                // Schreibmaschinen-Effekt (unskalierte Zeit, damit die Blase auch
                // während einer Spielpause – z. B. Sprachfrage – weiterläuft).
                if (charactersPerSecond > 0f)
                {
                    float elapsed = 0f;
                    int visible = 0;
                    while (visible < line.text.Length)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        visible = Mathf.Min(line.text.Length,
                                            Mathf.FloorToInt(elapsed * charactersPerSecond));
                        m_Label.text = line.text.Substring(0, visible);
                        yield return null;
                    }
                }

                m_Label.text = line.text;

                float wait = 0f;
                while (wait < hold)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            yield return ScaleTo(0f, 0.12f);
            m_CanvasObject.SetActive(false);
            m_Routine = null;
        }

        /// <summary>Kleine Pop-Animation beim Ein-/Ausblenden.</summary>
        private IEnumerator ScaleTo(float target, float duration)
        {
            float start = m_CanvasRect.localScale.x / m_BaseScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(start, target, Mathf.Clamp01(elapsed / duration));
                m_CanvasRect.localScale = Vector3.one * (m_BaseScale * Mathf.Max(0.001f, t));
                yield return null;
            }
            m_CanvasRect.localScale = Vector3.one * (m_BaseScale * Mathf.Max(0.001f, target));
        }

        private void LateUpdate()
        {
            if (m_CanvasRect == null)
            {
                return;
            }

            // Immer zum Spieler (CAVE-Mitte) drehen. +Z zeigt vom Spieler weg,
            // damit der Text richtig herum lesbar ist.
            Vector3 head = new Vector3(0f, playerHeadHeight, 0f);
            Vector3 away = m_CanvasRect.position - head;
            if (away.sqrMagnitude > 0.0001f)
            {
                m_CanvasRect.rotation = Quaternion.LookRotation(away);
            }
        }

        // ---------------------------------------------------------------------
        // UI-Aufbau
        // ---------------------------------------------------------------------

        private void BuildUi()
        {
            m_CanvasObject = new GameObject("Bubble Canvas");
            m_CanvasObject.transform.SetParent(transform, false);

            var canvas = m_CanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;

            m_CanvasRect = m_CanvasObject.GetComponent<RectTransform>();
            m_CanvasRect.sizeDelta = new Vector2(bubbleSize.x, bubbleSize.y + tailHeight);
            m_CanvasRect.pivot = new Vector2(0.5f, 0f); // Fußpunkt = Dreiecksspitze
            m_BaseScale = worldWidth / Mathf.Max(1f, bubbleSize.x);
            m_CanvasRect.localScale = Vector3.one * m_BaseScale;

            // Panel (abgerundetes Rechteck, 9-Slice)
            var panel = CaveUiFactory.CreateImage(m_CanvasObject.transform, "Panel", panelColor);
            panel.sprite = GetPanelSprite();
            panel.type = Image.Type.Sliced;
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.offsetMin = new Vector2(0f, tailHeight);
            panelRect.offsetMax = Vector2.zero;

            // Zeigedreieck nach unten (zum Avatar)
            var tail = CaveUiFactory.CreateImage(m_CanvasObject.transform, "Tail", panelColor);
            tail.sprite = GetTailSprite();
            var tailRect = tail.rectTransform;
            tailRect.anchorMin = new Vector2(0.5f, 0f);
            tailRect.anchorMax = new Vector2(0.5f, 0f);
            tailRect.pivot = new Vector2(0.5f, 0f);
            tailRect.anchoredPosition = Vector2.zero;
            tailRect.sizeDelta = new Vector2(64f, tailHeight + 6f);

            // Textfeld mit Innenabstand
            m_Label = CaveUiFactory.CreateText(panel.transform, "Text", string.Empty, fontSize,
                                               FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            var labelRect = m_Label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(30f, 22f);
            labelRect.offsetMax = new Vector2(-30f, -22f);
            m_Label.horizontalOverflow = HorizontalWrapMode.Wrap;
            m_Label.verticalOverflow = VerticalWrapMode.Overflow;

            m_CanvasObject.SetActive(false);
        }

        /// <summary>Prozedural erzeugtes Sprite: weißes Rechteck mit runden Ecken.</summary>
        private static Sprite GetPanelSprite()
        {
            if (s_PanelSprite != null)
            {
                return s_PanelSprite;
            }

            const int size = 64;
            const float radius = 18f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BubblePanel",
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Abstand zur nächsten Ecken-Kreismitte -> weicher Rand.
                    float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                    float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            s_PanelSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                          100f, 0, SpriteMeshType.FullRect,
                                          new Vector4(24f, 24f, 24f, 24f));
            return s_PanelSprite;
        }

        /// <summary>Prozedural erzeugtes Sprite: Dreieck mit Spitze nach unten.</summary>
        private static Sprite GetTailSprite()
        {
            if (s_TailSprite != null)
            {
                return s_TailSprite;
            }

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BubbleTail",
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                // Oben (y = size-1) volle Breite, unten (y = 0) Spitze in der Mitte.
                float halfWidth = (y / (float)(size - 1)) * (size * 0.5f);
                for (int x = 0; x < size; x++)
                {
                    float alpha = Mathf.Clamp01(halfWidth - Mathf.Abs(x - size * 0.5f) + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            s_TailSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return s_TailSprite;
        }
    }
}
