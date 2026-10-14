using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    internal sealed class PlayerRunState : PlayerMovementState
    {
        public override string Name => "Run";
        public override bool IsSprinting => true;

        public override PlayerMovementState GetNextState(PlayerMovement player, Vector2 input,
            bool sprintRequested)
        {
            if (!HasMovementInput(input))
            {
                return player.IdleState;
            }

            return sprintRequested ? this : player.WalkState;
        }

        public override void Move(PlayerMovement player, Vector2 input)
        {
            player.MoveCharacter(input, player.RunSpeed);
        }
    }
}
