using System;
using System.IO;
using System.Linq;
using CK.SemesterProject.UI;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
internal static class TitleBannerVerification
{
    private const string Pending = "TitleBannerVerification.Pending";
    private static double _started;
    private static int _phase;
    private static float _min = 1f, _max;
    private static Keyboard _keyboard;
    private static GameObject _banner;
    private static TestRunnerApi _runner;
    private static int _frames, _lastFrame;

    static TitleBannerVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                _started = EditorApplication.timeSinceStartup;
                _phase = 0;
                _min = 1f;
                _max = 0f;
                _frames = 0;
                _lastFrame = -1;
                _banner = GameObject.Find("Canvas/Press Any key Banner");
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
                EditorApplication.update += VerifyUpdate;
            }
        };
    }

    [MenuItem("Tools/Title Screen/Verify Runtime Banner")]
    private static void VerifyRuntime()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void VerifyUpdate()
    {
        if (!EditorApplication.isPlaying) { Finish("FAIL: Play Mode stopped"); return; }
        double elapsed = EditorApplication.timeSinceStartup - _started;
        if (_banner == null) { Finish("FAIL: banner not found"); return; }
        if (_lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;
        _frames++;
        if (_phase == 0)
        {
            float alpha = _banner.GetComponent<CanvasGroup>().alpha;
            _min = Mathf.Min(_min, alpha);
            _max = Mathf.Max(_max, alpha);
            if (_frames < 180) return;
            var animator = _banner.GetComponent<Animator>();
            Debug.Log($"[TitleBannerTest] Runtime alpha range={_min:F3}..{_max:F3}; frames={_frames}; animator={animator.enabled}, state={animator.GetCurrentAnimatorStateInfo(0).shortNameHash}, normalizedTime={animator.GetCurrentAnimatorStateInfo(0).normalizedTime}; focus={Application.isFocused}; keyboard={Keyboard.current}");
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            _phase = 1;
            _frames = 0;
            _started = EditorApplication.timeSinceStartup;
            return;
        }
        if (_frames < 3) return;
        bool passed = _min < 0.02f && _max > 0.98f && !_banner.activeSelf;
        Finish($"{(passed ? "PASS" : "FAIL")}: alpha={_min:F3}..{_max:F3}, bannerActiveAfterSpace={_banner.activeSelf}, focus={Application.isFocused}");
    }

    private static void Finish(string result)
    {
        Debug.Log("[TitleBannerTest] " + result);
        File.WriteAllText("Temp/TitleBannerRuntimeResult.txt", result);
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= VerifyUpdate;
        if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        EditorApplication.isPlaying = false;
    }

    [MenuItem("Tools/Title Screen/Run Animation EditMode Tests")]
    private static void RunTests()
    {
        _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        _runner.RegisterCallbacks(new Results());
        _runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "CK.UI.Animation.Tests" } }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren && result.FailCount > 0) Debug.LogError("[AnimationTests] " + result.FullName + ": " + result.Message);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            string summary = $"passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}";
            File.WriteAllText("Temp/UIAnimationEditModeResult.txt", summary);
            Debug.Log("[AnimationTests] " + summary);
        }
    }
}
