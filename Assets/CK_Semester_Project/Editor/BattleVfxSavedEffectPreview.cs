using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.EditorTools
{
    public sealed class BattleVfxSavedEffectPreview : IDisposable
    {
        private static BattleVfxSavedEffectPreview _active;
        private GameObject _instance;
        private GameObject _prefab;
        private BattleVfxEffectPlacement _placement;
        private ParticleSystem[] _particles;
        private float[] _particleTimes;
        private SakuraSlashFade[] _fades;
        private SakuraImpactStreakPlayer[] _streaks;
        private EffectLightPulse[] _lightPulses;
        private Light[] _lights;
        private bool[] _lightEnabled;
        private Renderer[] _renderers;
        private bool[] _rendererEnabled;
        private Transform _dummy;
        private Scene _scene;
        private double _lastTime;
        private float _effectSpeed;
        private float _effectDuration;
        private bool _isDisposed;

        public GameObject Instance => _instance;
        public GameObject Prefab => _prefab;
        public bool IsPlaying { get; private set; }
        public bool Loop { get; set; } = true;
        public float Speed { get; set; } = 1f;
        public float Time { get; private set; }
        public float Duration { get; private set; } = 1f;
        public int ParticleCount => _particles == null ? 0 : _particles.Where(item => item != null).Sum(item => item.particleCount);

        public BattleVfxSavedEffectPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneClosing += SceneClosing;
        }

        public void Play(BattleVfxSavedEffectLibrary.Entry entry, Scene scene)
        {
            if (_isDisposed || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Play를 종료한 뒤 저장된 이펙트를 미리 보세요.");
            }
            if (entry == null || entry.Prefab == null || !scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("저장된 이펙트와 테스트 씬을 확인하세요.");
            }
            Transform dummy = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "VFX Training Dummy");
            if (dummy == null)
            {
                throw new InvalidOperationException("현재 씬에 기본 테스트 더미가 없습니다. BattleVfxTest 씬을 여세요.");
            }
            _active?.Stop();
            Stop();
            _active = this;
            _scene = scene;
            _dummy = dummy;
            _prefab = entry.Prefab;
            _placement = entry.Placement;
            _effectSpeed = _placement.EffectivePlaybackSpeed * Mathf.Max(.01f, entry.BasePlaybackSpeed);
            try
            {
                _instance = Object.Instantiate(entry.Prefab);
                SceneManager.MoveGameObjectToScene(_instance, scene);
                _instance.name = "Saved VFX Preview";
                foreach (Transform child in _instance.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                }
                foreach (BattleVfxSceneTemplate template in _instance.GetComponentsInChildren<BattleVfxSceneTemplate>(true))
                {
                    Object.DestroyImmediate(template);
                }
                _instance.SetActive(true);
                _placement.Apply(_instance, dummy);
                _lightPulses = _instance.GetComponentsInChildren<EffectLightPulse>(true)
                    .Where(item => item.enabled).ToArray();
                foreach (MonoBehaviour behaviour in _instance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.enabled = false;
                }
                foreach (Collider collider in _instance.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
                foreach (AudioSource audio in _instance.GetComponentsInChildren<AudioSource>(true))
                {
                    audio.enabled = false;
                }
                _fades = _instance.GetComponentsInChildren<SakuraSlashFade>(true);
                _streaks = _instance.GetComponentsInChildren<SakuraImpactStreakPlayer>(true);
                _lights = _instance.GetComponentsInChildren<Light>(true);
                _lightEnabled = _lights.Select(item => item.enabled).ToArray();
                _renderers = _instance.GetComponentsInChildren<Renderer>(true);
                _rendererEnabled = _renderers.Select(item => item.enabled).ToArray();
                _particles = _instance.GetComponentsInChildren<ParticleSystem>(true);
                _particleTimes = Enumerable.Repeat(-1f, _particles.Length).ToArray();
                foreach (ParticleSystem particle in _particles)
                {
                    var main = particle.main;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    main.simulationSpeed *= _effectSpeed;
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.useAutoRandomSeed = false;
                    particle.randomSeed = 12345u;
                }
                _effectDuration = SkillVfxPlayback.EstimateDuration(_instance);
                foreach (SakuraSlashFade fade in _fades)
                {
                    _effectDuration = Mathf.Max(_effectDuration, fade.Duration / _effectSpeed);
                }
                foreach (SakuraImpactStreakPlayer streak in _streaks)
                {
                    _effectDuration = Mathf.Max(_effectDuration, streak.Duration / _effectSpeed);
                }
                foreach (EffectLightPulse lightPulse in _lightPulses)
                {
                    _effectDuration = Mathf.Max(_effectDuration, lightPulse.Duration / _effectSpeed);
                }
                if (_placement.Lifetime > 0f)
                {
                    _effectDuration = _placement.Lifetime;
                }
                Duration = Mathf.Max(.2f, Mathf.Max(0f, _placement.StartDelay) + _effectDuration + .15f);
                Time = Mathf.Min(Mathf.Max(0f, _placement.StartDelay) + .05f / _effectSpeed, Duration);
                IsPlaying = true;
                _lastTime = EditorApplication.timeSinceStartup;
                Sample(Time);
            }
            catch
            {
                Stop();
                throw;
            }
        }

        public void Sample(float time)
        {
            if (_instance == null || _dummy == null)
            {
                return;
            }
            Time = Mathf.Clamp(time, 0f, Duration);
            _instance.transform.SetPositionAndRotation(_dummy.position + _dummy.rotation * _placement.Position,
                _dummy.rotation * Quaternion.Euler(_placement.Rotation));
            float elapsed = Time - Mathf.Max(0f, _placement.StartDelay);
            bool hidden = elapsed < 0f || elapsed >= _effectDuration;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = !hidden && _rendererEnabled[i];
                }
            }
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].enabled = !hidden && _lightEnabled[i];
                }
            }
            foreach (SakuraSlashFade fade in _fades)
            {
                if (fade != null)
                {
                    fade.Preview(hidden ? 1f : elapsed * _effectSpeed / fade.Duration);
                }
            }
            foreach (SakuraImpactStreakPlayer streak in _streaks)
            {
                if (streak != null)
                {
                    streak.Preview(hidden ? 1f : elapsed * _effectSpeed / streak.Duration);
                }
            }
            foreach (EffectLightPulse lightPulse in _lightPulses)
            {
                if (lightPulse != null)
                {
                    lightPulse.Preview(hidden ? 1f : elapsed * _effectSpeed / lightPulse.Duration);
                }
            }
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particle = _particles[i];
                if (particle == null)
                {
                    continue;
                }
                if (hidden || !particle.gameObject.activeInHierarchy)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _particleTimes[i] = -1f;
                    continue;
                }
                bool restart = _particleTimes[i] < 0f || elapsed < _particleTimes[i];
                particle.Simulate(restart ? elapsed : elapsed - _particleTimes[i], false, restart, false);
                particle.Pause(false);
                _particleTimes[i] = elapsed;
            }
            SceneView.RepaintAll();
        }

        public void Advance(float seconds)
        {
            if (!IsPlaying || _instance == null)
            {
                return;
            }
            float nextTime = Time + Mathf.Max(0f, seconds) * Mathf.Clamp(Speed, .1f, 4f);
            if (nextTime >= Duration)
            {
                if (Loop)
                {
                    Sample(Duration);
                    nextTime = Mathf.Repeat(nextTime, Duration);
                }
                else
                {
                    nextTime = Duration;
                    IsPlaying = false;
                }
            }
            Sample(nextTime);
        }

        public void SetPlaying(bool playing)
        {
            IsPlaying = playing && _instance != null;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        public void Stop()
        {
            IsPlaying = false;
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
            }
            _instance = null;
            _prefab = null;
            _dummy = null;
            _particles = null;
            _particleTimes = null;
            _fades = null;
            _streaks = null;
            _lightPulses = null;
            _lights = null;
            _lightEnabled = null;
            _renderers = null;
            _rendererEnabled = null;
            Time = 0f;
            if (_active == this)
            {
                _active = null;
            }
            SceneView.RepaintAll();
        }

        public void Dispose()
        {
            Stop();
            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorSceneManager.sceneClosing -= SceneClosing;
            _isDisposed = true;
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastTime);
            _lastTime = now;
            if (_prefab == null || _dummy == null || !_scene.IsValid() || !_scene.isLoaded)
            {
                if (_instance != null)
                {
                    Stop();
                }
                return;
            }
            Advance(Mathf.Min(delta, .1f));
        }

        private void PlayModeChanged(PlayModeStateChange state)
        {
            Stop();
        }

        private void SceneClosing(Scene scene, bool removingScene)
        {
            if (scene == _scene)
            {
                Stop();
            }
        }
    }
}
