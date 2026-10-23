using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FuchsiaIndoorLighting : MonoBehaviour
{
    [SerializeField, Tooltip("실내 주광을 받을 캐릭터의 이동 루트입니다.")]
    private Transform _characterRoot;

    [SerializeField, Tooltip("이 오브젝트를 중심으로 하는 조명 구역의 로컬 크기(m)입니다.")]
    private Vector3 _volumeSize = new Vector3(6f, 4f, 24f);

    [SerializeField, Min(0f), Tooltip("구역 바깥에서 원래 씬 주광으로 전환되는 거리(m)입니다.")]
    private float _blendDistance = 2f;

    [SerializeField, Tooltip("이동 루트에서 조명 구역을 판정할 지점의 로컬 오프셋(m)입니다.")]
    private Vector3 _sampleOffset = new Vector3(0f, 1f, 0f);

    [SerializeField, Tooltip("가상 방향광의 월드 회전(도)입니다. 카메라나 캐릭터 회전을 따라가지 않습니다.")]
    private Vector3 _keyEulerAngles = new Vector3(28f, 225f, 0f);

    [SerializeField, ColorUsage(false, true), Tooltip("캐릭터 형태를 잡는 실내 주광의 색입니다.")]
    private Color _keyColor = new Color(1f, .94f, .90f, 1f);

    [SerializeField, Range(0f, 2f), Tooltip("실내 주광의 선형 광량 배율입니다.")]
    private float _keyIntensity = .12f;

    [SerializeField, Range(0f, 1f), Tooltip("환경의 투영 그림자가 실내 주광을 어둡게 하는 최대 비율입니다. 카툰 경계와 주변광은 유지합니다.")]
    private float _environmentShadowStrength = .18f;

    private static readonly int KeyDirectionId = Shader.PropertyToID("_CharacterKeyDirection");
    private static readonly int KeyColorId = Shader.PropertyToID("_CharacterKeyColor");
    private static readonly int EnvironmentShadowId = Shader.PropertyToID("_EnvironmentShadowStrength");

    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;
    private Vector4 _lastDirection;
    private Vector4 _lastColor;
    private float _lastEnvironmentShadowStrength;
    private bool _hasApplied;
    private bool _needsRendererCache;

    public float CurrentWeight { get; private set; }

    private void OnEnable()
    {
        if (_characterRoot == null)
        {
            Debug.LogWarning("푸시아 실내 조명의 캐릭터 루트가 연결되지 않았습니다.", this);
            return;
        }

        // CharacterLightZone + CharacterLighting으로 대체되었다. 둘이 같은 속성을 덮어쓰지 않게 한쪽만 동작시킨다.
        if (_characterRoot.GetComponentInParent<CK.SemesterProject.Rendering.CharacterLighting>() != null)
        {
            Debug.LogWarning("캐릭터에 CharacterLighting이 있어 FuchsiaIndoorLighting을 끕니다. CharacterLightZone을 사용하세요.", this);
            enabled = false;
            return;
        }

        RefreshLighting();
    }

    private void LateUpdate()
    {
        if (_characterRoot == null)
        {
            if (_renderers != null)
            {
                ResetOverrides();
                _renderers = null;
            }
            return;
        }

        if (_needsRendererCache)
        {
            RefreshLighting();
            return;
        }

        ApplyLighting();
    }

    private void OnDisable()
    {
        ResetOverrides();
    }

    private void OnValidate()
    {
        _volumeSize = new Vector3(Mathf.Max(.01f, _volumeSize.x),
            Mathf.Max(.01f, _volumeSize.y), Mathf.Max(.01f, _volumeSize.z));
        _hasApplied = false;
        _needsRendererCache = true;
    }

    public void RefreshLighting()
    {
        ResetOverrides();
        if (_characterRoot == null || !isActiveAndEnabled)
        {
            return;
        }

        var renderers = new List<Renderer>();
        foreach (Renderer renderer in _characterRoot.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && material.HasProperty(KeyDirectionId))
                {
                    renderers.Add(renderer);
                    break;
                }
            }
        }

        _renderers = renderers.ToArray();
        _propertyBlock ??= new MaterialPropertyBlock();
        _needsRendererCache = false;
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        if (_renderers == null)
        {
            return;
        }

        Vector3 samplePosition = _characterRoot.TransformPoint(_sampleOffset);
        Vector3 local = transform.InverseTransformPoint(samplePosition);
        Vector3 outside = new Vector3(Mathf.Max(0f, Mathf.Abs(local.x) - _volumeSize.x * .5f),
            Mathf.Max(0f, Mathf.Abs(local.y) - _volumeSize.y * .5f),
            Mathf.Max(0f, Mathf.Abs(local.z) - _volumeSize.z * .5f));
        float distance = outside.magnitude;
        CurrentWeight = distance <= 0f ? 1f : _blendDistance > 0f
            ? 1f - Mathf.SmoothStep(0f, 1f, distance / _blendDistance) : 0f;

        Vector3 keyDirection = Quaternion.Euler(_keyEulerAngles) * Vector3.back;
        var direction = new Vector4(keyDirection.x, keyDirection.y, keyDirection.z, CurrentWeight);
        Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? _keyColor.linear : _keyColor;
        Vector4 keyColor = new Vector4(color.r, color.g, color.b, 0f) * _keyIntensity;
        if (_hasApplied && direction == _lastDirection && keyColor == _lastColor
            && Mathf.Approximately(_environmentShadowStrength, _lastEnvironmentShadowStrength))
        {
            return;
        }

        foreach (Renderer renderer in _renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetVector(KeyDirectionId, direction);
            _propertyBlock.SetVector(KeyColorId, keyColor);
            _propertyBlock.SetFloat(EnvironmentShadowId, _environmentShadowStrength);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        _lastDirection = direction;
        _lastColor = keyColor;
        _lastEnvironmentShadowStrength = _environmentShadowStrength;
        _hasApplied = true;
    }

    private void ResetOverrides()
    {
        if (_renderers != null && _propertyBlock != null)
        {
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetVector(KeyDirectionId, Vector4.zero);
                _propertyBlock.SetVector(KeyColorId, Vector4.zero);
                _propertyBlock.SetFloat(EnvironmentShadowId, 1f);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        CurrentWeight = 0f;
        _hasApplied = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .75f, .3f, .7f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, _volumeSize);
        Gizmos.DrawWireCube(Vector3.zero, _volumeSize + Vector3.one * (_blendDistance * 2f));
        Gizmos.matrix = Matrix4x4.identity;
        Vector3 keyDirection = Quaternion.Euler(_keyEulerAngles) * Vector3.back;
        Gizmos.DrawLine(transform.position, transform.position + keyDirection);
    }
}
