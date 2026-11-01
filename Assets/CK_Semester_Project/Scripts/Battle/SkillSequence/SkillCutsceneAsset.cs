using UnityEngine;
using UnityEngine.Playables;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 카메라 무빙이 필요한 스킬 컷신(필살기 등). 이펙터가 만든 Unity Timeline을 그대로 재생한다.
    /// 프리팹 안에 PlayableDirector(타임라인)와 시네머신 가상 카메라·컷신용 이펙트를 함께 넣는다.
    /// 스킬 연출 스튜디오는 이 컷신을 미리보지 않으며, 편집은 Timeline 창에서 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CK/Skill Cutscene", fileName = "SkillCutscene")]
    public sealed class SkillCutsceneAsset : ScriptableObject
    {
        [SerializeField, Tooltip("PlayableDirector(타임라인)가 들어 있는 컷신 프리팹")]
        private GameObject _prefab;
        [SerializeField, Tooltip("켜면 시전자 위치·방향을 기준으로 프리팹을 놓습니다. 끄면 프리팹에 저장된 월드 위치 그대로 둡니다.")]
        private bool _alignToCaster = true;
        [SerializeField, Tooltip("이 이름의 타임라인 트랙은 재생할 때 시전자에 자동 연결됩니다(Animation 트랙이면 시전자의 Animator).")]
        private string _casterTrackName = "Caster";
        [SerializeField, Tooltip("이 이름의 타임라인 트랙은 재생할 때 대상(맞는 쪽)에 자동 연결됩니다.")]
        private string _targetTrackName = "Target";
        [SerializeField, Tooltip("컷신이 끝나면 생성한 프리팹을 지웁니다.")]
        private bool _destroyOnFinish = true;

        public GameObject Prefab => _prefab;
        public bool AlignToCaster => _alignToCaster;
        public string CasterTrackName => _casterTrackName;
        public string TargetTrackName => _targetTrackName;
        public bool DestroyOnFinish => _destroyOnFinish;

        /// <summary>타임라인 길이(초). 프리팹이나 타임라인이 없으면 0.</summary>
        public float Duration
        {
            get
            {
                PlayableDirector director = _prefab != null ? _prefab.GetComponentInChildren<PlayableDirector>(true) : null;
                return director != null && director.playableAsset != null ? (float)director.playableAsset.duration : 0f;
            }
        }
    }
}
