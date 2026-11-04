using System;
using CK.SemesterProject.Tutorial;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 레벨 프리셋(적 이동·컷씬)이 쓰는 플레이어 감지 영역. 박스 콜라이더의 위치·크기를 씬에서 조절하면 된다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PresetTriggerZone : MonoBehaviour
    {
        // 플레이어 몸통 높이(발 기준)와 영역 경계 여유(m)
        private const float BodyHeight = 0.9f;
        private const float ExitMargin = 0.3f;

        [SerializeField, Tooltip("씬 뷰에서 영역을 표시할 색")]
        private Color _gizmoColor = new Color(1f, 0.6f, 0.1f, 1f);

        private BoxCollider _box;
        private Transform _player;

        public event Action<Transform> PlayerEntered;
        public event Action<Transform> PlayerExited;

        public bool IsPlayerInside => _player != null;
        public Transform Player => _player;
        public BoxCollider Box => _box != null ? _box : _box = GetComponent<BoxCollider>();

        private void Reset()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 3f, 4f);
            box.center = new Vector3(0f, 1.5f, 0f);
        }

        private void Awake()
        {
            Box.isTrigger = true;
        }

        private void OnDisable()
        {
            _player = null;
        }

        // 전투 장소 이동 등으로 충돌체를 끈 채 순간이동하면 OnTriggerExit가 오지 않으므로 직접 영역 밖인지 확인한다.
        private void Update()
        {
            if (_player != null && !Contains(_player.position + Vector3.up * BodyHeight))
            {
                Transform player = _player;
                _player = null;
                PlayerExited?.Invoke(player);
            }
        }

        public bool Contains(Vector3 worldPoint)
        {
            BoxCollider box = Box;
            Vector3 local = transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 lossy = transform.lossyScale;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) * Mathf.Abs(lossy.x) <= half.x * Mathf.Abs(lossy.x) + ExitMargin
                && Mathf.Abs(local.y) * Mathf.Abs(lossy.y) <= half.y * Mathf.Abs(lossy.y) + ExitMargin
                && Mathf.Abs(local.z) * Mathf.Abs(lossy.z) <= half.z * Mathf.Abs(lossy.z) + ExitMargin;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player == null || _player != null)
            {
                return;
            }
            _player = player.transform;
            PlayerEntered?.Invoke(_player);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player == null || player.transform != _player)
            {
                return;
            }
            _player = null;
            PlayerExited?.Invoke(player.transform);
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = Box;
            if (box == null)
            {
                return;
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            Color fill = _gizmoColor;
            fill.a = 0.12f;
            Gizmos.color = fill;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = _gizmoColor;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
