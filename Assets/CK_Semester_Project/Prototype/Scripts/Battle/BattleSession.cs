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
        private string _extraActorId;
        private bool _resumeActorAfterPresentation;

        public BattleActionResult PendingResult { get; private set; }
        public BattleRules Rules => _rules;

        public BattleSession(IBattleActionResolver resolver = null, int? randomSeed = null, BattleRules rules = null)
        {
            _rules = (resolver as BattleActionResolver)?.Rules ?? rules ?? new BattleRules();
            _resolver = resolver ?? new BattleActionResolver(_rules, randomSeed);
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
                    participant.Data, participant.InitialHp,
                    (int)Math.Min(int.MaxValue, (long)participant.InitialMemory +
                        (entryCondition == BattleEntryCondition.MonsterCollision && participant.Data.Team == BattleTeam.Monster
                            ? _rules.MonsterCollisionMemoryBonus : 0)), participant.SkippedTurns,
                    rageEnergy: participant.InitialRageEnergy, isOverheated: _rules.Mechanics.EnableRage
                        && participant.InitialRageEnergy >= BattleCombatRules.OverheatThreshold));
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

            BattleHitResult hit = null;
            IReadOnlyList<BattleEffect> effects;
            error = _resolver is BattleActionResolver common
                ? common.ResolveDetailed(GetSnapshot(), request, out effects, out hit)
                : _resolver.Resolve(GetSnapshot(), request, out effects);
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
            CompleteAction(request, false, changes, hit);
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
            if (_outcome != BattleOutcome.None)
            {
                _phase = BattlePhase.Finished;
                _currentActorId = null;
                _resumeActorAfterPresentation = false;
                _turnOrder.Clear();
                return true;
            }

            if (_resumeActorAfterPresentation)
            {
                _resumeActorAfterPresentation = false;
                _turnId++;
                _phase = BattlePhase.AwaitingAction;
                return true;
            }
            _currentActorId = null;
            if (_remaining.Count == 0 && _extraActorId == null)
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
            int investment = request.MemoryInvestment;
            if (_resolver is BattleActionResolver && !_rules.TryGetInvestment(actor.Data, request, out investment, out _, out _))
            {
                return BattleActionError.InvalidMemoryInvestment;
            }
            if (investment < 0 || investment > actor.Memory)
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
                    || (request.Kind == BattleActionKind.Wait && (investment != 0 || (request.InvestmentStage ?? 0) != 0)))
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
            if (request.TargetId == null || !_combatants.TryGetValue(request.TargetId, out CombatantState target)
                || !IsValidTarget(actor, target, skill))
            {
                return BattleActionError.InvalidTarget;
            }
            if ((long)skill.MemoryCost + investment > actor.Memory)
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
                    || before.IsDead || effect.SkippedTurns < 0
                    || effect.ChainStep < 0 || effect.ChainStep > 3 || effect.ImprintDamage < 0)
                {
                    throw new InvalidOperationException("행동 실행기의 효과 대상 또는 상태가 잘못되었습니다.");
                }
                // long으로 합산해 큰 피해·회복 값이 int 오버플로로 반전되는 것을 방지한다.
                int hp = Clamp((long)before.Hp + effect.HpDelta, before.Data.MaxHp);
                int memory = Clamp((long)before.Memory + effect.MemoryDelta, Math.Max(before.Memory, before.Data.MaxMemory));
                int skippedTurns = hp == 0 ? 0 : effect.SkippedTurns ?? before.SkippedTurns;
                var after = new CombatantState(before.InstanceId, before.Data, hp, memory, skippedTurns,
                    effect.IsDefending ?? before.IsDefending, Clamp((long)before.RageEnergy + effect.RageDelta, 100),
                    effect.ChainStep ?? before.ChainStep, effect.ImprintDamage ?? before.ImprintDamage,
                    _rules.Mechanics.EnableRage && (long)before.RageEnergy + effect.RageDelta >= BattleCombatRules.OverheatThreshold, before.HasMemoryLoss);
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
                    CombatantState unit = _combatants[id];
                    if (_round > 1 && _rules.EnableMemoryLoss && unit.Memory == 0 && unit.Data.InitialMemory > 0)
                    {
                        _combatants[id] = new CombatantState(id, unit.Data, unit.Hp, unit.Memory,
                            unit.SkippedTurns, unit.IsDefending, unit.RageEnergy, unit.ChainStep,
                            unit.ImprintDamage, unit.IsOverheated, hasMemoryLoss: true);
                    }
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
            if (_extraActorId != null && !_combatants[_extraActorId].IsDead)
            {
                _turnOrder.Remove(_extraActorId);
                _turnOrder.Insert(0, _extraActorId);
            }
        }

        private void SelectNextActor()
        {
            _currentActorId = _extraActorId ?? _turnOrder[0];
            _extraActorId = null;
            _turnId++;
            _phase = BattlePhase.AwaitingAction;
            CombatantState actor = _combatants[_currentActorId];
            bool overheated = _rules.Mechanics.EnableRage && actor.RageEnergy >= BattleCombatRules.OverheatThreshold;
            bool skipped = actor.SkippedTurns > 0 || overheated || actor.HasMemoryLoss;
            int hp = Math.Max(0, actor.Hp - actor.ImprintDamage);
            var after = new CombatantState(actor.InstanceId, actor.Data, hp,
                actor.HasMemoryLoss && hp > 0 ? Clamp((long)actor.Memory + actor.Data.InitialMemory, actor.Data.MaxMemory) : actor.Memory,
                hp == 0 ? 0 : Math.Max(0, actor.SkippedTurns - 1), false,
                overheated ? 0 : actor.RageEnergy, actor.ChainStep, 0);
            if (!actor.IsDefending && actor.ImprintDamage == 0 && !skipped)
            {
                return;
            }
            _combatants[actor.InstanceId] = after;
            if (actor.ImprintDamage == 0 && !skipped)
            {
                return;
            }
            // 각인은 선택 전에 별도 결과로 전달한다. 살아 있고 행동 가능하면 연출 후 같은 행동 기회를 이어간다.
            bool consumesTurn = skipped || after.IsDead;
            _resumeActorAfterPresentation = !consumesTurn;
            var request = new BattleActionRequest(_turnId, actor.InstanceId, BattleActionKind.Wait);
            CompleteAction(request, consumesTurn, new[] { new BattleStateChange(actor, after) },
                isTurnStartEffect: true, consumesTurn: consumesTurn);
        }

        private void CompleteAction(BattleActionRequest request, bool wasSkipped,
            IEnumerable<BattleStateChange> changes, BattleHitResult hit = null,
            bool isTurnStartEffect = false, bool consumesTurn = true)
        {
            if (consumesTurn)
            {
                _remaining.Remove(_currentActorId);
            }
            _outcome = EvaluateOutcome();
            if (hit != null && hit.GrantsExtraAction && !_combatants[_currentActorId].IsDead
                && _outcome == BattleOutcome.None)
            {
                _extraActorId = _currentActorId;
            }
            _phase = BattlePhase.AwaitingPresentation;
            if (consumesTurn)
            {
                RebuildOrder();
            }
            if (_outcome != BattleOutcome.None)
            {
                _turnOrder.Clear();
                _extraActorId = null;
            }
            PendingResult = new BattleActionResult(_turnId, request, wasSkipped, _outcome, changes,
                hit, isTurnStartEffect);
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
