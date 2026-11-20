using System;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialSpotlightGraphic : Graphic
    {
        public enum OutlineShape { RoundedRectangle, Ellipse, Circle }

        [Serializable]
        public struct Style
        {
            public OutlineShape Shape;
            public Color GlowColor;
            [Range(0f, 1f)] public float Intensity;
            [Min(1f)] public float LineWidth;
            [Min(0f)] public float GlowWidth;
            [Min(0f)] public float CornerRadius;

            public static Style Mint => new Style
            {
                GlowColor = new Color(0.28f, 1f, 0.82f),
                Intensity = 0.9f,
                LineWidth = 2.5f,
                GlowWidth = 14f,
                CornerRadius = 12f,
            };
        }

        private const int SegmentsPerCorner = 16;
        private const int PointCount = 4 * (SegmentsPerCorner + 1);
        private static readonly Vector2[] Directions = CreateDirections();

        private Style _style;
        private Vector2 _halfSize;
        private float _cornerRadius;
        private float _extent;
        private float _dim;
        private float _entrance;
        private float _elapsed;

        public void SetFrame(Vector2 halfSize, float extent, Style style, float dim, float entrance, float elapsed)
        {
            _style = style;
            _cornerRadius = Mathf.Clamp(style.CornerRadius, 0f, Mathf.Min(halfSize.x, halfSize.y));
            _halfSize = style.Shape switch
            {
                OutlineShape.Circle => Vector2.one * halfSize.magnitude,
                OutlineShape.Ellipse => halfSize * Mathf.Sqrt(2f),
                _ => halfSize + Vector2.one * (_cornerRadius * (1f - Mathf.Sqrt(0.5f))),
            };
            _extent = extent;
            _dim = dim;
            _entrance = entrance;
            _elapsed = elapsed;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Color dim = new Color(0f, 0f, 0f, _dim);
            // An oversized outer contour covers the canvas even when the cutout is rotated.
            AddBand(mesh, 0f, _extent * 1.1f, dim, dim);
            if (_style.Intensity <= 0f || _style.GlowColor.a <= 0f)
            {
                return;
            }

            float approach = 1f - _entrance;
            float spread = Mathf.Min(_halfSize.x, _halfSize.y) * 0.75f + 22f;
            float sweep = approach * approach * spread;
            float arrival = Mathf.Clamp01(_entrance * 5f);
            float pulse = 0.85f + 0.15f * Mathf.Cos(_elapsed * 5f);
            float brightness = _style.Intensity * arrival * Mathf.Lerp(1f, pulse, _entrance);
            float line = Mathf.Max(1f, _style.LineWidth);
            float glow = _style.GlowWidth;
            Color core = Color.Lerp(_style.GlowColor, Color.white, 0.45f);
            core.a = _style.GlowColor.a * brightness;
            Color halo = _style.GlowColor;
            halo.a *= brightness * 0.52f;
            Color clear = new Color(halo.r, halo.g, halo.b, 0f);

            AddBand(mesh, Mathf.Max(0f, sweep - glow * 0.45f), sweep + line * 0.5f, clear, halo);
            AddBand(mesh, sweep + line * 0.5f, sweep + line + glow, halo, clear);
            AddBand(mesh, sweep, sweep + line, core, core);
            if (approach > 0f)
            {
                Color echo = core;
                echo.a *= approach * 0.65f;
                float echoOffset = sweep + (glow + spread * 0.28f) * approach;
                AddBand(mesh, echoOffset, echoOffset + line * 0.55f, echo, echo);
            }
        }

        private void AddBand(VertexHelper mesh, float inner, float outer, Color innerColor, Color outerColor)
        {
            int start = mesh.currentVertCount;
            for (int index = 0; index < PointCount; index++)
            {
                mesh.AddVert(GetPoint(index, inner), innerColor, Vector2.zero);
                mesh.AddVert(GetPoint(index, outer), outerColor, Vector2.zero);
                int current = start + index * 2;
                int next = start + (index + 1) % PointCount * 2;
                mesh.AddTriangle(current, next, current + 1);
                mesh.AddTriangle(current + 1, next, next + 1);
            }
        }

        private Vector2 GetPoint(int index, float offset)
        {
            Vector2 direction = Directions[index];
            if (_style.Shape != OutlineShape.RoundedRectangle)
            {
                return Vector2.Scale(direction, _halfSize + Vector2.one * offset);
            }

            int quadrant = index / (SegmentsPerCorner + 1);
            Vector2 corner = _halfSize - Vector2.one * _cornerRadius;
            if (quadrant == 1 || quadrant == 2)
            {
                corner.x = -corner.x;
            }
            if (quadrant >= 2)
            {
                corner.y = -corner.y;
            }
            return corner + direction * (_cornerRadius + offset);
        }

        private static Vector2[] CreateDirections()
        {
            var directions = new Vector2[PointCount];
            for (int index = 0; index < directions.Length; index++)
            {
                int quadrant = index / (SegmentsPerCorner + 1);
                int segment = index % (SegmentsPerCorner + 1);
                float angle = (quadrant + (float)segment / SegmentsPerCorner) * Mathf.PI * 0.5f;
                directions[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return directions;
        }
    }
}
