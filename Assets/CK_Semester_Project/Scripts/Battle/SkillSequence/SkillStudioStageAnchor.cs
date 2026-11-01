using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 스킬 연출 스튜디오가 쓸 씬 무대 설정. 별도 무대를 만들지 않고 씬에 있는 플레이어·몬스터를 그대로 지정한다.
    /// 스튜디오는 이 오브젝트 위치를 무대 중심(이펙트 기준점 '무대 중심')으로 쓴다.
    /// 미리보기 중 바뀐 플레이어·몬스터 포즈는 스튜디오가 닫히거나 씬을 저장할 때 원래대로 돌아간다.
    /// </summary>
    public sealed class SkillStudioStageAnchor : MonoBehaviour
    {
        [SerializeField, Tooltip("씬의 플레이어. 아군 스킬에서는 시전자, 적 스킬에서는 대상입니다. 자식에 Animator가 있어야 애니메이션이 보입니다.")]
        private Transform _player;
        [SerializeField, Tooltip("씬의 몬스터(더미)들. 위에서부터 슬롯 1, 2, 3… 순서입니다.")]
        private List<Transform> _monsterSlots = new List<Transform>();

        public Transform Player => _player;
        public IReadOnlyList<Transform> MonsterSlots => _monsterSlots;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            if (_player == null)
            {
                return;
            }
            foreach (Transform slot in _monsterSlots)
            {
                if (slot != null)
                {
                    Gizmos.DrawLine(_player.position + Vector3.up * 0.05f, slot.position + Vector3.up * 0.05f);
                }
            }
        }
    }
}
