using System.Collections;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.Tests
{
    // 프로젝트가 쓰는 Input System UI 모듈을 통과한 마우스 입력으로 이벤트 이펙트를 확인한다.
    public sealed class UIAnimationInputTests
    {
        private const string Folder = "Assets/__UIAnimationInputTests";
        private const float Settle = 0.25f;

        private GameObject _root;
        private GameObject _eventSystem;
        private Mouse _mouse;
        private Image _image;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;

        private Vector3 Scale => _image.rectTransform.localScale;

        [SetUp]
        public void SetUp()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _mouse = InputSystem.AddDevice<Mouse>();

            _eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            _eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            _root = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var target = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(_root.transform, false);
            _image = target.GetComponent<Image>();
            _image.rectTransform.sizeDelta = new Vector2(200f, 200f);
            UIAnimationGenerator.Create(_image, Folder, "Panel", UIPointerEffectMask.All);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root);
            Object.Destroy(_eventSystem);
            InputSystem.RemoveDevice(_mouse);
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
            AssetDatabase.DeleteAsset(Folder);
        }

        [UnityTest]
        public IEnumerator Mouse_HoverPressReleaseAndLeave()
        {
            Vector2 inside = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            yield return Send(Vector2.one, false);
            yield return new WaitForSecondsRealtime(0.5f);

            yield return Send(inside, false);
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.002f), "마우스를 올리면 Hover");

            yield return Send(inside, true);
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(0.96f).Within(0.002f), "누르면 Pressed");

            yield return Send(inside, false);
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.002f), "놓으면 다시 Hover");

            yield return Send(Vector2.one, false);
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.002f), "나가면 Exit");
        }

        [UnityTest]
        public IEnumerator Mouse_DraggedInWhileHeld_PlaysHover()
        {
            Vector2 inside = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            yield return Send(Vector2.one, false);
            yield return new WaitForSecondsRealtime(0.5f);

            yield return Send(Vector2.one, true);
            yield return Send(inside, true);
            yield return new WaitForSecondsRealtime(Settle);

            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.002f), "버튼을 누른 채 끌고 들어와도 Hover");
            yield return Send(inside, false);
        }

        private IEnumerator Send(Vector2 position, bool held)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position, buttons = (ushort)(held ? 1 : 0) });
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mouse_TriggersOncePerInput_ThroughInputSystem()
        {
            var calls = new System.Collections.Generic.List<string>();
            var effects = _image.GetComponent<UIAnimationPointerEffects>();
            effects.Trigger(UIPointerEffect.Hover).AddListener(() => calls.Add("Hover"));
            effects.Trigger(UIPointerEffect.Pressed).AddListener(() => calls.Add("Pressed"));
            effects.Trigger(UIPointerEffect.Exit).AddListener(() => calls.Add("Exit"));
            yield return Send(Vector2.one, false);
            yield return new WaitForSecondsRealtime(0.5f);
            calls.Clear();
            Vector2 inside = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            yield return Send(inside, false);
            yield return Send(inside, true);
            yield return Send(inside, false);
            yield return Send(Vector2.one, false);
            Assert.That(calls, Is.EqualTo(new[] { "Hover", "Pressed", "Exit" }));
        }
    }
}
