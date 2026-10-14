using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    internal sealed class PlayerWalkState : PlayerMovementState
    {
        public override string Name => "Walk";

        public override PlayerMovementState GetNextState(PlayerMovement player, Vector2 input,
            bool sprintRequested)
        {
            if (!HasMovementInput(input))
            {
                return player.IdleState;
            }

            return sprintRequested ? player.RunState : this;
        }

        public override void Move(PlayerMovement player, Vector2 input)
        {
            player.MoveCharacter(input, player.WalkSpeed);
        }
    }
}
