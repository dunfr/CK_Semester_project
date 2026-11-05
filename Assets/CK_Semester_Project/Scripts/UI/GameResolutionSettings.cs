using System;
using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.UI
{
    /// <summary>Stores the requested display size independently of the dropdown's option order.</summary>
    public static class GameResolutionSettings
    {
        public const string WidthPreferenceKey = "CK.Settings.Resolution.Width";
        public const string HeightPreferenceKey = "CK.Settings.Resolution.Height";
        public const int DefaultIndex = 5;

        public static IReadOnlyList<Vector2Int> Options { get; } = Array.AsReadOnly(new[]
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1280, 800),
            new Vector2Int(1440, 900),
            new Vector2Int(1600, 900),
            new Vector2Int(1680, 1050),
            new Vector2Int(1920, 1080)
        });

        private static bool s_initialized;
        private static int s_selectedIndex = DefaultIndex;

        public static int SelectedIndex
        {
            get
            {
                Initialize();
                return s_selectedIndex;
            }
        }

        public static Vector2Int SelectedResolution => Options[SelectedIndex];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            s_initialized = false;
            s_selectedIndex = DefaultIndex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (s_initialized)
                return;

            Vector2Int fallback = Options[DefaultIndex];
            int savedIndex = FindIndex(
                PlayerPrefs.GetInt(WidthPreferenceKey, fallback.x),
                PlayerPrefs.GetInt(HeightPreferenceKey, fallback.y));
            s_selectedIndex = savedIndex >= 0 ? savedIndex : DefaultIndex;
            s_initialized = true;
            ApplyDisplaySize();
        }

        public static void SetResolutionIndex(int index)
        {
            Initialize();
            s_selectedIndex = ClampIndex(index);
            Vector2Int size = Options[s_selectedIndex];
            PlayerPrefs.SetInt(WidthPreferenceKey, size.x);
            PlayerPrefs.SetInt(HeightPreferenceKey, size.y);
            ApplyDisplaySize();
#if UNITY_EDITOR
            // 에디터에서도 고르는 즉시 Game 뷰 해상도에 적용해 바로 확인할 수 있게 한다.
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x, (uint)size.y, "Settings");
#endif
        }

        public static int ClampIndex(int index) => Mathf.Clamp(index, 0, Options.Count - 1);

        /// <returns>The supported option index, or -1 when the dimensions are not in the list.</returns>
        public static int FindIndex(int width, int height)
        {
            for (int i = 0; i < Options.Count; i++)
                if (Options[i].x == width && Options[i].y == height)
                    return i;
            return -1;
        }

        private static void ApplyDisplaySize()
        {
#if !UNITY_EDITOR
            Vector2Int size = Options[s_selectedIndex];
            // 해상도만 바꾸고 전체 화면/창 모드는 지금 상태 그대로 둔다(창 모드로 바꾸지 않는다).
            Screen.SetResolution(size.x, size.y, Screen.fullScreenMode, Screen.currentResolution.refreshRateRatio);
#endif
        }
    }
}
