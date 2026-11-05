using System;
using System.Collections.Generic;
using CK.SemesterProject.SkillSequence;
using Semester.Enemies;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    // 전투 중 스킬을 쓰면 이펙터가 만든 완성 컷신(Timeline 프리팹)을 재생한다. 컷신 테스트 씬은 불러오지 않고, 컷신 프리팹을 생성해
    // 시전자·대상·메인 카메라의 Cinemachine Brain에 연결한다(SkillCutscenePlayer).
    // 컷신이 건드리는 전투 상태(카메라·플레이어 자리·캐릭터 모델 위치·적 자리·후처리 볼륨·아레나 배경)는 시작 전에 저장해 두고
    // 끝나면 되돌리며, 되돌아오는 순간은 화면 페이드로 가린다. TutorialBattleController에는 이 컴포넌트를 부르는 훅만 둔다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TutorialBattleController))]
    public sealed class TutorialSkillCutscene : MonoBehaviour
    {
        [Serializable]
        private struct Binding
        {
            [Tooltip("Skill_DT의 스킬 ID")]
            public string SkillId;

            [Tooltip("재생할 컷신(Timeline 프리팹). 같은 스킬의 기존 모션·Skill Cinematics보다 우선합니다")]
            public SkillCutsceneAsset Cutscene;

            [Min(0f), Tooltip("컷신 동안 플레이어(와 컷신 루트·카메라·이펙트)를 대상 반대 방향으로 물리는 거리(m). 컷신 원본은 대상 앞까지 파고들어 가므로 몰입감이 깨지지 않게 간격을 벌립니다. 뒤가 막혀 있으면 막히기 전까지만 물립니다")]
            public float BackOffset;

            [Tooltip("컷신이 끝났을 때 캐릭터 모델(Animator)이 서 있는 자리를 플레이어 루트 기준 로컬 좌표로 적습니다(타임라인이 시작점에서 이만큼 옮겨 놓고 끝남. Play에서 컷신 끝 무렵 모델의 localPosition을 읽으면 됩니다). 0이 아니면 컷신이 끝나는 자리가 전투 대기 자리가 되도록 컷신 루트와 적을 함께 평행 이동합니다. 0이면 적용하지 않습니다")]
            public Vector3 LandingLocalOffset;
        }

        // 컷신의 Caster 트랙은 시전자 Animator의 트랜스폼(루트)을 직접 움직이고 끝나도 되돌리지 않는다.
        private struct CasterPose
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        // 컷신 끝 착지점을 전투 대기 자리에 맞추려고 컷신 루트와 함께 옮겨 둔 적들.
        private struct EncounterPose
        {
            public EnemyStateMachine Enemy;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        [SerializeField, Tooltip("스킬별 컷신")]
        private Binding[] _bindings = Array.Empty<Binding>();
        [Header("Transition")]
        [SerializeField, Min(0f), Tooltip("컷신이 끝나기 이 시간(초) 전부터 화면을 가리기 시작해, 컷신이 끝나는 순간 완전히 가려집니다. 가려진 사이에 카메라·플레이어·모델·적·볼륨을 전투 상태로 즉시 되돌립니다")]
        private float _fadeOutSeconds = 0.35f;
        [SerializeField, Min(0f), Tooltip("전투 상태로 복귀한 뒤 화면을 다시 보여 주는 시간(초)")]
        private float _fadeInSeconds = 0.5f;
        [SerializeField, Tooltip("화면 전환 색. 검정이면 페이드, 흰색이면 플래시처럼 보입니다")]
        private Color _fadeColor = Color.black;
        [Header("Background")]
        [SerializeField, Tooltip("컷신 동안 숨길 배경 루트(전투 아레나의 벽·소품). 컷신 테스트 씬처럼 빈 공간에 포그와 검은 배경만 보이게 하며 끝나면 복구합니다. 벽이 있으면 포그 돔이 깊이 테스트로 가려집니다")]
        private Transform _hiddenRoot;
        [SerializeField, Tooltip("컷신 동안에도 계속 보이게 둘 렌더러(바닥 등)")]
        private Renderer[] _keptRenderers = Array.Empty<Renderer>();

        private readonly List<CasterPose> _casterPoses = new List<CasterPose>();
        private readonly List<EncounterPose> _enemyPoses = new List<EncounterPose>();
        private readonly List<KeyValuePair<Volume, float>> _suppressedVolumes = new List<KeyValuePair<Volume, float>>();
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private TutorialBattleController _controller;
        private SkillCutscenePlayer _cutscenePlayer;
        private Pose _returnCameraPose;
        private float _returnFieldOfView;
        private bool _isPlayerDisplaced;
        private Pose _returnPlayerPose;
        private Vector3 _displacedPlayerPosition;
        private Vector3 _enemyShift;
        private Image _fadeImage;
        private float _duration;
        // 페이드 인 경과 시간. 음수면 페이드 인 중이 아니다.
        private float _fadeInElapsed = -1f;

        // 컷신이 카메라·플레이어를 쥐고 있는 동안 참.
        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<TutorialBattleController>();
        }

        // 스킬에 지정된 컷신이 있으면 생성해 재생하고 참을 돌려준다. seconds는 컷신 길이이며, 호출한 쪽이 결과 연출을 그만큼 기다린다.
        // - 시전자(플레이어)와 대상(몬스터)은 SkillCutscenePlayer가 Caster/Target 트랙에 연결한다.
        // - Cam 트랙은 메인 카메라의 Cinemachine Brain에 연결되므로, 전투 중 꺼 둔 Brain을 컷신 동안만 켠다.
        public bool TryPlay(string skillId, Transform target, out float seconds)
        {
            seconds = 0f;
            SkillCutsceneAsset cutscene = null;
            float backOffset = 0f;
            Vector3 landingOffset = Vector3.zero;
            foreach (Binding binding in _bindings)
            {
                if (binding.Cutscene != null && binding.SkillId == skillId)
                {
                    cutscene = binding.Cutscene;
                    backOffset = binding.BackOffset;
                    landingOffset = binding.LandingLocalOffset;
                    break;
                }
            }
            if (cutscene == null)
            {
                return false;
            }
            Abort();
            Camera camera = _controller.BattleCamera;
            _returnCameraPose = new Pose(camera.transform.position, camera.transform.rotation);
            _returnFieldOfView = camera.fieldOfView;
            CaptureCasterPose();
            Transform player = _controller.PlayerRoot;
            var home = new Pose(player.position, player.rotation);
            // 컷신 루트는 시전자 위치·방향 기준으로 생성되므로, 먼저 플레이어를 옮겨 두면 카메라·이펙트도 함께 간다.
            PullPlayerBack(target, backOffset);
            AlignLandingToHome(home, landingOffset);
            _controller.BattleBrain.enabled = true;
            _cutscenePlayer = SkillCutscenePlayer.Play(cutscene, player, target);
            if (_cutscenePlayer == null)
            {
                _controller.BattleBrain.enabled = false;
                _casterPoses.Clear();
                RestorePlayerPlacement();
                RestoreEncounterPlacement();
                return false;
            }
            SuppressSceneVolumes(_cutscenePlayer.transform);
            HideBackdrop();
            // 첫 프레임부터 컷신 카메라가 잡히도록 바로 한 번 평가한다(전투 시점이 한 프레임 비치지 않게).
            _cutscenePlayer.Director.Evaluate();
            IsPlaying = true;
            _duration = cutscene.Duration;
            _cutscenePlayer.Finished += OnFinished;
            _controller.SetBattleUiVisible(false);
            seconds = Mathf.Max(cutscene.Duration, 0.01f);
            if (!_cutscenePlayer.IsPlaying)
            {
                OnFinished();
            }
            return true;
        }

        // 전투가 끝나거나 새 컷신을 시작할 때 재생 중인 컷신을 끊고 저장해 둔 전투 상태를 되돌린다. 카메라 복구는 호출한 쪽이 맡는다.
        public void Abort()
        {
            IsPlaying = false;
            _fadeInElapsed = -1f;
            SetFadeAlpha(0f);
            if (_cutscenePlayer != null)
            {
                SkillCutscenePlayer player = _cutscenePlayer;
                _cutscenePlayer = null;
                player.Finished -= OnFinished;
                player.Stop();
            }
            RestoreCasterPose();
            RestorePlayerPlacement();
            RestoreEncounterPlacement();
            RestoreSceneVolumes();
            RestoreBackdrop();
        }

        private void LateUpdate()
        {
            if (IsPlaying && _cutscenePlayer != null)
            {
                HoldPlacements();
            }
            UpdateFade();
        }

        private void OnFinished()
        {
            if (_cutscenePlayer != null)
            {
                _cutscenePlayer.Finished -= OnFinished;
            }
            _cutscenePlayer = null;
            if (!IsPlaying)
            {
                return;
            }
            IsPlaying = false;
            // 컷신 끝에서 화면은 이미 가려져 있다. 그 사이 전부 전투 상태로 즉시 되돌리고, 페이드 인으로 보여 준다.
            RestoreBackdrop();
            RestoreCasterPose();
            RestorePlayerPlacement();
            RestoreEncounterPlacement();
            RestoreSceneVolumes();
            // 전투 중에는 Brain을 꺼 두고 전투 카메라가 직접 움직인다. 컷신 직전의 전투 시점으로 되돌린다.
            _controller.BattleBrain.enabled = false;
            Camera camera = _controller.BattleCamera;
            camera.transform.SetPositionAndRotation(_returnCameraPose.position, _returnCameraPose.rotation);
            camera.fieldOfView = _returnFieldOfView;
            if (_controller.IsInBattle)
            {
                _controller.SetBattleUiVisible(true);
                SetFadeAlpha(1f);
                _fadeInElapsed = 0f;
            }
        }

        // 컷신 끝 직전에는 서서히 가리고(끝나는 순간 완전히 가림), 복귀 후에는 서서히 걷는다.
        // 튜토리얼 팝업 등으로 시간이 멈춰도 끝나도록 실제 시간으로 진행한다.
        private void UpdateFade()
        {
            if (IsPlaying && _cutscenePlayer != null && _fadeOutSeconds > 0.01f)
            {
                float remaining = _duration - (float)_cutscenePlayer.Director.time;
                float k = Mathf.Clamp01(1f - remaining / _fadeOutSeconds);
                SetFadeAlpha(k * k * (3f - 2f * k));
            }
            else if (_fadeInElapsed >= 0f)
            {
                _fadeInElapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_fadeInElapsed / Mathf.Max(0.01f, _fadeInSeconds));
                SetFadeAlpha(1f - k * k * (3f - 2f * k));
                if (k >= 1f)
                {
                    _fadeInElapsed = -1f;
                }
            }
        }

        private void SetFadeAlpha(float alpha)
        {
            if (alpha <= 0.001f)
            {
                if (_fadeImage != null)
                {
                    _fadeImage.gameObject.SetActive(false);
                }
                return;
            }
            if (_fadeImage == null)
            {
                var overlay = new GameObject("CutsceneTransitionFade", typeof(RectTransform), typeof(Canvas), typeof(Image));
                overlay.transform.SetParent(transform, false);
                Canvas canvas = overlay.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = short.MaxValue;
                _fadeImage = overlay.GetComponent<Image>();
                _fadeImage.raycastTarget = false;
            }
            Color color = _fadeColor;
            color.a = alpha;
            _fadeImage.color = color;
            _fadeImage.gameObject.SetActive(true);
        }

        private void CaptureCasterPose()
        {
            _casterPoses.Clear();
            foreach (Animator animator in _controller.PlayerRoot.GetComponentsInChildren<Animator>(true))
            {
                Transform node = animator.transform;
                _casterPoses.Add(new CasterPose
                {
                    Transform = node,
                    LocalPosition = node.localPosition,
                    LocalRotation = node.localRotation,
                    LocalScale = node.localScale,
                });
            }
        }

        // 컷신이 시전자를 컷신 안의 자리로 옮겨 놓으므로, 끝나면 전투 자리로 되돌린다.
        private void RestoreCasterPose()
        {
            foreach (CasterPose pose in _casterPoses)
            {
                if (pose.Transform != null)
                {
                    pose.Transform.SetLocalPositionAndRotation(pose.LocalPosition, pose.LocalRotation);
                    pose.Transform.localScale = pose.LocalScale;
                }
            }
            _casterPoses.Clear();
        }

        // 플레이어를 대상 반대 방향(대상이 없으면 등 뒤)으로 물린다. 뒤가 막혀 있으면 막히기 직전까지만 물린다.
        private void PullPlayerBack(Transform target, float distance)
        {
            _isPlayerDisplaced = false;
            if (distance <= 0f)
            {
                return;
            }
            Transform player = _controller.PlayerRoot;
            Vector3 back = -player.forward;
            if (target != null)
            {
                Vector3 away = player.position - target.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                {
                    back = away.normalized;
                }
            }
            back.y = 0f;
            back.Normalize();
            const float Clearance = 0.6f;
            Vector3 origin = player.position + Vector3.up * 0.9f;
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, 0.3f, back, distance + Clearance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(player) || (target != null && hit.transform.IsChildOf(target)))
                {
                    continue;
                }
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - Clearance));
            }
            if (distance <= 0.01f)
            {
                return;
            }
            _returnPlayerPose = new Pose(player.position, player.rotation);
            _displacedPlayerPosition = player.position + back * distance;
            _isPlayerDisplaced = true;
            _controller.PlacePlayer(_displacedPlayerPosition, player.rotation);
        }

        // 컷신이 끝나는 자리(착지점)가 전투 대기 자리가 되게 한다. 타임라인은 캐릭터 모델을 컷신 루트에서 landingLocal만큼 옮겨 놓고
        // 끝나므로, 컷신 루트(=플레이어 루트)를 대기 자리에서 그만큼 반대로 둔다. 그러면 루트가 대기 자리보다 앞서 적에게 붙으므로,
        // 적도 같은 양만큼 옮겨 컷신 루트와 적의 간격(BackOffset으로 벌린 간격)은 그대로 둔다. 타임라인과 프리팹은 건드리지 않는다.
        private void AlignLandingToHome(Pose home, Vector3 landingLocal)
        {
            _enemyPoses.Clear();
            _enemyShift = Vector3.zero;
            if (landingLocal == Vector3.zero)
            {
                return;
            }
            Vector3 modelHomeLocal = _casterPoses.Count > 0 ? _casterPoses[0].LocalPosition : Vector3.zero;
            Vector3 start = _isPlayerDisplaced ? _displacedPlayerPosition : home.position;
            Vector3 root = home.position + home.rotation * (modelHomeLocal - landingLocal);
            _enemyShift = root - start;
            _returnPlayerPose = home;
            _displacedPlayerPosition = root;
            _isPlayerDisplaced = true;
            _controller.PlacePlayer(root, home.rotation);
            foreach (EnemyStateMachine member in _controller.EncounterMembers)
            {
                if (member != null)
                {
                    _enemyPoses.Add(new EncounterPose
                    {
                        Enemy = member,
                        Position = member.transform.position,
                        Rotation = member.transform.rotation,
                    });
                }
            }
            ApplyEnemyShift();
        }

        // 기존 전투 연출(TutorialBattleActionPresentation)은 진행도가 1이 되면 플레이어를, 매 프레임 대상을 자기 기준 자리로 되돌린다.
        // 컷신이 끝날 때까지는 옮겨 둔 자리를 유지해, 컷신 마지막 프레임에 튀지 않게 한다.
        private void HoldPlacements()
        {
            if (_isPlayerDisplaced)
            {
                Transform player = _controller.PlayerRoot;
                if ((player.position - _displacedPlayerPosition).sqrMagnitude > 0.000001f)
                {
                    _controller.PlacePlayer(_displacedPlayerPosition, player.rotation);
                }
            }
            ApplyEnemyShift();
        }

        private void ApplyEnemyShift()
        {
            foreach (EncounterPose pose in _enemyPoses)
            {
                if (pose.Enemy != null)
                {
                    pose.Enemy.transform.position = pose.Position + _enemyShift;
                }
            }
        }

        private void RestorePlayerPlacement()
        {
            if (!_isPlayerDisplaced)
            {
                return;
            }
            _isPlayerDisplaced = false;
            if (_controller != null)
            {
                _controller.PlacePlayer(_returnPlayerPose.position, _returnPlayerPose.rotation);
            }
        }

        private void RestoreEncounterPlacement()
        {
            foreach (EncounterPose pose in _enemyPoses)
            {
                if (pose.Enemy != null)
                {
                    TutorialBattleController.WarpEnemy(pose.Enemy, pose.Position, pose.Rotation);
                }
            }
            _enemyPoses.Clear();
            _enemyShift = Vector3.zero;
        }

        // 컷신 테스트 씬에는 상시 볼륨(톤매핑·비네트·블룸)이 없다. 같은 색으로 보이도록 컷신 동안 씬의 다른 Volume을 끈다.
        private void SuppressSceneVolumes(Transform cutsceneRoot)
        {
            RestoreSceneVolumes();
            foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!volume.enabled || volume.weight <= 0f || volume.transform.IsChildOf(cutsceneRoot))
                {
                    continue;
                }
                _suppressedVolumes.Add(new KeyValuePair<Volume, float>(volume, volume.weight));
                volume.weight = 0f;
            }
        }

        private void RestoreSceneVolumes()
        {
            foreach (KeyValuePair<Volume, float> saved in _suppressedVolumes)
            {
                if (saved.Key != null)
                {
                    saved.Key.weight = saved.Value;
                }
            }
            _suppressedVolumes.Clear();
        }

        // 컷신 테스트 씬은 빈 공간이다. 아레나의 벽·소품이 있으면 그 뒤의 포그 돔이 깊이 테스트로 잘려 포그가 가려지므로 컷신 동안 숨긴다.
        private void HideBackdrop()
        {
            RestoreBackdrop();
            if (_hiddenRoot == null)
            {
                return;
            }
            foreach (Renderer renderer in _hiddenRoot.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    || Array.IndexOf(_keptRenderers, renderer) >= 0)
                {
                    continue;
                }
                renderer.enabled = false;
                _hiddenRenderers.Add(renderer);
            }
        }

        private void RestoreBackdrop()
        {
            foreach (Renderer renderer in _hiddenRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            _hiddenRenderers.Clear();
        }
    }
}
