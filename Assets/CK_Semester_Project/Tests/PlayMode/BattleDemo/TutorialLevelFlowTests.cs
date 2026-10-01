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
    public sealed class TutorialLevelFlowTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Component _flow;
        private Component _battle;
        private Component _movement;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Assets/CK_Semester_Project/Prototype/Scenes/TutorialGrayboxingDemo.unity");
            yield return null;
            _flow = GameObject.Find("Tutorial Level Content").GetComponent("TutorialLevelFlow");
            _battle = GameObject.Find("Tutorial Battle").GetComponent("TutorialBattleController");
            _movement = GameObject.Find("Fuchsia Player").GetComponent("PlayerMovement");
            Set(_flow, "_awakeningSeconds", .02f);
            Set(_flow, "_elevatorSeconds", .4f);
            Set(_flow, "_observationSeconds", .15f);
            Set(_flow, "_collapseSeconds", .1f);
            Set(_battle, "_automaticEncounters", false);
            Set(_battle, "_actionSeconds", .02f);
            Set(_battle, "_cameraEntrySeconds", .02f);
            yield return new WaitForSeconds(.1f);
            Assert.That(Stage, Is.EqualTo("FirstBattle"));
        }

        [UnityTearDown]
        public IEnumerator RestoreScene()
        {
            Invoke(_battle, "CancelBattle");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LockedDoorAndOutOfOrderTriggersCannotSkipLearning()
        {
            Component door = Door("CardDoor");
            Invoke(door, "Interact");
            Assert.That(Get(door, "IsOpen"), Is.False);
            Assert.That(Get(_flow, "HasCardKey"), Is.False);
            Assert.That(GameObject.Find("Runtime Level Feedback"), Is.Not.Null);
            MethodInfo enter = _flow.GetType().GetMethod("EnterZone");
            enter.Invoke(_flow, new[] { Enum.Parse(enter.GetParameters()[0].ParameterType, "Completed") });
            Assert.That(Stage, Is.EqualTo("FirstBattle"));
            Assert.That(Invoke(_flow, "BeginElevator"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VictoryDoorsLiftDuctAndUpperExitFormOneRoute()
        {
            yield return WinFirstBattle();
            Assert.That(Get(_flow, "HasCardKey"), Is.True);
            Assert.That(Stage, Is.EqualTo("CardDoor"));
            Component door = Door("CardDoor");
            yield return AimAtDoor(door, Vector3.left * 1.6f);
            Component interaction = _flow.GetComponent("TutorialFieldInteraction");
            Assert.That(Get(interaction, "Current"), Is.EqualTo(door), "문을 바라보면 좌클릭 대상이 되어야 합니다.");
            Assert.That(Invoke(interaction, "TryInteract"), Is.True);
            Assert.That(Stage, Is.EqualTo("Elevator"));
            Invoke(Door("ElevatorDoor"), "Interact");
            yield return new WaitForSeconds(1.05f);
            Teleport(new Vector3(-29f, 47.03f, -.08f));
            Assert.That(Invoke(_flow, "BeginElevator"), Is.True);
            Transform car = ((Transform)_flow.GetType().GetField("_elevatorCar", PrivateInstance).GetValue(_flow));
            Vector3 passengerOffset = _movement.transform.position - car.position;
            yield return new WaitForSeconds(.15f);
            Assert.That(Vector3.Distance(_movement.transform.position - car.position, passengerOffset), Is.LessThan(.01f));
            Assert.That(car.position.y, Is.LessThan(46f));
            yield return new WaitForSeconds(1.3f);
            Assert.That(Stage, Is.EqualTo("ObserveGuard"));
            Assert.That(_movement.transform.position.y, Is.InRange(8.5f, 8.9f));
            Assert.That(Get(Door("ArrivalDoor"), "IsOpen"), Is.True);
            Assert.That(Get(Door("ElevatorDoor"), "IsOpen"), Is.False);
            Teleport(new Vector3(-36.7f, 8.72f, 7.5f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("DuctRamp"));
            Teleport(new Vector3(-52.9f, 13.2f, 18.91f));
            yield return new WaitForSeconds(.3f);
            Assert.That(Stage, Is.EqualTo("DuctEscape"));
            bool reactingGuard = new[] { "emey (4) AI", "emey (5) AI" }
                .Any(name => Get(GameObject.Find(name).GetComponent("EnemyStateMachine"), "CurrentState").ToString() == "SoundLook");
            Assert.That(reactingGuard, Is.True, "관찰 소음이 실제 경비 AI의 소리 반응으로 연결되어야 합니다.");
            Teleport(new Vector3(-55.7f, 13.2f, 18.91f));
            yield return new WaitForSeconds(1.5f);
            Assert.That(Get(_flow, "HasCollapsed"), Is.True);
            Assert.That(_movement.transform.position.y, Is.InRange(8.5f, 8.9f), "덕트 바닥 제거 후 실제 중력으로 착지해야 합니다.");
            Assert.That(Invoke(_flow, "CollapseDuct"), Is.False, "붕괴 이벤트는 반복하면 안 됩니다.");
            Invoke(Door("HallDoor"), "Interact");
            yield return new WaitForSeconds(1.05f);
            Teleport(new Vector3(-52.67f, 8.72f, 11.1f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("Hall"));
            Teleport(new Vector3(-52.67f, 12.22f, -18.1f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("UpperExit"));
            Invoke(Door("ExitDoor"), "Interact");
            yield return new WaitForSeconds(1.05f);
            Teleport(new Vector3(-52.67f, 12.7f, -21f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("Completed"));
            Assert.That(GameObject.Find("Default_State"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator CharacterControllerCanTraverseConnectedGeometry()
        {
            yield return WinFirstBattle();
            Component firstDoor = Door("CardDoor");
            yield return AimAtDoor(firstDoor, Vector3.left * 1.6f);
            Invoke(firstDoor, "Interact");
            Invoke(Door("ElevatorDoor"), "Interact");
            yield return new WaitForSeconds(1.1f);
            yield return WalkTo(new Vector3(-29f, 47f, -.08f));
            Assert.That(Invoke(_flow, "BeginElevator"), Is.True);
            yield return new WaitForSeconds(1.5f);
            yield return WalkTo(new Vector3(-36.7f, 8.7f, -.08f));
            yield return WalkTo(new Vector3(-36.7f, 8.7f, 7.5f));
            Assert.That(Stage, Is.EqualTo("DuctRamp"));
            yield return WalkTo(new Vector3(-36.7f, 8.7f, 18.91f));
            yield return WalkTo(new Vector3(-43f, 8.7f, 18.91f));
            yield return WalkTo(new Vector3(-52.9f, 13.2f, 18.91f));
            yield return new WaitForSeconds(.3f);
            yield return WalkTo(new Vector3(-55.7f, 13.2f, 18.91f), false);
            yield return new WaitForSeconds(1.5f);
            Invoke(Door("HallDoor"), "Interact");
            yield return new WaitForSeconds(1.1f);
            yield return WalkTo(new Vector3(-52.67f, 8.7f, 18.91f));
            yield return WalkTo(new Vector3(-52.67f, 8.7f, 11.1f));
            yield return WalkTo(new Vector3(-52.67f, 8.7f, -10f));
            yield return WalkTo(new Vector3(-52.67f, 12.22f, -18.1f));
            Invoke(Door("ExitDoor"), "Interact");
            yield return new WaitForSeconds(1.1f);
            yield return WalkTo(new Vector3(-52.67f, 12.7f, -21f));
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("Completed"));
        }

        private IEnumerator WalkTo(Vector3 target, bool checkHeight = true)
        {
            Behaviour movement = (Behaviour)_movement;
            movement.enabled = false;
            CharacterController controller = _movement.GetComponent<CharacterController>();
            int frame = 0;
            for (; frame < 500; frame++)
            {
                Vector3 delta = target - _movement.transform.position;
                delta.y = 0;
                if (delta.magnitude < .2f)
                {
                    break;
                }
                controller.Move(Vector3.ClampMagnitude(delta, .12f) + Vector3.down * .06f);
                yield return null;
            }
            movement.enabled = true;
            Assert.That(frame, Is.LessThan(500), "경로가 막힘: " + _movement.transform.position + " -> " + target);
            if (checkHeight)
            {
                Assert.That(_movement.transform.position.y, Is.EqualTo(target.y).Within(.3f), "높이 연결 실패: " + target);
            }
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator CheckpointRestoresPositionAndKeepsAcquiredCard()
        {
            yield return WinFirstBattle();
            Invoke(Door("CardDoor"), "Interact");
            Vector3 checkpoint = (Vector3)Get(_flow, "Checkpoint");
            Teleport(checkpoint + Vector3.right * 4);
            Invoke(_flow, "RespawnAtCheckpoint");
            Assert.That(Vector3.Distance(_movement.transform.position, checkpoint), Is.LessThan(.01f));
            Assert.That(Get(_flow, "HasCardKey"), Is.True);
            Assert.That(Stage, Is.EqualTo("Elevator"));
            yield return null;
        }

        private IEnumerator WinFirstBattle()
        {
            Component enemy = GameObject.Find("emey AI").GetComponent("EnemyStateMachine");
            bool started = false;
            for (int index = 0; index < 8 && !started; index++)
            {
                NavMeshHit hit;
                Vector3 offset = Quaternion.Euler(0, index * 45, 0) * Vector3.forward * 2;
                if (!NavMesh.SamplePosition(enemy.transform.position + offset, out hit, 1, NavMesh.AllAreas))
                {
                    continue;
                }
                Teleport(hit.position + Vector3.up * .05f);
                started = (bool)_battle.GetType().GetMethod("BeginBattle").Invoke(_battle,
                    new object[] { enemy, BattleEntryCondition.PlayerInitiated, null });
            }
            Assert.That(started, Is.True, "실제 맵 전투 배치 실패");
            for (int frame = 0; frame < 500 && Snapshot.Phase != BattlePhase.Finished; frame++)
            {
                if ((bool)Get(_battle, "CanChooseAction"))
                {
                    _battle.GetType().GetMethod("UseSkill").Invoke(_battle, new object[] { 0 });
                }
                yield return null;
            }
            Assert.That(Snapshot.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Invoke(_battle, "ReturnToExploration");
            yield return null;
        }

        private IEnumerator AimAtDoor(Component door, Vector3 offset)
        {
            Vector3 point = (Vector3)Get(door, "InteractionPosition");
            Teleport(point - Vector3.up * .95f + offset);
            Component orbit = _movement.GetComponentInChildren(Type.GetType("CK.SemesterProject.Tutorial.CameraController, Assembly-CSharp"));
            orbit.GetType().GetMethod("FaceDirection").Invoke(orbit, new object[] { -offset });
            yield return null;
            yield return null;
        }

        private Component Door(string kind)
        {
            return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(item => item.GetType().Name == "TutorialFieldInteractable" && Get(item, "Kind").ToString() == kind);
        }

        private string Stage => Get(_flow, "Stage").ToString();
        private BattleSnapshot Snapshot => (BattleSnapshot)Get(_battle, "Snapshot");
        private static object Get(Component target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Invoke(Component target, string name) => target.GetType().GetMethod(name).Invoke(target, null);
        private static void Set(Component target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

        private void Teleport(Vector3 point)
        {
            _movement.GetType().GetMethod("Teleport").Invoke(_movement, new object[] { point });
        }
    }
}
