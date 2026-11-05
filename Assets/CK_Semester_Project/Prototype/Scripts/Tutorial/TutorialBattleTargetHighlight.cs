using System;
using System.Linq;
using CK.SemesterProject.Battle;
using UnityEngine;
using UnityEngine.Rendering;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 전투 중 몬스터를 클릭/타겟팅했을 때 발밑 타겟 링·충격파 연출과 렌더러 하이라이트를 표시한다.
    /// </summary>
    public sealed class TutorialBattleTargetHighlight : IDisposable
    {
        private const int CircleSegments = 48;
        private const int ArcSegments = 14;
        private const int ArcCount = 4;
        private const float PulseDuration = 0.35f;

        private readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private readonly int _colorId = Shader.PropertyToID("_Color");
        private readonly int _rimColorId = Shader.PropertyToID("_RimColor");
        private readonly int _rimStrengthId = Shader.PropertyToID("_RimStrength");
        private readonly int _rimPowerId = Shader.PropertyToID("_RimPower");
        private readonly int _depthRimColorId = Shader.PropertyToID("_DepthRimColor");
        private readonly int _depthRimStrengthId = Shader.PropertyToID("_DepthRimStrength");
        private readonly int _outlineColorId = Shader.PropertyToID("_OutlineColor");
        private readonly int _outlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private readonly int _litBrightnessId = Shader.PropertyToID("_LitBrightness");

        private GameObject _root;
        private LineRenderer _innerRing;
        private LineRenderer _shockwaveRing;
        private readonly LineRenderer[] _outerArcs = new LineRenderer[ArcCount];
        private Material _lineMaterial;

        private Transform _currentTarget;
        private Renderer[] _targetRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock[] _originalBlocks = Array.Empty<MaterialPropertyBlock>();
        private MaterialPropertyBlock[] _highlightBlocks = Array.Empty<MaterialPropertyBlock>();
        private Color[] _baseColors = Array.Empty<Color>();
        private Color[] _outlineColors = Array.Empty<Color>();
        private float[] _outlineWidths = Array.Empty<float>();
        private float[] _litBrightnesses = Array.Empty<float>();
        private bool _hasAppliedBlocks;
        private float _baseRadius = 0.65f;
        private float _pulseStartTime = -10f;

        public void TriggerSelectPulse(Transform target)
        {
            if (target != _currentTarget)
            {
                BindTarget(target);
            }
            _pulseStartTime = Time.unscaledTime;
        }

        public void UpdateHighlight(Transform target, BattleElement element, bool isVisible, bool applyRendererHighlight)
        {
            if (!isVisible || target == null || !target.gameObject.activeInHierarchy)
            {
                RestoreRenderers();
                if (_root != null && _root.activeSelf)
                {
                    _root.SetActive(false);
                }
                return;
            }

            if (target != _currentTarget)
            {
                BindTarget(target);
                _pulseStartTime = Time.unscaledTime;
            }

            EnsureRingObjects();
            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }

            Color primaryColor = GetElementHighlightColor(element);
            float elapsed = Time.unscaledTime;
            float pulseAge = Mathf.Max(0f, elapsed - _pulseStartTime);
            float pulseNormalized = Mathf.Clamp01(pulseAge / PulseDuration);
            float clickFlash = 1f - Mathf.SmoothStep(0f, 1f, pulseNormalized);
            float breathe = 0.5f + 0.5f * Mathf.Sin(elapsed * 5.2f);

            // 1. 발밑 타겟 링 & 클릭 충격파 링 갱신
            Vector3 center = target.position + Vector3.up * 0.04f;
            _root.transform.position = center;

            float popScale = pulseNormalized < 1f
                ? Mathf.Lerp(0.72f, 1f, 1f - Mathf.Pow(1f - pulseNormalized, 3f)) + Mathf.Sin(pulseNormalized * Mathf.PI) * 0.14f
                : 1f + 0.04f * Mathf.Sin(elapsed * 4f);
            float innerRadius = _baseRadius * popScale;

            Color innerColor = Color.Lerp(primaryColor, Color.white, 0.25f + clickFlash * 0.55f);
            innerColor.a = Mathf.Lerp(0.72f, 0.95f, breathe) + clickFlash * 0.2f;
            DrawCircle(_innerRing, innerRadius, 0.042f + clickFlash * 0.025f, innerColor);

            float outerRadius = _baseRadius * (1.22f + 0.05f * Mathf.Sin(elapsed * 3.5f)) * popScale;
            float baseAngle = elapsed * 48f;
            Color arcColor = Color.Lerp(primaryColor, Color.white, 0.4f + clickFlash * 0.45f);
            arcColor.a = 0.92f;
            for (int i = 0; i < ArcCount; i++)
            {
                float startDeg = baseAngle + i * 90f - 26f;
                float endDeg = baseAngle + i * 90f + 26f;
                DrawArc(_outerArcs[i], outerRadius, startDeg, endDeg, 0.055f + clickFlash * 0.02f, arcColor);
            }

            if (pulseNormalized < 1f)
            {
                _shockwaveRing.enabled = true;
                float waveRadius = _baseRadius * Mathf.Lerp(0.55f, 1.85f, 1f - Mathf.Pow(1f - pulseNormalized, 2.2f));
                Color waveColor = Color.Lerp(primaryColor, Color.white, 0.65f);
                waveColor.a = (1f - pulseNormalized) * 0.85f;
                float waveWidth = Mathf.Lerp(0.08f, 0.01f, pulseNormalized);
                DrawCircle(_shockwaveRing, waveRadius, waveWidth, waveColor);
            }
            else if (_shockwaveRing.enabled)
            {
                _shockwaveRing.enabled = false;
            }

            // 2. 몬스터 렌더러 림/아웃라인/클릭 플래시 하이라이트 갱신
            if (!applyRendererHighlight)
            {
                RestoreRenderers();
                return;
            }

            ApplyRendererHighlight(primaryColor, breathe, clickFlash);
        }

        public void Clear()
        {
            RestoreRenderers();
            _currentTarget = null;
            _targetRenderers = Array.Empty<Renderer>();
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        public void Dispose()
        {
            Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            if (_lineMaterial != null)
            {
                UnityEngine.Object.Destroy(_lineMaterial);
                _lineMaterial = null;
            }
        }

        private void BindTarget(Transform target)
        {
            RestoreRenderers();
            _currentTarget = target;
            if (target == null)
            {
                _targetRenderers = Array.Empty<Renderer>();
                return;
            }

            _targetRenderers = target.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null && !(r is ParticleSystemRenderer) && !(r is LineRenderer)
                    && r.GetComponent<TMPro.TMP_Text>() == null && r.GetComponent<TextMesh>() == null)
                .ToArray();

            _originalBlocks = new MaterialPropertyBlock[_targetRenderers.Length];
            _highlightBlocks = new MaterialPropertyBlock[_targetRenderers.Length];
            _baseColors = new Color[_targetRenderers.Length];
            _outlineColors = new Color[_targetRenderers.Length];
            _outlineWidths = new float[_targetRenderers.Length];
            _litBrightnesses = new float[_targetRenderers.Length];

            Bounds combined = default;
            bool hasBounds = false;
            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                Renderer r = _targetRenderers[i];
                _originalBlocks[i] = new MaterialPropertyBlock();
                _highlightBlocks[i] = new MaterialPropertyBlock();
                r.GetPropertyBlock(_originalBlocks[i]);
                r.GetPropertyBlock(_highlightBlocks[i]);

                Material mat = r.sharedMaterial;
                _baseColors[i] = mat != null && mat.HasProperty(_baseColorId) ? mat.GetColor(_baseColorId)
                    : mat != null && mat.HasProperty(_colorId) ? mat.GetColor(_colorId) : Color.white;
                _outlineColors[i] = mat != null && mat.HasProperty(_outlineColorId) ? mat.GetColor(_outlineColorId) : new Color(0.35f, 0.3f, 0.4f, 1f);
                _outlineWidths[i] = mat != null && mat.HasProperty(_outlineWidthId) ? mat.GetFloat(_outlineWidthId) : 1.25f;
                _litBrightnesses[i] = mat != null && mat.HasProperty(_litBrightnessId) ? mat.GetFloat(_litBrightnessId) : 1f;

                if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    if (hasBounds)
                    {
                        combined.Encapsulate(r.bounds);
                    }
                    else
                    {
                        combined = r.bounds;
                        hasBounds = true;
                    }
                }
            }

            float horizontalExtent = hasBounds ? Mathf.Max(combined.extents.x, combined.extents.z) : 0.55f;
            _baseRadius = Mathf.Clamp(horizontalExtent * 1.05f, 0.52f, 1.15f);
        }

        private void ApplyRendererHighlight(Color elementColor, float breathe, float clickFlash)
        {
            if (_targetRenderers.Length == 0)
            {
                return;
            }
            _hasAppliedBlocks = true;
            Color rimColor = Color.Lerp(elementColor, Color.white, 0.35f + clickFlash * 0.45f);
            float rimStrength = Mathf.Lerp(0.55f, 0.92f, breathe) + clickFlash * 0.45f;
            float rimPower = Mathf.Lerp(3.2f, 2.1f, clickFlash);
            float tintAmount = Mathf.Lerp(0.08f, 0.16f, breathe) + clickFlash * 0.38f;

            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                Renderer r = _targetRenderers[i];
                if (r == null)
                {
                    continue;
                }
                MaterialPropertyBlock block = _highlightBlocks[i];
                Color tintedBase = Color.Lerp(_baseColors[i], Color.Lerp(elementColor, Color.white, 0.45f), tintAmount);
                block.SetColor(_baseColorId, tintedBase);
                block.SetColor(_colorId, tintedBase);
                block.SetColor(_rimColorId, rimColor);
                block.SetFloat(_rimStrengthId, rimStrength);
                block.SetFloat(_rimPowerId, rimPower);
                block.SetColor(_depthRimColorId, rimColor);
                block.SetFloat(_depthRimStrengthId, Mathf.Lerp(0.75f, 1.2f, breathe) + clickFlash * 0.4f);
                block.SetColor(_outlineColorId, Color.Lerp(_outlineColors[i], rimColor, 0.65f + clickFlash * 0.35f));
                block.SetFloat(_outlineWidthId, _outlineWidths[i] * (1.18f + clickFlash * 0.35f));
                block.SetFloat(_litBrightnessId, _litBrightnesses[i] * (1.08f + 0.08f * breathe + clickFlash * 0.28f));
                r.SetPropertyBlock(block);
            }
        }

        private void RestoreRenderers()
        {
            if (!_hasAppliedBlocks)
            {
                return;
            }
            for (int i = 0; i < _targetRenderers.Length && i < _originalBlocks.Length; i++)
            {
                if (_targetRenderers[i] != null)
                {
                    _targetRenderers[i].SetPropertyBlock(_originalBlocks[i]);
                }
            }
            _hasAppliedBlocks = false;
        }

        private void EnsureRingObjects()
        {
            if (_root != null)
            {
                return;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSave
            };

            _root = new GameObject("BattleTargetHighlight")
            {
                hideFlags = HideFlags.DontSave
            };
            _innerRing = CreateLine(_root.transform, "InnerRing", CircleSegments, true);
            _shockwaveRing = CreateLine(_root.transform, "ShockwaveRing", CircleSegments, true);
            for (int i = 0; i < ArcCount; i++)
            {
                _outerArcs[i] = CreateLine(_root.transform, "OuterArc_" + i, ArcSegments, false);
            }
        }

        private LineRenderer CreateLine(Transform parent, string name, int positionCount, bool loop)
        {
            var go = new GameObject(name)
            {
                hideFlags = HideFlags.DontSave
            };
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = positionCount;
            line.sharedMaterial = _lineMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            return line;
        }

        private static void DrawCircle(LineRenderer line, float radius, float width, Color color)
        {
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            int count = line.positionCount;
            float step = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = i * step;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private static void DrawArc(LineRenderer line, float radius, float startDeg, float endDeg, float width, Color color)
        {
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : (float)i / (count - 1);
                float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                line.SetPosition(i, new Vector3(Mathf.Cos(rad) * radius, 0.01f, Mathf.Sin(rad) * radius));
            }
        }

        private static Color GetElementHighlightColor(BattleElement element)
        {
            switch (element)
            {
                case BattleElement.Afterimage:
                    return new Color(0.25f, 0.9f, 1f, 1f);
                case BattleElement.Imprint:
                    return new Color(1f, 0.32f, 0.56f, 1f);
                case BattleElement.Oblivion:
                    return new Color(0.75f, 0.42f, 1f, 1f);
                default:
                    return new Color(0.45f, 0.92f, 1f, 1f);
            }
        }
    }
}
