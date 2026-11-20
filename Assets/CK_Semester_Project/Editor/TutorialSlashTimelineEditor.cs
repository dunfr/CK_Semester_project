using System.Linq;
using CK.SemesterProject.SkillSequence;
using CK.SemesterProject.Tutorial;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Editor
{
    internal static class TutorialSlashTimelineEditor
    {
        private const string PlayerAnimationTrackName = "Player Animation (Skill Sequence)";
        internal const string PrefabPath =
            "Assets/CK_Semester_Project/Cinematics/Timelines/PF_Battle_SlashFollow.prefab";
        private static PlayableDirector _director;
        internal static PlayableDirector PreviewDirector => _director;
        private static TutorialSlashFramingReceiver _framing;
        private static Camera _camera;
        private static CinemachineCamera _shot;
        private static CinemachineBrain _brain;
        private static Transform _previewPlayer;
        private static SkillSequenceSampler _movementSampler;
        private static LensSettings _lens;
        private static SkillSequenceAsset _sequence;
        private static Vector3 _home;
        private static Vector3 _target;
        private static Vector3 _forward;
        private static Pose _openingPose;
        private static double _cameraEnd;
        private static TutorialSlashHitFeel _hitFeel = TutorialSlashHitFeel.None;
        private static TimelineEditorWindow _timelineWindow;
        private static bool _timelineWasLocked;

        [MenuItem("CK/Slash Follow Timeline")]
        private static void Open()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var sourceDirector = prefab != null ? prefab.GetComponent<PlayableDirector>() : null;
            var timeline = sourceDirector != null ? sourceDirector.playableAsset as TimelineAsset : null;
            var battle = Object.FindFirstObjectByType<TutorialBattleController>();
            if (timeline == null || battle == null || battle.BattleCamera == null)
            {
                EditorUtility.DisplayDialog("Slash Follow Timeline", "전투 씬을 연 뒤 다시 실행하세요.", "확인");
                return;
            }

            Close();
            using var battleSettings = new SerializedObject(battle);
            var arena = battleSettings.FindProperty("_battleArena").objectReferenceValue as Transform;
            if (arena == null)
            {
                EditorUtility.DisplayDialog("Slash Follow Timeline", "전투 아레나가 연결된 씬이 필요합니다.", "확인");
                return;
            }
            var runner = battle.GetComponent<TutorialSkillSequenceRunner>();
            _sequence = runner != null ? runner.FindPlayerSequence("SK01") : null;
            Animator playerAnimator = battle.PlayerRoot != null ? battle.PlayerRoot.GetComponentInChildren<Animator>() : null;
            double cameraEnd = TutorialSkillSequenceRunner.GetSlashCameraEnd(timeline);
            if (_sequence == null || _sequence.CasterClip == null || playerAnimator == null
                || cameraEnd <= 0d || _sequence.ReturnEnd <= _sequence.ApproachStart)
            {
                EditorUtility.DisplayDialog("Slash Follow Timeline", "플레이어 Animator와 기본 베기 애니메이션 연결을 확인하세요.", "확인");
                return;
            }
            AnimationTrack playerTrack = SyncPlayerAnimationTrack(timeline, _sequence);
            _forward = Vector3.ProjectOnPlane(arena.forward, Vector3.up).normalized;
            float spacing = battleSettings.FindProperty("_battleSpacing").floatValue;
            _home = arena.position - _forward * (spacing * 0.5f);
            _target = arena.position + _forward * (spacing * 0.5f);
            Vector2 offset = battleSettings.FindProperty("_battleCameraOffsetXY").vector2Value;
            float distance = battleSettings.FindProperty("_battleCameraDistance").floatValue;
            _openingPose = TutorialBattleCamera.GetBasePose(_home, _target, new Vector3(offset.x, offset.y, -distance));
            _lens = LensSettings.FromCamera(battle.BattleCamera);
            _lens.FieldOfView = battleSettings.FindProperty("_battleFieldOfView").floatValue;

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, battle.gameObject.scene);
            rig.name = "Slash Follow Edit Preview";
            rig.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            _director = rig.GetComponent<PlayableDirector>();
            _director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            _director.extrapolationMode = DirectorWrapMode.None;
            _cameraEnd = cameraEnd;
            // 프리뷰는 타임라인 시간 그대로 재생되므로 히트스톱은 건너뛰고 카메라 흔들림만 미리 본다.
            _hitFeel = new TutorialSlashHitFeel(timeline);
            _framing = rig.GetComponent<TutorialSlashFramingReceiver>();

            var cameraObject = new GameObject("Preview Output") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(rig.transform);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.CopyFrom(battle.BattleCamera);
            if (battle.BattleCamera.TryGetComponent(out UniversalAdditionalCameraData sourceCameraData))
            {
                var previewCameraData = _camera.GetUniversalAdditionalCameraData();
                previewCameraData.renderPostProcessing = sourceCameraData.renderPostProcessing;
                previewCameraData.volumeLayerMask = sourceCameraData.volumeLayerMask;
                previewCameraData.volumeTrigger = sourceCameraData.volumeTrigger;
            }
            _camera.enabled = true;
            _camera.depth = battle.BattleCamera.depth + 10f;
            _brain = cameraObject.AddComponent<CinemachineBrain>();
            _brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;

            _shot = rig.GetComponentInChildren<CinemachineCamera>(true);
            _shot.Lens = _lens;
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is CinemachineTrack)
                {
                    _director.SetGenericBinding(track, _brain);
                }
            }
            Animator previewAnimator = CreatePreviewPlayer(rig.transform, battle.PlayerRoot, playerAnimator);
            _director.SetGenericBinding(playerTrack, previewAnimator);
            Transform enemyMarker = CreateMarker(rig.transform, "Enemy stand-in", _target);
            var context = new SkillSequenceContext(_previewPlayer, new[] { enemyMarker }, null, 0);
            _movementSampler = new SkillSequenceSampler(_sequence, context, playCasterClip: false);
            Vector3 playerPosition = _previewPlayer.position;
            Vector3 enemyPosition = enemyMarker.position;
            rig.transform.SetPositionAndRotation(context.GetTargetCenter(enemyMarker), context.Facing);
            _previewPlayer.position = playerPosition;
            enemyMarker.position = enemyPosition;

            _timelineWindow = TimelineEditor.GetOrCreateWindow();
            _timelineWasLocked = _timelineWindow.locked;
            _timelineWindow.SetTimeline(_director);
            _timelineWindow.locked = true;
            _director.Evaluate();
            UpdatePreview();
            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            TutorialSlashVfxOverridesWindow.Open();
        }

        [MenuItem("CK/Close Slash Follow Preview")]
        private static void Close()
        {
            EditorApplication.update -= UpdatePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
            _movementSampler?.Dispose();
            _movementSampler = null;
            if (_timelineWindow != null)
            {
                _timelineWindow.locked = _timelineWasLocked;
                _timelineWindow = null;
            }
            if (_director != null)
            {
                _director.Stop();
                Object.DestroyImmediate(_director.gameObject);
                _director = null;
                SceneView.RepaintAll();
            }
        }

        [MenuItem("CK/Slash Follow Timeline", true)]
        private static bool CanOpen()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                Close();
            }
        }

        private static Transform CreateMarker(Transform rig, string name, Vector3 position)
        {
            var anchor = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            anchor.transform.SetParent(rig);
            anchor.transform.position = position;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.hideFlags = HideFlags.HideAndDontSave;
            marker.transform.SetParent(anchor.transform, false);
            marker.transform.localScale = new Vector3(0.45f, 0.88f, 0.45f);
            marker.transform.localPosition = Vector3.up * 0.88f;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            return anchor.transform;
        }

        private static AnimationTrack SyncPlayerAnimationTrack(TimelineAsset timeline, SkillSequenceAsset sequence)
        {
            double start = System.Math.Max(0d, sequence.ClipStart - sequence.ApproachStart);
            double clipIn = System.Math.Max(0d, sequence.ApproachStart - sequence.ClipStart);
            double motionEnd = System.Math.Min(sequence.Duration, sequence.ClipStart + sequence.CasterClip.length);
            double duration = System.Math.Max(1d / 60d, motionEnd - sequence.ApproachStart - start);
            var track = timeline.GetOutputTracks().OfType<AnimationTrack>()
                .FirstOrDefault(item => item.name == PlayerAnimationTrackName);
            if (track == null)
            {
                track = timeline.CreateTrack<AnimationTrack>(null, PlayerAnimationTrackName);
            }
            TimelineClip clip = track.GetClips().FirstOrDefault() ?? track.CreateClip(sequence.CasterClip);
            var playable = (AnimationPlayableAsset)clip.asset;
            if (playable.clip == sequence.CasterClip && clip.displayName == sequence.CasterClip.name
                && System.Math.Abs(clip.start - start) < 0.0001d
                && System.Math.Abs(clip.clipIn - clipIn) < 0.0001d
                && System.Math.Abs(clip.duration - duration) < 0.0001d
                && System.Math.Abs(clip.timeScale - 1d) < 0.0001d && track.locked
                && !playable.removeStartOffset && !playable.applyFootIK)
            {
                return track;
            }

            playable.clip = sequence.CasterClip;
            playable.removeStartOffset = false;
            playable.applyFootIK = false;
            clip.displayName = sequence.CasterClip.name;
            clip.start = start;
            clip.clipIn = clipIn;
            clip.duration = duration;
            clip.timeScale = 1d;
            track.locked = true;
            EditorUtility.SetDirty(playable);
            EditorUtility.SetDirty(track);
            EditorUtility.SetDirty(timeline);
            return track;
        }

        private static Animator CreatePreviewPlayer(Transform rig, Transform playerRoot, Animator source)
        {
            var player = new GameObject("Fuchsia Animation Preview") { hideFlags = HideFlags.HideAndDontSave };
            player.SetActive(false);
            _previewPlayer = player.transform;
            _previewPlayer.SetParent(rig);
            _previewPlayer.SetPositionAndRotation(_home, Quaternion.LookRotation(_forward));

            GameObject model = Object.Instantiate(source.gameObject, _previewPlayer, false);
            model.transform.localPosition = Quaternion.Inverse(playerRoot.rotation) * (source.transform.position - playerRoot.position);
            model.transform.localRotation = Quaternion.Inverse(playerRoot.rotation) * source.transform.rotation;
            model.transform.localScale = source.transform.lossyScale;
            var animator = model.GetComponent<Animator>();
            foreach (Behaviour behaviour in model.GetComponentsInChildren<Behaviour>(true))
            {
                behaviour.enabled = behaviour == animator;
            }
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            player.SetActive(true);
            return animator;
        }

        private static void UpdatePreview()
        {
            if (_director == null || TimelineEditor.inspectedDirector != _director)
            {
                Close();
                return;
            }
            var frame = _framing.Frame;
            float skillTime = _sequence.ApproachStart + (float)_director.time;
            _movementSampler.Sample(skillTime);
            bool inCameraShot = frame != null && _director.time < _cameraEnd;
            Pose pose = inCameraShot
                ? frame.GetPose(_movementSampler.EvaluateCasterPosition(Mathf.Min(skillTime, _sequence.ReturnStart)),
                    _target, _forward, _openingPose)
                : _openingPose;
            _shot.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var lens = _lens;
            if (inCameraShot)
            {
                lens.FieldOfView = frame.GetFieldOfView(_lens.FieldOfView);
            }
            _shot.Lens = lens;
            if (inCameraShot && _hitFeel.EvaluateShake((float)_director.time, out Vector3 shakePosition, out Vector3 shakeRotation))
            {
                pose = new Pose(pose.position + pose.rotation * shakePosition, pose.rotation * Quaternion.Euler(shakeRotation));
                _shot.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
            _camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _camera.fieldOfView = lens.FieldOfView;
            _brain.ManualUpdate();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
