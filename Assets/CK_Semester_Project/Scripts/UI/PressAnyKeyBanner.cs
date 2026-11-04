using UnityEngine;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class PressAnyKeyBanner : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _blinkPeriod = 1.5f;
        private CanvasGroup _group;
        private float _elapsed;
        private int _enabledFrame;

        private void OnEnable()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 1f;
            _elapsed = 0f;
            _enabledFrame = Time.frameCount;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _group.alpha = (1f + Mathf.Cos(_elapsed * Mathf.PI * 2f / _blinkPeriod)) * 0.5f;
            if (Time.frameCount > _enabledFrame + 1
                && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_group != null) _group.alpha = 1f;
        }
    }
}
