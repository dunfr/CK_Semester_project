# 푸시아 스킬 모션

그래픽 파트의 스킬 모션 `Assets/Normal Skill.fbx`, `Assets/Main Skills.fbx`를 푸시아(`FuchsiaUpdatedAnimated.prefab`)에 연결했다.
- FBX의 메시·바닥은 쓰지 않는다. 클립만 구워(`FuchsiaNormalSkill.anim`, `FuchsiaMainSkill.anim`) 기존 리그에 붙인다.
- `FuchsiaLocomotion.controller`의 상태 이름은 `NormalSkill`, `MainSkill`이다. `PlayerBattleAnimation` 스킬 모션 표에도 같은 이름으로 들어 있다.

## 미리보기

`Assets/CK_Semester_Project/Prototype/Scenes/FuchsiaSkillPreview.unity`를 열고 플레이한다. 왼쪽 위 버튼이나 숫자키로 재생한다(1 = Ult, 2 = NormalSkill, 3 = MainSkill, 표 순서). 0.25·0.5·1배 속도, 연출 카메라, 자동 반복을 켜거나 끌 수 있다. 자유 시점은 오른쪽 드래그로 회전하고 휠로 거리를 조절한다.

편집 중에는 푸시아 인스턴스를 선택하고 Animation 창에서 `FuchsiaNormalSkill` 또는 `FuchsiaMainSkill`을 열어 시간을 넘긴다. `FuchsiaSkillPreview`의 Editor Preview Skill에 스킬 ID를 지정한다. 비우면 첫 PhysCamera001 스킬을 쓴다. Game 뷰는 숨김 임시 카메라로 미리보기하고 Animation 미리보기가 끝나면 원래 카메라 화면으로 돌아간다. Main·Normal은 같은 두 노드를 사용한다.

## 굽기

`Tools > Fuchsia > 모션 클립 다시 만들기`로 기존 세 클립과 새 두 클립을 굽는다. 없는 Attack_idle·Ult·Jump 원본은 경고 후 건너뛰며 기존 .anim을 유지한다. 새 두 클립은 반복하지 않는다.

Normal은 `w/Bip001` 경로를 `Move/Bip001`로 바꾼다. 이름을 고정하지 않고 최상위 `X/Bip001` 규칙으로 찾는다. Move 시작 가로 위치와 Bip001 시작 X·Y를 빼고, 두 오프셋의 루트 공간 합계를 카메라 등 형제 노드에도 적용한다. 끝에서 모든 Transform 곡선이 멈춘 구간은 키와 stopTime을 함께 줄인다. Normal 타깃의 빠진 위치 곡선 등은 FBX 노드의 기본 로컬 값으로 보충한다. 카메라 회전은 쿼터니언 형식으로 맞춘다.

Main의 Move 이동·높이·회전은 안무이므로 시작 가로 위치만 빼고 유지한다. 두 클립에도 기존 카메라 컷 계단 처리와 정지 곡선 축소를 적용한다. 기존 세 클립에는 새 경로 보정·정지 구간 자르기·곡선 보충을 적용하지 않는다.

## 새 스킬 추가

1. FBX를 추가하고 Baker의 ClipSpec 목록에 출력 이름·반복 여부·카메라 노드를 넣어 굽는다. 새 스킬 보정이 필요하면 PrepareSkill을 켠다.
2. Controller에 상태와 구운 클립을 연결한다. 기존 Ult와 같은 Write Defaults를 쓰고 전이 없이 코드로 재생한다.
3. 프리팹 루트에 카메라·타깃 노드를 만들고 클립 0초의 카메라 곡선 값만 기본 자세로 넣는다. 프리팹 전체를 SampleAnimation해서 저장하지 않는다.
4. PlayerBattleAnimation 표에 스킬 ID, 상태, 접근 여부, 카메라·타깃과 가로 시야각을 넣는다. 미리보기 버튼은 표를 자동으로 읽는다.

## 가정과 남은 일

- Normal·Main 가로 시야각은 FBX에 값이 없어 36mm / 40mm 기준 48.5°로 가정했다. Ult는 기존 45°다.
- Skill_DT ID는 미정이라 `NormalSkill`, `MainSkill`을 임시 ID로 사용한다.
- 두 스킬의 ApproachTarget은 꺼 두었다. 실제 전투의 타격 타이밍·이동 연출 연결은 별도다.
- 스킬 이펙트는 아직 없다.
- 전투 씬(TutorialDummyModelingDemo)의 스킬 버튼에는 아직 연결하지 않았다. 실제 Skill_DT ID가 정해지면 표의 SkillId만 바꾼다.

## 검증 결과 (2026-11-04)

이 브랜치의 에디터:
- 컴파일 오류 없음(기존 UnityChan CS0436 경고만).
- 미리보기 씬을 열어 플레이를 시작하고 멈출 때 오류·경고 없음. BattleIdle로 대기하는 화면 확인.
- 플레이 뒤 씬이 바뀌지 않음.

아래는 같은 파일로 프로젝트 복사본(배치 Unity)에서 잰 결과다.

- 굽기: Normal 2.0초(루트 `w` → `Move`, 4.6MB), Main 4.0초(8.6MB). 두 클립 모두 시작 프레임 몸의 가로 위치 0.
  - Normal은 발이 바닥 위 ±1cm 안에 있다.
  - Main은 시작 때 발이 3cm 떠 있다(원본 그대로). 1.3초부터 0.85m 떠올라 옆으로 최대 1.2m 순간이동한 뒤 4.0초에 제자리로 돌아온다.
  - 카메라 컷은 두 클립 모두 없다.
- 연출 카메라 렌더: 두 스킬 모두 어깨 너머 구도로 푸시아를 잡는다. 몸·카메라가 뒤집히지 않았다.
- 플레이 모드(미리보기 씬): Ult·Normal·Main을 재생했다.
  - 매 프레임 메인 카메라가 연출 카메라 시점과 1mm·0.1° 안에서 같았다.
  - 끝나면 BattleIdle로 돌아오고 카메라 자세·시야각이 재생 전으로 돌아왔다.
- 확인하지 못한 것:
  - 사용자 에디터에서의 직접 조작(버튼 클릭·숫자키·오른쪽 드래그)
  - Animation 창으로 시간을 넘길 때의 Game 뷰 연출 시점과, 그때 씬이 더러워지지 않는지

