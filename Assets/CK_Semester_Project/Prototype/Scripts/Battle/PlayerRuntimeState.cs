using System;

namespace CK.SemesterProject.Battle
{
    // 전투 밖에서도 유지할 값만 보관한다. 폭주·연쇄·상태이상은 전투 세션 소유다.
    public sealed class PlayerRuntimeState
    {
        public event Action Changed;
        public string CharacterId { get; private set; }
        public int Hp { get; private set; }
        public int Memory { get; private set; }
        public int MaxHp { get; private set; }
        public int MaxMemory { get; private set; }
        public int InitialMemory { get; private set; }

        public void Initialize(CombatantData data)
        {
            if (data == null || data.Team != BattleTeam.Player)
            {
                throw new ArgumentException("플레이어 데이터가 필요합니다.", nameof(data));
            }
            bool isNewCharacter = CharacterId != data.Id;
            CharacterId = data.Id;
            MaxHp = data.MaxHp;
            MaxMemory = data.MaxMemory;
            InitialMemory = data.InitialMemory;
            SetVitals(isNewCharacter ? MaxHp : Hp, isNewCharacter ? InitialMemory : Memory);
        }

        public void SetVitals(int hp, int memory)
        {
            if (CharacterId == null)
            {
                throw new InvalidOperationException("플레이어 상태를 먼저 초기화해야 합니다.");
            }
            Hp = Math.Max(0, Math.Min(MaxHp, hp));
            Memory = Math.Max(0, Math.Min(MaxMemory, memory));
            Changed?.Invoke();
        }

        public void ApplyBattleState(CombatantState state)
        {
            if (state == null || state.Data.Team != BattleTeam.Player || state.Data.Id != CharacterId)
            {
                throw new ArgumentException("현재 플레이어의 전투 상태가 아닙니다.", nameof(state));
            }
            SetVitals(state.Hp, state.Memory);
        }

        public void RestoreFull()
        {
            SetVitals(MaxHp, InitialMemory);
        }

        public void RestoreFieldMemory()
        {
            SetVitals(Hp, InitialMemory);
        }
    }
}
