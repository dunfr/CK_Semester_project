using System;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 플레이어가 Walk 상태인 동안 걷기 사운드를 일정 간격으로 출력한다.
    // - 등록된 사운드 중 하나를 랜덤으로 고르되, 직전에 출력한 사운드는 제외한다.
    // - 출력 간격(초)은 기획에서 인스펙터로 자유롭게 조절한다.
    public sealed class PlayerWalkSound : MonoBehaviour
    {
        private const string WalkStateName = "Walk";

        [SerializeField, Tooltip("상태를 확인할 플레이어 이동 컴포넌트")]
        private PlayerMovement _movement;
        [SerializeField, Tooltip("걷기 사운드를 출력할 AudioSource")]
        private AudioSource _source;
        [SerializeField, Tooltip("걷기 사운드 목록(a, b, c, d …). 이 중에서 랜덤으로 출력하며 직전 사운드는 제외합니다.")]
        private AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField, Min(0.05f), Tooltip("걷기 사운드 출력 간격(초). 기획 조정값")]
        private float _interval = 0.5f;
        [SerializeField, Min(0f), Tooltip("Walk 상태에 들어간 뒤 첫 사운드까지의 시간(초). 0이면 들어가자마자 출력")]
        private float _firstStepDelay = 0f;
        [SerializeField, Range(0f, 1f), Tooltip("걷기 사운드 볼륨")]
        private float _volume = 1f;

        private bool _wasWalking;
        private float _timer;
        private int _lastIndex = -1;

        // 테스트·디버그용: 마지막으로 출력한 사운드 번호(_clips 기준)와 출력 알림
        public int LastPlayedIndex => _lastIndex;
        public float Interval
        {
            get => _interval;
            set => _interval = Mathf.Max(0.05f, value);
        }
        public event Action<int> Played;

        private void Reset()
        {
            _movement = FindFirstObjectByType<PlayerMovement>();
            _source = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
            }
            if (_movement == null)
            {
                _movement = FindFirstObjectByType<PlayerMovement>();
            }
        }

        private void OnDisable()
        {
            _wasWalking = false;
        }

        private void Update()
        {
            bool isWalking = _movement != null && _movement.isActiveAndEnabled
                && _movement.CurrentMovementStateName == WalkStateName;
            if (!isWalking)
            {
                _wasWalking = false;
                return;
            }
            if (!_wasWalking)
            {
                _wasWalking = true;
                _timer = _firstStepDelay;
            }
            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }
            PlayNext();
            // 프레임 오차가 쌓이지 않게 남은 시간을 이어 쓰되, 멈춤 뒤 몰아서 출력하지는 않는다.
            _timer = Mathf.Max(_timer + _interval, 0f);
        }

        // 직전 사운드를 제외하고 랜덤으로 하나를 출력한다.
        public void PlayNext()
        {
            int index = PickIndex();
            if (index < 0 || _source == null)
            {
                return;
            }
            _source.PlayOneShot(_clips[index], _volume);
            _lastIndex = index;
            Played?.Invoke(index);
        }

        private int PickIndex()
        {
            int count = 0;
            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i] != null && i != _lastIndex)
                {
                    count++;
                }
            }
            if (count == 0)
            {
                // 사운드가 하나뿐이면 같은 사운드를 반복한다.
                return _lastIndex >= 0 && _lastIndex < _clips.Length && _clips[_lastIndex] != null ? _lastIndex : -1;
            }
            int pick = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i] == null || i == _lastIndex)
                {
                    continue;
                }
                if (pick-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
