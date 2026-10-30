using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class PlayerLocomotionStateTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _root;
        private Behaviour _movement;
        private CharacterController _controller;
        private MethodInfo _move;
        private MethodInfo _normalizedAnimationSpeed;

        // Tutorial scripts belong to Assembly-CSharp; this asmdef accesses their runtime type.
        [UnitySetUp]
        public IEnumerator CreatePlayer()
        {
            Type movementType = Type.GetType("CK.SemesterProject.Tutorial.PlayerMovement, Assembly-CSharp");
            Assert.That(movementType, Is.Not.Null);

            _root = new GameObject("Locomotion test fixture");
            _root.SetActive(false);
            _root.transform.position = Vector3.up * 1000f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Locomotion test floor";
            ground.transform.SetParent(_root.transform, false);
            ground.transform.localPosition = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(30f, 1f, 30f);

            var cameraReference = new GameObject("Locomotion camera reference");
            cameraReference.transform.SetParent(_root.transform, false);

            var player = new GameObject("Locomotion player");
            player.transform.SetParent(_root.transform, false);
            player.transform.localPosition = Vector3.up * .02f;
            _controller = player.AddComponent<CharacterController>();
            _controller.height = 2f;
            _controller.radius = .25f;
            _controller.center = Vector3.up;
            _controller.minMoveDistance = 0f;
            _movement = (Behaviour)player.AddComponent(movementType);
            movementType.GetField("_cameraTransform", PrivateInstance)
                .SetValue(_movement, cameraReference.transform);
            _move = movementType.GetMethod("Move", PrivateInstance);
            _normalizedAnimationSpeed = movementType.GetMethod("GetNormalizedAnimationSpeed", PrivateInstance);
            Assert.That(_move, Is.Not.Null);
            Assert.That(_normalizedAnimationSpeed, Is.Not.Null);

            _root.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
            Move(Vector2.zero, false);
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
        public void ShiftTransitionsIdleWalkRunWalkIdleAndUsesConfiguredSpeeds()
        {
            Assert.That(State, Is.EqualTo("Idle"));
            Move(Vector2.zero, true);
            Assert.That(State, Is.EqualTo("Idle"), "Shift alone must not enter Run.");

            Move(Vector2.up, false);
            Assert.That(State, Is.EqualTo("Walk"));
            Assert.That(IsSprinting, Is.False);
            float walkSpeed = HorizontalSpeed;
            Assert.That(walkSpeed, Is.EqualTo(3f).Within(.02f));
            Assert.That(AnimationSpeed, Is.EqualTo(.5f).Within(.01f));

            Move(Vector2.up, true);
            Assert.That(State, Is.EqualTo("Run"));
            Assert.That(IsSprinting, Is.True);
            Assert.That(HorizontalSpeed / walkSpeed, Is.EqualTo(1.8f).Within(.02f));
            Assert.That(AnimationSpeed, Is.EqualTo(1f).Within(.01f));

            Move(Vector2.up, false);
            Assert.That(State, Is.EqualTo("Walk"));
            Assert.That(IsSprinting, Is.False);
            Move(Vector2.zero, true);
            Assert.That(State, Is.EqualTo("Idle"));
            Assert.That(IsSprinting, Is.False);
            Assert.That(HorizontalSpeed, Is.LessThan(.01f));
            Assert.That(AnimationSpeed, Is.LessThan(.01f));
        }

        [Test]
        public void SprintToggleSurvivesStoppingAndDisableResetsStateAndToggle()
        {
            ToggleSprint();
            Assert.That(IsSprintToggled, Is.True);
            Move(Vector2.up, false);
            Assert.That(State, Is.EqualTo("Run"));

            Move(Vector2.zero, false);
            Assert.That(State, Is.EqualTo("Idle"));
            Assert.That(IsSprintToggled, Is.True);
            Move(Vector2.up, false);
            Assert.That(State, Is.EqualTo("Run"));

            ToggleSprint();
            Move(Vector2.up, false);
            Assert.That(State, Is.EqualTo("Walk"));
            ToggleSprint();
            Move(Vector2.up, false);
            Assert.That(State, Is.EqualTo("Run"));

            _movement.enabled = false;
            Assert.That(State, Is.EqualTo("Idle"));
            Assert.That(IsSprinting, Is.False);
            Assert.That(IsSprintToggled, Is.False);
            ToggleSprint();
            Assert.That(IsSprintToggled, Is.False, "Disabled movement must ignore the HUD toggle.");
            _movement.enabled = true;
            Move(Vector2.up, false);
            Assert.That(State, Is.EqualTo("Walk"));
        }

        [Test]
        public void WallStopsNormalizedAnimationEvenWhileWalkInputIsHeld()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Locomotion test wall";
            wall.transform.SetParent(_root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.5f, 1f);
            wall.transform.localScale = new Vector3(10f, 3f, .5f);
            Physics.SyncTransforms();

            // Reach the wall even when the test runner uses a short frame duration.
            int steps = Mathf.CeilToInt(1f / Mathf.Max(Time.deltaTime, .0001f));
            for (int i = 0; i < steps; i++)
            {
                Move(Vector2.up, false);
            }

            Assert.That(State, Is.EqualTo("Walk"));
            Assert.That(_movement.transform.localPosition.z, Is.LessThan(.8f));
            Assert.That(HorizontalSpeed, Is.LessThan(.05f));
            Assert.That(AnimationSpeed, Is.LessThan(.01f),
                "Fuchsia animation must use achieved horizontal speed, not held movement input.");
        }

        [Test]
        public void SpaceJumpsFromGroundOnceAndLandsBackToIdle()
        {
            float groundY = _movement.transform.localPosition.y;
            Move(Vector2.zero, false, true);
            Assert.That(State, Is.EqualTo("Jump"));

            // 공중에서 누르는 Space는 무시한다(이중 점프 없음).
            float peak = 0f;
            bool wasAirborne = false;
            int steps = Mathf.CeilToInt(2f / Mathf.Max(Time.deltaTime, .0001f));
            for (int i = 0; i < steps && State == "Jump"; i++)
            {
                Move(Vector2.zero, false, wasAirborne && !_controller.isGrounded);
                wasAirborne |= !_controller.isGrounded;
                peak = Mathf.Max(peak, _movement.transform.localPosition.y - groundY);
            }

            Assert.That(wasAirborne, Is.True);
            // 프레임 단위 중력 적분은 최고점을 프레임 길이에 비례해 조금 낮게 잡는다.
            Assert.That(peak, Is.InRange(.48f - 2.5f * Time.deltaTime, .52f),
                "The jump must rise to the configured 0.5m only once.");
            Assert.That(State, Is.EqualTo("Idle"), "Without a jump animation the jump ends on landing.");
            // 착지한 CharacterController는 바닥에서 Skin Width만큼 떨어져 멈춘다(처음 놓인 0.02m는 그보다 바닥에 가깝다).
            Assert.That(_controller.isGrounded, Is.True);
            Assert.That(_movement.transform.localPosition.y - groundY, Is.LessThan(_controller.skinWidth + .01f));
        }

        [Test]
        public void RunningJumpKeepsRunSpeedInAirAndLandsIntoInputState()
        {
            Move(Vector2.up, true);
            Assert.That(State, Is.EqualTo("Run"));
            Move(Vector2.up, true, true);
            Assert.That(State, Is.EqualTo("Jump"));
            Assert.That(IsSprinting, Is.True, "A running jump keeps the sprint state for the HUD and footstep noise.");

            bool wasAirborne = false;
            int steps = Mathf.CeilToInt(2f / Mathf.Max(Time.deltaTime, .0001f));
            for (int i = 0; i < steps && State == "Jump"; i++)
            {
                // Shift를 놓아도 공중에서는 뛰기 직전 속도를 유지한다.
                Move(Vector2.up, false);
                if (!_controller.isGrounded)
                {
                    wasAirborne = true;
                    Assert.That(HorizontalSpeed, Is.EqualTo(3f * 1.8f).Within(.05f));
                }
            }

            Assert.That(wasAirborne, Is.True);
            Assert.That(State, Is.EqualTo("Walk"), "Landing with move input must go straight to the state chosen by input.");
            Assert.That(IsSprinting, Is.False);
        }

        [Test]
        public void SpaceDoesNotJumpWhileFalling()
        {
            _movement.transform.localPosition += Vector3.up * 3f;
            Physics.SyncTransforms();
            Move(Vector2.zero, false);
            Assert.That(_controller.isGrounded, Is.False);

            Move(Vector2.zero, false, true);
            Assert.That(State, Is.EqualTo("Idle"), "Space must not jump while the feet are off the ground.");
        }

        private string State => (string)_movement.GetType()
            .GetProperty("CurrentMovementStateName").GetValue(_movement);
        private bool IsSprinting => (bool)_movement.GetType().GetProperty("IsSprinting").GetValue(_movement);
        private bool IsSprintToggled => (bool)_movement.GetType().GetProperty("IsSprintToggled").GetValue(_movement);
        private float HorizontalSpeed => Vector3.ProjectOnPlane(_controller.velocity, Vector3.up).magnitude;
        private float AnimationSpeed => (float)_normalizedAnimationSpeed.Invoke(_movement, null);

        private void Move(Vector2 input, bool shiftHeld, bool jumpPressed = false) =>
            _move.Invoke(_movement, new object[] { input, shiftHeld, jumpPressed });
        private void ToggleSprint() => _movement.GetType().GetMethod("ToggleSprint").Invoke(_movement, null);
    }
}
