using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class BattleSnapshot
    {
        public BattlePhase Phase { get; }
        public BattleOutcome Outcome { get; }
        public BattleEntryCondition EntryCondition { get; }
        public int Round { get; }
        public long TurnId { get; }
        public string CurrentActorId { get; }
        public IReadOnlyList<CombatantState> Combatants { get; }
        public IReadOnlyList<string> TurnOrder { get; }

        internal BattleSnapshot(BattlePhase phase, BattleOutcome outcome, BattleEntryCondition entryCondition,
            int round, long turnId, string currentActorId, IEnumerable<CombatantState> combatants,
            IEnumerable<string> turnOrder)
        {
            Phase = phase;
            Outcome = outcome;
            EntryCondition = entryCondition;
            Round = round;
            TurnId = turnId;
            CurrentActorId = currentActorId;
            Combatants = Array.AsReadOnly(combatants.ToArray());
            TurnOrder = Array.AsReadOnly(turnOrder.ToArray());
        }
    }
}
