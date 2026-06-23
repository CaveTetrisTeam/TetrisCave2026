using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanTetris
{
    public sealed class HumanTetrisStartMenu : MonoBehaviour
    {
        private const string MenuObjectName = "HumanTetris Start Menu";

        /// <summary>
        /// Wird ausgelöst, wenn der Start angefordert wird – egal ob über den
        /// klassischen UI-Button, die Tastatur oder den physischen Kinect-Knopf.
        /// Der CaveGame-GameManager hört darauf und wechselt in den Spielablauf.
        /// </summary>
        public static event Action StartRequested;

        private static HumanTetrisStartMenu _instance;
        private static Font _cachedMenuFont;
        private static Sprite _roundedSprite;

        [SerializeField] private bool pauseGameWhileMenuIsOpen = true;

        private CanvasGroup _menuGroup;
        private Text _highscoreText;
        private int _displayedHighscore = -1;
        private float _timeScaleBeforeMenu = 1f;
        private bool _storedTimeScale;
        private bool _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ShowMenuOnGameStart()
        {
            EnsureMenuExists();
        }

        public static void EnsureMenuExists()
        {
            if (_instance != null)
            {
                _instance.Show();
                return;
            }

            var menuObject = new GameObject(MenuObjectName);
            DontDestroyOnLoad(menuObject);
            menuObject.AddComponent<HumanTetrisStartMenu>();
        }

        public static void ReportScore(int score)
        {
            HumanTetrisHighscore.SaveIfHigher(score);

            if (_instance != null)
            {
                _instance.RefreshHighscore();
                _instance.Show();
            }
        }

        /// <summary>Stellt sicher, dass das Menü existiert und sichtbar ist.</summary>
        public static void ShowMenu()
        {
            EnsureMenuExists();
        }

        /// <summary>Versteckt das Menü (falls vorhanden).</summary>
        public static void HideMenu()
        {
            if (_instance != null)
            {
                _instance.Hide();
            }
        }

        /// <summary>Start anfordern (statischer Einstieg, z. B. für den physischen Knopf).</summary>
        public static void RequestStartStatic()
        {
            EnsureMenuExists();
            if (_instance != null)
            {
                _instance.RequestStart();
            }
        }

        /// <summary>
        /// Löst den Spielstart aus: meldet <see cref="StartRequested"/> und versteckt das Menü.
        /// Wird vom UI-Button, der Tastatur und dem physischen Kinect-Knopf genutzt.
        /// </summary>
        public void RequestStart()
        {
            StartRequested?.Invoke();
            Hide();
        }

        public void Show()
        {
            RefreshHighscore();
            SetMenuVisible(true);

            if (!pauseGameWhileMenuIsOpen || _storedTimeScale)
            {
                return;
            }

            _timeScaleBeforeMenu = Time.timeScale <= 0f ? 1f : Time.timeScale;
            _storedTimeScale = true;
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            SetMenuVisible(false);

            if (!pauseGameWhileMenuIsOpen || !_storedTimeScale)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforeMenu;
            _storedTimeScale = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildMenu();
            Show();
        }

        private void OnEnable()
        {
            HumanTetrisHighscore.BestScoreChanged += HandleBestScoreChanged;
        }

        private void OnDisable()
        {
            HumanTetrisHighscore.BestScoreChanged -= HandleBestScoreChanged;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (_displayedHighscore != HumanTetrisHighscore.BestScore)
            {
                RefreshHighscore();
            }

            if (!_isOpen)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                RequestStart();
            }
        }

        private void BuildMenu()
        {
            EnsureEventSystemExists();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            _menuGroup = gameObject.AddComponent<CanvasGroup>();

            var background = CreateImage("Background", transform, new Color(0.015f, 0.020f, 0.045f, 1f));
            Stretch(background.rectTransform);

            var topGlow = CreateImage("Top Cyan Glow", background.transform, new Color(0.00f, 0.70f, 0.85f, 0.16f));
            SetRect(topGlow.rectTransform, 0f, 425f, 1920f, 300f);

            var bottomGlow = CreateImage("Bottom Blue Glow", background.transform, new Color(0.10f, 0.24f, 0.65f, 0.22f));
            SetRect(bottomGlow.rectTransform, 0f, -455f, 1920f, 250f);

            var titleStripe = CreateImage("Title Stripe", background.transform, new Color(0.03f, 0.12f, 0.18f, 0.78f));
            SetRect(titleStripe.rectTransform, 0f, 158f, 1920f, 260f);

            var panel = CreateImage("Menu Panel", background.transform, new Color(0.02f, 0.04f, 0.075f, 0.74f));
            Stretch(panel.rectTransform);

            CreateTetromino(panel.transform, "Left Tetris Piece", new Vector2(-720f, 55f), 62f, new Color(0.00f, 0.83f, 0.96f), new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 2f)
            });
            CreateTetromino(panel.transform, "Right Tetris Piece", new Vector2(635f, 90f), 58f, new Color(1.00f, 0.77f, 0.16f), new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(2f, 1f)
            });
            CreateTetromino(panel.transform, "Bottom Tetris Piece", new Vector2(-210f, -340f), 46f, new Color(0.26f, 0.92f, 0.48f), new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(2f, 0f),
                new Vector2(1f, 1f)
            });
            CreateTetromino(panel.transform, "Top Tetris Piece", new Vector2(300f, 305f), 42f, new Color(1.00f, 0.30f, 0.42f), new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(2f, 0f),
                new Vector2(3f, 0f)
            });

            var title = CreateText("Title", panel.transform, "Human Tetris", 78, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.95f, 1f, 1f));
            SetRect(title.rectTransform, 0f, 200f, 900f, 120f);

            var subtitle = CreateText("Subtitle", panel.transform, "Weiche den Formen aus und knacke deinen Rekord.", 30, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.72f, 0.92f, 1f));
            SetRect(subtitle.rectTransform, 0f, 105f, 840f, 70f);

            var highscorePlate = CreateRoundedImage("Highscore Plate", panel.transform, new Color(0.00f, 0.72f, 0.86f, 0.16f));
            SetRect(highscorePlate.rectTransform, 0f, 0f, 520f, 92f);

            _highscoreText = CreateText("Highscore", highscorePlate.transform, string.Empty, 44, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.92f, 1f, 0.55f));
            Stretch(_highscoreText.rectTransform);
            MoveRect(_highscoreText.rectTransform, 0f, -3f);

            var startButton = CreateButton(panel.transform);
            SetRect(startButton.GetComponent<RectTransform>(), 0f, -145f, 360f, 90f);
            startButton.onClick.AddListener(RequestStart);

            RefreshHighscore();
        }

        private void HandleBestScoreChanged(int bestScore)
        {
            RefreshHighscore();
        }

        private void RefreshHighscore()
        {
            if (_highscoreText == null)
            {
                return;
            }

            _displayedHighscore = HumanTetrisHighscore.BestScore;
            _highscoreText.text = "Highscore: " + _displayedHighscore;
        }

        private void SetMenuVisible(bool visible)
        {
            _isOpen = visible;
            _menuGroup.alpha = visible ? 1f : 0f;
            _menuGroup.interactable = visible;
            _menuGroup.blocksRaycasts = visible;
        }

        private static void EnsureEventSystemExists()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystem);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            var imageObject = new GameObject(objectName);
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image CreateRoundedImage(string objectName, Transform parent, Color color)
        {
            var image = CreateImage(objectName, parent, color);
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            return image;
        }

        private static Sprite GetRoundedSprite()
        {
            if (_roundedSprite != null)
            {
                return _roundedSprite;
            }

            const int size = 64;
            const int radius = 18;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HumanTetrisRoundedUiSprite",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var centerMin = radius;
            var centerMax = size - radius - 1;
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nearestX = Mathf.Clamp(x, centerMin, centerMax);
                    var nearestY = Mathf.Clamp(y, centerMin, centerMax);
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(nearestX, nearestY));
                    var alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            _roundedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));

            return _roundedSprite;
        }

        private static void CreateTetromino(Transform parent, string objectName, Vector2 position, float blockSize, Color color, Vector2[] blocks)
        {
            var pieceObject = new GameObject(objectName);
            pieceObject.transform.SetParent(parent, false);

            var pieceRect = pieceObject.AddComponent<RectTransform>();
            pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRect.pivot = new Vector2(0.5f, 0.5f);
            pieceRect.anchoredPosition = position;
            pieceRect.sizeDelta = Vector2.zero;

            foreach (var block in blocks)
            {
                var blockImage = CreateImage("Block", pieceObject.transform, color);
                var blockRect = blockImage.rectTransform;
                blockRect.anchorMin = new Vector2(0.5f, 0.5f);
                blockRect.anchorMax = new Vector2(0.5f, 0.5f);
                blockRect.pivot = new Vector2(0.5f, 0.5f);
                blockRect.anchoredPosition = new Vector2(block.x * blockSize, block.y * blockSize);
                blockRect.sizeDelta = new Vector2(blockSize - 6f, blockSize - 6f);

                var shine = CreateImage("Shine", blockImage.transform, new Color(1f, 1f, 1f, 0.18f));
                shine.rectTransform.anchorMin = new Vector2(0f, 0.65f);
                shine.rectTransform.anchorMax = Vector2.one;
                shine.rectTransform.offsetMin = Vector2.zero;
                shine.rectTransform.offsetMax = Vector2.zero;
            }
        }

        private static Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);

            var label = textObject.AddComponent<Text>();
            label.text = text;
            label.font = GetMenuFont(fontSize);
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.alignByGeometry = true;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static Font GetMenuFont(int fontSize)
        {
            if (_cachedMenuFont != null)
            {
                return _cachedMenuFont;
            }

            var fontNames = new[] { "Arial", "Helvetica Neue", "Helvetica", "Lucida Grande" };
            foreach (var fontName in fontNames)
            {
                _cachedMenuFont = Font.CreateDynamicFontFromOSFont(fontName, fontSize);
                if (_cachedMenuFont != null)
                {
                    return _cachedMenuFont;
                }
            }

            _cachedMenuFont = TryLoadBuiltInFont("LegacyRuntime.ttf");
            if (_cachedMenuFont != null)
            {
                return _cachedMenuFont;
            }

            return TryLoadBuiltInFont("Arial.ttf");
        }

        private static Font TryLoadBuiltInFont(string fontName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(fontName);
            }
            catch
            {
                return null;
            }
        }

        private static Button CreateButton(Transform parent)
        {
            var buttonImage = CreateRoundedImage("Start Button", parent, new Color(1.00f, 0.38f, 0.20f));
            var button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            var colors = button.colors;
            colors.normalColor = new Color(1.00f, 0.38f, 0.20f);
            colors.highlightedColor = new Color(1.00f, 0.58f, 0.23f);
            colors.pressedColor = new Color(0.78f, 0.18f, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var buttonText = CreateText("Label", button.transform, "Spiel starten", 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(buttonText.rectTransform);
            MoveRect(buttonText.rectTransform, 0f, -4f);

            // Derselbe sichtbare Button verarbeitet zusätzlich den unsichtbaren
            // Kinect-Handdruck. Dadurch gibt es keinen zweiten, leeren 3D-Knopf mehr.
            button.gameObject.AddComponent<CaveGame.KinectUiStartButton>();

            return button;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void MoveRect(RectTransform rectTransform, float x, float y)
        {
            rectTransform.offsetMin += new Vector2(x, y);
            rectTransform.offsetMax += new Vector2(x, y);
        }

        private static void SetRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(x, y);
            rectTransform.sizeDelta = new Vector2(width, height);
        }
    }
}
