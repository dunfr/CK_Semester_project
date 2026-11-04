아웃로 일러스트 v3 — Unity 6 UI 재생

대상: Unity 6000 이상, Canvas 기반 Unity UI(UGUI).
필요 패키지: Unity UI(com.unity.ugui). 유료 플러그인과 Live2D 설치는 필요하지 않습니다.

넣는 방법
1. 제공된 .unitypackage를 Import하거나 UnityPackage/Assets/OutlawIllustration 폴더를 프로젝트 Assets 안으로 복사합니다.
2. 기존 Canvas의 부모 Panel을 선택한 뒤 Tools > Outlaw Illustration > Create UI Illustration를 실행합니다.
   Canvas가 없으면 화면용 Canvas가 생성됩니다. Data/rig.json과 PNG는 자동 연결됩니다.
3. Play를 누릅니다. 새 Outlaw Animated Illustration 오브젝트의 OutlawRigPlayer에서 조정합니다.
4. 생성된 오브젝트를 Project 창으로 끌어 Prefab으로 저장하면 다른 화면에서도 재사용할 수 있습니다.

v2에서 업데이트하는 경우
기존 파일은 같은 Assets 경로와 GUID를 유지합니다. 패키지를 재Import하면 해당 그림·코드·JSON이 갱신됩니다.
레이어가 추가되거나 구성이 바뀐 경우, 기존 v2 오브젝트의 저장된 텍스처 목록까지 자동 갱신되지는 않습니다.
v2 일러스트 자체가 아닌 부모 Panel을 선택하고 Create UI Illustration로 v3 오브젝트를 새로 생성합니다.
기존 v2 오브젝트는 비활성화해 그림이 겹치지 않게 한 뒤, v3 오브젝트를 Prefab으로 저장합니다.
세부 설명은 같은 폴더의 V3_UPDATE_GUIDE.txt를 참고합니다.

조절
- Intensity: 움직임 크기. 기본 1.0, 우선 0.5~1.5 범위에서 조절합니다.
- Speed: 재생 속도. 기본 1.0일 때 8초 루프이며, 우선 0.5~1.5 범위에서 조절합니다.
- Show Background: 복원 배경 켜기/끄기. 끄면 투명한 UI 일러스트로 배치됩니다.
- Enable Blink: 눈 깜빡임 켜기/끄기.
- Use Unscaled Time: 기본 켜짐. 게임 timeScale이 0이어도 UI 일러스트가 재생됩니다.
- Inspector의 Play/Pause와 Preview Time으로 정지·시간 이동을 할 수 있습니다.

화면 배치
AspectRatioFitter가 부모 안에서 원본 3840:2176 비율을 유지합니다.
레이어는 모두 raycastTarget=false이므로 아래쪽 버튼 입력을 가로채지 않습니다.
Canvas의 Mask/RectMask2D 안에 배치할 수 있습니다. 기본 재질은 UI/Default입니다.
생성된 root의 하위 Canvas가 애니메이션 리빌드를 주변 UI와 분리합니다. 정지·Speed 0·편집 정지 중에는 값이 변한 경우에만 메시를 갱신합니다.

코드에서 조절하는 예
player.intensity = 1.2f;
player.speed = 0.8f;
player.showBackground = false;
player.Pause();
player.SetTime(2.1f);
player.Play();

구성
Data/rig.json: 원본 좌표, 레이어 순서, 변형 계수, 깜빡임 시각.
Textures/*.png: 투명 여백을 잘라낸 텍스처. 원본 위치·크기는 JSON의 rect가 보존합니다.
Runtime/: UI 메시 재생 코드. JSON과 텍스처 참조를 Prefab에 저장하므로 Resources 폴더나 네트워크가 필요하지 않습니다.
Editor/: 생성 메뉴와 이 패키지 Textures 폴더에만 적용되는 PNG 가져오기 설정.
칼은 하나의 강체 변환으로 곧게 유지하고, 쥔 손에서 어깨까지 가중치를 달리하여 팔을 연결합니다.

확인 범위
v3의 실제 파츠 수와 텍스처 경로는 Data/rig.json을 기준으로 합니다.
배포 아카이브는 파일 경로·GUID·그림/코드 해시를 저장 후 재조회합니다. 미리보기와 패키지의 입력 PNG도 비교합니다.
C# 수식·구문 검증과 Unity Editor의 실제 Import/Play는 별개입니다.
이 배포본에는 실제 Unity 프로젝트 Import/Play·기기 FPS·빌드 검증 결과가 포함되지 않습니다.
구체적 검증 범위는 VERIFICATION_SCOPE.txt와 함께 제공되는 최종 작업 기록을 참고합니다.

공식 API 근거
https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.MaskableGraphic.html
https://docs.unity3d.com/2022.3/Documentation/ScriptReference/JsonUtility.FromJson.html
https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TextureImporter-alphaIsTransparency.html
