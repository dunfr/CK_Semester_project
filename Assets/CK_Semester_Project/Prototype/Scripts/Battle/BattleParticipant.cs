using System;

namespace CK.SemesterProject.Battle
{
    public sealed class BattleParticipant
    {
        public string InstanceId { get; }
        public CombatantData Data { get; }
        public int InitialHp { get; }
        public int InitialMemory { get; }
        public int SkippedTurns { get; }
        public int InitialRageEnergy { get; }

        public BattleParticipant(string instanceId, CombatantData data, int? initialHp = null,
            int? initialMemory = null, int skippedTurns = 0, int initialRageEnergy = 0)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException("전투 개체 ID가 필요합니다.", nameof(instanceId));
            }
            InitialHp = initialHp ?? data.MaxHp;
            InitialMemory = initialMemory ?? data.InitialMemory;
            if (InitialHp < 0 || InitialHp > data.MaxHp || InitialMemory < 0
                || skippedTurns < 0 || initialRageEnergy < 0 || initialRageEnergy > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(initialHp), "초기 전투 상태 범위가 잘못되었습니다.");
            }
            InstanceId = instanceId;
            SkippedTurns = skippedTurns;
            InitialRageEnergy = initialRageEnergy;
        }
    }
}
