using System;
using System.Linq;
using CK.SemesterProject.Battle;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 전투 중 몬스터를 클릭/타겟팅했을 때 렌더러 하이라이트(림·아웃라인·클릭 플래시)를 표시한다.
    /// 조준 표시는 몸통의 BattleLockOnReticle 하나만 쓴다(발밑 링 제거, AD 협의).
    /// </summary>
    public sealed class TutorialBattleTargetHighlight : IDisposable
    {
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

        private Transform _currentTarget;
        private Renderer[] _targetRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock[] _originalBlocks = Array.Empty<MaterialPropertyBlock>();
        private MaterialPropertyBlock[] _highlightBlocks = Array.Empty<MaterialPropertyBlock>();
        private Color[] _baseColors = Array.Empty<Color>();
        private Color[] _outlineColors = Array.Empty<Color>();
        private float[] _outlineWidths = Array.Empty<float>();
        private float[] _litBrightnesses = Array.Empty<float>();
        private bool _hasAppliedBlocks;
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
                return;
            }

            if (target != _currentTarget)
            {
                BindTarget(target);
                _pulseStartTime = Time.unscaledTime;
            }

            Color primaryColor = GetElementHighlightColor(element);
            float elapsed = Time.unscaledTime;
            float pulseAge = Mathf.Max(0f, elapsed - _pulseStartTime);
            float pulseNormalized = Mathf.Clamp01(pulseAge / PulseDuration);
            float clickFlash = 1f - Mathf.SmoothStep(0f, 1f, pulseNormalized);
            float breathe = 0.5f + 0.5f * Mathf.Sin(elapsed * 5.2f);

            // 몬스터 렌더러 림/아웃라인/클릭 플래시 하이라이트 갱신
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
        }

        public void Dispose()
        {
            Clear();
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
            }
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
