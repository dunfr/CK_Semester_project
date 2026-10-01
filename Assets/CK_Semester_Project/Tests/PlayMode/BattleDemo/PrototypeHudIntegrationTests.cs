using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CK.SemesterProject.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class PrototypeHudIntegrationTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Component _battle;
        private Component _hud;
        private PlayerRuntimeState _playerState;

        // Tutorial 코드는 기존 Assembly-CSharp에 있어 asmdef 테스트에서는 런타임 타입으로 접근한다.
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Assets/CK_Semester_Project/Prototype/Scenes/TutorialGrayboxingDemo.unity");
            yield return null;
            _battle = GameObject.Find("Tutorial Battle").GetComponent("TutorialBattleController");
            _hud = _battle.GetComponent("PrototypeHudPresenter");
            _battle.GetType().GetField("_automaticEncounters", PrivateInstance).SetValue(_battle, false);
            _playerState = (PlayerRuntimeState)Type.GetType("CK.SemesterProject.Tutorial.PlayerSessionState, Assembly-CSharp")
                .GetProperty("Current").GetValue(null);
            _playerState.RestoreFull();
        }

        [UnityTearDown]
        public IEnumerator RestoreScene()
        {
            _battle.GetType().GetMethod("CancelBattle").Invoke(_battle, null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FieldVitalsAndFloorStateRemainWithoutUnavailableControls()
        {
            _playerState.SetVitals(731, 143);
            yield return null;
            Assert.That(Text("Runtime Field Vitals"), Does.Contain("731/1000").And.Contain("143/200"));
            Component floor = GameObject.Find("Fuchsia Player").GetComponent("FloorTravelButtons");
            string[] unavailable = { "Travel First Floor", "Travel Second Floor", "03_Menu_C", "04_Menu_J",
                "05_Menu_ESC", "07_Tab_Button", "08_R_Button", "11_Interact_E", "12_Sprint_Shift",
                "SkillButton_LockedBoundary", "SkillDetailsPrompt" };
            Transform[] ui = _hud.GetType().GetField("_fieldRoot", PrivateInstance).GetValue(_hud)
                is GameObject field ? field.GetComponentInParent<Canvas>().GetComponentsInChildren<Transform>(true) : null;
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.Any(item => unavailable.Contains(item.name)), Is.False);
            floor.GetType().GetMethod("TravelToSecondFloor").Invoke(floor, null);
            yield return null;
            Assert.That(floor.GetType().GetProperty("CurrentFloor").GetValue(floor), Is.EqualTo(2));
            Assert.That(Text("Runtime Location"), Does.StartWith("2층"));
            floor.GetType().GetMethod("TravelToFirstFloor").Invoke(floor, null);
            yield return null;
            Assert.That(floor.GetType().GetProperty("CurrentFloor").GetValue(floor), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SelectingSkillThenExecutingUsesCoreCostsAndRestoresField()
        {
            Assert.That(BeginBattle(), Is.True, "실제 맵에서 단독 전투 배치를 확보해야 합니다.");
            yield return new WaitForSeconds(1.1f);
            Assert.That((bool)_battle.GetType().GetProperty("CanChooseAction").GetValue(_battle), Is.True);
            BattleSnapshot before = Snapshot();
            Click("SkillButton_QuickSlash");
            Assert.That(Snapshot().TurnId, Is.EqualTo(before.TurnId), "선택만으로 행동하면 안 됩니다.");
            Assert.That(_hud.GetType().GetProperty("SelectedSkill").GetValue(_hud), Is.EqualTo(1));
            Click("MemoryThrowPanel");
            yield return null;
            Assert.That(Text("Runtime Investment"), Does.Contain("비용 20"));
            Assert.That(Text("SkillNameText"), Is.EqualTo("테스트_각인"));
            Click("Use Selected Skill");
            BattleSnapshot after = Snapshot();
            Assert.That(after.Phase, Is.EqualTo(BattlePhase.AwaitingPresentation));
            Assert.That(after.Combatants.Single(unit => unit.InstanceId == "player").Memory,
                Is.EqualTo(before.Combatants.Single(unit => unit.InstanceId == "player").Memory - 35));
            Assert.That(after.Combatants.Single(unit => unit.InstanceId == "monster").Hp,
                Is.LessThan(before.Combatants.Single(unit => unit.InstanceId == "monster").Hp));
            yield return null;
            Assert.That(GameObject.Find("PlayerTurnUI"), Is.Null, "연출 중에는 조작 패널이 숨겨져야 합니다.");
            _battle.GetType().GetMethod("CancelBattle").Invoke(_battle, null);
            yield return null;
            Assert.That(GameObject.Find("Default_State"), Is.Not.Null);
            Assert.That(GameObject.Find("Battle_State"), Is.Null);
            Assert.That(Text("Runtime Field Vitals"), Does.Contain("200/200"));
        }

        [UnityTest]
        public IEnumerator UnaffordableInvestmentDisablesActionsAndShowsLocks()
        {
            _playerState.SetVitals(1000, 10);
            Assert.That(BeginBattle(), Is.True);
            yield return new WaitForSeconds(1.1f);
            _battle.GetType().GetMethod("SetInvestmentStage").Invoke(_battle, new object[] { 5 });
            yield return null;
            Component execute = GameObject.Find("Use Selected Skill").GetComponent("Button");
            Component defend = GameObject.Find("HUD Defend").GetComponent("Button");
            Assert.That(execute.GetType().GetProperty("interactable").GetValue(execute), Is.False);
            Assert.That(defend.GetType().GetProperty("interactable").GetValue(defend), Is.False);
            Transform investment = GameObject.Find("MemoryThrowPanel").transform;
            Assert.That(investment.Find("MemorySlot_05/Lock").gameObject.activeSelf, Is.True);
            Assert.That(Text("Runtime Investment"), Does.Contain("비용 100"));
        }

        [UnityTest]
        public IEnumerator ExistingArtworkAndPanelsFitSupportedViewportsWithoutCovers()
        {
            GameObject field = (GameObject)_battle.GetType().GetField("_fieldUI", PrivateInstance).GetValue(_battle);
            Canvas canvas = field.GetComponentInParent<Canvas>();
            Assert.That(canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name.EndsWith("Backdrop")), Is.False);
            Component panel = canvas.GetComponentsInChildren<Component>(true)
                .Single(c => c.name == "SkillPanel" && c.GetType().Name == "Image");
            UnityEngine.Object sprite = (UnityEngine.Object)panel.GetType().GetProperty("sprite").GetValue(panel);
            Assert.That(sprite.name, Is.EqualTo("Turn_SkillPanel_Shell_BlankNumber"));
            ((Behaviour)canvas.GetComponent("CanvasScaler")).enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform root = (RectTransform)canvas.transform;
            string[] panels = { "01_Location", "02_Floor_Banner", "06_Student_Card", "09_Story_Panel", "10_Minimap",
                "CombatHeader", "EnemyNameBanner", "WaveTurnControls", "EnemyCard", "SkillPanel", "SkillDescriptionPanel",
                "MemoryThrowPanel", "Use Selected Skill", "HUD Defend", "PlayerPortrait", "Runtime Field Vitals" };
            foreach (Vector2 size in new[] { new Vector2(1920, 1080), new Vector2(1920, 1440), new Vector2(2520, 1080) })
            {
                root.sizeDelta = size;
                Canvas.ForceUpdateCanvases();
                foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true).Where(r => panels.Contains(r.name)))
                {
                    Vector3[] corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 local = root.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(root.rect.xMin - 1, root.rect.xMax + 1), rect.name + " / " + size);
                        Assert.That(local.y, Is.InRange(root.rect.yMin - 1, root.rect.yMax + 1), rect.name + " / " + size);
                    }
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MinimapLimitsRenderingAndStopsInBattle()
        {
            Camera map = GameObject.Find("Field Minimap Camera").GetComponent<Camera>();
            int renders = 0;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < 1f)
            {
                yield return new WaitForEndOfFrame();
                if (map.enabled)
                {
                    renders++;
                }
            }
            Assert.That(renders, Is.InRange(1, 11));
            Assert.That(BeginBattle(), Is.True);
            yield return new WaitForEndOfFrame();
            Assert.That(map.enabled, Is.False);
        }

        private bool BeginBattle()
        {
            MonoBehaviour enemy = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(item => item.GetType().Name == "EnemyStateMachine" && item.name == "emey AI");
            CharacterController player = GameObject.Find("Fuchsia Player").GetComponent<CharacterController>();
            Array members = Array.CreateInstance(enemy.GetType(), 1);
            members.SetValue(enemy, 0);
            for (int index = 0; index < 8; index++)
            {
                NavMeshHit hit;
                Vector3 offset = Quaternion.Euler(0f, index * 45f, 0f) * Vector3.forward * 2f;
                if (!NavMesh.SamplePosition(enemy.transform.position + offset, out hit, 1f, NavMesh.AllAreas))
                {
                    continue;
                }
                player.enabled = false;
                player.transform.position = hit.position + Vector3.up * 0.05f;
                player.enabled = true;
                Physics.SyncTransforms();
                _battle.GetType().GetMethod("CancelBattle").Invoke(_battle, null);
                bool entered = (bool)_battle.GetType().GetMethod("BeginBattle").Invoke(_battle,
                    new object[] { enemy, BattleEntryCondition.PlayerInitiated, members });
                if (entered)
                {
                    return true;
                }
            }
            return false;
        }

        private BattleSnapshot Snapshot()
        {
            return (BattleSnapshot)_battle.GetType().GetProperty("Snapshot").GetValue(_battle);
        }

        private static void Click(string name)
        {
            Component button = GameObject.Find(name).GetComponent("Button");
            ((UnityEvent)button.GetType().GetProperty("onClick").GetValue(button)).Invoke();
        }

        private static string Text(string name)
        {
            Component text = GameObject.Find(name).GetComponent("TextMeshProUGUI");
            return (string)text.GetType().GetProperty("text").GetValue(text);
        }
    }
}
