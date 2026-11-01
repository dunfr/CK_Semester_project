using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 스킬 컷신 재생기. SkillCutscenePlayer.Play(컷신, 시전자, 대상)로 컷신 프리팹을 만들고 타임라인을 재생한다.
    /// - 트랙 이름이 Caster / Target(SkillCutsceneAsset에서 변경 가능)인 트랙은 시전자·대상에 자동 연결한다.
    /// - Cinemachine 트랙이 비어 있으면 메인 카메라의 CinemachineBrain에 자동 연결한다.
    /// - 끝나면 Finished 이벤트를 알리고(설정 시) 프리팹을 지운다.
    /// </summary>
    public sealed class SkillCutscenePlayer : MonoBehaviour
    {
        private PlayableDirector _director;
        private bool _destroyOnFinish;
        private bool _hasFinished;

        public event Action Finished;

        public PlayableDirector Director => _director;
        public bool IsPlaying => _director != null && !_hasFinished;

        /// <summary>컷신을 재생한다. 컷신·프리팹·타임라인이 없으면 null을 돌려준다.</summary>
        public static SkillCutscenePlayer Play(SkillCutsceneAsset cutscene, Transform caster, Transform target)
        {
            if (cutscene == null || cutscene.Prefab == null)
            {
                return null;
            }
            bool align = cutscene.AlignToCaster && caster != null;
            GameObject instance = align
                ? Instantiate(cutscene.Prefab, caster.position, Quaternion.Euler(0f, caster.eulerAngles.y, 0f))
                : Instantiate(cutscene.Prefab);
            PlayableDirector director = instance.GetComponentInChildren<PlayableDirector>(true);
            if (director == null || director.playableAsset == null)
            {
                Debug.LogWarning("SkillCutscenePlayer: 컷신 프리팹에 PlayableDirector(타임라인)가 없습니다. " + cutscene.name, cutscene);
                Destroy(instance);
                return null;
            }

            BindTracks(director, cutscene, caster, target);
            var player = instance.AddComponent<SkillCutscenePlayer>();
            player._director = director;
            player._destroyOnFinish = cutscene.DestroyOnFinish;
            director.extrapolationMode = DirectorWrapMode.None;
            director.stopped += player.OnDirectorStopped;
            director.time = 0d;
            director.Play();
            return player;
        }

        /// <summary>재생 중인 컷신을 바로 끝낸다(스킵 등).</summary>
        public void Stop()
        {
            if (_director != null)
            {
                _director.Stop();
            }
        }

        private void OnDestroy()
        {
            if (_director != null)
            {
                _director.stopped -= OnDirectorStopped;
            }
            Finish();
        }

        private void OnDirectorStopped(PlayableDirector director)
        {
            Finish();
            if (_destroyOnFinish && this != null)
            {
                Destroy(gameObject);
            }
        }

        private void Finish()
        {
            if (_hasFinished)
            {
                return;
            }
            _hasFinished = true;
            Finished?.Invoke();
        }

        private static void BindTracks(PlayableDirector director, SkillCutsceneAsset cutscene, Transform caster, Transform target)
        {
            CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            foreach (PlayableBinding binding in director.playableAsset.outputs)
            {
                if (binding.sourceObject == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(cutscene.CasterTrackName) && binding.streamName == cutscene.CasterTrackName)
                {
                    director.SetGenericBinding(binding.sourceObject, Resolve(binding.outputTargetType, caster));
                }
                else if (!string.IsNullOrEmpty(cutscene.TargetTrackName) && binding.streamName == cutscene.TargetTrackName)
                {
                    director.SetGenericBinding(binding.sourceObject, Resolve(binding.outputTargetType, target));
                }
                else if (binding.outputTargetType == typeof(CinemachineBrain) && brain != null
                    && director.GetGenericBinding(binding.sourceObject) == null)
                {
                    director.SetGenericBinding(binding.sourceObject, brain);
                }
            }
        }

        // 트랙이 원하는 종류(Animator, GameObject 등)에 맞춰 연결할 대상을 찾는다.
        private static UnityEngine.Object Resolve(Type outputType, Transform owner)
        {
            if (owner == null)
            {
                return null;
            }
            if (outputType == null || outputType == typeof(GameObject))
            {
                return owner.gameObject;
            }
            if (typeof(Component).IsAssignableFrom(outputType))
            {
                return owner.GetComponentInChildren(outputType, true);
            }
            return owner.gameObject;
        }
    }
}
