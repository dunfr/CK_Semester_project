using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationHighlightEditorTests
    {
        private const string Folder = "Assets/__UIHighlightEditorTests";
        private const string Art = "Assets/CK_Semester_Project/Prototype/Temp/Graphics/UI/10-01/Battle_State/";
        private readonly List<Object> _created = new List<Object>();

        private Image Image()
        {
            var go = new GameObject("Target", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _created.Add(go);
            return go.GetComponent<Image>();
        }
        private Sprite Selected => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Turn_SkillButton_Selected.png");
        private UIAnimationPreset Create(Image image, System.Action<UIAnimationPreset> setup = null)
        {
            return UIAnimationGenerator.Create(image, Folder, "Target", UIPointerEffectMask.All, setup);
        }
        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created) Object.DestroyImmediate(item);
            _created.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Create_SetupRunsBeforeBakingAndConnection()
        {
            Image image = Image();
            UIAnimationPreset preset = Create(image, p => { p.ApplyStyle(UIAnimationStyle.JrpgDialogue); p.Get(UIAnimationState.Open).Time = 0.73f; });
            Assert.That(UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open).length, Is.EqualTo(0.73f).Within(0.001f));
            Assert.That(image.GetComponent<UIAnimationPlayer>().Preset, Is.SameAs(preset));
            Assert.That(preset.AnimatesScale, Is.False);
            foreach (UIAnimationState state in System.Enum.GetValues(typeof(UIAnimationState)))
                Assert.That(AnimationUtility.GetCurveBindings(UIAnimationGenerator.ClipOf(preset, state))
                    .Any(binding => binding.type == typeof(RectTransform) && binding.propertyName.StartsWith("m_LocalScale.")), Is.False);
        }

        [Test]
        public void Create_SetupBakesFadeScaleRelativeToCapturedImage()
        {
            Image image = Image();
            image.rectTransform.localScale = new Vector3(1.2f, 0.8f, 1f);
            UIAnimationPreset preset = Create(image, p => p.ApplyStyle(UIAnimationStyle.Fade));
            AnimationClip clip = UIAnimationGenerator.ClipOf(preset, UIAnimationState.Open);
            Assert.That(preset.AnimatesScale, Is.True);
            foreach (string axis in new[] { "x", "y" })
            {
                float full = axis == "x" ? 1.2f : 0.8f;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(RectTransform), "m_LocalScale." + axis));
                Assert.That(curve, Is.Not.Null);
                Assert.That(curve.Evaluate(0f), Is.EqualTo(full * 0.975f).Within(0.001f));
                Assert.That(curve.Evaluate(clip.length), Is.EqualTo(full).Within(0.001f));
            }
        }

        [Test]
        public void Highlight_UsesButtonSpriteStateFirst()
        {
            Image image = Image();
            var state = image.GetComponent<Button>().spriteState;
            state.highlightedSprite = Selected;
            state.pressedSprite = Selected;
            image.GetComponent<Button>().spriteState = state;
            UIAnimationPreset preset = Create(image);
            Assert.That(Selected, Is.Not.Null);
            Assert.That(preset.Get(UIPointerEffect.Hover).Sprite, Is.SameAs(Selected));
            Assert.That(preset.Get(UIPointerEffect.Pressed).Sprite, Is.SameAs(Selected));
        }

        [Test]
        public void Highlight_FindsDefaultSelectedNamePair()
        {
            Image image = Image();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Turn_SkillButton_Default.png");
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(UIAnimationGenerator.SuggestHighlight(image), Is.SameAs(Selected));
            Assert.That(Create(image).Get(UIPointerEffect.Hover).Sprite, Is.SameAs(Selected));
        }

        [Test]
        public void DisabledHover_DoesNotFillSprite()
        {
            Image image = Image();
            var state = image.GetComponent<Button>().spriteState;
            state.highlightedSprite = Selected;
            image.GetComponent<Button>().spriteState = state;
            UIAnimationPreset preset = Create(image, p => p.SetPointerEffects(UIPointerEffectMask.None));
            Assert.That(preset.Get(UIPointerEffect.Hover).Sprite, Is.Null);
        }

        [Test]
        public void SyncComponents_PreservesPersistentTriggers()
        {
            Image image = Image();
            UIAnimationPreset preset = Create(image);
            UIAnimationPointerEffects effects = image.GetComponent<UIAnimationPointerEffects>();
            UnityEventTools.AddBoolPersistentListener(effects.Trigger(UIPointerEffect.Hover), image.gameObject.SetActive, true);
            preset.SetPointerEffects(UIPointerEffectMask.None);
            UIAnimationGenerator.SyncComponents(preset);
            Assert.That(image.GetComponent<UIAnimationPointerEffects>(), Is.SameAs(effects));
            Assert.That(effects.HasTriggers, Is.True);
        }

        [TestCase(UIPointerStyle.Grow)]
        [TestCase(UIPointerStyle.Nudge)]
        [TestCase(UIPointerStyle.Punch)]
        public void PointerStyle_LeavingGlowResetsBrightness(UIPointerStyle style)
        {
            UIAnimationPreset preset = Create(Image());
            preset.ApplyPointerStyle(UIPointerStyle.Glow);
            Assert.That(preset.Get(UIPointerEffect.Hover).Brightness, Is.EqualTo(1.3f));
            preset.ApplyPointerStyle(style);
            foreach (UIPointerEffect effect in System.Enum.GetValues(typeof(UIPointerEffect)))
                Assert.That(preset.Get(effect).Brightness, Is.EqualTo(1f));
        }

        [Test]
        public void CopyMotionFrom_CopiesHighlightSpriteColorBrightness()
        {
            UIAnimationPreset source = Create(Image());
            source.ApplyPointerStyle(UIPointerStyle.Glow);
            source.Get(UIPointerEffect.Hover).Sprite = Selected;
            source.Get(UIPointerEffect.Hover).Color.Set(Color.white, Color.cyan, UIEase.Linear);
            UIAnimationPreset copy = ScriptableObject.CreateInstance<UIAnimationPreset>();
            _created.Add(copy);
            copy.CopyMotionFrom(source);
            Assert.That(copy.Get(UIPointerEffect.Hover).Sprite, Is.SameAs(Selected));
            Assert.That(copy.Get(UIPointerEffect.Hover).Color.To, Is.EqualTo(Color.cyan));
            Assert.That(copy.Get(UIPointerEffect.Hover).Brightness, Is.EqualTo(1.3f));
        }

        [TestCase(UIPointerEffect.Hover)]
        [TestCase(UIPointerEffect.Pressed)]
        public void Preview_BindsSpriteToImage(UIPointerEffect effect)
        {
            Image image = Image();
            UIAnimationPreset preset = Create(image);
            preset.Get(UIPointerEffect.Hover).Sprite = Selected;
            var clip = new AnimationClip();
            _created.Add(clip);
            UIAnimationClipBaker.BakeEffectPreview(preset, effect, image, clip);
            EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            Assert.That(binding.type, Is.EqualTo(typeof(Image)));
            Assert.That(binding.propertyName, Is.EqualTo("m_Sprite"));
            Assert.That(AnimationUtility.GetObjectReferenceCurve(clip, binding)[0].value, Is.SameAs(Selected));
        }

        [Test]
        public void EffectBrightness_HoverExitAndNoChange()
        {
            UIAnimationPreset preset = Create(Image());
            Assert.That(UIAnimationClipBaker.EffectBrightness(preset, UIPointerEffect.Hover), Is.Null);
            preset.ApplyPointerStyle(UIPointerStyle.Glow);
            var hover = UIAnimationClipBaker.EffectBrightness(preset, UIPointerEffect.Hover);
            var exit = UIAnimationClipBaker.EffectBrightness(preset, UIPointerEffect.Exit);
            Assert.That(hover(0f), Is.EqualTo(1f));
            Assert.That(hover(1f), Is.EqualTo(1.3f).Within(0.001f));
            Assert.That(exit(0f), Is.EqualTo(1.3f).Within(0.001f));
            Assert.That(exit(1f), Is.EqualTo(1f));
        }

        [Test]
        public void UIImageBrightness_ClearAndReleaseRestoreCustomMaterial()
        {
            Image image = Image();
            var original = new Material(image.defaultMaterial) { color = new Color(0.7f, 0.8f, 0.9f, 0.6f) };
            _created.Add(original);
            image.material = original;
            var brightness = new UIImageBrightness(image);
            try
            {
                brightness.Set(1.3f);
                Assert.That(image.materialForRendering.color.r, Is.EqualTo(0.91f).Within(0.001f));
                Assert.That(image.materialForRendering.color.a, Is.EqualTo(0.6f).Within(0.001f));
                brightness.Clear();
                Assert.That(image.material, Is.SameAs(original));
                brightness.Set(1.3f);
            }
            finally { brightness.Release(); }
            Assert.That(image.material, Is.SameAs(original));
            Assert.That(original.color.r, Is.EqualTo(0.7f).Within(0.001f));
        }
    }
}
