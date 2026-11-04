using UnityEngine;

namespace CK.CutsceneTools
{
    /// <summary>
    /// 필살기 컷신(Ultimate_ZZZ)의 색·크기·타이밍 값을 한곳에 모은 에디터 전용 설정.
    /// 값을 바꾸고 인스펙터의 "적용" 버튼(또는 Auto Apply)을 누르면 워크스페이스 씬의 컷신 인스턴스에 반영되고 프리팹에 Apply 된다.
    /// 런타임 코드는 이 에셋을 참조하지 않는다(에디터 전용).
    /// </summary>
    [CreateAssetMenu(menuName = "CK/Ultimate Cutscene Style", fileName = "UltimateCutsceneStyle")]
    public sealed class UltimateCutsceneStyle : ScriptableObject
    {
        [Header("적용 방식")]
        [Tooltip("켜면 값을 바꿀 때마다 자동으로 적용한다(텍스처 재생성이 있어 약간 느릴 수 있다).")]
        public bool autoApply;

        [Header("0. 룩 (원본 FoxPreSlash 프로젝트 기준: 어두운 배경 + 블룸)")]
        public float bloomThreshold = 1.05f;
        public float bloomIntensity = 0.9f;
        public float bloomScatter = 0.66f;
        public Color bloomTint = new Color(1f, 0.87f, 0.98f);
        [Tooltip("포그 구간 전체 노출(EV). 낮출수록 어둡다.")]
        public float lookExposure = -0.35f;
        [Range(-100f, 100f)] public float lookSaturation = 0f;
        public Color lookColorFilter = Color.white;
        [Tooltip("룩 볼륨이 켜지는 구간: 시작 / 최대 / 감소 시작 / 끝 (초)")]
        public Vector4 lookVolumeTimes = new Vector4(1.05f, 1.75f, 5.12f, 5.6f);

        [Header("1. 크기 (원본 이펙트 = 1.0)")]
        [Tooltip("쏟아지는 난격 A/B 교차 참격 배율")]
        public float flurryScale = 0.42f;
        [Tooltip("쏟아지는 난격 메인(Combo) 배율")]
        public float comboScale = 0.48f;
        [Tooltip("휘두를 때마다 터지는 참격 1개 배율")]
        public float strikeScale = 1.0f;
        [Tooltip("1차 공격 세로 슬래시 배율")]
        public float slash1Scale = 4.6f;
        [Tooltip("1차 공격 슬래시를 화면 중앙을 가로로 가르는 위치·각도(컷신 루트 기준)로 고정한다. 끄면 프리팹의 원래 위치·각도를 쓴다.")]
        public bool slash1UsePose = true;
        [Tooltip("1차 슬래시의 긴 리본(Raised Blade 등)이 월드 위쪽으로 떠오르는 원본 움직임을 끈다. 가로로 눕혔기 때문에 그대로 두면 베기가 위로 올라가 보인다.")]
        public bool slash1NoRise = true;
        public Vector3 slash1Position = new Vector3(-0.10f, 0.64f, 3.40f);
        public Vector3 slash1Euler = new Vector3(1.1f, 335.2f, 250f);

        [Header("2. 잔상")]
        public Color ghostColorA = new Color(0.85f, 0.32f, 0.78f);
        public Color ghostColorB = new Color(0.5f, 0.32f, 0.95f);
        [Range(0f, 1f)] public float ghostAlphaA = 0.5f;
        [Range(0f, 1f)] public float ghostAlphaB = 0.32f;
        [Tooltip("원본 모션보다 늦는 시간(초). 클수록 잔상이 멀리 남는다.")]
        public float ghostDelayA = 0.07f;
        public float ghostDelayB = 0.14f;
        public float ghostFadeIn = 0.10f;
        public float ghostFadeOut = 0.18f;
        [Tooltip("잔상이 보이는 구간 1(돌진): 시작 / 끝")]
        public Vector2 ghostWindowDash = new Vector2(0.20f, 1.25f);
        [Tooltip("잔상이 보이는 구간(난격) 시작/끝 프레임. 아래 프레임 속도로 초로 바꿔 쓴다. 구간마다 잔상이 나타났다 사라지고, 잔상 입자(디졸브)도 같은 구간에서만 나온다.")]
        public Vector2[] ghostWindowFrames = new Vector2[0];
        public float ghostFrameRate = 60f;
        [Tooltip("위 프레임 구간이 비어 있을 때의 난격 잔상 구간: 첫 휘두름(B1) 시작 후 몇 초에 나타나 / 착지(B6 끝) 몇 초 전에 사라지나")]
        public float ghostSwingStart = 0.15f;
        public float ghostSwingEnd = 0.37f;
        [Tooltip("잔상이 흩어지는 입자 색 (A/B)")]
        public Color dissolveColorA = new Color(1f, 0.6f, 0.95f);
        public Color dissolveColorB = new Color(0.75f, 0.55f, 1f);

        [Header("3. 포그 색")]
        public Color domeBottom = new Color(0.02f, 0.01f, 0.05f);
        public Color domeLow = new Color(0.07f, 0.02f, 0.14f);
        public Color domeHorizon = new Color(0.28f, 0.10f, 0.42f);
        public Color domeTop = new Color(0.03f, 0.01f, 0.09f);
        [Tooltip("돔에 흐르는 가로 빛줄기 색")]
        public Color domeStreak = new Color(1f, 0.75f, 0.97f);
        [Range(0f, 1f)] public float domeStreakStrength = 0.55f;
        public Color floorCenter = new Color(0.14f, 0.06f, 0.24f);
        public Color floorEdge = new Color(0.04f, 0.015f, 0.10f);
        [Tooltip("소용돌이 호 색 (어두운 쪽 / 밝은 쪽)")]
        public Color swirlLow = new Color(0.55f, 0.20f, 0.85f);
        public Color swirlHigh = new Color(1f, 0.80f, 0.97f);
        [Tooltip("안개 덩어리 색 3종 (랜덤)")]
        public Color puffColor1 = Color.white;
        public Color puffColor2 = new Color(1f, 0.55f, 0.88f);
        public Color puffColor3 = new Color(0.55f, 0.3f, 0.95f);
        [Range(0f, 1f)] public float puffAlpha = 0.3f;
        [Tooltip("흐르는 빛줄기 색 3종")]
        public Color streakColor1 = Color.white;
        public Color streakColor2 = new Color(1f, 0.55f, 0.88f);
        public Color streakColor3 = new Color(0.55f, 0.3f, 0.95f);
        [Tooltip("떠다니는 파편 색 3종")]
        public Color shardColor1 = new Color(0.55f, 0.3f, 0.95f);
        public Color shardColor2 = new Color(1f, 0.55f, 0.88f);
        public Color shardColor3 = new Color(0.3f, 0.12f, 0.5f);

        [Header("4. 포그 타이밍")]
        [Tooltip("포그가 나타나기 시작 / 완전히 보이는 시각(초)")]
        public Vector2 fogIn = new Vector2(1.10f, 1.75f);
        [Tooltip("돔 → 바닥 → 소용돌이 순서로 겹쳐 올라오는 간격(초). 0 이면 한꺼번에 나타난다.")]
        public float fogLayerStagger = 0.1f;
        [Tooltip("1차 공격 뒤 화면을 덮던 분홍 와이프 패널을 쓸지 (기본 끔)")]
        public bool enableWipe = false;
        [Tooltip("화면 중앙을 기점으로 어두운 배경이 퍼져 나가게 한다(카메라 앞쪽 먼 곳의 원형 그라데이션이 커진다. 캐릭터·바닥 앞에서는 가려지지 않는다).")]
        public bool enableDarkSpread = true;
        [Tooltip("퍼지기 시작 / 화면을 다 덮는 시각(초)")]
        public Vector2 darkSpreadTime = new Vector2(1.10f, 1.75f);
        public Color darkSpreadColor = new Color(0.012f, 0.004f, 0.04f);
        [Range(0f, 1f)] public float darkSpreadAlpha = 0.93f;
        [Tooltip("카메라에서 얼마나 먼 곳에 두는지(m)")]
        public float darkSpreadDistance = 40f;
        [Tooltip("포그가 사라지기 시작 / 완전히 사라지는 시각(초)")]
        public Vector2 fogOut = new Vector2(5.12f, 5.6f);
        public float domeSpinDegPerSec = 20f;
        public float vortexASpinDegPerSec = -95f;
        public float vortexBSpinDegPerSec = 150f;

        [Header("5. 타격 FX 색")]
        public Color hitRingA = new Color(1f, 0.55f, 0.9f);
        public Color hitRingB = new Color(1f, 0.97f, 1f);
        public Color hitFlareA = new Color(1f, 0.97f, 1f);
        public Color hitFlareB = new Color(1f, 0.55f, 0.9f);
        public Color hitSparkA = new Color(1f, 0.97f, 1f);
        public Color hitSparkB = new Color(1f, 0.55f, 0.9f);
        public Color landRingA = new Color(1f, 0.55f, 0.9f);
        public Color landRingB = new Color(1f, 0.97f, 1f);
        public Color landDustA = new Color(1f, 0.97f, 1f);
        public Color landDustB = new Color(1f, 0.55f, 0.9f);

        [Header("6. 쏟아지는 난격 개수·타이밍 (착지 시각 기준)")]
        [Tooltip("쏟아지는 난격을 겹치는 횟수. 1 = 원본 PF_FoxPreSlash 한 벌(파티클 시스템 67개, 입자 238개). 2·3 이면 A/B 겹이 추가된다.")]
        [Range(1, 3)] public int pourLayers = 1;
        [Tooltip("켜면 원본처럼 67개 전부 사용. 끄면 초반(delay≤0.12초) 13개는 제외한다.")]
        public bool comboIncludesEarlyParticles = true;
        [Tooltip("착지(애니메이션 끝) 몇 초 전부터 Combo 참격이 시작되나")]
        public float comboLead = 0.57f;
        [Tooltip("Combo 시작 대비 난격 A / B 시작 오프셋(초)")]
        public float flurryAOffset = 0.02f;
        public float flurryBOffset = 0.22f;
        [Tooltip("쏟아지는 난격 중심 위치 (컷신 루트 기준). 참격 면은 쏟아짐 카메라를 바라본다.")]
        public Vector3 comboCenter = new Vector3(0.2f, 1.7f, 5.0f);
        public Vector3 flurryACenter = new Vector3(0.4f, 1.6f, 4.8f);
        public Vector3 flurryBCenter = new Vector3(-0.2f, 1.6f, 5.2f);

        [Header("6-2. 쏟아짐 카메라 (시전자 + 난격이 함께 보이도록)")]
        [Tooltip("착지 몇 초 전에 쏟아짐 카메라로 이동을 시작하나")]
        public float pourCameraLead = 0.47f;
        [Tooltip("쏟아짐 카메라로 이동하는 데 걸리는 시간(초)")]
        public float pourCameraMoveTime = 0.5f;
        public Vector3 pourCameraPos = new Vector3(3.2f, 2.0f, -1.2f);
        [Tooltip("쏟아짐 카메라가 바라보는 지점")]
        public Vector3 pourCameraLook = new Vector3(0.1f, 1.3f, 2.8f);
        [Range(20f, 90f)] public float pourCameraFov = 52f;
        [Tooltip("이동 후 마지막 샷 전까지 천천히 움직이는 양")]
        public Vector3 pourCameraDrift = new Vector3(-0.3f, 0.05f, 0.2f);
        [Tooltip("마지막 와이드 샷으로 넘어가는 시각(초)")]
        public float finalShotTime = 4.85f;

        [Header("6-3. 복귀 카메라 (TutorialBattleController 전투 카메라 자세)")]
        [Tooltip("전투 카메라: 플레이어 기준 오른쪽·높이·뒤 (씬 값 2.2, 1.3, -2.3 × 간격 보정). 컷신 시작·끝 자세가 이 값이다.")]
        public Vector3 battleCameraPos = new Vector3(2.2f, 1.3f, -2.3f);
        [Tooltip("전투 카메라가 바라보는 지점 (플레이어·적 간격 4m 기준: 적 방향 2.2m, 높이 0.9m)")]
        public Vector3 battleCameraLook = new Vector3(0f, 0.9f, 2.2f);
        [Range(20f, 90f)] public float battleCameraFov = 56f;
        [Tooltip("화면 깨짐 후 복귀 카메라가 움직이기 시작하기까지(초)")]
        public float returnStartAfterBreak = 0.25f;
        [Tooltip("전투 카메라로 복귀하는 데 걸리는 시간(초)")]
        public float returnDuration = 1.0f;
        [Tooltip("복귀 후 자세를 유지하는 시간(초) = 컷신 끝")]
        public float endHold = 0.2f;

        [Header("6-4. 난격 마무리: 지글거림 → 완성 → 라이팅 → 폭발 (착지 시각 기준, 초)")]
        public float webFirstLine = -0.57f;
        [Tooltip("직선 빛줄기가 하나씩 그려지는 데 걸리는 시간")]
        public float webBuildTime = 1.0f;
        [Tooltip("지글거림 시작")]
        public float sizzleStart = 0.10f;
        [Tooltip("난격 완성(지글거림 감소 후 그물이 안정되는 시각)")]
        public float completeTime = 0.55f;
        [Tooltip("라이팅(빛 번짐) 시작")]
        public float glowStart = 0.65f;
        [Tooltip("폭발 시각 (라이팅 정점)")]
        public float breakTime = 1.15f;
        public bool enableWeb = true;
        [Tooltip("원본 난격(PF_FoxPreSlash)의 참격 리본 전부를 복제해 쓴다. 원본 배치 기준 배율 (카메라에 꽉 차게 보이는 정도)")]
        public float webScale = 0.6f;
        [Tooltip("끄면(기본) 원본 난격 그대로: 리본마다 원래 수명만 보였다 사라진다. 켜면 모든 리본을 폭발 순간까지 붙잡아 쌓는다(겹쳐 보인다).")]
        public bool webHoldUntilBurst = false;
        [Tooltip("원본 수명에 더하는 시간(초). 원본 난격 모드에서 리본을 조금 더 오래 보이고 싶을 때")]
        public float webExtraLife = 0f;
        [Tooltip("지글거림 진폭(m, 리본 로컬)과 회전(도), 크기 맥동(비율)")]
        public float sizzleAmplitude = 0.25f;
        public float sizzleRotation = 2.2f;
        [Range(0f, 0.3f)] public float sizzleScalePulse = 0.03f;
        [Tooltip("완성 순간 리본 밝기 배수(원본 머티리얼 Opacity 대비 알파 부스트는 없고, 크기만 살짝 키움)")]
        public float completePop = 1f;
        [Range(8, 60)] public int sizzleFps = 24;
        public int webSeed = 7;
        [Tooltip("라이팅 색 · 코어 크기 · 블룸 강도 · 노출(EV) · 라이트 세기")]
        public Color glowColor = new Color(1f, 0.82f, 0.96f);
        public float glowCoreSize = 7f;
        public float glowBloomIntensity = 1.8f;
        public float glowExposure = 0.5f;
        public float glowLightIntensity = 9f;

        [Header("6-5. 참격 폭발 + 벚꽃잎 (켜고 끌 수 있음)")]
        [Tooltip("끄면 폭발(섬광 + 벚꽃잎)을 뮤트한다. Timeline 의 Finale_Burst_MuteToDisable 그룹을 뮤트해도 같다.")]
        public bool enableBurst = true;
        [Tooltip("끄면 벚꽃잎만 뮤트한다.")]
        public bool enableBreakPetals = true;
        [Tooltip("터지는 순간 리본이 가장 강하게 빛난 뒤 사라지는 데 걸리는 시간(초)")]
        public float burstFadeTime = 0.2f;
        [Tooltip("리본이 쌓일수록 밝아지다 터질 때 최대로 빛나는 정도. 원본 머티리얼의 Glow Intensity 에 곱해진다(1 = 2배). 모양·색은 건드리지 않는다.")]
        public float burstGlowBoost = 1.5f;
        [Tooltip("난격을 쌓아 두는 동안 흰 코어 리본(Glow 84)의 투명도(0~1). 낮을수록 선이 가늘고 또렷하게 보이고, 라이팅 시작~폭발 사이에 1 까지 올라간다.")]
        public float webCoreHoldAlpha = 0.12f;
        [Tooltip("터지는 순간 흰 코어 리본의 투명도(0~1). 1 이면 화면이 하얗게 날아가고, 낮추면 분홍 선이 보인 채로 터진다.")]
        public float webCorePeakAlpha = 0.6f;
        [Tooltip("터진 직후 리본 두께가 줄어드는 정도(1 = 그대로, 0.2 = 가는 선으로). 영상처럼 선이 가늘어지며 사라진다.")]
        public float webBurstThin = 0.25f;
        [Tooltip("폭발 섬광(중심에서 퍼지는 빛) 크기(m)")]
        public float breakFlareSize = 14f;
        [Tooltip("원본 난격 꽃잎 세트(4종)를 참격 선 위 몇 곳에서 터뜨릴지. 한 곳에 약 46장")]
        [Range(1, 40)] public int petalGroups = 6;
        [Tooltip("벚꽃잎 하나의 크기 배율(원본 Petal 크기 x 배율)")]
        public float petalScale = 1f;
        [Tooltip("벚꽃잎이 아래로 떨어지는 정도(중력 배율)와 떠 있는 시간 배율")]
        public float petalGravity = 0.6f;
        public float petalLifeMul = 1.25f;
        public float breakShake = 9f;
        [Tooltip("폭발 순간 화면이 하얘지는 정도")]
        [Range(0f, 1f)] public float breakFlash = 0.3f;

        [Header("7. 카메라 쉐이크 · 피격 펄스")]
        [Tooltip("1차 공격 임팩트 시각 / 돌진 시작 시각(초)")]
        public float impactTime = 1.02f;
        [Tooltip("1차 공격 임팩트 플래시를 완전히 하얗게 유지하는 시각(초)과 그 뒤 걷히는 데 걸리는 시간. 캐릭터가 공중으로 떠오르는 동작이 끝나는 시각까지 가린다.")]
        public float flashHoldUntil = 1.24f;
        public float flashHoldDecay = 0.12f;
        public float dashRumbleTime = 0.25f;
        [Tooltip("모든 쉐이크 진폭에 곱해지는 배율")]
        public float shakeMultiplier = 1f;
        public float shakeImpact = 3.2f;
        public float shakeSwing = 2.8f;
        public float shakeLastSwing = 3.4f;
        public float shakeLand = 7.5f;
        public float shakePour = 3.5f;
        [Range(0f, 1f)] public float hitPulseSwing = 0.7f;
        [Range(0f, 1f)] public float hitPulseLastSwing = 0.9f;
        [Range(0f, 1f)] public float hitPulseLand = 1f;

        [Header("8. 화면 플래시")]
        [Range(0f, 1f)] public float flashSwing = 0.15f;
        [Range(0f, 1f)] public float flashLand = 0.55f;
        [Range(0f, 1f)] public float flashLandSecond = 0.4f;
        [Tooltip("포그가 걷힐 때 화이트아웃 시각 / 강도")]
        public float whiteoutTime = 4.85f;
        [Range(0f, 1f)] public float whiteoutPeak = 0f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (autoApply)
            {
                UnityEditor.EditorApplication.delayCall -= DelayedApply;
                UnityEditor.EditorApplication.delayCall += DelayedApply;
            }
        }

        private void DelayedApply()
        {
            if (this != null && autoApply)
            {
                UltimateCutsceneBuilder.ApplyAll(this);
            }
        }
#endif
    }
}
