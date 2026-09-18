# 적 이동 / 감지 상태머신

Unity 6000.3, AI Navigation / NavMeshAgent 기반. `enemy_test` 씬에서 Play.

## 조작과 기획 수치

- Player: WASD 이동, Space로 소리 이벤트 발생. Game 뷰에 포커스를 두세요.
- Enemy의 **Enemy State Machine** 컴포넌트에서 public 필드를 조절합니다. 각 항목에 툴팁이 있습니다.
- 배회: Patrol Half Extents는 중심 기준 X/Z 반폭(m), Minimum Patrol Distance는 현재 위치부터의 최소 거리입니다. 기본 대기는 1~3초입니다.
- 시각: Player, Eyes, Eye Height, Target Height, View Distance, View Angle, Sight Blocking Layers.
- 청각: Hearing Distance, Sound Look Seconds, Sound Reaction Cooldown, Turn Speed.
- 추적: Alert Seconds(기본 1초), Pursuit Speed, Repath Interval, Arrival Distance, Travel Timeout.
- 탐색: Search Cycles(기본 2회), Search Angle, Search Pause Seconds.
- 표시: Indicator Offset/Size, Sound Color, Alert Color. TextMesh를 자동 생성하거나 직접 연결할 수 있습니다.
- Agent의 Radius/Height/Acceleration/Angular Speed도 Inspector에서 변경할 수 있습니다.
- Play 중 바꾼 Inspector 값은 Play 종료 시 복원됩니다. 영구 수정은 Edit 모드에서 하세요.

## 상태 흐름

1. Patrol → 지정 X/Z 영역의 완전한 NavMesh 경로를 가진 임의 목적지로 이동.
2. PatrolWait → 도착 후 1~3초 대기 → Patrol.
3. 시야와 소리는 서로 독립적입니다. 시각은 이동/대기/소리반응/탐색 중에도 매 프레임 검사합니다.
4. 소리만 감지: SoundLook → 정지하고 `?` 표시 → 근원지 방향으로 회전 → 설정 시간 유지 → 배회 복귀(탐색 중이었다면 탐색 재개). 소리 위치로 이동하지 않습니다.
5. 플레이어 목격: Alert → `!`를 1초 표시하며 정지 → PursueLastSeen. 보이는 동안에만 실제 위치를 매 프레임 저장하며 경로 갱신은 별도 주기로 제한합니다.
6. 시야를 벗어나면 마지막 목격 위치까지 이동 → Search에서 왼쪽/오른쪽 2회 → Patrol. 탐색 중 발견하면 다시 Alert.
7. 동시에 목격과 소리가 발생하면 확실한 시각 정보가 우선합니다. 발견/추적 중 소리가 추적을 끊지 않습니다.
8. 무효/부분 경로는 선택하지 않으며, 목적지가 없으면 대기 후 재시도합니다. 이동이 막히면 제한 시간 뒤 복귀/탐색합니다.

## 실제 게임 연결

적에 NavMeshAgent + EnemyStateMachine을 붙이고 Player를 지정하세요. 테스트용 Enemy 프리팹을 사용하는 경우에도 Player를 씬에서 직접 연결해야 합니다. NavMeshSurface에서 바닥과 장애물을 Bake해야 이동합니다. 지형을 수정하면 다시 Bake하세요.

소리는 **게임 이벤트**입니다. AudioSource를 재생하는 것만으로 자동 감지하지 않습니다. 발소리/문/투척물/총성의 실제 발생 시점에 다음을 호출합니다.

```csharp
using Semester.Enemies;
EnemyNoise.Emit(transform.position, 12f, gameObject);
```

또는 소리를 내는 오브젝트에 EnemyNoiseEmitter를 붙이고 `EmitNoise()`를 호출합니다(Animation Event/UnityEvent 지원). Noise Radius와 적의 Hearing Distance 중 작은 값까지 벽·시야각과 무관하게 감지합니다. 실제 오디오가 필요하면 Audio Source도 연결하세요. 테스트 플레이어의 Emit Footsteps는 기본 꺼져 있어 시각/청각을 따로 확인할 수 있습니다.

공격·피해 처리는 이번 이동/감지 범위에 포함되지 않습니다. 플레이어가 계속 보이고 이미 도착했다면 현재 위치에서 대기하며 시각 추적을 유지합니다.

## 검증

Window → General → Test Runner → PlayMode → Semester.EnemyAI.PlayModeTests.
후방 소리/방향 전환, 감지 반경, 벽 차폐와 독립 청각, 시각 우선순위/마지막 목격 기억, 추적→탐색→배회, 유효 배회 목적지와 실제 이동, 도착 후 정지/대기, 비활성화 구독 해제를 검사합니다. Unity 6000.3.23f1에서 PlayMode 테스트 8개 통과.
