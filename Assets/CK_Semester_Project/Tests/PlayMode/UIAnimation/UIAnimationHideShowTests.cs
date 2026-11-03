using System;
using System.Collections;
using System.Collections.Generic;
using CK.SemesterProject.UIAnimation.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CK.SemesterProject.UIAnimation.Tests
{
    public sealed class UIAnimationHideShowTests
    {
        private const string Folder = "Assets/__UIAnimationHideShowPlayTests";
        private GameObject _canvas;
        private GameObject _events;
        private UnityEngine.UI.Image _image;
        private UIAnimationPreset _preset;
        private UIAnimationPlayer _player;
        private Animator _animator;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(Canvas));
            _events = new GameObject("EventSystem", typeof(EventSystem));
            var target = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            target.transform.SetParent(_canvas.transform, false);
            _image = target.GetComponent<UnityEngine.UI.Image>();
            _preset = UIAnimationGenerator.Create(_image, Folder, "Panel", UIPointerEffectMask.All, preset =>
            {
                preset.ApplyStyle(UIAnimationStyle.Fade);
                preset.ApplyStayStyle(UIStayStyle.Still);
                preset.SetHideShowEnabled(true);
                preset.Get(UIAnimationState.Open).Time = 0.12f;
                preset.Get(UIAnimationState.Hide).Time = 0.16f;
                preset.Get(UIAnimationState.Show).Time = 0.14f;
            });
            _player = target.GetComponent<UIAnimationPlayer>();
            _animator = target.GetComponent<Animator>();
            Restart();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            UnityEngine.Object.DestroyImmediate(_canvas);
            UnityEngine.Object.DestroyImmediate(_events);
            AssetDatabase.DeleteAsset(Folder);
        }

        private void Restart()
        {
            _image.gameObject.SetActive(false);
            _image.gameObject.SetActive(true);
        }

        private static IEnumerator Wait(Func<bool> condition)
        {
            float end = Time.realtimeSinceStartup + 4f;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, "제한 시간 안에 상태가 끝나지 않았습니다.");
        }

        private IEnumerator Stay()
        {
            yield return Wait(() => !_animator.enabled);
            Assert.That(_image.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator ButtonStateChangedWhileHidden_IsKeptAfterShow()
        {
            var child = new GameObject("Skill", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button));
            child.transform.SetParent(_image.transform, false);
            UnityEngine.UI.Button button = child.GetComponent<UnityEngine.UI.Button>();
            yield return Stay();
            bool hiddenDone = false, shownDone = false;
            _player.HideFinished.AddListener(() => hiddenDone = true);
            _player.ShowFinished.AddListener(() => shownDone = true);
            _player.Hide();
            Assert.That(button.IsInteractable(), Is.False);
            yield return Wait(() => hiddenDone);
            // 스킬을 쓰는 동안 게임이 쿨타임 때문에 버튼을 끈다.
            button.interactable = false;
            _player.Show();
            yield return Wait(() => shownDone);
            Assert.That(button.interactable, Is.False, "숨김 중에 게임이 바꾼 값을 Show가 덮어쓰면 안 됩니다.");
            Assert.That(button.IsInteractable(), Is.False);
            button.interactable = true;
            Assert.That(button.IsInteractable(), Is.True);
        }

        [UnityTest]
        public IEnumerator Hide_BeforeFirstFrame_UsesGraph_AndFinishesOnlyAfterAnimation()
        {
            int hidden = 0, shown = 0;
            _player.HideFinished.AddListener(() => hidden++);
            _player.ShowFinished.AddListener(() => shown++);
            _player.Hide();
            Assert.That(_player.IsHidden, Is.True);
            Assert.That(_animator.enabled, Is.True);
            Assert.That(hidden, Is.Zero, "첫 프레임도 즉시 숨김으로 우회하지 않습니다.");
            yield return Wait(() => hidden == 1);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Hide"), Is.True);
            Assert.That(_image.color.a, Is.EqualTo(0f).Within(0.01f));
            _player.Show();
            Assert.That(shown, Is.Zero);
            yield return Wait(() => shown == 1);
            yield return Stay();
            Assert.That(_image.color.a, Is.EqualTo(1f).Within(0.01f));
            Assert.That(_image.GetComponent<CanvasGroup>().interactable, Is.True);
        }

        [Test]
        public void HideShowGraph_BlendsCompleteWhileTheRequestStaysSet()
        {
            _animator.Play("Stay", 0, 0f);
            _animator.Update(0f);
            _player.Hide();
            _animator.Update(0.01f);
            Assert.That(_animator.IsInTransition(0), Is.True, "숨김은 현재 모습에서 블렌드합니다.");
            for (int frame = 0; frame < 60; frame++) _animator.Update(0.02f);
            Assert.That(_animator.GetBool("IsHidden"), Is.True);
            Assert.That(_animator.IsInTransition(0), Is.False, "같은 숨김 요청 때문에 블렌드가 재시작되면 안 됩니다.");
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Hide"), Is.True);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(1f));

            _player.Show();
            _animator.Update(0.01f);
            Assert.That(_animator.IsInTransition(0), Is.True);
            for (int frame = 0; frame < 60; frame++) _animator.Update(0.02f);
            Assert.That(_animator.GetBool("IsHidden"), Is.False);
            Assert.That(_animator.IsInTransition(0), Is.False);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Stay"), Is.True);
            Assert.That(_image.color.a, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator HideShow_HoldsHiddenPose_KeepsObjectAlive_AndReturnsToParkedStay_WithOrderedEvents()
        {
            yield return Stay();
            var calls = new List<string>();
            _player.HideStarted.AddListener(() => calls.Add("Hide 시작"));
            _player.HideFinished.AddListener(() => calls.Add("Hide 끝"));
            _player.ShowStarted.AddListener(() => calls.Add("Show 시작"));
            _player.ShowFinished.AddListener(() => calls.Add("Show 끝"));
            _preset.Get(UIAnimationState.Hide).KeepEnabled = false;
            _preset.Get(UIAnimationState.Show).KeepEnabled = false;
            UIAnimationGenerator.Rebuild(_preset);
            _player.Hide();
            Assert.That(_player.IsHidden, Is.True);
            Assert.That(_animator.enabled, Is.True);
            yield return Wait(() => calls.Contains("Hide 끝"));
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Hide"), Is.True);
            float alpha = _image.color.a;
            Vector3 scale = _image.rectTransform.localScale;
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(_image.gameObject.activeSelf, Is.True);
            Assert.That(_image.color.a, Is.EqualTo(alpha).Within(0.001f));
            Assert.That(alpha, Is.EqualTo(0f).Within(0.01f));
            Assert.That(_image.rectTransform.localScale, Is.EqualTo(scale));
            Assert.That(calls, Is.EqualTo(new[] { "Hide 시작", "Hide 끝" }));
            _player.Show();
            Assert.That(_player.IsHidden, Is.False);
            yield return Wait(() => calls.Contains("Show 끝"));
            yield return Stay();
            Assert.That(_image.color.a, Is.EqualTo(1f).Within(0.01f));
            Assert.That(calls, Is.EqualTo(new[] { "Hide 시작", "Hide 끝", "Show 시작", "Show 끝" }));
        }

        [UnityTest]
        public IEnumerator HiddenInput_BlocksChildClickHoverAndNavigation_ThenRestoresOriginalSettingsAndSelection()
        {
            var child = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(CanvasGroup));
            child.transform.SetParent(_image.transform, false);
            UnityEngine.UI.Button button = child.GetComponent<UnityEngine.UI.Button>();
            CanvasGroup childGroup = child.GetComponent<CanvasGroup>();
            childGroup.ignoreParentGroups = true;
            UIAnimationPreset childPreset = UIAnimationGenerator.Create(child.GetComponent<UnityEngine.UI.Image>(), Folder, "Child", UIPointerEffectMask.All);
            UIAnimationPointerEffects effects = child.GetComponent<UIAnimationPointerEffects>();
            int clicks = 0, hovered = 0, pressed = 0;
            button.onClick.AddListener(() => clicks++);
            effects.Trigger(UIPointerEffect.Hover).AddListener(() => hovered++);
            effects.Trigger(UIPointerEffect.Pressed).AddListener(() => pressed++);
            var disabled = new GameObject("Disabled", typeof(RectTransform), typeof(UnityEngine.UI.Button));
            disabled.transform.SetParent(_image.transform, false);
            disabled.GetComponent<UnityEngine.UI.Button>().interactable = false;
            CanvasGroup group = _image.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            yield return Stay();
            EventSystem.current.SetSelectedGameObject(child);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(hovered, Is.EqualTo(1));
            bool hiddenDone = false, shownDone = false;
            _player.HideFinished.AddListener(() => hiddenDone = true);
            _player.ShowFinished.AddListener(() => shownDone = true);
            _player.Hide();
            Assert.That(button.IsInteractable(), Is.False);
            Assert.That(group.interactable, Is.False);
            Assert.That(childGroup.blocksRaycasts, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(child, pointer, ExecuteEvents.pointerClickHandler);
            effects.OnPointerEnter(pointer);
            effects.OnSubmit(new BaseEventData(EventSystem.current));
            yield return Wait(() => hiddenDone);
            Assert.That(clicks, Is.Zero);
            Assert.That(pressed, Is.Zero);
            Assert.That(hovered, Is.EqualTo(1));
            Assert.That(child.transform.localScale.x, Is.EqualTo(childPreset.BaseScale.x).Within(0.001f));
            _player.Show();
            Assert.That(button.IsInteractable(), Is.False, "Show가 끝날 때까지 입력을 막습니다.");
            yield return Wait(() => shownDone);
            Assert.That(group.interactable, Is.True);
            Assert.That(group.blocksRaycasts, Is.False, "원래 꺼 둔 값도 그대로 복구합니다.");
            Assert.That(childGroup.blocksRaycasts, Is.True);
            Assert.That(button.interactable, Is.True);
            Assert.That(disabled.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(child));
            ExecuteEvents.Execute(child, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(clicks, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(child.transform.localScale.x, Is.GreaterThan(childPreset.BaseScale.x));
        }

        [UnityTest]
        public IEnumerator CloseDuringHide_InterruptsAndClearsHiddenState_ThenOpenStartsVisible()
        {
            yield return Stay();
            _preset.Get(UIAnimationState.Hide).Time = 1f;
            UIAnimationGenerator.Rebuild(_preset);
            int hideFinished = 0, showStarted = 0;
            _player.HideFinished.AddListener(() => hideFinished++);
            _player.ShowStarted.AddListener(() => showStarted++);
            _player.Hide();
            yield return new WaitForSecondsRealtime(0.05f);
            _player.Close();
            Assert.That(_player.IsHidden, Is.False);
            yield return Wait(() => !_image.gameObject.activeSelf);
            Assert.That(hideFinished, Is.Zero);
            Assert.That(showStarted, Is.Zero);
            _player.Open();
            yield return Stay();
            Assert.That(_image.color.a, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator HideDuringShow_InterruptsBlend_AndDoesNotFinishInterruptedShow()
        {
            yield return Stay();
            _preset.Get(UIAnimationState.Show).Time = 1f;
            UIAnimationGenerator.Rebuild(_preset);
            int hidden = 0, shown = 0;
            _player.HideFinished.AddListener(() => hidden++);
            _player.ShowFinished.AddListener(() => shown++);
            _player.Hide();
            yield return Wait(() => hidden == 1);
            _player.Show();
            yield return new WaitForSecondsRealtime(0.05f);
            float before = _image.color.a;
            _player.Hide();
            _animator.Update(0.001f);
            Assert.That(Mathf.Abs(_image.color.a - before), Is.LessThan(0.1f));
            yield return Wait(() => hidden == 2);
            Assert.That(shown, Is.Zero);
            Assert.That(_image.gameObject.activeSelf, Is.True);
            _player.Show();
            yield return Wait(() => shown == 1);
        }

        [UnityTest]
        public IEnumerator HideDuringOpen_UsesCurrentAppearance_AndShowRestartsStayLoop()
        {
            _preset.Get(UIAnimationState.Open).Time = 1f;
            _preset.ApplyStayStyle(UIStayStyle.Float);
            _preset.Get(UIAnimationState.Stay).Time = 0.4f;
            UIAnimationGenerator.Rebuild(_preset);
            Restart();
            yield return null;
            yield return null;
            _animator.Play("Open", 0, 0.1f);
            _animator.Update(0f);
            float before = _image.color.a;
            bool hidden = false, shown = false;
            _player.HideFinished.AddListener(() => hidden = true);
            _player.ShowFinished.AddListener(() => shown = true);
            _player.Hide();
            _animator.Update(0.001f);
            Assert.That(Mathf.Abs(_image.color.a - before), Is.LessThan(0.1f));
            yield return Wait(() => hidden);
            Vector2 position = _image.rectTransform.anchoredPosition;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(_image.rectTransform.anchoredPosition, Is.EqualTo(position));
            _player.Show();
            yield return Wait(() => shown);
            Assert.That(_animator.enabled, Is.True);
            float time = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(time));
        }

        [UnityTest]
        public IEnumerator DisabledObject_HideIsNoOp_AndDoesNotCarryOverToOpen()
        {
            yield return Stay();
            int started = 0;
            _player.HideStarted.AddListener(() => started++);
            _image.gameObject.SetActive(false);
            _player.Hide();
            Assert.That(_player.IsHidden, Is.False);
            Assert.That(started, Is.Zero);
            _player.Open();
            yield return Stay();
            _canvas.SetActive(false);
            _player.Hide();
            Assert.That(_player.IsHidden, Is.False);
            _canvas.SetActive(true);
            yield return Stay();
            Assert.That(_image.color.a, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator OptionalStatesOff_InstantHideShow_PreservesAlphaInputAndParkedAnimator()
        {
            _preset.SetHideShowEnabled(false);
            UIAnimationGenerator.Rebuild(_preset);
            Restart();
            yield return Stay();
            CanvasGroup group = _image.GetComponent<CanvasGroup>();
            group.alpha = 0.63f;
            group.interactable = false;
            var calls = new List<string>();
            _player.HideStarted.AddListener(() => calls.Add("Hide 시작"));
            _player.HideFinished.AddListener(() => calls.Add("Hide 끝"));
            _player.ShowStarted.AddListener(() => calls.Add("Show 시작"));
            _player.ShowFinished.AddListener(() => calls.Add("Show 끝"));
            _player.Hide();
            _player.Hide();
            Assert.That(_player.IsHidden, Is.True);
            Assert.That(group.alpha, Is.Zero);
            Assert.That(_image.gameObject.activeSelf, Is.True);
            yield return new WaitForSecondsRealtime(0.1f);
            _player.Show();
            _player.Show();
            Assert.That(_player.IsHidden, Is.False);
            Assert.That(group.alpha, Is.EqualTo(0.63f));
            Assert.That(group.interactable, Is.False);
            Assert.That(_animator.enabled, Is.False);
            Assert.That(calls, Is.EqualTo(new[] { "Hide 시작", "Hide 끝", "Show 시작", "Show 끝" }));
            _player.Hide();
            _player.Close();
            Assert.That(group.alpha, Is.EqualTo(0.63f));
            yield return Wait(() => !_image.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator CloseDuringShow_InterruptsWithoutShowFinished_AndRestoresInput()
        {
            yield return Stay();
            _preset.Get(UIAnimationState.Show).Time = 1f;
            UIAnimationGenerator.Rebuild(_preset);
            bool hidden = false;
            int shown = 0;
            _player.HideFinished.AddListener(() => hidden = true);
            _player.ShowFinished.AddListener(() => shown++);
            _player.Hide();
            yield return Wait(() => hidden);
            _player.Show();
            yield return new WaitForSecondsRealtime(0.05f);
            _player.Close();
            yield return Wait(() => !_image.gameObject.activeSelf);
            Assert.That(_player.IsHidden, Is.False);
            Assert.That(shown, Is.Zero);
            Assert.That(_image.GetComponent<CanvasGroup>().interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator OptionalStatesOff_AddsMissingCanvasGroup_AndSuspendsMovingStayUntilShow()
        {
            _preset.SetHideShowEnabled(false);
            _preset.ApplyStayStyle(UIStayStyle.Float);
            _preset.Get(UIAnimationState.Stay).Time = 0.8f;
            UIAnimationGenerator.Rebuild(_preset);
            UnityEngine.Object.DestroyImmediate(_image.GetComponent<CanvasGroup>());
            Restart();
            yield return Wait(() => _animator.GetCurrentAnimatorStateInfo(0).IsName("Stay"));
            yield return null;
            _player.Hide();
            Assert.That(_image.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(_animator.enabled, Is.False);
            Vector2 hidden = _image.rectTransform.anchoredPosition;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(_image.rectTransform.anchoredPosition, Is.EqualTo(hidden));
            _player.Show();
            Assert.That(_animator.enabled, Is.True);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That((_image.rectTransform.anchoredPosition - hidden).sqrMagnitude, Is.GreaterThan(0.0001f));
            Assert.That(_image.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator OpenClearsHidden_AndReentrantEventsCanShowAndHideAgain()
        {
            yield return Stay();
            int hidden = 0, shown = 0;
            _player.HideFinished.AddListener(() => hidden++);
            _player.ShowFinished.AddListener(() => shown++);
            _player.Hide();
            yield return Wait(() => hidden == 1);
            _player.Open();
            Assert.That(_player.IsHidden, Is.False);
            yield return Wait(() => shown == 1);
            yield return Stay();
            _player.HideFinished.AddListener(_player.Show);
            _player.Hide();
            yield return Wait(() => shown == 2);
            yield return Stay();
            Assert.That(_image.gameObject.activeSelf, Is.True);
            Assert.That(_image.GetComponent<CanvasGroup>().interactable, Is.True);
        }
    }
}
