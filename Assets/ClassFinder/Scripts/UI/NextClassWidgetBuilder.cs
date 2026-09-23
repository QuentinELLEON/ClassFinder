using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClassFinder.UI
{
    /// <summary>
    /// Builds the small top-right widget (student ID box + next class card) and wires it
    /// into a NextClassController. Works at runtime and from the editor menu.
    /// </summary>
    public static class NextClassWidgetBuilder
    {
        public const string CanvasName = "ClassFinder Widget";

        const float Width = 520f;               // in reference pixels (1080 x 1920)
        static readonly Vector2 Margin = new Vector2(24f, 80f); // from the top-right corner

        static readonly Color CardColor = new Color(1f, 1f, 1f, 0.92f);
        static readonly Color TextDark = new Color(0.10f, 0.11f, 0.14f);
        static readonly Color TextMuted = new Color(0.40f, 0.42f, 0.47f);
        static readonly Color Accent = new Color(0.16f, 0.42f, 0.95f);
        static readonly Color ErrorColor = new Color(0.85f, 0.2f, 0.2f);
        static readonly Color InputColor = new Color(0.93f, 0.94f, 0.96f);

        public static GameObject Build(NextClassController c)
        {
            EnsureEventSystem();

            // Canvas (child of the controller's GameObject)
            var canvasGO = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(c.transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            // ----- Student ID box -----
            var idPanel = CreateTopRightPanel("Student ID Box", canvasGO.transform);
            CreateText("Label", idPanel.transform, "Student ID", 28, TextDark, FontStyles.Bold);
            var input = CreateInputField("Student ID Input", idPanel.transform, "e.g. 22106448");
            var idError = CreateText("Error", idPanel.transform, "", 22, ErrorColor);
            var save = CreateButton("Save Button", idPanel.transform, "Save", Accent, Color.white, 76, 30);

            // ----- Next class card -----
            var card = CreateTopRightPanel("Next Class Card", canvasGO.transform);
            var header = CreateText("Header", card.transform, "NEXT CLASS", 22, Accent, FontStyles.Bold);
            header.characterSpacing = 6;
            var title = CreateText("Title", card.transform, "", 32, TextDark, FontStyles.Bold);
            var room = CreateText("Room", card.transform, "", 36, Accent, FontStyles.Bold);
            var time = CreateText("Time", card.transform, "", 26, TextDark);
            var countdown = CreateText("Countdown", card.transform, "", 24, TextMuted, FontStyles.Italic);
            var go = CreateButton("Go To Next Class Button", card.transform, "Go to next class", Accent, Color.white, 80, 30);
            var change = CreateButton("Change ID Button", card.transform, "Change ID", new Color(0, 0, 0, 0), TextMuted, 44, 22);
            change.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Right;

            // ----- Wire into the controller -----
            c.studentIdPanel = idPanel;
            c.studentIdInput = input;
            c.confirmButton = save;
            c.studentIdErrorText = idError;
            c.nextClassPanel = card;
            c.headerText = header;
            c.titleText = title;
            c.roomText = room;
            c.timeText = time;
            c.countdownText = countdown;
            c.goToNextClassButton = go;
            c.changeIdButton = change;

            card.SetActive(false);
            return canvasGO;
        }

        // ---------- helpers ----------

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        static GameObject CreateTopRightPanel(string name, Transform parent)
        {
            var go = CreateImage(name, parent, CardColor);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(Width, 0);
            rt.anchoredPosition = new Vector2(-Margin.x, -Margin.y);

            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 20, 20);
            v.spacing = 8;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        static GameObject CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetHeight(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color,
            FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.alignment = TextAlignmentOptions.Left;
            t.raycastTarget = false;
            return t;
        }

        static Button CreateButton(string name, Transform parent, string label, Color bg, Color fg, float height, float fontSize)
        {
            var go = CreateImage(name, parent, bg);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            SetHeight(go, height);

            var text = CreateText("Label", go.transform, label, fontSize, fg, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform);
            return button;
        }

        static TMP_InputField CreateInputField(string name, Transform parent, string placeholder)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = InputColor;
            SetHeight(go, 76);

            var field = go.GetComponent<TMP_InputField>();
            field.contentType = TMP_InputField.ContentType.IntegerNumber; // numeric keyboard on mobile
            field.characterLimit = 20;
            field.pointSize = 32;
            if (field.textComponent) field.textComponent.color = TextDark;
            if (field.placeholder is TMP_Text ph)
            {
                ph.text = placeholder;
                ph.fontSize = 30;
            }
            return field;
        }
    }
}
