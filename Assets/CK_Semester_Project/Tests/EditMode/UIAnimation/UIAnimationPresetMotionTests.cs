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
            if (open.Offset.Use) Assert.That(open.Offset.From.magnitude, Is.InRange(1f, 140f));
            if (close.Offset.Use) Assert.That(close.Offset.To.magnitude, Is.InRange(1f, 140f));
            Assert.That(Mathf.Abs(open.Rotation.From), Is.LessThan(12f));
            Assert.That(open.Duration, Is.InRange(0.18f, 0.5f));
            Assert.That(close.Duration, Is.LessThan(open.Duration));
            Assert.That(close.Duration / open.Duration, Is.InRange(0.5f, 0.7f));
            Assert.That(stay.Duration, Is.InRange(3.6f, 12f));
            Assert.That(stay.AutoClose, Is.False);
            Assert.That(close.KeepEnabled, Is.False);
            UIAnimationSpan hidden = preset.GetSpan(UIAnimationState.Close);
            Assert.That(hidden.Color.To.a == 0f || hidden.Wipe.To == 0f, Is.True);
            UIAnimationSpan idle = preset.GetSpan(UIAnimationState.Stay);
            Assert.That((idle.Offset.To - rest.Offset.To).magnitude, Is.LessThanOrEqualTo(1.5f));
            Assert.That(Mathf.Abs(idle.Scale.To.x / rest.Scale.To.x - 1f), Is.LessThanOrEqualTo(0.012f));
            Assert.That(Mathf.Abs(idle.Scale.To.y / rest.Scale.To.y - 1f), Is.LessThanOrEqualTo(0.012f));
            Assert.That(idle.Color.To.a / rest.Color.To.a, Is.GreaterThanOrEqualTo(0.85f));

            var restValues = new Dictionary<string, float>();
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState)))
            {
                UIAnimationStateSettings settings = preset.Get(state);
                // 충격 트랙은 쉬는 값에서 시작해 중간에만 켜질 수 있으므로 실제 베이크 값을 검사한다.
                AnimationClip clip = NewClip();
                Assert.DoesNotThrow(() => UIAnimationClipBaker.Bake(preset, state, clip));
                Assert.That(clip.length, Is.EqualTo(settings.Time).Within(0.001f));
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve baked = AnimationUtility.GetEditorCurve(clip, binding);
                    if (state == UIAnimationState.Open) restValues[binding.propertyName] = baked.Evaluate(clip.length);
                    else if (binding.propertyName != "_seed")
                        Assert.That(baked.Evaluate(0f), Is.EqualTo(restValues[binding.propertyName]).Within(0.001f), binding.propertyName);
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
            for (int i = 0; i <= 100; i++)
                foreach (UIEaseCurve curve in Curves(close)) Assert.That(curve.Evaluate(i / 100f), Is.InRange(0f, 1f));
            foreach (UIEaseCurve curve in Curves(close))
                for (int i = 1; i <= 100; i++)
                    Assert.That(curve.Evaluate(i / 100f), Is.GreaterThanOrEqualTo(curve.Evaluate((i - 1) / 100f) - 0.00001f));
            bool steppedOpen = style == UIAnimationStyle.ZenlessSnap || style == UIAnimationStyle.ZenlessGlitch || style == UIAnimationStyle.JrpgCommand;
            bool steppedClose = style == UIAnimationStyle.ZenlessGlitch || style == UIAnimationStyle.JrpgCommand;
            Assert.That(open.StepsPerSecond > 0, Is.EqualTo(steppedOpen));
            Assert.That(close.StepsPerSecond > 0, Is.EqualTo(steppedClose));
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
            Color hidden = preset.BaseColor;
            hidden.a = 0f;
            open.Color.Set(hidden, preset.BaseColor, UIEase.Linear);
            open.Color.Curve.SetCustom(UIMotionCurves.Steps((0f, 0f), (0.13f, 1f), (0.21f, 0.4f), (0.32f, 1f), (1f, 1f)));
            open.Rotation.Set(-2f, 0f, UIEase.Linear);
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
            string exit = JsonUtility.ToJson(preset.Get(UIPointerEffect.Exit));
            string pressed = JsonUtility.ToJson(preset.Get(UIPointerEffect.Pressed));
            preset.Get(UIAnimationState.Stay).Scale.Set(Vector2.one, Vector2.one * 2f, UIEase.EaseOutElastic);
            preset.Get(UIAnimationState.Open).Speed = 7f;
            preset.ApplyStyle(UIAnimationStyle.JrpgDialogue);
            Assert.That(preset.IsStayStatic, Is.True);
            Assert.That(preset.Get(UIAnimationState.Open).Speed, Is.EqualTo(1f));
            Assert.That(JsonUtility.ToJson(preset.Get(UIPointerEffect.Hover)), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(preset.Get(UIPointerEffect.Exit)), Is.EqualTo(exit));
            Assert.That(JsonUtility.ToJson(preset.Get(UIPointerEffect.Pressed)), Is.EqualTo(pressed));
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

            stay.KeepEnabled = false;
            foreach (UIAnimationStyle style in Styles)
            {
                preset.ApplyStyle(style);
                foreach (UIStayStyle idle in Enum.GetValues(typeof(UIStayStyle)))
                {
                    preset.ApplyStayStyle(idle);
                    Assert.That(stay.AutoClose, Is.True);
                    Assert.That(stay.Time, Is.EqualTo(2f));
                    Assert.That(stay.KeepEnabled, Is.False);
                    Assert.That(preset.Get(UIAnimationState.Open).KeepEnabled, Is.False);
                    Assert.That(preset.Get(UIAnimationState.Close).KeepEnabled, Is.True);
                }
            }
        }

        [TestCaseSource(nameof(Styles))]
        public void EveryStyle_BakedFxStayInRangeAndOpenEndsWithNoEffect(UIAnimationStyle style)
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(style);
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState)))
            {
                AnimationClip clip = NewClip();
                UIAnimationClipBaker.Bake(preset, state, clip);
                UIAnimationSpan span = preset.GetSpan(state);
                var ranges = new[] { span.Wipe, span.Skew, span.Slice, span.Split, span.Scan, span.Dissolve, span.Sheen, span.Flash };
                int fxIndex = 0;
                foreach (var fx in FxTracks(preset.Get(state)))
                {
                    UIRange<float> range = ranges[fxIndex++];
                    // 베이크의 Clamp가 잘못된 원본 값을 가리지 않게 양쪽을 함께 검사한다.
                    for (int i = 0; i <= 1000; i++)
                    {
                        float u = i / 1000f;
                        float t = state == UIAnimationState.Stay ? 1f - Mathf.Abs(2f * u - 1f) : u;
                        float value = Mathf.LerpUnclamped(range.From, range.To, fx.track.Curve.Evaluate(t));
                        Assert.That(value, Is.InRange(fx.name == "Skew" ? -45.001f : -0.001f, fx.name == "Skew" ? 45.001f : 1.001f),
                            style + ":" + state + ":" + fx.name + " 원본");
                    }
                    AnimationCurve curve = Curve(clip, typeof(UIAnimationFx), "_" + fx.name.ToLowerInvariant());
                    if (curve == null) continue;
                    for (int i = 0; i <= 1000; i++)
                        Assert.That(curve.Evaluate(clip.length * i / 1000f),
                            Is.InRange(fx.name == "Skew" ? -45.001f : -0.001f, fx.name == "Skew" ? 45.001f : 1.001f),
                            style + ":" + state + ":" + fx.name);
                    if (state == UIAnimationState.Open)
                        Assert.That(curve.Evaluate(clip.length), Is.EqualTo(fx.name == "Wipe" ? 1f : 0f).Within(0.0001f));
                }
                foreach (string axis in new[] { "x", "y" })
                {
                    AnimationCurve curve = Curve(clip, typeof(RectTransform), "m_LocalScale." + axis);
                    if (curve == null) continue;
                    float rest = axis == "x" ? preset.BaseScale.x : preset.BaseScale.y;
                    for (int i = 0; i <= 1000; i++)
                        Assert.That(curve.Evaluate(clip.length * i / 1000f) / rest, Is.InRange(0.01f, 1.25f));
                }
            }
        }

        [TestCaseSource(nameof(Styles))]
        public void EveryStyle_FlashHasFadedBy120MillisecondsAndCloseOnlyAccelerates(UIAnimationStyle style)
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(style);
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open), close = preset.Get(UIAnimationState.Close);
            if (open.Flash.Use)
            {
                AnimationClip clip = NewClip();
                UIAnimationClipBaker.Bake(preset, UIAnimationState.Open, clip);
                AnimationCurve flash = Curve(clip, typeof(UIAnimationFx), "_flash");
                float peak = Enumerable.Range(0, 1001).Max(i => flash.Evaluate(clip.length * i / 1000f));
                Assert.That(peak, Is.GreaterThan(0.5f));
                for (int i = 0; i <= 1000; i++)
                    Assert.That(flash.Evaluate(Mathf.Lerp(0.12f, clip.length, i / 1000f)), Is.LessThanOrEqualTo(peak * 0.5f + 0.001f));
            }
            Assert.That(close.Flash.Use, Is.False);
            var curves = new List<UIEaseCurve>();
            if (close.Offset.Use) { curves.Add(close.Offset.Curve); curves.Add(close.Offset.CurveY); }
            if (close.Scale.Use) { curves.Add(close.Scale.Curve); curves.Add(close.Scale.CurveY); }
            if (close.Rotation.Use) curves.Add(close.Rotation.Curve);
            if (close.Color.Use) curves.Add(close.Color.Curve);
            curves.AddRange(FxTracks(close).Where(fx => fx.track.Use).Select(fx => fx.track.Curve));
            foreach (UIEaseCurve curve in curves)
            {
                Assert.That(curve.Evaluate(0.25f), Is.LessThan(0.25f));
                for (int i = 0; i <= 1000; i++)
                {
                    float value = curve.Evaluate(i / 1000f);
                    Assert.That(value, Is.InRange(-0.00001f, 1.00001f));
                    if (i > 0) Assert.That(value, Is.GreaterThanOrEqualTo(curve.Evaluate((i - 1) / 1000f) - 0.00001f));
                }
            }
        }

        [TestCase(UIAnimationStyle.Pop, "")]
        [TestCase(UIAnimationStyle.Fade, "")]
        [TestCase(UIAnimationStyle.BouncePop, "")]
        [TestCase(UIAnimationStyle.ExpandHorizontal, "")]
        [TestCase(UIAnimationStyle.ExpandVertical, "")]
        [TestCase(UIAnimationStyle.PersonaDrift, "Wipe")]
        [TestCase(UIAnimationStyle.PersonaSlash, "Skew")]
        [TestCase(UIAnimationStyle.StarRailSlide, "Sheen")]
        [TestCase(UIAnimationStyle.StarRailWipe, "Wipe,Sheen")]
        [TestCase(UIAnimationStyle.ZenlessTv, "Scan,Flash")]
        [TestCase(UIAnimationStyle.ZenlessSnap, "Split")]
        [TestCase(UIAnimationStyle.PersonaCutIn, "Wipe,Skew,Split,Flash")]
        [TestCase(UIAnimationStyle.PersonaList, "")]
        [TestCase(UIAnimationStyle.StarRailHologram, "Split,Scan")]
        [TestCase(UIAnimationStyle.StarRailWindow, "")]
        [TestCase(UIAnimationStyle.ZenlessGlitch, "Slice,Split")]
        [TestCase(UIAnimationStyle.ZenlessSticker, "Flash")]
        [TestCase(UIAnimationStyle.JrpgCommand, "")]
        [TestCase(UIAnimationStyle.JrpgScroll, "Wipe")]
        [TestCase(UIAnimationStyle.JrpgSummon, "Sheen,Flash")]
        [TestCase(UIAnimationStyle.JrpgVictory, "Sheen")]
        [TestCase(UIAnimationStyle.JrpgDialogue, "")]
        [TestCase(UIAnimationStyle.JrpgItem, "Sheen,Flash")]
        public void Signatures_UseOnlyTheirChosenFx(UIAnimationStyle style, string effects)
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(style);
            Assert.That(string.Join(",", FxTracks(preset.Get(UIAnimationState.Open)).Where(fx => fx.track.Use).Select(fx => fx.name)), Is.EqualTo(effects));
        }

        [Test]
        public void Signatures_NoTwoOpenTrackProfilesAreTheSame()
        {
            var profiles = new Dictionary<UIAnimationStyle, Dictionary<string, float[]>>();
            foreach (UIAnimationStyle style in Styles)
            {
                UIAnimationPreset preset = MakePreset();
                preset.ApplyStyle(style);
                AnimationClip clip = NewClip();
                UIAnimationClipBaker.Bake(preset, UIAnimationState.Open, clip);
                // 같은 스케일·알파 트랙도 진폭과 박자가 다르면 별개의 시그니처다. 길이만 다른 복제는 허용하지 않는다.
                var profile = AnimationUtility.GetCurveBindings(clip)
                    .Where(binding => binding.propertyName != "m_Enabled" && binding.propertyName != "_seed")
                    .Select(binding => new { binding.propertyName, Values = Enumerable.Range(0, 65)
                        .Select(i => AnimationUtility.GetEditorCurve(clip, binding).Evaluate(clip.length * i / 64f)).ToArray() })
                    .Where(track => track.Values.Max() - track.Values.Min() > 0.0001f)
                    .ToDictionary(track => track.propertyName, track => track.Values);
                foreach (var previous in profiles)
                {
                    bool same = previous.Value.Keys.OrderBy(key => key).SequenceEqual(profile.Keys.OrderBy(key => key))
                        && profile.All(track => track.Value.Zip(previous.Value[track.Key], (a, b) => Mathf.Abs(a - b) < 0.002f).All(equal => equal));
                    Assert.That(same, Is.False, previous.Key + " / " + style);
                }
                profiles.Add(style, profile);
            }
        }

        [Test]
        public void ApplyingEveryPair_LeavesExactlyTheNewMotionAndFxShape()
        {
            UIAnimationPreset expected = MakePreset(), actual = MakePreset();
            foreach (UIAnimationStyle target in Styles)
            {
                expected.ApplyStyle(target);
                float[] snapshot = Snapshot(expected);
                foreach (UIAnimationStyle previous in Styles)
                {
                    actual.ApplyStyle(previous);
                    actual.ApplyStyle(target);
                    CollectionAssert.AreEqual(snapshot, Snapshot(actual), previous + " → " + target);
                }
            }
        }

        [TestCaseSource(nameof(Styles))]
        public void StaySwap_ClearsPresetFxWithoutChangingOpenOrClose(UIAnimationStyle style)
        {
            UIAnimationPreset preset = MakePreset();
            foreach (UIStayStyle idle in Enum.GetValues(typeof(UIStayStyle)))
            {
                preset.ApplyStyle(style);
                string open = JsonUtility.ToJson(preset.Get(UIAnimationState.Open));
                string close = JsonUtility.ToJson(preset.Get(UIAnimationState.Close));
                preset.ApplyStayStyle(idle);
                Assert.That(FxTracks(preset.Get(UIAnimationState.Stay)).All(fx => !fx.track.Use), Is.True);
                Assert.That(JsonUtility.ToJson(preset.Get(UIAnimationState.Open)), Is.EqualTo(open));
                Assert.That(JsonUtility.ToJson(preset.Get(UIAnimationState.Close)), Is.EqualTo(close));
                Assert.That(preset.IsStayStatic, Is.EqualTo(idle == UIStayStyle.Still));
            }
        }

        [TestCaseSource(nameof(Styles))]
        public void Stay_KeepsTextStillAndSheenRare(UIAnimationStyle style)
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(style);
            UIAnimationStateSettings stay = preset.Get(UIAnimationState.Stay);
            Assert.That(stay.Scale.Use || stay.Rotation.Use || stay.Skew.Use || stay.Slice.Use || stay.Split.Use || stay.Flash.Use || stay.Color.Use, Is.False);
            Assert.That(stay.Offset.Use, Is.EqualTo(style == UIAnimationStyle.PersonaDrift));
            if (stay.Sheen.Use)
            {
                Assert.That(stay.Duration, Is.GreaterThanOrEqualTo(9f));
                AnimationClip clip = NewClip();
                UIAnimationClipBaker.Bake(preset, UIAnimationState.Stay, clip);
                AnimationCurve sheen = Curve(clip, typeof(UIAnimationFx), "_sheen");
                int visible = Enumerable.Range(0, 2001).Count(i =>
                {
                    float value = sheen.Evaluate(clip.length * i / 2000f);
                    return value > 0.0001f && value < 0.9999f;
                });
                Assert.That(visible / 2001f, Is.LessThan(0.08f));
                Assert.That(sheen.Evaluate(0f), Is.Zero);
                Assert.That(sheen.Evaluate(clip.length), Is.Zero);
            }
            if (stay.Scan.Use) Assert.That(stay.Scan.To, Is.LessThanOrEqualTo(0.06f));
        }

        [Test]
        public void BannerWipe_LightAndSoftBoundaryTravelTogether()
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(UIAnimationStyle.StarRailWipe);
            AnimationClip clip = NewClip();
            UIAnimationClipBaker.Bake(preset, UIAnimationState.Open, clip);
            AnimationCurve wipe = Curve(clip, typeof(UIAnimationFx), "_wipe"), sheen = Curve(clip, typeof(UIAnimationFx), "_sheen");
            Assert.That(preset.SheenAngle, Is.EqualTo(preset.WipeAngle));
            for (int i = 0; i < 1000; i++)
            {
                float t = clip.length * i / 1000f;
                float boundary = wipe.Evaluate(t) * (1f + preset.WipeSoftness) - preset.WipeSoftness * 0.5f;
                float light = Mathf.Lerp(-preset.SheenWidth, 1f + preset.SheenWidth, sheen.Evaluate(t));
                Assert.That(light, Is.EqualTo(boundary).Within(0.002f));
            }
        }

        [Test]
        public void BasicSignatures_HaveDifferentScaleRhythmsAndAxisOrder()
        {
            UIAnimationPreset preset = MakePreset();
            float ScaleX(float t)
            {
                UIAnimationSpan span = preset.GetSpan(UIAnimationState.Open);
                return preset.Get(UIAnimationState.Open).Scale.Evaluate(span.Scale.From, span.Scale.To, t).x / preset.BaseScale.x;
            }
            preset.ApplyStyle(UIAnimationStyle.Pop);
            Assert.That(ScaleX(0f), Is.EqualTo(0.96f).Within(0.0001f));
            Assert.That(ScaleX(0.08f), Is.LessThan(ScaleX(0f)));
            Assert.That(ScaleX(0.58f), Is.InRange(1.01f, 1.025f));
            Assert.That(ScaleX(0.98f), Is.EqualTo(1f).Within(0.0001f));

            preset.ApplyStyle(UIAnimationStyle.Fade);
            Assert.That(ScaleX(0f), Is.InRange(0.97f, 0.98f));
            Assert.That(preset.Get(UIAnimationState.Open).Color.Curve.Evaluate(0.4f), Is.LessThan(0.8f));

            preset.ApplyStyle(UIAnimationStyle.BouncePop);
            Assert.That(ScaleX(0.48f), Is.GreaterThan(1.04f));
            Assert.That(ScaleX(0.70f), Is.LessThan(1f));
            Assert.That(ScaleX(0.84f), Is.InRange(1.001f, 1.01f));
            for (int i = 90; i <= 100; i++) Assert.That(ScaleX(i / 100f), Is.InRange(0.999f, 1.01f));

            preset.ApplyStyle(UIAnimationStyle.ExpandHorizontal);
            UIVector2Track scale = preset.Get(UIAnimationState.Open).Scale;
            Assert.That(scale.Curve.Evaluate(0.2f), Is.GreaterThan(0.5f));
            Assert.That(scale.CurveY.Evaluate(0.2f), Is.Zero);
            preset.ApplyStyle(UIAnimationStyle.ExpandVertical);
            scale = preset.Get(UIAnimationState.Open).Scale;
            Assert.That(scale.Curve.Evaluate(0.5f), Is.EqualTo(1f));
            Assert.That(scale.CurveY.Evaluate(0.5f), Is.LessThan(1f));
        }

        [Test]
        public void Tv_ExpandsAcrossThenUpAndClosesToALineBeforeCollapsing()
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(UIAnimationStyle.ZenlessTv);
            foreach (UIAnimationState state in new[] { UIAnimationState.Open, UIAnimationState.Close })
            {
                AnimationClip clip = NewClip();
                UIAnimationClipBaker.Bake(preset, state, clip);
                float t = clip.length * (state == UIAnimationState.Open ? 0.30f : 0.65f);
                float x = Curve(clip, typeof(RectTransform), "m_LocalScale.x").Evaluate(t) / preset.BaseScale.x;
                float y = Curve(clip, typeof(RectTransform), "m_LocalScale.y").Evaluate(t) / preset.BaseScale.y;
                Assert.That(x, Is.GreaterThan(0.95f));
                Assert.That(y, Is.LessThan(state == UIAnimationState.Open ? 0.5f : 0.02f));
            }
        }

        [TestCase(UIAnimationStyle.BouncePop)]
        [TestCase(UIAnimationStyle.PersonaDrift)]
        [TestCase(UIAnimationStyle.PersonaSlash)]
        [TestCase(UIAnimationStyle.PersonaCutIn)]
        [TestCase(UIAnimationStyle.PersonaList)]
        [TestCase(UIAnimationStyle.ZenlessSnap)]
        [TestCase(UIAnimationStyle.ZenlessSticker)]
        [TestCase(UIAnimationStyle.JrpgVictory)]
        [TestCase(UIAnimationStyle.JrpgItem)]
        public void MovingEntrances_PositionArrivesBeforeScaleAndTiltSettle(UIAnimationStyle style)
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(style);
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
            float LastMoving(UIEaseCurve curve)
            {
                float last = 0f, rest = curve.Evaluate(1f);
                for (int i = 0; i <= 1000; i++)
                    if (Mathf.Abs(curve.Evaluate(i / 1000f) - rest) > 0.0001f) last = i / 1000f;
                return last;
            }
            float VectorEnd(UIVector2Track track) => Mathf.Max(
                track.From.x == track.To.x ? 0f : LastMoving(track.Curve),
                track.From.y == track.To.y ? 0f : LastMoving(track.CurveY));

            Assert.That(open.Offset.Use, Is.True);
            float arrived = VectorEnd(open.Offset);
            if (open.Scale.Use) Assert.That(VectorEnd(open.Scale), Is.GreaterThan(arrived + 0.02f));
            if (open.Rotation.Use) Assert.That(LastMoving(open.Rotation.Curve), Is.GreaterThan(arrived + 0.02f));
            if (open.Skew.Use) Assert.That(LastMoving(open.Skew.Curve), Is.GreaterThan(arrived + 0.02f));
        }

        [Test]
        public void Snap_LandsAt12FpsWithSplitAndCommandStaysAt10FpsWithoutFx()
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(UIAnimationStyle.ZenlessSnap);
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
            Assert.That(open.StepsPerSecond, Is.EqualTo(12));
            AnimationClip clip = NewClip();
            UIAnimationClipBaker.Bake(preset, UIAnimationState.Open, clip);
            AnimationCurve scale = Curve(clip, typeof(RectTransform), "m_LocalScale.y");
            foreach (Keyframe key in scale.keys)
                Assert.That(key.time * 12f, Is.EqualTo(Mathf.Round(key.time * 12f)).Within(0.0001f));
            Assert.That(scale.Evaluate(0.10f), Is.EqualTo(scale.Evaluate(0.16f)));
            AnimationCurve split = Curve(clip, typeof(UIAnimationFx), "_split");
            Assert.That(split.Evaluate(0.16f), Is.Zero);
            Assert.That(split.Evaluate(2f / 12f), Is.GreaterThan(0.1f));
            Assert.That(split.Evaluate(clip.length), Is.Zero);
            preset.ApplyStyle(UIAnimationStyle.JrpgCommand);
            Assert.That(preset.Get(UIAnimationState.Open).StepsPerSecond, Is.EqualTo(10));
            Assert.That(preset.Get(UIAnimationState.Close).StepsPerSecond, Is.EqualTo(10));
            Assert.That(preset.UsesFx, Is.False);
        }

        [TestCase(UIAnimationStyle.ZenlessSnap)]
        [TestCase(UIAnimationStyle.ZenlessGlitch)]
        [TestCase(UIAnimationStyle.JrpgCommand)]
        public void SteppedPresets_AllTracksKeepTheirFrameCadenceAfterAnotherStyle(UIAnimationStyle style)
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(UIAnimationStyle.StarRailHologram);
            preset.ApplyStyle(style);
            foreach (UIAnimationState state in new[] { UIAnimationState.Open, UIAnimationState.Close })
            {
                UIAnimationStateSettings settings = preset.Get(state);
                if (settings.StepsPerSecond == 0) continue;
                AnimationClip clip = NewClip();
                UIAnimationClipBaker.Bake(preset, state, clip);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    foreach (Keyframe key in curve.keys)
                        Assert.That(key.time * settings.StepsPerSecond,
                            Is.EqualTo(Mathf.Round(key.time * settings.StepsPerSecond)).Within(0.0001f),
                            style + ":" + state + ":" + binding.propertyName);
                }
            }
        }

        [TestCase(UIAnimationStyle.JrpgSummon, 0.2f)]
        [TestCase(UIAnimationStyle.JrpgItem, 0.8f)]
        public void RewardFlash_PeaksAfterTheCardBecomesVisible(UIAnimationStyle style, float minimumWidth)
        {
            UIAnimationPreset preset = MakePreset();
            preset.ApplyStyle(style);
            AnimationClip clip = NewClip();
            UIAnimationClipBaker.Bake(preset, UIAnimationState.Open, clip);
            float peakTime = clip.length * 0.14f;
            Assert.That(Curve(clip, typeof(UIAnimationFx), "_flash").Evaluate(peakTime), Is.GreaterThan(0.5f));
            Assert.That(Curve(clip, typeof(UnityEngine.UI.Image), "m_Color.a").Evaluate(peakTime) / preset.BaseColor.a,
                Is.GreaterThan(0.6f));
            Assert.That(Curve(clip, typeof(RectTransform), "m_LocalScale.x").Evaluate(peakTime) / preset.BaseScale.x,
                Is.GreaterThan(minimumWidth));
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

        private static IEnumerable<(string name, UIFloatTrack track)> FxTracks(UIAnimationStateSettings state)
        {
            yield return ("Wipe", state.Wipe);
            yield return ("Skew", state.Skew);
            yield return ("Slice", state.Slice);
            yield return ("Split", state.Split);
            yield return ("Scan", state.Scan);
            yield return ("Dissolve", state.Dissolve);
            yield return ("Sheen", state.Sheen);
            yield return ("Flash", state.Flash);
        }

        private static float[] Snapshot(UIAnimationPreset preset)
        {
            var values = new List<float>
            {
                (float)preset.WipeDirection, preset.WipeAngle, preset.WipeSoftness, preset.GlitchBands, preset.GlitchFps,
                preset.DissolveCellSize, preset.SheenAngle, preset.SheenWidth, preset.SheenIntensity
            };
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState)))
            {
                UIAnimationStateSettings settings = preset.Get(state);
                UIAnimationSpan span = preset.GetSpan(state);
                values.AddRange(new[] { settings.Time, settings.Speed, settings.StepsPerSecond,
                    settings.Offset.Use ? 1f : 0f, settings.Scale.Use ? 1f : 0f, settings.Rotation.Use ? 1f : 0f, settings.Color.Use ? 1f : 0f });
                var ranges = new[] { span.Wipe, span.Skew, span.Slice, span.Split, span.Scan, span.Dissolve, span.Sheen, span.Flash };
                var tracks = FxTracks(settings).Select(fx => fx.track).ToArray();
                values.AddRange(tracks.Select(track => track.Use ? 1f : 0f));
                for (int i = 0; i <= 64; i++)
                {
                    float u = i / 64f;
                    float t = state == UIAnimationState.Stay ? 1f - Mathf.Abs(2f * u - 1f) : u;
                    Vector2 offset = settings.Offset.Evaluate(span.Offset.From, span.Offset.To, t);
                    Vector2 scale = settings.Scale.Evaluate(span.Scale.From, span.Scale.To, t);
                    Color color = Color.LerpUnclamped(span.Color.From, span.Color.To, settings.Color.Curve.Evaluate(t));
                    values.AddRange(new[] { offset.x, offset.y, scale.x, scale.y,
                        Mathf.LerpUnclamped(span.Rotation.From, span.Rotation.To, settings.Rotation.Curve.Evaluate(t)), color.r, color.g, color.b, color.a });
                    for (int fx = 0; fx < tracks.Length; fx++)
                        values.Add(Mathf.LerpUnclamped(ranges[fx].From, ranges[fx].To, tracks[fx].Curve.Evaluate(t)));
                }
            }
            return values.ToArray();
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
                { "_wipe", stay ? span.Wipe.From : span.Wipe.To },
                { "_skew", stay ? span.Skew.From : span.Skew.To },
                { "_slice", stay ? span.Slice.From : span.Slice.To },
                { "_split", stay ? span.Split.From : span.Split.To },
                { "_scan", stay ? span.Scan.From : span.Scan.To },
                { "_dissolve", stay ? span.Dissolve.From : span.Dissolve.To },
                { "_sheen", stay ? span.Sheen.From : span.Sheen.To },
                { "_flash", stay ? span.Flash.From : span.Flash.To },
                { "_seed", preset.Get(state).Slice.Use ? Mathf.Floor(preset.Get(state).Duration * preset.GlitchFps) : 0f },
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
