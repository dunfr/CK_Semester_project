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

        private string State => (string)_movement.GetType()
            .GetProperty("CurrentMovementStateName").GetValue(_movement);
        private bool IsSprinting => (bool)_movement.GetType().GetProperty("IsSprinting").GetValue(_movement);
        private bool IsSprintToggled => (bool)_movement.GetType().GetProperty("IsSprintToggled").GetValue(_movement);
        private float HorizontalSpeed => Vector3.ProjectOnPlane(_controller.velocity, Vector3.up).magnitude;
        private float AnimationSpeed => (float)_normalizedAnimationSpeed.Invoke(_movement, null);

        private void Move(Vector2 input, bool shiftHeld) => _move.Invoke(_movement, new object[] { input, shiftHeld });
        private void ToggleSprint() => _movement.GetType().GetMethod("ToggleSprint").Invoke(_movement, null);
    }
}
