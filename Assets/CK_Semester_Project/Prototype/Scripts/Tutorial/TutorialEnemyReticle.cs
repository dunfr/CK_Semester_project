using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialEnemyReticle : Graphic, IPointerClickHandler
    {
        private const float EntranceSeconds = 0.24f;
        private Color _tint;
        private float _glow;
        private float _elapsed;
        private float _radius;

        public event Action Clicked;

        public void SetFrame(float diameter, Color tint, float glow, float elapsed)
        {
            rectTransform.sizeDelta = Vector2.one * diameter;
            _radius = diameter * 0.5f;
            _tint = tint;
            _glow = glow;
            _elapsed = elapsed;
            SetVerticesDirty();
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            return base.Raycast(screenPoint, eventCamera)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out Vector2 point)
                && point.sqrMagnitude <= _radius * _radius;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float entry = Mathf.Clamp01(_elapsed / EntranceSeconds);
            float wave = 0.5f + 0.5f * Mathf.Sin(_elapsed * 6f);
            float radius = _radius * Mathf.Lerp(1.26f, 0.82f + 0.025f * wave, Mathf.SmoothStep(0f, 1f, entry));
            Color core = Color.Lerp(_tint, Color.white, 0.63f);
            core.a = _tint.a * entry;
            Color glow = _tint;
            glow.a *= _glow * entry * (0.38f + 0.16f * wave);
            Color clear = new Color(glow.r, glow.g, glow.b, 0f);
            float halo = _radius * 0.22f;
            AddArc(mesh, 0f, 360f, radius - halo, radius, clear, glow);
            AddArc(mesh, 0f, 360f, radius, radius + halo, glow, clear);
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                float angle = quadrant * 90f;
                AddArc(mesh, angle + 11f, angle + 79f, radius - _radius * 0.025f,
                    radius + _radius * 0.025f, core, core);
            }

            float center = _radius * 0.16f;
            AddArc(mesh, 0f, 360f, center, center + _radius * 0.043f, core, core);
            AddArc(mesh, 0f, 360f, center + _radius * 0.043f, center + _radius * 0.16f, glow, clear);
            AddPointer(mesh, radius, 1f, core);
            AddPointer(mesh, radius, -1f, core);
            AddDot(mesh, radius * Vector2.up, core);
            AddDot(mesh, radius * Vector2.down, core);
        }

        private static void AddArc(VertexHelper mesh, float start, float end, float inner, float outer,
            Color innerColor, Color outerColor, Vector2 center = default)
        {
            int segments = Mathf.CeilToInt((end - start) / 6f);
            int first = mesh.currentVertCount;
            for (int index = 0; index <= segments; index++)
            {
                float angle = Mathf.Lerp(start, end, (float)index / segments) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * inner, innerColor, Vector2.zero);
                mesh.AddVert(center + direction * outer, outerColor, Vector2.zero);
                if (index > 0)
                {
                    int previous = first + (index - 1) * 2;
                    mesh.AddTriangle(previous, previous + 2, previous + 1);
                    mesh.AddTriangle(previous + 1, previous + 2, previous + 3);
                }
            }
        }

        private void AddPointer(VertexHelper mesh, float radius, float side, Color color)
        {
            int first = mesh.currentVertCount;
            float span = _radius * 0.10f;
            mesh.AddVert(new Vector2(side * (radius - span), 0f), color, Vector2.zero);
            mesh.AddVert(new Vector2(side * (radius + span), side * span), color, Vector2.zero);
            mesh.AddVert(new Vector2(side * (radius + span), -side * span), color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
        }

        private void AddDot(VertexHelper mesh, Vector2 center, Color color)
        {
            AddArc(mesh, 0f, 360f, 0f, _radius * 0.045f, color, color, center);
        }
    }
}
