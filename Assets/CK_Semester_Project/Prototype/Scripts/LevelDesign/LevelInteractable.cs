using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>플레이어가 트리거 범위 안에서 F 키로 사용하는 레벨 오브젝트의 공통 기반.</summary>
    [RequireComponent(typeof(Collider))]
    public abstract class LevelInteractable : MonoBehaviour
    {
        [SerializeField, Tooltip("[F] 뒤에 표시할 안내 문구")]
        private string _prompt = "상호작용";
        [SerializeField] private LevelProgress _progress;

        public virtual string Prompt => _prompt;
        public virtual bool CanInteract => true;
        protected LevelProgress Progress => _progress;

        protected virtual void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (_progress == null)
            {
                Debug.LogError(name + ": LevelProgress 참조가 없습니다.", this);
                enabled = false;
            }
        }

        protected virtual void OnDisable()
        {
            if (_progress != null)
            {
                _progress.UnregisterInteractable(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (enabled && IsPlayer(other))
            {
                _progress.RegisterInteractable(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
            {
                _progress.UnregisterInteractable(this);
            }
        }

        public abstract void Interact();

        private bool IsPlayer(Collider other)
        {
            return _progress != null && _progress.Player != null && other.transform == _progress.Player;
        }
    }
}
