using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    internal abstract class PlayerMovementState
    {
        public abstract string Name { get; }
        public virtual bool IsSprinting => false;

        public abstract PlayerMovementState GetNextState(PlayerMovement player, Vector2 input,
            bool sprintRequested);

        public abstract void Move(PlayerMovement player, Vector2 input);

        protected static bool HasMovementInput(Vector2 input) => input.sqrMagnitude > 0.001f;
    }
}
