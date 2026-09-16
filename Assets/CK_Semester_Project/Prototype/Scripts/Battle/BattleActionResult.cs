using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class BattleActionResult
    {
        public long ActionId { get; }
        public BattleActionRequest Request { get; }
        public bool WasSkipped { get; }
        public bool IsTurnStartEffect { get; }
        public BattleHitResult Hit { get; }
        public bool GrantsExtraAction => Outcome == BattleOutcome.None && Hit != null && Hit.GrantsExtraAction;
        public BattleOutcome Outcome { get; }
        public IReadOnlyList<BattleStateChange> Changes { get; }

        internal BattleActionResult(long actionId, BattleActionRequest request, bool wasSkipped,
            BattleOutcome outcome, IEnumerable<BattleStateChange> changes, BattleHitResult hit = null, bool isTurnStartEffect = false)
        {
            ActionId = actionId;
            Request = request;
            WasSkipped = wasSkipped;
            Hit = hit;
            IsTurnStartEffect = isTurnStartEffect;
            Outcome = outcome;
            Changes = Array.AsReadOnly(changes.ToArray());
        }
    }
}
