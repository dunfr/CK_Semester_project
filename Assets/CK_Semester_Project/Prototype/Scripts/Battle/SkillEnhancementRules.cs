using System;

namespace CK.SemesterProject.Battle
{
    public sealed class SkillEnhancementRules
    {
        private const int MaxStage = 3;
        public int VictoriesPerStage { get; }

        public SkillEnhancementRules(int victoriesPerStage = 5)
        {
            if (victoriesPerStage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(victoriesPerStage));
            }
            VictoriesPerStage = victoriesPerStage;
        }

        public int GetStage(int victories)
        {
            if (victories < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(victories));
            }
            return Math.Min(MaxStage - 1, victories / VictoriesPerStage) + 1;
        }

        public int GetRemainingVictories(int victories)
        {
            return GetStage(victories) == MaxStage ? 0 : VictoriesPerStage - victories % VictoriesPerStage;
        }
    }
}
