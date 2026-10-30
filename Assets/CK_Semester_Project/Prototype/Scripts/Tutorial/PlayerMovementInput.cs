using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 한 프레임의 이동 입력. 상태들은 키보드를 직접 읽지 않고 이 값만 본다.
    // 새 입력이 생기면 여기에 값을 추가하고 PlayerMovement.Update에서 채운다.
    internal readonly struct PlayerMovementInput
    {
        public PlayerMovementInput(Vector2 move, bool sprintRequested, bool jumpRequested)
        {
            Move = move;
            SprintRequested = sprintRequested;
            JumpRequested = jumpRequested;
        }

        public Vector2 Move { get; }

        // Shift를 누르고 있거나 HUD 달리기 토글이 켜져 있다.
        public bool SprintRequested { get; }

        // 이번 프레임에 Space를 눌렀다. 누르고 있는 동안이 아니라 누른 순간에만 참이다.
        public bool JumpRequested { get; }

        public bool HasMove => Move.sqrMagnitude > 0.001f;
    }
}
