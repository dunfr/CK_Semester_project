using System.Collections.Generic;

namespace CK.SemesterProject.Battle
{
    public interface IBattleActionResolver
    {
        // 입력은 불변이다. 외부 상태를 변경하지 않고 개체별 한 개의 효과를 반환한다.
        BattleActionError Resolve(BattleSnapshot snapshot, BattleActionRequest request,
            out IReadOnlyList<BattleEffect> effects);
    }
}
