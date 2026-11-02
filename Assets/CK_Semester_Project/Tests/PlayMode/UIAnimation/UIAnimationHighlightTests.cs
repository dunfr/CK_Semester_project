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

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationHighlightTests
    {
        private const string Folder = "Assets/__UIHighlightTests";
        private GameObject _root;
        private GameObject _events;
        private Image _image;
        private UIAnimationPreset _preset;
        private UIAnimationPlayer _player;
        private UIAnimationPointerEffects _effects;
        private readonly List<string> _calls = new List<string>();
        private readonly List<UnityEngine.Object> _temporary = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("EventSystem", typeof(EventSystem));
            _root = new GameObject("Canvas", typeof(Canvas));
            var target = new GameObject("Target", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            target.transform.SetParent(_root.transform, false);
            _image = target.GetComponent<Image>();
            target.GetComponent<Button>().transition = Selectable.Transition.None;
            _preset = UIAnimationGenerator.Create(_image, Folder, "Target", UIPointerEffectMask.All);
            _player = target.GetComponent<UIAnimationPlayer>();
            _effects = target.GetComponent<UIAnimationPointerEffects>();
            foreach (UIPointerEffect effect in Enum.GetValues(typeof(UIPointerEffect)))
            {
                string label = effect.ToString();
                _effects.Trigger(effect).AddListener(() => _calls.Add(label));
            }
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_events);
            foreach (UnityEngine.Object item in _temporary) UnityEngine.Object.DestroyImmediate(item);
            _temporary.Clear();
            _calls.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        private PointerEventData Pointer() => new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        private BaseEventData Keyboard() => new BaseEventData(EventSystem.current);
        private IEnumerator Settle() { yield return new WaitForSecondsRealtime(0.3f); }
        private IEnumerator Wait(Func<bool> ready)
        {
            float end = Time.realtimeSinceStartup + 3f;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(ready(), Is.True, "상태 전환 시간 초과");
        }
        private Sprite Sprite()
        {
            var texture = new Texture2D(2, 2);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            _temporary.Add(sprite);
            _temporary.Add(texture);
            return sprite;
        }

        [UnityTest]
        public IEnumerator HoverSprite_ExitRestoresBase()
        {
            _image.sprite = Sprite();
            Sprite original = _image.sprite;
            _preset.Get(UIPointerEffect.Hover).Sprite = Sprite();
            _effects.OnPointerEnter(Pointer());
            Assert.That(_image.overrideSprite, Is.SameAs(_preset.Get(UIPointerEffect.Hover).Sprite));
            _effects.OnPointerExit(Pointer());
            yield return Settle();
            Assert.That(_image.overrideSprite, Is.SameAs(original));
        }

        [UnityTest]
        public IEnumerator PressedWithoutSprite_KeepsHover()
        {
            Sprite hover = _preset.Get(UIPointerEffect.Hover).Sprite = Sprite();
            _effects.OnPointerEnter(Pointer());
            _effects.OnPointerDown(Pointer());
            yield return Settle();
            Assert.That(_image.overrideSprite, Is.SameAs(hover));
        }

        [UnityTest]
        public IEnumerator PressedSprite_ReleaseRestoresHover_ExitRestoresBase()
        {
            Sprite original = _image.sprite = Sprite();
            Sprite hover = _preset.Get(UIPointerEffect.Hover).Sprite = Sprite();
            Sprite pressed = _preset.Get(UIPointerEffect.Pressed).Sprite = Sprite();
            _effects.OnPointerEnter(Pointer());
            _effects.OnPointerDown(Pointer());
            Assert.That(_image.overrideSprite, Is.SameAs(pressed));
            _effects.OnPointerUp(Pointer());
            Assert.That(_image.overrideSprite, Is.SameAs(hover));
            _effects.OnPointerExit(Pointer());
            yield return Settle();
            Assert.That(_image.overrideSprite, Is.SameAs(original));
        }

        [UnityTest]
        public IEnumerator Disable_RestoresSprite()
        {
            Sprite original = _image.sprite = Sprite();
            _preset.Get(UIPointerEffect.Hover).Sprite = Sprite();
            _effects.OnPointerEnter(Pointer());
            _image.gameObject.SetActive(false);
            yield return null;
            Assert.That(_image.overrideSprite, Is.SameAs(original));
        }

        [UnityTest]
        public IEnumerator HighlightColor_AppliesAndReturns()
        {
            _preset.Get(UIPointerEffect.Hover).Color.Set(Color.white, new Color(0.4f, 0.6f, 0.8f), UIEase.Linear);
            _effects.OnPointerEnter(Pointer());
            yield return Settle();
            Assert.That(_image.canvasRenderer.GetColor().r, Is.EqualTo(0.4f).Within(0.01f));
            _effects.OnPointerExit(Pointer());
            yield return Settle();
            Assert.That(_image.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
        }

        [UnityTest]
        public IEnumerator Glow_UsesBrightnessCopy_ExitRestoresMaterial()
        {
            _preset.ApplyPointerStyle(UIPointerStyle.Glow);
            Material original = _image.material;
            _effects.OnPointerEnter(Pointer());
            yield return Settle();
            Assert.That(_image.material, Is.Not.SameAs(original));
            Assert.That(_image.materialForRendering.color.r, Is.EqualTo(original.color.r * 1.3f).Within(0.01f));
            _effects.OnPointerExit(Pointer());
            yield return Settle();
            Assert.That(_image.material, Is.SameAs(original));
        }

        [UnityTest]
        public IEnumerator Disable_RestoresBrightnessMaterial()
        {
            _preset.ApplyPointerStyle(UIPointerStyle.Glow);
            Material original = _image.material;
            _effects.OnPointerEnter(Pointer());
            yield return Settle();
            _image.gameObject.SetActive(false);
            yield return null;
            Assert.That(_image.material, Is.SameAs(original));
            Assert.That(original.color.r, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Triggers_HoverPressedExit_ReleaseDoesNotRaiseHover()
        {
            _effects.OnPointerEnter(Pointer());
            _effects.OnPointerDown(Pointer());
            _effects.OnPointerUp(Pointer());
            _effects.OnPointerExit(Pointer());
            yield return null;
            Assert.That(_calls, Is.EqualTo(new[] { "Hover", "Pressed", "Exit" }));
        }

        [UnityTest]
        public IEnumerator DragOut_RaisesExitOnce()
        {
            _effects.OnPointerEnter(Pointer());
            _effects.OnPointerDown(Pointer());
            _effects.OnPointerExit(Pointer());
            _effects.OnPointerExit(Pointer());
            _effects.OnPointerUp(Pointer());
            yield return null;
            Assert.That(_calls, Is.EqualTo(new[] { "Hover", "Pressed", "Exit" }));
        }

        [UnityTest]
        public IEnumerator DisabledEvents_DoNotInvoke()
        {
            _preset.SetPointerEffects(UIPointerEffectMask.None);
            _effects.OnPointerEnter(Pointer());
            _effects.OnPointerDown(Pointer());
            _effects.OnPointerUp(Pointer());
            _effects.OnPointerExit(Pointer());
            yield return null;
            Assert.That(_calls, Is.Empty);
        }

        [UnityTest]
        public IEnumerator NonInteractableButton_DoesNotInvoke()
        {
            _image.GetComponent<Button>().interactable = false;
            _effects.OnPointerEnter(Pointer());
            _effects.OnPointerDown(Pointer());
            _effects.OnSubmit(Keyboard());
            _effects.OnPointerExit(Pointer());
            yield return null;
            Assert.That(_calls, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Keyboard_SelectSubmitDeselect()
        {
            _effects.OnSelect(Keyboard());
            _effects.OnSubmit(Keyboard());
            yield return Settle();
            _effects.OnDeselect(Keyboard());
            Assert.That(_calls, Is.EqualTo(new[] { "Hover", "Pressed", "Exit" }));
        }

        [UnityTest]
        public IEnumerator MouseAndKeyboard_OverlapRaisesOnePair()
        {
            _effects.OnPointerEnter(Pointer());
            _effects.OnSelect(Keyboard());
            _effects.OnPointerExit(Pointer());
            Assert.That(_calls, Is.EqualTo(new[] { "Hover" }));
            _effects.OnDeselect(Keyboard());
            yield return null;
            Assert.That(_calls, Is.EqualTo(new[] { "Hover", "Exit" }));
        }

        [UnityTest]
        public IEnumerator Close_RaisesExitOnce()
        {
            _effects.OnPointerEnter(Pointer());
            _player.Close();
            yield return Wait(() => !_image.gameObject.activeSelf);
            Assert.That(_calls, Is.EqualTo(new[] { "Hover", "Exit" }));
        }

        [UnityTest]
        public IEnumerator Disable_RaisesExitOnce()
        {
            _effects.OnPointerEnter(Pointer());
            _image.gameObject.SetActive(false);
            yield return null;
            Assert.That(_calls, Is.EqualTo(new[] { "Hover", "Exit" }));
        }

        private void StateCalls()
        {
            _player.OpenStarted.AddListener(() => _calls.Add("OpenStarted"));
            _player.OpenFinished.AddListener(() => _calls.Add("OpenFinished"));
            _player.CloseStarted.AddListener(() => _calls.Add("CloseStarted"));
            _player.CloseFinished.AddListener(() => _calls.Add("CloseFinished"));
        }

        [UnityTest]
        public IEnumerator StateTriggers_RunInOrder()
        {
            _image.gameObject.SetActive(false);
            StateCalls();
            _player.Open();
            yield return Wait(() => _calls.Contains("OpenFinished"));
            _player.Close();
            yield return Wait(() => !_image.gameObject.activeSelf);
            Assert.That(_calls, Is.EqualTo(new[] { "OpenStarted", "OpenFinished", "CloseStarted", "CloseFinished" }));
        }

        [UnityTest]
        public IEnumerator KeepEnabled_CloseFinishedRunsOnce()
        {
            _preset.Get(UIAnimationState.Close).KeepEnabled = true;
            UIAnimationGenerator.Rebuild(_preset);
            StateCalls();
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            _calls.Clear();
            _player.Close();
            yield return Wait(() => _calls.Contains("CloseFinished"));
            yield return Settle();
            Assert.That(_image.gameObject.activeSelf, Is.True);
            Assert.That(_calls, Is.EqualTo(new[] { "CloseStarted", "CloseFinished" }));
        }

        [UnityTest]
        public IEnumerator Delay_PrecedesOpenStarted()
        {
            _image.gameObject.SetActive(false);
            var data = new SerializedObject(_player);
            data.FindProperty("_openDelay").floatValue = 0.25f;
            data.ApplyModifiedPropertiesWithoutUndo();
            StateCalls();
            _player.Open();
            Assert.That(_calls, Is.Empty);
            yield return null;
            Assert.That(_calls, Is.Empty);
            yield return Wait(() => _calls.Contains("OpenStarted"));
        }

        [UnityTest]
        public IEnumerator OpenFinished_CanClose()
        {
            _player.OpenFinished.AddListener(_player.Close);
            // 준비 단계의 긴 첫 프레임에 Open이 먼저 끝날 수 있어 연결한 뒤 다시 켠다.
            _image.gameObject.SetActive(false);
            _image.gameObject.SetActive(true);
            yield return Wait(() => !_image.gameObject.activeSelf);
            Assert.That(_player.IsClosing, Is.True);
        }

        [UnityTest]
        public IEnumerator CloseFinished_CanOpen()
        {
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            _player.CloseFinished.AddListener(_player.Open);
            _player.Close();
            yield return Wait(() => !_player.IsClosing);
            yield return Wait(() => !_image.GetComponent<Animator>().enabled);
            Assert.That(_image.gameObject.activeSelf, Is.True);
            Assert.That(_image.color.a, Is.EqualTo(1f).Within(0.01f));
        }
    }
}
