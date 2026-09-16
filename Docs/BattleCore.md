# 전투 코어 연결 안내

`feat/battle-core`는 `prototype`에서 분기한 전투 로직의 첫 단계다. Unity에 독립적인 C# 코어, EditMode 테스트, 직접 조작할 수 있는 Unity 데모 씬을 제공한다.

## 직접 실행하기

1. Unity 6000.3.23f1에서 `feat/battle-core` 브랜치의 프로젝트를 연다. 기본 작업 폴더는 `C:/CK_Semester_project/Semesterproject`다.
2. `Assets/CK_Semester_Project/Prototype/Scenes/BattleCoreDemo.unity`를 열고 Play를 누른다. 메뉴 `CK > Battle Demo > Open And Play`도 사용할 수 있다.
3. 하단에서 스킬과 센티널 A/B를 선택하고 **공격 실행**을 누른다. 몬스터는 자동으로 응답한다.
4. **자동 진행 OFF**를 누르면 **다음 단계로 진행** 버튼으로 연출 완료·몬스터 행동을 한 단계씩 실행한다.
5. 상단의 **일반 전투 / 메모리 동률 / 행동 불능**으로 초기 상황을 바꾸거나 **처음부터 다시**로 재시작한다.

Game 뷰는 16:10 화면에 맞췄으며 다른 크기에서는 비율을 유지해 축소한다. 창을 크게 하면 글자를 읽기 쉽다.

데모 모델·수치는 검증용이다. 기본 공격은 고정 피해 24, 강타는 36이며, **메모리 교란은 피해 12와 메모리 -8을 적용하는 재정렬 확인용 샘플**이다. 센티널 A에게 교란을 사용하면 A의 메모리가 18에서 10으로 줄어 B(14)가 먼저 행동한다. 이 효과는 `BattleDemoResolver`에만 있으며 정식 속성·메모리 규칙이 아니다. 몬스터의 첫 스킬 자동 선택도 최종 AI를 대신하는 데모 입력이다.

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

추가 행동·연쇄·폭주 규칙은 후속 브랜치 범위다. 현재 `SkippedTurns`는 일반적인 행동 불능 상태를 표현하며 폭주 조건을 자동 계산하지 않는다.

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
| `AwaitingAction` | 현재 행동자의 요청 생성. 플레이어는 UI, 몬스터는 후속 AI가 결정 |
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

이번 코어에서는 **`TrySubmit` 성공 시 상태를 한 번 적용**한다. B는 `Before`와 `After`를 사용해 타격 시점에 화면을 갱신하고, 모든 연출이 끝나면 `CompletePresentation`을 호출한다. 타격 콜백에서 피해를 다시 적용하지 않는다. 타격 시점 자체를 코어가 기다리는 계약이 필요하면 이후 연출 브랜치에서 조정한다.

최종 공격도 연출 완료 전에는 `AwaitingPresentation`을 유지한다. 결과의 `Outcome`으로 종료 연출을 준비하고, 완료 통보 후 `Finished`에서 종료 화면을 연다.

잘못된 턴 ID, 다른 행동자, 잘못된 스킬·대상, 범위를 벗어난 투자, 중복 입력은 실패 코드로 반환하며 상태와 턴을 소모하지 않는다. 완료 콜백은 행동 ID가 일치할 때 한 번만 수락한다. 이전 전투의 콜백은 이전 세션에 연결되도록 B가 세션별 콜백 수명을 관리한다.

## 후속 행동 실행기 연결

`IBattleActionResolver`를 `BattleSession` 생성자에 주입한다. 플레이어와 몬스터 모두 같은 `TrySubmit` 경로를 사용한다. 실행기는 불변 스냅샷과 검증된 요청을 받아 `BattleEffect` 목록을 반환한다. 개체마다 효과 하나로 합산하며 메모리 소모·회복도 효과에 포함한다. 지원하지 않는 요청은 실패 코드를 반환한다.

세션은 모든 효과를 먼저 검증한 후 일괄 적용한다. 알 수 없는 개체, 죽은 개체, 중복 개체 효과, 음수 행동 불능 횟수는 실행기 계약 오류로 예외를 발생시키며 부분 적용하지 않는다. HP·메모리는 0~최댓값으로 제한한다. 이 단계는 부활을 지원하지 않는다. 여러 효과가 동시에 양 팀을 전멸시키면 무승부다.

기본 `PrototypeActionResolver`는 흐름 검증용으로 다음만 지원한다.

- 적 한 명에게 `SkillData.Power`만큼 고정 피해.
- `Wait`로 자기 행동 종료.
- 방어, 0보다 큰 메모리 투자, 아군·자신 대상 스킬은 `UnsupportedAction` 반환.

명중·회피·치명타, 속성·연쇄·폭주·메모리 배율, 방어 효과, 몬스터 AI, 추가 행동, CSV 입력은 아직 구현하지 않았다. 진입 조건은 보관만 하며 추가 메모리 수치를 임의로 부여하지 않는다. 치명타·약점·연쇄 수치는 기획서 내 불일치를 확인한 뒤 후속 계산기에 반영한다.

## 검증 실행

Unity 6000.3.23f1에서 Test Runner의 EditMode 탭을 열고 `CK.Battle.Core.Tests`를 실행한다. 테스트는 전투 시작·종료, 메모리 정렬·동률·라운드, 결과 불변성, 중복 입력·완료 콜백, 사망 대상 제외, 행동 불능, 실행기 오류의 원자적 처리, 수치 범위를 검증한다.

코어 구현의 검증 결과: Unity 6000.3.23f1 EditMode 26개 통과, 실패·건너뜀 0개. 같은 테스트를 별도 .NET 실행기에서도 실행해 26개 통과했다.

데모 추가 후 Unity PlayMode 테스트 7개가 통과했다. 실제 컨트롤러의 공격·대상 선택·중복 입력 차단, 메모리 변화 후 순서 재계산, 동률·행동 불능 시나리오 재시작, 전체 전투의 승리·패배, 자동 연출 완료·몬스터 응답을 검증한다. UI 배치는 Game 뷰 화면으로 별도 확인한다. 최종 게임의 모델·연출·AI는 검증 범위에 포함되지 않는다.

명령줄 실행 예:

```text
Unity.exe -batchmode -nographics -projectPath <프로젝트 경로> -runTests -testPlatform EditMode -testFilter CK.SemesterProject.Battle.Tests -testResults <결과.xml> -logFile <로그.txt>
```

테스트 실행에는 `-quit`을 덧붙이지 않는다. Unity Test Runner가 실행 종료를 관리한다. 같은 프로젝트를 Unity에서 열어둔 경우 에디터 내 Test Runner를 사용한다.
