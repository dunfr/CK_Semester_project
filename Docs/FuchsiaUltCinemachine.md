# Fuchsia Ult · 영상 기준 Cinemachine Timeline

씬: `Assets/CK_Semester_Project/Prototype/Scenes/FuchsiaUltCinemachinePreview.unity`

## Timeline에서 재생하고 편집하기

1. 씬을 열고 Hierarchy의 **Fuchsia Ult Cinemachine Timeline**을 선택한다.
2. Timeline 창의 **Play** 버튼을 누른다. 편집 모드에서 재생·스크럽이 가능하다. 창이 다른 대상을 보고 있으면 `Tools > Fuchsia > 영상 카메라 Timeline 열기`를 실행한다.
3. 아래쪽 **Cinemachine · 영상 카메라** 트랙의 카메라 아이콘이 있는 클립을 선택한다. 트랙 헤더에는 연결 대상인 `Main Camera (Cinemachine Brain)`이 표시될 수 있다.
4. 클립을 좌우로 이동하면 시작 시점이 바뀐다. 양끝을 드래그하면 해당 카메라 무브 전체가 새 길이에 맞춰 재생된다. Inspector의 **Clip Timing > Start / Duration**으로 정확한 수치를 입력해도 된다.

클립 Inspector의 **Camera**는 실제 Cinemachine Camera, **Motion**은 그 카메라의 위치·회전 키가 있는 애니메이션이다. **Source In / Source Out**은 사용할 원본 구간이다. 이 구간은 유지하면서 Duration만 늘리면 느려지고, 줄이면 빨라진다. 음수 Source In은 원본 첫 프레임을 유지하는 준비 구간이다.

**Horizontal Field Of View**는 가로 시야각이다. 0으로 설정하면 해당 Cinemachine Camera의 Lens 값을 그대로 사용한다. **Hierarchy에서 Cinemachine 카메라 찾기**로 연결된 카메라를 찾을 수 있다. 위치·회전 키 자체를 변경하려면 해당 Motion을 Animation 창에서 편집한다. 각 숏은 별도 카메라와 별도 Motion 에셋을 사용한다.

## 영상에 맞춘 기본 구성

첨부한 약 9.07초 영상의 컷과 흐름을 기준으로 9개 숏을 구성했다. 기본 Timeline은 `Cinematics/FuchsiaUlt/ReferenceEditable/TL_FuchsiaUlt_ReferenceEditable.playable`이다.

| 숏 | 기본 구간(초) | 구도 |
| --- | --- | --- |
| 01 | 0.00–1.40 | 준비·발도, 넓은 전신 구도 |
| 02 | 1.40–2.33 | 눈 클로즈업 (0.5배속) |
| 03 | 2.33–3.10 | 발 로우앵글 |
| 04 | 3.10–8.17 | 정면 조준 (0.25배속) |
| 05 | 8.17–8.60 | 눈 확대 |
| 06 | 8.60–9.03 | 어깨 너머 |
| 07 | 9.03–10.30 | 표적 타격 |
| 08 | 10.30–12.37 | 후퇴·회전 |
| 09 | 12.37–13.33 | 마무리 |

30fps 기준 02 숏은 42–70프레임 구간에서 0.5배속, 04 숏은 93–245프레임 구간에서 0.25배속으로 재생된다. 카메라·캐릭터·표적 및 투사체·검 스파크의 속도를 맞췄고, 마법진에도 04 숏의 속도를 적용했다.

원본 FuchsiaUlt의 카메라 곡선을 사용하고, 영상의 준비 구간과 숏 타이밍에 맞췄다. 전신 숏은 가로 시야각 65°, 발 숏은 50°, 나머지는 45°로 구성했다. 참고용 체크 바닥, 노란 표적, 원본의 분홍 구체도 배치했다. 영상과 모델·재질·렌더링 환경이 달라 화면이 픽셀 단위로 같지는 않다.

Timeline 길이는 **Based On Clips**다. 카메라 클립을 뒤로 늘려도 기존 8.633초에서 강제로 중단하지 않는다. 캐릭터/구체 트랙은 독립적이므로 카메라만 늦추면 캐릭터와의 상대 타이밍도 바뀐다.

## Play Mode 버튼

- **1:** 애니메이션 + 카메라
- **2:** 애니메이션만 (기본 시점)
- **3:** 카메라만 (캐릭터 대기)
- **Space:** 일시 정지 / 계속
- **Esc:** 정지·기본 시점 복귀
- 시간 슬라이더와 0.25 / 0.5 / 1배 버튼 지원

PlayableDirector가 시간을 관리한다. 화면 버튼과 Timeline transport가 동일한 그래프를 사용하며, 별도의 Update/LateUpdate 시계로 Timeline 시간을 덮어쓰지 않는다. Director는 Unscaled Game Time, Brain은 Late Update다. 실제 출력 카메라의 위치·회전은 Cinemachine Brain이 적용한다.

## 에셋과 다시 생성

- `Reference Cinemachine Cameras` 아래에 숏별 실제 Cinemachine Camera가 있다.
- `ReferenceEditable` 폴더에 숏별 Motion, 동시/단독 재생 Timeline, 참고용 재질이 있다.
- 기존 `FuchsiaUlt_Character.anim`은 리그/메시만 포함하고 카메라 곡선은 포함하지 않는다. 원본 `FuchsiaUlt.anim`은 그대로 유지한다.
- `Tools > Fuchsia > 영상 카메라 편집 트랙 추가 또는 재생성`은 현재 미리보기 씬의 생성된 숏과 기본 타이밍을 다시 만든다. **사용자가 수정한 카메라 클립·타이밍은 기본값으로 덮어쓴다.** 평소에는 이 메뉴를 실행하지 않고 Timeline에서 편집한다.
- `Tools > Fuchsia > 궁극기 Cinemachine 검증`은 별도 Preview Scene에서 검사한다. 결과는 Console과 `Temp/FuchsiaUltReferenceTimelineValidation.txt`에 남는다.

## 검증 (2026-11-17)

Unity 6000.3.23f1 / Cinemachine 3.1.7에서 확인했다.

- 566개 시점 및 컷 경계에서 원본 곡선과 Timeline/Brain 출력 비교: 카메라 최대 위치 차이 약 0.084mm, 회전 차이는 Unity 비교 정밀도에서 0°.
- 캐릭터 원본 모션 동기화, 역방향 스크럽, 클립을 12초로 늘렸을 때의 자동 리타이밍, Timeline 길이 연장, 클립 사이 빈 구간의 기본 카메라 복귀 확인.
- 실제 Timeline Play 버튼으로 편집 모드 재생 확인.
- 실제 Play Mode에서 직접 Director.Play, 10개 카메라 시점, 일시 정지/계속, 재시작, 3개 재생 모드, 자연 종료·복귀 확인.

## CherryBlossomMagicCircle 컨트롤 트랙

기본 ReferenceEditable Timeline의 마법진 컨트롤 클립은 2.33–8.60초에 재생된다. 씬의 Fuchsia Ult Cinemachine Timeline 아래 CherryBlossomMagicCircle이 제어 대상이다. 클립을 이동하거나 길이를 바꿔 타이밍을 편집할 수 있다.

CherryBlossomTimelineControl이 원본의 마법진 등장·회전, 빔 페이드, 순차 파티클을 클립 시간으로 샘플링한다. 원본 코루틴 스크립트 두 개는 씬 인스턴스에서 끄고, 클립의 Control ITimeControl과 Search Hierarchy를 켰다. 파티클 시간도 어댑터가 관리하므로 Control Particle Systems는 껐다. 클립 종료 후 오브젝트는 비활성화된다.

