using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CK.SemesterProject.Battle.Demo
{
    // 입력과 연출을 실제 BattleSession 계약에 연결하는 검증용 어댑터.
    public sealed class BattleDemoController : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("한 행동의 연출 시간(초)")]
        private float _presentationSeconds = 1.1f;

        private BattleSession _session;
        private readonly List<string> _history = new List<string>();
        private float _phaseElapsed;
        private long _recordedActionId;
        private int _scenario;
        private string _skillId = "strike";
        private string _targetId = "sentinel_a";
        private bool _autoAdvance = true;

        public BattleSnapshot Snapshot { get; private set; }
        public BattleActionResult PendingResult => _session.PendingResult;
        public IReadOnlyList<string> History => _history;
        public int Scenario => _scenario;
        public int MemoryInvestment { get; private set; }

        public void CycleInvestment()
        {
            if (IsPlayerInput)
            {
                MemoryInvestment = (MemoryInvestment + 1) % 4;
            }
        }

        public bool Defend()
        {
            return IsPlayerInput && Submit(new BattleActionRequest(Snapshot.TurnId, Snapshot.CurrentActorId,
                BattleActionKind.Defend, memoryInvestment: MemoryInvestment));
        }
        public string SelectedSkillId => _skillId;
        public string SelectedTargetId => _targetId;
        public float PresentationProgress => Mathf.Clamp01(_phaseElapsed / _presentationSeconds);
        public bool AutoAdvance => _autoAdvance;
        public bool IsPlayerInput => Snapshot.Phase == BattlePhase.AwaitingAction
            && Snapshot.CurrentActorId == "player";
        public BattleActionError LastError { get; private set; }

        private void Awake()
        {
            RestartScenario(0);
        }

        private void Update()
        {
            _phaseElapsed += Time.unscaledDeltaTime;
            if (!_autoAdvance)
            {
                return;
            }
            if (Snapshot.Phase == BattlePhase.AwaitingPresentation && _phaseElapsed >= _presentationSeconds)
            {
                Advance();
            }
            else if (Snapshot.Phase == BattlePhase.AwaitingAction && !IsPlayerInput && _phaseElapsed >= 0.7f)
            {
                Advance();
            }
        }

        public void RestartScenario(int scenario)
        {
            _scenario = Mathf.Clamp(scenario, 0, 2);
            var strike = new SkillData("strike", "기본 공격", 24, BattleElement.Afterimage, memoryRecovery: 3);
            var heavy = new SkillData("heavy", "강타", 36, BattleElement.Imprint, memoryCost: 2);
            var disrupt = new SkillData("disrupt", "메모리 교란", 12, BattleElement.Oblivion, memorySteal: 8);
            var enemyStrike = new SkillData("claw", "타격", 13);
            var player = new CombatantData("demo_player", "플레이어", BattleTeam.Player,
                140, 40, 24, new[] { strike, heavy, disrupt });
            int enemyMemory = _scenario == 1 ? 24 : 18;
            var enemy = new CombatantData("demo_sentinel", "센티널", BattleTeam.Monster,
                72, 40, enemyMemory, new[] { enemyStrike });

            // 동률 시드와 전투 수치는 데모 재현용이다. 밸런스 확정값이 아니다.
            _session = new BattleSession(randomSeed: 17, rules: new BattleRules(new[] { 1.0, 1.1, 1.2, 1.35 }, 5));
            _history.Clear();
            _recordedActionId = 0;
            MemoryInvestment = 0;
            _skillId = "strike";
            _targetId = "sentinel_a";
            LastError = BattleActionError.None;
            _session.Start(new[]
            {
                new BattleParticipant("player", player, skippedTurns: _scenario == 2 ? 1 : 0, initialRageEnergy: 50),
                new BattleParticipant("sentinel_a", enemy),
                new BattleParticipant("sentinel_b", enemy, initialMemory: _scenario == 1 ? 24 : 14)
            });
            AddHistory("전투 시작 · 메모리가 높은 순서로 행동합니다.");
            Sync();
        }

        public void SetAutoAdvance(bool enabled)
        {
            _autoAdvance = enabled;
        }

        public bool SelectSkill(string skillId)
        {
            if (!IsPlayerInput || !Snapshot.Combatants[0].Data.Skills.Any(skill => skill.Id == skillId))
            {
                return false;
            }
            _skillId = skillId;
            return true;
        }

        public bool SelectTarget(string targetId)
        {
            if (!IsPlayerInput || !_session.GetSelectableTargets(_skillId).Contains(targetId))
            {
                return false;
            }
            _targetId = targetId;
            return true;
        }

        public bool Attack()
        {
            if (!IsPlayerInput)
            {
                return false;
            }
            return Submit(new BattleActionRequest(Snapshot.TurnId, Snapshot.CurrentActorId,
                BattleActionKind.Skill, _skillId, _targetId, MemoryInvestment));
        }

        public bool Wait()
        {
            if (!IsPlayerInput)
            {
                return false;
            }
            return Submit(new BattleActionRequest(Snapshot.TurnId, Snapshot.CurrentActorId, BattleActionKind.Wait));
        }

        public bool Advance()
        {
            if (Snapshot.Phase == BattlePhase.AwaitingPresentation)
            {
                bool accepted = _session.CompletePresentation(_session.PendingResult.ActionId);
                Sync();
                return accepted;
            }
            if (Snapshot.Phase != BattlePhase.AwaitingAction || IsPlayerInput)
            {
                return false;
            }
            CombatantState actor = Snapshot.Combatants.First(state => state.InstanceId == Snapshot.CurrentActorId);
            // 후속 몬스터 AI를 대신하는 데모 입력: 첫 스킬로 살아 있는 플레이어 공격.
            string skillId = actor.Data.Skills[0].Id;
            string target = _session.GetSelectableTargets(skillId).First();
            return Submit(new BattleActionRequest(Snapshot.TurnId, actor.InstanceId,
                BattleActionKind.Skill, skillId, target));
        }

        public string GetName(string id)
        {
            switch (id)
            {
                case "player": return "플레이어";
                case "sentinel_a": return "센티널 A";
                case "sentinel_b": return "센티널 B";
                default: return "—";
            }
        }

        public CombatantState GetDisplayedState(string id)
        {
            BattleActionResult result = PendingResult;
            if (result != null && PresentationProgress < 0.4f)
            {
                BattleStateChange change = result.Changes.FirstOrDefault(item => item.Before.InstanceId == id);
                if (change != null)
                {
                    return change.Before;
                }
            }
            return Snapshot.Combatants.First(state => state.InstanceId == id);
        }

        private bool Submit(BattleActionRequest request)
        {
            bool accepted = _session.TrySubmit(request, out _, out BattleActionError error);
            LastError = error;
            if (accepted)
            {
                Sync();
            }
            else
            {
                AddHistory("행동 요청 거절: " + error);
            }
            return accepted;
        }

        private void Sync()
        {
            Snapshot = _session.GetSnapshot();
            _phaseElapsed = 0f;
            if (IsPlayerInput && !_session.GetSelectableTargets(_skillId).Contains(_targetId))
            {
                _targetId = _session.GetSelectableTargets(_skillId).FirstOrDefault();
            }
            BattleActionResult result = PendingResult;
            if (result == null || result.ActionId == _recordedActionId)
            {
                return;
            }
            _recordedActionId = result.ActionId;
            string actor = GetName(result.Request.ActorId);
            if (result.WasSkipped)
            {
                AddHistory(actor + " · 행동 불능으로 이번 턴을 건너뜁니다.");
            }
            else if (result.Request.Kind == BattleActionKind.Wait)
            {
                AddHistory(actor + " · 대기");
            }
            if (result.Request.Kind == BattleActionKind.Defend)
            {
                AddHistory(actor + " · 방어 / 받는 피해 50% 감소");
            }
            foreach (BattleStateChange change in result.Changes)
            {
                if (change.HpDelta < 0)
                {
                    AddHistory(actor + " → " + GetName(change.After.InstanceId) + "  " + -change.HpDelta + " 피해"
                        + (change.BecameDead ? " · 사망" : ""));
                }
                if (change.MemoryDelta != 0)
                {
                    AddHistory(GetName(change.After.InstanceId) + " · 메모리 " + change.MemoryDelta + " / 순서 재계산");
                }
            }
        }

        private void AddHistory(string message)
        {
            _history.Insert(0, message);
            if (_history.Count > 5)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }
    }
}
