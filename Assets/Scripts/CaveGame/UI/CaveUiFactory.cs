using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HTW.CAVE;

namespace CaveGame
{
    /// <summary>
    /// Kleine Helfer, um die UI (HUD, Game Over) komplett per Code aufzubauen –
    /// gleiches Vorgehen wie beim vorhandenen Startbildschirm, damit nichts manuell
    /// in die Szene gelegt werden muss.
    /// </summary>
    internal static class CaveUiFactory
    {
        private static Font s_Font;

        public static Font GetFont()
        {
            if (s_Font != null)
            {
                return s_Font;
            }

            foreach (var name in new[] { "Arial", "Helvetica Neue", "Helvetica", "Lucida Grande" })
            {
                s_Font = Font.CreateDynamicFontFromOSFont(name, 32);
                if (s_Font != null)
                {
                    return s_Font;
                }
            }

            try { s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (s_Font == null)
            {
                try { s_Font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }

            return s_Font;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        public static Canvas CreateOverlayCanvas(GameObject host, int sortingOrder)
        {
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            host.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// Erzeugt ein World-Space-Canvas unmittelbar vor der physischen CAVE-
        /// Frontwand. ScreenSpaceOverlay landet nur im Desktop-GameView und wird
        /// von den virtuellen Projektorkameras nicht in den Raum gerendert.
        /// </summary>
        public static Canvas CreateFrontWallCanvas(GameObject host, int sortingOrder)
        {
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            var rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1920f, 1080f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var environment = Object.FindObjectOfType<VirtualEnvironment>(true);
            Vector3 dimensions = environment != null
                ? environment.dimensions
                : new Vector3(3f, 2.45f, 3f);

            if (environment != null)
            {
                rect.SetParent(environment.transform, false);
            }

            // Zwei Zentimeter innerhalb der Frontfläche verhindern Z-Fighting.
            rect.localPosition = new Vector3(0f, dimensions.y * 0.5f, dimensions.z * 0.5f - 0.02f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * (dimensions.x / 1920f);

            return canvas;
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string text, int fontSize,
                                      FontStyle style, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<Text>();
            label.text = text;
            label.font = GetFont();
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = anchor;
            label.alignByGeometry = true;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button CreateButton(Transform parent, string label, Color color, out Text labelText)
        {
            var image = CreateImage(parent, "Button_" + label, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color * 1.2f;
            colors.pressedColor = color * 0.8f;
            colors.selectedColor = color * 1.2f;
            button.colors = colors;

            labelText = CreateText(image.transform, "Label", label, 34, FontStyle.Bold,
                                   TextAnchor.MiddleCenter, Color.white);
            Stretch(labelText.rectTransform);
            return button;
        }

        public static void SetAnchored(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                                       Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
