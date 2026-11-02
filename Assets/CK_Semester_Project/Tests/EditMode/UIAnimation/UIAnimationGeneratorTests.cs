using System.Collections.Generic;
using System.IO;
using System.Linq;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationGeneratorTests
    {
        private const string Root = "Assets/__UIAnimationTests";
        private const string Folder = Root + "/UI/Animation";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }
            _created.Clear();
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void Create_WritesControllerAndThreeClips_WithSpecNames()
        {
            UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);

            Assert.That(File.Exists($"{Folder}/OptionPanel.controller"));
            Assert.That(File.Exists($"{Folder}/OptionPanel_Open.anim"));
            Assert.That(File.Exists($"{Folder}/OptionPanel_Stay.anim"));
            Assert.That(File.Exists($"{Folder}/OptionPanel_Close.anim"));
            Assert.That(Directory.GetFiles(Folder).Where(path => !path.EndsWith(".meta")).Count(), Is.EqualTo(4));
        }

        [Test]
        public void SaveAll_SavesAnimationFilesAndScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                var go = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                UIAnimationPreset preset = UIAnimationGenerator.Create(go.GetComponent<Image>(), Folder, "OptionPanel", UIPointerEffectMask.None);
                Assert.That(EditorSceneManager.SaveScene(scene, $"{Folder}/SaveAll.unity"));
                preset.Get(UIAnimationState.Open).Time = 0.5f;
                UIAnimationGenerator.Rebuild(preset);
                EditorSceneManager.MarkSceneDirty(scene);

                Assert.That(UIAnimationGenerator.SaveAll(preset, scene));

                Assert.That(scene.isDirty, Is.False);
                Assert.That(EditorUtility.IsDirty(UIAnimationGenerator.ControllerOf(preset)), Is.False);
                Assert.That(EditorUtility.IsDirty(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open)), Is.False);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void SaveAll_WithoutScene_SavesFilesOnly()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            preset.Get(UIAnimationState.Open).Time = 0.5f;
            UIAnimationGenerator.Rebuild(preset);

            Assert.That(UIAnimationGenerator.SaveAll(preset, default));

            Assert.That(EditorUtility.IsDirty(UIAnimationGenerator.ControllerOf(preset)), Is.False);
        }

        [Test]
        public void Create_ConnectsAnimatorAndPlayer()
        {
            Image image = MakeImage("Image");

            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "OptionPanel", UIPointerEffectMask.None);

            Animator animator = image.GetComponent<Animator>();
            Assert.That(animator.runtimeAnimatorController, Is.EqualTo(UIAnimationGenerator.ControllerOf(preset)));
            Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.UnscaledTime));
            Assert.That(image.GetComponent<UIAnimationPlayer>().Preset, Is.EqualTo(preset));
        }

        [Test]
        public void Create_BuildsOpenStayCloseGraph()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);

            AnimatorController controller = UIAnimationGenerator.ControllerOf(preset);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            Assert.That(machine.defaultState.name, Is.EqualTo("Open"));
            Assert.That(machine.states.Select(child => child.state.name), Is.EquivalentTo(new[] { "Open", "Stay", "Close" }));
            Assert.That(machine.states.All(child => !child.state.writeDefaultValues));

            AnimatorControllerParameter parameter = controller.parameters.Single();
            Assert.That(parameter.name, Is.EqualTo(UIAnimationPreset.IsOpenParameter));
            Assert.That(parameter.defaultBool, Is.True);

            AnimatorState open = State(controller, "Open");
            AnimatorStateTransition finished = open.transitions.Single(t => t.destinationState.name == "Stay");
            Assert.That(finished.hasExitTime, Is.True);
            Assert.That(finished.exitTime, Is.EqualTo(1f));
            Assert.That(finished.duration, Is.EqualTo(0f));
            Assert.That(open.transitions.Any(t => t.destinationState.name == "Close"), Is.True);
            Assert.That(State(controller, "Stay").transitions.Single().destinationState.name, Is.EqualTo("Close"));
            Assert.That(State(controller, "Close").transitions.Single().destinationState.name, Is.EqualTo("Open"));
        }

        [Test]
        public void Bake_FollowsTimeSpeedAndEase()
        {
            Image image = MakeImage("Image");
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "OptionPanel", UIPointerEffectMask.None);
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
            open.Time = 0.8f;
            open.Speed = 2f;
            open.Scale.Set(Vector2.one * 0.5f, Vector2.one, UIEase.EaseOutQuad);

            UIAnimationGenerator.Rebuild(preset);

            AnimationClip clip = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            Assert.That(clip.length, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(State(UIAnimationGenerator.ControllerOf(preset), "Open").speed, Is.EqualTo(2f));
            AnimationCurve scaleX = AnimationUtility.GetEditorCurve(clip, Scale("x"));
            Assert.That(scaleX.Evaluate(0.4f), Is.EqualTo(0.875f).Within(0.002f));
            Assert.That(scaleX.Evaluate(0.8f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Bake_ChainsCloseFromOpenEnd_AndTurnsOffAtEnd()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            preset.Get(UIAnimationState.Open).Scale.Set(Vector2.zero, Vector2.one * 1.5f, UIEase.Linear);
            preset.Get(UIAnimationState.Close).Scale.Set(Vector2.zero, Vector2.one * 0.5f, UIEase.Linear);

            UIAnimationGenerator.Rebuild(preset);

            AnimationClip close = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Close);
            AnimationCurve scaleX = AnimationUtility.GetEditorCurve(close, Scale("x"));
            Assert.That(scaleX.Evaluate(0f), Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(scaleX.Evaluate(close.length), Is.EqualTo(0.5f).Within(0.001f));

            AnimationCurve enabled = AnimationUtility.GetEditorCurve(close, Enabled());
            Assert.That(enabled.Evaluate(close.length * 0.99f), Is.EqualTo(1f));
            Assert.That(enabled.Evaluate(close.length), Is.EqualTo(0f));
        }

        [Test]
        public void Bake_LeavesDisabledImageDisabled()
        {
            Image image = MakeImage("Frame");
            image.enabled = false;

            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "Frame", UIPointerEffectMask.None);

            AnimationClip open = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            AnimationCurve enabled = AnimationUtility.GetEditorCurve(open, Enabled());
            Assert.That(enabled.Evaluate(0f), Is.EqualTo(0f));
            Assert.That(enabled.Evaluate(open.length), Is.EqualTo(0f));
        }

        [Test]
        public void Bake_StayLoopsAndReturnsToStart()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            preset.Get(UIAnimationState.Stay).Scale.Set(Vector2.one, Vector2.one * 1.1f, UIEase.Linear);

            UIAnimationGenerator.Rebuild(preset);

            AnimationClip stay = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Stay);
            AnimationCurve scaleX = AnimationUtility.GetEditorCurve(stay, Scale("x"));
            Assert.That(AnimationUtility.GetAnimationClipSettings(stay).loopTime, Is.True);
            Assert.That(scaleX.Evaluate(0f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(scaleX.Evaluate(stay.length * 0.5f), Is.EqualTo(1.1f).Within(0.002f));
            Assert.That(scaleX.Evaluate(stay.length), Is.EqualTo(1f).Within(0.001f));
            Assert.That(preset.IsStayStatic, Is.False);
        }

        [Test]
        public void Bake_ClipsShareTheSameProperties()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);

            EditorCurveBinding[] open = Bindings(preset, UIAnimationState.Open);

            Assert.That(open, Is.EquivalentTo(Bindings(preset, UIAnimationState.Stay)));
            Assert.That(open, Is.EquivalentTo(Bindings(preset, UIAnimationState.Close)));
        }

        [Test]
        public void Bake_LeavesUnusedPropertiesOutOfClips()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            foreach (UIAnimationState state in new[] { UIAnimationState.Open, UIAnimationState.Stay, UIAnimationState.Close })
            {
                preset.Get(state).Scale.Off();
                preset.Get(state).Color.Off();
            }
            preset.Get(UIAnimationState.Open).Time = 0.5f;

            UIAnimationGenerator.Rebuild(preset);

            AnimationClip clip = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            Assert.That(Bindings(preset, UIAnimationState.Open).Single().propertyName, Is.EqualTo("m_Enabled"));
            Assert.That(clip.length, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Bake_ClampsColorForOvershootingEase()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            preset.Get(UIAnimationState.Open).Color.Set(new Color(1f, 1f, 1f, 0f), Color.white, UIEase.EaseOutBack);

            UIAnimationGenerator.Rebuild(preset);

            AnimationClip clip = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            AnimationCurve alpha = AnimationUtility.GetEditorCurve(
                clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_Color.a"));
            for (int step = 0; step <= 100; step++)
            {
                Assert.That(alpha.Evaluate(clip.length * step / 100f), Is.LessThanOrEqualTo(1.001f));
            }
        }

        [Test]
        public void Rebuild_KeepsFileIdentityAndClipReference()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            string guid = AssetDatabase.AssetPathToGUID($"{Folder}/OptionPanel_Open.anim");
            AnimationClip before = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);

            preset.Get(UIAnimationState.Open).Time = 1.25f;
            UIAnimationGenerator.Rebuild(preset);
            UIAnimationGenerator.Save(preset);

            Assert.That(AssetDatabase.AssetPathToGUID($"{Folder}/OptionPanel_Open.anim"), Is.EqualTo(guid));
            Assert.That(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open), Is.SameAs(before));
            Assert.That(before.length, Is.EqualTo(1.25f).Within(0.001f));
        }

        [Test]
        public void Rebuild_RestoresDeletedClip()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            AssetDatabase.DeleteAsset($"{Folder}/OptionPanel_Close.anim");

            UIAnimationGenerator.Rebuild(preset);

            Assert.That(File.Exists($"{Folder}/OptionPanel_Close.anim"));
            Assert.That(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Close), Is.Not.Null);
        }

        [Test]
        public void Preset_SurvivesReimport_AndStaysHiddenInsideController()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            preset.Get(UIAnimationState.Open).Time = 0.77f;
            UIAnimationGenerator.Rebuild(preset);
            UIAnimationGenerator.Save(preset);

            AssetDatabase.ImportAsset($"{Folder}/OptionPanel.controller", ImportAssetOptions.ForceUpdate);

            UIAnimationPreset loaded = UIAnimationGenerator.FindPreset(Folder, "OptionPanel");
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Get(UIAnimationState.Open).Time, Is.EqualTo(0.77f).Within(0.0001f));
            Assert.That(loaded.hideFlags & HideFlags.HideInHierarchy, Is.EqualTo(HideFlags.HideInHierarchy));
        }

        [Test]
        public void Create_UsesCanvasGroupWhenImageHasChildGraphics()
        {
            Image image = MakeImage("Panel");
            Image child = MakeImage("Child");
            child.transform.SetParent(image.transform);

            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "Panel", UIPointerEffectMask.None);

            Assert.That(preset.FadeChildren, Is.True);
            Assert.That(image.GetComponent<CanvasGroup>(), Is.Not.Null);
            EditorCurveBinding[] bindings = Bindings(preset, UIAnimationState.Open);
            Assert.That(bindings.Any(b => b.type == typeof(CanvasGroup) && b.propertyName == "m_Alpha"), Is.True);
            Assert.That(bindings.Any(b => b.propertyName == "m_Color.a"), Is.False);
        }

        [Test]
        public void Capture_KeepsImageColorAndScaleAsFinalLook()
        {
            Image image = MakeImage("Image");
            image.color = new Color(0.5f, 0.6f, 0.7f, 0.8f);
            image.rectTransform.localScale = new Vector3(2f, 3f, 1f);

            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "Image", UIPointerEffectMask.None);

            UIAnimationSpan open = preset.GetSpan(UIAnimationState.Open);
            Assert.That(open.Color.To, Is.EqualTo(image.color));
            Assert.That(open.Color.From.a, Is.EqualTo(0f));
            Assert.That(open.Scale.To, Is.EqualTo(new Vector2(2f, 3f)));
        }

        [Test]
        public void SuggestName_UsesObjectNameNotSprite()
        {
            Image image = MakeImage("SkillPanel");
            Assert.That(UIAnimationGenerator.SuggestName(image), Is.EqualTo("SkillPanel"));

            image.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            image.sprite.name = "Turn_Panel_Default";
            _created.Add(image.sprite);

            Assert.That(UIAnimationGenerator.SuggestName(image), Is.EqualTo("SkillPanel"));
        }

        [Test]
        public void FolderFor_UsesTargetName_AndGathersChildrenInTopAnimatedParent()
        {
            Image panel = MakeImage("SkillPanel");
            Image row = MakeImage("Row");
            Image button = MakeImage("SkillButton");
            row.transform.SetParent(panel.transform);
            button.transform.SetParent(row.transform);

            Assert.That(UIAnimationGenerator.FolderFor(panel, Root), Is.EqualTo(Root + "/SkillPanel"));
            Assert.That(UIAnimationGenerator.FolderFor(button, Root), Is.EqualTo(Root + "/SkillButton"));

            UIAnimationGenerator.Create(panel, UIAnimationGenerator.FolderFor(panel, Root), "SkillPanel", UIPointerEffectMask.None);
            string folder = UIAnimationGenerator.FolderFor(button, Root);
            UIAnimationPreset child = UIAnimationGenerator.Create(button, folder, "SkillButton", UIPointerEffectMask.None);

            Assert.That(folder, Is.EqualTo(Root + "/SkillPanel"));
            Assert.That(AssetDatabase.GetAssetPath(child), Is.EqualTo(Root + "/SkillPanel/SkillButton.controller"));
            Assert.That(File.Exists(Root + "/SkillPanel/SkillPanel_Open.anim"));
            Assert.That(File.Exists(Root + "/SkillPanel/SkillButton_Open.anim"));
        }

        [Test]
        public void CleanName_ReplacesCharactersFilesCannotHold()
        {
            Assert.That(UIAnimationGenerator.CleanName(" Panel:01? "), Is.EqualTo("Panel_01_"));
            Assert.That(UIAnimationGenerator.CleanName(null), Is.Empty);
        }

        [Test]
        public void UniqueName_SkipsNamesInUse()
        {
            UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);

            Assert.That(UIAnimationGenerator.IsNameFree(Folder, "OptionPanel"), Is.False);
            Assert.That(UIAnimationGenerator.UniqueName(Folder, "OptionPanel"), Is.EqualTo("OptionPanel_2"));
            Assert.That(UIAnimationGenerator.UniqueName(Folder, "Other"), Is.EqualTo("Other"));
        }

        [Test]
        public void Connect_LetsTwoImagesShareOnePreset()
        {
            Image first = MakeImage("First");
            Image second = MakeImage("Second");
            UIAnimationPreset preset = UIAnimationGenerator.Create(first, Folder, "Shared", UIPointerEffectMask.None);

            UIAnimationGenerator.Connect(second, preset);

            Assert.That(second.GetComponent<UIAnimationPlayer>().Preset, Is.EqualTo(preset));
            Assert.That(second.GetComponent<Animator>().runtimeAnimatorController,
                Is.EqualTo(first.GetComponent<Animator>().runtimeAnimatorController));
        }

        [Test]
        public void Undo_RevertsPresetAndBakedClipTogether()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "OptionPanel", UIPointerEffectMask.None);
            AnimationClip clip = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            Undo.IncrementCurrentGroup();

            var serialized = new SerializedObject(preset);
            serialized.FindProperty("_open._time").floatValue = 2f;
            serialized.ApplyModifiedProperties();
            UIAnimationGenerator.RecordUndo(preset);
            UIAnimationGenerator.Rebuild(preset);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Assert.That(clip.length, Is.EqualTo(2f).Within(0.001f));

            Undo.PerformUndo();

            Assert.That(preset.Get(UIAnimationState.Open).Time, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(clip.length, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void SyncComponents_AddsCanvasGroupToEverySharedImage()
        {
            Image first = MakeImage("First");
            Image second = MakeImage("Second");
            UIAnimationPreset preset = UIAnimationGenerator.Create(first, Folder, "Shared", UIPointerEffectMask.None);
            UIAnimationGenerator.Connect(second, preset);
            preset.FadeChildren = true;

            UIAnimationGenerator.SyncComponents(preset);

            Assert.That(first.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(second.GetComponent<CanvasGroup>(), Is.Not.Null);
        }

        [Test]
        public void Create_WithoutMouseEvents_AddsNoPointerComponent()
        {
            Image image = MakeImage("Panel");

            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "Panel", UIPointerEffectMask.None);

            Assert.That(preset.HasPointerEffects, Is.False);
            Assert.That(image.GetComponent<UIAnimationPlayer>(), Is.Not.Null);
            Assert.That(image.GetComponent<UIAnimationPointerEffects>(), Is.Null);
        }

        [Test]
        public void Create_MakesOnlyChosenMouseEvents()
        {
            Image image = MakeImage("Button");

            UIAnimationPreset preset = UIAnimationGenerator.Create(
                image, Folder, "Button", UIPointerEffectMask.Hover | UIPointerEffectMask.Pressed);

            Assert.That(preset.Get(UIPointerEffect.Hover).Enabled, Is.True);
            Assert.That(preset.Get(UIPointerEffect.Exit).Enabled, Is.False);
            Assert.That(preset.Get(UIPointerEffect.Pressed).Enabled, Is.True);
            Assert.That(image.GetComponent<UIAnimationPointerEffects>(), Is.Not.Null);
        }

        [Test]
        public void SuggestEffects_TurnsOnOnlyForInteractiveImages()
        {
            Image panel = MakeImage("Panel");
            Image button = MakeImage("Button");
            button.gameObject.AddComponent<Button>();

            Assert.That(UIAnimationGenerator.SuggestEffects(panel), Is.EqualTo(UIPointerEffectMask.None));
            Assert.That(UIAnimationGenerator.SuggestEffects(button), Is.EqualTo(UIPointerEffectMask.All));
        }

        [Test]
        public void SyncComponents_FollowsMouseChoiceOnEverySharedImage()
        {
            Image first = MakeImage("First");
            Image second = MakeImage("Second");
            UIAnimationPreset preset = UIAnimationGenerator.Create(first, Folder, "Shared", UIPointerEffectMask.None);
            UIAnimationGenerator.Connect(second, preset);

            preset.Get(UIPointerEffect.Hover).Enabled = true;
            UIAnimationGenerator.SyncComponents(preset);
            Assert.That(first.GetComponent<UIAnimationPointerEffects>(), Is.Not.Null);
            Assert.That(second.GetComponent<UIAnimationPointerEffects>(), Is.Not.Null);

            preset.Get(UIPointerEffect.Hover).Enabled = false;
            UIAnimationGenerator.SyncComponents(preset);
            Assert.That(first.GetComponent<UIAnimationPointerEffects>(), Is.Null);
            Assert.That(second.GetComponent<UIAnimationPointerEffects>(), Is.Null);
        }

        [Test]
        public void Connect_AddsPointerEffectsOnlyWhenPresetUsesThem()
        {
            Image first = MakeImage("First");
            Image second = MakeImage("Second");
            UIAnimationPreset preset = UIAnimationGenerator.Create(first, Folder, "Shared", UIPointerEffectMask.All);

            UIAnimationGenerator.Connect(second, preset);

            Assert.That(second.GetComponent<UIAnimationPointerEffects>(), Is.Not.Null);
        }

        [Test]
        public void Bake_WritesPositionAndRotationAsRelativeValues()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "Panel", UIPointerEffectMask.None);
            preset.Get(UIAnimationState.Open).Offset.Set(new Vector2(-80f, 20f), Vector2.zero, UIEase.Linear);
            preset.Get(UIAnimationState.Open).Rotation.Set(-8f, 0f, UIEase.Linear);

            UIAnimationGenerator.Rebuild(preset);

            AnimationClip open = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            AnimationCurve offsetX = AnimationUtility.GetEditorCurve(open, PlayerField("_animatedOffset.x"));
            AnimationCurve rotation = AnimationUtility.GetEditorCurve(open, PlayerField("_animatedRotation"));
            Assert.That(offsetX.Evaluate(0f), Is.EqualTo(-80f).Within(0.01f));
            Assert.That(offsetX.Evaluate(open.length), Is.EqualTo(0f).Within(0.01f));
            Assert.That(rotation.Evaluate(0f), Is.EqualTo(-8f).Within(0.01f));
            Assert.That(Bindings(preset, UIAnimationState.Open).Any(b => b.propertyName.StartsWith("m_AnchoredPosition")), Is.False,
                "절대 좌표를 구우면 클립을 함께 쓰는 Image가 한 자리로 몰린다");
        }

        [Test]
        public void Bake_SteppedMotionHoldsBetweenSteps()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "Panel", UIPointerEffectMask.None);
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
            open.Time = 1f;
            open.StepsPerSecond = 10;
            open.Scale.Set(Vector2.zero, Vector2.one, UIEase.Linear);

            UIAnimationGenerator.Rebuild(preset);

            AnimationCurve scaleX = AnimationUtility.GetEditorCurve(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open), Scale("x"));
            Assert.That(scaleX.length, Is.EqualTo(11));
            Assert.That(scaleX.Evaluate(0.15f), Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(scaleX.Evaluate(0.19f), Is.EqualTo(0.1f).Within(0.001f));
        }

        [Test]
        public void Wipe_SwitchesImageToFilled_AndRestoresWhenTurnedOff()
        {
            Image image = MakeImage("Image");
            image.type = Image.Type.Sliced;
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "Panel", UIPointerEffectMask.None);

            preset.WipeDirection = UIWipeDirection.TopToBottom;
            preset.Get(UIAnimationState.Open).Wipe.Set(0f, 1f, UIEase.Linear);
            UIAnimationGenerator.SyncComponents(preset);
            UIAnimationGenerator.Rebuild(preset);

            Assert.That(image.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(image.fillMethod, Is.EqualTo(Image.FillMethod.Vertical));
            Assert.That(image.fillOrigin, Is.EqualTo((int)Image.OriginVertical.Top));
            Assert.That(Bindings(preset, UIAnimationState.Open).Any(b => b.propertyName == "m_FillAmount"), Is.True);

            preset.Get(UIAnimationState.Open).Wipe.Off();
            UIAnimationGenerator.SyncComponents(preset);

            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
        }

        [Test]
        public void EveryStyle_BakesAndKeepsStayContinuous()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "Panel", UIPointerEffectMask.None);
            foreach (UIAnimationStyle style in System.Enum.GetValues(typeof(UIAnimationStyle)))
            {
                preset.ApplyStyle(style);
                UIAnimationGenerator.Rebuild(preset);

                UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
                Assert.That(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open).length, Is.EqualTo(open.Time).Within(0.001f), style.ToString());
                bool moves = style == UIAnimationStyle.PersonaDrift || style == UIAnimationStyle.StarRailHologram
                    || style == UIAnimationStyle.JrpgSummon || style == UIAnimationStyle.JrpgItem;
                Assert.That(preset.IsStayStatic, Is.EqualTo(!moves), style.ToString());
                AnimationClip stay = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Stay);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(stay))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(stay, binding);
                    Assert.That(curve.Evaluate(stay.length), Is.EqualTo(curve.Evaluate(0f)).Within(0.001f), style + " " + binding.propertyName);
                }
            }
        }

        [Test]
        public void StayStyle_FloatKeepsMovingAndReturnsToStart()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "Panel", UIPointerEffectMask.None);

            preset.ApplyStayStyle(UIStayStyle.Float);
            UIAnimationGenerator.Rebuild(preset);

            AnimationClip stay = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Stay);
            AnimationCurve offsetY = AnimationUtility.GetEditorCurve(stay, PlayerField("_animatedOffset.y"));
            Assert.That(preset.IsStayStatic, Is.False);
            Assert.That(offsetY.Evaluate(stay.length * 0.5f), Is.EqualTo(6f).Within(0.01f));
            Assert.That(offsetY.Evaluate(stay.length), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void MirrorOpenToClose_ReversesTracksAndSwapsEase()
        {
            UIAnimationPreset preset = UIAnimationGenerator.Create(MakeImage("Image"), Folder, "Panel", UIPointerEffectMask.None);
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
            open.Time = 1f;
            open.Offset.Set(new Vector2(100f, 0f), Vector2.zero, UIEase.EaseOutBack);
            open.Rotation.Off();

            preset.MirrorOpenToClose();

            UIAnimationStateSettings close = preset.Get(UIAnimationState.Close);
            Assert.That(close.Time, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(close.Offset.To, Is.EqualTo(new Vector2(100f, 0f)));
            Assert.That(close.Offset.Curve.Ease, Is.EqualTo(UIEase.EaseInBack));
            Assert.That(close.Rotation.Use, Is.False);
        }

        [Test]
        public void Inspector_ChoosingAnotherEaseAfterPresetMarksOnlyItsPage()
        {
            Image image = MakeImage("Image");
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "Panel", UIPointerEffectMask.All);
            UIAnimationEditor component = image.gameObject.AddComponent<UIAnimationEditor>();
            var editor = (UIAnimationEditorInspector)UnityEditor.Editor.CreateEditor(component);
            _created.Add(editor);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Type type = typeof(UIAnimationEditorInspector);
            type.GetMethod("Bind", flags).Invoke(editor, new object[] { image });
            type.GetMethod("SetEase", flags).Invoke(editor, new object[] { "_open._scale._curve", UIEase.Linear });
            Assert.That(preset.Get(UIAnimationState.Open).Scale.Curve.Ease, Is.EqualTo(UIEase.Linear));
            Assert.That(preset.StatePresetEdited, Is.True);
            Assert.That(preset.PointerPresetEdited, Is.False);
            System.Type page = type.GetNestedType("Page", System.Reflection.BindingFlags.NonPublic);
            type.GetField("_page", flags).SetValue(editor, System.Enum.ToObject(page, 1));
            type.GetMethod("SetEase", flags).Invoke(editor, new object[] { "_hover._scale._curve", UIEase.EaseOutSine });
            Assert.That(preset.Get(UIPointerEffect.Hover).Scale.Curve.Ease, Is.EqualTo(UIEase.EaseOutSine));
            Assert.That(preset.PointerPresetEdited, Is.True);
        }

        [Test]
        public void SavedPreset_LoadsIntoAnotherImageAtItsOwnScale()
        {
            Image source = MakeImage("Source");
            Image target = MakeImage("Target");
            target.rectTransform.localScale = new Vector3(0.5f, 0.5f, 1f);
            target.color = new Color(1f, 0f, 0f, 1f);
            UIAnimationPreset first = UIAnimationGenerator.Create(source, Folder, "Source", UIPointerEffectMask.None);
            UIAnimationPreset second = UIAnimationGenerator.Create(target, Folder, "Target", UIPointerEffectMask.None);
            first.ApplyStyle(UIAnimationStyle.PersonaSlash);
            first.Get(UIAnimationState.Open).Scale.Set(Vector2.one * 0.8f, Vector2.one, UIEase.Linear);

            UIAnimationGenerator.SaveAsPreset(first, $"{Root}/Presets/Saved.asset");
            UIAnimationPreset saved = UIAnimationGenerator.SavedPresets().Single(preset => preset.name == "Saved");
            second.CopyMotionFrom(saved);

            Assert.That(second.BaseScale, Is.EqualTo(new Vector3(0.5f, 0.5f, 1f)));
            Assert.That(second.BaseColor, Is.EqualTo(new Color(1f, 0f, 0f, 1f)));
            Assert.That(second.Get(UIAnimationState.Open).Scale.From, Is.EqualTo(Vector2.one * 0.4f));
            Assert.That(second.Get(UIAnimationState.Open).Offset.From, Is.EqualTo(new Vector2(120f, 0f)));
        }

        [Test]
        public void Stagger_DelaysChildrenInOrder_AndClosesInReverse()
        {
            Image parent = MakeImage("Parent");
            Image first = MakeImage("First");
            Image second = MakeImage("Second");
            Image third = MakeImage("Third");
            foreach (Image child in new[] { first, second, third })
            {
                child.transform.SetParent(parent.transform);
            }
            UIAnimationGenerator.Create(parent, Folder, "Parent", UIPointerEffectMask.None);
            UIAnimationPreset shared = UIAnimationGenerator.Create(first, Folder, "Child", UIPointerEffectMask.None);
            UIAnimationGenerator.ShareWithChildren(parent.GetComponent<UIAnimationPlayer>(), shared);

            UIAnimationPlayer player = parent.GetComponent<UIAnimationPlayer>();
            UIAnimationGenerator.ApplyStagger(player, 0.05f, false);

            Assert.That(player.Followers.Count, Is.EqualTo(3));
            Assert.That(third.GetComponent<UIAnimationPlayer>().OpenDelay, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(first.GetComponent<UIAnimationPlayer>().CloseDelay, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(third.GetComponent<UIAnimationPlayer>().CloseDelay, Is.EqualTo(0f));
            Assert.That(player.CloseDelay, Is.EqualTo(0.1f + shared.Get(UIAnimationState.Close).Duration).Within(0.0001f));

            UIAnimationGenerator.ClearStagger(player);

            Assert.That(player.Followers, Is.Empty);
            Assert.That(third.GetComponent<UIAnimationPlayer>().OpenDelay, Is.EqualTo(0f));
            Assert.That(player.CloseDelay, Is.EqualTo(0f));
        }

        [Test]
        public void ConnectButton_WiresOnceAndCanBeRemoved()
        {
            Image image = MakeImage("Panel");
            Image buttonImage = MakeImage("Button");
            Button button = buttonImage.gameObject.AddComponent<Button>();
            UIAnimationGenerator.Create(image, Folder, "Panel", UIPointerEffectMask.None);
            UIAnimationPlayer player = image.GetComponent<UIAnimationPlayer>();

            UIAnimationGenerator.ConnectButton(button, player, nameof(UIAnimationPlayer.Close));
            UIAnimationGenerator.ConnectButton(button, player, nameof(UIAnimationPlayer.Close));

            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(UIAnimationPlayer.Close)));

            UIAnimationGenerator.DisconnectButton(button, player, nameof(UIAnimationPlayer.Close));

            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(0));
        }

        [Test]
        public void SetPivot_KeepsTheImageWhereItIsOnScreen()
        {
            Image image = MakeImage("Panel");
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(200f, 100f);
            rect.anchoredPosition = new Vector2(30f, 40f);

            UIAnimationGenerator.SetPivot(rect, new Vector2(0f, 1f));

            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(-70f, 90f)));
        }

        [Test]
        public void Connect_OnPrefabInstance_RecordsOverrides()
        {
            GameObject source = new GameObject("Hud", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator));
            _created.Add(source);
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(source, $"{Root}/Hud.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            _created.Add(instance);

            UIAnimationPreset preset = UIAnimationGenerator.Create(instance.GetComponent<Image>(), Folder, "Hud", UIPointerEffectMask.All);

            Animator animator = instance.GetComponent<Animator>();
            PropertyModification[] changes = PrefabUtility.GetPropertyModifications(instance);
            Assert.That(changes.Any(change => change.target != null && change.propertyPath == "m_Controller"), Is.True, "씬을 다시 열면 연결이 사라지면 안 된다");
            Assert.That(animator.runtimeAnimatorController, Is.EqualTo(UIAnimationGenerator.ControllerOf(preset)));
            Assert.That(PrefabUtility.GetAddedComponents(instance).Select(added => added.instanceComponent.GetType()),
                Is.SupersetOf(new[] { typeof(UIAnimationPlayer), typeof(UIAnimationPointerEffects) }));
        }

        [Test]
        public void IsProjectFolder_RejectsPathsOutsideAssets()
        {
            Assert.That(UIAnimationGenerator.IsProjectFolder("Assets/UI/Animation"), Is.True);
            Assert.That(UIAnimationGenerator.IsProjectFolder("Assets"), Is.True);
            Assert.That(UIAnimationGenerator.IsProjectFolder("Packages/UI"), Is.False);
            Assert.That(UIAnimationGenerator.IsProjectFolder("AssetsOther/UI"), Is.False);
        }

        private Image MakeImage(string name)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _created.Add(target);
            return target.GetComponent<Image>();
        }

        private static AnimatorState State(AnimatorController controller, string name)
        {
            return controller.layers[0].stateMachine.states.Single(child => child.state.name == name).state;
        }

        private static EditorCurveBinding[] Bindings(UIAnimationPreset preset, UIAnimationState state)
        {
            return AnimationUtility.GetCurveBindings(UIAnimationGenerator.ClipOf(preset, state));
        }

        private static EditorCurveBinding Scale(string axis)
        {
            return EditorCurveBinding.FloatCurve(string.Empty, typeof(RectTransform), $"m_LocalScale.{axis}");
        }

        private static EditorCurveBinding PlayerField(string property)
        {
            return EditorCurveBinding.FloatCurve(string.Empty, typeof(UIAnimationPlayer), property);
        }

        private static EditorCurveBinding Enabled()
        {
            return EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_Enabled");
        }
    }
}
