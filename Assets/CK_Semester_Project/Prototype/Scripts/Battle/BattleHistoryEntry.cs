using System;
using System.Linq;
using System.Text;

namespace CK.SemesterProject.Battle
{
    public sealed class BattleHistoryEntry
    {
        public BattleActionResult Result { get; }
        public string ActorName { get; }
        public string SkillName { get; }
        public int MemorySpent { get; }
        public int PreventedDamage { get; }

        internal BattleHistoryEntry(BattleActionResult result, CombatantState actor, BattleRules rules, BattleSnapshot snapshot)
        {
            Result = result;
            ActorName = actor.Data.DisplayName + " (" + actor.InstanceId + ")";
            SkillData skill = actor.Data.Skills.FirstOrDefault(item => item.Id == result.Request.SkillId);
            SkillName = skill?.DisplayName;
            if (!result.IsTurnStartEffect && !result.WasSkipped && result.Request.Kind != BattleActionKind.Wait)
            {
                rules.TryGetInvestment(actor.Data, result.Request, out int investment, out _, out _, snapshot);
                MemorySpent = investment + (skill?.MemoryCost ?? 0);
            }
            BattleHitResult hit = result.Hit;
            if (hit != null && hit.IsHit)
            {
                int undefended = BattleMath.RoundDamage(hit.BaseDamage * hit.ElementMultiplier * hit.ChainMultiplier
                    * hit.RageMultiplier * hit.CriticalMultiplier * hit.InvestmentMultiplier);
                PreventedDamage = Math.Max(0, undefended - hit.Damage);
            }
        }

        public override string ToString()
        {
            string action = Result.WasSkipped ? "행동 불능 / 턴 시작 효과" : Result.IsTurnStartEffect ? "턴 시작 효과"
                : Result.Request.Kind == BattleActionKind.Defend ? "방어"
                : Result.Request.Kind == BattleActionKind.Wait ? "대기" : SkillName ?? "공격";
            var text = new StringBuilder("[행동 " + Result.ActionId + "] " + ActorName + " · " + action);
            text.Append(" · 메모리 사용 ").Append(MemorySpent).Append(" (투자 ").Append(Result.Request.InvestmentStage ?? 0).Append("단계)");
            if (Result.Hit != null)
            {
                text.Append(Result.Hit.IsHit ? Result.Hit.IsCritical ? " · 치명타" : " · 명중" : " · 빗나감");
                text.Append(" · 공격 피해 ").Append(Result.Hit.Damage).Append(" · 방어 감소 ").Append(PreventedDamage);
            }
            foreach (BattleStateChange change in Result.Changes)
            {
                text.Append("\n  ").Append(change.After.Data.DisplayName).Append(" (").Append(change.After.InstanceId).Append(")");
                if (change.HpDelta < 0)
                {
                    text.Append(Result.IsTurnStartEffect && change.Before.ImprintDamage > 0 ? " 각인 피해 " : " 받은 피해 ").Append(-change.HpDelta);
                }
                text.Append(" · HP ").Append(change.Before.Hp).Append("→").Append(change.After.Hp)
                    .Append(" · 메모리 ").Append(change.Before.Memory).Append("→").Append(change.After.Memory);
                if (change.After.HasAfterimageRecovery)
                {
                    text.Append(" · 다음 행동 방어 시 잔상 회복");
                }
                if (change.After.ImprintDamage > 0)
                {
                    text.Append(" · 다음 행동 각인 ").Append(change.After.ImprintDamage);
                }
            }
            if (Result.Outcome != BattleOutcome.None)
            {
                text.Append("\n전투 종료: ").Append(Result.Outcome);
            }
            return text.ToString();
        }
    }
}
