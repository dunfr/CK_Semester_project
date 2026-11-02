using System.Collections;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationPreviewerTests
    {
        private const string Folder = "Assets/__UIAnimationPreviewTests";

        private GameObject _target;
        private Image _image;
        private UIAnimationPreset _preset;
        private UIAnimationPreviewer _previewer;

        [SetUp]
        public void SetUp()
        {
            _target = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _image = _target.GetComponent<Image>();
            _image.rectTransform.localScale = new Vector3(2f, 2f, 1f);
            _image.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            _preset = UIAnimationGenerator.Create(_image, Folder, "Preview", UIPointerEffectMask.All);
            _preset.Get(UIAnimationState.Open).Time = 30f;
            UIAnimationGenerator.Rebuild(_preset);
            _previewer = new UIAnimationPreviewer();
        }

        [TearDown]
        public void TearDown()
        {
            _previewer.Stop();
            Object.DestroyImmediate(_target);
            AssetDatabase.DeleteAsset(Folder);
        }

        [UnityTest]
        public IEnumerator Stop_RestoresImageAndLeavesAnimationMode()
        {
            AnimationClip clip = UIAnimationGenerator.ClipOf(_preset, UIAnimationState.Open);
            _previewer.Play(new[] { Entry(clip) });
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }
            Assert.That(_previewer.IsPlaying, Is.True);
            Assert.That(AnimationMode.InAnimationMode(), Is.True);
            Assert.That(_image.color.a, Is.LessThan(1f), "재생 중에는 Open 시작값이 보여야 한다");

            _previewer.Stop();

            Assert.That(AnimationMode.InAnimationMode(), Is.False);
            Assert.That(_image.color.a, Is.EqualTo(1f));
            Assert.That(_image.rectTransform.localScale, Is.EqualTo(new Vector3(2f, 2f, 1f)));
        }

        [UnityTest]
        public IEnumerator Play_StopsByItselfAtTheEnd()
        {
            _preset.Get(UIAnimationState.Close).Time = 0.05f;
            UIAnimationGenerator.Rebuild(_preset);
            AnimationClip clip = UIAnimationGenerator.ClipOf(_preset, UIAnimationState.Close);
            bool stopped = false;
            _previewer.Stopped += () => stopped = true;

            _previewer.Play(new[] { Entry(clip) });

            double end = EditorApplication.timeSinceStartup + 3.0;
            while (!stopped && EditorApplication.timeSinceStartup < end)
            {
                yield return null;
            }
            Assert.That(stopped, Is.True);
            Assert.That(_image.enabled, Is.True, "끝에서 꺼진 Enable도 원래대로 돌아와야 한다");
            Assert.That(_image.color.a, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator InactiveImage_ShowsDuringPreview_AndTurnsOffAgain()
        {
            _target.SetActive(false);
            AnimationClip clip = UIAnimationGenerator.ClipOf(_preset, UIAnimationState.Open);

            _previewer.Play(new[] { Entry(clip) });
            yield return null;
            yield return null;
            Assert.That(_target.activeSelf, Is.True);

            _previewer.Stop();
            Assert.That(_target.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DeletedTarget_EndsPreviewCleanly()
        {
            AnimationClip clip = UIAnimationGenerator.ClipOf(_preset, UIAnimationState.Open);
            _previewer.Play(new[] { Entry(clip) });
            yield return null;

            Object.DestroyImmediate(_target);
            yield return null;
            yield return null;

            Assert.That(_previewer.IsPlaying, Is.False);
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
        }

        [UnityTest]
        public IEnumerator PreviewClip_ShowsPositionOnScreenAndRestoresIt()
        {
            _image.rectTransform.anchoredPosition = new Vector2(10f, 20f);
            _preset.Get(UIAnimationState.Open).Offset.Set(new Vector2(100f, 0f), new Vector2(100f, 0f), UIEase.Linear);
            var clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            UIAnimationClipBaker.BakePreview(_preset, UIAnimationState.Open, _image.rectTransform, clip);

            _previewer.Play(new[] { Entry(clip) }, new[] { clip });
            yield return null;
            yield return null;
            Assert.That(_image.rectTransform.anchoredPosition.x, Is.EqualTo(110f).Within(0.01f));

            _previewer.Stop();
            Assert.That(_image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(10f, 20f)));
        }

        [UnityTest]
        public IEnumerator Seek_HoldsTheChosenMoment()
        {
            AnimationClip clip = UIAnimationGenerator.ClipOf(_preset, UIAnimationState.Open);
            _previewer.Play(new[] { Entry(clip) });

            _previewer.Seek(clip.length * 0.5);
            yield return null;
            yield return null;

            Assert.That(_previewer.Paused, Is.True);
            Assert.That(_previewer.Position, Is.EqualTo(clip.length * 0.5).Within(0.0001));
            Assert.That(_image.color.a, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator DelayedEntry_ShowsItsFirstFrameUntilItStarts()
        {
            var other = new GameObject("Other", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            AnimationClip clip = UIAnimationGenerator.ClipOf(_preset, UIAnimationState.Open);
            var entries = new[]
            {
                Entry(clip),
                new UIAnimationPreviewer.Entry(other, clip, 100.0, 1f)
            };

            _previewer.Play(entries);
            yield return null;
            yield return null;

            Assert.That(other.GetComponent<Image>().color.a, Is.EqualTo(0f).Within(0.01f));
            Assert.That(_previewer.Duration, Is.EqualTo(100.0 + clip.length).Within(0.001));
            _previewer.Stop();
            Object.DestroyImmediate(other);
        }

        [Test]
        public void EmptyTimeline_DoesNotStartPreview()
        {
            _previewer.Play(new UIAnimationPreviewer.Entry[0]);

            Assert.That(_previewer.IsPlaying, Is.False);
            Assert.That(AnimationMode.InAnimationMode(), Is.False);
        }

        [Test]
        public void ExitPreview_StartsFromHoverLook()
        {
            var clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };

            UIAnimationClipBaker.BakeEffectPreview(_preset, UIPointerEffect.Exit, _image, clip);

            var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(RectTransform), "m_LocalScale.x");
            AnimationCurve scaleX = AnimationUtility.GetEditorCurve(clip, binding);
            Assert.That(scaleX.Evaluate(0f), Is.EqualTo(2.08f).Within(0.001f));
            Assert.That(scaleX.Evaluate(clip.length), Is.EqualTo(2f).Within(0.001f));
            Object.DestroyImmediate(clip);
        }

        [UnityTest]
        public IEnumerator EffectPreview_LeavesImageUntouched()
        {
            var clip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            UIAnimationClipBaker.BakeEffectPreview(_preset, UIPointerEffect.Pressed, _image, clip);
            Assert.That(clip.length, Is.EqualTo(_preset.Get(UIPointerEffect.Pressed).Time).Within(0.001f));

            _previewer.Play(new[] { Entry(clip) }, new[] { clip });
            yield return null;
            yield return null;
            _previewer.Stop();

            Assert.That(clip == null, Is.True, "임시 클립은 멈출 때 지운다");
            Assert.That(_image.rectTransform.localScale, Is.EqualTo(new Vector3(2f, 2f, 1f)));
            Assert.That(_image.color, Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1f)));
        }

        private UIAnimationPreviewer.Entry Entry(AnimationClip clip)
        {
            return new UIAnimationPreviewer.Entry(_target, clip, 0.0, 1f);
        }
    }
}
