using UnityEngine;
using UnityEngine.InputSystem;
using CK.SemesterProject.UIAnimation;

namespace CK.SemesterProject.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class PressAnyKeyBanner : MonoBehaviour
    {
        [Tooltip("표시/숨김 각 단계의 시간 (페이드 시간 포함)")]
        [SerializeField, Min(0.1f)] private float _blinkPeriod = 1.5f;
        [Tooltip("알파가 0에서 1 또는 1에서 0으로 변하는 시간")]
        [SerializeField, Min(0.01f)] private float _fadeDuration = 0.2f;
        [SerializeField] private UIAnimationPlayer[] _openOnAnyKey = new UIAnimationPlayer[0];
        private CanvasGroup _group;
        private float _elapsed;
        private int _enabledFrame;

        private void OnEnable()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _elapsed = 0f;
            _enabledFrame = Time.frameCount;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float period = Mathf.Max(0.1f, _blinkPeriod);
            float fade = Mathf.Clamp(_fadeDuration, 0.01f, period);
            float phase = Mathf.Repeat(_elapsed, period * 2f);
            bool showing = phase < period;
            float progress = Mathf.Clamp01((showing ? phase : phase - period) / fade);
            _group.alpha = showing ? progress : 1f - progress;
            if (Time.frameCount > _enabledFrame + 1
                && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                OpenMenu();
        }

        public void OpenMenu()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            foreach (UIAnimationPlayer player in _openOnAnyKey)
                if (player != null) player.Open();
        }

        private void OnDisable()
        {
            if (_group != null) _group.alpha = 1f;
        }
    }
}
