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
    // 실제 Fuchsia 프리팹과 FuchsiaLocomotion Controller로, 이동 상태마다 자기 모션을 재생하는지 확인한다.
    public sealed class PlayerAnimationStateTests
    {
        private const string VisualPrefabPath =
            "Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/Prefabs/FuchsiaUpdatedAnimated.prefab";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        // 상태 전환(0.15초 페이드)이 끝날 만큼 Animator를 직접 진행한다.
        private const float SettleSeconds = .4f;
        private GameObject _root;
        private Behaviour _movement;
        private CharacterController _controller;
        private Animator _animator;
        private Transform _pelvisRoot;
        private MethodInfo _move;

        // Tutorial scripts belong to Assembly-CSharp; this asmdef accesses their runtime type.
        [UnitySetUp]
        public IEnumerator CreatePlayer()
        {
            Type movementType = Type.GetType("CK.SemesterProject.Tutorial.PlayerMovement, Assembly-CSharp");
            Assert.That(movementType, Is.Not.Null);
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            Assert.That(visualPrefab, Is.Not.Null);

            _root = new GameObject("Animation test fixture");
            _root.SetActive(false);
            _root.transform.position = Vector3.up * 1000f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Animation test floor";
            ground.transform.SetParent(_root.transform, false);
            ground.transform.localPosition = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(30f, 1f, 30f);

            var cameraReference = new GameObject("Animation camera reference");
            cameraReference.transform.SetParent(_root.transform, false);

            var player = new GameObject("Animation player");
            player.transform.SetParent(_root.transform, false);
            player.transform.localPosition = Vector3.up * .02f;
            _controller = player.AddComponent<CharacterController>();
            _controller.height = 1.7f;
            _controller.radius = .28f;
            _controller.center = Vector3.up * .85f;
            _controller.minMoveDistance = 0f;

            GameObject visual = UnityEngine.Object.Instantiate(visualPrefab, player.transform);
            visual.transform.localPosition = new Vector3(0f, -.025f, 0f);
            _animator = visual.GetComponent<Animator>();
            _pelvisRoot = visual.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip001");

            _movement = (Behaviour)player.AddComponent(movementType);
            movementType.GetField("_cameraTransform", PrivateInstance)
                .SetValue(_movement, cameraReference.transform);
            _move = movementType.GetMethod("Move", PrivateInstance);
            Assert.That(_move, Is.Not.Null);

            _root.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
        }

        [TearDown]
        public void DestroyPlayer()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void EachMovementStatePlaysItsOwnAnimatorState()
        {
            AssertAnimation(Vector2.zero, false, "Idle");
            AssertAnimation(Vector2.zero, true, "Idle");
            AssertAnimation(Vector2.up, false, "Walk");
            AssertAnimation(Vector2.up, true, "Run");
            AssertAnimation(Vector2.up, false, "Walk");
            AssertAnimation(Vector2.zero, false, "Idle");
        }

        [Test]
        public void WalkingIntoWallShowsIdleUntilMovingAgain()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Animation test wall";
            wall.transform.SetParent(_root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.5f, 1f);
            wall.transform.localScale = new Vector3(10f, 3f, .5f);
            Physics.SyncTransforms();

            int steps = Mathf.CeilToInt(1f / Mathf.Max(Time.deltaTime, .0001f));
            for (int i = 0; i < steps; i++)
            {
                Move(Vector2.up, false);
            }
            _animator.Update(SettleSeconds);
            Assert.That(State, Is.EqualTo("Walk"));
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True,
                "A blocked walk must not run in place.");

            AssertAnimation(Vector2.down, false, "Walk");
        }

        [Test]
        public void DisablingMovementReturnsToIdleAnimation()
        {
            AssertAnimation(Vector2.up, true, "Run");
            _movement.enabled = false;
            _animator.Update(SettleSeconds);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        }

        [Test]
        public void JumpPlaysInPlaceJumpAnimationThroughLandingRecovery()
        {
            AssertAnimation(Vector2.zero, false, "Idle");
            float standingPelvis = PelvisHeight;
            float groundY = _movement.transform.position.y;
            Move(Vector2.zero, false, true);
            Assert.That(State, Is.EqualTo("Jump"));

            float dt = Mathf.Max(Time.deltaTime, .0001f);
            float highestPelvis = float.MinValue;
            float highestBody = float.MinValue;
            bool playedJump = false, wasAirborne = false, recoveredOnGround = false;
            for (float t = 0f; t < 3f && State == "Jump"; t += dt)
            {
                _animator.Update(dt);
                playedJump |= JumpAnimationInfo.IsName("Jump")
                    && _animator.GetCurrentAnimatorClipInfo(0).Concat(_animator.GetNextAnimatorClipInfo(0))
                        .Any(info => info.clip.name == "FuchsiaJump");
                highestPelvis = Mathf.Max(highestPelvis, PelvisHeight);
                highestBody = Mathf.Max(highestBody, _pelvisRoot.position.y - groundY - standingPelvis);
                Move(Vector2.zero, false);
                wasAirborne |= !_controller.isGrounded;
                recoveredOnGround |= wasAirborne && _controller.isGrounded && State == "Jump";
            }

            Assert.That(playedJump, Is.True, "Jump state must play the FuchsiaJump clip.");
            Assert.That(wasAirborne, Is.True);
            // 모션 원본에는 몸이 52cm 떠오르는 높이가 있다. 제자리 클립이어야 CharacterController 높이와 겹치지 않는다.
            Assert.That(highestPelvis, Is.LessThan(standingPelvis + .05f),
                "The jump clip must stay in place so the body does not rise twice.");
            Assert.That(highestBody, Is.GreaterThan(.3f), "The body must rise with the character controller.");
            Assert.That(recoveredOnGround, Is.True, "Without input the landing recovery must play on the ground.");
            Assert.That(State, Is.EqualTo("Idle"));
            _animator.Update(SettleSeconds);
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        }

        [Test]
        public void SpaceDuringLandingRecoveryJumpsAgain()
        {
            Move(Vector2.zero, false);
            Move(Vector2.zero, false, true);
            float dt = Mathf.Max(Time.deltaTime, .0001f);
            bool wasAirborne = false;
            for (float t = 0f; t < 2f && !(wasAirborne && _controller.isGrounded); t += dt)
            {
                _animator.Update(dt);
                Move(Vector2.zero, false);
                wasAirborne |= !_controller.isGrounded;
            }
            _animator.Update(dt);
            Assert.That(wasAirborne && _controller.isGrounded, Is.True);
            Assert.That(State, Is.EqualTo("Jump"), "The landing recovery must still be playing.");
            float landedAt = JumpAnimationInfo.normalizedTime;

            Move(Vector2.zero, false, true);
            _animator.Update(dt);
            Assert.That(State, Is.EqualTo("Jump"));
            Assert.That(JumpAnimationInfo.IsName("Jump"), Is.True);
            Assert.That(JumpAnimationInfo.normalizedTime, Is.LessThan(landedAt),
                "Jumping again must restart the jump animation.");
            bool jumpedAgain = false;
            for (float t = 0f; t < 1f && !jumpedAgain; t += dt)
            {
                Move(Vector2.zero, false);
                _animator.Update(dt);
                jumpedAgain = !_controller.isGrounded;
            }
            Assert.That(jumpedAgain, Is.True);
        }

        private void AssertAnimation(Vector2 input, bool shiftHeld, string expected)
        {
            Move(input, shiftHeld);
            _animator.Update(SettleSeconds);
            Assert.That(State, Is.EqualTo(expected));
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName(expected), Is.True,
                $"{expected} state must play the {expected} animator state.");
            // 모션 FBX 중에는 Bip001이 41cm 옆에 있는 것이 있다(예전 Idle, 전투 모션). 필드 모션도 몸이 충돌체 위에 남아야 한다.
            Vector3 offset = _pelvisRoot.position - _movement.transform.position;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(.2f),
                $"{expected} animation must keep the body over the character controller.");
        }

        private string State => (string)_movement.GetType()
            .GetProperty("CurrentMovementStateName").GetValue(_movement);

        private float PelvisHeight => _pelvisRoot.position.y - _movement.transform.position.y;

        // 전환 중이면 넘어가는 쪽(새 모션) 상태를 본다.
        private AnimatorStateInfo JumpAnimationInfo => _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);

        private void Move(Vector2 input, bool shiftHeld, bool jumpPressed = false) =>
            _move.Invoke(_movement, new object[] { input, shiftHeld, jumpPressed });
    }
}
#endif
