using System;
using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationHideShowEditorTests
    {
        private const string Folder = "Assets/__UIAnimationHideShowEditorTests";
        private readonly List<GameObject> _objects = new List<GameObject>();

        private UnityEngine.UI.Image Image(string name = "Panel")
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            _objects.Add(target);
            return target.GetComponent<UnityEngine.UI.Image>();
        }

        private UIAnimationPreset Create(bool enabled = false)
        {
            return UIAnimationGenerator.Create(Image(), Folder, "Panel", UIPointerEffectMask.None,
                preset => preset.SetHideShowEnabled(enabled));
        }

        private static AnimatorState State(UIAnimationPreset preset, UIAnimationState state)
        {
            return UIAnimationGenerator.ControllerOf(preset).layers[0].stateMachine.states
                .Select(child => child.state).Single(value => value.name == state.ToString());
        }

        private static void AssertEdge(UIAnimationPreset preset, UIAnimationState from, UIAnimationState to,
            bool finished, params string[] conditions)
        {
            AnimatorStateTransition transition = State(preset, from).transitions.Single(t => t.destinationState == State(preset, to));
            Assert.That(transition.hasExitTime, Is.EqualTo(finished));
            Assert.That(transition.hasFixedDuration, Is.True);
            Assert.That(transition.conditions.Select(c => c.parameter + ":" + c.mode), Is.EquivalentTo(conditions));
            Assert.That(transition.interruptionSource, Is.EqualTo(TransitionInterruptionSource.Destination));
            Assert.That(transition.orderedInterruption, Is.False);
            if (finished)
            {
                Assert.That(transition.exitTime, Is.EqualTo(1f));
                Assert.That(transition.duration, Is.EqualTo(from == UIAnimationState.Show ? 0.1f : 0f));
            }
            else if (to != UIAnimationState.Close || from != UIAnimationState.Stay)
                Assert.That(transition.duration, Is.EqualTo(0.1f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject target in _objects) if (target != null) Object.DestroyImmediate(target);
            _objects.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void LegacyPreset_DefaultsOff_AndKeepsThreeStateController()
        {
            UIAnimationPreset preset = Create();
            JsonUtility.FromJsonOverwrite("{\"_statePresetName\":\"이전 프리셋\"}", preset);
            Assert.That(preset.HideShowEnabled, Is.False);
            Assert.That((int)UIAnimationState.Close, Is.EqualTo(2));
            Assert.That(UIAnimationGenerator.ControllerOf(preset).layers[0].stateMachine.states.Length, Is.EqualTo(3));
            Assert.That(UIAnimationGenerator.ControllerOf(preset).parameters.Select(p => p.name), Is.EqualTo(new[] { "IsOpen" }));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Panel_Hide.anim"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Panel_Show.anim"), Is.Null);
        }

        [Test]
        public void EnabledGraph_HasAllStatesParametersPrioritiesAndInterruptions()
        {
            UIAnimationPreset preset = Create(true);
            AnimatorController controller = UIAnimationGenerator.ControllerOf(preset);
            Assert.That(controller.layers[0].stateMachine.states.Select(s => s.state.name),
                Is.EquivalentTo(new[] { "Open", "Stay", "Close", "Hide", "Show" }));
            Assert.That(controller.parameters.Select(p => p.name), Is.EquivalentTo(new[] { "IsOpen", "IsHidden" }));
            Assert.That(controller.parameters.Single(p => p.name == "IsHidden").defaultBool, Is.False);
            Assert.That(controller.parameters.All(p => p.type == AnimatorControllerParameterType.Bool), Is.True);
            foreach (UIAnimationState from in new[] { UIAnimationState.Open, UIAnimationState.Stay, UIAnimationState.Hide, UIAnimationState.Show })
            {
                AssertEdge(preset, from, UIAnimationState.Close, false, "IsOpen:IfNot");
                Assert.That(State(preset, from).transitions[0].destinationState.name, Is.EqualTo("Close"));
            }
            foreach (UIAnimationState from in new[] { UIAnimationState.Open, UIAnimationState.Stay, UIAnimationState.Show })
                AssertEdge(preset, from, UIAnimationState.Hide, false, "IsOpen:If", "IsHidden:If");
            AssertEdge(preset, UIAnimationState.Hide, UIAnimationState.Show, false, "IsOpen:If", "IsHidden:IfNot");
            AssertEdge(preset, UIAnimationState.Show, UIAnimationState.Stay, true, "IsOpen:If", "IsHidden:IfNot");
            AssertEdge(preset, UIAnimationState.Open, UIAnimationState.Stay, true, "IsOpen:If", "IsHidden:IfNot");
            Assert.That(State(preset, UIAnimationState.Hide).transitions.Any(t => t.hasExitTime), Is.False);
            Assert.That(controller.layers[0].stateMachine.states.All(s => !s.state.writeDefaultValues), Is.True);
        }

        [Test]
        public void Toggle_DeletesOnlyOptionalClips_AndPreservesExistingAssetGuids()
        {
            UIAnimationPreset preset = Create();
            string[] original = UIAnimationGenerator.AssetPaths(Folder, "Panel").ToArray();
            string[] guids = original.Select(AssetDatabase.AssetPathToGUID).ToArray();
            AssetDatabase.CreateAsset(new AnimationClip(), Folder + "/Unrelated.anim");
            preset.SetHideShowEnabled(true);
            UIAnimationGenerator.Rebuild(preset);
            foreach (UIAnimationState state in new[] { UIAnimationState.Hide, UIAnimationState.Show })
            {
                AnimationClip clip = UIAnimationGenerator.ClipOf(preset, state);
                Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo($"{Folder}/Panel_{state}.anim"));
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.False);
                AnimationCurve enabled = AnimationUtility.GetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(UnityEngine.UI.Image), "m_Enabled"));
                Assert.That(enabled.Evaluate(clip.length), Is.EqualTo(1f));
            }
            preset.SetHideShowEnabled(false);
            UIAnimationGenerator.Rebuild(preset);
            UIAnimationGenerator.Save(preset);
            Assert.That(original.Select(AssetDatabase.AssetPathToGUID), Is.EqualTo(guids));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Unrelated.anim"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Panel_Hide.anim"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Panel_Show.anim"), Is.Null);
            Assert.That(UIAnimationGenerator.ControllerOf(preset).layers[0].stateMachine.states.Length, Is.EqualTo(3));
            Assert.That(UIAnimationGenerator.ControllerOf(preset).parameters.Length, Is.EqualTo(1));
            preset.SetHideShowEnabled(true);
            UIAnimationGenerator.Rebuild(preset);
            Assert.That(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Hide), Is.Not.Null);
        }

        [Test]
        public void EveryStyle_GeneratesIndependentCloseBasedHideAndShortOpenBasedShow()
        {
            UIAnimationPreset preset = Create();
            foreach (UIAnimationStyle style in Enum.GetValues(typeof(UIAnimationStyle)))
            {
                preset.ApplyStyle(style);
                preset.SetHideShowEnabled(true);
                UIAnimationStateSettings hide = preset.Get(UIAnimationState.Hide), show = preset.Get(UIAnimationState.Show);
                UIAnimationStateSettings close = JsonUtility.FromJson<UIAnimationStateSettings>(JsonUtility.ToJson(preset.Get(UIAnimationState.Close)));
                close.KeepEnabled = true;
                close.AutoClose = false;
                Assert.That(JsonUtility.ToJson(hide), Is.EqualTo(JsonUtility.ToJson(close)), style.ToString());
                UIAnimationStateSettings open = JsonUtility.FromJson<UIAnimationStateSettings>(JsonUtility.ToJson(preset.Get(UIAnimationState.Open)));
                open.Time *= 0.7f;
                open.KeepEnabled = true;
                open.AutoClose = false;
                Assert.That(JsonUtility.ToJson(show), Is.EqualTo(JsonUtility.ToJson(open)), style.ToString());
                Assert.That(show.Offset, Is.Not.SameAs(preset.Get(UIAnimationState.Open).Offset));
                Assert.That(hide.Slice.Curve, Is.Not.SameAs(preset.Get(UIAnimationState.Close).Slice.Curve));
                preset.SetHideShowEnabled(false);
            }
        }

        [Test]
        public void StyleAndMirror_RefreshEnabledStates_AndManualEditsKeepTheirLabel()
        {
            UIAnimationPreset preset = Create(true);
            preset.Get(UIAnimationState.Hide).Time = 9f;
            preset.MarkStatePresetEdited();
            Assert.That(preset.StatePresetLabel, Does.Contain("세부 조정함"));
            UIAnimationGenerator.Rebuild(preset);
            Assert.That(preset.Get(UIAnimationState.Hide).Time, Is.EqualTo(9f));
            preset.ApplyStyle(UIAnimationStyle.ZenlessGlitch);
            Assert.That(preset.StatePresetEdited, Is.False);
            Assert.That(preset.Get(UIAnimationState.Hide).Slice.To, Is.EqualTo(preset.Get(UIAnimationState.Close).Slice.To));
            Assert.That(preset.Get(UIAnimationState.Show).Time, Is.EqualTo(preset.Get(UIAnimationState.Open).Time * 0.7f));
            preset.MirrorOpenToClose();
            Assert.That(preset.Get(UIAnimationState.Hide).Time, Is.EqualTo(preset.Get(UIAnimationState.Close).Time));
            Assert.That(preset.Get(UIAnimationState.Show).Slice.From, Is.EqualTo(preset.Get(UIAnimationState.Open).Slice.From));
            preset.SetHideShowEnabled(false);
            preset.Get(UIAnimationState.Hide).Time = 9f;
            preset.ApplyStyle(UIAnimationStyle.Fade);
            Assert.That(preset.Get(UIAnimationState.Hide).Time, Is.EqualTo(9f));
        }

        [Test]
        public void OptionalOnlyFx_AreBakedEverywhere_AndComponentsSyncAcrossUsers()
        {
            UIAnimationPreset preset = Create(true);
            UnityEngine.UI.Image other = Image("Other");
            UIAnimationGenerator.Connect(other, preset);
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState))) preset.Get(state).ClearMotion();
            UIAnimationStateSettings hide = preset.Get(UIAnimationState.Hide);
            foreach (UIFloatTrack track in new[] { hide.Wipe, hide.Skew, hide.Slice, hide.Split, hide.Scan, hide.Dissolve, hide.Sheen, hide.Flash })
                track.Set(0f, 0.5f, UIEase.Linear);
            UIAnimationGenerator.Rebuild(preset);
            UIAnimationGenerator.SyncComponents(preset);
            foreach (UIAnimationPlayer user in UIAnimationGenerator.Users(preset))
            {
                Assert.That(user.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(user.GetComponent<UIAnimationFx>(), Is.Not.Null);
            }
            var expected = AnimationUtility.GetCurveBindings(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Hide));
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState)))
                Assert.That(AnimationUtility.GetCurveBindings(UIAnimationGenerator.ClipOf(preset, state)), Is.EquivalentTo(expected));
            preset.SetHideShowEnabled(false);
            UIAnimationGenerator.Rebuild(preset);
            UIAnimationGenerator.SyncComponents(preset);
            Assert.That(preset.UsesFx, Is.False);
            Assert.That(other.GetComponent<UIAnimationFx>(), Is.Null);
        }

        [Test]
        public void Repair_UpgradesOldGraphAndRepairsDamage_WithoutReplacingDesignerSettings()
        {
            UIAnimationPreset preset = Create();
            UnityEngine.UI.Image first = _objects[0].GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Image other = Image("Other");
            UIAnimationGenerator.Connect(other, preset);
            preset.SetHideShowEnabled(true);
            preset.Get(UIAnimationState.Hide).Time = 0.83f;
            string values = JsonUtility.ToJson(preset);
            UIAnimationGenerator.Repair(first, preset);
            Assert.That(State(preset, UIAnimationState.Hide).motion, Is.Not.Null);
            Assert.That(other.GetComponent<CanvasGroup>(), Is.Not.Null);
            AnimatorController controller = UIAnimationGenerator.ControllerOf(preset);
            controller.layers[0].stateMachine.RemoveState(State(preset, UIAnimationState.Show));
            State(preset, UIAnimationState.Hide).transitions = Array.Empty<AnimatorStateTransition>();
            controller.RemoveParameter(Array.FindIndex(controller.parameters, p => p.name == "IsHidden"));
            UIAnimationGenerator.Repair(first, preset);
            Assert.That(JsonUtility.ToJson(preset), Is.EqualTo(values));
            AssertEdge(preset, UIAnimationState.Hide, UIAnimationState.Show, false, "IsOpen:If", "IsHidden:IfNot");
            Assert.That(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Hide).length, Is.EqualTo(0.83f).Within(0.001f));
            preset.SetHideShowEnabled(false);
            UIAnimationGenerator.Repair(first, preset);
            Assert.That(controller.parameters.Select(p => p.name), Is.EqualTo(new[] { "IsOpen" }));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Panel_Show.anim"), Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Repair_UpgradesInterruptionPolicy_WithoutChangingSettingsOrClips(bool hideShow)
        {
            UIAnimationPreset preset = Create(hideShow);
            AnimatorController controller = UIAnimationGenerator.ControllerOf(preset);
            string values = JsonUtility.ToJson(preset);
            AnimationClip[] clips = controller.layers[0].stateMachine.states.Select(child => (AnimationClip)child.state.motion).ToArray();
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                foreach (AnimatorStateTransition transition in child.state.transitions)
                    transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;

            UIAnimationGenerator.Repair(_objects[0].GetComponent<UnityEngine.UI.Image>(), preset);

            Assert.That(JsonUtility.ToJson(preset), Is.EqualTo(values));
            Assert.That(controller.layers[0].stateMachine.states.Select(child => child.state.motion), Is.EqualTo(clips));
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                foreach (AnimatorStateTransition transition in child.state.transitions)
                    Assert.That(transition.interruptionSource, Is.EqualTo(TransitionInterruptionSource.Destination));
        }

        [Test]
        public void SavedPresetAndSeparate_CopyOptionalSettingsAndRescaleTracks()
        {
            UIAnimationPreset preset = Create(true);
            preset.Get(UIAnimationState.Hide).Scale.Set(Vector2.one, Vector2.one * 0.2f, UIEase.Linear);
            preset.Get(UIAnimationState.Hide).Time = 0.71f;
            preset.MarkStatePresetEdited();
            UIAnimationGenerator.SaveAsPreset(preset, Folder + "/Saved.asset");
            UIAnimationPreset saved = AssetDatabase.LoadAssetAtPath<UIAnimationPreset>(Folder + "/Saved.asset");
            UnityEngine.UI.Image other = Image("Other");
            other.rectTransform.localScale = new Vector3(2f, 3f, 1f);
            UIAnimationPreset loaded = UIAnimationGenerator.Create(other, Folder, "Other", UIPointerEffectMask.None,
                copy => copy.CopyMotionFrom(saved));
            Assert.That(loaded.HideShowEnabled, Is.True);
            Assert.That(loaded.Get(UIAnimationState.Hide).Scale.To, Is.EqualTo(new Vector2(0.4f, 0.6f)));
            Assert.That(UIAnimationGenerator.ClipOf(loaded, UIAnimationState.Show), Is.Not.Null);
            UnityEngine.UI.Image separateImage = Image("Separate");
            UIAnimationGenerator.Connect(separateImage, preset);
            UIAnimationPreset separate = UIAnimationGenerator.Separate(separateImage, preset, Folder);
            Assert.That(separate.HideShowEnabled, Is.True);
            Assert.That(separate.StatePresetEdited, Is.True);
            Assert.That(separate.Get(UIAnimationState.Hide).Time, Is.EqualTo(0.71f));
            Assert.That(UIAnimationGenerator.ClipOf(separate, UIAnimationState.Hide), Is.Not.SameAs(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Hide)));
            separate.Get(UIAnimationState.Hide).Time = 1f;
            Assert.That(preset.Get(UIAnimationState.Hide).Time, Is.EqualTo(0.71f));
            preset.SetHideShowEnabled(false);
            loaded.CopyMotionFrom(preset);
            UIAnimationGenerator.Rebuild(loaded);
            Assert.That(loaded.HideShowEnabled, Is.False);
            Assert.That(UIAnimationGenerator.ClipOf(loaded, UIAnimationState.Hide), Is.Null);
        }

        [Test]
        public void FlowPreview_BlendsFromHiddenEnd_AndReturnsToStayPose()
        {
            UIAnimationPreset preset = Create(true);
            preset.ApplyStayStyle(UIStayStyle.Still);
            preset.Get(UIAnimationState.Hide).Offset.Set(Vector2.zero, new Vector2(60f, 20f), UIEase.Linear);
            preset.Get(UIAnimationState.Show).Offset.Set(new Vector2(-80f, -10f), Vector2.zero, UIEase.EaseOutCubic);
            var hidden = new AnimationClip();
            var flow = new AnimationClip();
            try
            {
                RectTransform rect = _objects[0].GetComponent<RectTransform>();
                UIAnimationClipBaker.BakePreview(preset, UIAnimationState.Hide, rect, hidden);
                UIAnimationClipBaker.BakeShowFlowPreview(preset, rect, flow, 0.1f);
                var offset = EditorCurveBinding.FloatCurve(string.Empty, typeof(RectTransform), "m_AnchoredPosition.x");
                AnimationCurve curve = AnimationUtility.GetEditorCurve(flow, offset);
                Assert.That(curve.Evaluate(0f), Is.EqualTo(AnimationUtility.GetEditorCurve(hidden, offset).Evaluate(hidden.length)).Within(0.001f));
                Assert.That(curve.Evaluate(flow.length), Is.EqualTo(0f).Within(0.001f));
                Assert.That(flow.length / preset.Get(UIAnimationState.Show).Speed,
                    Is.EqualTo(preset.Get(UIAnimationState.Show).Duration + 0.1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(hidden);
                Object.DestroyImmediate(flow);
            }
        }

        [Test]
        public void HideAndShowButtons_UseTheRequestedPersistentMethods()
        {
            UIAnimationPreset preset = Create(true);
            UIAnimationPlayer player = UIAnimationGenerator.Users(preset).Single();
            var button = Image("Button").gameObject.AddComponent<UnityEngine.UI.Button>();
            foreach (string method in new[] { "Open", "Close", "Hide", "Show" })
            {
                UIAnimationGenerator.ConnectButton(button, player, method);
                Assert.That(UIAnimationGenerator.ButtonsCalling(player, method), Does.Contain(button));
                Assert.That(button.onClick.GetPersistentMethodName(button.onClick.GetPersistentEventCount() - 1), Is.EqualTo(method));
            }
            UIAnimationGenerator.DisconnectButton(button, player, "Hide");
            Assert.That(UIAnimationGenerator.ButtonsCalling(player, "Hide"), Is.Empty);
            Assert.That(UIAnimationGenerator.ButtonsCalling(player, "Show"), Does.Contain(button));
        }
    }
}
