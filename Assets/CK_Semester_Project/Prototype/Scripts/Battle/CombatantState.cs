namespace CK.SemesterProject.Battle
{
    // UI와 실행기에 전달되는 불변 상태. 변경은 BattleSession에서만 반영한다.
    public sealed class CombatantState
    {
        public string InstanceId { get; }
        public CombatantData Data { get; }
        public int Hp { get; }
        public int Memory { get; }
        public int SkippedTurns { get; }
        public bool IsDefending { get; }
        public int RageEnergy { get; }
        public int ChainStep { get; }
        public int ImprintDamage { get; }
        public bool IsOverheated { get; }
        public bool IsDead => Hp == 0;
        public bool CanAct => !IsDead && SkippedTurns == 0 && !IsOverheated;

        internal CombatantState(string instanceId, CombatantData data, int hp, int memory, int skippedTurns,
            bool isDefending = false, int rageEnergy = 0, int chainStep = 0, int imprintDamage = 0,
            bool isOverheated = false)
        {
            InstanceId = instanceId;
            Data = data;
            Hp = hp;
            Memory = memory;
            SkippedTurns = skippedTurns;
            IsDefending = !IsDead && isDefending;
            RageEnergy = rageEnergy;
            ChainStep = IsDead ? 0 : chainStep;
            ImprintDamage = IsDead ? 0 : imprintDamage;
            IsOverheated = !IsDead && isOverheated;
        }
    }
}
