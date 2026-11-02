using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.EditorTools
{
    // 프리셋 값을 .anim 커브로 굽는다. 세 클립이 같은 속성을 쓰므로 상태가 바뀌어도 이전 값이 남지 않는다.
    public static class UIAnimationClipBaker
    {
        private sealed class CurveSet
        {
            private readonly List<EditorCurveBinding> _bindings = new List<EditorCurveBinding>();
            private readonly List<AnimationCurve> _curves = new List<AnimationCurve>();
            private readonly float _duration;
            private readonly int _stepsPerSecond;

            public CurveSet(float duration, int stepsPerSecond)
            {
                _duration = duration;
                _stepsPerSecond = stepsPerSecond;
            }

            public void Add(Type type, string property, Func<float, float> value, UIEaseCurve shape = null, bool roundTrip = false)
            {
                _bindings.Add(EditorCurveBinding.FloatCurve(string.Empty, type, property));
                _curves.Add(_stepsPerSecond > 0
                    ? UIAnimationCurveFitter.Stepped(value, _duration, _stepsPerSecond, shape, roundTrip)
                    : UIAnimationCurveFitter.Fit(value, _duration, shape, roundTrip));
            }

            // 모든 클립에 넣어 다른 항목을 다 꺼도 클립 길이가 Time과 같게 유지된다.
            public void AddEnabled(bool startsOn, bool staysOn)
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, startsOn ? 1f : 0f),
                    new Keyframe(_duration, startsOn && staysOn ? 1f : 0f));
                AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Constant);
                _bindings.Add(EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_Enabled"));
                _curves.Add(curve);
            }

            public void WriteTo(AnimationClip clip)
            {
                clip.ClearCurves();
                AnimationUtility.SetEditorCurves(clip, _bindings.ToArray(), _curves.ToArray());
            }
        }

        public static void Bake(UIAnimationPreset preset, UIAnimationState state, AnimationClip clip)
        {
            Write(preset, state, clip, null);
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = state == UIAnimationState.Stay;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        }

        // 편집 중에는 UIAnimationPlayer가 돌지 않아 상대값이 적용되지 않으므로 실제 좌표로 굽는다.
        public static void BakePreview(UIAnimationPreset preset, UIAnimationState state, RectTransform rect, AnimationClip clip)
        {
            Write(preset, state, clip, rect);
        }

        // 입력 반응은 실행 중에 트윈으로 재생하므로, 미리보기용 임시 클립에만 굽는다.
        public static void BakeEffectPreview(UIAnimationPreset preset, UIPointerEffect effect, Image image, AnimationClip clip)
        {
            UIPointerEffectSettings settings = preset.Get(effect);
            UIPointerEffectSettings before = EffectBefore(preset, effect);
            RectTransform rect = image.rectTransform;
            Vector3 scale = rect.localScale;
            Color color = image.color;
            var curves = new CurveSet(settings.Time, 0);

            if (settings.Offset.Use || before != null && before.Offset.Use)
            {
                Vector2 from = before != null && before.Offset.Use ? before.Offset.To : Vector2.zero;
                Vector2 to = settings.Offset.Use ? settings.Offset.To : Vector2.zero;
                AddOffset(curves, from, to, settings.Offset.Curve, settings.Offset.CurveY, false, rect);
            }
            if (settings.Scale.Use)
            {
                Vector2 from = before != null && before.Scale.Use ? Vector2.Scale(scale, before.Scale.To) : (Vector2)scale;
                AddScale(curves, from, Vector2.Scale(scale, settings.Scale.To), scale.z, settings.Scale.Curve, settings.Scale.CurveY, false);
            }
            if (settings.Rotation.Use || before != null && before.Rotation.Use)
            {
                float from = before != null && before.Rotation.Use ? before.Rotation.To : 0f;
                float to = settings.Rotation.Use ? settings.Rotation.To : 0f;
                AddRotation(curves, from, to, settings.Rotation.Curve, false, rect);
            }
            if (settings.Color.Use)
            {
                Color from = before != null && before.Color.Use ? color * before.Color.To : color;
                AddColor(curves, from, color * settings.Color.To, settings.Color.Curve, false, false);
            }
            curves.AddEnabled(image.enabled, true);
            curves.WriteTo(clip);

            Sprite sprite = EffectSprite(preset, effect);
            if (sprite != null)
            {
                var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(Image), "m_Sprite");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] { new ObjectReferenceKeyframe { value = sprite } });
            }
        }

        // 밝기는 클립에 담을 수 없는 머티리얼 값이라 미리보기가 재생 위치에 맞춰 직접 넣는다. 밝기가 변하지 않으면 null이다.
        public static Func<float, float> EffectBrightness(UIAnimationPreset preset, UIPointerEffect effect)
        {
            UIPointerEffectSettings settings = preset.Get(effect);
            UIPointerEffectSettings before = EffectBefore(preset, effect);
            float from = before != null && before.Color.Use ? before.Brightness : 1f;
            float to = settings.Color.Use ? settings.Brightness : 1f;
            if (Mathf.Approximately(from, 1f) && Mathf.Approximately(to, 1f))
            {
                return null;
            }
            UIEaseCurve curve = settings.Color.Curve;
            return u => Mathf.Max(0f, Mathf.LerpUnclamped(from, to, curve.Evaluate(u)));
        }

        // Exit는 Hover가 만든 모습에서 돌아온다.
        private static UIPointerEffectSettings EffectBefore(UIAnimationPreset preset, UIPointerEffect effect)
        {
            UIPointerEffectSettings hover = preset.Get(UIPointerEffect.Hover);
            return effect == UIPointerEffect.Exit && hover.Enabled ? hover : null;
        }

        // Pressed에 이미지가 없으면 게임에서처럼 Hover 이미지를 보여 준다.
        private static Sprite EffectSprite(UIAnimationPreset preset, UIPointerEffect effect)
        {
            Sprite sprite = preset.Get(effect).Sprite;
            UIPointerEffectSettings hover = preset.Get(UIPointerEffect.Hover);
            return sprite == null && effect == UIPointerEffect.Pressed && hover.Enabled ? hover.Sprite : sprite;
        }

        private static void Write(UIAnimationPreset preset, UIAnimationState state, AnimationClip clip, RectTransform previewRect)
        {
            UIAnimationStateSettings settings = preset.Get(state);
            UIAnimationSpan span = preset.GetSpan(state);
            bool roundTrip = state == UIAnimationState.Stay;
            var curves = new CurveSet(settings.Time, settings.StepsPerSecond);

            if (preset.AnimatesOffset)
            {
                AddOffset(curves, span.Offset.From, span.Offset.To, settings.Offset.Curve, settings.Offset.CurveY, roundTrip, previewRect);
            }
            if (preset.AnimatesScale)
            {
                AddScale(curves, span.Scale.From, span.Scale.To, preset.BaseScale.z, settings.Scale.Curve, settings.Scale.CurveY, roundTrip);
            }
            if (preset.AnimatesRotation)
            {
                AddRotation(curves, span.Rotation.From, span.Rotation.To, settings.Rotation.Curve, roundTrip, previewRect);
            }
            if (preset.AnimatesColor)
            {
                AddColor(curves, span.Color.From, span.Color.To, settings.Color.Curve, roundTrip, preset.FadeChildren);
            }
            if (preset.AnimatesWipe)
            {
                UIFloatTrack wipe = settings.Wipe;
                float from = span.Wipe.From;
                float to = span.Wipe.To;
                curves.Add(typeof(Image), "m_FillAmount", u => Mathf.Clamp01(Mathf.LerpUnclamped(from, to, Shape(wipe.Curve, u, roundTrip))), wipe.Curve, roundTrip);
            }
            curves.AddEnabled(preset.ImageEnabled, roundTrip || settings.KeepEnabled);
            curves.WriteTo(clip);
        }

        private static void AddOffset(CurveSet curves, Vector2 from, Vector2 to, UIEaseCurve curve, UIEaseCurve curveY, bool roundTrip, RectTransform previewRect)
        {
            Vector2 origin = previewRect != null ? previewRect.anchoredPosition : Vector2.zero;
            Type type = previewRect != null ? typeof(RectTransform) : typeof(UIAnimationPlayer);
            string property = previewRect != null ? "m_AnchoredPosition" : UIAnimationPreset.OffsetField;
            curves.Add(type, property + ".x", u => origin.x + Mathf.LerpUnclamped(from.x, to.x, Shape(curve, u, roundTrip)), curve, roundTrip);
            curves.Add(type, property + ".y", u => origin.y + Mathf.LerpUnclamped(from.y, to.y, Shape(curveY, u, roundTrip)), curveY, roundTrip);
        }

        private static void AddScale(CurveSet curves, Vector2 from, Vector2 to, float z, UIEaseCurve curve, UIEaseCurve curveY, bool roundTrip)
        {
            curves.Add(typeof(RectTransform), "m_LocalScale.x", u => Mathf.LerpUnclamped(from.x, to.x, Shape(curve, u, roundTrip)), curve, roundTrip);
            curves.Add(typeof(RectTransform), "m_LocalScale.y", u => Mathf.LerpUnclamped(from.y, to.y, Shape(curveY, u, roundTrip)), curveY, roundTrip);
            curves.Add(typeof(RectTransform), "m_LocalScale.z", u => z);
        }

        private static void AddRotation(CurveSet curves, float from, float to, UIEaseCurve curve, bool roundTrip, RectTransform previewRect)
        {
            if (previewRect == null)
            {
                curves.Add(typeof(UIAnimationPlayer), UIAnimationPreset.RotationField, u => Mathf.LerpUnclamped(from, to, Shape(curve, u, roundTrip)), curve, roundTrip);
                return;
            }

            Vector3 origin = previewRect.localEulerAngles;
            curves.Add(typeof(RectTransform), "localEulerAnglesRaw.x", u => origin.x);
            curves.Add(typeof(RectTransform), "localEulerAnglesRaw.y", u => origin.y);
            curves.Add(typeof(RectTransform), "localEulerAnglesRaw.z", u => origin.z + Mathf.LerpUnclamped(from, to, Shape(curve, u, roundTrip)), curve, roundTrip);
        }

        // 색은 0~1을 넘으면 의미가 없어 Back·Elastic이어도 잘라 낸다.
        private static void AddColor(CurveSet curves, Color from, Color to, UIEaseCurve curve, bool roundTrip, bool alphaOnCanvasGroup)
        {
            Func<float, float, float, float> channel =
                (a, b, u) => Mathf.Clamp01(Mathf.LerpUnclamped(a, b, Shape(curve, u, roundTrip)));
            curves.Add(typeof(Image), "m_Color.r", u => channel(from.r, to.r, u), curve, roundTrip);
            curves.Add(typeof(Image), "m_Color.g", u => channel(from.g, to.g, u), curve, roundTrip);
            curves.Add(typeof(Image), "m_Color.b", u => channel(from.b, to.b, u), curve, roundTrip);
            if (alphaOnCanvasGroup)
            {
                curves.Add(typeof(CanvasGroup), "m_Alpha", u => channel(from.a, to.a, u), curve, roundTrip);
            }
            else
            {
                curves.Add(typeof(Image), "m_Color.a", u => channel(from.a, to.a, u), curve, roundTrip);
            }
        }

        // Stay는 목표값까지 갔다가 되돌아와 끊김 없이 반복된다.
        private static float Shape(UIEaseCurve curve, float u, bool roundTrip)
        {
            return curve.Evaluate(roundTrip ? 1f - Mathf.Abs(2f * u - 1f) : u);
        }
    }
}
