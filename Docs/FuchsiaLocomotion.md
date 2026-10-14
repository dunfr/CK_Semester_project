# Fuchsia 이동 애니메이션

적용 씬: `Assets/CK_Semester_Project/Prototype/Scenes/TutorialGrayboxingDemo.unity`

## 연결 구조와 조작

- `Fuchsia Player`: 기존 CharacterController, PlayerMovement, 층 이동 및 카메라 연결.
- 자식 `Fuchsia Visual`: `FuchsiaAnimated.prefab`의 인스턴스. 리깅된 모델, Animator, 검을 포함한다.
- Game 뷰를 클릭해 마우스를 잠근 뒤 WASD로 걷고 Shift를 누른 채 이동하면 달린다. 기존 달리기 토글도 사용 가능하다.
- 입력이 없거나 이동 기능이 비활성화되면 Idle로 돌아간다.

## 에셋

기본 경로: `Assets/CK_Semester_Project/Prototype/Sandbox/Junmo/FuchsiaShading/`

- `Prefabs/FuchsiaAnimated.prefab`: `Assets/Ani_perpef(test)/Idle/idle.fbx`에서 만든 프리팹 Variant. 유효한 Generic `idleAvatar`와 7개 SkinnedMeshRenderer를 사용한다.
- `Animations/FuchsiaLocomotion.controller`: float `Speed`를 사용하는 1D Blend Tree. Idle 0, Walk 0.5, Run 1.
- `Animations/FuchsiaIdle.anim`, `FuchsiaWalk.anim`, `FuchsiaRun.anim`: 각 FBX의 모션을 복사한 반복 재생용 클립. Biped 루트의 수평 시작 오프셋을 제거해 모션 전환 시 위치가 튀지 않도록 했다. 원래의 전투 대기 자세와 방향 변화는 유지한다.
- 기존 얼굴·머리·의상 툰 머티리얼과 투명 안경알을 재사용한다. 검은 `Materials/FuchsiaKatanaToon.mat`에서 제공된 Katana_BaseColor 텍스처를 사용한다.

PlayerMovement의 Animator 필드는 새 자식 Animator를 참조하며 Normalize Animation Speed가 활성화되어 있다. 실제 CharacterController 수평 이동 속도를 파라미터로 전달한다. 기본 걷기는 3m/s, 달리기는 5.4m/s이며 Root Motion은 꺼져 있다. 다른 씬에서는 기본값 false로 기존 Speed/Direction 전달 방식을 유지한다.

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
