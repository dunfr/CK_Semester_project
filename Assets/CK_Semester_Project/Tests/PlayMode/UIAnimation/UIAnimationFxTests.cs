using System;
using System.Collections;
using System.Collections.Generic;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationFxTests
    {
        private const string Folder = "Assets/__UIAnimationFxPlayTests";
        private GameObject _canvas;
        private Image _image;
        private UIAnimationPreset _preset;
        private UIAnimationFx _fx;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(Canvas));
            var target = new GameObject("FX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(_canvas.transform, false);
            _image = target.GetComponent<Image>();
            _image.rectTransform.sizeDelta = new Vector2(200f, 100f);
            _preset = UIAnimationGenerator.Create(_image, Folder, "FX", UIPointerEffectMask.All, preset =>
            {
                foreach (UIAnimationState state in Enum.GetValues(typeof(UIAnimationState))) preset.Get(state).ClearMotion();
                preset.Get(UIAnimationState.Open).Time = 0.12f;
                preset.Get(UIAnimationState.Close).Time = 0.12f;
                preset.Get(UIAnimationState.Open).Skew.Set(30f, 0f, UIEase.Linear);
                preset.Get(UIAnimationState.Open).Flash.Set(1f, 0f, UIEase.Linear);
                preset.Get(UIAnimationState.Close).Skew.Set(0f, -30f, UIEase.Linear);
            });
            _fx = target.GetComponent<UIAnimationFx>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            AssetDatabase.DeleteAsset(Folder);
        }

        private void Set(string field, float value)
        {
            var serialized = new SerializedObject(_fx);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _fx.ApplyChangedValues();
        }

        private IEnumerator Wait(Func<bool> condition)
        {
            float end = Time.realtimeSinceStartup + 3f;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, "FX 상태 전환 시간 초과");
        }

        [UnityTest]
        public IEnumerator OpenEndAndDisabledClose_ReturnToRest()
        {
            _image.gameObject.SetActive(false); _image.gameObject.SetActive(true);
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            Assert.That(_fx.IsRest, Is.True);
            Assert.That(_image.material, Is.SameAs(_image.defaultMaterial));
            _image.GetComponent<UIAnimationPlayer>().Close();
            yield return Wait(() => !_image.gameObject.activeSelf);
            Assert.That(_fx.Values.IsRest, Is.True);
            Assert.That(_fx.Values.Seed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ChildImageAndTmp_AreConnectedAndUseRootCoordinates()
        {
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            var child = new GameObject("Child", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(_image.transform, false);
            child.transform.localPosition = new Vector3(20f, 10f);
            child.transform.localScale = new Vector3(0.5f, 2f, 1f);
            Type tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            label.transform.SetParent(_image.transform, false);
            label.transform.localPosition = new Vector3(0f, 15f);
            ((RectTransform)label.transform).sizeDelta = new Vector2(100f, 40f);
            Component text = label.AddComponent(tmpType);
            Object font = AssetDatabase.LoadAssetAtPath<Object>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Assert.That(font, Is.Not.Null, "TMP 회귀 테스트용 폰트가 필요합니다.");
            tmpType.GetProperty("font").SetValue(text, font);
            tmpType.GetProperty("text").SetValue(text, "A");
            tmpType.GetProperty("fontSize").SetValue(text, 24f);
            Action regenerate = () => tmpType.GetMethod("ForceMeshUpdate").Invoke(text, new object[] { false, false });
            regenerate();
            Vector3[] before = GlyphVertices(text, tmpType);
            Set("_skew", 30f);
            yield return null;
            Assert.That(child.GetComponent<UIAnimationFxMesh>().Owner, Is.SameAs(_fx));
            Assert.That(label.GetComponent<UIAnimationFxText>().Owner, Is.SameAs(_fx));
            regenerate();
            Vector3[] after = GlyphVertices(text, tmpType);
            for (int i = 0; i < 4; i++)
            {
                Vector3 rootPoint = _image.transform.InverseTransformPoint(label.transform.TransformPoint(before[i]));
                rootPoint.x += Mathf.Tan(30f * Mathf.Deg2Rad) * rootPoint.y;
                Vector3 expected = label.transform.InverseTransformPoint(_image.transform.TransformPoint(rootPoint));
                Assert.That(Vector3.Distance(after[i], expected), Is.LessThan(0.001f));
            }
            regenerate();
            CollectionAssert.AreEqual(after, GlyphVertices(text, tmpType), "재생성이 변형을 누적하면 안 됩니다.");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(_image.gameObject, Folder + "/WithText.prefab");
            Assert.That(saved.GetComponentsInChildren<UIAnimationFxMesh>(true), Is.Empty);
            Assert.That(saved.GetComponentsInChildren<UIAnimationFxText>(true), Is.Empty);
            using (var vh = new VertexHelper())
            {
                UIVertex a = UIVertex.simpleVert, b = a, c = a;
                a.position = new Vector3(0f, 10f); b.position = new Vector3(0f, -10f); c.position = new Vector3(20f, -10f);
                vh.AddUIVertexTriangleStream(new List<UIVertex> { a, b, c });
                child.GetComponent<UIAnimationFxMesh>().ModifyMesh(vh);
                vh.PopulateUIVertex(ref a, 0);
                Assert.That(a.position.x, Is.EqualTo(Mathf.Tan(30f * Mathf.Deg2Rad) * 60f).Within(0.001f));
            }
            Set("_skew", 0f); regenerate();
            CollectionAssert.AreEqual(before, GlyphVertices(text, tmpType));
        }

        private static Vector3[] GlyphVertices(Component text, Type type)
        {
            object info = type.GetProperty("textInfo").GetValue(text);
            var meshes = (Array)info.GetType().GetField("meshInfo").GetValue(info);
            object mesh = meshes.GetValue(0);
            var vertices = (Vector3[])mesh.GetType().GetField("vertices").GetValue(mesh);
            var result = new Vector3[4]; Array.Copy(vertices, result, 4); return result;
        }

        [UnityTest]
        public IEnumerator UnchangedValuesAndUnusedSeed_DoNotDirtyGraphics()
        {
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            int dirty = 0;
            UnityEngine.Events.UnityAction callback = () => dirty++;
            _image.RegisterDirtyVerticesCallback(callback);
            try
            {
                Set("_skew", 15f);
                Assert.That(dirty, Is.GreaterThan(0));
                int changed = dirty;
                for (int i = 0; i < 100; i++) _fx.ApplyChangedValues();
                Set("_seed", 5f);
                Assert.That(dirty, Is.EqualTo(changed));
                Set("_skew", 0f);
                Assert.That(dirty, Is.GreaterThan(changed));
            }
            finally { _image.UnregisterDirtyVerticesCallback(callback); }
        }

        [UnityTest]
        public IEnumerator DisableAndReenableInOneFrame_RestoresAndRetainsTheLinks()
        {
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            Set("_skew", 20f);
            UIAnimationFxMesh link = _image.GetComponent<UIAnimationFxMesh>();
            Assert.That(link, Is.Not.Null);
            _fx.enabled = false;
            Assert.That(_fx.Values.IsRest, Is.True);
            _fx.enabled = true;
            Set("_skew", 30f);
            yield return null;
            Assert.That(_image.GetComponent<UIAnimationFxMesh>(), Is.SameAs(link));
            Assert.That(link.Owner, Is.SameAs(_fx));
            Set("_skew", 0f);
        }

        [UnityTest]
        public IEnumerator HighlightAndFlash_MultiplyAndClearIndependently()
        {
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            UIPointerEffectSettings hover = _preset.Get(UIPointerEffect.Hover);
            hover.ClearMotion(); hover.Time = 0.01f;
            hover.Color.Set(Color.white, Color.white, UIEase.Linear); hover.Brightness = 1.3f;
            UIAnimationPointerEffects pointer = _image.GetComponent<UIAnimationPointerEffects>();
            pointer.PlayEffect(UIPointerEffect.Hover);
            yield return new WaitForSecondsRealtime(0.04f);
            Set("_flash", 0.5f);
            Assert.That(_image.materialForRendering.color.r, Is.EqualTo(1.95f).Within(0.001f));
            Set("_flash", 0f);
            Assert.That(_image.materialForRendering.color.r, Is.EqualTo(1.3f).Within(0.001f));
            Set("_flash", 0.5f);
            pointer.enabled = false;
            Assert.That(_image.materialForRendering.color.r, Is.EqualTo(1.5f).Within(0.001f));
            Set("_flash", 0f);
            Assert.That(_image.material, Is.SameAs(_image.defaultMaterial));
        }

        [UnityTest]
        public IEnumerator Links_AreTransientAndNestedFxOwnTheirSubtree()
        {
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            var child = new GameObject("Child", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(_image.transform, false);
            var nested = new GameObject("Nested", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIAnimationFx));
            nested.transform.SetParent(_image.transform, false);
            var grandchild = new GameObject("Nested Child", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            grandchild.transform.SetParent(nested.transform, false);
            Set("_skew", 20f);
            Assert.That(child.GetComponent<UIAnimationFxMesh>(), Is.Not.Null);
            Assert.That(nested.GetComponent<UIAnimationFxMesh>(), Is.Null);
            Assert.That(grandchild.GetComponent<UIAnimationFxMesh>(), Is.Null);
            foreach (UIAnimationFxMesh link in _image.GetComponentsInChildren<UIAnimationFxMesh>(true))
            {
                Assert.That((link.hideFlags & HideFlags.HideInInspector) != 0, Is.True);
                Assert.That((link.hideFlags & HideFlags.DontSaveInEditor) != 0, Is.True);
                Assert.That((link.hideFlags & HideFlags.DontSaveInBuild) != 0, Is.True);
            }
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(_image.gameObject, Folder + "/Saved.prefab");
            Assert.That(saved.GetComponentsInChildren<UIAnimationFxMesh>(true), Is.Empty);
            Assert.That(saved.GetComponentsInChildren<UIAnimationFxText>(true), Is.Empty);
        }
    }
}
