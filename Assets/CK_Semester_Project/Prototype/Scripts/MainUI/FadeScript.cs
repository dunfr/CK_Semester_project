using System.Collections;
using UnityEngine;

/// <summary>
/// 오버레이를 투명 → 불투명 → 유지 → 투명 순서로 재생합니다.
/// 각 시간은 초 단위의 전체 구간 길이입니다.
/// 재생이 완료되면 UI를 닫고 오브젝트를 제거합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class FadeScript : MonoBehaviour
{
    [Header("Duration (Seconds)")]
    [Tooltip("오버레이가 불투명해지는 시간입니다. 검은 이미지라면 화면이 어두워집니다.")]
    [SerializeField, Min(0f)]
    private float fadeIn;

    [SerializeField, Min(0f)]
    private float stay;

    [Tooltip("오버레이가 다시 투명해지는 시간입니다.")]
    [SerializeField, Min(0f)]
    private float fadeOut;

    [Header("Playback")]
    [SerializeField]
    private bool playOnEnable = true;

    [Tooltip("Time.timeScale이 0인 일시정지 상태에서도 재생합니다.")]
    [SerializeField]
    private bool useUnscaledTime = true;

    [Tooltip("재생 중 오버레이 뒤의 UI에 대한 포인터 입력을 차단합니다.")]
    [SerializeField]
    private bool blockRaycastsWhilePlaying = true;

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
        else
        {
            Stop();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnValidate()
    {
        fadeIn = SanitizeDuration(fadeIn);
        stay = SanitizeDuration(stay);
        fadeOut = SanitizeDuration(fadeOut);
    }

    /// <summary>Inspector의 시간으로 재생합니다. 재호출하면 투명 상태부터 다시 시작합니다.</summary>
    public void Play()
    {
        Play(fadeIn, stay, fadeOut);
    }

    /// <summary>이번 재생의 구간 시간을 지정합니다. 비활성 상태에서는 재생하지 않습니다.</summary>
    public void Play(float fadeInDuration, float stayDuration, float fadeOutDuration)
    {
        Stop();
        if (!isActiveAndEnabled)
        {
            return;
        }

        IsPlaying = true;
        canvasGroup.blocksRaycasts = blockRaycastsWhilePlaying;
        Coroutine started = StartCoroutine(Fade(
            SanitizeDuration(fadeInDuration),
            SanitizeDuration(stayDuration),
            SanitizeDuration(fadeOutDuration)));

        // 완료 후 닫기 대기 중에도 Stop이나 재호출로 취소할 수 있도록 보관합니다.
        fadeCoroutine = IsPlaying ? started : null;
    }

    /// <summary>재생을 취소하고 투명 상태로 돌립니다. 포인터 입력 차단도 해제합니다.</summary>
    public void Stop()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        IsPlaying = false;
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private IEnumerator Fade(float fadeInDuration, float stayDuration, float fadeOutDuration)
    {
        if (fadeInDuration > 0f)
        {
            yield return FadeAlpha(0f, 1f, fadeInDuration);
        }

        canvasGroup.alpha = 1f;
        float elapsed = 0f;
        while (elapsed < stayDuration)
        {
            yield return null;
            elapsed += DeltaTime;
        }

        if (fadeOutDuration > 0f)
        {
            yield return FadeAlpha(1f, 0f, fadeOutDuration);
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        // 시간이 모두 0이어도 UIManager의 Instantiate와 등록이 끝난 뒤 닫습니다.
        yield return null;
        IsPlaying = false;
        fadeCoroutine = null;
        CloseAfterPlayback();
    }

    private void CloseAfterPlayback()
    {
        UIManager manager = UIManager.Instance;
        if (manager != null &&
            manager.TryGet(gameObject.name, out GameObject opened) &&
            opened == gameObject)
        {
            manager.Close(gameObject.name);
            return;
        }

        // 씬에 직접 배치한 FadeUI도 재생 완료 후 제거합니다.
        Destroy(gameObject);
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
            elapsed += DeltaTime;
        }

        canvasGroup.alpha = to;
    }

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private static float SanitizeDuration(float duration)
    {
        return float.IsNaN(duration) || float.IsInfinity(duration) ? 0f : Mathf.Max(0f, duration);
    }
}
