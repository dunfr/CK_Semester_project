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
        private TutorialBattleTestArena _arena;

        // Tutorial 코드는 기존 Assembly-CSharp에 있어 asmdef 테스트에서는 런타임 타입으로 접근한다.
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Assets/CK_Semester_Project/Prototype/Scenes/TutorialDummyModelingDemo.unity");
            yield return null;
            _battle = GameObject.Find("Tutorial Battle").GetComponent("TutorialBattleController");
            _hud = _battle.GetComponent("PrototypeHudPresenter");
            _battle.GetType().GetField("_automaticEncounters", PrivateInstance).SetValue(_battle, false);
            _playerState = (PlayerRuntimeState)Type.GetType("CK.SemesterProject.Tutorial.PlayerSessionState, Assembly-CSharp")
                .GetProperty("Current").GetValue(null);
            _playerState.RestoreFull();
            _arena = new TutorialBattleTestArena(_battle);
            yield return null;
            yield return null;
            _hud.GetType().GetField("_bannerStartedAt", PrivateInstance).SetValue(_hud, Time.unscaledTime);
        }

        [UnityTearDown]
        public IEnumerator RestoreScene()
        {
            if (_battle != null)
            {
                _battle.GetType().GetMethod("CancelBattle").Invoke(_battle, null);
            }
            _arena?.Dispose();
            _arena = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FieldVitalsAndMovementRemainWithoutUnavailableControls()
        {
            _playerState.SetVitals(731, 143);
            yield return null;
            Assert.That(Text("Runtime Field Vitals"), Does.Contain("731/1000").And.Contain("143/200"));
            Assert.That(GameObject.Find("Fuchsia Player").GetComponent("FloorTravelButtons"), Is.Null);
            string[] unavailable = { "Travel First Floor", "Travel Second Floor", "03_Menu_C", "04_Menu_J",
                "05_Menu_ESC", "07_Tab_Button", "08_R_Button", "11_Interact_E",
                "SkillButton_LockedBoundary", "SkillDetailsPrompt" };
            Transform[] ui = _hud.GetType().GetField("_fieldRoot", PrivateInstance).GetValue(_hud)
                is GameObject field ? field.GetComponentInParent<Canvas>().GetComponentsInChildren<Transform>(true) : null;
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.Any(item => unavailable.Contains(item.name)), Is.False);
            Component movement = GameObject.Find("Fuchsia Player").GetComponent("PlayerMovement");
            Assert.That(((Behaviour)movement).enabled, Is.True);
            Vector3 destination = movement.transform.position + Vector3.right * 3f;
            movement.GetType().GetMethod("Teleport").Invoke(movement, new object[] { destination });
            yield return null;
            Assert.That(Vector3.ProjectOnPlane(movement.transform.position - destination, Vector3.up).magnitude,
                Is.LessThan(.01f));
            Assert.That(Text("Runtime Location"), Does.Contain("더미 맵"));
            Assert.That(GameObject.Find("Tutorial Level Content"), Is.Null);
        }

        [UnityTest]
        public IEnumerator AreaBannerFadesAndSprintButtonResetsOnDisable()
        {
            CanvasGroup banner = GameObject.Find("02_Floor_Banner").GetComponent<CanvasGroup>();
            Assert.That(banner.alpha, Is.EqualTo(1f));
            Assert.That(banner.blocksRaycasts, Is.False);
            _hud.GetType().GetField("_bannerStartedAt", PrivateInstance).SetValue(_hud, Time.unscaledTime - 3.1f);
            yield return null;
            Assert.That(banner.alpha, Is.InRange(0.35f, 0.65f));
            _hud.GetType().GetField("_bannerStartedAt", PrivateInstance).SetValue(_hud, Time.unscaledTime - 4f);
            yield return null;
            Assert.That(banner.gameObject.activeSelf, Is.False);
            Component movement = GameObject.Find("Fuchsia Player").GetComponent("PlayerMovement");
            Click("12_Sprint_Shift");
            Assert.That(movement.GetType().GetProperty("IsSprintToggled").GetValue(movement), Is.True);
            Click("12_Sprint_Shift");
            Assert.That(movement.GetType().GetProperty("IsSprintToggled").GetValue(movement), Is.False);
            Click("12_Sprint_Shift");
            ((Behaviour)movement).enabled = false;
            Assert.That(movement.GetType().GetProperty("IsSprintToggled").GetValue(movement), Is.False);
            ((Behaviour)movement).enabled = true;
            Assert.That(GameObject.Find("11_Interact_LMB"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SprintMovesFasterAndLeftClickUiStartsNearbyBattle()
        {
            Component movement = GameObject.Find("Fuchsia Player").GetComponent("PlayerMovement");
            MethodInfo move = movement.GetType().GetMethod("Move", PrivateInstance);
            Vector3 start = movement.transform.position;
            move.Invoke(movement, new object[] { Vector2.up, false, false });
            float walkDistance = Vector3.ProjectOnPlane(movement.transform.position - start, Vector3.up).magnitude;
            start = movement.transform.position;
            move.Invoke(movement, new object[] { Vector2.up, true, false });
            float sprintDistance = Vector3.ProjectOnPlane(movement.transform.position - start, Vector3.up).magnitude;
            Assert.That(walkDistance, Is.GreaterThan(0f));
            Assert.That(sprintDistance / walkDistance, Is.EqualTo(1.8f).Within(.05f));
            move.Invoke(movement, new object[] { Vector2.zero, true, false });
            Assert.That(movement.GetType().GetProperty("IsSprinting").GetValue(movement), Is.False);
            Assert.That(BeginBattle(true), Is.True, "좌클릭 UI로 실제 필드 전투가 시작되어야 합니다.");
            yield return null;
            Assert.That(Snapshot(), Is.Not.Null);
        }

        [UnityTest, Category("BattleSkillMenu")]
        public IEnumerator ArrowSelectionOpensDescriptionWithoutExecutingAndWraps()
        {
            Assert.That(BeginBattle(), Is.True);
            yield return new WaitForSeconds(1.1f);
            _battle.GetType().GetMethod("SelectTarget").Invoke(_battle, new object[] { 0 });
            yield return null;
            Transform details = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SelectMany(canvas => canvas.GetComponentsInChildren<Transform>(true)).Single(item => item.name == "SkillDescriptionPanel");
            Assert.That(details.gameObject.activeSelf, Is.False);
            BattleSnapshot before = Snapshot();
            MethodInfo move = _hud.GetType().GetMethod("MoveSkillSelection");
            move.Invoke(_hud, new object[] { 1 });
            Assert.That(_hud.GetType().GetProperty("SelectedSkill").GetValue(_hud), Is.EqualTo(1));
            Assert.That(details.gameObject.activeSelf, Is.True);
            Assert.That(Text("SkillNameText"), Is.EqualTo("테스트_각인"));
            Assert.That(Snapshot(), Is.SameAs(before), "방향키 선택은 비용을 소모하거나 턴을 진행하지 않습니다.");
            move.Invoke(_hud, new object[] { 1 });
            move.Invoke(_hud, new object[] { 1 });
            Assert.That(_hud.GetType().GetProperty("SelectedSkill").GetValue(_hud), Is.EqualTo(0));
            move.Invoke(_hud, new object[] { -1 });
            Assert.That(_hud.GetType().GetProperty("SelectedSkill").GetValue(_hud), Is.EqualTo(2));
            _battle.GetType().GetMethod("CancelBattle").Invoke(_battle, null);
            yield return null;
            Assert.That(BeginBattle(), Is.True);
            yield return new WaitForSeconds(1.1f);
            _battle.GetType().GetMethod("SelectTarget").Invoke(_battle, new object[] { 0 });
            yield return null;
            Assert.That(details.gameObject.activeSelf, Is.False, "다음 전투에는 설명을 닫아야 합니다.");
        }

        [UnityTest, Category("BattleSkillMenu")]
        public IEnumerator EnterKeepsUsingHighlightedSkillWithoutExtraConfirmation()
        {
            Assert.That(BeginBattle(), Is.True);
            yield return new WaitForSeconds(1.1f);
            _battle.GetType().GetMethod("SelectTarget").Invoke(_battle, new object[] { 0 });
            yield return null;
            MethodInfo confirm = _hud.GetType().GetMethod("ExecuteSelectedSkill");
            Assert.That(_hud.GetType().GetProperty("HasSkillSelection").GetValue(_hud), Is.False);
            confirm.Invoke(_hud, null);
            Assert.That(Snapshot().Phase, Is.EqualTo(BattlePhase.AwaitingPresentation));
            yield return null;
            Transform details = (Transform)((GameObject)_hud.GetType().GetField("_skillDetailsPanel", PrivateInstance).GetValue(_hud)).transform;
            Assert.That(details.gameObject.activeSelf, Is.False);
        }

        [UnityTest, Category("BattleSkillMenu")]
        public IEnumerator SelectingSkillThenExecutingUsesCoreCostsAndRestoresField()
        {
            Assert.That(BeginBattle(), Is.True, "테스트 전투 공간에서 단독 전투 배치를 확보해야 합니다.");
            yield return new WaitForSeconds(1.1f);
            _battle.GetType().GetMethod("SelectTarget").Invoke(_battle, new object[] { 0 });
            yield return null;
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

        [UnityTest, Category("BattleSkillMenu")]
        public IEnumerator UnaffordableInvestmentDisablesActionsAndShowsLocks()
        {
            _playerState.SetVitals(1000, 10);
            Assert.That(BeginBattle(), Is.True);
            yield return new WaitForSeconds(1.1f);
            _battle.GetType().GetMethod("SelectTarget").Invoke(_battle, new object[] { 0 });
            yield return null;
            Click("SkillButton_NormalAttack");
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
                "MemoryThrowPanel", "Use Selected Skill", "HUD Defend", "PlayerPortrait", "Runtime Field Vitals", "11_Interact_LMB", "12_Sprint_Shift", "Runtime Interaction Hint Panel", "Runtime Level Feedback Panel", "Runtime Route Hint Panel" };
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

        [UnityTest, Category("BattleSkillMenu")]
        public IEnumerator EnemyClickOpensCommandUiOnlyAfterTargeting()
        {
            Assert.That(BeginBattle(false, false), Is.True);
            yield return new WaitForSeconds(1.1f);
            Transform commands = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SelectMany(canvas => canvas.GetComponentsInChildren<Transform>(true))
                .Single(item => item.name == "PlayerTurnUI");
            Assert.That(commands.gameObject.activeSelf, Is.False);
            Assert.That(GameObject.Find("EnemyName_TMP"), Is.Not.Null);
            Assert.That(GameObject.Find("PlayerStatusPanel"), Is.Not.Null);
            Vector2 emptyPoint = new Vector2(4, 4);
            Assert.That(_battle.GetType().GetMethod("TrySelectBattleTargetAtScreenPoint")
                .Invoke(_battle, new object[] { emptyPoint }), Is.False);
            Assert.That(commands.gameObject.activeSelf, Is.False);
            Transform enemy = GameObject.Find("emey AI").transform;
            Vector3 point = Camera.main.WorldToScreenPoint(enemy.GetComponentInChildren<Collider>().bounds.center);
            Assert.That(_battle.GetType().GetMethod("TrySelectBattleTargetAtScreenPoint")
                .Invoke(_battle, new object[] { (Vector2)point }), Is.True);
            yield return null;
            Assert.That(commands.gameObject.activeSelf, Is.True);
            Assert.That(GameObject.Find("SkillPanel"), Is.Not.Null);
            Assert.That(GameObject.Find("MemoryThrowPanel"), Is.Not.Null);
            Assert.That(GameObject.Find("EnemyCard"), Is.Not.Null);
            _battle.GetType().GetMethod("CancelBattle").Invoke(_battle, null);
            yield return null;
            Assert.That(BeginBattle(false, false), Is.True);
            yield return new WaitForSeconds(1.1f);
            Assert.That(commands.gameObject.activeSelf, Is.False, "새 전투에서 타깃 선택 상태가 초기화되어야 합니다.");
        }

        private bool BeginBattle(bool useFieldUi = false, bool selectTarget = true)
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
                bool entered;
                if (useFieldUi)
                {
                    if (!(bool)_battle.GetType().GetProperty("CanInitiateNearbyBattle").GetValue(_battle))
                    {
                        continue;
                    }
                    Click("11_Interact_LMB");
                    entered = Snapshot() != null;
                }
                else
                {
                    entered = (bool)_battle.GetType().GetMethod("BeginBattle").Invoke(_battle,
                        new object[] { enemy, BattleEntryCondition.PlayerInitiated, members });
                }
                if (entered)
                {
                    if (selectTarget)
                    {
                        _battle.GetType().GetMethod("SelectTarget").Invoke(_battle, new object[] { 0 });
                    }
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
