using System.Collections.Generic;

namespace CK.SemesterProject.Battle.Demo
{
    // 코어의 메모리 변화·재정렬을 화면에서 확인하기 위한 샘플 효과다.
    // 정식 속성 효과·메모리 투자 공식으로 사용하지 않는다.
    public sealed class BattleDemoResolver : IBattleActionResolver
    {
        private readonly PrototypeActionResolver _fallback = new PrototypeActionResolver();

        public BattleActionError Resolve(BattleSnapshot snapshot, BattleActionRequest request,
            out IReadOnlyList<BattleEffect> effects)
        {
            if (request.Kind == BattleActionKind.Skill && request.SkillId == "disrupt"
                && request.MemoryInvestment == 0)
            {
                effects = new[] { new BattleEffect(request.TargetId, -12, -8) };
                return BattleActionError.None;
            }
            return _fallback.Resolve(snapshot, request, out effects);
        }
    }
}
