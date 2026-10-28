using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Rendering
{
    // 플레이어·몬스터·보스 루트에 하나씩 붙인다. 자기 위치의 조명 구역을 섞고(우선순위 순),
    // 보스 연출 덮어쓰기를 얹은 뒤, 시간 보간해서 툰 셰이더 렌더러에 MaterialPropertyBlock으로 넣는다.
    // 대상: _CharacterKeyDirection 속성을 가진 재질(Semester/Fuchsia 툰 셰이더)을 쓰는 렌더러.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CharacterLighting : MonoBehaviour
    {
        private static readonly int KeyDirectionId = Shader.PropertyToID("_CharacterKeyDirection");
        private static readonly int KeyColorId = Shader.PropertyToID("_CharacterKeyColor");
        private static readonly int EnvironmentShadowId = Shader.PropertyToID("_EnvironmentShadowStrength");
        private static readonly int BrightnessId = Shader.PropertyToID("_CharacterBrightness");
        private static readonly int RimTintId = Shader.PropertyToID("_CharacterRimTint");
        private static readonly int ShadowSlotId = Shader.PropertyToID("_CharacterShadowSlot");

        // 캐릭터 자기 그림자 맵(CharacterSelfShadowFeature)의 칸 수. 칸이 없는 캐릭터는 자기 그림자 없이 그린다.
        public const int MaxShadowSlots = 4;
        private static readonly List<CharacterLighting> ActiveList = new List<CharacterLighting>();
        public static IReadOnlyList<CharacterLighting> Active => ActiveList;

        [SerializeField, Tooltip("구역 판정 지점의 로컬 오프셋(m)입니다. 보통 가슴 높이입니다.")]
        private Vector3 _sampleOffset = new Vector3(0f, 1f, 0f);

        [SerializeField, Min(0f), Tooltip("구역이 바뀔 때 조명이 따라가는 시간(초)입니다. 0이면 즉시 바뀝니다.")]
        private float _smoothTime = .35f;

        [SerializeField, Tooltip("구역이 없을 때 이 캐릭터의 기본 조명입니다. 비우면 씬 주광을 그대로 씁니다.")]
        private CharacterLightProfile _baseProfile;

        [Header("Self Shadow")]
        [SerializeField, Tooltip("자기 그림자 맵에 담을 구의 중심(루트 기준 로컬, m)입니다. 보통 골반 높이입니다.")]
        private Vector3 _selfShadowCenter = new Vector3(0f, .9f, 0f);

        [SerializeField, Min(0f), Tooltip("자기 그림자 맵에 담을 구의 반지름(m)입니다. 몸이 들어갈 만큼만 잡을수록 그림자가 선명합니다. "
            + "애니메이션과 무기 길이에 따라 흔들리지 않도록 고정 크기를 씁니다. 0이면 렌더러 경계를 씁니다.")]
        private float _selfShadowRadius = 1f;

        [Header("Override (보스 연출 등)")]
        [SerializeField, Tooltip("구역 위에 덮어쓸 조명입니다. 타임라인/애니메이션으로 아래 비율을 움직일 수 있습니다.")]
        private CharacterLightProfile _overrideProfile;

        [SerializeField, Range(0f, 1f)]
        private float _overrideWeight;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private CharacterLightState _current;
        private CharacterLightState _applied;
        private bool _hasState;
        private bool _hasApplied;
        private bool _needsRendererRefresh = true;
        private int _appliedSlot = -2;

        // SetOverride로 들어온 런타임 덮어쓰기. 인스펙터의 _overrideProfile보다 우선한다.
        private bool _hasRuntimeOverride;
        private CharacterLightSettings _runtimeOverride;
        private float _runtimeWeight;
        private float _runtimeTarget;
        private float _runtimeFadeSpeed;

        public CharacterLightState CurrentState => _current;
        public IReadOnlyList<Renderer> Renderers => _renderers;

        // 자기 그림자 맵 칸 번호(0~3). -1이면 칸이 없다. 활성화된 순서대로 배정한다.
        public int ShadowSlot
        {
            get
            {
                int index = ActiveList.IndexOf(this);
                return index < MaxShadowSlots ? index : -1;
            }
        }

        // 셰이더가 실제로 쓰는 명암 방향(표면 → 광원). 씬 주광과 가상 주광을 KeyWeight로 섞은 값과 같다.
        public Vector3 ShadingLightDirection
        {
            get
            {
                Light main = CharacterLightEnvironment.MainLight;
                Vector3 mainDirection = main != null ? -main.transform.forward : Vector3.up;
                Vector3 direction = Vector3.Lerp(mainDirection, _current.KeyDirection, Mathf.Clamp01(_current.KeyWeight));
                return direction.sqrMagnitude > 1e-6f ? direction.normalized : mainDirection;
            }
        }

        // 자기 그림자 맵에 담을 구. 고정 반지름이면 애니메이션·무기(카타나 등)에 따라 범위가 바뀌지 않아
        // 그림자 텍셀이 작게 유지되고 프레임마다 격자가 흔들리지 않는다.
        public bool TryGetSelfShadowSphere(out Vector3 center, out float radius)
        {
            if (_selfShadowRadius > 0f)
            {
                center = transform.TransformPoint(_selfShadowCenter);
                Vector3 scale = transform.lossyScale;
                radius = _selfShadowRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                return _renderers.Count > 0;
            }
            bool found = TryGetBounds(out Bounds bounds);
            center = bounds.center;
            radius = bounds.extents.magnitude;
            return found;
        }

        // 렌더러 경계의 합.
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return any;
        }

        public void SetOverride(CharacterLightProfile profile, float fadeSeconds = .5f)
        {
            if (profile == null)
            {
                ClearOverride(fadeSeconds);
                return;
            }
            SetOverride(profile.Settings, fadeSeconds);
        }

        public void SetOverride(CharacterLightSettings settings, float fadeSeconds = .5f)
        {
            _runtimeOverride = settings;
            _hasRuntimeOverride = true;
            _runtimeTarget = 1f;
            _runtimeFadeSpeed = fadeSeconds > 0f ? 1f / fadeSeconds : float.PositiveInfinity;
        }

        public void ClearOverride(float fadeSeconds = .5f)
        {
            _runtimeTarget = 0f;
            _runtimeFadeSpeed = fadeSeconds > 0f ? 1f / fadeSeconds : float.PositiveInfinity;
        }

        // 장비 교체 등으로 자식 렌더러가 바뀌었을 때 호출한다.
        public void RefreshRenderers()
        {
            ResetRenderers();
            _renderers.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.HasProperty(KeyDirectionId))
                    {
                        _renderers.Add(renderer);
                        break;
                    }
                }
            }
            _needsRendererRefresh = false;
            _hasApplied = false;
        }

        // 현재 위치 기준의 목표 조명(시간 보간 전)을 계산한다.
        public CharacterLightState EvaluateTarget()
        {
            Light mainLight = CharacterLightEnvironment.MainLight;
            Camera camera = Camera.main;
            CharacterLightSettings baseSettings = _baseProfile != null ? _baseProfile.Settings : CharacterLightSettings.Default;
            Vector3 samplePosition = transform.TransformPoint(_sampleOffset);
            CharacterLightState state = baseSettings.Resolve(mainLight, camera, samplePosition, transform);
            foreach (CharacterLightZone zone in CharacterLightZone.Active)
            {
                float weight = zone.GetWeight(samplePosition);
                if (weight > 0f)
                {
                    state = CharacterLightState.Lerp(state, zone.Settings.Resolve(mainLight, camera, samplePosition, transform), weight);
                }
            }

            if (_hasRuntimeOverride && _runtimeWeight > 0f)
            {
                state = CharacterLightState.Lerp(state, _runtimeOverride.Resolve(mainLight, camera, samplePosition, transform), _runtimeWeight);
            }
            else if (_overrideProfile != null && _overrideWeight > 0f)
            {
                state = CharacterLightState.Lerp(state, _overrideProfile.Settings.Resolve(mainLight, camera, samplePosition, transform), _overrideWeight);
            }
            return state;
        }

        private void OnEnable()
        {
            if (!ActiveList.Contains(this))
            {
                ActiveList.Add(this);
            }
            _block ??= new MaterialPropertyBlock();
            _needsRendererRefresh = true;
            _hasState = false;
        }

        private void OnDisable()
        {
            ActiveList.Remove(this);
            ResetRenderers();
            _hasState = false;
        }

        private void OnValidate()
        {
            _hasApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            _needsRendererRefresh = true;
        }

        private void LateUpdate()
        {
            if (_needsRendererRefresh)
            {
                RefreshRenderers();
            }

            bool playing = Application.isPlaying;
            float deltaTime = playing ? Time.deltaTime : 0f;
            if (_hasRuntimeOverride)
            {
                _runtimeWeight = float.IsInfinity(_runtimeFadeSpeed) || !playing
                    ? _runtimeTarget
                    : Mathf.MoveTowards(_runtimeWeight, _runtimeTarget, _runtimeFadeSpeed * deltaTime);
                if (_runtimeTarget <= 0f && _runtimeWeight <= 0f)
                {
                    _hasRuntimeOverride = false;
                }
            }

            CharacterLightState target = EvaluateTarget();
            if (!_hasState || !playing || _smoothTime <= 0f)
            {
                _current = target;
                _hasState = true;
            }
            else
            {
                // 프레임 속도와 무관한 지수 보간.
                _current = CharacterLightState.Lerp(_current, target, 1f - Mathf.Exp(-deltaTime / _smoothTime));
            }

            // 플레이 중에는 매 프레임 넣는다. 피격 점멸처럼 블록을 저장·복원하는 다른 연출이 값을 덮어써도 곧바로 복구된다.
            if (!playing && _hasApplied && _current.Approximately(_applied) && _appliedSlot == ShadowSlot)
            {
                return;
            }
            Apply(_current);
        }

        private void Apply(CharacterLightState state)
        {
            _block ??= new MaterialPropertyBlock();
            var direction = new Vector4(state.KeyDirection.x, state.KeyDirection.y, state.KeyDirection.z, state.KeyWeight);
            var color = new Vector4(state.KeyColor.x, state.KeyColor.y, state.KeyColor.z, 0f);
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                // 다른 시스템이 넣은 값은 유지하고 조명 값만 바꾼다.
                renderer.GetPropertyBlock(_block);
                _block.SetVector(KeyDirectionId, direction);
                _block.SetVector(KeyColorId, color);
                _block.SetFloat(EnvironmentShadowId, state.EnvironmentShadowStrength);
                _block.SetVector(BrightnessId, state.Brightness);
                _block.SetVector(RimTintId, state.RimTint);
                _block.SetFloat(ShadowSlotId, ShadowSlot);
                renderer.SetPropertyBlock(_block);
            }
            _applied = state;
            _appliedSlot = ShadowSlot;
            _hasApplied = true;
        }

        private void ResetRenderers()
        {
            if (_block == null)
            {
                return;
            }
            CharacterLightState neutral = CharacterLightSettings.Default.Resolve(null, null, Vector3.zero, null);
            neutral.EnvironmentShadowStrength = 1f;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                renderer.GetPropertyBlock(_block);
                _block.SetVector(KeyDirectionId, Vector4.zero);
                _block.SetVector(KeyColorId, Vector4.zero);
                _block.SetFloat(EnvironmentShadowId, neutral.EnvironmentShadowStrength);
                _block.SetVector(BrightnessId, neutral.Brightness);
                _block.SetVector(RimTintId, neutral.RimTint);
                _block.SetFloat(ShadowSlotId, -1f);
                renderer.SetPropertyBlock(_block);
            }
            _hasApplied = false;
        }
    }
}
