using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    internal sealed class PlayerIdleState : PlayerMovementState
    {
        public override string Name => "Idle";

        public override PlayerMovementState GetNextState(PlayerMovement player, Vector2 input,
            bool sprintRequested)
        {
            if (!HasMovementInput(input))
            {
                return this;
            }

            return sprintRequested ? player.RunState : player.WalkState;
        }

        public override void Move(PlayerMovement player, Vector2 input)
        {
            player.MoveCharacter(Vector2.zero, 0f);
        }
    }
}
