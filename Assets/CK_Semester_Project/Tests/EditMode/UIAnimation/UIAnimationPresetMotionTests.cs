using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationPresetMotionTests
    {
        private readonly List<Object> _created = new List<Object>();
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static IEnumerable Styles => Enum.GetValues(typeof(UIAnimationStyle));

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [TestCaseSource(nameof(Styles))]
        public void EveryStyle_EndsAtRestHidesOnCloseAndMeetsBudgets(UIAnimationStyle style)
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(style);
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
            UIAnimationStateSettings close = preset.Get(UIAnimationState.Close);
            UIAnimationStateSettings stay = preset.Get(UIAnimationState.Stay);
            UIAnimationSpan rest = preset.GetSpan(UIAnimationState.Open);
            Assert.That(rest.Offset.To, Is.EqualTo(Vector2.zero));
            Assert.That(rest.Scale.To, Is.EqualTo((Vector2)preset.BaseScale));
            Assert.That(rest.Rotation.To, Is.Zero);
            Assert.That(rest.Color.To, Is.EqualTo(preset.BaseColor));
            Assert.That(rest.Wipe.To, Is.EqualTo(1f));
            if (open.Offset.Use) Assert.That(open.Offset.From.magnitude, Is.InRange(24f, 140f));
            if (close.Offset.Use) Assert.That(close.Offset.To.magnitude, Is.InRange(24f, 140f));
            Assert.That(Mathf.Abs(open.Rotation.From), Is.LessThan(12f));
            Assert.That(open.Duration, Is.InRange(0.16f, 0.6f));
            Assert.That(close.Duration, Is.LessThan(open.Duration));
            Assert.That(close.Duration / open.Duration, Is.InRange(0.55f, 0.75f));
            Assert.That(stay.Duration, Is.InRange(2.8f, 5f));
            Assert.That(stay.AutoClose, Is.False);
            Assert.That(close.KeepEnabled, Is.False);
            UIAnimationSpan hidden = preset.GetSpan(UIAnimationState.Close);
            Assert.That(hidden.Color.To.a == 0f || hidden.Wipe.To == 0f, Is.True);
            UIAnimationSpan idle = preset.GetSpan(UIAnimationState.Stay);
            Assert.That((idle.Offset.To - rest.Offset.To).magnitude, Is.LessThanOrEqualTo(6f));
            Assert.That(Mathf.Abs(idle.Scale.To.x / rest.Scale.To.x - 1f), Is.LessThanOrEqualTo(0.012f));
            Assert.That(Mathf.Abs(idle.Scale.To.y / rest.Scale.To.y - 1f), Is.LessThanOrEqualTo(0.012f));
            Assert.That(idle.Color.To.a / rest.Color.To.a, Is.GreaterThanOrEqualTo(0.85f));

            var restValues = new Dictionary<string, float>();
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState)))
            {
                UIAnimationStateSettings settings = preset.Get(state);
                foreach (UIEaseCurve curve in Curves(settings))
                {
                    if (curve.Ease != UIEase.Custom) continue;
                    Assert.That(curve.Evaluate(0f), Is.EqualTo(0f).Within(0.00001f));
                    Assert.That(curve.Evaluate(1f), Is.EqualTo(1f).Within(0.00001f));
                }
                AnimationClip clip = NewClip();
                Assert.DoesNotThrow(() => UIAnimationClipBaker.Bake(preset, state, clip));
                Assert.That(clip.length, Is.EqualTo(settings.Time).Within(0.001f));
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve baked = AnimationUtility.GetEditorCurve(clip, binding);
                    if (state == UIAnimationState.Open) restValues[binding.propertyName] = baked.Evaluate(clip.length);
                    else Assert.That(baked.Evaluate(0f), Is.EqualTo(restValues[binding.propertyName]).Within(0.001f), binding.propertyName);
                    for (int i = 0; i <= 128; i++)
                    {
                        float value = baked.Evaluate(clip.length * i / 128f);
                        Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False, binding.propertyName);
                    }
                    if (state == UIAnimationState.Stay)
                        Assert.That(baked.Evaluate(clip.length), Is.EqualTo(baked.Evaluate(0f)).Within(0.001f), binding.propertyName);
                }
                AssertBakedEnd(preset, state, clip);
            }
            if (open.Color.Use && style != UIAnimationStyle.ZenlessGlitch)
                Assert.That(open.Color.Curve.Evaluate(0.4f), Is.EqualTo(1f).Within(0.0001f));
            if (close.Color.Use) Assert.That(close.Color.Curve.Evaluate(0.5f), Is.Zero.Within(0.0001f));
            for (int i = 0; i <= 100; i++)
                foreach (UIEaseCurve curve in Curves(close)) Assert.That(curve.Evaluate(i / 100f), Is.InRange(0f, 1f));
            foreach (UIEaseCurve curve in Curves(close))
                for (int i = 1; i <= 100; i++)
                    Assert.That(curve.Evaluate(i / 100f), Is.GreaterThanOrEqualTo(curve.Evaluate((i - 1) / 100f) - 0.00001f));
            bool stepped = style == UIAnimationStyle.ZenlessTv || style == UIAnimationStyle.ZenlessSnap
                || style == UIAnimationStyle.ZenlessGlitch || style == UIAnimationStyle.ZenlessSticker || style == UIAnimationStyle.JrpgCommand;
            Assert.That(open.StepsPerSecond > 0, Is.EqualTo(stepped));
            Assert.That(close.StepsPerSecond > 0, Is.EqualTo(stepped));
        }

        [Test]
        public void SplitCurves_BakeIndependentAxesAndOffClearsSplit()
        {
            UIAnimationPreset preset = MakePreset();
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
            open.Scale.Set(Vector2.zero, (Vector2)preset.BaseScale, UIEase.Linear);
            open.Scale.Curve.SetCustom(UIMotionCurves.Window(UIEase.Linear, 0f, 0.4f));
            open.Scale.SetYCurve(UIMotionCurves.Window(UIEase.Linear, 0.6f, 1f));
            open.Offset.Set(new Vector2(-100f, 80f), Vector2.zero, UIEase.Linear);
            open.Offset.Curve.SetCustom(UIMotionCurves.Window(UIEase.Linear, 0f, 0.4f));
            open.Offset.SetYCurve(UIMotionCurves.Window(UIEase.Linear, 0.6f, 1f));
            AnimationClip clip = NewClip();
            UIAnimationClipBaker.Bake(preset, UIAnimationState.Open, clip);
            Assert.That(Curve(clip, typeof(RectTransform), "m_LocalScale.x").Evaluate(open.Time * 0.5f), Is.EqualTo(2f).Within(0.001f));
            Assert.That(Curve(clip, typeof(RectTransform), "m_LocalScale.y").Evaluate(open.Time * 0.5f), Is.Zero.Within(0.001f));
            Assert.That(Curve(clip, typeof(UIAnimationPlayer), "_animatedOffset.x").Evaluate(open.Time * 0.5f), Is.Zero.Within(0.001f));
            Assert.That(Curve(clip, typeof(UIAnimationPlayer), "_animatedOffset.y").Evaluate(open.Time * 0.5f), Is.EqualTo(80f).Within(0.001f));
            open.Scale.Off();
            Assert.That(open.Scale.SplitY, Is.False);
            Assert.That(open.Scale.CurveY, Is.SameAs(open.Scale.Curve));
        }

        [Test]
        public void MirrorOpenToClose_ReversesCustomSplitAndFlickerCurves()
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(UIAnimationStyle.PersonaDrift);
            var open = preset.Get(UIAnimationState.Open);
            open.Color.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (0.13f, 1f), (0.21f, 0.4f), (0.32f, 1f), (1f, 1f)));
            open.Rotation.Curve.SetCustom(UIMotionCurves.Anticipate(0.08f, 0.17f, UIEase.EaseOutCubic));
            open.Wipe.Set(0f, 1f, UIEase.Linear);
            open.Wipe.Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutQuad, 0.05f, 0.7f));
            preset.MirrorOpenToClose();
            var close = preset.Get(UIAnimationState.Close);
            Assert.That(close.Offset.SplitY, Is.True);
            UIEaseCurve[] a = Curves(open).ToArray();
            UIEaseCurve[] b = Curves(close).ToArray();
            for (int k = 0; k < a.Length; k++)
            {
                Assert.That(b[k].Evaluate(0f), Is.EqualTo(1f - a[k].Evaluate(1f)).Within(0.0001f));
                Assert.That(b[k].Evaluate(1f), Is.EqualTo(1f - a[k].Evaluate(0f)).Within(0.0001f));
                for (int i = 0; i < 200; i++)
                {
                    float t = (i + 0.37f) / 200f;
                    Assert.That(b[k].Evaluate(t), Is.EqualTo(1f - a[k].Evaluate(1f - t)).Within(0.0001f));
                }
            }
            Assert.DoesNotThrow(() => UIAnimationClipBaker.Bake(preset, UIAnimationState.Close, NewClip()));
        }

        [Test]
        public void CopyMotionFrom_CarriesCustomSplitCurvesAndSavedName()
        {
            UIAnimationPreset source = MakePreset();
            UIAnimationPreset target = MakePreset();
            source.name = "저장한 카드";
            source.ApplyStyle(UIAnimationStyle.ZenlessTv);
            source.MarkStatePresetEdited();
            source.MarkPointerPresetEdited();
            target.CopyMotionFrom(source);
            Assert.That(target.StatePresetLabel, Is.EqualTo("적용한 프리셋: 저장한 카드"));
            Assert.That(target.PointerPresetLabel, Is.EqualTo("적용한 프리셋: 저장한 카드"));
            Assert.That(target.Get(UIAnimationState.Open).Scale.SplitY, Is.True);
            for (int i = 0; i <= 100; i++)
            {
                Assert.That(target.Get(UIAnimationState.Open).Scale.Curve.Evaluate(i / 100f),
                    Is.EqualTo(source.Get(UIAnimationState.Open).Scale.Curve.Evaluate(i / 100f)).Within(0.00001f));
                Assert.That(target.Get(UIAnimationState.Open).Scale.CurveY.Evaluate(i / 100f),
                    Is.EqualTo(source.Get(UIAnimationState.Open).Scale.CurveY.Evaluate(i / 100f)).Within(0.00001f));
            }
            source.Get(UIAnimationState.Open).Scale.CurveY.SetCustom(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            Assert.That(target.Get(UIAnimationState.Open).Scale.CurveY.Evaluate(0.2f), Is.Zero.Within(0.00001f));
        }

        [Test]
        public void PresetLabels_KeepStateAndPointerEditsSeparateAndClearOnApply()
        {
            UIAnimationPreset preset = MakePreset(false);
            Assert.That(preset.StatePresetLabel, Is.EqualTo("적용한 프리셋: 없음"));
            Assert.That(preset.PointerPresetLabel, Is.EqualTo("적용한 프리셋: 없음"));
            preset.ApplyStyle(UIAnimationStyle.PersonaSlash);
            preset.ApplyPointerStyle(UIPointerStyle.Glow);
            preset.MarkStatePresetEdited();
            Assert.That(preset.StatePresetLabel, Is.EqualTo("적용한 프리셋: 페르소나풍/사선 카드 (세부 조정함)"));
            Assert.That(preset.PointerPresetEdited, Is.False);
            preset.MarkPointerPresetEdited();
            preset.ApplyStyle(UIAnimationStyle.Fade);
            Assert.That(preset.StatePresetEdited, Is.False);
            Assert.That(preset.PointerPresetEdited, Is.True);
            preset.ApplyPointerStyle(UIPointerStyle.Grow);
            Assert.That(preset.PointerPresetEdited, Is.False);
            preset.ApplyStayStyle(UIStayStyle.Pulse);
            Assert.That(preset.StatePresetName, Is.EqualTo("기본/페이드"));
            Assert.That(preset.StayPresetName, Is.EqualTo("은은하게 깜빡이기"));
            Assert.That(preset.StayPresetLabel, Is.EqualTo("적용한 프리셋: 은은하게 깜빡이기"));
            preset.ApplyStyle(UIAnimationStyle.Pop);
            Assert.That(preset.StayPresetLabel, Is.EqualTo("적용한 프리셋: 기본/팝업"));
        }

        [Test]
        public void Inspector_ManualEditsUseTheCorrectLabels()
        {
            UIAnimationPreset preset = MakePreset();
            var component = ((GameObject)_created[0]).AddComponent<UIAnimationEditor>();
            var editor = (UIAnimationEditorInspector)UnityEditor.Editor.CreateEditor(component);
            _created.Add(editor);
            var data = new SerializedObject(preset);
            Set(editor, "_presetObject", data);
            Type page = typeof(UIAnimationEditorInspector).GetNestedType("Page", BindingFlags.NonPublic);
            Set(editor, "_page", Enum.ToObject(page, 0));
            Call(editor, "MarkManualChange");
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(preset.StatePresetEdited, Is.True);
            Assert.That(preset.PointerPresetEdited, Is.False);
            Set(editor, "_page", Enum.ToObject(page, 1));
            Call(editor, "MarkManualChange");
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(preset.PointerPresetEdited, Is.True);
        }

        [Test]
        public void ApplyStyle_ResetsAllStatesAndLeavesPointerSettingsUntouched()
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyPointerStyle(UIPointerStyle.Nudge);
            string before = JsonUtility.ToJson(preset.Get(UIPointerEffect.Hover));
            preset.Get(UIAnimationState.Stay).Scale.Set(Vector2.one, Vector2.one * 2f, UIEase.EaseOutElastic);
            preset.Get(UIAnimationState.Open).Speed = 7f;
            preset.ApplyStyle(UIAnimationStyle.JrpgDialogue);
            Assert.That(preset.IsStayStatic, Is.True);
            Assert.That(preset.Get(UIAnimationState.Open).Speed, Is.EqualTo(1f));
            Assert.That(JsonUtility.ToJson(preset.Get(UIPointerEffect.Hover)), Is.EqualTo(before));
        }

        [Test]
        public void ApplyingPresets_KeepsEnableAndAutoCloseChoices()
        {
            UIAnimationPreset preset = MakePreset();
            UIAnimationStateSettings stay = preset.Get(UIAnimationState.Stay);
            stay.AutoClose = true;
            stay.Time = 2f;
            preset.Get(UIAnimationState.Open).KeepEnabled = false;
            preset.Get(UIAnimationState.Close).KeepEnabled = true;

            preset.ApplyStyle(UIAnimationStyle.JrpgSummon);
            preset.ApplyStayStyle(UIStayStyle.Pulse);

            Assert.That(stay.AutoClose, Is.True);
            Assert.That(stay.Time, Is.EqualTo(2f));
            Assert.That(preset.Get(UIAnimationState.Open).KeepEnabled, Is.False);
            Assert.That(preset.Get(UIAnimationState.Close).KeepEnabled, Is.True);
        }

        [Test]
        public void Pulse_UsesShownAndDimEndpointsAndReturnsWithoutAPop()
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStayStyle(UIStayStyle.Pulse);
            UIColorTrack color = preset.Get(UIAnimationState.Stay).Color;
            Assert.That(color.From, Is.EqualTo(preset.BaseColor));
            Assert.That(color.To.a, Is.EqualTo(preset.BaseColor.a * 0.9f).Within(0.0001f));
            Assert.That(preset.IsStayStatic, Is.False);
            AnimationClip clip = NewClip();
            UIAnimationClipBaker.Bake(preset, UIAnimationState.Stay, clip);
            AnimationCurve alpha = Curve(clip, typeof(Image), "m_Color.a");
            Assert.That(alpha.Evaluate(0f), Is.EqualTo(preset.BaseColor.a).Within(0.001f));
            Assert.That(alpha.Evaluate(clip.length * 0.5f), Is.EqualTo(color.To.a).Within(0.001f));
            Assert.That(alpha.Evaluate(clip.length), Is.EqualTo(preset.BaseColor.a).Within(0.001f));
        }

        [Test]
        public void OldPreset_WithoutNewFieldsKeepsItsTimingSingleCurveAndBakedValues()
        {
            UIAnimationPreset preset = MakePreset(false);
            JsonUtility.FromJsonOverwrite(@"{""_open"":{""_time"":0.25,""_offset"":{""_use"":true,""_from"":{""x"":-80,""y"":20},""_to"":{""x"":0,""y"":0},""_curve"":{""_ease"":8}}},""_stay"":{""_time"":1},""_close"":{""_time"":0.2,""_keepEnabled"":false}}", preset);
            Assert.That(preset.StatePresetLabel, Is.EqualTo("적용한 프리셋: 없음"));
            Assert.That(preset.Get(UIAnimationState.Open).Offset.SplitY, Is.False);
            Assert.That(preset.Get(UIAnimationState.Stay).Time, Is.EqualTo(1f));
            AnimationClip clip = NewClip();
            UIAnimationClipBaker.Bake(preset, UIAnimationState.Open, clip);
            AnimationCurve x = Curve(clip, typeof(UIAnimationPlayer), "_animatedOffset.x");
            AnimationCurve y = Curve(clip, typeof(UIAnimationPlayer), "_animatedOffset.y");
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                float remaining = Mathf.Pow(1f - t, 3f);
                Assert.That(x.Evaluate(t * 0.25f), Is.EqualTo(-80f * remaining).Within(0.01f));
                Assert.That(y.Evaluate(t * 0.25f), Is.EqualTo(20f * remaining).Within(0.01f));
            }
            Assert.That(clip.length, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(preset.Get(UIAnimationState.Close).Time, Is.EqualTo(0.2f));
        }

        private UIAnimationPreset MakePreset(bool capture = true)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _created.Add(go);
            Image image = go.GetComponent<Image>();
            image.rectTransform.localScale = new Vector3(2f, 0.75f, 1.5f);
            image.color = new Color(0.35f, 0.6f, 0.9f, 0.95f);
            var preset = ScriptableObject.CreateInstance<UIAnimationPreset>();
            _created.Add(preset);
            if (capture) preset.Capture(image, false, UIPointerEffectMask.None);
            return preset;
        }

        private AnimationClip NewClip()
        {
            var clip = new AnimationClip();
            _created.Add(clip);
            return clip;
        }

        private static IEnumerable<UIEaseCurve> Curves(UIAnimationStateSettings settings)
        {
            yield return settings.Offset.Curve;
            yield return settings.Offset.CurveY;
            yield return settings.Scale.Curve;
            yield return settings.Scale.CurveY;
            yield return settings.Rotation.Curve;
            yield return settings.Color.Curve;
            yield return settings.Wipe.Curve;
        }

        private static AnimationCurve Curve(AnimationClip clip, Type type, string property)
            => AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, type, property));

        private static void AssertBakedEnd(UIAnimationPreset preset, UIAnimationState state, AnimationClip clip)
        {
            UIAnimationSpan span = preset.GetSpan(state);
            bool stay = state == UIAnimationState.Stay;
            var values = new Dictionary<string, float>
            {
                { "_animatedOffset.x", stay ? span.Offset.From.x : span.Offset.To.x },
                { "_animatedOffset.y", stay ? span.Offset.From.y : span.Offset.To.y },
                { "m_LocalScale.x", stay ? span.Scale.From.x : span.Scale.To.x },
                { "m_LocalScale.y", stay ? span.Scale.From.y : span.Scale.To.y },
                { "m_LocalScale.z", preset.BaseScale.z },
                { "_animatedRotation", stay ? span.Rotation.From : span.Rotation.To },
                { "m_Color.r", stay ? span.Color.From.r : span.Color.To.r },
                { "m_Color.g", stay ? span.Color.From.g : span.Color.To.g },
                { "m_Color.b", stay ? span.Color.From.b : span.Color.To.b },
                { "m_Color.a", stay ? span.Color.From.a : span.Color.To.a },
                { "m_FillAmount", stay ? span.Wipe.From : span.Wipe.To },
                { "m_Enabled", state == UIAnimationState.Close ? 0f : 1f }
            };
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                Assert.That(AnimationUtility.GetEditorCurve(clip, binding).Evaluate(clip.length),
                    Is.EqualTo(values[binding.propertyName]).Within(0.001f), binding.propertyName);
        }

        private static void Set(object target, string field, object value)
            => typeof(UIAnimationEditorInspector).GetField(field, Private).SetValue(target, value);
        private static object Call(object target, string method, params object[] args)
            => typeof(UIAnimationEditorInspector).GetMethod(method, Private).Invoke(target, args);
    }
}
