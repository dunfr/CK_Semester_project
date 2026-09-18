using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    // 흐름 검증용: Power를 고정 피해로 사용한다. 최종 명중·치명타·배율 공식이 아니다.
    public sealed class PrototypeActionResolver : IBattleActionResolver
    {
        public BattleActionError Resolve(BattleSnapshot snapshot, BattleActionRequest request,
            out IReadOnlyList<BattleEffect> effects)
        {
            effects = Array.Empty<BattleEffect>();
            if (request.MemoryInvestment != 0 || request.Kind == BattleActionKind.Defend)
            {
                return BattleActionError.UnsupportedAction;
            }
            if (request.Kind == BattleActionKind.Wait)
            {
                return BattleActionError.None;
            }
            CombatantState actor = snapshot.Combatants.First(state => state.InstanceId == request.ActorId);
            SkillData skill = actor.Data.Skills.First(data => data.Id == request.SkillId);
            if (skill.Target != SkillTarget.Enemy)
            {
                return BattleActionError.UnsupportedAction;
            }
            effects = new[] { new BattleEffect(request.TargetId, -skill.Power) };
            return BattleActionError.None;
        }
    }
}
