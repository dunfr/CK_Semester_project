using System;
using System.Collections.Generic;
using CK.SemesterProject.Tutorial;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("CK/칼날 소켓 슬래시")]
public sealed class SocketSlashEffect : MonoBehaviour
{
    [SerializeField, Tooltip("명중 위치에서 따로 재생할 타격 효과의 루트입니다.")]
    private Transform[] _impactRoots = Array.Empty<Transform>();
    [SerializeField, Range(0f, .5f), Tooltip("명중 시점보다 검기를 먼저 켜는 공격 애니메이션 비율입니다.")]
    private float _swingLeadFraction = .12f;
    [SerializeField, Range(0f, .5f), Tooltip("명중 시점 이후 검기가 칼을 따라가는 공격 애니메이션 비율입니다.")]
    private float _swingTailFraction = .12f;

    private Vector3[] _impactPositions;
    private Quaternion[] _impactRotations;
    private Vector3[] _impactScales;
    private bool[] _impactActive;
    private Renderer[] _bladeRenderers;
    private bool[] _bladeRendererEnabled;
    private ParticleSystem[] _bladeParticles;
    private Light[] _bladeLights;
    private bool[] _bladeLightEnabled;
    private GameObject _impactInstance;
    private bool _isInitialized;

    public Transform[] ImpactRoots => _impactRoots;
    public GameObject ImpactInstance => _impactInstance;
    public float SwingLeadFraction => Mathf.Clamp(_swingLeadFraction, 0f, .5f);
    public float SwingTailFraction => Mathf.Clamp(_swingTailFraction, 0f, .5f);

    private void OnDisable()
    {
        ClearImpact();
    }

    private void OnDestroy()
    {
        ClearImpact();
    }

    public void Attach(Transform socket)
    {
        if (socket == null)
        {
            throw new ArgumentNullException(nameof(socket), "슬래시를 붙일 칼날 소켓이 필요합니다.");
        }
        Initialize();
        ClearImpact();
        foreach (TutorialSlashVfxPlacement automatic in GetComponentsInChildren<TutorialSlashVfxPlacement>(true))
        {
            automatic.enabled = false;
        }
        // The saved root pose is local to the blade socket, not the enemy or world.
        transform.SetParent(socket, false);
        for (int i = 0; i < _impactRoots.Length; i++)
        {
            if (_impactRoots[i] != null)
            {
                _impactRoots[i].gameObject.SetActive(false);
            }
        }
        for (int i = 0; i < _bladeRenderers.Length; i++)
        {
            if (_bladeRenderers[i] != null)
            {
                _bladeRenderers[i].enabled = _bladeRendererEnabled[i];
            }
        }
        for (int i = 0; i < _bladeLights.Length; i++)
        {
            if (_bladeLights[i] != null)
            {
                _bladeLights[i].enabled = _bladeLightEnabled[i];
            }
        }
    }

    public GameObject ReleaseImpact(Transform target, Camera camera)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        Initialize();
        if (_impactInstance != null)
        {
            return _impactInstance;
        }
        bool hasImpact = false;
        for (int i = 0; i < _impactRoots.Length; i++)
        {
            hasImpact |= _impactRoots[i] != null && _impactActive[i];
        }
        if (!hasImpact)
        {
            return null;
        }
        Bounds bounds = GetTargetBounds(target);
        Vector3 direction = camera != null ? camera.transform.position - bounds.center : transform.position - bounds.center;
        direction = direction.sqrMagnitude > .0001f ? direction.normalized : -target.forward;
        Vector3 absoluteDirection = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
        float depth = Vector3.Dot(bounds.extents, absoluteDirection);
        _impactInstance = new GameObject("Slash Impact");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_impactInstance, gameObject.scene);
        _impactInstance.transform.SetPositionAndRotation(bounds.center + direction * (depth + .05f),
            Quaternion.LookRotation(-direction, Vector3.up));
        _impactInstance.transform.localScale = transform.lossyScale;
        for (int i = 0; i < _impactRoots.Length; i++)
        {
            if (_impactRoots[i] == null || !_impactActive[i])
            {
                continue;
            }
            GameObject clone = Instantiate(_impactRoots[i].gameObject, _impactInstance.transform, false);
            clone.name = _impactRoots[i].name;
            clone.transform.localPosition = _impactPositions[i];
            clone.transform.localRotation = _impactRotations[i];
            clone.transform.localScale = _impactScales[i];
            clone.SetActive(true);
            foreach (ParticleSystem particle in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (particle.gameObject.activeInHierarchy)
                {
                    particle.Play(false);
                }
            }
        }
        return _impactInstance;
    }

    public void FinishSwing()
    {
        Initialize();
        foreach (Renderer renderer in _bladeRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
        foreach (ParticleSystem particle in _bladeParticles)
        {
            if (particle != null)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        foreach (Light light in _bladeLights)
        {
            if (light != null)
            {
                light.enabled = false;
            }
        }
    }

    public GameObject TakeImpact()
    {
        GameObject impact = _impactInstance;
        _impactInstance = null;
        return impact;
    }

    public void ClearImpact()
    {
        if (_impactInstance == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(_impactInstance);
        }
        else
        {
            DestroyImmediate(_impactInstance);
        }
        _impactInstance = null;
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        if (_impactRoots == null || _impactRoots.Length == 0)
        {
            var roots = new List<Transform>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "SakuraHitDetails" || child.name == "ImpactStreak")
                {
                    roots.Add(child);
                }
            }
            _impactRoots = roots.ToArray();
        }
        _impactPositions = new Vector3[_impactRoots.Length];
        _impactRotations = new Quaternion[_impactRoots.Length];
        _impactScales = new Vector3[_impactRoots.Length];
        _impactActive = new bool[_impactRoots.Length];
        for (int i = 0; i < _impactRoots.Length; i++)
        {
            Transform root = _impactRoots[i];
            if (root == null || root == transform || !root.IsChildOf(transform))
            {
                _impactRoots[i] = null;
                continue;
            }
            // OnEnable already starts the streak animation; cache its authored size, not frame-zero shrinkage.
            foreach (SakuraImpactStreakPlayer streak in root.GetComponentsInChildren<SakuraImpactStreakPlayer>(true))
            {
                streak.ResetToAuthoredScale();
            }
            _impactPositions[i] = transform.InverseTransformPoint(root.position);
            _impactRotations[i] = Quaternion.Inverse(transform.rotation) * root.rotation;
            Vector3 rootScale = transform.lossyScale;
            Vector3 scale = root.lossyScale;
            _impactScales[i] = new Vector3(Divide(scale.x, rootScale.x), Divide(scale.y, rootScale.y), Divide(scale.z, rootScale.z));
            _impactActive[i] = root.gameObject.activeSelf;
        }
        var renderers = new List<Renderer>();
        var particles = new List<ParticleSystem>();
        var lights = new List<Light>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!IsImpact(renderer.transform))
            {
                renderers.Add(renderer);
            }
        }
        foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!IsImpact(particle.transform))
            {
                particles.Add(particle);
            }
        }
        foreach (Light light in GetComponentsInChildren<Light>(true))
        {
            if (!IsImpact(light.transform))
            {
                lights.Add(light);
            }
        }
        _bladeRenderers = renderers.ToArray();
        _bladeRendererEnabled = renderers.ConvertAll(item => item.enabled).ToArray();
        _bladeParticles = particles.ToArray();
        _bladeLights = lights.ToArray();
        _bladeLightEnabled = lights.ConvertAll(item => item.enabled).ToArray();
        _isInitialized = true;
    }

    private bool IsImpact(Transform item)
    {
        foreach (Transform root in _impactRoots)
        {
            if (root != null && item.IsChildOf(root))
            {
                return true;
            }
        }
        return false;
    }

    private static float Divide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > .0001f ? value / divisor : 1f;
    }

    private static Bounds GetTargetBounds(Transform target)
    {
        Bounds bounds = new Bounds(target.position + Vector3.up, new Vector3(1f, 2f, 1f));
        bool hasBounds = false;
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                continue;
            }
            if (renderer.GetComponent<TMPro.TMP_Text>() != null || renderer.GetComponent<TextMesh>() != null)
            {
                continue;
            }
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds;
    }
}
