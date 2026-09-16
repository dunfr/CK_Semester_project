# 전투 코어 연결 안내

`feat/battle-core`는 `prototype`에서 분기한 전투 로직의 첫 단계다. Unity에 독립적인 C# 코어, EditMode 테스트, 직접 조작할 수 있는 Unity 데모 씬을 제공한다.

## 직접 실행하기

1. Unity 6000.3.23f1에서 `feat/battle-core` 브랜치의 프로젝트를 연다. 기본 작업 폴더는 `C:/CK_Semester_project/Semesterproject`다.
2. `Assets/CK_Semester_Project/Prototype/Scenes/BattleCoreDemo.unity`를 열고 Play를 누른다. 메뉴 `CK > Battle Demo > Open And Play`도 사용할 수 있다.
3. 하단에서 스킬과 센티널 A/B를 선택하고 **공격 실행**을 누른다. 몬스터는 자동으로 응답한다.
4. **자동 진행 OFF**를 누르면 **다음 단계로 진행** 버튼으로 연출 완료·몬스터 행동을 한 단계씩 실행한다.
5. 상단의 **일반 전투 / 메모리 동률 / 행동 불능**으로 초기 상황을 바꾸거나 **처음부터 다시**로 재시작한다.

Game 뷰는 16:10 화면에 맞췄으며 다른 크기에서는 비율을 유지해 축소한다. 창을 크게 하면 글자를 읽기 쉽다.

데모 모델·밸런스 수치는 검증용이다. 기본 공격은 피해 24와 메모리 회복 3, 강타는 피해 36과 기본 비용 2, 메모리 교란은 피해 12와 최대 8 강탈이다. 강탈은 대상 보유량과 행동자의 남은 용량까지만 이전한다. 센티널 A에게 첫 교란을 사용하면 A의 메모리가 18→10, 플레이어는 24→32가 되어 B(14)가 A보다 먼저 행동한다. 기획서의 망각 강탈 2와 구별되는 데모 데이터이며 속성 효과 자동 발동은 후속 작업이다.

하단 **투자 MEM** 버튼을 누르면 0~3을 순환한다. 선택한 투자량은 공격·방어 요청에 포함되며 대기에는 적용하지 않는다. 데모 투자 배율은 1 / 1.1 / 1.2 / 1.35이고, 방어 투자 1당 폭주 에너지 5를 감소시킨다. 이 표는 별도 메모리 기획서가 없는 상태에서 조작 확인용으로 지정한 값이다. 방어는 다음 자기 턴 시작까지 받는 피해를 50% 줄이며 상태 카드에서 방어·폭주를 확인할 수 있다. 초기 폭주 50은 감소 확인용이며 자동 획득·폭주 발동은 아직 없다. 몬스터 행동은 `MonsterAi`가 선택한다. 기존 데모 피해 수치를 유지하도록 데모의 AI 최대 투자량만 0으로 지정했다. 일반 AI 설정은 최대 3이며 몬스터별 설정으로 교체할 수 있다.

청록색 플레이어와 두 센티널, HP·메모리·턴 순서·전투 기록, 공격·피해·사망 표시, 승리·패배 화면이 포함된다. 모델·UI는 프로토타입 검증용이며 팀원 B가 최종 화면으로 교체할 수 있다. 실제 코어의 요청·결과 계약을 그대로 사용한다.

데모 파일은 `Prototype/Scripts/BattleDemo/`, 씬 생성·실행 메뉴는 `Editor/BattleDemo/`, 데모 연동 테스트는 `Tests/PlayMode/BattleDemo/`에 있다.

## 위치와 구현 범위

- 코어: `Assets/CK_Semester_Project/Prototype/Scripts/Battle/`
- 테스트: `Assets/CK_Semester_Project/Tests/EditMode/Battle/`
- 네임스페이스: `CK.SemesterProject.Battle`
- 어셈블리: `CK.Battle.Core` — UnityEngine 의존성 없음.

공통 정의, 전투 개체 상태, 전투 초기화, 메모리 기반 순서, 행동 요청 검증, 결과 전달, 연출 완료 후 진행, 사망·승패 판정을 구현했다. 스킬·캐릭터·몬스터 정의는 생성자로 만들며 CSV 로더와 Inspector 자산 연결은 후속 작업이다.

`CombatantData`는 캐릭터와 몬스터가 공유하는 정의다. `Team`으로 구분하며 동일한 몬스터 정의를 여러 `BattleParticipant`에 사용할 수 있다. `Data.Id`는 콘텐츠 정의 ID, `InstanceId`는 해당 전투에서 고유한 개체 ID다. UI·요청·결과에서는 `InstanceId`를 사용한다.

정의와 외부에 전달하는 상태는 불변이다. HP·메모리·행동 불능 횟수는 세션 내부에서 새 상태로 교체한다. 과거 스냅샷과 결과는 이후 행동으로 바뀌지 않는다.

## 현재 턴 규칙

1. 메모리 내림차순으로 행동한다.
2. 메모리가 같으면 플레이어가 몬스터보다 먼저 행동한다.
3. 같은 팀의 동률은 무작위 순서다. 테스트는 시드를 지정해 재현할 수 있다.
4. 한 라운드에서 생존 개체마다 기본 행동 기회를 한 번 갖는다.
5. 행동 후 아직 행동하지 않은 생존 개체만 현재 메모리로 다시 정렬한다.
6. 모두 행동하면 새 라운드를 시작한다. 사망한 개체는 순서·선택 대상에서 제외한다.
7. 행동 불능은 자기 행동 기회 하나를 소모하며 남은 횟수가 하나 줄어든다. `WasSkipped` 결과를 연출한 뒤 진행한다.

**기획 가정:** 4~5번의 라운드 방식과 플레이어끼리 동률일 때의 무작위 처리는 이번 프로토타입의 가정이다. 기획서의 “행동 후 재측정”만으로 이미 행동한 개체의 재선택 여부가 명확하지 않아, 가장 빠른 개체가 계속 행동하는 상황을 피하도록 정했다. 확정 기획이 다르면 이 정책과 테스트를 함께 수정한다.

추가 행동·연쇄·폭주 규칙은 후속 작업 범위다. 현재 `SkippedTurns`는 일반적인 행동 불능 상태를 표현하며 폭주 조건을 자동 계산하지 않는다.

## B가 호출하는 흐름

```csharp
using CK.SemesterProject.Battle;

var attack = new SkillData("basic_attack", "기본 공격", 25);
var player = new CombatantData("hero", "플레이어", BattleTeam.Player,
    100, 100, 20, new[] { attack });
var monster = new CombatantData("slime", "슬라임", BattleTeam.Monster,
    60, 100, 10, new[] { attack });

var battle = new BattleSession();
BattleSnapshot snapshot = battle.Start(new[]
{
    new BattleParticipant("player_1", player),
    new BattleParticipant("monster_1", monster)
}, BattleEntryCondition.PlayerInitiated);

// AwaitingAction일 때 선택 UI를 열고 제공된 목록으로 대상을 표시한다.
var targets = battle.GetSelectableTargets("basic_attack");
var request = new BattleActionRequest(snapshot.TurnId, snapshot.CurrentActorId,
    BattleActionKind.Skill, "basic_attack", targets[0]);

if (battle.TrySubmit(request, out BattleActionResult result, out BattleActionError error))
{
    // result.Request로 공격 연출, result.Changes로 피해·사망·게이지 변화를 표시한다.
    // 실제 게임에서는 연출 완료 콜백에서 아래 호출을 한다.
    battle.CompletePresentation(result.ActionId);
    snapshot = battle.GetSnapshot();
}
```

위 코드는 기본 호출 예제다. 실제 연결에서는 매 전환 후 `GetSnapshot()`의 `Phase`를 확인한다.

| 단계 | B의 동작 |
|---|---|
| `NotStarted` | 참가자를 구성한 뒤 `Start` 호출 |
| `AwaitingAction` | 현재 행동자의 요청 생성. 플레이어는 UI, 몬스터는 MonsterAi가 결정 |
| `AwaitingPresentation` | `PendingResult`를 받아 연출하고 완료 시 `CompletePresentation(ActionId)` 호출 |
| `Finished` | `Outcome`에 따라 승리·패배·무승부 화면 표시 |

`Start` 직후에도 행동 불능이면 `AwaitingPresentation`, 한 팀이 이미 사망했으면 `Finished`가 될 수 있다. 무조건 선택 UI부터 열지 않는다. 새 전투에는 새 `BattleSession`을 사용한다.

### 데이터 전달 계약

- `BattleSnapshot`: 현재 단계, 라운드, 턴 ID, 행동자 ID, 전체 개체 상태, 행동 순서, 승패, 진입 조건.
- `BattleActionRequest`: 턴 ID, 행동자 ID, 종류, 스킬 ID, 대상 ID, 메모리 투자량.
- `BattleActionResult`: 행동 ID, 원래 요청, 행동 불능 여부, 상태 변화 목록, 승패.
- `BattleStateChange`: 적용 전·후 상태, 실제 HP·메모리 변화량, 이번에 사망했는지 여부.
- `GetSelectableTargets`: 현재 행동자가 가진 스킬의 살아 있는 유효 대상. 아군 대상에는 자신도 포함하며 Self는 자신만 반환한다.

행동 대기 중 `TurnOrder`는 현재 행동자를 포함한다. 행동 결과 대기 중에는 이번 행동자와 사망자를 제외한 남은 순서다. 현재 연출 중인 행동자는 `CurrentActorId`로 별도 표시한다. 종료 결과가 발생하면 남은 순서는 비운다.

### 상태 적용과 연출 시점

이번 코어에서는 **`TrySubmit` 성공 시 상태를 한 번 적용**한다. B는 `Before`와 `After`를 사용해 타격 시점에 화면을 갱신하고, 모든 연출이 끝나면 `CompletePresentation`을 호출한다. 타격 콜백에서 피해를 다시 적용하지 않는다. 타격 시점 자체를 코어가 기다리는 계약이 필요하면 이후 연출 연결에서 조정한다.

최종 공격도 연출 완료 전에는 `AwaitingPresentation`을 유지한다. 결과의 `Outcome`으로 종료 연출을 준비하고, 완료 통보 후 `Finished`에서 종료 화면을 연다.

잘못된 턴 ID, 다른 행동자, 잘못된 스킬·대상, 범위를 벗어난 투자, 중복 입력은 실패 코드로 반환하며 상태와 턴을 소모하지 않는다. 완료 콜백은 행동 ID가 일치할 때 한 번만 수락한다. 이전 전투의 콜백은 이전 세션에 연결되도록 B가 세션별 콜백 수명을 관리한다.

## 후속 행동 실행기 연결

`IBattleActionResolver`를 `BattleSession` 생성자에 주입한다. 플레이어와 몬스터 모두 같은 `TrySubmit` 경로를 사용한다. 실행기는 불변 스냅샷과 검증된 요청을 받아 `BattleEffect` 목록을 반환한다. 개체마다 효과 하나로 합산하며 메모리 소모·회복도 효과에 포함한다. 지원하지 않는 요청은 실패 코드를 반환한다.

세션은 모든 효과를 먼저 검증한 후 일괄 적용한다. 알 수 없는 개체, 죽은 개체, 중복 개체 효과, 음수 행동 불능 횟수는 실행기 계약 오류로 예외를 발생시키며 부분 적용하지 않는다. HP·메모리는 0~최댓값으로 제한한다. 이 단계는 부활을 지원하지 않는다. 여러 효과가 동시에 양 팀을 전멸시키면 무승부다.

기본 실행기는 `BattleActionResolver`다. `PrototypeActionResolver`는 이전 최소 동작의 비교 테스트용으로만 남겨 두었다. 데모도 기본 실행기를 사용한다.

### 메모리·방어 계약

- `SkillData.MemoryCost`: 스킬 기본 비용. `MemoryRecovery`: 행동자 회복량. `MemorySteal`: 대상에서 행동자로 이전할 최대량. 기본값은 모두 0이다.
- 요청의 `MemoryInvestment`는 소비할 메모리 수량이다. 스킬 비용 + 투자량을 현재 보유량으로 감당해야 하며, 이번 행동에서 얻을 회복·강탈분을 미리 사용할 수 없다.
- 비용 소모 → 행동자 회복 → 강탈 순서다. 회복은 MaxMemory까지, 강탈은 상대 보유량과 행동자 빈 용량까지 제한한다. 자기 자신에게서는 강탈하지 않는다. 사망 전 유효했던 대상에는 해당 공격과 강탈을 함께 적용한다.
- `BattleRules.InvestmentMultipliers`의 인덱스는 투자 수량이다. 목록 밖의 스킬 투자는 거절한다. 기본 설정은 `[1.0]`으로 0 투자만 허용한다. 확정된 비용·배율 표는 세션 생성 시 주입한다. 입력 배열은 복사해 보관한다.
- 방어는 별도 스킬·대상 없이 요청한다. 투자 메모리를 소모하고 `DefenseRageReductionPerMemory`만큼 폭주 에너지를 낮춘다. 계수 기본값 0, 피해 배율 기본값 0.5다. `RageEnergy` 범위는 0~100이다.
- **지속 시간 가정:** 방어 직후부터 다음 자기 턴 시작까지 모든 공격에 적용한다. 그때 행동 불능이어도 만료한다. 연속 방어로 중첩되지 않으며 사망 시 해제한다. 기획의 ‘다음 턴’ 해석이 확정되면 이 시점과 테스트를 함께 변경한다.
- 스킬 피해는 Power × 투자 배율 × 대상 방어 배율을 계산한 뒤 0.5 이상 올림으로 정수 반올림한다. 명중·치명타·속성·연쇄 배율은 아직 포함하지 않는다.
- `ValidateRequest`로 현재 요청의 실패 사유를 미리 확인할 수 있다. `TrySubmit`에서도 다시 검증한다. 실패 시 메모리·HP·턴·방어는 바뀌지 않는다. 커스텀 실행기의 추가 제한은 실행기에서 판정한다.
- 결과 `Changes`의 `Before/After.IsDefending`, `RageEnergy`, `RageDelta`로 B가 표시한다. 다음 자기 턴 시작의 방어 만료는 `CompletePresentation` 이후 새 스냅샷에서 읽는다.

```csharp
// 예시용 표다. 실제 밸런스 데이터로 교체한다.
var rules = new BattleRules(new[] { 1.0, 1.1, 1.2, 1.35 },
    defenseRageReductionPerMemory: 5);
var session = new BattleSession(rules: rules);
// Start 후 현재 TurnId / ActorId로 생성
var defend = new BattleActionRequest(turnId, actorId, BattleActionKind.Defend,
    memoryInvestment: 2);
```

명중·회피·치명타, 속성·연쇄·폭주 발동, 몬스터별 전용 패턴, 추가 행동, CSV 입력은 후속 작업이다. 진입 조건은 보관만 하며 추가 메모리 수치를 임의로 부여하지 않는다. 치명타·약점·연쇄 수치는 기획서 내 불일치를 확인한 뒤 반영한다.

## 몬스터 AI 연결

`MonsterAi.TryChooseAction(session, out request)`는 현재 몬스터 턴의 행동 요청만 만든다. 호출자가 `TrySubmit`으로 실행하고 기존 결과·연출 완료 흐름을 사용한다. 플레이어 턴, 행동 불능 결과 대기, 연출 중, 종료 상태에서는 false를 반환한다. 선택 과정은 HP·메모리·턴 순서·난수를 변경하지 않는다.

```csharp
var ai = new MonsterAi(new MonsterAiSettings(maxMemoryInvestment: 3,
    memoryWeight: 1, killBonus: 100));
if (ai.TryChooseAction(battle, out BattleActionRequest monsterRequest))
{
    bool accepted = battle.TrySubmit(monsterRequest, out BattleActionResult result,
        out BattleActionError error);
    // 성공 시 B에게 result 전달. 선택 이후 상태가 바뀌었으면 거절될 수 있다.
}
```

- 보유 스킬, 살아 있는 유효 대상, 지불 가능한 투자량을 후보로 만든다. 최대 투자량과 세션의 배율 표 범위를 모두 지킨다.
- 공통 계산기의 방어·반올림·회복·강탈 계산을 사용해 예상 효과를 평가한다. 실제 HP 한도를 넘어선 과잉 피해는 이득으로 계산하지 않는다.
- 아군 HP 증가·적 HP 감소 + 메모리 손익 × MemoryWeight + 적 처치 보너스 − 아군 사망 손실로 점수를 계산한다. 기본 가중치 1과 처치 보너스 100은 초기 정책값이다.
- 양수 점수 중 가장 높은 후보를 선택한다. 동점이면 총 소비 메모리가 적은 후보, 다시 동점이면 스킬 정의 순서와 참가자 순서를 사용한다. 같은 상태에서 선택은 재현 가능하다.
- 공격이 이미 처치에 충분하면 불필요한 추가 투자를 줄인다. 자기 회복도 선택할 수 있고 아군 공격·자기 강탈은 이득으로 평가하지 않는다.
- 유익한 스킬이 없거나 모두 비용 부족이면 투자 없는 방어를 요청한다. 지금은 체력이 낮다는 이유만으로 공격 대신 방어하는 별도 성향은 없다.
- 현재 예측은 `BattleActionResolver`의 확정 계산에 맞춰져 있다. 커스텀 실행기, 향후 명중·치명타 확률, 추가 효과를 넣을 때는 AI의 예상 효과 평가도 함께 확장해야 한다. 장기 턴 예측이나 보스 전용 패턴은 아직 없다.

## 검증 실행

Unity 6000.3.23f1에서 Test Runner의 EditMode 탭을 열고 `CK.Battle.Core.Tests`를 실행한다. 테스트는 전투 시작·종료, 메모리 정렬·동률·라운드, 결과 불변성, 중복 입력·완료 콜백, 사망 대상 제외, 행동 불능, 실행기 오류의 원자적 처리, 수치 범위를 검증한다.

2026-09-16 몬스터 AI 추가 후 Unity 6000.3.23f1 **EditMode 50개 통과**, 실패·건너뜀 0개. AI 선택·비용·최소 처치 투자·대상 제외·회복·방어 대체·불변성·호출 시점 등을 검증했다. 이번 작업에서는 사용자 요청에 따라 데모와 PlayMode를 실행하지 않았다. 직전 메모리·방어 구현 시 PlayMode 9개가 통과한 이력이 있다. 컴파일 오류는 없었으며 스크립트 재로딩에서 Unity 내부의 `Deleting invalid font reference` 로그가 한 차례 관찰됐다.

메모리 비용+투자 검증, 소모 후 회복·강탈 순서, 부족한 메모리를 회복으로 선지급할 수 없음, 강탈의 양측 한도, 자기 대상 효과 집계, 데미지 반올림, 방어 지속·만료·폭주 감소, 중복 실행 방지, 기존 턴·사망·승패 계약을 검증했다. PlayMode에서는 투자 버튼 잠금, 두 몬스터의 공격 모두 방어, 다음 자기 턴 방어 만료, 시나리오 재시작, 전투 승패와 자동 진행을 확인했다. 최종 모델·연출과 몬스터별 전용 패턴은 검증 범위에 포함되지 않는다.

명령줄 실행 예:

```text
Unity.exe -batchmode -nographics -projectPath <프로젝트 경로> -runTests -testPlatform EditMode -testFilter CK.SemesterProject.Battle.Tests -testResults <결과.xml> -logFile <로그.txt>
```

테스트 실행에는 `-quit`을 덧붙이지 않는다. Unity Test Runner가 실행 종료를 관리한다. 같은 프로젝트를 Unity에서 열어둔 경우 에디터 내 Test Runner를 사용한다.
