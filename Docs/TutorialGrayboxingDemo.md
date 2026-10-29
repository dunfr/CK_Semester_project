# 튜토리얼 그레이박스 데모

브랜치: `feat/tutorial-demo-grayboxing`.
현재 필드·전투 HUD는 `prototype-ui`의 UI를 사용하며 실제 데이터 연결과 조작은 [UI 통합 안내](PrototypeUiIntegration.md)를 따른다.
기준: `prototype`의 `c1c1349`에서 생성했다. `feat/map-grayboxing-fuchsia`의 맵·푸시아 모델·이동 스크립트·ProBuilder 의존성을 가져왔으며, 작업 당시 로컬에 있던 최신 맵·카메라·층 이동 버튼 수정도 데모에 복사했다. 원본 작업 폴더의 미커밋 수정은 보존했다.

## 실행

`Assets/CK_Semester_Project/Prototype/Scenes/TutorialGrayboxingDemo.unity`를 열고 Play한다. Build Settings에서도 첫 씬으로 등록했다.

- WASD로 이동하고 마우스로 카메라를 회전한다.
- Esc로 마우스를 해제하면 좌측 상단의 1층/2층 이동 버튼을 사용할 수 있다.
- 몬스터 근처에서 좌클릭하면 플레이어 선제로 진입하며, 몬스터와 가까이 접촉하면 몬스터 선제로 진입한다.
- 전투에서는 기존 스킬·투자·방어 UI를 사용한다. 전투 중 층 이동 버튼은 비활성화된다.

## 몬스터 연결

원본 `emey` 및 `emey (1)`~`emey (9)`의 10기를 모두 사용한다. 각 박스를 발 위치의 `... AI` 부모 아래에 두고 NavMeshAgent·EnemyStateMachine·TutorialMonster를 연결했다. 박스 메시와 외형은 유지하며, 시작 지점은 같은 구역의 가까운 NavMesh로 보정했다.

| 원본 박스 | 기존 전투 AI |
| --- | --- |
| emey, emey (3), emey (6), emey (9) | 각인 |
| emey (1), emey (4), emey (7) | 잔상 |
| emey (2), emey (5), emey (8) | 망각 |

종류가 지정되지 않은 박스에 기존 3종을 이름 순서로 순환 연결했다. Inspector의 TutorialMonster → Definition에서 변경할 수 있다. HP·메모리·행동표는 기존 `Prototype/Data/Monsters` 정의 에셋을 공유한다.

필드에서는 기존 FSM의 순찰·시야 감지·추적·소리 감지를 사용한다. 각 몬스터의 배회 범위는 시작점 기준 X/Z ±3m, 시야 8m, 청각 6m다. 같은 층의 연결된 NavMesh 안에서 이동하며, 별도 층간 NavMeshLink는 추가하지 않았다.

작은 실내 구역을 고려해 전투 간격은 4m, 참여 수는 1기로 설정했다. 벽·충돌·카메라 검사는 기존 전투 배치 코드를 유지하므로 공간이 부족한 곳에서는 진입을 거부하고 넓은 곳에서 다시 시도한다. Tutorial Battle의 Encounter Size로 1~3기를 조절할 수 있지만 다수 배치는 이번 검증 범위에 포함하지 않았다.

## 공용 코드 변경

- `TutorialBattleController`: Animator 없는 정적 푸시아 모델도 전투 진입·복귀·제어기 비활성화 시 복구할 수 있다. 기존 Animator가 있는 플레이어의 정지·복귀 처리는 유지한다.
- `FloorTravelButtons`: 플레이어 이동이 비활성화된 동안 버튼 및 `TravelTo` 호출을 막아 전투 배치가 바뀌지 않게 한다.
- 기존 공개 함수명과 Inspector 직렬화 필드명은 바꾸지 않았다.

## 재생성

Unity 메뉴 `CK/Tutorial/Build Grayboxing Demo` 또는 `CK.SemesterProject.Editor.TutorialGrayboxingIntegration.Build`를 실행한다. 원본 MapGrayboxingFuchsia와 TutorialDemo를 사용해 데모 씬과 전용 NavMesh를 다시 만든다. 데모의 수동 편집은 덮어쓰므로 재생성이 필요한 경우에만 실행한다.

## 검증 (2026-10-01)

검증은 현재 연결된 Semesterproject Unity 6000.3.23f1 에디터에서 새 브랜치와 동일한 변경 스크립트 및 데모 자산으로 수행한 뒤, 결과 씬·NavMesh·메타를 별도 작업 폴더로 복사했다. 별도 작업 폴더 자체를 Unity에서 다시 임포트하는 검증은 수행하지 않았다.

- 컴파일 오류 없음. 기존 EditMode 테스트 169개 통과.
- 저장 후 다시 연 씬에서 누락 스크립트 없음, 전투 제어기의 다른 씬 참조 없음.
- 전용 NavMesh 822개 정점, 몬스터 10기 시작 위치 연결 확인.
- Play 모드에서 10기 모두 NavMesh 위에서 순찰 이동 확인.
- 10기 각각 단독 전투 진입·취소·이동/AI/NavMesh 복귀 확인.
- 잔상 첫 턴 방어: 메모리 260→234, 방어 상태 적용, 플레이어 턴으로 전환 확인.
- 전투 중 층 이동 호출이 플레이어 위치를 변경하지 않는지 확인.
- 시야 감지 및 추적 상태 전환 확인. 검증 중 런타임 Console 오류 없음.
- 실제 키보드/마우스를 통한 전체 튜토리얼 완주, 다수 몬스터 전투 및 빌드 실행은 미검증.
