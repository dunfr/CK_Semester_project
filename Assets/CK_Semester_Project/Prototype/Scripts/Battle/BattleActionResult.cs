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
        public BattleOutcome Outcome { get; }
        public IReadOnlyList<BattleStateChange> Changes { get; }

        internal BattleActionResult(long actionId, BattleActionRequest request, bool wasSkipped,
            BattleOutcome outcome, IEnumerable<BattleStateChange> changes)
        {
            ActionId = actionId;
            Request = request;
            WasSkipped = wasSkipped;
            Outcome = outcome;
            Changes = Array.AsReadOnly(changes.ToArray());
        }
    }
}
