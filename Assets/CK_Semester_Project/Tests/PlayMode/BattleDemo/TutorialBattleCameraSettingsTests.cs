using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CK.SemesterProject.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class TutorialBattleCameraSettingsTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Component _battle;
        private Behaviour _movement;
        private Behaviour _orbit;
        private Behaviour _brain;
        private Camera _camera;
        private GameObject _target;
        private TutorialBattleTestArena _arena;

        // Tutorial 컴포넌트는 Assembly-CSharp에 있어 기존 통합 테스트와 같은 reflection 경로를 쓴다.
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Assets/CK_Semester_Project/Prototype/Scenes/TutorialDummyModelingDemo.unity");
            yield return null;
            _battle = GameObject.Find("Tutorial Battle").GetComponent("TutorialBattleController");
            Set("_automaticEncounters", false);
            _movement = Field<Behaviour>("_movement");
            _orbit = Field<Behaviour>("_orbit");
            _brain = Field<Behaviour>("_brain");
            _camera = Field<Camera>("_camera");
            Invoke("CancelBattle");
            var playerState = (PlayerRuntimeState)Type.GetType("CK.SemesterProject.Tutorial.PlayerSessionState, Assembly-CSharp")
                .GetProperty("Current").GetValue(null);
            playerState.RestoreFull();
            _arena = new TutorialBattleTestArena(_battle);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreScene()
        {
            if (_battle != null)
            {
                Invoke("CancelBattle");
            }
            if (_target != null)
            {
                UnityEngine.Object.Destroy(_target);
            }
            _arena?.Dispose();
            _arena = null;
            yield return null;
        }

        [Test]
        public void PreviewChangesOnlyCameraAndRestoresExplorationOnFalse()
        {
            CameraState original = Capture();
            Vector3 playerPosition = _movement.transform.position;
            GameObject fieldUi = Field<GameObject>("_fieldUI");
            GameObject battleUi = Field<GameObject>("_battleUI");
            bool fieldWasActive = fieldUi.activeSelf;
            bool battleWasActive = battleUi.activeSelf;

            Set("_debugBattleCamera", true);
            TickCamera();

            Assert.That(IsInBattle, Is.False, "미리보기는 전투 세션을 시작하지 않아야 합니다.");
            Assert.That(_movement.enabled, Is.EqualTo(original.MovementEnabled),
                "카메라만 변경하는 미리보기는 탐색 이동 상태를 유지해야 합니다.");
            Assert.That(_orbit.enabled, Is.False);
            Assert.That(_brain.enabled, Is.False);
            Assert.That(Vector3.Distance(_camera.transform.position, original.Position), Is.GreaterThan(.01f));
            Assert.That(_camera.orthographic, Is.False);
            AssertPosition(_movement.transform.position, playerPosition);
            Assert.That(fieldUi.activeSelf, Is.EqualTo(fieldWasActive));
            Assert.That(battleUi.activeSelf, Is.EqualTo(battleWasActive));

            Set("_debugBattleCamera", false);
            TickCamera();

            AssertRestored(original);
            Assert.That(IsInBattle, Is.False);
        }

        [Test]
        public void PreviewImmediatelyUsesEditedOffsetsDistanceFieldOfViewAndTarget()
        {
            Vector3 player = _movement.transform.position;
            _target = new GameObject("Battle Camera Test Target");
            _target.transform.position = player + Vector3.forward * 16f;
            Set("_debugCameraTarget", _target.transform);
            Set("_battleCameraOffsetXY", new Vector2(1.5f, 2.25f));
            Set("_battleCameraDistance", 3.5f);
            Set("_battleFieldOfView", 51f);
            Set("_debugBattleCamera", true);
            TickCamera();
            // 16m 대치의 거리 보정은 2배다. 양수 Distance는 대상 반대편으로 이동한다.
            AssertPosition(_camera.transform.position, player + new Vector3(3f, 4.5f, -7f));
            Assert.That(_camera.fieldOfView, Is.EqualTo(51f).Within(.001f));

            Set("_battleCameraOffsetXY", new Vector2(-1f, 3f));
            Set("_battleCameraDistance", 5f);
            Set("_battleFieldOfView", 61f);
            TickCamera();
            AssertPosition(_camera.transform.position, player + new Vector3(-2f, 6f, -10f));
            Assert.That(_camera.fieldOfView, Is.EqualTo(61f).Within(.001f));

            _target.transform.position = player + Vector3.right * 16f;
            TickCamera();
            AssertPosition(_camera.transform.position, player + new Vector3(-10f, 6f, 2f));
            Vector3 focus = player + Vector3.right * 8.8f + Vector3.up * .9f;
            Assert.That(Vector3.Angle(_camera.transform.forward, focus - _camera.transform.position), Is.LessThan(.01f));
            Pose unchanged = new Pose(_camera.transform.position, _camera.transform.rotation);
            TickCamera();
            AssertPosition(_camera.transform.position, unchanged.position);
            Assert.That(Quaternion.Angle(_camera.transform.rotation, unchanged.rotation), Is.LessThan(.01f),
                "미리보기에는 전투 대기 흔들림이 섞이지 않아야 합니다.");
            Assert.That(IsInBattle, Is.False);
        }

        [Test]
        public void DisableAndCancelRestoreOriginallyDisabledComponentsAndCameraProjection()
        {
            _movement.enabled = false;
            _orbit.enabled = false;
            _brain.enabled = false;
            _camera.orthographic = true;
            _camera.fieldOfView = 47f;
            CameraState original = Capture();

            Set("_debugBattleCamera", true);
            TickCamera();
            ((Behaviour)_battle).enabled = false;
            AssertRestored(original);
            Assert.That(Field<bool>("_debugBattleCamera"), Is.False);

            ((Behaviour)_battle).enabled = true;
            Set("_debugBattleCamera", true);
            TickCamera();
            Invoke("CancelBattle");
            AssertRestored(original);
            Assert.That(Field<bool>("_debugBattleCamera"), Is.False);
            Assert.That(IsInBattle, Is.False);
        }

        [Test]
        public void BattleEntryFromPreviewKeepsBattleLockedAcrossCameraToggleAndRestoresFieldOnCancel()
        {
            CameraState fieldState;
            Vector3 playerHome;
            Assert.That(BeginBattleFromPreview(out fieldState, out playerHome), Is.True,
                "테스트 전투 공간에서 실제 전투 배치를 확보해야 합니다.");
            object session = Field<object>("_session");
            Assert.That(IsInBattle, Is.True);
            Assert.That(Field<bool>("_debugBattleCamera"), Is.True);
            Assert.That(_movement.enabled, Is.False);
            Assert.That(_brain.enabled, Is.False);

            Set("_debugBattleCamera", false);
            Invoke("LateUpdate");
            Assert.That(Field<object>("_session"), Is.SameAs(session));
            Assert.That(_movement.enabled, Is.False, "탐색 카메라로 전환해도 전투 이동 잠금은 유지해야 합니다.");
            AssertRestored(fieldState, includeMovement: false);

            Set("_debugBattleCamera", true);
            Invoke("LateUpdate");
            Assert.That(Field<object>("_session"), Is.SameAs(session));
            Assert.That(_movement.enabled, Is.False);
            Assert.That(_brain.enabled, Is.False);
            Assert.That(_orbit.enabled, Is.False);
            Assert.That(_camera.orthographic, Is.False);

            Invoke("CancelBattle");
            Assert.That(IsInBattle, Is.False);
            Assert.That(Field<bool>("_debugBattleCamera"), Is.False);
            AssertRestored(fieldState);
            AssertPosition(_movement.transform.position, playerHome);
        }

        [UnityTest]
        public IEnumerator BattleUsesChildAnimatorIdleLoopAndRestoresFieldPlayback()
        {
            Animator animator = _movement.GetComponentsInChildren<Animator>()
                .Single(item => item.enabled && item.runtimeAnimatorController != null);
            Assert.That(Field<Animator>("_animator"), Is.SameAs(animator));
            animator.speed = .75f;
            animator.SetFloat("Speed", 1f);
            animator.Update(.1f);
            CameraState fieldState;
            Vector3 playerHome;
            Assert.That(BeginBattleFromPreview(out fieldState, out playerHome), Is.True);
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0f));
            Assert.That(animator.speed, Is.GreaterThan(0f));
            float startedAt = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSeconds(.2f);
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0f));
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)
                .Any(info => info.clip.name == "FuchsiaIdle" && info.weight > .99f), Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(startedAt),
                "Idle 상태를 유지하면서 대기 모션 자체는 재생되어야 합니다.");
            Invoke("CancelBattle");
            Assert.That(animator.speed, Is.EqualTo(.75f));
            Assert.That(_movement.enabled, Is.True);
            _movement.GetType().GetMethod("Move", PrivateInstance)
                .Invoke(_movement, new object[] { Vector2.up, true });
            Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(0f), "필드 복귀 후 달리기 파라미터가 다시 갱신되어야 합니다.");
        }

        private bool BeginBattleFromPreview(out CameraState fieldState, out Vector3 playerHome)
        {
            MonoBehaviour enemy = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(item => item.GetType().Name == "EnemyStateMachine" && item.name == "emey AI");
            CharacterController player = _movement.GetComponent<CharacterController>();
            Array members = Array.CreateInstance(enemy.GetType(), 1);
            members.SetValue(enemy, 0);
            fieldState = default;
            playerHome = default;
            for (int index = 0; index < 8; index++)
            {
                Invoke("CancelBattle");
                Vector3 offset = Quaternion.Euler(0f, index * 45f, 0f) * Vector3.forward * 2f;
                if (!NavMesh.SamplePosition(enemy.transform.position + offset, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                {
                    continue;
                }
                player.enabled = false;
                player.transform.position = hit.position + Vector3.up * .05f;
                player.enabled = true;
                Physics.SyncTransforms();
                fieldState = Capture();
                playerHome = player.transform.position;
                Set("_debugBattleCamera", true);
                TickCamera();
                if ((bool)_battle.GetType().GetMethod("BeginBattle").Invoke(_battle,
                    new object[] { enemy, BattleEntryCondition.PlayerInitiated, members }))
                {
                    return true;
                }
            }
            return false;
        }

        private void TickCamera()
        {
            // 同一フレームで切り替えて、Cinemachine の次フレームの追従と復元の検証を分離する。
            Invoke("Update");
            Invoke("LateUpdate");
        }

        private bool IsInBattle => (bool)_battle.GetType().GetProperty("IsInBattle").GetValue(_battle);
        private T Field<T>(string name) => (T)_battle.GetType().GetField(name, PrivateInstance).GetValue(_battle);
        private void Set(string name, object value) => _battle.GetType().GetField(name, PrivateInstance).SetValue(_battle, value);
        private void Invoke(string name) => _battle.GetType().GetMethod(name, PrivateInstance | BindingFlags.Public).Invoke(_battle, null);

        private CameraState Capture() => new CameraState
        {
            Position = _camera.transform.position,
            Rotation = _camera.transform.rotation,
            FieldOfView = _camera.fieldOfView,
            Orthographic = _camera.orthographic,
            MovementEnabled = _movement.enabled,
            OrbitEnabled = _orbit.enabled,
            BrainEnabled = _brain.enabled,
        };

        private void AssertRestored(CameraState state, bool includeMovement = true)
        {
            AssertPosition(_camera.transform.position, state.Position);
            Assert.That(Quaternion.Angle(_camera.transform.rotation, state.Rotation), Is.LessThan(.01f));
            Assert.That(_camera.fieldOfView, Is.EqualTo(state.FieldOfView).Within(.001f));
            Assert.That(_camera.orthographic, Is.EqualTo(state.Orthographic));
            Assert.That(_orbit.enabled, Is.EqualTo(state.OrbitEnabled));
            Assert.That(_brain.enabled, Is.EqualTo(state.BrainEnabled));
            if (includeMovement)
            {
                Assert.That(_movement.enabled, Is.EqualTo(state.MovementEnabled));
            }
        }

        private static void AssertPosition(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f));
        }

        private struct CameraState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float FieldOfView;
            public bool Orthographic;
            public bool MovementEnabled;
            public bool OrbitEnabled;
            public bool BrainEnabled;
        }
    }
}
