using System;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    public enum CutsceneStepType
    {
        [InspectorName("대사")] Dialogue,
        [InspectorName("캐릭터 이동")] Move,
        [InspectorName("카메라")] Camera,
        [InspectorName("대기")] Wait
    }

    public enum CutsceneStepStart
    {
        [InspectorName("앞 단계가 끝난 뒤")] AfterPrevious,
        [InspectorName("앞 단계와 동시에")] WithPrevious
    }

    /// <summary>컷씬 한 단계. 종류(대사·이동·카메라·대기)에 따라 쓰는 항목이 다르다.</summary>
    [Serializable]
    public sealed class CutsceneStep
    {
        [SerializeField, Tooltip("단계 종류")]
        private CutsceneStepType _type = CutsceneStepType.Dialogue;
        [SerializeField, Tooltip("시작 시점. '동시에'면 바로 앞 단계와 함께 시작하고, 묶인 단계가 모두 끝나야 다음으로 넘어갑니다.")]
        private CutsceneStepStart _start = CutsceneStepStart.AfterPrevious;
        [SerializeField, Min(0f), Tooltip("시작 전 대기 시간(초)")]
        private float _delay;

        [SerializeField, Tooltip("말하는 캐릭터 이름")]
        private string _speaker = "푸시아";
        [SerializeField, TextArea(2, 4), Tooltip("대사 내용")]
        private string _text = "";
        [SerializeField, Min(0f), Tooltip("표시 시간(초). 0이면 F·Space·Enter를 누를 때까지 기다립니다.")]
        private float _duration;

        [SerializeField, Tooltip("움직일 캐릭터. 비우면 플레이어입니다.")]
        private Transform _actor;
        [SerializeField, Tooltip("도착 지점(위치와 도착 후 바라볼 방향)")]
        private Transform _destination;
        [SerializeField, Min(0.1f), Tooltip("이동 속도(m/s)")]
        private float _moveSpeed = 2f;
        [SerializeField, Tooltip("도착 후 도착 지점의 방향으로 몸을 돌립니다.")]
        private bool _matchDestinationRotation = true;

        [SerializeField, Tooltip("카메라 위치·각도. 여러 개면 순서대로 경로를 따라 이동합니다.")]
        private Transform[] _shots = Array.Empty<Transform>();
        [SerializeField, Min(0f), Tooltip("샷 사이를 이동하는 시간(초, 구간마다)")]
        private float _travelSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("마지막 샷에 도착한 뒤 머무는 시간(초)")]
        private float _holdSeconds = 1f;
        [SerializeField, Tooltip("지정하면 이동하는 동안 이 대상을 계속 바라봅니다(선택).")]
        private Transform _lookAt;
        [SerializeField, Tooltip("이동을 부드럽게 시작·정지합니다.")]
        private bool _easeInOut = true;

        [SerializeField, Min(0f), Tooltip("대기 시간(초)")]
        private float _waitSeconds = 1f;

        public CutsceneStepType Type => _type;
        public CutsceneStepStart Start => _start;
        public float Delay => _delay;
        public string Speaker => _speaker;
        public string Text => _text;
        public float Duration => _duration;
        public Transform Actor => _actor;
        public Transform Destination => _destination;
        public float MoveSpeed => _moveSpeed;
        public bool MatchDestinationRotation => _matchDestinationRotation;
        public Transform[] Shots => _shots;
        public float TravelSeconds => _travelSeconds;
        public float HoldSeconds => _holdSeconds;
        public Transform LookAt => _lookAt;
        public bool EaseInOut => _easeInOut;
        public float WaitSeconds => _waitSeconds;
    }
}
