using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    internal sealed class PlayerIdleState : PlayerGroundedState
    {
        public override string Name => "Idle";

        public override void Tick(PlayerMovement player, in PlayerMovementInput input)
        {
            player.MoveCharacter(Vector2.zero, 0f);
        }
    }
}
