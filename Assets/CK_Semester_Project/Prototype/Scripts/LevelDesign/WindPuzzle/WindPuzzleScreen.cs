using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.LevelDesign.WindPuzzle
{
    /// <summary>
    /// 퍼즐 조작 중에 띄우는 화면 UI. 기획 프로토타입(HTML)과 같은 색 체계로 판을 크게 그린다.
    /// 칸·버튼 클릭은 콜백으로 퍼즐 장치에 넘긴다.
    /// </summary>
    public sealed class WindPuzzleScreen
    {
        public static readonly Color Background = Hex(0x15171c, 0.86f);
        public static readonly Color Panel = Hex(0x1e2128);
        public static readonly Color CellColor = Hex(0x252932);
        public static readonly Color Line = Hex(0x3a4050);
        public static readonly Color Text = Hex(0xe6e8ec);
        public static readonly Color SubText = Hex(0x9aa1ad);
        public static readonly Color Gray = Hex(0x8b929e);
        public static readonly Color Orange = Hex(0xf08a24);
        public static readonly Color Vent = Hex(0x2f7fd6);
        public static readonly Color Wind = Hex(0x8fe3ff);
        public static readonly Color BoxColor = Hex(0xc99a5b);
        public static readonly Color ExitColor = Hex(0x43c463);
        public static readonly Color Highlight = Hex(0xffd34d);

        private const float CellPx = 100f;
        private const float PipePx = 32f;
        private const float TopSpace = 230f;    // 제목·부제목 + 위쪽 송풍구/출구 자리
        private const float BottomSpace = 300f; // 아래쪽 송풍구 + 버튼 + 안내 문구

        private readonly WindPuzzleLevel _level;
        private readonly TMP_FontAsset _font;
        private readonly GameObject _root;
        private readonly RectTransform _board;
        private readonly RectTransform[,] _pipes;
        private readonly List<RectTransform> _boxes = new List<RectTransform>();
        private readonly Dictionary<string, Image> _buttons = new Dictionary<string, Image>();
        private readonly TextMeshProUGUI _status;
        private readonly RectTransform _windLayer;

        public event Action<int, int> CellClicked;
        public event Action<string> ButtonClicked;

        public WindPuzzleScreen(WindPuzzleLevel level, TMP_FontAsset font, string title)
        {
            _level = level;
            _font = font;
            _root = new GameObject("WindPuzzle_Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            Image dim = Rect("Dim", _root.transform, Vector2.zero, Vector2.zero, Background, true);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;

            float boardW = level.W * CellPx, boardH = level.H * CellPx;
            float panelW = Mathf.Max(boardW + 340f, 860f), panelH = boardH + TopSpace + BottomSpace;
            float boardY = (BottomSpace - TopSpace) * 0.5f;
            Image panel = Rect("Panel", _root.transform, Vector2.zero, new Vector2(panelW, panelH), Panel);
            Outline(panel.rectTransform, Hex(0x5fd0ff, 0.55f), 3f);
            Label(panel.transform, title, new Vector2(0f, panelH * 0.5f - 48f), 44f, Text, FontStyles.Bold, new Vector2(panelW - 40f, 56f));
            Label(panel.transform, "바람으로 상자를 <color=#43c463>초록 출구</color>까지 밀어내세요", new Vector2(0f, panelH * 0.5f - 96f), 26f, SubText, FontStyles.Bold, new Vector2(panelW - 40f, 36f));

            Image boardBg = Rect("Board", panel.transform, new Vector2(0f, boardY), new Vector2(boardW + 16f, boardH + 16f), Line);
            _board = Rect("Cells", boardBg.transform, Vector2.zero, new Vector2(boardW, boardH), Color.clear).rectTransform;
            _pipes = new RectTransform[level.H, level.W];
            for (int y = 0; y < level.H; y++)
            {
                for (int x = 0; x < level.W; x++)
                {
                    Image cell = Rect("Cell_" + x + "_" + y, _board, CellCenter(x + 0.5f, y + 0.5f), Vector2.one * (CellPx - 6f), CellColor, true);
                    int cx = x, cy = y;
                    var button = cell.gameObject.AddComponent<Button>();
                    button.transition = Selectable.Transition.ColorTint;
                    ColorBlock colors = button.colors;
                    colors.highlightedColor = new Color(1.25f, 1.25f, 1.35f, 1f);
                    colors.pressedColor = new Color(1.5f, 1.5f, 1.6f, 1f);
                    button.colors = colors;
                    button.onClick.AddListener(() => CellClicked?.Invoke(cx, cy));
                }
            }
            foreach (WindPuzzleLevel.Exit exit in level.Exits)
            {
                Vector2 dir = new Vector2(WindPuzzleEngine.DX[exit.Side], -WindPuzzleEngine.DY[exit.Side]);
                Vector2 c = CellCenter(exit.X + 0.5f, exit.Y + 0.5f) + dir * (CellPx * 0.5f + 34f);
                Arrow(_board, c, dir, ExitColor, 54f);
                Label(_board, "EXIT", c + dir * 52f, 26f, ExitColor, FontStyles.Bold, new Vector2(110f, 34f));
            }
            foreach (WindPuzzleLevel.Vent vent in level.Vents)
            {
                Vector2 dir = new Vector2(WindPuzzleEngine.DX[vent.Side], -WindPuzzleEngine.DY[vent.Side]);
                Vector2 c = CellCenter(vent.X + 0.5f, vent.Y + 0.5f) + dir * (CellPx * 0.5f + 22f);
                bool horizontal = vent.Side == 0 || vent.Side == 2;
                Rect("Vent_" + vent.Id, _board, c, horizontal ? new Vector2(72f, 30f) : new Vector2(30f, 72f), Vent);
                Arrow(_board, c - dir * 2f, -dir, Hex(0xbfe0ff), 22f);
                Image badge = Rect("VentBadge_" + vent.Id, _board, c + dir * 40f, new Vector2(38f, 38f), Vent);
                Label(badge.transform, vent.Id.ToString(), Vector2.zero, 26f, Color.white, FontStyles.Bold, new Vector2(38f, 38f));
            }
            _windLayer = Rect("Wind", _board, Vector2.zero, new Vector2(boardW, boardH), Color.clear).rectTransform;

            float buttonY = boardY - boardH * 0.5f - 150f;
            for (int i = 0; i < level.ButtonKeys.Count; i++)
            {
                string key = level.ButtonKeys[i];
                float x = (i - (level.ButtonKeys.Count - 1) * 0.5f) * 200f;
                Image b = Rect("Button_" + key, panel.transform, new Vector2(x, buttonY), new Vector2(180f, 96f), Vent, true);
                Outline(b.rectTransform, Hex(0xbfe0ff, 0.6f), 2f);
                var button = b.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => ButtonClicked?.Invoke(key));
                Label(b.transform, key, new Vector2(0f, 14f), 44f, Color.white, FontStyles.Bold, new Vector2(170f, 50f));
                Label(b.transform, "송풍구 " + string.Join(", ", Array.ConvertAll(level.Buttons[key], v => v.ToString())), new Vector2(0f, -26f), 22f, Hex(0xdcecff), FontStyles.Bold, new Vector2(170f, 30f));
                _buttons[key] = b;
            }
            _status = Label(panel.transform, "", new Vector2(0f, -panelH * 0.5f + 34f), 22f, SubText, FontStyles.Bold, new Vector2(panelW - 30f, 32f));
            SetStatus("클릭: <color=#f08a24>주황 관</color> 회전   ·   " + string.Join("/", level.ButtonKeys) + ": 송풍   ·   Z: 되돌리기   ·   R: 처음부터   ·   Esc: 나가기");
            _root.SetActive(false);
        }

        public void SetVisible(bool visible) => _root.SetActive(visible);

        public void Destroy() => UnityEngine.Object.Destroy(_root);

        public void SetStatus(string text) => _status.text = text;

        public void SetButtonActive(string key, bool active)
        {
            if (_buttons.TryGetValue(key, out Image image))
            {
                image.color = active ? Wind : Vent;
            }
        }

        public void Refresh(WindPuzzleState state)
        {
            for (int y = 0; y < _level.H; y++)
            {
                for (int x = 0; x < _level.W; x++)
                {
                    RefreshPipe(state, x, y);
                }
            }
            RefreshBoxes(state);
        }

        public void RefreshPipe(WindPuzzleState state, int x, int y)
        {
            if (_pipes[y, x] != null)
            {
                UnityEngine.Object.Destroy(_pipes[y, x].gameObject);
            }
            var root = new GameObject("Pipe_" + x + "_" + y, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_board, false);
            root.anchoredPosition = CellCenter(x + 0.5f, y + 0.5f);
            root.sizeDelta = Vector2.one * CellPx;
            _pipes[y, x] = root;
            // 관 위의 바람 줄기·상자가 클릭을 막지 않도록, 관 그래픽은 칸 바로 위에 둔다.
            root.SetSiblingIndex(_level.W * _level.H);
            char c = state.Grid[y, x];
            int mask = WindPuzzleEngine.Mask(c);
            if (mask == 0)
            {
                return;
            }
            bool bend = WindPuzzleEngine.IsBend(c);
            Color body = bend ? Orange : Gray;
            Color core = bend ? Hex(0xffc27a) : Hex(0xc3c8d0);
            for (int d = 0; d < 4; d++)
            {
                if ((mask & WindPuzzleEngine.Bit(d)) == 0)
                {
                    continue;
                }
                Vector2 dir = new Vector2(WindPuzzleEngine.DX[d], -WindPuzzleEngine.DY[d]);
                bool horizontal = d == 1 || d == 3;
                float length = CellPx * 0.5f + PipePx * 0.5f;
                Vector2 pos = dir * (CellPx * 0.25f - PipePx * 0.25f + 1f);
                Rect("Arm", root, pos, horizontal ? new Vector2(length, PipePx) : new Vector2(PipePx, length), body);
                Rect("Core", root, pos, horizontal ? new Vector2(length, PipePx * 0.34f) : new Vector2(PipePx * 0.34f, length), core);
            }
            Rect("Joint", root, Vector2.zero, Vector2.one * PipePx, body);
            Rect("JointCore", root, Vector2.zero, Vector2.one * PipePx * 0.34f, core);
        }

        public void RefreshBoxes(WindPuzzleState state)
        {
            while (_boxes.Count < state.Boxes.Length)
            {
                Image box = Rect("Box", _board, Vector2.zero, new Vector2(70f, 70f), Hex(0x6b4a24));
                Rect("BoxFace", box.transform, Vector2.zero, new Vector2(60f, 60f), BoxColor);
                Rect("StrapA", box.transform, Vector2.zero, new Vector2(78f, 9f), Hex(0x6b4a24)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Rect("StrapB", box.transform, Vector2.zero, new Vector2(78f, 9f), Hex(0x6b4a24)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                _boxes.Add(box.rectTransform);
            }
            for (int i = 0; i < state.Boxes.Length; i++)
            {
                RectTransform box = _boxes[i];
                box.SetAsLastSibling();
                bool present = state.Boxes[i].HasValue;
                box.gameObject.SetActive(present);
                box.localScale = Vector3.one;
                if (present)
                {
                    box.anchoredPosition = CellCenter(state.Boxes[i].Value.x + 0.5f, state.Boxes[i].Value.y + 0.5f);
                }
            }
            _windLayer.SetAsLastSibling();
        }

        public void PlaceBox(int index, Vector2 gridPoint, float scale)
        {
            RectTransform box = _boxes[index];
            box.gameObject.SetActive(true);
            box.anchoredPosition = CellCenter(gridPoint.x, gridPoint.y);
            box.localScale = Vector3.one * scale;
        }

        /// <summary>바람 줄기 점들을 만든다. RevealWind로 길이를 늘린다.</summary>
        public List<Image> CreateWind(List<Vector2> points)
        {
            var dots = new List<Image>();
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = CellCenter(points[i - 1].x, points[i - 1].y), b = CellCenter(points[i].x, points[i].y);
                Vector2 delta = b - a;
                Image seg = Rect("WindSeg", _windLayer, (a + b) * 0.5f, new Vector2(delta.magnitude + 10f, 14f), Wind);
                seg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                seg.gameObject.SetActive(false);
                dots.Add(seg);
            }
            return dots;
        }

        public static void RevealWind(List<Image> segments, float t)
        {
            int visible = Mathf.CeilToInt(t * segments.Count);
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].gameObject.SetActive(i < visible);
            }
        }

        public static void ClearWind(List<Image> segments)
        {
            foreach (Image seg in segments)
            {
                if (seg != null)
                {
                    UnityEngine.Object.Destroy(seg.gameObject);
                }
            }
        }

        public void FlashSolved()
        {
            SetStatus("<color=#43c463>바람길 개방!</color>");
        }

        // ------------------------------------------------------------------ helpers

        private Vector2 CellCenter(float gx, float gy) => new Vector2((gx - _level.W * 0.5f) * CellPx, (_level.H * 0.5f - gy) * CellPx);

        private static Color Hex(int rgb, float alpha = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

        private static Image Rect(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static void Outline(RectTransform target, Color color, float width)
        {
            var outline = target.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
        }

        private static void Arrow(Transform parent, Vector2 center, Vector2 dir, Color color, float size)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 back = -dir * size * 0.35f;
            Vector2 side = new Vector2(-dir.y, dir.x);
            Image a = Rect("ArrowA", parent, center + back * 0.5f + side * size * 0.2f, new Vector2(size * 0.62f, size * 0.2f), color);
            a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 40f);
            Image b = Rect("ArrowB", parent, center + back * 0.5f - side * size * 0.2f, new Vector2(size * 0.62f, size * 0.2f), color);
            b.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + 40f);
            Image shaft = Rect("ArrowShaft", parent, center - dir * size * 0.3f, new Vector2(size * 0.7f, size * 0.18f), color);
            shaft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private TextMeshProUGUI Label(Transform parent, string text, Vector2 position, float size, Color color, FontStyles style, Vector2 box)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                tmp.font = _font;
            }
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }
    }
}
