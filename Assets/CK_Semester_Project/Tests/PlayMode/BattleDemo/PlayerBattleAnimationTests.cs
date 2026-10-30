#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CK.SemesterProject.Battle.Tests
{
    // 실제 Fuchsia 프리팹과 FuchsiaLocomotion Controller로 전투 모션 FSM(전투 대기 · 궁극기 · 전투 종료)을 확인한다.
    public sealed class PlayerBattleAnimationTests
    {
        private const string VisualPrefabPath =
            "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Prefabs/FuchsiaUpdatedAnimated.prefab";
        private const string UltClipPath =
            "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Animations/FuchsiaUlt.anim";
        private const string UltSourcePath = "Assets/FuchsiaAnimation/Ult.fbx";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        // 상태 전환(0.2초 페이드)이 끝날 만큼 Animator를 직접 진행한다.
        private const float SettleSeconds = .5f;
        private GameObject _root;
        private Behaviour _movement;
        private Component _battle;
        private Animator _animator;
        private Transform _pelvisRoot;

        // Tutorial scripts belong to Assembly-CSharp; this asmdef accesses their runtime types.
        [UnitySetUp]
        public IEnumerator CreatePlayer()
        {
            Type movementType = Type.GetType("CK.SemesterProject.Tutorial.PlayerMovement, Assembly-CSharp");
            Assert.That(movementType, Is.Not.Null);
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            Assert.That(visualPrefab, Is.Not.Null);

            _root = new GameObject("Battle animation test fixture");
            _root.SetActive(false);
            _root.transform.position = Vector3.up * 1000f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Battle animation test floor";
            ground.transform.SetParent(_root.transform, false);
            ground.transform.localPosition = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(30f, 1f, 30f);

            var player = new GameObject("Battle animation player");
            player.transform.SetParent(_root.transform, false);
            player.transform.localPosition = Vector3.up * .02f;
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = .28f;
            controller.center = Vector3.up * .85f;

            GameObject visual = UnityEngine.Object.Instantiate(visualPrefab, player.transform);
            visual.transform.localPosition = new Vector3(0f, -.025f, 0f);
            _animator = visual.GetComponent<Animator>();
            _battle = visual.GetComponent("PlayerBattleAnimation");
            Assert.That(_battle, Is.Not.Null, "Fuchsia prefab must carry PlayerBattleAnimation.");
            _pelvisRoot = visual.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip001");

            _movement = (Behaviour)player.AddComponent(movementType);
            movementType.GetField("_cameraTransform", PrivateInstance).SetValue(_movement, player.transform);

            _root.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
        }

        [TearDown]
        public void DestroyPlayer()
        {
            Time.timeScale = 1f;
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void BattleStartsInBattleIdleAndEndReturnsToFieldIdle()
        {
            BeginBattle();
            AssertAnimation("BattleIdle");

            Invoke("ExitBattle");
            _movement.enabled = true;
            AssertAnimation("Idle");
            Assert.That(BattleState, Is.EqualTo("Field"));
        }

        [Test]
        public void UltimateSkillPlaysUltInPlace()
        {
            BeginBattle();
            float length = PlaySkill("SK10", out bool approachTarget);
            Assert.That(length, Is.EqualTo(8.63f).Within(.05f));
            Assert.That(approachTarget, Is.False, "The ultimate motion carries its own choreography.");
            AssertAnimation("Ult", centered: false);
            // 궁극기는 모션 안에서 몸이 움직이므로, 오프셋을 뺀 시작 자세가 충돌체 위에 있는지만 본다.
            AssetDatabase.LoadAssetAtPath<AnimationClip>(UltClipPath).SampleAnimation(_animator.gameObject, 0f);
            AssertCentered("Ult start pose");
        }

        [Test]
        public void SkillWithoutMotionKeepsBattleIdle()
        {
            BeginBattle();
            float length = PlaySkill("SK00", out bool approachTarget);
            Assert.That(length, Is.Zero);
            Assert.That(approachTarget, Is.True);
            AssertAnimation("BattleIdle");
            Assert.That(TryGetCinematicCamera(out _, out _), Is.False);
        }

        [Test]
        public void UltimateLooksThroughUltCameraWithCleanCutsAndOriginalFraming()
        {
            BeginBattle();
            Assert.That(TryGetCinematicCamera(out _, out _), Is.False, "Battle idle has no cinematic camera.");
            PlaySkill("SK10", out _);
            _animator.Update(.1f);
            Assert.That(TryGetCinematicCamera(out Pose firstShot, out float fieldOfView), Is.True);
            Assert.That(fieldOfView, Is.EqualTo(45f));
            Assert.That(Vector3.Angle(firstShot.forward, _pelvisRoot.position - firstShot.position), Is.LessThan(25f),
                "The first Ult shot must look at Fuchsia.");

            // 원본 FBX는 컷 프레임 사이를 보간하므로, 구도는 첫 숏 한가운데(0.7초)에서 비교한다.
            _animator.Update(.6f);
            TryGetCinematicCamera(out Pose holding, out _);
            AssertSourceFraming(holding, .7f);

            // 첫 컷(26프레임) 직전까지는 첫 숏에 머물고, 컷을 지나면 한 번에 다음 숏으로 넘어간다(두 숏 사이를 훑지 않는다).
            _animator.Update(.15f);
            TryGetCinematicCamera(out Pose beforeCut, out _);
            Assert.That(Vector3.Distance(beforeCut.position, firstShot.position), Is.LessThan(.01f));
            Assert.That(Vector3.Angle(beforeCut.forward, firstShot.forward), Is.LessThan(1f),
                "The camera must not turn toward the next shot before the cut.");
            _animator.Update(.03f);
            TryGetCinematicCamera(out Pose afterCut, out _);
            Assert.That(Vector3.Distance(afterCut.position, firstShot.position), Is.GreaterThan(2f));
        }

        [UnityTest]
        public IEnumerator UltimateReturnsToBattleIdleWhenFinished()
        {
            BeginBattle();
            float length = PlaySkill("SK10", out _);
            Time.timeScale = 8f;
            yield return new WaitForSeconds(length + SettleSeconds);
            Time.timeScale = 1f;
            Assert.That(BattleState, Is.EqualTo("BattleIdle"));
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("BattleIdle"), Is.True);
            Assert.That(TryGetCinematicCamera(out _, out _), Is.False, "The battle camera returns after the ultimate.");
        }

        // 전투 컨트롤러처럼 이동을 끄고 전투 모션으로 들어간다.
        private void BeginBattle()
        {
            _movement.enabled = false;
            Invoke("EnterBattle");
        }

        private void AssertAnimation(string expected, bool centered = true)
        {
            _animator.Update(SettleSeconds);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName(expected), Is.True,
                $"Animator must play the {expected} state.");
            if (centered)
            {
                AssertCentered(expected + " motion");
            }
        }

        // 전투 모션 원본(Attack_idle, Ult)은 Bip001이 41cm 옆에 있어, 오프셋을 뺀 클립을 써야 몸이 충돌체 위에 남는다.
        private void AssertCentered(string label)
        {
            Vector3 offset = _pelvisRoot.position - _movement.transform.position;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(.2f),
                $"{label} must keep the body over the character controller.");
        }

        // 원본 Ult.fbx를 같은 시각으로 샘플링해, 몸(Bip001) 기준 카메라 위치·시선이 같은지 본다.
        // 구운 클립은 몸을 충돌체 위로 옮긴 만큼 카메라도 옮겨야 원본 구도가 남는다.
        private void AssertSourceFraming(Pose cinematic, float time)
        {
            GameObject source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UltSourcePath));
            try
            {
                source.transform.position = _animator.transform.position + Vector3.right * 20f;
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(UltSourcePath).OfType<AnimationClip>()
                    .First(candidate => !candidate.name.StartsWith("__preview__"));
                clip.SampleAnimation(source, time);
                Transform[] nodes = source.GetComponentsInChildren<Transform>(true);
                Transform camera = nodes.First(t => t.name == "Ult_cam");
                Vector3 sourceOffset = camera.position - nodes.First(t => t.name == "Bip001").position;
                Assert.That(Vector3.Distance(cinematic.position - _pelvisRoot.position, sourceOffset), Is.LessThan(.03f),
                    "The Ult camera must keep its original position relative to the body.");
                Assert.That(Vector3.Angle(cinematic.forward, -camera.forward), Is.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private bool TryGetCinematicCamera(out Pose pose, out float fieldOfView)
        {
            object[] arguments = { null, null };
            bool found = (bool)_battle.GetType().GetMethod("TryGetCinematicCamera").Invoke(_battle, arguments);
            pose = (Pose)arguments[0];
            fieldOfView = (float)arguments[1];
            return found;
        }

        private string BattleState => (string)_battle.GetType().GetProperty("CurrentStateName").GetValue(_battle);

        private void Invoke(string method) => _battle.GetType().GetMethod(method).Invoke(_battle, null);

        private float PlaySkill(string skillId, out bool approachTarget)
        {
            object[] arguments = { skillId, null };
            float length = (float)_battle.GetType().GetMethod("PlaySkill").Invoke(_battle, arguments);
            approachTarget = (bool)arguments[1];
            return length;
        }
    }
}
#endif
