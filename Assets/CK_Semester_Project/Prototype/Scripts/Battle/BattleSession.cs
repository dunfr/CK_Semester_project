using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class BattleSession
    {
        private readonly IBattleActionResolver _resolver;
        private readonly BattleRules _rules;
        private readonly Random _random;
        private readonly Dictionary<string, CombatantState> _combatants = new Dictionary<string, CombatantState>();
        private readonly List<string> _roster = new List<string>();
        private readonly HashSet<string> _remaining = new HashSet<string>();
        private readonly List<string> _turnOrder = new List<string>();
        private BattlePhase _phase;
        private BattleOutcome _outcome;
        private BattleEntryCondition _entryCondition;
        private string _currentActorId;
        private int _round;
        private long _turnId;

        public BattleActionResult PendingResult { get; private set; }
        public BattleRules Rules => _rules;

        public BattleSession(IBattleActionResolver resolver = null, int? randomSeed = null, BattleRules rules = null)
        {
            _rules = (resolver as BattleActionResolver)?.Rules ?? rules ?? new BattleRules();
            _resolver = resolver ?? new BattleActionResolver(_rules);
            _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        }

        public BattleSnapshot GetSnapshot()
        {
            return new BattleSnapshot(_phase, _outcome, _entryCondition, _round, _turnId,
                _currentActorId, _roster.Select(id => _combatants[id]), _turnOrder);
        }

        public BattleSnapshot Start(IEnumerable<BattleParticipant> participants,
            BattleEntryCondition entryCondition = BattleEntryCondition.Normal)
        {
            if (_phase != BattlePhase.NotStarted)
            {
                throw new InvalidOperationException("시작한 전투는 재초기화할 수 없습니다. 새 세션을 생성하세요.");
            }
            BattleParticipant[] roster = (participants
                ?? throw new ArgumentNullException(nameof(participants))).ToArray();
            if (!Enum.IsDefined(typeof(BattleEntryCondition), entryCondition)
                || roster.Any(participant => participant == null)
                || roster.Select(participant => participant.InstanceId).Distinct(StringComparer.Ordinal).Count() != roster.Length
                || !roster.Any(participant => participant.Data.Team == BattleTeam.Player)
                || !roster.Any(participant => participant.Data.Team == BattleTeam.Monster))
            {
                throw new ArgumentException("양 팀의 개체, 고유한 개체 ID, 유효한 진입 조건이 필요합니다.", nameof(participants));
            }

            foreach (BattleParticipant participant in roster)
            {
                _roster.Add(participant.InstanceId);
                _combatants.Add(participant.InstanceId, new CombatantState(participant.InstanceId,
                    participant.Data, participant.InitialHp, participant.InitialMemory, participant.SkippedTurns, rageEnergy: participant.InitialRageEnergy));
            }
            _entryCondition = entryCondition;
            _outcome = EvaluateOutcome();
            if (_outcome != BattleOutcome.None)
            {
                _phase = BattlePhase.Finished;
                return GetSnapshot();
            }

            BeginRound();
            SelectNextActor();
            return GetSnapshot();
        }

        public IReadOnlyList<string> GetSelectableTargets(string skillId)
        {
            if (_phase != BattlePhase.AwaitingAction)
            {
                return Array.Empty<string>();
            }
            CombatantState actor = _combatants[_currentActorId];
            SkillData skill = actor.Data.Skills.FirstOrDefault(data => data.Id == skillId);
            if (skill == null)
            {
                return Array.Empty<string>();
            }
            return Array.AsReadOnly(_roster.Where(id => IsValidTarget(actor, _combatants[id], skill)).ToArray());
        }

        public bool TrySubmit(BattleActionRequest request, out BattleActionResult result,
            out BattleActionError error)
        {
            result = null;
            error = ValidateRequest(request);
            if (error != BattleActionError.None)
            {
                return false;
            }

            error = _resolver.Resolve(GetSnapshot(), request, out IReadOnlyList<BattleEffect> effects);
            if (error != BattleActionError.None)
            {
                return false;
            }
            // 잘못된 계산 결과가 일부만 적용되지 않도록 모든 효과를 먼저 검증한다.
            List<BattleStateChange> changes = BuildChanges(effects);
            foreach (BattleStateChange change in changes)
            {
                _combatants[change.After.InstanceId] = change.After;
            }
            CompleteAction(request, false, changes);
            result = PendingResult;
            return true;
        }

        public bool CompletePresentation(long actionId)
        {
            if (_phase != BattlePhase.AwaitingPresentation || PendingResult == null
                || PendingResult.ActionId != actionId)
            {
                return false;
            }
            PendingResult = null;
            _currentActorId = null;
            if (_outcome != BattleOutcome.None)
            {
                _phase = BattlePhase.Finished;
                _turnOrder.Clear();
                return true;
            }

            if (_remaining.Count == 0)
            {
                BeginRound();
            }
            SelectNextActor();
            return true;
        }

        public BattleActionError ValidateRequest(BattleActionRequest request)
        {
            if (_phase != BattlePhase.AwaitingAction)
            {
                return BattleActionError.InvalidPhase;
            }
            if (request == null)
            {
                return BattleActionError.InvalidAction;
            }
            if (request.TurnId != _turnId)
            {
                return BattleActionError.StaleTurn;
            }
            if (request.ActorId != _currentActorId || !_combatants[_currentActorId].CanAct)
            {
                return BattleActionError.InvalidActor;
            }
            CombatantState actor = _combatants[_currentActorId];
            if (request.MemoryInvestment < 0 || request.MemoryInvestment > actor.Memory)
            {
                return BattleActionError.InvalidMemoryInvestment;
            }
            if (!Enum.IsDefined(typeof(BattleActionKind), request.Kind))
            {
                return BattleActionError.InvalidAction;
            }
            if (request.Kind != BattleActionKind.Skill)
            {
                if (request.SkillId != null || request.TargetId != null
                    || (request.Kind == BattleActionKind.Wait && request.MemoryInvestment != 0))
                {
                    return BattleActionError.InvalidAction;
                }
                return BattleActionError.None;
            }

            SkillData skill = actor.Data.Skills.FirstOrDefault(data => data.Id == request.SkillId);
            if (skill == null)
            {
                return BattleActionError.UnknownSkill;
            }
            if (_resolver is BattleActionResolver && request.MemoryInvestment >= _rules.InvestmentMultipliers.Count)
            {
                return BattleActionError.InvalidMemoryInvestment;
            }
            if (request.TargetId == null || !_combatants.TryGetValue(request.TargetId, out CombatantState target)
                || !IsValidTarget(actor, target, skill))
            {
                return BattleActionError.InvalidTarget;
            }
            if ((long)skill.MemoryCost + request.MemoryInvestment > actor.Memory)
            {
                return BattleActionError.InsufficientMemory;
            }
            return BattleActionError.None;
        }

        private static bool IsValidTarget(CombatantState actor, CombatantState target, SkillData skill)
        {
            if (target.IsDead)
            {
                return false;
            }
            switch (skill.Target)
            {
                case SkillTarget.Enemy:
                    return actor.Data.Team != target.Data.Team;
                case SkillTarget.Ally:
                    return actor.Data.Team == target.Data.Team;
                case SkillTarget.Self:
                    return actor.InstanceId == target.InstanceId;
                default:
                    return false;
            }
        }

        private List<BattleStateChange> BuildChanges(IReadOnlyList<BattleEffect> effects)
        {
            if (effects == null)
            {
                throw new InvalidOperationException("행동 실행기가 효과 목록을 반환하지 않았습니다.");
            }
            var seen = new HashSet<string>();
            var changes = new List<BattleStateChange>();
            foreach (BattleEffect effect in effects)
            {
                if (effect == null || effect.TargetId == null || !seen.Add(effect.TargetId)
                    || !_combatants.TryGetValue(effect.TargetId, out CombatantState before)
                    || before.IsDead || effect.SkippedTurns < 0)
                {
                    throw new InvalidOperationException("행동 실행기의 효과 대상 또는 상태가 잘못되었습니다.");
                }
                // long으로 합산해 큰 피해·회복 값이 int 오버플로로 반전되는 것을 방지한다.
                int hp = Clamp((long)before.Hp + effect.HpDelta, before.Data.MaxHp);
                int memory = Clamp((long)before.Memory + effect.MemoryDelta, before.Data.MaxMemory);
                int skippedTurns = hp == 0 ? 0 : effect.SkippedTurns ?? before.SkippedTurns;
                var after = new CombatantState(before.InstanceId, before.Data, hp, memory, skippedTurns,
                    effect.IsDefending ?? before.IsDefending, Clamp((long)before.RageEnergy + effect.RageDelta, 100));
                changes.Add(new BattleStateChange(before, after));
            }
            return changes;
        }

        private static int Clamp(long value, int maximum)
        {
            return (int)Math.Max(0L, Math.Min(maximum, value));
        }

        private void BeginRound()
        {
            _round++;
            _remaining.Clear();
            foreach (string id in _roster)
            {
                if (!_combatants[id].IsDead)
                {
                    _remaining.Add(id);
                }
            }
            RebuildOrder();
        }

        private void RebuildOrder()
        {
            _remaining.RemoveWhere(id => _combatants[id].IsDead);
            List<string> candidates = _roster.Where(id => _remaining.Contains(id)).ToList();
            // 비교 함수에서 난수를 뽑으면 정렬 규칙이 깨지므로 미리 섞고 안정 정렬한다.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int other = _random.Next(i + 1);
                string previous = candidates[i];
                candidates[i] = candidates[other];
                candidates[other] = previous;
            }
            _turnOrder.Clear();
            _turnOrder.AddRange(candidates.OrderByDescending(id => _combatants[id].Memory)
                .ThenBy(id => _combatants[id].Data.Team == BattleTeam.Player ? 0 : 1));
        }

        private void SelectNextActor()
        {
            _currentActorId = _turnOrder[0];
            _turnId++;
            _phase = BattlePhase.AwaitingAction;
            CombatantState actor = _combatants[_currentActorId];
            // 방어는 다음 자기 턴 시작 시 만료되며 행동 불능 턴에도 연장되지 않는다.
            if (actor.IsDefending)
            {
                actor = new CombatantState(actor.InstanceId, actor.Data, actor.Hp, actor.Memory,
                    actor.SkippedTurns, rageEnergy: actor.RageEnergy);
                _combatants[actor.InstanceId] = actor;
            }
            if (actor.SkippedTurns == 0)
            {
                return;
            }

            // 행동 불능도 한 번의 결과로 전달해 UI가 표시할 수 있게 한다. 재귀 진행을 피한다.
            var after = new CombatantState(actor.InstanceId, actor.Data, actor.Hp, actor.Memory, actor.SkippedTurns - 1, rageEnergy: actor.RageEnergy);
            _combatants[actor.InstanceId] = after;
            var request = new BattleActionRequest(_turnId, actor.InstanceId, BattleActionKind.Wait);
            CompleteAction(request, true, new[] { new BattleStateChange(actor, after) });
        }

        private void CompleteAction(BattleActionRequest request, bool wasSkipped,
            IEnumerable<BattleStateChange> changes)
        {
            _remaining.Remove(_currentActorId);
            _outcome = EvaluateOutcome();
            _phase = BattlePhase.AwaitingPresentation;
            RebuildOrder();
            if (_outcome != BattleOutcome.None)
            {
                _turnOrder.Clear();
            }
            PendingResult = new BattleActionResult(_turnId, request, wasSkipped, _outcome, changes);
        }

        private BattleOutcome EvaluateOutcome()
        {
            bool hasPlayer = _combatants.Values.Any(state => !state.IsDead && state.Data.Team == BattleTeam.Player);
            bool hasMonster = _combatants.Values.Any(state => !state.IsDead && state.Data.Team == BattleTeam.Monster);
            if (!hasPlayer && !hasMonster)
            {
                return BattleOutcome.Draw;
            }
            if (!hasPlayer)
            {
                return BattleOutcome.Defeat;
            }
            return hasMonster ? BattleOutcome.None : BattleOutcome.Victory;
        }
    }
}
