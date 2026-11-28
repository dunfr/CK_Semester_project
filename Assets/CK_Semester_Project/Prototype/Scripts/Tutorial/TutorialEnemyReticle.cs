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

        // 몬스터 모델은 루트와 몸통 위치가 어긋날 수 있어 실제 몸통 메시 경계 중심을 몬스터 기준 오프셋(XZ)으로 잰다.
        // 정점이 가장 많은 스킨 메시를 몸통으로 본다. 무기·머리카락 메시가 중심을 치우치지 않게 한다.
        // 타겟을 고를 때 한 번만 재고 이후에는 고정해서, 애니메이션에 조준점이 흔들리지 않게 한다.
        public static Vector3 MeasureBodyOffset(Transform target)
        {
            SkinnedMeshRenderer body = null;
            int bestVertexCount = 0;
            foreach (SkinnedMeshRenderer skin in target.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin.enabled && skin.sharedMesh != null && skin.sharedMesh.vertexCount > bestVertexCount)
                {
                    body = skin;
                    bestVertexCount = skin.sharedMesh.vertexCount;
                }
            }
            if (body == null)
            {
                return Vector3.zero;
            }
            Vector3 local = target.InverseTransformPoint(body.bounds.center);
            return new Vector3(local.x, 0f, local.z);
        }

        // 높이는 몬스터 기준점(발밑)에서 잰다.
        public static Vector3 GetAimPoint(Transform target, Vector3 bodyOffset, float height)
        {
            Vector3 center = target.TransformPoint(bodyOffset);
            return new Vector3(center.x, target.position.y + height, center.z);
        }

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
