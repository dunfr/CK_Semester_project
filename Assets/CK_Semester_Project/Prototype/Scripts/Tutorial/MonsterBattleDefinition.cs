using System;
using System.Linq;
using CK.SemesterProject.Battle;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    [CreateAssetMenu(menuName = "Battle/Monster Definition")]
    public sealed class MonsterBattleDefinition : ScriptableObject
    {
        [Serializable]
        private sealed class BossSkillRule
        {
            [SerializeField, Range(0, 3), Tooltip("메모리 구간. 0=75% 이상, 1=50% 이상, 2=25% 이상, 3=25% 미만")]
            private int _memoryBand;
            [SerializeField, Range(0, 3), Tooltip("보스 HP 구간. 0=75% 이상부터 3=25% 미만")]
            private int _bossHpBand;
            [SerializeField, Range(0, 3), Tooltip("플레이어 HP 구간. 0=75% 이상부터 3=25% 미만")]
            private int _playerHpBand;
            [SerializeField, Range(1, 2), Tooltip("선택할 스킬 번호")]
            private int _skillNumber;

            public BossBehaviorProfile.SkillRule Create()
            {
                return new BossBehaviorProfile.SkillRule(_memoryBand, _bossHpBand, _playerHpBand, _skillNumber);
            }
        }

        [Serializable]
        private sealed class InvestmentRange
        {
            [SerializeField, Range(0, 7), Tooltip("최소 투자 단계. n은 방어 확률 0으로 표현")]
            private int _min;
            [SerializeField, Range(0, 7), Tooltip("최대 투자 단계")]
            private int _max;
            [SerializeField, Range(0f, 1f), Tooltip("방어표: 0=n, 0.5=범위/n, 1=방어. 공격표에서는 무시")]
            private float _defenseChance = 1;

            public InvestmentRange(MonsterInvestmentRange range)
            {
                _min = range.Min;
                _max = range.Max;
                _defenseChance = (float)range.DefenseChance;
            }

            public MonsterInvestmentRange Create() => new MonsterInvestmentRange(_min, _max, _defenseChance);
        }

        [Serializable]
        private sealed class Tactics
        {
            [SerializeField, Tooltip("행동 번호 1부터 순서대로 투자 범위")]
            private InvestmentRange[] _attack;
            [SerializeField, Tooltip("자신 메모리 구간 순서: 상대 폭주 45~74")]
            private InvestmentRange[] _rageThree;
            [SerializeField, Tooltip("자신 메모리 구간 순서: 상대 폭주 75~90")]
            private InvestmentRange[] _rageFour;
            [SerializeField, Tooltip("자신 메모리 구간 순서: 자신의 약점 연쇄 1단계")]
            private InvestmentRange[] _chainOne;
            [SerializeField, Tooltip("자신 메모리 구간 순서: 자신의 약점 연쇄 2단계 이상")]
            private InvestmentRange[] _chainTwo;

            public Tactics(MonsterTacticsTable table)
            {
                _attack = table.Attack.Select(row => new InvestmentRange(row)).ToArray();
                _rageThree = table.RageThree.Select(row => new InvestmentRange(row)).ToArray();
                _rageFour = table.RageFour.Select(row => new InvestmentRange(row)).ToArray();
                _chainOne = table.ChainOne.Select(row => new InvestmentRange(row)).ToArray();
                _chainTwo = table.ChainTwo.Select(row => new InvestmentRange(row)).ToArray();
            }

            public MonsterTacticsTable Create()
            {
                return new MonsterTacticsTable(_attack.Select(row => row.Create()),
                    _rageThree.Select(row => row.Create()), _rageFour.Select(row => row.Create()),
                    _chainOne.Select(row => row.Create()), _chainTwo.Select(row => row.Create()));
            }
        }

        [SerializeField, Tooltip("몬스터 정의 ID")]
        private string _id;
        [SerializeField, Tooltip("화면 표시 이름")]
        private string _displayName;
        [SerializeField, Tooltip("AI 유형과 공격 속성")]
        private BattleElement _element;
        [SerializeField, Min(1), Tooltip("Monster_DT의 최대 HP")]
        private int _maxHp = 600;
        [SerializeField, Min(1), Tooltip("Monster_DT의 기본/최대 메모리")]
        private int _memory = 180;
        [SerializeField, Min(0), Tooltip("Monster_DT의 기본 공격력")]
        private int _power = 90;
        [SerializeField, Range(0f, 1f), Tooltip("기본 공격 명중률")]
        private float _accuracy = 1;
        [SerializeField, Range(0f, 1f), Tooltip("Monster_DT의 회피율. 0.1이면 10%")]
        private float _evasion;
        [SerializeField, Tooltip("망각 기본 AI에 보스 전용 스킬 선택표를 적용합니다")]
        private bool _isBoss;
        [SerializeField, Min(0), Tooltip("보스 스킬_1의 기본 공격력 추가 피해")]
        private int _bossSkillOneBonus = 20;
        [SerializeField, Min(0), Tooltip("보스 스킬_2의 기본 공격력 추가 피해")]
        private int _bossSkillTwoBonus = 40;
        [SerializeField, Min(0), Tooltip("보스 스킬_1 기본 메모리 비용")]
        private int _bossSkillOneCost = 10;
        [SerializeField, Min(0), Tooltip("보스 스킬_2 기본 메모리 비용")]
        private int _bossSkillTwoCost = 20;
        [SerializeField, Tooltip("문서의 보스 스킬 선택표. 빈 배열이면 기본 28행. 미기재 조합은 가장 가까운 행, 동률은 앞 행")]
        private BossSkillRule[] _bossSkillRules;
        [SerializeField, Tooltip("4개 원소로 구성한 약점 연쇄")]
        private BattleElement[] _weaknessChain;
        [SerializeField, Tooltip("0단계부터 기본 메모리 대비 비용 백분율")]
        private int[] _costPercent;
        [SerializeField, Tooltip("0단계부터 공격 배율")]
        private double[] _multipliers;
        [SerializeField, Tooltip("상대 HP/메모리 구간의 하한 백분율. 내림차순")]
        private int[] _playerThresholds;
        [SerializeField, Tooltip("자신 HP/메모리 구간의 하한 백분율. 내림차순")]
        private int[] _selfThresholds;
        [SerializeField, Tooltip("단독 전투 행동표")]
        private Tactics _solo;
        [SerializeField, Tooltip("생존 동료가 각인인 2기 전투")]
        private Tactics _withImprint;
        [SerializeField, Tooltip("생존 동료가 잔상인 2기 전투")]
        private Tactics _withAfterimage;
        [SerializeField, Tooltip("생존 동료가 망각인 2기 전투")]
        private Tactics _withOblivion;

        [SerializeField, Range(0, 100), Tooltip("3기 조합의 각인 최대 투자 공격: 플레이어 HP 이하 비율")]
        private int _trioFinisherHpPercent = 40;
        [SerializeField, Range(0, 100), Tooltip("잔상 2기 생존 방어: 자신의 HP 이하 비율")]
        private int _trioSurvivorHpPercent = 75;
        [SerializeField, Range(0f, 1f), Tooltip("잔상 2기 생존 시 방어 확률")]
        private float _trioSurvivorDefenseChance = 0.5f;
        [SerializeField, Tooltip("잔상 확률 공격: 1·2·3단계 가중치")]
        private int[] _trioAttackWeights = { 50, 30, 20 };
        [SerializeField, Tooltip("대기형 각인 행동 1~5 투자 범위")]
        private InvestmentRange[] _trioWaitingAttack = new MonsterTrioRules().WaitingAttack.Select(row => new InvestmentRange(row)).ToArray();
        [SerializeField, Tooltip("잔상 방어: 메모리 내림차순 표, 행동 1~6")]
        private InvestmentRange[] _trioDefenseForward = new MonsterTrioRules().DefenseForward.Select(row => new InvestmentRange(row)).ToArray();
        [SerializeField, Tooltip("잔상 방어: 메모리 오름차순 표, 행동 1~6")]
        private InvestmentRange[] _trioDefenseReverse = new MonsterTrioRules().DefenseReverse.Select(row => new InvestmentRange(row)).ToArray();
        [SerializeField, Tooltip("잔상 2기 생존 방어: 행동 1~6 투자 범위")]
        private InvestmentRange[] _trioSurvivorDefense = new MonsterTrioRules().SurvivorDefense.Select(row => new InvestmentRange(row)).ToArray();

        public BattleElement Element => _element;
        public bool IsBoss => _isBoss;

        public void InitializeDefaults(BattleElement element)
        {
            MonsterBehaviorProfile profile = MonsterBehaviorProfile.CreateDefault(element);
            _element = element;
            _id = "prototype_" + element.ToString().ToLowerInvariant();
            _displayName = element == BattleElement.Imprint ? "각인 몬스터" : element == BattleElement.Afterimage ? "잔상 몬스터" : "망각 몬스터";
            _memory = element == BattleElement.Afterimage ? 260 : 180;
            _costPercent = profile.CostPercent.ToArray();
            _multipliers = profile.Multipliers.ToArray();
            _playerThresholds = profile.PlayerThresholds.ToArray();
            _selfThresholds = profile.SelfThresholds.ToArray();
            _solo = new Tactics(profile.Solo);
            _withImprint = new Tactics(profile.WithImprint);
            _withAfterimage = new Tactics(profile.WithAfterimage);
            _withOblivion = new Tactics(profile.WithOblivion);
            BattleElement first = element == BattleElement.Imprint ? BattleElement.Afterimage
                : element == BattleElement.Afterimage ? BattleElement.Oblivion : BattleElement.Imprint;
            _weaknessChain = new[] { first, element, element == BattleElement.Imprint ? BattleElement.Oblivion
                : element == BattleElement.Afterimage ? BattleElement.Imprint : BattleElement.Afterimage, first };
        }

        public CombatantData CreateData()
        {
            var profile = new MonsterBehaviorProfile(_element, _costPercent, _multipliers,
                _playerThresholds, _selfThresholds, _solo.Create(), _withImprint.Create(), _withAfterimage.Create(), _withOblivion.Create(),
                new MonsterTrioRules(_trioFinisherHpPercent, _trioSurvivorHpPercent, _trioSurvivorDefenseChance,
                    _trioAttackWeights, _trioWaitingAttack.Select(row => row.Create()).ToArray(),
                    _trioDefenseForward.Select(row => row.Create()).ToArray(), _trioDefenseReverse.Select(row => row.Create()).ToArray(),
                    _trioSurvivorDefense.Select(row => row.Create()).ToArray()));
            var attack = new SkillData(_id + "_attack", "공격", _power, _element, accuracy: _accuracy);
            if (_isBoss)
            {
                var first = new SkillData(_id + "_skill_1", "스킬_1", checked(_power + _bossSkillOneBonus),
                    BattleElement.Oblivion, memoryCost: _bossSkillOneCost, accuracy: _accuracy, englishName: "skill_1");
                var second = new SkillData(_id + "_skill_2", "스킬_2", checked(_power + _bossSkillTwoBonus),
                    BattleElement.Oblivion, memoryCost: _bossSkillTwoCost, accuracy: _accuracy, englishName: "skill_2");
                var boss = new BossBehaviorProfile(first.Id, second.Id,
                    _bossSkillRules != null && _bossSkillRules.Length > 0 ? _bossSkillRules.Select(rule => rule.Create()) : null);
                return new CombatantData(_id, _displayName, BattleTeam.Monster, _maxHp, _memory, _memory,
                    new[] { first, second }, _element, evasion: _evasion, weaknessChain: _weaknessChain,
                    monsterProfile: profile, bossProfile: boss);
            }
            return new CombatantData(_id, _displayName, BattleTeam.Monster, _maxHp, _memory, _memory,
                new[] { attack }, _element, evasion: _evasion, weaknessChain: _weaknessChain, monsterProfile: profile);
        }
    }
}
