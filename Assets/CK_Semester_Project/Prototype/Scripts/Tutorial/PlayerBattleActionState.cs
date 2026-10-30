using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 스킬 행동 모션(궁극기 등) 하나를 끝까지 재생하고 전투 대기로 돌아간다.
    internal sealed class PlayerBattleActionState : PlayerBattleState
    {
        private float _elapsed;

        public PlayerBattleActionState(string animatorState, Transform cinematicCamera = null,
            Transform cinematicCameraTarget = null, float cinematicFieldOfView = 0f)
            : base(animatorState)
        {
            CinematicCamera = cinematicCamera;
            CinematicCameraTarget = cinematicCameraTarget;
            CinematicFieldOfView = cinematicFieldOfView;
        }

        public override string Name => AnimatorState;

        // 모션이 움직이는 연출 카메라 노드(궁극기의 Ult_cam)·그 타깃(Ult_cam.Target)과 가로 시야각. 연출 카메라가 없으면 null.
        public Transform CinematicCamera { get; }
        public Transform CinematicCameraTarget { get; }
        public float CinematicFieldOfView { get; }

        // 모션 길이(초). 전투 연출은 이 시간만큼 기다린다.
        public float Length { get; private set; }

        public override void Enter(PlayerBattleAnimation owner)
        {
            _elapsed = 0f;
            Length = owner.CrossFade(AnimatorState);
        }

        public override void Tick(PlayerBattleAnimation owner, float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed >= Length)
            {
                owner.ChangeState(owner.IdleState);
            }
        }
    }
}
