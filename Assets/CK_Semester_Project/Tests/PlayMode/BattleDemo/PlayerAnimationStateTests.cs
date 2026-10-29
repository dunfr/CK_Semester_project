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
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = .28f;
            controller.center = Vector3.up * .85f;
            controller.minMoveDistance = 0f;

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

        private void AssertAnimation(Vector2 input, bool shiftHeld, string expected)
        {
            Move(input, shiftHeld);
            _animator.Update(SettleSeconds);
            Assert.That(State, Is.EqualTo(expected));
            Assert.That(_animator.GetCurrentAnimatorStateInfo(0).IsName(expected), Is.True,
                $"{expected} state must play the {expected} animator state.");
            // Idle FBX 원본은 Bip001이 41cm 옆에 있다. 오프셋을 뺀 클립을 써야 몸이 충돌체 위에 남는다.
            Vector3 offset = _pelvisRoot.position - _movement.transform.position;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(.2f),
                $"{expected} animation must keep the body over the character controller.");
        }

        private string State => (string)_movement.GetType()
            .GetProperty("CurrentMovementStateName").GetValue(_movement);

        private void Move(Vector2 input, bool shiftHeld) => _move.Invoke(_movement, new object[] { input, shiftHeld });
    }
}
#endif
