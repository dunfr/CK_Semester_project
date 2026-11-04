using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>컷씬 대사창. 처음 쓸 때 화면 하단에 만들어지고 컷씬끼리 같이 쓴다.</summary>
    public sealed class CutsceneDialogueView : MonoBehaviour
    {
        private const string ViewName = "CutsceneDialogueView";

        private GameObject _panel;
        private TMP_Text _speakerText;
        private TMP_Text _bodyText;
        private TMP_Text _nextHint;
        private bool _nextHintRequested;

        private static CutsceneDialogueView _instance;

        public TMP_Text Body => _bodyText;

        public static CutsceneDialogueView GetOrCreate(TMP_FontAsset font)
        {
            if (_instance != null)
            {
                return _instance;
            }
            var root = new GameObject(ViewName, typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _instance = root.AddComponent<CutsceneDialogueView>();
            _instance.Build(font);
            _instance.Hide();
            return _instance;
        }

        public void Show(string speaker, string body, bool showNextHint)
        {
            _panel.SetActive(true);
            _speakerText.text = speaker;
            _speakerText.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            _bodyText.text = body;
            _bodyText.maxVisibleCharacters = 0;
            _nextHint.gameObject.SetActive(false);
            _nextHintRequested = showNextHint;
        }

        public void SetVisibleCharacters(int count)
        {
            _bodyText.maxVisibleCharacters = count;
            if (count >= _bodyText.text.Length && _nextHintRequested)
            {
                _nextHint.gameObject.SetActive(true);
            }
        }

        /// <summary>대사창이 이미 만들어져 있을 때만 숨긴다(씬 종료 중 새로 만들지 않도록).</summary>
        public static void HideActive()
        {
            if (_instance != null)
            {
                _instance.Hide();
            }
        }

        public void Hide()
        {
            _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Build(TMP_FontAsset font)
        {
            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var panelRect = (RectTransform)_panel.transform;
            panelRect.SetParent(transform, false);
            panelRect.anchorMin = new Vector2(0.18f, 0.04f);
            panelRect.anchorMax = new Vector2(0.82f, 0.24f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.12f, 0.88f);

            _speakerText = CreateText("Speaker", font, 34f, FontStyles.Bold, new Color(0.45f, 0.85f, 1f));
            var speakerRect = _speakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0f, 1f);
            speakerRect.offsetMin = new Vector2(36f, -64f);
            speakerRect.offsetMax = new Vector2(-36f, -14f);

            _bodyText = CreateText("Body", font, 32f, FontStyles.Normal, Color.white);
            var bodyRect = _bodyText.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(36f, 24f);
            bodyRect.offsetMax = new Vector2(-36f, -70f);
            _bodyText.alignment = TextAlignmentOptions.TopLeft;

            _nextHint = CreateText("NextHint", font, 24f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.7f));
            var hintRect = _nextHint.rectTransform;
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.sizeDelta = new Vector2(220f, 40f);
            hintRect.anchoredPosition = new Vector2(-24f, 14f);
            _nextHint.alignment = TextAlignmentOptions.BottomRight;
            _nextHint.text = "F  ▶";
        }

        private TMP_Text CreateText(string objectName, TMP_FontAsset font, float size, FontStyles style, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(_panel.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }
    }
}
