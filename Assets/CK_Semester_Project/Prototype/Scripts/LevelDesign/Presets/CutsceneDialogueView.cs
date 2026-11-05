using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 컷씬 대사창. 처음 쓸 때 화면 하단에 만들어지고 컷씬끼리 같이 쓴다.
    /// 대사의 첫 줄은 큰 본문, 줄바꿈 뒤는 작은 보조 설명으로 보인다.
    /// AUTO(Q) 자동 넘김, SKIP(E) 빨리 넘김, LOG(R) 지난 대사 보기. 대사 넘기기는 ENTER.
    /// </summary>
    public sealed class CutsceneDialogueView : MonoBehaviour
    {
        private const string ViewName = "CutsceneDialogueView";
        private const string DefaultSpeaker = "SYSTEM";
        private const int MaxLogEntries = 40;
        // 프레임 스프라이트 원본 크기. 아래 배치 좌표는 이 이미지의 픽셀 좌표(왼쪽 위 기준)다.
        private static readonly Vector2 FrameSize = new Vector2(1873f, 349f);
        // 원본 크기(1920x1080 기준 화면 가로 대부분)는 필드에서 너무 커서 전체를 줄여 쓴다.
        private const float PanelScale = 0.75f;
        // 창 아래에 AUTO/SKIP/LOG 버튼(원본 기준 60px)이 붙으므로 그만큼 띄운다.
        private const float FrameBottom = 150f;
        // 좌측 하단 미니맵과 겹치지 않도록 오른쪽으로 이동
        private const float FrameX = 140f;
        private const float ArrowBob = 4f;

        private static readonly Color CategoryColor = new Color(0.91f, 0.93f, 0.96f);
        private static readonly Color SpeakerColor = new Color(0.12f, 0.14f, 0.2f);
        private static readonly Color SubColor = new Color(0.79f, 0.82f, 0.87f);
        private static readonly Color AccentColor = new Color(0.45f, 0.93f, 1f);

        private GameObject _panel;
        private TMP_Text _categoryText;
        private TMP_Text _speakerText;
        private TMP_Text _mainText;
        private TMP_Text _subText;
        private GameObject _nextHint;
        private RectTransform _nextArrow;
        private CutsceneDialogueButton _autoButton;
        private CutsceneDialogueButton _skipButton;
        private CutsceneDialogueButton _logButton;
        private GameObject _logPanel;
        private TMP_Text _logText;
        private int _mainCount;
        private int _subCount;
        private bool _waiting;
        private readonly List<string> _log = new List<string>();

        private static CutsceneDialogueView _instance;

        /// <summary>대사가 끝나면 입력 없이 자동으로 넘긴다.</summary>
        public bool AutoMode { get; private set; }
        /// <summary>남은 대사를 빠르게 넘긴다. 컷씬이 끝나면 꺼진다.</summary>
        public bool SkipMode { get; private set; }
        /// <summary>대사 기록 창이 열려 있으면 진행을 멈춘다.</summary>
        public bool IsLogOpen => _logPanel != null && _logPanel.activeSelf;
        /// <summary>타자 효과로 보여 줄 전체 글자 수(본문+보조).</summary>
        public int CharacterCount => _mainCount + _subCount;

        public static bool IsShowing => _instance != null && _instance._panel != null && _instance._panel.activeSelf;

        public static CutsceneDialogueView GetOrCreate(TMP_FontAsset font)
        {
            if (_instance != null)
            {
                return _instance;
            }
            var root = new GameObject(ViewName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // 가로 기준으로 맞춰 대사창(1873px)이 어떤 화면비에서도 잘리지 않게 한다.
            scaler.matchWidthOrHeight = 0f;
            _instance = root.AddComponent<CutsceneDialogueView>();
            _instance.Build(CutsceneDialogueSkin.Load(), font);
            _instance.Hide();
            return _instance;
        }

        /// <summary>대사를 띄운다. text의 첫 줄은 본문, 나머지 줄은 보조 설명.</summary>
        public void Show(string speaker, string text, string category)
        {
            _panel.SetActive(true);
            _categoryText.text = string.IsNullOrEmpty(category) ? "STORY" : category;
            _speakerText.text = string.IsNullOrEmpty(speaker) ? DefaultSpeaker : speaker;

            string main = text ?? string.Empty;
            if (main.StartsWith("(임시) ")) main = main.Substring(5).TrimStart();
            else if (main.StartsWith("(임시)")) main = main.Substring(4).TrimStart();
            string sub = string.Empty;
            int lineBreak = main.IndexOf('\n');
            if (lineBreak >= 0)
            {
                sub = main.Substring(lineBreak + 1).Trim();
                main = main.Substring(0, lineBreak).TrimEnd();
            }
            _mainText.text = main;
            _subText.text = sub;
            _mainText.maxVisibleCharacters = 99999;
            _subText.maxVisibleCharacters = 99999;
            _mainText.ForceMeshUpdate();
            _subText.ForceMeshUpdate();
            _mainCount = string.IsNullOrEmpty(main) ? 0 : _mainText.textInfo.characterCount;
            _subCount = string.IsNullOrEmpty(sub) ? 0 : _subText.textInfo.characterCount;
            SetVisibleCharacters(0);
            SetWaiting(false);
            AddLog(_speakerText.text, main, sub);
        }

        public void SetVisibleCharacters(int count)
        {
            _mainText.maxVisibleCharacters = Mathf.Clamp(count, 0, _mainCount);
            _subText.maxVisibleCharacters = Mathf.Clamp(count - _mainCount, 0, _subCount);
        }

        /// <summary>입력 대기 표시(Enter ▼)를 켜고 끈다.</summary>
        public void SetWaiting(bool waiting)
        {
            _waiting = waiting;
            if (_nextHint.activeSelf != waiting)
            {
                _nextHint.SetActive(waiting);
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

        /// <summary>컷씬이 끝날 때 부른다. SKIP을 끄고 창을 닫는다.</summary>
        public static void EndSequence()
        {
            if (_instance != null)
            {
                _instance.SetSkip(false);
                _instance.Hide();
            }
        }

        public void Hide()
        {
            _panel.SetActive(false);
            _logPanel.SetActive(false);
            _waiting = false;
        }

        private void Update()
        {
            if (!_panel.activeSelf)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    ToggleLog();
                }
                else if (!IsLogOpen && keyboard.qKey.wasPressedThisFrame)
                {
                    ToggleAuto();
                }
                else if (!IsLogOpen && keyboard.eKey.wasPressedThisFrame)
                {
                    SetSkip(!SkipMode);
                }
            }
            if (_waiting)
            {
                float bob = (Mathf.Sin(Time.unscaledTime * 6f) * 0.5f + 0.5f) * ArrowBob;
                _nextArrow.anchoredPosition = new Vector2(_nextArrow.anchoredPosition.x, -258f - bob);
            }
        }

        private void ToggleAuto()
        {
            AutoMode = !AutoMode;
            _autoButton.SetOn(AutoMode);
        }

        private void SetSkip(bool on)
        {
            SkipMode = on;
            _skipButton.SetOn(on);
        }

        private void ToggleLog()
        {
            bool open = !IsLogOpen;
            if (open)
            {
                _logText.text = BuildLogText();
            }
            _logPanel.SetActive(open);
            _logButton.SetOn(open);
        }

        private void AddLog(string speaker, string main, string sub)
        {
            var entry = new StringBuilder();
            entry.Append("<color=#73EDFF><b>").Append(speaker).Append("</b></color>\n").Append(main);
            if (!string.IsNullOrEmpty(sub))
            {
                entry.Append("\n<size=80%><color=#C9D1DE>").Append(sub).Append("</color></size>");
            }
            _log.Add(entry.ToString());
            if (_log.Count > MaxLogEntries)
            {
                _log.RemoveAt(0);
            }
        }

        private string BuildLogText()
        {
            var text = new StringBuilder();
            for (int i = 0; i < _log.Count; i++)
            {
                if (i > 0)
                {
                    text.Append("\n<size=40%> </size>\n");
                }
                text.Append(_log[i]);
            }
            return text.ToString();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Build(CutsceneDialogueSkin skin, TMP_FontAsset fallbackFont)
        {
            TMP_FontAsset regular = skin != null && skin.RegularFont != null ? skin.RegularFont : fallbackFont;
            TMP_FontAsset bold = skin != null && skin.BoldFont != null ? skin.BoldFont : regular;
            TMP_FontAsset extraBold = skin != null && skin.ExtraBoldFont != null ? skin.ExtraBoldFont : bold;

            _panel = new GameObject("Panel", typeof(RectTransform));
            var panelRect = (RectTransform)_panel.transform;
            panelRect.SetParent(transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = FrameSize;
            panelRect.anchoredPosition = new Vector2(FrameX, FrameBottom);
            panelRect.localScale = Vector3.one * PanelScale;

            Image frame = CreateImage("Frame", _panel.transform, skin != null ? skin.Frame : null);
            Stretch(frame.rectTransform);
            if (frame.sprite == null)
            {
                frame.color = new Color(0.17f, 0.19f, 0.25f, 0.92f);
            }

            Image icon = CreateImage("Category Icon", _panel.transform, skin != null ? skin.CategoryIcon : null);
            Place(icon.rectTransform, 40f, 31f, 34f, 27f);
            icon.enabled = icon.sprite != null;

            _categoryText = CreateText("Category", _panel.transform, regular, 26f, CategoryColor);
            Place(_categoryText.rectTransform, 90f, 25f, 210f, 40f);
            _categoryText.alignment = TextAlignmentOptions.MidlineLeft;
            _categoryText.textWrappingMode = TextWrappingModes.NoWrap;

            _speakerText = CreateText("Speaker", _panel.transform, extraBold, 34f, SpeakerColor);
            Place(_speakerText.rectTransform, 356f, 22f, 230f, 46f);
            _speakerText.alignment = TextAlignmentOptions.MidlineLeft;
            _speakerText.textWrappingMode = TextWrappingModes.NoWrap;
            _speakerText.enableAutoSizing = true;
            _speakerText.fontSizeMin = 22f;
            _speakerText.fontSizeMax = 34f;

            _mainText = CreateText("Main", _panel.transform, extraBold, 40f, Color.white);
            Place(_mainText.rectTransform, 130f, 124f, 1580f, 60f);
            _mainText.alignment = TextAlignmentOptions.MidlineLeft;
            _mainText.enableAutoSizing = true;
            _mainText.fontSizeMin = 28f;
            _mainText.fontSizeMax = 40f;

            _subText = CreateText("Sub", _panel.transform, regular, 26f, SubColor);
            Place(_subText.rectTransform, 132f, 192f, 1560f, 70f);
            _subText.alignment = TextAlignmentOptions.TopLeft;
            _subText.lineSpacing = -4f;

            _nextHint = new GameObject("Next Hint", typeof(RectTransform));
            var hintRect = (RectTransform)_nextHint.transform;
            hintRect.SetParent(_panel.transform, false);
            Stretch(hintRect);
            TMP_Text enter = CreateText("Enter", hintRect, bold, 28f, Color.white);
            Place(enter.rectTransform, 1600f, 238f, 124f, 40f);
            enter.alignment = TextAlignmentOptions.MidlineRight;
            enter.textWrappingMode = TextWrappingModes.NoWrap;
            enter.text = "Enter";
            Image arrow = CreateImage("Arrow", hintRect, skin != null ? skin.NextArrow : null);
            _nextArrow = arrow.rectTransform;
            _nextArrow.anchorMin = new Vector2(0f, 1f);
            _nextArrow.anchorMax = new Vector2(0f, 1f);
            _nextArrow.pivot = new Vector2(0.5f, 0.5f);
            _nextArrow.sizeDelta = new Vector2(26f, 23f);
            _nextArrow.anchoredPosition = new Vector2(1754f, -258f);
            if (arrow.sprite == null)
            {
                arrow.color = AccentColor;
            }

            _autoButton = CreateButton("Auto Button", "AUTO [Q]", regular, 1371f, 56f, skin != null ? skin.AutoNormal : null, skin != null ? skin.AutoHover : null);
            _skipButton = CreateButton("Skip Button", "SKIP [E]", regular, 1535f, 70f, skin != null ? skin.SkipNormal : null, skin != null ? skin.SkipHover : null);
            _logButton = CreateButton("Log Button", "LOG [R]", regular, 1699f, 70f, skin != null ? skin.LogNormal : null, skin != null ? skin.LogHover : null);
            _autoButton.Clicked += ToggleAuto;
            _skipButton.Clicked += () => SetSkip(!SkipMode);
            _logButton.Clicked += ToggleLog;
            _autoButton.SetOn(AutoMode);

            BuildLog(skin, regular, extraBold);
        }

        private void BuildLog(CutsceneDialogueSkin skin, TMP_FontAsset regular, TMP_FontAsset extraBold)
        {
            _logPanel = new GameObject("Log", typeof(RectTransform), typeof(Image));
            var logRect = (RectTransform)_logPanel.transform;
            logRect.SetParent(transform, false);
            Stretch(logRect);
            _logPanel.GetComponent<Image>().color = new Color(0f, 0.01f, 0.05f, 0.6f);

            Image frame = CreateImage("Frame", logRect, skin != null ? skin.LogFrame : null);
            frame.type = Image.Type.Sliced;
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(1400f, 760f);
            frameRect.anchoredPosition = new Vector2(0f, 20f);
            if (frame.sprite == null)
            {
                frame.color = new Color(0.17f, 0.19f, 0.25f, 0.96f);
            }

            TMP_Text title = CreateText("Title", frameRect, extraBold, 40f, Color.white);
            Place(title.rectTransform, 70f, 40f, 500f, 56f);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.text = "<color=#73EDFF>|</color>  LOG";

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.SetParent(frameRect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(80f, 70f);
            viewportRect.offsetMax = new Vector2(-90f, -120f);

            _logText = CreateText("Entries", viewportRect, regular, 28f, Color.white);
            Stretch(_logText.rectTransform);
            // 아래 정렬 + 마스크라서 넘치면 오래된 대사부터 위로 잘린다.
            _logText.alignment = TextAlignmentOptions.BottomLeft;
            _logText.overflowMode = TextOverflowModes.Overflow;
            _logText.lineSpacing = 6f;

            TMP_Text close = CreateText("Close Hint", frameRect, regular, 22f, SubColor);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(1f, 0f);
            close.rectTransform.pivot = new Vector2(1f, 0f);
            close.rectTransform.sizeDelta = new Vector2(400f, 36f);
            close.rectTransform.anchoredPosition = new Vector2(-70f, 24f);
            close.alignment = TextAlignmentOptions.MidlineRight;
            close.text = "R  닫기";

            _logPanel.SetActive(false);
        }

        private CutsceneDialogueButton CreateButton(string objectName, string label, TMP_FontAsset font, float x, float labelX, Sprite normal, Sprite hover)
        {
            Image image = CreateImage(objectName, _panel.transform, normal);
            image.raycastTarget = true;
            Place(image.rectTransform, x, 345f, 166f, 64f);
            TMP_Text text = CreateText("Label", image.rectTransform, font, 19f, Color.white);
            // 스프라이트마다 아이콘 폭이 달라 글자 시작 위치를 따로 준다.
            Place(text.rectTransform, labelX, 14f, 150f - labelX, 36f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = label;
            var button = image.gameObject.AddComponent<CutsceneDialogueButton>();
            button.Setup(image, text, normal, hover);
            return button;
        }

        /// <summary>부모의 왼쪽 위를 기준으로 프레임 픽셀 좌표(x, y=위에서 아래)에 놓는다.</summary>
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(string objectName, Transform parent, Sprite sprite)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(string objectName, Transform parent, TMP_FontAsset font, float size, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = size;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }
    }
}
