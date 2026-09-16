using System;
using UnityEngine;

namespace CK.SemesterProject.Data
{
    /// <summary>Character_DT의 한 행입니다. 확률은 0~1, 치명타 피해는 배율입니다.</summary>
    [Serializable]
    public sealed class CharacterData
    {
        [field: SerializeField] public string Id { get; private set; }
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public string EnglishName { get; private set; }
        [field: SerializeField] public uint Hp { get; private set; }
        [field: SerializeField] public uint Speed { get; private set; }
        [field: SerializeField] public uint BaseMovementSpeed { get; private set; }
        [field: SerializeField] public uint RunningMovementSpeed { get; private set; }
        [field: SerializeField] public uint MaxOverheatEnergy { get; private set; }
        [field: SerializeField] public float BaseCriticalChance { get; private set; }
        [field: SerializeField] public float CriticalDamage { get; private set; }
        [field: SerializeField] public float EvasionRate { get; private set; }
        [field: SerializeField] public uint BaseMemory { get; private set; }

        public CharacterData(string id, string name, string englishName, uint hp, uint speed,
            uint baseMovementSpeed, uint runningMovementSpeed, uint maxOverheatEnergy,
            float baseCriticalChance, float criticalDamage, float evasionRate, uint baseMemory)
        {
            Id = id;
            Name = name;
            EnglishName = englishName;
            Hp = hp;
            Speed = speed;
            BaseMovementSpeed = baseMovementSpeed;
            RunningMovementSpeed = runningMovementSpeed;
            MaxOverheatEnergy = maxOverheatEnergy;
            BaseCriticalChance = baseCriticalChance;
            CriticalDamage = criticalDamage;
            EvasionRate = evasionRate;
            BaseMemory = baseMemory;
        }
    }
}
