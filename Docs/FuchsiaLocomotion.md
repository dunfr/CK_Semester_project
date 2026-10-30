# Fuchsia 이동 애니메이션

적용 씬: `Assets/CK_Semester_Project/Prototype/Scenes/TutorialGrayboxingDemo.unity`

## 연결 구조와 조작

- `Fuchsia Player`: 기존 CharacterController, PlayerMovement, 층 이동 및 카메라 연결.
- 자식 `Fuchsia Visual`: `FuchsiaAnimated.prefab`의 인스턴스. 리깅된 모델, Animator, 검을 포함한다.
- Game 뷰를 클릭해 마우스를 잠근 뒤 WASD로 걷고 Shift를 누른 채 이동하면 달린다. 기존 달리기 토글도 사용 가능하다.
- 땅에 닿아 있을 때 Space를 누르면 점프한다(공중에서는 다시 뛰지 않음). TutorialBattleController의 몬스터 유인 '소리'(반경 12m)는 Space에서 Q로 옮겼고 필드 안내 문구도 "Q 소리"로 바꿨다(F는 전투 중 투자 단계 전환에 쓰여 피함). `Assets/EnemyAI`의 `enemy_test` 데모 플레이어는 따로라 그대로 Space다.
- 입력이 없거나 이동 기능이 비활성화되면 Idle로 돌아간다.

## 에셋

기본 경로: `Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/`

- `Prefabs/FuchsiaAnimated.prefab`: `Assets/Ani_perpef(test)/Idle/idle.fbx`에서 만든 프리팹 Variant. 유효한 Generic `idleAvatar`와 7개 SkinnedMeshRenderer를 사용한다.
- `Animations/FuchsiaLocomotion.controller`: 플레이어 모션 상태 6개. 필드 `Idle` / `Walk` / `Run` / `Jump`(이동 FSM), 전투 `BattleIdle` / `Ult`(전투 모션 FSM). 전이와 파라미터 없이 코드가 상태마다 CrossFade로 넘긴다.
- 모션(그래픽 파트 최종본, `Assets/FuchsiaAnimation`, shader-development-3에서 적용): Idle·Walk·Run은 `standing_idle.fbx`, `walking.fbx`, `Running.fbx` 클립(Take 001)을 그대로 쓴다. 전투 모션 `Attack_idle.fbx`, `Ult.fbx`는 Bip001이 시작부터 옆으로 41cm·앞으로 7cm 밀려 있어, 그 가로 오프셋만 뺀 `Animations/FuchsiaAttackIdle.anim`(반복), `FuchsiaUlt.anim`(8.6초, 반복 없음)을 쓴다. 점프 `Jump.fbx`(0.97초)는 몸이 52cm 떠오르는 높이가 모션에 들어 있어, 그 높이를 잘라 낸 제자리 클립 `FuchsiaJump.anim`(반복 없음)을 쓴다. FBX가 바뀌면 메뉴 `Tools > Fuchsia > 모션 클립 다시 만들기`(`Editor/FuchsiaMotionClipBaker.cs`)로 세 클립을 다시 만든다(GUID 유지).
- 클립 크기: 원본 FBX는 압축 없이 모든 곡선에 매 프레임 키가 있어, 그대로 구우면 `FuchsiaUlt.anim`이 122MB로 GitHub 파일 한도(100MB)를 넘는다. 그래서 굽기 도구가 거의 움직이지 않는 곡선(값의 폭이 위치 0.1mm, 회전·크기 0.00001 이하)을 처음 값의 키 두 개로 줄인다. 연출 카메라 노드는 줄이지 않는다. 줄인 뒤 Ult 62MB, Attack_idle 2.3MB, Jump 3.0MB이고, 줄이기 전과 반 프레임마다 포즈를 비교한 차이는 위치 0.15mm 이하, 회전은 측정되지 않을 만큼 작다(2026-10-30). Ult는 아직 GitHub 권장 크기(50MB)보다 커서 푸시할 때 경고가 나올 수 있다.
- 2026-10-29 수정본(unitypackage) 반영: 패키지가 기존 FBX와 GUID가 같은 `standing_idle` / `walking` / `Running` / `Attack_idle`은 제자리에서 덮어쓰고 새 `Jump`만 `1029` 폴더에 넣었다. 예전 `jump.fbx` / `jump.controller`는 휴지통으로 보내고 새 `Jump`를 `Assets/FuchsiaAnimation`으로 옮겼으며, `FuchsiaAttackIdle.anim`은 새 Attack_idle로 다시 구웠다.
- 페이셜: 모션 FBX(standing_idle, walking, Running, Attack_idle, Jump, 새 Ult)에는 얼굴 블렌드셰이프 커브(경로 `head`, EYE_closed, EB_anger, EB_down, Mouth_open, smick, EB_aww)가 있다. 머리 툰 메시에 같은 표정 6개를 옮겨(`Tools/Fuchsia/Transfer Head Expressions`) 표정이 재생된다.
- **2026-10-30 Ult 수정본(1029 폴더):** 예전 `Ult.fbx`·`Ult.controller`는 휴지통으로 보내고, 새 파일을 `Assets/FuchsiaAnimation`으로 옮긴 뒤 빈 `1029`를 지웠다. 새 Ult는 카메라·몸 동작과 컷 위치가 예전과 같고, 얼굴 표정 커브 6개가 새로 들어 있다.
  - 임포트 설정 Bake Axis Conversion이 켜져 있으면 몸과 카메라가 Fuchsia 리그 규약과 180° 어긋난다. 궁극기를 쓰는 순간 캐릭터가 뒤로 돌아 몬스터 반대쪽을 본다. 그래서 다른 모션 FBX처럼 껐다.
  - 받은 대로 둔 설정: 애니메이션 압축 끔, 클립 범위 1~260.
- 예전 모션 `Assets/Ani_perpef(test)`와 `Animations/FuchsiaIdle.anim` / `FuchsiaWalk.anim` / `FuchsiaRun.anim`은 Controller에서 더 이상 쓰지 않는다. `Ult.fbx` 안의 연출 카메라(`Ult_cam`, `Ult_cam.Target`)는 아래 '궁극기 연출 카메라'처럼 쓰고, 이펙트 메시(`Sphere001`, `petal`, `Cylinder001`) 애니메이션은 아직 쓰지 않는다.
- 기존 얼굴·머리·의상 툰 머티리얼과 투명 안경알을 재사용한다. 검은 `Materials/FuchsiaKatanaToon.mat`에서 제공된 Katana_BaseColor 텍스처를 사용한다.

PlayerMovement의 Animator 필드는 새 자식 Animator를 참조한다. Controller에 Idle / Walk / Run 상태가 있으면 상태마다 자기 모션을 재생하고, 없으면(다른 씬의 UnityChan 등) 기존 Speed/Direction 파라미터 방식을 쓴다. 기본 걷기는 3m/s, 달리기는 5.4m/s이며 Root Motion은 꺼져 있다.

모델 스케일은 기존의 1.07563651을 유지하고 새 리그의 원점과 CharacterController 접촉 여유에 맞게 로컬 위치를 `(0, -0.025, 0)`으로 조정했다. 렌더러의 컬링 범위는 세 모션과 Blend Tree의 중간 자세를 샘플링해 각 rootBone 공간에서 계산했다.

기존 정적 `FuchsiaToon.prefab`과 모션 FBX의 임포트 설정은 유지했다. 다른 씬의 Fuchsia는 이번 작업으로 교체하지 않았다.

## 검증 — 2026-10-14

- Unity 스크립트 재컴파일 성공.
- 임시 프리뷰에서 실제 Animator Controller를 평가했을 때 Speed 0/0.5/1에 각각 Idle/Walk/Run이 연결되고 본이 움직임.
- Play 모드에서 기존 Move 메서드를 입력 벡터·달리기 플래그로 호출하는 프로브로 정지 → 걷기 → 달리기 → 정지를 검증. 실제 속도 0 / 3.0 / 5.4 / 0m/s 및 접지 유지 확인.
- 이동 컴포넌트를 비활성화하면 Speed가 0으로 초기화됨.
- 세 모션과 Speed 0 / 0.25 / 0.5 / 0.75 / 1의 중간 자세를 검사했으며, 실제 씬의 회전·스케일을 적용한 Play 모드에서도 컬링 범위 밖 정점 0개.
- 게임 카메라로 모델·툰 재질·검·그림자 확인. 프로브 수행 중 새로운 런타임 오류 없음.
- 프로브는 실제 사용자 키보드 조작을 대신하지 않는다. 기존 UnityChan SpringBone/SpringManager 중복 타입 컴파일 경고는 별도 문제다.

![Fuchsia 애니메이션 적용 화면](Images/FuchsiaLocomotion.png)

## 발과 그림자 접점 보정

초기 Visual 높이 +0.02m에서 확인한 Idle 포즈의 밑창 높이는 46.99622m, 바닥 메시 표면은 46.95271m로 약 4.35cm 간격이 있었다. CharacterController의 Skin Width 0.03m에 의한 접촉 여유와 모델 높이 보정이 합쳐져, 멀리서는 발이 붙어 보이면서도 그림자는 분리되어 보였다.

같은 포즈에서 광원의 Depth/Normal Bias를 낮추거나 0으로 바꾸어도 간격이 남는 것을 확인했다. 광원은 원래 설정(URP 파이프라인 설정 사용)으로 복원하고 Visual 로컬 Y만 -0.025m로 수정했다. 해당 포즈의 밑창과 바닥 차이는 약 -0.00148m로 줄었다. 이 보정은 모델 전체의 기준 높이를 조정하며 지형 대응용 발 IK는 추가하지 않는다.

![발과 그림자 접점 보정](Images/FuchsiaShadowContact.png)


## 2026-10-15 새 FBX 적용 — 기존 리깅 유지

브랜치 `feat/tutorial-demo-grayboxing`을 원격 `9b542b0`까지 갱신한 뒤 제공된 `character_fuchsia_01 (1).fbx`를 기존 `Models/character_fuchsia_01.fbx`에 적용했다. 원본 FBX의 GUID는 유지한다.

전체 정적 외형에 가중치를 새로 계산하던 이전 방식은 제거했다. 현재 `FuchsiaUpdatedAnimated.prefab`은 수정 전 `FuchsiaAnimated.prefab`의 기존 **7개 SkinnedMeshRenderer, 본, 본 가중치, bindpose, 삼각형, 머티리얼, Avatar와 Animator Controller**를 유지한다. 머리·머리카락·안경·외투·치마·검은 기존 메시 그대로 사용한다.

새 FBX와 교체 전 FBX를 비교하면 실질적인 메시 변경은 `몸` 부품에 한정된다. 기존 리깅 body 메시 안에서 동일 UV와 위치로 대응되는 883개 정점에 새 FBX의 외형 데이터를 적용하고, 나머지 정점과 리깅 데이터는 그대로 둔다. 교체된 스킨 메시 하나만 `Models/FuchsiaUpdatedBody.asset`에 저장한다. 벨트의 내보내기 변환 차이는 외형 위치를 바꿀 수준의 변경이 아니므로 기존 리깅 위치를 유지한다.

튜토리얼 씬의 `Fuchsia Visual`은 새 프리팹을 참조한다. 이동·전투 코드 및 모션 클립은 변경하지 않았다. 이전에 생성했던 35개 재스키닝 부품 메시와 비교용 임시 FBX는 제거했다.

검증:

- 원본 대비 7개 렌더러의 본 가중치·bindpose·삼각형·머티리얼 일치.
- Idle/Walk/Run 각각 5개 시점, 총 15개 포즈에서 전체 정점의 원본 대비 최대 위치 차이 `0.000000648m`.
- 기존 리그와 동일한 모션으로 재생되는 정면·측면 렌더 확인.
- 전체 튜토리얼/전투 완주와 UI 화면 비율 검사는 이번 모델 교체 범위에서 실행하지 않았다.

아래는 교체 후 기존 Run 모션을 적용한 렌더다. 원본 모션의 손·소매 간격 등은 새 웨이트를 계산해 변경하지 않는다.

![기존 리깅 유지 후 Run](Images/FuchsiaModelFixRun.png)


## 전투 대기 모션

전투 진입 시 TutorialBattleController는 실제 자식 Fuchsia 모델의 활성 Animator와 `PlayerBattleAnimation`을 찾는다. 이동을 비활성화하고 `PlayerBattleAnimation.EnterBattle()`로 전투 대기(`BattleIdle`) 모션을 반복한다(전투 모션이 없는 캐릭터는 PlayerMovement의 Idle 또는 파라미터 방식 Speed 0). 전투 종료/취소 시 `ExitBattle()` 후 이전 Animator 재생 속도를 복원하고, 다시 켜진 PlayerMovement가 필드 Idle 모션으로 되돌린 뒤 입력에 맞춰 Walk/Run 상태로 넘어간다.

### 전투 모션 FSM(shader-development-3)

- `PlayerBattleAnimation`(Fuchsia 프리팹 루트): 전투 중 모션의 FSM. `EnterBattle` → `PlayerBattleIdleState`(BattleIdle 반복), `PlaySkill(스킬 ID)` → 스킬 모션 표에 있으면 `PlayerBattleActionState`가 그 모션을 끝까지 재생하고 BattleIdle로 돌아온다. `ExitBattle`로 상태를 비운다. 상태 기반 클래스는 `PlayerBattleState`.
- 스킬 모션 표: 지금은 `SK10`(테스트_궁극기) → `Ult`, 대상에게 다가가지 않음. 표에 없거나 Controller에 상태가 없는 스킬은 BattleIdle을 유지한다(기본 스킬·특수기 모션은 아직 없음).
- 전투 연출: 플레이어가 표에 있는 스킬을 쓰면 `PresentationSeconds`가 모션 길이(Ult 8.6초)만큼 늘어난다. 표에서 다가가기를 끈 스킬은 `TutorialBattleActionPresentation.Sample(..., approachTarget: false)`로 제자리에서 대상을 바라본다. 타격·피격 연출은 기존처럼 전체 시간의 35~55% 구간이라, 모션의 실제 타격 프레임에 맞추려면 따로 조정해야 한다.

### 궁극기 연출 카메라(Ult_cam)

- **원본:** `Ult.fbx`에는 3ds Max 타깃 카메라가 Null 노드 두 개(`Ult_cam` = 카메라, `Ult_cam.Target` = 타깃)로 들어 있다. Camera 컴포넌트와 시야각 정보는 없고, 노드 회전은 Max 규약대로 -Z가 시선이다.
- **프리팹:** `FuchsiaUpdatedAnimated.prefab` 루트 바로 아래에 같은 이름의 빈 노드 두 개를 두었다(기본 자세는 Ult 첫 프레임). Ult 상태가 재생되는 동안 `FuchsiaUlt.anim`이 이 노드를 움직인다.
- **설정:** `PlayerBattleAnimation` 스킬 모션 표 SK10의 Cinematic Camera = `Ult_cam`, Cinematic Camera Target = `Ult_cam.Target`, Cinematic Field Of View = 45.
  - 시야각은 Max 카메라와 같은 가로 기준이다. FBX에 값이 없어 Max 기본값 45°로 가정했고, 16:9에서 세로 약 26°가 된다. 구도가 좁거나 넓으면 이 값만 바꾼다.
- **전환:** SK10 모션이 재생되는 동안 `TutorialBattleController.LateUpdate`가 메인 카메라를 따라가지 않고 그 시점에 바로 맞춘다.
  - 위치는 `Ult_cam`, 시선은 `Ult_cam.Target` 쪽, 기울기(roll)는 `Ult_cam` 회전을 따른다.
  - 그동안 마우스 카메라 드래그는 무시한다.
- **복구:** 모션이 끝나 BattleIdle로 돌아가면 연출 직전의 전투 시점·시야각으로 되돌리고, 기존 전투 카메라가 이어서 움직인다. 전투가 끝나거나 전투 카메라가 꺼지면 연출 상태를 지운다.
- **굽기(`FuchsiaMotionClipBaker`):**
  - Ult의 몸을 가운데로 옮긴 만큼(옆 41cm, 앞 7cm) 루트 바로 아래 노드(카메라·이펙트)도 옮겨 원본 구도를 유지한다.
  - 숏이 바뀌는 컷 5곳(25·39·62·100·113프레임)은 원본에서 한 프레임 동안 보간되어 카메라가 두 숏 사이를 훑는다. 그래서 카메라·타깃 위치를 계단 키로 바꿔 한 번에 넘어가게 했다. 컷 키의 가파른 탄젠트가 앞뒤 숏으로 번져 생기던 흔들림도 없앴다.
  - 회전(쿼터니언) 곡선은 실행 중에 키 탄젠트를 쓰지 않아 계단이 듣지 않는다. 그래서 시선을 두 노드의 위치로 정한다.
- **아직 없는 것:** 연출 중 전투 UI 숨기기, 이펙트 메시(`Sphere001` 등) 재생. 4.5~5.5초 숏은 원래 이펙트(커지는 구체)를 보여 주는 숏이라, 지금은 몬스터만 가깝게 보인다.
- **사용 조건:** 궁극기 버튼은 폭주 80, 메모리 40이 있어야 켜진다.
- **실제 씬 확인(2026-10-30, 새 Ult):**
  - TutorialDummyModelingDemo를 플레이해 전투를 열고 플레이어 폭주를 85로 넣은 뒤, 궁극기 버튼과 같은 `UseSkill`(SK10)을 눌렀다.
  - 1077프레임(0.05~8.63초) 동안 메인 카메라가 Ult_cam 시점과 위치·각도·시야각 오차 0으로 같았다(시야각 세로 26.2°).
  - 끝나면 BattleIdle로 돌아가며 직전 전투 시점(1cm 차이)과 시야각 56°로 돌아왔다.
  - 장면 캡처: 0.4초 어깨 너머 숏, 1.2초 칼 손잡이 뒤 얼굴 클로즈업(눈 반쯤 감음), 3.6초 눈 클로즈업, 6.5초 기울인 숏, 종료 뒤 전투 시점.
- **참고:** 플레이 시작 직후 기존 `TutorialStartSequence.cs` 146줄의 NavMeshAgent `ResetPath` 오류가 난다. Console의 Error Pause가 켜져 있으면 플레이가 바로 일시정지된다(이번 작업과 무관).

새 스킬 모션 추가:

1. FBX를 받아 Controller에 상태를 추가하고 클립을 연결한다(Bip001 오프셋이 있으면 `FuchsiaMotionClipBaker`의 클립 표에 넣어 중앙 정렬 클립을 만든다). 연출 카메라가 있으면 그 노드 이름을 클립 표의 카메라 노드에 넣고, 프리팹 루트에 같은 이름의 노드를 만들어 스킬 모션 표의 Cinematic Camera·Target에 연결한다.
2. Fuchsia 프리팹의 `PlayerBattleAnimation` 스킬 모션 표에 스킬 ID와 상태 이름을 추가한다. 모션에 이동이 없으면 "Approach Target"을 켜서 기존 접근·복귀 연출을 함께 쓴다.
3. 피격·방어·승리처럼 스킬이 아닌 전투 상태는 `PlayerBattleState`를 상속한 클래스로 만들고 TutorialBattleController에서 그 상태로 바꾼다.

## 2026-10-29 이동 FSM(상태 패턴)

`Assets/CK_Semester_Project/Prototype/Scripts/Tutorial/`

- `PlayerMovement`: FSM의 주인. 매 프레임 입력을 `PlayerMovementInput`으로 모으고, 현재 상태의 `GetNextState`로 다음 상태를 고른 뒤(바뀌면 이전 상태 `Exit` → 새 상태 `Enter`) `Tick`으로 이동시킨다.
- `PlayerMovementState`: 상태 하나의 기반 클래스. `Enter` / `Exit` / `GetNextState` / `Tick`과 Animator 상태 이름(기본은 상태 이름과 같음)을 가진다. 기본 `Enter`는 같은 이름의 Animator 상태로 0.15초 CrossFade한다.
- `PlayerGroundedState`: 땅 위 상태 공통 전이. 땅에 닿아 있을 때 Space → Jump(이동 전이보다 먼저 검사), 입력 없음 → Idle, 이동 → Walk, 이동 + Shift(또는 HUD 달리기 토글) → Run. Shift만 눌러서는 달리지 않는다. 이동만으로 고르는 규칙은 `SelectByMove`로 두어 착지한 점프도 같은 규칙으로 돌아온다.
- `PlayerMoveState`: 걷기·달리기 공통. 정해진 속도로 이동하고, 벽에 막혀 실제로 거의 움직이지 못하면 상태는 그대로 둔 채 Idle 모션을 보여 준다(제자리 걸음 방지).
- `PlayerIdleState` / `PlayerWalkState` / `PlayerRunState`: 상태별 이름과 이동 속도만 정한다.
- `PlayerJumpState`: 아래 점프 규칙.

### 점프(shader-development-3)

- 입력: `PlayerMovementInput.JumpRequested`(이번 프레임에 Space를 누른 순간). 이동 키처럼 게임 화면에 마우스가 잠겨 있을 때만 읽는다.
- 도약: Jump 모션 앞부분 웅크림 동안(`PlayerMovement`의 Jump Takeoff Delay, 기본 0.1초) 땅에 있다가 발을 떼며 Jump Height(기본 0.5m)까지 오르는 속도를 받는다. 중력 20m/s²에서 체공은 약 0.45초로, 모션의 도약(0.13초)~착지(0.53초) 구간과 맞는다.
- 공중: 이동 입력으로 방향을 바꾸고, 속도는 뛰기 직전 그대로다(달리다 뛰면 달리기 속도, `IsSprinting` 유지). 공중에서 누른 Space는 무시한다. 머리가 천장에 닿으면 더 오르지 않는다.
- 착지: 이동 입력이 있으면 곧바로 Walk / Run, 없으면 Jump 모션의 착지 동작(약 0.4초)이 끝난 뒤 Idle. 착지 동작 중 Space를 다시 누르면 같은 상태에서 처음부터 다시 뛴다.
- 모션 높이: 실제 높이는 CharacterController가 내고 `FuchsiaJump.anim`은 제자리 모션이라 몸이 두 번 떠오르지 않는다. 높이·도약 시간을 바꾸면 모션의 공중 구간과 어긋날 수 있다.
- 아직 없는 것: 낙하 모션(점프하지 않고 턱에서 떨어질 때는 이전 모션 그대로), 높은 곳에 일찍 착지했을 때 모션을 착지 동작으로 건너뛰기.

### 새 상태 추가(예: 낙하)

1. 새 입력이 필요하면 `PlayerMovementInput`에 값을 추가하고 `PlayerMovement.Update`에서 채운다.
2. `PlayerMovementState`를 상속한 상태 클래스를 만들어 `Enter` / `Tick` / `GetNextState`를 정한다(`PlayerJumpState` 참고). 수직 속도·접지 상태를 다룰 메서드는 PlayerMovement에 둔다.
3. `PlayerMovement`에 상태 인스턴스를 두고, 그 상태로 들어가는 전이를 들어갈 수 있는 상태의 `GetNextState`에 넣는다.
4. 모션이 생기면 Controller에 같은 이름의 상태를 추가하고 클립을 연결한다. Controller에 그 상태가 없는 동안에는 이전 모션을 유지하고 경고를 한 번만 남긴다.

검증(shader-development-3, 점프·궁극기 연출 카메라 추가 후): PlayMode 테스트 16개 통과.
- `PlayerLocomotionStateTests` 6개: 기존 3개 + 땅에서 한 번만 0.5m 점프 후 Idle 착지(공중 Space 무시), 달리기 점프의 공중 속도 유지와 이동 입력 착지 시 Walk, 떨어지는 중 Space 무시.
- `PlayerAnimationStateTests` 5개: 기존 3개 + 실제 프리팹에서 Jump 상태가 `FuchsiaJump` 클립을 재생하고 몸이 충돌체 기준으로 떠오르지 않음(제자리)·착지 동작 뒤 Idle, 착지 동작 중 Space로 모션을 처음부터 다시 재생하며 다시 뜀.
- `PlayerBattleAnimationTests` 5개: 전투 대기·필드 복귀, 궁극기 제자리 재생과 시작 자세 중앙, 모션 없는 스킬(연출 카메라 없음), 궁극기 뒤 BattleIdle 복귀와 연출 카메라 해제, 새 연출 카메라 테스트.
  - 새 테스트는 첫 숏이 푸시아를 보는지, 시야각 45인지 본다.
  - 첫 컷 직전까지 위치·시선이 첫 숏에 머물고 컷 뒤에는 다음 숏으로 넘어가는지 본다.
  - 원본 Ult.fbx와 비교해 몸 기준 카메라 위치가 3cm, 시선이 1° 이내인지 본다.
- 구운 Ult를 1/240초 간격으로 샘플링해, 컷 5곳 모두 위치와 시선이 한 번에 바뀌고 빠른 돌리 이동은 부드럽게 남는 것을 확인했다.
- 실제 키보드 입력과 씬 플레이는 테스트에 포함되지 않는다. TutorialBattleController의 실제 카메라 전환·복구도 씬 플레이로 확인해야 한다(씬 기반 테스트는 이전부터 SetUp에서 멈춤).

이전 검증(feat/tutorial-demo-grayboxing): PlayMode 테스트 `PlayerLocomotionStateTests` 3개와 새 `PlayerAnimationStateTests` 3개 통과(실제 프리팹·Controller로 상태별 모션, 벽에 막힘, 이동 비활성화 시 Idle, 각 모션에서 몸이 충돌체 위에 남는지 확인). `TutorialBattleCameraSettingsTests`·`PrototypeHudIntegrationTests`는 `65e9470` 이후 씬의 전투 컨트롤러 `_enemies`가 10개라 SetUp에서 멈춘다(이번 변경 전부터, 미수정).
