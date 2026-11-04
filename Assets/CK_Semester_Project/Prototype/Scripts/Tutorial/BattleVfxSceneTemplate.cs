using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("CK/이펙트 배치 저장")]
    public sealed class BattleVfxSceneTemplate : MonoBehaviour
    {
        [SerializeField, HideInInspector] private GameObject _savedPrefab;

        public GameObject SavedPrefab => _savedPrefab;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void SetSavedPrefab(GameObject prefab)
        {
            _savedPrefab = prefab;
        }
    }
}
