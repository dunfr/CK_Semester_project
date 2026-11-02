using System;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.UIAnimation.EditorTools
{
    public static class UIEaseGraph
    {
        private const int Samples = 64;

        private static readonly Vector3[] Points = new Vector3[Samples];
        private static readonly Color LineColor = new Color(0.36f, 0.72f, 1f);
        private static readonly Color GuideColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);

        public static string Label(UIEase ease)
        {
            if (ease == UIEase.Custom)
            {
                return "직접 그리기";
            }
            string name = ease.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static void Draw(Rect rect, Func<float, float> curve)
        {
            float low = 0f;
            float high = 1f;
            for (int index = 0; index < Samples; index++)
            {
                float value = curve(index / (Samples - 1f));
                low = Mathf.Min(low, value);
                high = Mathf.Max(high, value);
                Points[index].y = value;
            }

            float Y(float value) => Mathf.LerpUnclamped(rect.yMax, rect.y, Mathf.InverseLerp(low, high, value));
            EditorGUI.DrawRect(new Rect(rect.x, Y(0f), rect.width, 1f), GuideColor);
            EditorGUI.DrawRect(new Rect(rect.x, Y(1f), rect.width, 1f), GuideColor);
            for (int index = 0; index < Samples; index++)
            {
                Points[index].x = Mathf.Lerp(rect.x, rect.xMax, index / (Samples - 1f));
                Points[index].y = Y(Points[index].y);
            }

            Handles.color = LineColor;
            Handles.DrawAAPolyLine(2f, Points);
            Handles.color = Color.white;
        }
    }

    // easings.net처럼 곡선 모양을 보고 고르는 팝업이다.
    public sealed class UIEasePicker : PopupWindowContent
    {
        private const float Padding = 8f;
        private const float CellSize = 62f;
        private const float RowLabelWidth = 46f;
        private const float HeaderHeight = 18f;
        private const float FooterHeight = 22f;

        private static readonly string[] Families =
            { "Sine", "Quad", "Cubic", "Quart", "Quint", "Expo", "Circ", "Back", "Elastic", "Bounce" };
        private static readonly string[] Kinds = { "In", "Out", "InOut" };

        private readonly UIEase _current;
        private readonly Action<UIEase> _picked;
        private string _hint = string.Empty;

        public UIEasePicker(UIEase current, Action<UIEase> picked)
        {
            _current = current;
            _picked = picked;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(
                Padding * 2f + RowLabelWidth + CellSize * Families.Length,
                Padding * 2f + HeaderHeight + CellSize * (Kinds.Length + 1) + FooterHeight);
        }

        public override void OnOpen()
        {
            editorWindow.wantsMouseMove = true;
        }

        public override void OnGUI(Rect rect)
        {
            _hint = string.Empty;
            float left = Padding + RowLabelWidth;
            float top = Padding + HeaderHeight;

            for (int family = 0; family < Families.Length; family++)
            {
                GUI.Label(new Rect(left + CellSize * family, Padding, CellSize, HeaderHeight), Families[family], EditorStyles.centeredGreyMiniLabel);
            }
            for (int kind = 0; kind < Kinds.Length; kind++)
            {
                GUI.Label(new Rect(Padding, top + CellSize * kind, RowLabelWidth, CellSize), Kinds[kind], EditorStyles.miniLabel);
                for (int family = 0; family < Families.Length; family++)
                {
                    DrawCell(new Rect(left + CellSize * family, top + CellSize * kind, CellSize, CellSize), (UIEase)(1 + family * Kinds.Length + kind));
                }
            }

            float extra = top + CellSize * Kinds.Length;
            GUI.Label(new Rect(Padding, extra, RowLabelWidth, CellSize), "기타", EditorStyles.miniLabel);
            DrawCell(new Rect(left, extra, CellSize, CellSize), UIEase.Linear);
            DrawCell(new Rect(left + CellSize, extra, CellSize, CellSize), UIEase.Custom);

            GUI.Label(new Rect(Padding, rect.height - Padding - FooterHeight, rect.width - Padding * 2f, FooterHeight), _hint);
            if (Event.current.type == EventType.MouseMove)
            {
                editorWindow.Repaint();
            }
        }

        private void DrawCell(Rect rect, UIEase ease)
        {
            Rect inner = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (hovered)
            {
                _hint = UIEasing.Overshoots(ease)
                    ? $"{UIEaseGraph.Label(ease)}  (0~1을 넘었다 돌아옵니다. 색은 0~1로 잘립니다)"
                    : UIEaseGraph.Label(ease);
            }

            if (Event.current.type == EventType.Repaint)
            {
                Color background = ease == _current
                    ? new Color(0.24f, 0.48f, 0.9f, 0.45f)
                    : new Color(0.5f, 0.5f, 0.5f, hovered ? 0.3f : 0.12f);
                EditorGUI.DrawRect(inner, background);
                if (ease == UIEase.Custom)
                {
                    GUI.Label(inner, "직접\n그리기", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    Rect graph = new Rect(inner.x + 5f, inner.y + 8f, inner.width - 10f, inner.height - 16f);
                    UIEaseGraph.Draw(graph, t => UIEasing.Evaluate(ease, t));
                }
            }

            if (Event.current.type == EventType.MouseDown && hovered)
            {
                _picked(ease);
                editorWindow.Close();
                Event.current.Use();
            }
        }
    }
}
