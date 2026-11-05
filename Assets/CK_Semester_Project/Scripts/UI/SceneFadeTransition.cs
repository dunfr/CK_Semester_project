using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CK.SemesterProject.UI
{
    // 로고 없는 검은 화면으로 덮고 씬을 바꾼 뒤 다시 걷어 낸다.
    // 씬이 바뀌어도 남도록 스스로 만든 맨 위 Canvas에서 동작하고, 끝나면 사라진다.
    [DisallowMultipleComponent]
    public sealed class SceneFadeTransition : MonoBehaviour
    {
        private const int SortingOrder = 32000;
        // 씬을 불러온 직후의 긴 프레임 하나가 페이드를 건너뛰지 않게 한다.
        private const float MaxFadeStep = 0.1f;

        private CanvasGroup _group;

        public static bool IsRunning { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => IsRunning = false;

        /// <summary>검게 덮은 뒤 beforeLoad를 부르고 sceneName을 연다. 시작하지 못하면 false.</summary>
        public static bool Run(string sceneName, float fadeOutSeconds, float fadeInSeconds, Action beforeLoad)
        {
            if (IsRunning) return false;
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"씬 '{sceneName}'을 Build Profiles의 Scene List에 추가하세요.");
                return false;
            }

            var root = new GameObject(nameof(SceneFadeTransition), typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(root.transform, false);
            var rect = (RectTransform)black.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            black.GetComponent<Image>().color = Color.black;

            SceneFadeTransition fade = root.AddComponent<SceneFadeTransition>();
            fade._group = root.GetComponent<CanvasGroup>();
            fade._group.alpha = 0f;
            IsRunning = true;
            fade.StartCoroutine(fade.Transition(sceneName, fadeOutSeconds, fadeInSeconds, beforeLoad));
            return true;
        }

        private IEnumerator Transition(string sceneName, float fadeOutSeconds, float fadeInSeconds, Action beforeLoad)
        {
            yield return Fade(0f, 1f, fadeOutSeconds);
            beforeLoad?.Invoke();
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            while (!load.isDone) yield return null;
            // 새 씬의 첫 프레임이 그려진 뒤 걷어 낸다.
            yield return null;
            yield return Fade(1f, 0f, fadeInSeconds);
            Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            _group.alpha = from;
            for (float elapsed = 0f; elapsed < seconds;)
            {
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);
                _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            }
            _group.alpha = to;
        }

        private void OnDestroy() => IsRunning = false;
    }
}
