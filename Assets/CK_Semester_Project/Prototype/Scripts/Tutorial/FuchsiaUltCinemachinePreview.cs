using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    // The Director owns time. The Timeline transport and these buttons use the same graph.
    [DisallowMultipleComponent]
    public sealed class FuchsiaUltCinemachinePreview : MonoBehaviour
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private Animator _character;
        [SerializeField] private TimelineAsset _synchronized;
        [SerializeField] private TimelineAsset _animationOnly;
        [SerializeField] private TimelineAsset _cameraOnly;
        private bool _active, _paused;
        private float _speed = 1f;
        private string _mode = "대기";

        public double PreviewTime => _director != null ? _director.time : 0d;
        public double Duration => _director != null ? _director.duration : 0d;
        public bool IsActive => _active;
        public bool IsPaused => _paused;

        private void OnEnable()
        {
            if (_director == null) return;
            _director.played += OnPlayed;
            _director.paused += OnPaused;
            _director.stopped += OnStopped;
        }

        private void OnDisable()
        {
            if (_director == null) return;
            if (Application.isPlaying) StopPreview();
            _director.played -= OnPlayed;
            _director.paused -= OnPaused;
            _director.stopped -= OnStopped;
        }

        public void PlaySynchronized() => Begin(_synchronized, "애니메이션 + 카메라");
        public void PlayAnimationOnly() => Begin(_animationOnly, "애니메이션만");
        public void PlayCameraOnly() => Begin(_cameraOnly, "카메라만");

        private void Begin(TimelineAsset timeline, string mode)
        {
            if (_director == null || timeline == null) return;
            StopPreview();
            _director.playableAsset = timeline;
            _director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            _director.extrapolationMode = DirectorWrapMode.None;
            _director.time = 0d;
            _mode = mode;
            _director.Play();
            ApplySpeed();
        }

        private void OnPlayed(PlayableDirector director) { _active = true; _paused = false; ApplySpeed(); }
        private void OnPaused(PlayableDirector director) => _paused = _active;
        private void OnStopped(PlayableDirector director)
        {
            _active = _paused = false;
            _mode = "대기";
            if (Application.isPlaying && _character != null && _character.isActiveAndEnabled)
            {
                _character.Rebind();
                _character.Update(0f);
            }
        }

        public void TogglePause()
        {
            if (_director == null || !_active) return;
            if (_paused) { _director.Resume(); _paused = false; ApplySpeed(); }
            else _director.Pause();
        }

        public void SeekPreview(double time)
        {
            if (_director == null || !_active) return;
            _director.time = System.Math.Clamp(time, 0d, System.Math.Max(0d, Duration - .00001d));
            _director.Evaluate();
        }

        public void StopPreview()
        {
            if (_director == null) return;
            _director.Stop();
            _director.time = 0d;
            _active = _paused = false;
            _mode = "대기";
        }

        // Explicit editor sampling never advances or resets playback time.
        public void EvaluatePreview() { if (_director != null && _director.playableGraph.IsValid()) _director.Evaluate(); }

        private void ApplySpeed()
        {
            if (_director == null || !_director.playableGraph.IsValid()) return;
            for (int i = 0; i < _director.playableGraph.GetRootPlayableCount(); i++)
                _director.playableGraph.GetRootPlayable(i).SetSpeed(_speed);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) PlaySynchronized();
            if (keyboard.digit2Key.wasPressedThisFrame) PlayAnimationOnly();
            if (keyboard.digit3Key.wasPressedThisFrame) PlayCameraOnly();
            if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
            if (keyboard.escapeKey.wasPressedThisFrame) StopPreview();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 286f, 320f), "Fuchsia Ult · Cinemachine", GUI.skin.window);
            if (GUILayout.Button("애니메이션 + 카메라 재생 [1]", GUILayout.Height(30f))) PlaySynchronized();
            if (GUILayout.Button("애니메이션만 재생 [2]", GUILayout.Height(30f))) PlayAnimationOnly();
            if (GUILayout.Button("카메라만 재생 [3]", GUILayout.Height(30f))) PlayCameraOnly();
            GUILayout.BeginHorizontal();
            GUI.enabled = _active;
            if (GUILayout.Button(_paused ? "계속 [Space]" : "일시 정지 [Space]", GUILayout.Height(27f))) TogglePause();
            if (GUILayout.Button("정지 / 복귀 [Esc]", GUILayout.Height(27f))) StopPreview();
            GUILayout.EndHorizontal();
            float value = GUILayout.HorizontalSlider((float)PreviewTime, 0f, Mathf.Max(.001f, (float)Duration - .00001f));
            if (_active && Mathf.Abs(value - (float)PreviewTime) > .0001f) { if (!_paused) TogglePause(); SeekPreview(value); }
            GUI.enabled = true;
            GUILayout.Label($"{_mode}{(_paused ? " (일시 정지)" : "")}");
            GUILayout.Label($"{PreviewTime:F2} / {Duration:F2} 초");
            GUILayout.BeginHorizontal();
            foreach (float speed in new[] { .25f, .5f, 1f })
                if (GUILayout.Toggle(Mathf.Approximately(_speed, speed), $" {speed:0.##}배", GUI.skin.button) && !Mathf.Approximately(_speed, speed)) { _speed = speed; ApplySpeed(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("Timeline 재생 버튼 사용 가능");
            GUILayout.EndArea();
        }
    }
}
