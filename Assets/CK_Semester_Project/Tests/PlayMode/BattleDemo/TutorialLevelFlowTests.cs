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
            Set(_flow, "_pressStopHoldSeconds", .02f);
            Set(_flow, "_guardWalkSeconds", .02f);
            Set(_flow, "_guardGestureSeconds", .02f);
            Set(_flow, "_elevatorSeconds", .25f);
            Set(_flow, "_observationSeconds", .1f);
            Set(_flow, "_collapseSeconds", .1f);
            Set(_battle, "_automaticEncounters", false);
            Set(_battle, "_actionSeconds", .02f);
            Set(_battle, "_cameraEntrySeconds", .02f);
            yield return new WaitForSeconds(.25f);
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
            Invoke(Door("CardDoor"), "Interact");
            Assert.That(Get(Door("CardDoor"), "IsOpen"), Is.False);
            Assert.That(Get(_flow, "HasCardKey"), Is.False);
            Enter("Completed");
            Assert.That(Stage, Is.EqualTo("FirstBattle"));
            Assert.That(Invoke(_flow, "BeginElevator"), Is.False);
            yield return new WaitForSeconds(.6f);
            Assert.That(((Behaviour)_movement).enabled, Is.True, "카드키 유도 카메라 이후 조작을 복구해야 합니다.");
        }

        [UnityTest]
        public IEnumerator VictoryRaisesCardAndRequiresManualPickup()
        {
            yield return WinFirstBattle(false);
            Assert.That(Get(_flow, "HasCardKey"), Is.False);
            Assert.That(Get(_flow, "IsCardKeyRaised"), Is.True);
            Assert.That(Stage, Is.EqualTo("CardDoor"));
            Component card = Door("CardKey");
            yield return AimAt(card, Vector3.forward * 1.6f);
            Component interaction = _flow.GetComponent("TutorialFieldInteraction");
            Assert.That(Get(interaction, "Current"), Is.EqualTo(card));
            Assert.That(Invoke(interaction, "TryInteract"), Is.True);
            Assert.That(Get(_flow, "HasCardKey"), Is.True);
            Assert.That(Get(_flow, "IsCardKeyRaised"), Is.False);
            Invoke(_flow, "CollectCardKey");
            Assert.That(Get(_flow, "HasCardKey"), Is.True, "카드 획득은 한 번만 적용됩니다.");
        }

        [UnityTest]
        public IEnumerator TwentySecondGuardWaitBeginsPursuit()
        {
            Component enemy = GameObject.Find("emey AI").GetComponent("EnemyStateMachine");
            Assert.That(Get(enemy, "CurrentState").ToString(), Is.EqualTo("Patrol").Or.EqualTo("PatrolWait"));
            Set(_flow, "_firstGuardWaitSeconds", .01f);
            yield return new WaitForSeconds(.15f);
            Assert.That(Get(enemy, "HasLastSeenPosition"), Is.True);
            Assert.That((Vector3)Get(enemy, "LastSeenPosition"), Is.EqualTo(_movement.transform.position));
        }

        [UnityTest]
        public IEnumerator GroundRouteDoesNotRequireDuctCollapseAndCheckpointKeepsCard()
        {
            yield return WinFirstBattle();
            yield return RideLift();
            Teleport(new Vector3(-3, -119.5f, 22));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Get(_flow, "HasDuctNoise"), Is.True);
            Assert.That(Get(Door("CorridorCardDoor"), "IsOpen"), Is.True);
            yield return new WaitForSeconds(1.2f);
            Assert.That(Get(GameObject.Find("emey (2) AI").GetComponent("EnemyStateMachine"), "HasLastSeenPosition"), Is.True);
            Assert.That(Get(GameObject.Find("emey (3) AI").GetComponent("EnemyStateMachine"), "HasLastSeenPosition"), Is.True);
            Teleport(new Vector3(20, -119.5f, -22.5f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("Hall"));
            Assert.That(Get(_flow, "HasCollapsed"), Is.False);
            Vector3 checkpoint = (Vector3)Get(_flow, "Checkpoint");
            Teleport(new Vector3(10, -140, 0));
            yield return null;
            Assert.That(Vector3.Distance(_movement.transform.position, checkpoint), Is.LessThan(.1f));
            Assert.That(Get(_flow, "HasCardKey"), Is.True);
            Teleport(new Vector3(36, -115.5f, -8.084f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("UpperExit"));
            Teleport(new Vector3(41, -115.5f, -8.084f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("Completed"));
        }

        [UnityTest]
        public IEnumerator DuctCollapseRequiresObservationAndArrivalOnDuct()
        {
            Assert.That(Invoke(_flow, "CollapseDuct"), Is.False);
            yield return WinFirstBattle();
            yield return RideLift();
            Teleport(new Vector3(-29.04f, -109.5f, -26.77f));
            Assert.That(Invoke(_flow, "CollapseDuct"), Is.False, "관찰 전에 붕괴 구역에 도착해도 실행하지 않습니다.");
            Teleport(new Vector3(-34.78f, -109.5f, -18));
            Enter("DuctObservation");
            yield return new WaitForSeconds(.2f);
            Assert.That(Stage, Is.EqualTo("DuctEscape"));
            Teleport(new Vector3(-4.47f, 1.05f, .88f));
            Assert.That(Invoke(_flow, "CollapseDuct"), Is.False, "시작 층에서는 붕괴하지 않습니다.");
            Teleport(new Vector3(-29.04f, -119.5f, -26.77f));
            Assert.That(Invoke(_flow, "CollapseDuct"), Is.False, "덕트 바로 아래 바닥에서는 붕괴하지 않습니다.");
            Assert.That(Get(_flow, "HasCollapsed"), Is.False);
            Teleport(new Vector3(-29.04f, -109.5f, -26.77f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Get(_flow, "HasCollapsed"), Is.True, "관찰을 마치고 덕트 위에 도착하면 트리거로 붕괴합니다.");
            Assert.That(Invoke(_flow, "CollapseDuct"), Is.False, "붕괴는 한 번만 실행합니다.");
        }

        [UnityTest]
        public IEnumerator CharacterControllerTraversesNewMapLiftDuctHallAndExit()
        {
            yield return WinFirstBattle();
            foreach (MonoBehaviour enemy in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(item => item.GetType().Name == "EnemyStateMachine"))
            {
                enemy.enabled = false;
                enemy.GetComponent<NavMeshAgent>().enabled = false;
                foreach (Collider collider in enemy.GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }
            }
            yield return AimAt(Door("CardDoor"), Vector3.back * 1.6f);
            Component interaction = _flow.GetComponent("TutorialFieldInteraction");
            Assert.That(Get(interaction, "Current"), Is.EqualTo(Door("CardDoor")));
            Invoke(interaction, "TryInteract");
            Invoke(Door("ElevatorDoor"), "Interact");
            yield return new WaitForSeconds(1.1f);
            yield return WalkTo(new Vector3(0, .3f, 43));
            yield return WalkTo(new Vector3(0, .4f, 49));
            Assert.That(Invoke(_flow, "BeginElevator"), Is.True);
            Transform car = (Transform)_flow.GetType().GetField("_elevatorCar", PrivateInstance).GetValue(_flow);
            Vector3 offset = _movement.transform.position - car.position;
            yield return new WaitForSeconds(.1f);
            Assert.That(Vector3.Distance(_movement.transform.position - car.position, offset), Is.LessThan(.02f));
            yield return new WaitForSeconds(1.4f);
            Assert.That(Stage, Is.EqualTo("ObserveGuard"));
            Assert.That(_movement.transform.position.y, Is.InRange(-119.7f, -119.3f));
            yield return WalkTo(new Vector3(0, -119.6f, 30));
            yield return WalkTo(new Vector3(-4.5f, -119.6f, 30));
            yield return WalkTo(new Vector3(-4.5f, -119.6f, 22));
            yield return WalkTo(new Vector3(-3, -119.6f, 22));
            yield return WalkTo(new Vector3(-36, -119.6f, 21));
            yield return WalkTo(new Vector3(-39, -119.6f, 15.5f));
            yield return WalkTo(new Vector3(-34.78f, -119.6f, 15.5f));
            yield return WalkTo(new Vector3(-34.78f, -109.6f, -18));
            yield return WalkTo(new Vector3(-33, -109.6f, -22));
            yield return new WaitForSeconds(.2f);
            Assert.That(Stage, Is.EqualTo("DuctEscape"));
            yield return WalkTo(new Vector3(-29.04f, -109.6f, -26.77f), false);
            yield return new WaitForSeconds(2);
            Assert.That(Get(_flow, "HasCollapsed"), Is.True);
            Assert.That(_movement.transform.position.y, Is.InRange(-119.7f, -119.3f));
            Assert.That(Invoke(_flow, "CollapseDuct"), Is.False);
            yield return WalkTo(new Vector3(-29, -119.6f, -34));
            yield return WalkTo(new Vector3(20, -119.6f, -34));
            yield return WalkTo(new Vector3(20, -119.6f, -8));
            Assert.That(Stage, Is.EqualTo("Hall"));
            yield return WalkTo(new Vector3(36, -116.28f, -8));
            yield return WalkTo(new Vector3(41, -115.6f, -8));
            yield return new WaitForFixedUpdate();
            Assert.That(Stage, Is.EqualTo("Completed"));
        }

        private IEnumerator RideLift()
        {
            Invoke(Door("CardDoor"), "Interact");
            Invoke(Door("ElevatorDoor"), "Interact");
            yield return new WaitForSeconds(1.1f);
            Teleport(new Vector3(0, .45f, 49));
            Assert.That(Invoke(_flow, "BeginElevator"), Is.True);
            yield return new WaitForSeconds(1.5f);
            Assert.That(Stage, Is.EqualTo("ObserveGuard"));
        }

        private IEnumerator WalkTo(Vector3 target, bool checkHeight = true)
        {
            Behaviour movement = (Behaviour)_movement;
            movement.enabled = false;
            CharacterController controller = _movement.GetComponent<CharacterController>();
            int frame = 0;
            for (; frame < 900; frame++)
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
            Assert.That(frame, Is.LessThan(900), "경로가 막힘: " + _movement.transform.position + " -> " + target);
            if (checkHeight)
            {
                Assert.That(_movement.transform.position.y, Is.EqualTo(target.y).Within(.35f), "높이 연결 실패: " + target);
            }
            yield return new WaitForFixedUpdate();
        }

        private IEnumerator WinFirstBattle(bool collect = true)
        {
            Component enemy = GameObject.Find("emey AI").GetComponent("EnemyStateMachine");
            bool started = false;
            for (int index = 0; index < 8 && !started; index++)
            {
                Vector3 offset = Quaternion.Euler(0, index * 45, 0) * Vector3.forward * 2;
                if (!NavMesh.SamplePosition(enemy.transform.position + offset, out NavMeshHit hit, 1, NavMesh.AllAreas))
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
            yield return new WaitForSeconds(.65f);
            if (collect)
            {
                Invoke(_flow, "CollectCardKey");
                Assert.That(Get(_flow, "HasCardKey"), Is.True);
            }
        }

        private IEnumerator AimAt(Component target, Vector3 offset)
        {
            Vector3 point = (Vector3)Get(target, "InteractionPosition");
            Teleport(point - Vector3.up + offset);
            Component orbit = _movement.GetComponentInChildren(Type.GetType("CK.SemesterProject.Tutorial.CameraController, Assembly-CSharp"));
            orbit.GetType().GetMethod("FaceDirection").Invoke(orbit, new object[] { -offset });
            yield return null;
            yield return null;
        }

        private void Enter(string stage)
        {
            MethodInfo method = _flow.GetType().GetMethod("EnterZone");
            method.Invoke(_flow, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, stage) });
        }

        private Component Door(string kind) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(item => item.GetType().Name == "TutorialFieldInteractable" && Get(item, "Kind").ToString() == kind);
        private string Stage => Get(_flow, "Stage").ToString();
        private BattleSnapshot Snapshot => (BattleSnapshot)Get(_battle, "Snapshot");
        private static object Get(Component target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Invoke(Component target, string name) => target.GetType().GetMethod(name).Invoke(target, null);
        private static void Set(Component target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        private void Teleport(Vector3 point) => _movement.GetType().GetMethod("Teleport").Invoke(_movement, new object[] { point });
    }
}
