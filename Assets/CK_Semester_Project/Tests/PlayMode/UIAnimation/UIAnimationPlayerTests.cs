using System;
using System.Collections;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationPlayerTests
    {
        private const float Settle = 0.2f;

        private static int s_runs;

        private string _folder;
        private GameObject _canvas;
        private Image _image;
        private UIAnimationPlayer _player;
        private UIAnimationPointerEffects _effects;
        private Animator _animator;
        private UIAnimationPreset _preset;

        private Vector3 Scale => _image.rectTransform.localScale;
        private float Alpha => _image.color.a;
        private bool IsParked => !_animator.enabled;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(Canvas));
            var target = new GameObject("OptionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(_canvas.transform, false);
            _image = target.GetComponent<Image>();
            _folder = $"Assets/__UIAnimationPlayModeTests{++s_runs}";
            _preset = UIAnimationGenerator.Create(_image, _folder, "OptionPanel", UIPointerEffectMask.All);
            _player = target.GetComponent<UIAnimationPlayer>();
            _effects = target.GetComponent<UIAnimationPointerEffects>();
            _animator = target.GetComponent<Animator>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            UnityEngine.Object.Destroy(_canvas);
            AssetDatabase.DeleteAsset(_folder);
        }

        [UnityTest]
        public IEnumerator Enable_PlaysOpen_ThenStopsAnimatorInStay()
        {
            yield return RestartWithSlowOpen();
            Assert.That(Alpha, Is.LessThan(1f), "Open이 시작되면 아직 투명해야 한다");

            yield return WaitUntil(() => IsParked);

            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Close_FromStay_FadesAndTurnsObjectOff()
        {
            yield return WaitUntil(() => IsParked);

            _player.Close();
            Assert.That(_animator.enabled, Is.True);
            yield return WaitUntil(() => !_image.gameObject.activeSelf);

            Assert.That(Alpha, Is.EqualTo(0f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator Open_AfterClose_ReplaysFromOpen()
        {
            yield return WaitUntil(() => IsParked);
            _player.Close();
            yield return WaitUntil(() => !_image.gameObject.activeSelf);

            _player.Open();

            Assert.That(_image.gameObject.activeSelf, Is.True);
            yield return null;
            yield return null;
            Assert.That(Alpha, Is.LessThan(0.9f));
            yield return WaitUntil(() => IsParked);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator SetActive_AfterClose_AlsoReplaysFromOpen()
        {
            yield return WaitUntil(() => IsParked);
            _player.Close();
            yield return WaitUntil(() => !_image.gameObject.activeSelf);

            _image.gameObject.SetActive(true);

            yield return WaitUntil(() => IsParked);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Close_WhileOpening_StillEndsOff()
        {
            yield return RestartWithSlowOpen();

            _player.Close();

            yield return WaitUntil(() => !_image.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Open_WhileClosing_ReturnsToStay()
        {
            yield return WaitUntil(() => IsParked);
            _player.Close();
            yield return null;
            yield return null;

            _player.Open();

            yield return WaitUntil(() => IsParked);
            Assert.That(_image.gameObject.activeSelf, Is.True);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void CloseAndReopenGraph_BlendsCompleteWhileTheRequestStaysSet()
        {
            Restart();
            Assert.That(_preset.HideShowEnabled, Is.False);
            _animator.Play("Open", 0, 0.1f);
            _animator.Update(0f);
            _player.Close();
            _animator.Update(0.01f);
            Assert.That(_animator.IsInTransition(0), Is.True);
            for (int frame = 0; frame < 60; frame++) _animator.Update(0.02f);
            Assert.That(_animator.GetBool("IsOpen"), Is.False);
            Assert.That(_animator.IsInTransition(0), Is.False, "같은 퇴장 요청 때문에 블렌드가 재시작되면 안 됩니다.");
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Close"), Is.True);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(1f));

            _player.Open();
            _animator.Update(0.01f);
            Assert.That(_animator.IsInTransition(0), Is.True);
            for (int frame = 0; frame < 60; frame++) _animator.Update(0.02f);
            Assert.That(_animator.GetBool("IsOpen"), Is.True);
            Assert.That(_animator.IsInTransition(0), Is.False);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Stay"), Is.True);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Close_CalledRepeatedly_DoesNotBreakTheFlow()
        {
            yield return WaitUntil(() => IsParked);

            _player.Close();
            _player.Close();
            yield return null;
            _player.Close();

            yield return WaitUntil(() => !_image.gameObject.activeSelf);
            _player.Close();
            Assert.That(_image.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Open_CalledWhileOpen_DoesNotRestart()
        {
            yield return WaitUntil(() => IsParked);

            _player.Open();

            yield return null;
            Assert.That(IsParked, Is.True);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator AutoClose_LeavesStayAfterItsTime()
        {
            UIAnimationStateSettings stay = _preset.Get(UIAnimationState.Stay);
            stay.AutoClose = true;
            stay.Time = 0.2f;
            UIAnimationGenerator.Rebuild(_preset);
            Restart();

            yield return WaitUntil(() => !_image.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator KeepEnabled_StaysOnAfterClose_AndCanReopen()
        {
            _preset.Get(UIAnimationState.Close).KeepEnabled = true;
            UIAnimationGenerator.Rebuild(_preset);
            yield return WaitUntil(() => IsParked);

            _player.Close();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(_image.gameObject.activeSelf, Is.True);
            Assert.That(Alpha, Is.EqualTo(0f).Within(0.02f));

            _player.Open();
            yield return WaitUntil(() => IsParked);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator AnimatedStay_KeepsAnimatorRunning()
        {
            _preset.Get(UIAnimationState.Stay).Scale.Set(Vector2.one, Vector2.one * 1.2f, UIEase.Linear);
            UIAnimationGenerator.Rebuild(_preset);
            Restart();

            yield return new WaitForSecondsRealtime(0.6f);

            Assert.That(IsParked, Is.False);
            Assert.That(Scale.x, Is.GreaterThan(1.01f));
        }

        [UnityTest]
        public IEnumerator TimeScaleZero_StillAnimates()
        {
            Time.timeScale = 0f;

            yield return WaitUntil(() => IsParked);
            _player.Close();
            yield return WaitUntil(() => !_image.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Speed_ShortensPlayback()
        {
            _preset.Get(UIAnimationState.Open).Time = 2f;
            _preset.Get(UIAnimationState.Open).Speed = 4f;
            UIAnimationGenerator.Rebuild(_preset);
            Restart();

            float start = Time.realtimeSinceStartup;
            yield return WaitUntil(() => IsParked);

            float elapsed = Time.realtimeSinceStartup - start;
            Assert.That(elapsed, Is.InRange(0.4f, 0.75f));
        }

        [UnityTest]
        public IEnumerator Hover_ScalesOnTopOfStay_AndExitRestores()
        {
            yield return WaitUntil(() => IsParked);

            _effects.OnPointerEnter(Pointer());
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.001f));

            _effects.OnPointerExit(Pointer());
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Hover_DuringOpen_MultipliesAnimatedScale()
        {
            yield return RestartWithSlowOpen();
            _effects.OnPointerEnter(Pointer());

            yield return WaitUntil(() => IsParked);
            yield return new WaitForSecondsRealtime(Settle);

            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Pressed_TintsAndShrinks_ThenReturnsToHover()
        {
            yield return WaitUntil(() => IsParked);
            _effects.OnPointerEnter(Pointer());
            yield return new WaitForSecondsRealtime(Settle);

            _effects.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(0.96f).Within(0.001f));
            Assert.That(_image.canvasRenderer.GetColor().r, Is.EqualTo(0.85f).Within(0.01f));

            _effects.OnPointerUp(Pointer());
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.001f));
            Assert.That(_image.canvasRenderer.GetColor().r, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Release_AfterDraggingOut_ReturnsToNormal()
        {
            yield return WaitUntil(() => IsParked);
            _effects.OnPointerEnter(Pointer());
            _effects.OnPointerDown(Pointer());
            _effects.OnPointerExit(Pointer());
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(0.96f).Within(0.001f), "누른 채로 나가도 놓을 때까지 눌린 모양을 유지한다");

            _effects.OnPointerUp(Pointer());
            yield return new WaitForSecondsRealtime(Settle);

            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator DraggedIn_PlaysHover()
        {
            yield return WaitUntil(() => IsParked);
            PointerEventData dragging = Pointer();
            dragging.dragging = true;
            dragging.eligibleForClick = false;

            _effects.OnPointerEnter(dragging);
            yield return new WaitForSecondsRealtime(Settle);

            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator DisabledEffect_DoesNothing()
        {
            _preset.Get(UIPointerEffect.Hover).Enabled = false;
            yield return WaitUntil(() => IsParked);

            _effects.OnPointerEnter(Pointer());
            yield return new WaitForSecondsRealtime(Settle);

            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator DisabledExit_SnapsBackInsteadOfStaying()
        {
            _preset.Get(UIPointerEffect.Exit).Enabled = false;
            yield return WaitUntil(() => IsParked);
            _effects.OnPointerEnter(Pointer());
            yield return new WaitForSecondsRealtime(Settle);

            _effects.OnPointerExit(Pointer());
            yield return null;
            yield return null;

            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator NonInteractableButton_IgnoresPointer()
        {
            Button button = _image.gameObject.AddComponent<Button>();
            button.interactable = false;
            UnityEngine.Object.DestroyImmediate(_effects);
            _effects = _image.gameObject.AddComponent<UIAnimationPointerEffects>();
            yield return WaitUntil(() => IsParked);

            _effects.OnPointerEnter(Pointer());
            yield return new WaitForSecondsRealtime(Settle);

            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Close_WhileHovered_IgnoresPointerUntilReopened()
        {
            yield return WaitUntil(() => IsParked);
            _effects.OnPointerEnter(Pointer());
            yield return new WaitForSecondsRealtime(Settle);

            _player.Close();
            _effects.OnPointerEnter(Pointer());
            yield return WaitUntil(() => !_image.gameObject.activeSelf);

            _player.Open();
            yield return WaitUntil(() => IsParked);
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f), "다시 열려도 이전 Hover 배율이 남으면 안 된다");
        }

        [UnityTest]
        public IEnumerator Position_SlidesInAndEndsAtOriginalPlace()
        {
            _image.rectTransform.anchoredPosition = new Vector2(50f, 60f);
            _preset.Get(UIAnimationState.Open).Offset.Set(new Vector2(-100f, 0f), Vector2.zero, UIEase.Linear);
            UIAnimationGenerator.Rebuild(_preset);

            yield return RestartWithSlowOpen();
            Assert.That(_image.rectTransform.anchoredPosition.x, Is.LessThan(40f), "Open 도중에는 시작 쪽에 있어야 한다");

            yield return WaitUntil(() => IsParked);
            yield return null;
            Assert.That(_image.rectTransform.anchoredPosition.x, Is.EqualTo(50f).Within(0.01f));
            Assert.That(_image.rectTransform.anchoredPosition.y, Is.EqualTo(60f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator SharedClip_MovesEachImageFromItsOwnPlace()
        {
            _preset.Get(UIAnimationState.Open).Offset.Set(new Vector2(0f, -40f), Vector2.zero, UIEase.Linear);
            UIAnimationGenerator.Rebuild(_preset);
            var other = new GameObject("Other", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            other.transform.SetParent(_canvas.transform, false);
            other.GetComponent<RectTransform>().anchoredPosition = new Vector2(300f, 200f);
            UIAnimationGenerator.Connect(other.GetComponent<Image>(), _preset);
            _image.rectTransform.anchoredPosition = new Vector2(-300f, -200f);
            Restart();

            yield return WaitUntil(() => IsParked && !other.GetComponent<Animator>().enabled);
            yield return null;

            Assert.That(other.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(300f, 200f)));
            Assert.That(_image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(-300f, -200f)));
        }

        [UnityTest]
        public IEnumerator Rotation_ReturnsToOriginalAngle()
        {
            _preset.Get(UIAnimationState.Open).Rotation.Set(-8f, 0f, UIEase.Linear);
            UIAnimationGenerator.Rebuild(_preset);

            yield return RestartWithSlowOpen();
            Assert.That(Mathf.DeltaAngle(0f, _image.rectTransform.localEulerAngles.z), Is.LessThan(-1f));

            yield return WaitUntil(() => IsParked);
            yield return null;
            Assert.That(Mathf.DeltaAngle(0f, _image.rectTransform.localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator PositionIsRestored_WhenClosedAwayFromHome()
        {
            _image.rectTransform.anchoredPosition = new Vector2(50f, 60f);
            _preset.Get(UIAnimationState.Close).Offset.Set(Vector2.zero, new Vector2(200f, 0f), UIEase.Linear);
            UIAnimationGenerator.Rebuild(_preset);
            Restart();
            yield return WaitUntil(() => IsParked);

            _player.Close();
            yield return WaitUntil(() => !_image.gameObject.activeSelf);

            Assert.That(_image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(50f, 60f)), "다시 열릴 때 기준 자리가 밀리면 안 된다");
        }

        [UnityTest]
        public IEnumerator OpenDelay_HoldsTheFirstFrameBeforePlaying()
        {
            SetDelays(_player, 0.3f, 0f);
            _preset.Get(UIAnimationState.Open).Time = 0.1f;
            UIAnimationGenerator.Rebuild(_preset);
            Restart();

            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Alpha, Is.EqualTo(0f).Within(0.01f), "지연 중에는 시작 모습으로 기다린다");

            yield return WaitUntil(() => IsParked);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator CloseDelay_WaitsBeforeClosing()
        {
            SetDelays(_player, 0f, 0.3f);
            yield return WaitUntil(() => IsParked);

            _player.Close();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.01f));

            yield return WaitUntil(() => !_image.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Open_DuringCloseDelay_CancelsTheClose()
        {
            SetDelays(_player, 0f, 0.3f);
            yield return WaitUntil(() => IsParked);

            _player.Close();
            yield return null;
            _player.Open();
            yield return new WaitForSecondsRealtime(0.6f);

            Assert.That(_image.gameObject.activeSelf, Is.True);
            Assert.That(Alpha, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Stagger_ChildrenEnterInOrder_LeaveWithParent_AndComeBack()
        {
            Image first = MakeChild("First");
            Image second = MakeChild("Second");
            UIAnimationPreset shared = UIAnimationGenerator.Create(first, _folder, "Child", UIPointerEffectMask.None);
            shared.Get(UIAnimationState.Open).Time = 0.1f;
            shared.Get(UIAnimationState.Close).Time = 0.1f;
            UIAnimationGenerator.Rebuild(shared);
            UIAnimationGenerator.Connect(second, shared);
            UIAnimationGenerator.ApplyStagger(_player, 0.4f, false);
            Restart();

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(first.color.a, Is.GreaterThan(0.5f), "첫째는 이미 나왔다");
            Assert.That(second.color.a, Is.LessThan(0.1f), "둘째는 아직 기다린다");
            yield return WaitUntil(() => !second.GetComponent<Animator>().enabled);

            _player.Close();
            yield return WaitUntil(() => !_image.gameObject.activeSelf);
            Assert.That(first.gameObject.activeSelf, Is.False);

            _player.Open();
            Assert.That(first.gameObject.activeSelf, Is.True, "부모가 다시 열리면 자식도 돌아온다");
            yield return WaitUntil(() => !second.GetComponent<Animator>().enabled);
            Assert.That(second.color.a, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator Nudge_MovesOnHoverAndReturns()
        {
            _preset.ApplyPointerStyle(UIPointerStyle.Nudge);
            _image.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            yield return WaitUntil(() => IsParked);

            _effects.OnPointerEnter(Pointer());
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(_image.rectTransform.anchoredPosition.x, Is.EqualTo(24f).Within(0.01f));

            _effects.OnPointerExit(Pointer());
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(_image.rectTransform.anchoredPosition.x, Is.EqualTo(10f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator KeyboardSelection_PlaysHover_AndSubmitPlaysPressed()
        {
            yield return WaitUntil(() => IsParked);

            _effects.OnSelect(new BaseEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.001f));

            _effects.OnSubmit(new BaseEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(0.03f);
            Assert.That(Scale.x, Is.LessThan(1.04f));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(Scale.x, Is.EqualTo(1.04f).Within(0.001f), "누른 뒤에는 고른 모습으로 돌아온다");

            _effects.OnDeselect(new BaseEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(Settle);
            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator MouseClickSelection_DoesNotStayHighlighted()
        {
            yield return WaitUntil(() => IsParked);

            _effects.OnPointerEnter(Pointer());
            _effects.OnSelect(Pointer());
            _effects.OnPointerExit(Pointer());
            yield return new WaitForSecondsRealtime(Settle);

            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator SelectionOff_IgnoresKeyboard()
        {
            _preset.ReactToSelection = false;
            yield return WaitUntil(() => IsParked);

            _effects.OnSelect(new BaseEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(Settle);

            Assert.That(Scale.x, Is.EqualTo(1f).Within(0.001f));
        }

        private Image MakeChild(string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(_image.transform, false);
            return child.GetComponent<Image>();
        }

        private static void SetDelays(UIAnimationPlayer player, float open, float close)
        {
            var serialized = new SerializedObject(player);
            serialized.FindProperty("_openDelay").floatValue = open;
            serialized.FindProperty("_closeDelay").floatValue = close;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 준비 단계의 긴 첫 프레임을 지나 다시 켜야 Open 도중을 확인할 수 있다.
        private IEnumerator RestartWithSlowOpen()
        {
            _preset.Get(UIAnimationState.Open).Time = 1f;
            UIAnimationGenerator.Rebuild(_preset);
            Restart();
            yield return null;
            yield return null;
        }

        private void Restart()
        {
            _image.gameObject.SetActive(false);
            _image.gameObject.SetActive(true);
        }

        private static PointerEventData Pointer()
        {
            return new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds = 3f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            Assert.That(condition(), Is.True, "제한 시간 안에 조건이 만족되지 않았다");
        }
    }
}
