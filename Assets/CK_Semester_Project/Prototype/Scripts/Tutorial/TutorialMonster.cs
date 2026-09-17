using System;
using CK.SemesterProject.Battle;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialMonster : MonoBehaviour
    {
        [SerializeField, Tooltip("몬스터 능력치·AI 행동표 에셋")]
        private MonsterBattleDefinition _definition;
        private CombatantData _data;

        public static string GetElementName(BattleElement element)
        {
            switch (element)
            {
                case BattleElement.Imprint: return "각인";
                case BattleElement.Afterimage: return "잔상";
                case BattleElement.Oblivion: return "망각";
                default: return "무속성";
            }
        }

        public CombatantData GetData()
        {
            if (_definition == null)
            {
                throw new InvalidOperationException(name + ": 몬스터 정의 에셋이 없습니다.");
            }
            return _data ?? (_data = _definition.CreateData());
        }
    }
}
