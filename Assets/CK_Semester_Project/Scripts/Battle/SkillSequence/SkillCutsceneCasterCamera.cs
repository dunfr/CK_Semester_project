using Unity.Cinemachine;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 컷신 프리팹의 Cinemachine 카메라를 시전자 모델 안의 연출 카메라 노드(예: 궁극기 모션의 Ult_cam)에 붙인다.
    /// 컷신 프리팹은 시전자를 미리 알 수 없으므로, SkillCutscenePlayer가 재생 직전에 Bind를 호출한다.
    /// </summary>
    public sealed class SkillCutsceneCasterCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("시전자 노드를 따라갈 Cinemachine 카메라. 비워 두면 같은 오브젝트에서 찾습니다")]
        private CinemachineCamera _camera;
        [SerializeField, Tooltip("카메라 위치로 쓸 시전자 모델 안의 노드 이름")]
        private string _followNodeName = "Ult_cam";
        [SerializeField, Tooltip("카메라가 바라볼 시전자 모델 안의 노드 이름")]
        private string _lookAtNodeName = "Ult_cam.Target";

        /// <summary>시전자 모델에서 노드를 찾아 카메라의 Follow·LookAt에 연결한다. 못 찾으면 경고를 남긴다.</summary>
        public void Bind(Transform caster)
        {
            if (_camera == null)
            {
                _camera = GetComponent<CinemachineCamera>();
            }
            if (_camera == null || caster == null)
            {
                return;
            }
            Transform follow = FindDeep(caster, _followNodeName);
            Transform lookAt = FindDeep(caster, _lookAtNodeName);
            if (follow == null || lookAt == null)
            {
                Debug.LogWarning($"SkillCutsceneCasterCamera: {caster.name}에서 '{_followNodeName}' 또는 '{_lookAtNodeName}' 노드를 찾지 못했습니다.", this);
                return;
            }
            _camera.Follow = follow;
            _camera.LookAt = lookAt;
        }

        private static Transform FindDeep(Transform root, string nodeName)
        {
            if (root.name == nodeName)
            {
                return root;
            }
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, nodeName);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
