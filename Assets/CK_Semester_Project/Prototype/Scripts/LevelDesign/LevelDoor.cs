using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 문 개폐. 문짝이 따로 있으면 위로 들어 올리고, 더미 메시처럼 문틀과 합쳐진 문은 바닥으로 내린 뒤 숨긴다.
    /// 닫힌 동안 NavMeshObstacle로 적 경로를 막는다.
    /// </summary>
    public sealed class LevelDoor : MonoBehaviour
    {
        [SerializeField, Tooltip("열릴 때 위로 들어 올릴 문짝(선택)")]
        private Transform _panel;
        [SerializeField, Min(0f), Tooltip("문짝을 들어 올리는 높이(m)")]
        private float _panelLift = 2.1f;
        [SerializeField, Tooltip("열릴 때 바닥으로 내린 뒤 숨길 더미 문 메시(선택)")]
        private Transform[] _sinkingParts = Array.Empty<Transform>();
        [SerializeField, Min(0f), Tooltip("더미 문 메시를 내리는 깊이(m)")]
        private float _sinkDepth = 5.2f;
        [SerializeField, Min(0.01f), Tooltip("개폐 시간(초). 기획서 출입문 1.0초")]
        private float _openSeconds = 1f;
        [SerializeField, Tooltip("닫힌 동안 적 경로를 막는 장애물(선택)")]
        private NavMeshObstacle _obstacle;
        [SerializeField, Tooltip("열린 뒤 끌 충돌체(선택). 문틀 상단이 낮으면 CharacterController가 바닥 이음매에서 계단 오르기를 시도하다 상단에 막히므로 꺼 둡니다.")]
        private Collider[] _collidersDisabledWhenOpen = Array.Empty<Collider>();
        [SerializeField, Range(0.3f, 1f), Tooltip("열리는 동안 이 진행도(0~1)부터 문짝·상단 충돌을 꺼서 지나갈 수 있게 합니다. 1이면 다 열린 뒤에 끕니다.")]
        private float _passableProgress = 0.8f;

        private Vector3 _panelClosedPosition;
        private Vector3[] _sinkingClosedPositions = Array.Empty<Vector3>();
        private Coroutine _routine;

        public event Action Opened;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_panel != null)
            {
                _panelClosedPosition = _panel.position;
            }
            _sinkingClosedPositions = new Vector3[_sinkingParts.Length];
            for (int i = 0; i < _sinkingParts.Length; i++)
            {
                _sinkingClosedPositions[i] = _sinkingParts[i] != null ? _sinkingParts[i].position : Vector3.zero;
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            IsOpen = true;
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(OpenRoutine());
        }

        /// <summary>엘리베이터 출발처럼 즉시 닫아야 할 때 사용한다.</summary>
        public void CloseImmediately()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            IsOpen = false;
            SetProgress(0f);
            SetPartsActive(true);
            SetExtraCollidersEnabled(true);
            if (_obstacle != null)
            {
                _obstacle.enabled = true;
            }
        }

        private IEnumerator OpenRoutine()
        {
            bool isPassable = false;
            for (float elapsed = 0f; elapsed < _openSeconds; elapsed += Time.deltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / _openSeconds);
                SetProgress(progress);
                // 문짝이 머리 위로 거의 올라갔는데도 끝까지 막혀 '열렸는데 걸리는' 느낌이 나지 않도록 미리 통과시킨다.
                if (!isPassable && progress >= _passableProgress)
                {
                    isPassable = true;
                    SetBlockingCollidersEnabled(false);
                }
                yield return null;
            }
            SetProgress(1f);
            SetPartsActive(false);
            SetBlockingCollidersEnabled(false);
            if (_obstacle != null)
            {
                _obstacle.enabled = false;
            }
            _routine = null;
            Opened?.Invoke();
        }

        private void SetProgress(float progress)
        {
            if (_panel != null)
            {
                _panel.position = _panelClosedPosition + Vector3.up * (_panelLift * progress);
            }
            for (int i = 0; i < _sinkingParts.Length; i++)
            {
                if (_sinkingParts[i] != null)
                {
                    _sinkingParts[i].position = _sinkingClosedPositions[i] + Vector3.down * (_sinkDepth * progress);
                }
            }
        }

        private void SetPartsActive(bool isActive)
        {
            foreach (Transform part in _sinkingParts)
            {
                if (part != null)
                {
                    part.gameObject.SetActive(isActive);
                }
            }
            if (_panel != null)
            {
                foreach (Collider panelCollider in _panel.GetComponentsInChildren<Collider>(true))
                {
                    panelCollider.enabled = isActive;
                }
            }
        }

        private void SetBlockingCollidersEnabled(bool isEnabled)
        {
            if (_panel != null)
            {
                foreach (Collider panelCollider in _panel.GetComponentsInChildren<Collider>(true))
                {
                    panelCollider.enabled = isEnabled;
                }
            }
            SetExtraCollidersEnabled(isEnabled);
        }

        private void SetExtraCollidersEnabled(bool isEnabled)
        {
            foreach (Collider extraCollider in _collidersDisabledWhenOpen)
            {
                if (extraCollider != null)
                {
                    extraCollider.enabled = isEnabled;
                }
            }
        }
    }
}
