using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CK.SemesterProject.Rendering
{
    // 게임 화면의 안티앨리어싱 방식을 고르고 적용한다. 씬에 아무것도 놓지 않아도 게임이 시작될 때 스스로 켜진다.
    //
    // 옵션 UI에서 쓰는 법:
    //   AntiAliasingSettings.Mode = AntiAliasingMode.Fsr2;      // 바꾸면 바로 적용되고 저장된다
    //   AntiAliasingSettings.IsSupported(AntiAliasingMode.Fsr2)  // 이 기기에서 고를 수 있는지(항목을 비활성화할 때)
    //   AntiAliasingSettings.ModeChanged += OnModeChanged;       // 값이 바뀔 때 UI를 갱신하려면
    //
    // SMAA와 끄기는 카메라의 URP 안티앨리어싱 값으로 적용한다.
    // FSR 2는 URP 에셋의 업스케일 필터를 FSR 2로 돌려 적용한다(렌더 스케일 100%에서는 해상도를 낮추지 않고 다듬기만 한다).
    public static class AntiAliasingSettings
    {
        public const AntiAliasingMode DefaultMode = AntiAliasingMode.Smaa;

        // URP 에셋에 고르는 FSR 2 업스케일러의 이름(Unity 쪽 등록 이름과 같아야 한다).
        public const string Fsr2UpscalerName = "FSR2 (IUpscaler)";

        private const string PrefsKey = "CK.Graphics.AntiAliasing";

        // 고른 방식이 바뀌면 알린다(인자는 새로 고른 방식).
        public static event Action<AntiAliasingMode> ModeChanged;

        private static AntiAliasingMode s_Mode = DefaultMode;
        private static bool s_Initialized;
        private static bool s_Fsr2Checked;
        private static bool s_Fsr2Available;
        private static bool s_WarnedFsr2Fallback;
        // FSR 2를 켜느라 바꾼 URP 에셋의 원래 업스케일 필터(끄거나 게임을 끝낼 때 되돌린다).
        private static readonly Dictionary<UniversalRenderPipelineAsset, UpscalingFilterSelection> s_OriginalFilters =
            new Dictionary<UniversalRenderPipelineAsset, UpscalingFilterSelection>();

        // 플레이어가 고른 방식. 바꾸면 바로 적용하고 저장한다.
        public static AntiAliasingMode Mode
        {
            get
            {
                EnsureLoaded();
                return s_Mode;
            }
            set => SetMode(value, true);
        }

        // 지금 실제로 적용되는 방식. FSR 2를 골랐지만 이 기기가 지원하지 않으면 SMAA다.
        public static AntiAliasingMode AppliedMode
        {
            get
            {
                EnsureLoaded();
                return s_Mode == AntiAliasingMode.Fsr2 && !IsSupported(AntiAliasingMode.Fsr2) ? AntiAliasingMode.Smaa : s_Mode;
            }
        }

        // save가 false면 이번 실행에만 적용하고 저장하지 않는다(미리보기용).
        public static void SetMode(AntiAliasingMode mode, bool save)
        {
            EnsureLoaded();
            if (!Enum.IsDefined(typeof(AntiAliasingMode), mode))
            {
                mode = DefaultMode;
            }

            bool changed = mode != s_Mode;
            s_Mode = mode;
            if (save)
            {
                PlayerPrefs.SetInt(PrefsKey, (int)mode);
                PlayerPrefs.Save();
            }

            s_WarnedFsr2Fallback = false;
            ApplyToPipeline();
            if (changed)
            {
                ModeChanged?.Invoke(mode);
            }
        }

        // 이 기기와 현재 그래픽 설정에서 해당 방식을 쓸 수 있는지.
        public static bool IsSupported(AntiAliasingMode mode)
        {
            if (mode != AntiAliasingMode.Fsr2)
            {
                return true;
            }

            if (!s_Fsr2Checked)
            {
                s_Fsr2Checked = true;
                s_Fsr2Available = CheckFsr2Device();
            }

            return s_Fsr2Available && HasFsr2Selected(UniversalRenderPipeline.asset);
        }

        // 플레이 모드를 다시 시작해도(도메인 리로드를 끈 설정 포함) 깨끗한 상태에서 시작한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RestorePipeline();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Application.quitting -= RestorePipeline;
            ModeChanged = null;
            s_Mode = DefaultMode;
            s_Initialized = false;
            s_Fsr2Checked = false;
            s_Fsr2Available = false;
            s_WarnedFsr2Fallback = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureLoaded();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Application.quitting -= RestorePipeline;
            Application.quitting += RestorePipeline;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
            ApplyToPipeline();
        }

        private static void EnsureLoaded()
        {
            if (s_Initialized)
            {
                return;
            }

            s_Initialized = true;
            int saved = PlayerPrefs.GetInt(PrefsKey, (int)DefaultMode);
            s_Mode = Enum.IsDefined(typeof(AntiAliasingMode), saved) ? (AntiAliasingMode)saved : DefaultMode;
        }

        // 카메라마다 그리기 직전에 맞춘다. 씬이 바뀌거나 카메라가 새로 생겨도 따로 등록할 필요가 없다.
        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // 게임 화면 카메라만 다룬다(씬 뷰·미리보기·바닥 반사용 카메라는 건드리지 않는다).
            if (camera.cameraType != CameraType.Game || !camera.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                return;
            }

            if (data.renderType != CameraRenderType.Base)
            {
                return;
            }

            // 품질 단계가 바뀌어 URP 에셋이 달라졌을 수 있으므로 에셋 쪽도 매번 확인한다(값이 같으면 아무것도 하지 않는다).
            ApplyToPipeline();

            AntiAliasingMode applied = AppliedMode;
            // FSR 2는 스스로 다듬으므로 카메라 쪽 안티앨리어싱은 끈다.
            AntialiasingMode cameraMode = applied == AntiAliasingMode.Smaa
                ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : AntialiasingMode.None;
            if (data.antialiasing != cameraMode)
            {
                data.antialiasing = cameraMode;
            }

            if (applied == AntiAliasingMode.Smaa && data.antialiasingQuality != AntialiasingQuality.High)
            {
                data.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        // FSR 2를 쓸 때만 URP 에셋의 업스케일 필터를 FSR 2로 바꾸고, 아니면 원래 값으로 되돌린다.
        private static void ApplyToPipeline()
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (asset == null)
            {
                return;
            }

#if ENABLE_UPSCALER_FRAMEWORK
            bool wantFsr2 = s_Mode == AntiAliasingMode.Fsr2;
            if (wantFsr2 && IsSupported(AntiAliasingMode.Fsr2))
            {
                if (asset.upscalingFilter != UpscalingFilterSelection.IUpscaler)
                {
                    if (!s_OriginalFilters.ContainsKey(asset))
                    {
                        s_OriginalFilters[asset] = asset.upscalingFilter;
                    }

                    asset.upscalingFilter = UpscalingFilterSelection.IUpscaler;
                }

                return;
            }

            if (wantFsr2 && !s_WarnedFsr2Fallback)
            {
                s_WarnedFsr2Fallback = true;
                Debug.LogWarning("[AntiAliasing] 이 기기 또는 현재 URP 에셋에서는 FSR 2를 쓸 수 없어 SMAA로 대신합니다.");
            }
#endif
            RestoreAsset(asset);
        }

        private static void RestoreAsset(UniversalRenderPipelineAsset asset)
        {
            if (s_OriginalFilters.TryGetValue(asset, out UpscalingFilterSelection original))
            {
                asset.upscalingFilter = original;
                s_OriginalFilters.Remove(asset);
            }
        }

        // 게임을 끝낼 때 URP 에셋을 원래대로 돌린다(에디터에서는 에셋이 프로젝트 파일이라 바뀐 채 남지 않게).
        private static void RestorePipeline()
        {
            foreach (KeyValuePair<UniversalRenderPipelineAsset, UpscalingFilterSelection> pair in s_OriginalFilters)
            {
                if (pair.Key != null)
                {
                    pair.Key.upscalingFilter = pair.Value;
                }
            }

            s_OriginalFilters.Clear();
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                RestorePipeline();
            }
        }
#endif

        // URP 에셋에 FSR 2 업스케일러가 골라져 있는지. 이름은 에셋에 저장되는 값이라 에디터에서 한 번 맞춰 둔다.
        private static bool HasFsr2Selected(UniversalRenderPipelineAsset asset)
        {
#if ENABLE_UPSCALER_FRAMEWORK
            return asset != null && asset.upscalerName == Fsr2UpscalerName && asset.GetIUpscalerOptions(Fsr2UpscalerName) != null;
#else
            return false;
#endif
        }

        // FSR 2 플러그인이 이 기기·그래픽 API에서 실제로 동작하는지 한 번만 확인한다.
        private static bool CheckFsr2Device()
        {
#if ENABLE_UPSCALER_FRAMEWORK && ENABLE_AMD && CK_AMD_MODULE
            try
            {
                if (!UnityEngine.AMD.AMDUnityPlugin.IsLoaded() && !UnityEngine.AMD.AMDUnityPlugin.Load())
                {
                    return false;
                }

                return UnityEngine.AMD.GraphicsDevice.device != null || UnityEngine.AMD.GraphicsDevice.CreateGraphicsDevice() != null;
            }
            catch (Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }
    }
}
