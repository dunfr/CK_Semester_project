using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationFxEditorTests
    {
        private const string Folder = "Assets/__UIAnimationFxEditorTests";
        private readonly List<Object> _created = new List<Object>();

        private static UIFloatTrack[] Tracks(UIAnimationStateSettings state) => new[]
            { state.Wipe, state.Skew, state.Slice, state.Split, state.Scan, state.Dissolve, state.Sheen, state.Flash };
        private static readonly string[] Fields = { "_wipe", "_skew", "_slice", "_split", "_scan", "_dissolve", "_sheen", "_flash" };

        private Image Image()
        {
            var target = new GameObject("FX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _created.Add(target);
            return target.GetComponent<Image>();
        }

        private AnimationClip Clip()
        {
            var clip = new AnimationClip(); _created.Add(clip); return clip;
        }

        private static AnimationCurve Curve(AnimationClip clip, string field)
            => AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(UIAnimationFx), field));

        [TearDown]
        public void TearDown()
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            foreach (Object item in _created) if (item != null) Object.DestroyImmediate(item);
            _created.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void EveryUsedFxTrack_IsBoundInAllStatesAndPreviewUsesIdenticalCurves()
        {
            Image image = Image();
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "FX", UIPointerEffectMask.None);
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState))) preset.Get(state).ClearMotion();
            preset.Get(UIAnimationState.Open).StepsPerSecond = 24;
            foreach (UIFloatTrack track in Tracks(preset.Get(UIAnimationState.Close))) track.Set(0.75f, 0.1f, UIEase.EaseOutCubic);
            preset.Get(UIAnimationState.Close).Skew.Set(-30f, 10f, UIEase.EaseOutBack);
            UIAnimationGenerator.SyncComponents(preset);
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState)))
            {
                AnimationClip normal = Clip(), preview = Clip();
                UIAnimationClipBaker.Bake(preset, state, normal);
                UIAnimationClipBaker.BakePreview(preset, state, image.rectTransform, preview);
                foreach (string field in Fields.Concat(new[] { "_seed" }))
                {
                    AnimationCurve a = Curve(normal, field), b = Curve(preview, field);
                    Assert.That(a, Is.Not.Null, state + ":" + field);
                    Assert.That(b, Is.Not.Null, state + ":" + field);
                    Assert.That(b.length, Is.EqualTo(a.length));
                    for (int i = 0; i <= 100; i++)
                        Assert.That(b.Evaluate(preview.length * i / 100f), Is.EqualTo(a.Evaluate(normal.length * i / 100f)).Within(0.00001f));
                }
                Assert.That(AnimationUtility.GetCurveBindings(normal).Any(binding => binding.propertyName == "m_FillAmount"), Is.False);
                if (state != UIAnimationState.Close)
                {
                    Assert.That(Curve(normal, "_wipe").Evaluate(normal.length), Is.EqualTo(1f));
                    foreach (string field in Fields.Skip(1)) Assert.That(Curve(normal, field).Evaluate(normal.length), Is.Zero);
                    foreach (string field in Fields) Assert.That(Curve(normal, field).length, Is.EqualTo(2));
                }
            }
        }

        [Test]
        public void SliceSeed_UsesConstantTangentsAndRealPlaybackFps()
        {
            Image image = Image();
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "FX", UIPointerEffectMask.None);
            UIAnimationStateSettings open = preset.Get(UIAnimationState.Open);
            open.ClearMotion(); open.Time = 1f; open.Speed = 2f; preset.GlitchFps = 8f;
            open.Slice.Set(0.8f, 0f, UIEase.Linear);
            AnimationClip clip = Clip();
            UIAnimationClipBaker.Bake(preset, UIAnimationState.Open, clip);
            AnimationCurve seed = Curve(clip, "_seed");
            Assert.That(seed.length, Is.EqualTo(5));
            for (int i = 0; i < seed.length; i++)
            {
                Assert.That(seed[i].time, Is.EqualTo(i * 0.25f).Within(0.00001f));
                Assert.That(seed[i].value, Is.EqualTo(i));
                Assert.That(AnimationUtility.GetKeyLeftTangentMode(seed, i), Is.EqualTo(AnimationUtility.TangentMode.Constant));
                Assert.That(AnimationUtility.GetKeyRightTangentMode(seed, i), Is.EqualTo(AnimationUtility.TangentMode.Constant));
            }
            Assert.That(seed.Evaluate(0.249f), Is.Zero);
            Assert.That(seed.Evaluate(0.251f), Is.EqualTo(1f));
            Assert.That(open.Duration, Is.EqualTo(0.5f));
            UIAnimationClipBaker.Bake(preset, UIAnimationState.Stay, clip);
            Assert.That(Curve(clip, "_seed").keys.All(key => key.value == 0f), Is.True);
        }

        [Test]
        public void CopyMirrorSpanAndClear_IncludeAllFxAndShapeValues()
        {
            UIAnimationPreset source = ScriptableObject.CreateInstance<UIAnimationPreset>();
            UIAnimationPreset copy = ScriptableObject.CreateInstance<UIAnimationPreset>();
            _created.Add(source); _created.Add(copy);
            source.WipeAngle = 33f; source.WipeSoftness = 0.4f; source.GlitchBands = 19; source.GlitchFps = 24f;
            source.DissolveCellSize = 11f; source.SheenAngle = -32f; source.SheenWidth = 0.3f; source.SheenIntensity = 2.5f;
            UIFloatTrack[] open = Tracks(source.Get(UIAnimationState.Open));
            for (int i = 0; i < open.Length; i++)
            {
                open[i].Set(0.2f, 0.7f, UIEase.Linear);
                open[i].Curve.SetCustom(UIMotionCurves.Window(UIEase.EaseOutCubic, 0.1f, 0.8f));
            }
            source.MirrorOpenToClose();
            copy.CopyValuesFrom(source);
            Assert.That(copy.WipeAngle, Is.EqualTo(33f)); Assert.That(copy.WipeSoftness, Is.EqualTo(0.4f));
            Assert.That(copy.GlitchBands, Is.EqualTo(19)); Assert.That(copy.GlitchFps, Is.EqualTo(24f));
            Assert.That(copy.DissolveCellSize, Is.EqualTo(11f)); Assert.That(copy.SheenAngle, Is.EqualTo(-32f));
            Assert.That(copy.SheenWidth, Is.EqualTo(0.3f)); Assert.That(copy.SheenIntensity, Is.EqualTo(2.5f));
            UIFloatTrack[] close = Tracks(copy.Get(UIAnimationState.Close));
            UIFloatTrack[] copiedOpen = Tracks(copy.Get(UIAnimationState.Open));
            for (int i = 0; i < open.Length; i++)
            {
                Assert.That(copiedOpen[i].Use, Is.True); Assert.That(copiedOpen[i].From, Is.EqualTo(0.2f));
                Assert.That(close[i].From, Is.EqualTo(0.7f)); Assert.That(close[i].To, Is.EqualTo(0.2f));
                for (int t = 0; t <= 20; t++)
                    Assert.That(close[i].Curve.Evaluate(t / 20f), Is.EqualTo(1f - open[i].Curve.Evaluate(1f - t / 20f)).Within(0.0001f));
            }
            UIAnimationSpan span = copy.GetSpan(UIAnimationState.Stay);
            foreach (UIRange<float> range in new[] { span.Wipe, span.Skew, span.Slice, span.Split, span.Scan, span.Dissolve, span.Sheen, span.Flash })
            { Assert.That(range.From, Is.EqualTo(0.7f)); Assert.That(range.To, Is.EqualTo(0.7f)); }
            copiedOpen[1].Curve.SetCustom(AnimationCurve.Linear(0f, 0f, 1f, 0f));
            Assert.That(open[1].Curve.Evaluate(1f), Is.EqualTo(1f));
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState)))
            {
                copy.Get(state).ClearMotion();
                Assert.That(Tracks(copy.Get(state)).All(track => !track.Use), Is.True);
            }
            Assert.That(copy.UsesFx, Is.False);
        }

        [Test]
        public void StayStatic_IncludesEachFxAndSeedAnimation()
        {
            var preset = ScriptableObject.CreateInstance<UIAnimationPreset>(); _created.Add(preset);
            UIAnimationStateSettings stay = preset.Get(UIAnimationState.Stay);
            foreach (UIFloatTrack track in Tracks(stay))
            {
                track.Set(0f, 0.5f, UIEase.Linear);
                Assert.That(preset.IsStayStatic, Is.False);
                Assert.That(preset.UsesFx, Is.True);
                track.Off();
                Assert.That(preset.IsStayStatic, Is.True);
            }
            preset.Get(UIAnimationState.Open).Slice.Set(0.4f, 0.4f, UIEase.Linear);
            stay.Slice.Set(0.4f, 0.4f, UIEase.Linear);
            Assert.That(preset.IsStayStatic, Is.False, "값이 일정해도 띠가 갱신됩니다.");
            stay.Slice.Off();
            Assert.That(preset.IsStayStatic, Is.True);
        }

        [Test]
        public void UsesFx_AddsAndRemovesOnlyTheRequiredRootComponent()
        {
            Image image = Image();
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "FX", UIPointerEffectMask.None);
            Assert.That(image.GetComponent<UIAnimationFx>(), Is.Null);
            foreach (UIFloatTrack track in Tracks(preset.Get(UIAnimationState.Open)))
            {
                track.Set(0.5f, 0f, UIEase.Linear);
                UIAnimationGenerator.SyncComponents(preset);
                Assert.That(image.GetComponent<UIAnimationFx>(), Is.Not.Null);
                Assert.That(image.GetComponent<UIAnimationFxMesh>(), Is.Null, "쉬는 동안은 연결 컴포넌트도 만들지 않습니다.");
                Object.DestroyImmediate(image.GetComponent<UIAnimationFx>());
                UIAnimationGenerator.Repair(image, preset);
                Assert.That(image.GetComponent<UIAnimationFx>(), Is.Not.Null);
                track.Off();
                UIAnimationGenerator.SyncComponents(preset);
                Assert.That(image.GetComponent<UIAnimationFx>(), Is.Null);
            }
        }

        private static UIAnimationFx ActiveFx(Image image)
        {
            UIAnimationFx fx = image.gameObject.AddComponent<UIAnimationFx>();
            var serialized = new SerializedObject(fx);
            serialized.FindProperty("_skew").floatValue = 20f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fx.ApplyChangedValues();
            return fx;
        }

        [UnityTest]
        public IEnumerator DestroyingFxComponent_RemovesRootAndChildLinks()
        {
            Image image = Image(), child = Image();
            child.transform.SetParent(image.transform, false);
            UIAnimationFx fx = ActiveFx(image);
            UIAnimationFxMesh rootLink = image.GetComponent<UIAnimationFxMesh>();
            UIAnimationFxMesh childLink = child.GetComponent<UIAnimationFxMesh>();
            Assert.That(rootLink, Is.Not.Null);
            Assert.That(childLink, Is.Not.Null);

            Object.DestroyImmediate(fx);
            Assert.That(rootLink.Owner, Is.Null);
            Assert.That(childLink.Owner, Is.Null);
            yield return null; yield return null;

            Assert.That(image.GetComponent<UIAnimationFxMesh>(), Is.Null);
            Assert.That(child.GetComponent<UIAnimationFxMesh>(), Is.Null);
            Assert.That(image, Is.Not.Null);
            Assert.That(child, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ReplacingFxBeforeDeferredCleanup_PreservesReboundLinks()
        {
            Image image = Image(), child = Image();
            child.transform.SetParent(image.transform, false);
            UIAnimationFx fx = ActiveFx(image);
            UIAnimationFxMesh rootLink = image.GetComponent<UIAnimationFxMesh>();
            UIAnimationFxMesh childLink = child.GetComponent<UIAnimationFxMesh>();
            Assert.That(rootLink, Is.Not.Null);
            Assert.That(childLink, Is.Not.Null);

            Object.DestroyImmediate(fx);
            UIAnimationFx replacement = ActiveFx(image);
            yield return null; yield return null;

            Assert.That(image.GetComponent<UIAnimationFxMesh>(), Is.SameAs(rootLink));
            Assert.That(child.GetComponent<UIAnimationFxMesh>(), Is.SameAs(childLink));
            Assert.That(rootLink.Owner, Is.SameAs(replacement));
            Assert.That(childLink.Owner, Is.SameAs(replacement));
            Assert.That(replacement.IsRest, Is.False);
        }

        [Test]
        public void Repair_RebakesLegacyFillAndRestoresSlicedImageWithoutChangingPresetValues()
        {
            Image image = Image(); image.type = UnityEngine.UI.Image.Type.Sliced;
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "FX", UIPointerEffectMask.None,
                value => value.Get(UIAnimationState.Open).Wipe.Set(0f, 1f, UIEase.Linear));
            string values = JsonUtility.ToJson(preset);
            AnimationClip old = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            old.ClearCurves();
            AnimationUtility.SetEditorCurve(old, EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_FillAmount"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            image.type = UnityEngine.UI.Image.Type.Filled;
            old.SampleAnimation(image.gameObject, 0.3f);
            UIAnimationGenerator.SyncComponents(preset);
            Assert.That(image.fillAmount, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(image.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
            Object.DestroyImmediate(image.GetComponent<UIAnimationFx>());
            UIAnimationGenerator.Repair(image, preset);
            Assert.That(image.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
            Assert.That(image.GetComponent<UIAnimationFx>(), Is.Not.Null);
            Assert.That(JsonUtility.ToJson(preset), Is.EqualTo(values));
            foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState)))
            {
                AnimationClip clip = UIAnimationGenerator.ClipOf(preset, state);
                Assert.That(Curve(clip, "_wipe"), Is.Not.Null);
                Assert.That(AnimationUtility.GetCurveBindings(clip).Any(binding => binding.propertyName == "m_FillAmount"), Is.False);
            }
        }

        [Test]
        public void Repair_AlsoRestoresOtherImagesSharingTheLegacyPreset()
        {
            Image first = Image(); first.type = UnityEngine.UI.Image.Type.Sliced;
            Image second = Image(); second.type = UnityEngine.UI.Image.Type.Sliced;
            UIAnimationPreset preset = UIAnimationGenerator.Create(first, Folder, "FX", UIPointerEffectMask.None,
                value => value.Get(UIAnimationState.Open).Wipe.Set(0f, 1f, UIEase.Linear));
            UIAnimationGenerator.Connect(second, preset);
            AnimationClip old = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            old.ClearCurves();
            AnimationUtility.SetEditorCurve(old, EditorCurveBinding.FloatCurve(string.Empty, typeof(Image), "m_FillAmount"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            first.type = second.type = UnityEngine.UI.Image.Type.Filled;

            UIAnimationGenerator.Repair(first, preset);

            Assert.That(second.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
            Assert.That(second.GetComponent<UIAnimationFx>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PreviewStopAndExternalAnimationModeStop_RestoreFx()
        {
            Image image = Image();
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "FX", UIPointerEffectMask.None,
                value => value.Get(UIAnimationState.Open).Skew.Set(35f, 0f, UIEase.Linear));
            var previewer = new UIAnimationPreviewer();
            try
            {
                for (int external = 0; external < 2; external++)
                {
                    AnimationClip clip = Clip(); UIAnimationClipBaker.BakePreview(preset, UIAnimationState.Open, image.rectTransform, clip);
                    previewer.Play(new[] { new UIAnimationPreviewer.Entry(image.gameObject, clip, 0.0, 1f) });
                    previewer.Seek(0.0);
                    yield return null; yield return null;
                    Assert.That(image.GetComponent<UIAnimationFx>().IsRest, Is.False);
                    if (external == 0) previewer.Stop();
                    else AnimationMode.StopAnimationMode();
                    yield return null; yield return null;
                    Assert.That(image.GetComponent<UIAnimationFx>().IsRest, Is.True);
                }
            }
            finally { previewer.Stop(); }
        }

        [Test]
        public void TransientLinks_AreExcludedFromPrefabAndPrefabOverrides()
        {
            Image image = Image();
            UIAnimationPreset preset = UIAnimationGenerator.Create(image, Folder, "FX", UIPointerEffectMask.None,
                value => value.Get(UIAnimationState.Open).Skew.Set(30f, 0f, UIEase.Linear));
            string path = Folder + "/FX.prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(image.gameObject, path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset); _created.Add(instance);
            UIAnimationFx fx = instance.GetComponent<UIAnimationFx>();
            var serialized = new SerializedObject(fx); serialized.FindProperty("_skew").floatValue = 20f; serialized.ApplyModifiedPropertiesWithoutUndo();
            fx.ApplyChangedValues();
            UIAnimationFxMesh link = instance.GetComponent<UIAnimationFxMesh>();
            Assert.That(link, Is.Not.Null);
            Assert.That((link.hideFlags & HideFlags.DontSaveInEditor) != 0, Is.True);
            Assert.That(PrefabUtility.GetAddedComponents(instance).Any(added => added.instanceComponent is UIAnimationFxMesh), Is.False);
            PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/FXCopy.prefab");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/FXCopy.prefab").GetComponentsInChildren<UIAnimationFxMesh>(true), Is.Empty);
        }

        [Test]
        public void SceneSave_DoesNotSerializeMeshLinks()
        {
            Scene scene = SceneManager.GetActiveScene();
            Image image = Image();
            UIAnimationGenerator.Create(image, Folder, "FX", UIPointerEffectMask.None,
                value => value.Get(UIAnimationState.Open).Skew.Set(30f, 0f, UIEase.Linear));
            UIAnimationFx fx = image.GetComponent<UIAnimationFx>();
            var serialized = new SerializedObject(fx);
            serialized.FindProperty("_skew").floatValue = 20f;
            serialized.ApplyModifiedPropertiesWithoutUndo(); fx.ApplyChangedValues();
            UIAnimationFxMesh link = image.GetComponent<UIAnimationFxMesh>();
            Assert.That(link, Is.Not.Null);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(link)));
            string rootGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(fx)));
            Assert.That(guid, Is.Not.Empty);
            Assert.That(rootGuid, Is.Not.Empty);
            string path = Folder + "/Transient.unity";
            // 테스트 러너의 이름 없는 씬은 그대로 두고 복사본의 실제 직렬화 결과를 검사한다.
            Assert.That(EditorSceneManager.SaveScene(scene, path, true), Is.True);
            string saved = File.ReadAllText(path);
            Assert.That(saved, Does.Contain(rootGuid));
            Assert.That(saved, Does.Not.Contain(guid));
        }
    }
}
